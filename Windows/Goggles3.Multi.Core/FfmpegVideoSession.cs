using System.Diagnostics;
using System.Threading.Channels;

namespace Goggles3.Core;

public sealed class FfmpegVideoSession : IAsyncDisposable
{
    public const int Width = 1920, Height = 1080, FrameSize = Width * Height * 4;
    private readonly string decoderPath;
    private readonly CancellationTokenSource lifetime = new();
    private Process? decoder;
    private Task? worker;
    private readonly object recordingLock = new();
    private Mp4Recorder? recorder;
    private Task recordingFinalization = Task.CompletedTask;
    public event Action<byte[]>? Frame;
    public event Action<double>? Bitrate;
    public event Action<string>? Log;
    public event Action<string>? Ended;
    public event Action<RecordingResult>? RecordingEnded;

    public FfmpegVideoSession(string decoderPath) => this.decoderPath = decoderPath;

    public void Start(UdpVideoReceiver receiver)
    {
        receiver.Bitrate += rate => Bitrate?.Invoke(rate);
        receiver.Log += text => Log?.Invoke(text);
        Begin((input, token) => receiver.RunAsync(async (data, ct) =>
        {
            await input.WriteAsync(data, ct).ConfigureAwait(false);
        }, token));
    }

    // Explicit local-file test mode, never passed off as a live USB session.
    public void StartReplay(string path) => Begin(async (input, token) =>
    {
        await using var file = File.OpenRead(path);
        await file.CopyToAsync(input, token).ConfigureAwait(false);
    }, paced: true);

    private void Begin(Func<Stream, CancellationToken, Task> source, bool paced = false)
    {
        if (worker != null) throw new InvalidOperationException("The session is already running.");
        decoder = CreateProcess(decoderPath, ["-hide_banner","-loglevel","error","-filter_threads","1","-flags2","+showall",
            "-threads","2","-probesize","65536","-analyzeduration","0","-f","h264","-r","60","-i","pipe:0",
            "-an","-pix_fmt","bgra","-vf","scale=1920:1080","-fps_mode","passthrough","-threads","1","-f","rawvideo","pipe:1"]);
        decoder.Start();
        Process active = decoder;
        worker = Task.Run(async () =>
        {
            var errors = DrainErrorsAsync(active, text => Log?.Invoke(text));
            Exception? sourceFailure = null;
            var writer = Task.Run(async () =>
            {
                try { await source(active.StandardInput.BaseStream, lifetime.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                catch (Exception error) { sourceFailure = error; Log?.Invoke("USB reception: " + error.Message); }
                finally { active.StandardInput.Close(); }
            });
            try
            {
                var watch = Stopwatch.StartNew(); long frames = 0;
                while (!lifetime.IsCancellationRequested)
                {
                    byte[] pixels = new byte[FrameSize];
                    int offset = 0;
                    while (offset < pixels.Length)
                    {
                        int read = await active.StandardOutput.BaseStream.ReadAsync(pixels.AsMemory(offset), lifetime.Token).ConfigureAwait(false);
                        if (read == 0) break;
                        offset += read;
                    }
                    if (offset != pixels.Length) break;
                    if (paced)
                    {
                        double delay = frames / 60.0 - watch.Elapsed.TotalSeconds;
                        if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay), lifetime.Token).ConfigureAwait(false);
                    }
                    frames++;
                    lock (recordingLock) recorder?.Append(pixels);
                    Frame?.Invoke(pixels);
                }
                await active.WaitForExitAsync(lifetime.Token).ConfigureAwait(false);
                if (sourceFailure != null) throw new IOException("USB reception: " + sourceFailure.Message, sourceFailure);
                if (active.ExitCode != 0) throw new IOException($"The decoder exited with code {active.ExitCode}.");
                Ended?.Invoke(paced ? "End of local test recording." : "The USB connection has closed.");
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception error) { Log?.Invoke(error.Message); Ended?.Invoke(error.Message); }
            finally
            {
                lifetime.Cancel();
                StopProcess(active);
                try { await writer.ConfigureAwait(false); }
                catch (Exception error) { if (error is not OperationCanceledException) Log?.Invoke(error.Message); }
                await errors.ConfigureAwait(false);
                await StopRecordingAsync().ConfigureAwait(false);
            }
        });
    }

