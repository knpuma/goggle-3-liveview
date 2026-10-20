using System.Diagnostics;
using System.Runtime.InteropServices;
using Goggles3.Core;

namespace Goggles3.Multi;

// Independent of WPF, visibility and fullscreen. Immutable decoded frames are
// retained by reference; each render takes one consistent snapshot per tile.
public sealed class Mosaic
{
    private sealed record Sample(byte[] Pixels, long Timestamp, long Generation);
    private readonly Sample?[] latest = new Sample?[4];
    private readonly long[] generations = new long[4];
    private readonly object gate = new();
    public long Reset(int slot)
    {
        lock (gate) { latest[slot] = null; return ++generations[slot]; }
    }
    public void Update(int slot, long generation, byte[] pixels, long? timestamp = null)
    {
        if (pixels.Length != FfmpegVideoSession.FrameSize) throw new ArgumentException("Expected 1920x1080 BGRA.", nameof(pixels));
        lock (gate)
            if (generation == generations[slot])
                latest[slot] = new Sample(pixels, timestamp ?? Stopwatch.GetTimestamp(), generation);
    }
    public bool HasFrames(long? now = null)
    {
        long tick = now ?? Stopwatch.GetTimestamp();
        lock (gate) return latest.Any(sample => sample != null && Fresh(sample.Timestamp, tick));
    }
    private static bool Fresh(long then, long now) => now >= then && (now - then) / (double)Stopwatch.Frequency < 3;
    public byte[] Render(long? now = null)
    {
        long tick = now ?? Stopwatch.GetTimestamp();
        Sample?[] snapshot;
        lock (gate) snapshot = (Sample?[])latest.Clone();
        byte[] canvas = new byte[FfmpegVideoSession.FrameSize];
        Span<uint> output = MemoryMarshal.Cast<byte, uint>(canvas.AsSpan());
        output.Fill(0xff000000);
        for (int slot = 0; slot < 4; slot++)
        {
            Sample? sample = snapshot[slot];
            if (sample == null || !Fresh(sample.Timestamp, tick)) continue;
            ReadOnlySpan<uint> input = MemoryMarshal.Cast<byte, uint>(sample.Pixels);
            int x0 = slot % 2 * 960, y0 = slot / 2 * 540;
            for (int y = 0; y < 540; y++)
                for (int x = 0; x < 960; x++)
                    output[(y + y0) * 1920 + x + x0] = input[y * 2 * 1920 + x * 2];
        }
        return canvas;
    }
}

public sealed class MosaicRecording : IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task worker;
    private readonly Mp4Recorder recorder;
    private readonly object disposeGate = new();
    private Task? shutdown;
    public MosaicRecording(Mosaic mosaic, string encoder, string path, Action<RecordingResult> finished)
    {
        recorder = new Mp4Recorder(encoder, path, 30, result =>
        {
            lifetime.Cancel();
            finished(result);
        });
        worker = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / 30));
            try
            {
                while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
                    recorder.Append(mosaic.Render());
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
    }
    public ValueTask DisposeAsync()
    {
        lock (disposeGate) return new ValueTask(shutdown ??= FinishAsync());
    }
    private async Task FinishAsync()
    {
        lifetime.Cancel();
        await worker.ConfigureAwait(false);
        await recorder.FinishAsync().ConfigureAwait(false);
        lifetime.Dispose();
    }
}
