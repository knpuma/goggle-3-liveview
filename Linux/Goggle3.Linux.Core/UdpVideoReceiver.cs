using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Goggles3.Core;

public sealed class UdpVideoReceiver
{
    private readonly IPEndPoint local, peer;
    private readonly int interfaceIndex;
    public event Action<double>? Bitrate;
    public event Action<string>? Log;
    public UdpVideoReceiver(IPAddress localAddress, IPEndPoint? remote = null, int localPort = 12346, int interfaceIndex = 0)
    {
        local = new(localAddress, localPort);
        peer = remote ?? new(IPAddress.Parse("192.168.60.2"), 9003);
        this.interfaceIndex = interfaceIndex;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setsockopt(int fd, int level, int name, ref int value, uint length);

    public async Task RunAsync(Func<byte[], CancellationToken, ValueTask> receiveVideo, CancellationToken token)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ExclusiveAddressUse = true;
        udp.Client.ReceiveBufferSize = 2 * 1024 * 1024;
        if (interfaceIndex > 0)
        {
            if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
            // Linux IP_UNICAST_IF (50) accepts a network-order interface index.
            // Unlike SO_BINDTODEVICE, this requires no elevated app privileges.
            int index = IPAddress.HostToNetworkOrder(interfaceIndex);
            if (setsockopt(udp.Client.Handle.ToInt32(), 0, 50, ref index, 4) != 0)
                throw new SocketException(Marshal.GetLastPInvokeError());
        }
        udp.Client.Bind(local);
        udp.Connect(peer); // Accept only packets from the expected goggles peer.
        var protocol = new LiveProtocol();
        byte[] handshake = protocol.CreateHandshake();
        var watch = Stopwatch.StartNew();
        double lastStats = 0, lastSubscribe = -1;
        long lastBytes = 0;
        bool started = false;
        while (!token.IsCancellationRequested)
        {
            double now = watch.Elapsed.TotalSeconds;
            if (now - lastStats >= 1)
            {
                Bitrate?.Invoke((protocol.VideoBytes - lastBytes) * 8 / (now - lastStats) / 1_000_000);
                lastStats = now; lastBytes = protocol.VideoBytes;
            }
            if (!started && now - lastSubscribe >= 1)
            {
                await udp.SendAsync(handshake, token).ConfigureAwait(false);
                lastSubscribe = now;
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(250);
            UdpReceiveResult datagram;
            try { datagram = await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { continue; }
            // Connected UDP sockets may report ICMP port-unreachable while the
            // goggles restart LiveView. Continue waiting rather than kill the UI.
            catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused) { continue; }
            PacketResult result = protocol.Accept(datagram.Buffer);
            if (result.HandshakeConfirmed) Log?.Invoke("LiveView session confirmed.");
            if (result.Acknowledgement != null)
                await udp.SendAsync(result.Acknowledgement, token).ConfigureAwait(false);
            if (result.Video != null)
            {
                started = true;
                await receiveVideo(result.Video, token).ConfigureAwait(false);
            }
        }
    }
}
