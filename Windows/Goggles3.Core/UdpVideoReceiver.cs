using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Goggles3.Core;

public sealed class UdpVideoReceiver
{
    private readonly IPEndPoint local;
    private readonly IPEndPoint peer;
    public event Action<double>? Bitrate;
    public event Action<string>? Log;
    public UdpVideoReceiver(IPAddress localAddress, IPEndPoint? remote = null, int localPort = 12346)
    {
        local = new IPEndPoint(localAddress, localPort);
        peer = remote ?? new IPEndPoint(IPAddress.Parse("192.168.60.2"), 9003);
    }

    public async Task RunAsync(Func<byte[], CancellationToken, ValueTask> receiveVideo, CancellationToken token)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ExclusiveAddressUse = true;
        udp.Client.ReceiveBufferSize = 2 * 1024 * 1024;
        udp.Client.Bind(local);
        // Binding to the exact DJI adapter prevents Wi-Fi/default-route selection.
        udp.Connect(peer);
        var protocol = new LiveProtocol();
        byte[] handshake = protocol.CreateHandshake();
        var watch = Stopwatch.StartNew();
        double lastStats = 0, lastSubscribe = -1;
        long lastBytes = 0;
        bool started = false;
        while (!token.IsCancellationRequested)
        {
            if (!started && watch.Elapsed.TotalSeconds - lastSubscribe >= 1)
            {
                await udp.SendAsync(handshake, token).ConfigureAwait(false);
                lastSubscribe = watch.Elapsed.TotalSeconds;
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(250);
            UdpReceiveResult datagram;
            try { datagram = await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { continue; }
            PacketResult result = protocol.Accept(datagram.Buffer);
            if (result.HandshakeConfirmed) Log?.Invoke("Sesión LiveView confirmada.");
            if (result.Acknowledgement != null)
                await udp.SendAsync(result.Acknowledgement, token).ConfigureAwait(false);
            if (result.Video != null)
            {
                started = true;
                await receiveVideo(result.Video, token).ConfigureAwait(false);
            }
            double now = watch.Elapsed.TotalSeconds;
            if (now - lastStats >= 1)
            {
                Bitrate?.Invoke((protocol.VideoBytes - lastBytes) * 8 / (now - lastStats) / 1_000_000);
                lastStats = now; lastBytes = protocol.VideoBytes;
            }
        }
    }
}
