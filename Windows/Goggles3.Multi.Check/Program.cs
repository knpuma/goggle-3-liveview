using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Goggles3.Core;
using Goggles3.Multi;

// Developer-only executable: never included in the viewer package.
if (args.Length != 3) { Console.Error.WriteLine("Usage: check <ffmpeg> <four-h264-input-folder> <output-folder>"); return 2; }
if (args[1] == "--verify-recordings") return await VisualVerification.Run(args[0], args[2]);
Directory.CreateDirectory(args[2]);
var results = new System.Collections.Concurrent.ConcurrentBag<RecordingResult>();
var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
var mosaic = new Mosaic();
var sessions = new List<FfmpegVideoSession>();
var counts = new long[4];
var colors = new int[4][];
MosaicRecording? recording = null;
var timer = Stopwatch.StartNew();
bool udp = false;
bool finalization = false;
try
{
    // Exercise all four session state machines and actual Windows interface
    // pinning against separate loopback peers (not physical USB evidence).
    int index = OperatingSystem.IsWindows() ? NetworkInterface.GetAllNetworkInterfaces().Single(n => n.NetworkInterfaceType == NetworkInterfaceType.Loopback).GetIPProperties().GetIPv4Properties()!.Index : 0;
    await Task.WhenAll(Enumerable.Range(0, 4).Select(async slot =>
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var source = new UdpVideoReceiver(IPAddress.Loopback, (IPEndPoint)peer.Client.LocalEndPoint!, 0, index);
        var done = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = source.RunAsync((data, _) => { done.TrySetResult(data); return ValueTask.CompletedTask; }, timeout.Token);
        try
        {
            var hello = await peer.ReceiveAsync(timeout.Token);
            byte[] message = new byte[24];
            BinaryPrimitives.WriteUInt16LittleEndian(message, 0x8018);
            hello.Buffer.AsSpan(2, 2).CopyTo(message.AsSpan(2));
            message[6] = 2; message[7] = LiveProtocol.HeaderXor(message); message[17] = 1;
            message[20] = (byte)(slot + 1);
            await peer.SendAsync(message, hello.RemoteEndPoint, timeout.Token);
            if ((await done.Task.WaitAsync(timeout.Token))[0] != slot + 1) throw new IOException("Cross-channel UDP data.");
            var ack = await peer.ReceiveAsync(timeout.Token);
            if (ack.Buffer[6] != 4) throw new IOException("Missing acknowledgement.");
        }
        finally
        {
            timeout.Cancel();
            try { await running; } catch (OperationCanceledException) { }
        }
    }));
    udp = true;
    for (int slot = 0; slot < 4; slot++)
    {
        int current = slot; long generation = mosaic.Reset(slot);
        var session = new FfmpegVideoSession(args[0]); sessions.Add(session);
        session.Log += text => errors.Add(text);
        session.RecordingEnded += results.Add;
        session.Frame += pixels =>
        {
            mosaic.Update(current, generation, pixels);
            colors[current] ??= [pixels[0], pixels[1], pixels[2]];
            Interlocked.Increment(ref counts[current]);
        };
        session.StartRecording(Path.Combine(args[2], $"channel-{slot + 1}.mp4"));
        session.StartReplay(Path.Combine(args[1], $"channel-{slot + 1}.h264"));
    }
    while (counts.Any(c => Interlocked.Read(ref c) < 12))
    {
        if (timer.Elapsed.TotalSeconds > 60) throw new TimeoutException("Four decoders did not deliver frames.");
        await Task.Delay(100);
    }
    recording = new MosaicRecording(mosaic, args[0], Path.Combine(args[2], "mosaic.mp4"), results.Add);
    await Task.Delay(4000);
    var firstStop = recording.DisposeAsync().AsTask();
    await recording.DisposeAsync();
    if (!firstStop.IsCompleted) throw new IOException("Repeated mosaic stop returned before finalization.");
    recording = null;
}
catch (Exception error) { errors.Add(error.ToString()); }
finally
{
    if (recording != null) await recording.DisposeAsync();
    var saves = sessions.Select(session => session.StopRecordingAsync()).ToArray();
    foreach (var session in sessions) await session.DisposeAsync();
    finalization = saves.All(save => save.IsCompletedSuccessfully);
    await Task.WhenAll(saves);
}
bool ok = udp && finalization && counts.All(c => c >= 12) && results.Count == 5 && results.All(r => r.Frames > 0 && r.Error == null);
string report = JsonSerializer.Serialize(new { ok, udp, finalization, counts, colors, recordings = results, errors, seconds = timer.Elapsed.TotalSeconds }, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(Path.Combine(args[2], "result.json"), report);
Console.WriteLine(report);
return ok ? 0 : 1;
