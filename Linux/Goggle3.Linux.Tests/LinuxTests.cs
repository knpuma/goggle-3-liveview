using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Goggles3.Core;
using Xunit;

namespace Goggle3.Linux.Tests;

public sealed class LinuxTests
{
    [Theory]
    [InlineData("../../eth0")][InlineData("eth0;id")][InlineData("..")][InlineData("a name")]
    public void InvalidInterfaceNamesAreRejected(string name) => Assert.False(UsbNetwork.IsGoggles(name));

    [Fact]
    public void SysfsMustContainVerifiedDjiUsbAncestry()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            string usb = Path.Combine(root, "devices/usb1/1-2");
            string device = Path.Combine(usb, "1-2:1.0");
            string nic = Path.Combine(root, "class/net/enx123");
            Directory.CreateDirectory(device); Directory.CreateDirectory(nic);
            Directory.CreateSymbolicLink(Path.Combine(nic, "device"), device);
            File.WriteAllText(Path.Combine(usb, "idVendor"), "2ca3\n");
            File.WriteAllText(Path.Combine(usb, "idProduct"), "0020\n");
            Assert.True(UsbNetwork.IsGoggles("enx123", root));
            File.WriteAllText(Path.Combine(usb, "idVendor"), "1234");
            Assert.False(UsbNetwork.IsGoggles("enx123", root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LinuxSocketPinsUnprivilegedInterfaceAndFiltersOtherPeers()
    {
        if (!OperatingSystem.IsLinux()) return;
        int index = NetworkInterface.GetAllNetworkInterfaces().Single(n => n.Name == "lo").GetIPProperties().GetIPv4Properties()!.Index;
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var other = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var receiver = new UdpVideoReceiver(IPAddress.Loopback, (IPEndPoint)peer.Client.LocalEndPoint!, 0, index);
        int frames = 0;
        Task running = receiver.RunAsync((_, _) => { Interlocked.Increment(ref frames); return ValueTask.CompletedTask; }, stop.Token);
        var handshake = await peer.ReceiveAsync(stop.Token);
        byte[] packet = new byte[25];
        packet[0] = 25; packet[1] = 0x80; packet[2] = handshake.Buffer[2]; packet[3] = handshake.Buffer[3];
        packet[6] = 2; packet[17] = 1; packet[23] = 1; packet[24] = 0x65; packet[7] = LiveProtocol.HeaderXor(packet);
        await other.SendAsync(packet, handshake.RemoteEndPoint, stop.Token);
        await Task.Delay(200); Assert.Equal(0, Volatile.Read(ref frames));
        await peer.SendAsync(packet, handshake.RemoteEndPoint, stop.Token);
        var ack = await peer.ReceiveAsync(stop.Token); Assert.Equal((byte)4, ack.Buffer[6]);
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(1, frames);
    }

    [Fact]
    public async Task FfmpegDecodesRecordsAndFinalizesPlayableMp4()
    {
        string? fixture = Environment.GetEnvironmentVariable("GOGGLE_TEST_H264");
        if (fixture == null) { Assert.False(OperatingSystem.IsLinux(), "Linux CI must supply a generated H.264 fixture"); return; }
        string path = Path.Combine(Path.GetTempPath(), $"goggle-test-{Guid.NewGuid():N}.mp4");
        try
        {
            var recording = new TaskCompletionSource<RecordingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int frames = 0;
            await using (var session = new FfmpegVideoSession("/usr/bin/ffmpeg"))
            {
                session.Frame += bytes => { Assert.Equal(FfmpegVideoSession.FrameSize, bytes.Length); Interlocked.Increment(ref frames); };
                session.RecordingEnded += result => recording.TrySetResult(result);
                session.Ended += _ => ended.TrySetResult();
                session.StartRecording(path); session.StartReplay(fixture);
                await ended.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await session.StopRecordingAsync();
                var result = await recording.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Null(result.Error); Assert.True(result.Frames > 10); Assert.True(frames > 10);
            }
            var info = new ProcessStartInfo("/usr/bin/ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height,codec_name", "-of", "csv=p=0", path }) info.ArgumentList.Add(arg);
            using var probe = Process.Start(info)!;
            string output = await probe.StandardOutput.ReadToEndAsync(); await probe.WaitForExitAsync();
            Assert.Equal(0, probe.ExitCode); Assert.Contains("mpeg4,1920,1080", output);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DecoderWithoutIncomingVideoStopsPromptly()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var session = new FfmpegVideoSession("/usr/bin/ffmpeg");
        session.Start(new UdpVideoReceiver(IPAddress.Loopback, (IPEndPoint)peer.Client.LocalEndPoint!, 0));
        await Task.Delay(200);
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
