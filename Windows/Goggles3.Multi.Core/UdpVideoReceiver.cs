using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Goggles3.Core;

// Each DJI USB adapter gets a distinct local IPv4 address. Both outgoing and
// incoming traffic are pinned to its interface, despite the identical peer IPs.
public sealed class UdpVideoReceiver
{
    private readonly IPEndPoint local;
    private readonly IPEndPoint peer;
    private readonly int interfaceIndex;
    public event Action<double>? Bitrate;
    public event Action<string>? Log;
    public UdpVideoReceiver(IPAddress localAddress, IPEndPoint? remote = null, int localPort = 12346, int interfaceIndex = 0)
    {
        local = new IPEndPoint(localAddress, localPort);
        peer = remote ?? new IPEndPoint(IPAddress.Parse("192.168.60.2"), 9003);
        this.interfaceIndex = interfaceIndex;
        if (interfaceIndex < 0) throw new ArgumentOutOfRangeException(nameof(interfaceIndex));
    }
    public async Task RunAsync(Func<byte[], CancellationToken, ValueTask> receiveVideo, CancellationToken token)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ExclusiveAddressUse = true;
        udp.Client.ReceiveBufferSize = 2 * 1024 * 1024;
        if (interfaceIndex != 0)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Interface isolation requires Windows.");
            // Microsoft IPPROTO_IP: UNICAST_IF uses network order; ADD_IFLIST
            // uses a native IF_INDEX. Fail closed if isolation is unavailable.
            udp.Client.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)31, IPAddress.HostToNetworkOrder(interfaceIndex)); // IP_UNICAST_IF
            udp.Client.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)28, 1); // IP_IFLIST
            udp.Client.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)29, interfaceIndex); // IP_ADD_IFLIST
        }
        udp.Client.Bind(local);
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
            try
            {
                var datagram = await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false);
                PacketResult result = protocol.Accept(datagram.Buffer);
                if (result.HandshakeConfirmed) Log?.Invoke("LiveView session confirmed.");
                if (result.Acknowledgement != null) await udp.SendAsync(result.Acknowledgement, token).ConfigureAwait(false);
                if (result.Video != null)
                {
                    started = true;
                    await receiveVideo(result.Video, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            double now = watch.Elapsed.TotalSeconds;
            if (now - lastStats >= 1)
            {
                Bitrate?.Invoke((protocol.VideoBytes - lastBytes) * 8 / (now - lastStats) / 1_000_000);
                lastStats = now; lastBytes = protocol.VideoBytes;
            }
        }
        token.ThrowIfCancellationRequested();
    }
}