    public void StartRecording(string path, int frameRate = 60)
    {
        lock (recordingLock)
        {
            if (recorder != null || !recordingFinalization.IsCompleted) throw new InvalidOperationException("A recording is already in progress or being saved.");
            Mp4Recorder? active = null;
            active = new Mp4Recorder(decoderPath, path, frameRate, result =>
            {
                lock (recordingLock) if (ReferenceEquals(recorder, active)) recorder = null;
                RecordingEnded?.Invoke(result);
            });
            recorder = active;
        }
    }

    public Task StopRecordingAsync()
    {
        lock (recordingLock)
        {
            Mp4Recorder? active = recorder; recorder = null;
            if (active != null) recordingFinalization = Task.WhenAll(recordingFinalization, active.FinishAsync());
            return recordingFinalization;
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (decoder != null) StopProcess(decoder);
        if (worker != null) await worker.ConfigureAwait(false);
        await StopRecordingAsync().ConfigureAwait(false);
        decoder?.Dispose(); lifetime.Dispose();
    }

    internal static Process CreateProcess(string path, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        return new Process { StartInfo = info };
    }

    internal static async Task DrainErrorsAsync(Process process, Action<string> log)
    {
        while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line) log(line);
    }

    internal static void StopProcess(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* Already exited before Kill. */ }
        catch (System.ComponentModel.Win32Exception) when (process.HasExited) { }
    }
}

public sealed record RecordingResult(string Path, long Frames, long Dropped, string? Error);

internal sealed class Mp4Recorder
{
    private readonly Process process;
    private readonly Channel<byte[]> pending = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4)
    { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task worker;
    private readonly CancellationTokenSource shutdown = new();
    private long dropped;
    public Mp4Recorder(string executable, string path, int fps, Action<RecordingResult> finished)
    {
        // Native LGPL MPEG-4 encoder: works without proprietary GPU drivers or
        // a GPL x264 dependency. MP4 remains playable; H.264 MF can be added later.
        // The bundled x64 build's SIMD BGRA-to-YUV path corrupts colors under
        // Windows ARM x64 emulation. Use the portable conversion/encoder path;
        // decoding and the live preview retain their normal CPU acceleration.
        process = FfmpegVideoSession.CreateProcess(executable, ["-hide_banner","-loglevel","error","-cpuflags","0",
            "-use_wallclock_as_timestamps","1","-f","rawvideo","-pixel_format","bgra","-video_size","1920x1080","-framerate",fps.ToString(),
            "-i","pipe:0","-an","-c:v","mpeg4","-threads","2","-filter_threads","1","-q:v","2","-g",fps.ToString(),
            "-pix_fmt","yuv420p","-fps_mode","vfr","-movflags","+faststart","-f","mp4",path]);
        process.Start();
        worker = Task.Run(async () =>
        {
            string? failure = null; long frames = 0;
            var errors = FfmpegVideoSession.DrainErrorsAsync(process, line => failure = line);
            try
            {
                await foreach (byte[] pixels in pending.Reader.ReadAllAsync(shutdown.Token).ConfigureAwait(false))
                { await process.StandardInput.BaseStream.WriteAsync(pixels, shutdown.Token).ConfigureAwait(false); frames++; }
                process.StandardInput.Close();
                await process.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);
                if (process.ExitCode == 0 && frames > 0) failure = null;
                else failure ??= "No frames were received or the encoder could not finalize the MP4.";
            }
            catch (Exception error)
            {
                failure = error.Message; pending.Writer.TryComplete(error);
                FfmpegVideoSession.StopProcess(process);
            }
            finally
            {
                await errors.ConfigureAwait(false);
                finished(new RecordingResult(path, frames, Interlocked.Read(ref dropped), failure));
                process.Dispose(); shutdown.Dispose();
            }
        });
    }
    public void Append(byte[] pixels) { if (!pending.Writer.TryWrite(pixels)) Interlocked.Increment(ref dropped); }
    public Task FinishAsync()
    {
        if (pending.Writer.TryComplete()) shutdown.CancelAfter(TimeSpan.FromSeconds(30));
        return worker;
    }
}
