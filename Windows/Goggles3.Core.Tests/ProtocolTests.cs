using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Goggles3.Core;
using Xunit;

namespace Goggles3.Core.Tests;

public sealed class ProtocolTests
{
    private const ushort Session = 0xdd3a;
    private static byte[] Packet(int part = 0, int count = 1, ushort sequence = 0x1000, byte[]? payload = null)
    {
        payload ??= [0,0,0,1,0x65];
        var bytes = new byte[20 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(bytes.Length | 0x8000));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2),Session);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4),sequence);
        bytes[6] = 2; bytes[7] = LiveProtocol.HeaderXor(bytes);
        bytes[17] = (byte)(count | ((part & 1) << 7));
        bytes[18] = (byte)(part >> 1); bytes[19] = (byte)(part >> 9);
        payload.CopyTo(bytes,20); return bytes;
    }

    [Fact] public void Handshake_MatchesMacReceiver_WhenSessionIsFixed()
    {
        byte[] expected = Convert.FromHexString("30803ADD00000057D0E964006400C0051400000A0064006400C005140000640014006400C00514000064000101040A02");
        Assert.Equal(expected, new LiveProtocol(Session).CreateHandshake());
    }
    [Fact] public void Accept_EmitsCompleteVideoAndAck_WhenSinglePartArrives()
    {
        var receiver = new LiveProtocol(Session); PacketResult result = receiver.Accept(Packet());
        Assert.Equal(new byte[] {0,0,0,1,0x65}, result.Video);
        Assert.NotNull(result.Acknowledgement);
        Assert.Equal((byte)4,result.Acknowledgement[6]);
        Assert.Equal(Session,BinaryPrimitives.ReadUInt16LittleEndian(result.Acknowledgement.AsSpan(2)));
        Assert.Equal((ushort)0x1000,BinaryPrimitives.ReadUInt16LittleEndian(result.Acknowledgement.AsSpan(8)));
        Assert.Equal(LiveProtocol.HeaderXor(result.Acknowledgement),result.Acknowledgement[7]);
        Assert.Equal(5,receiver.VideoBytes);
    }
    [Fact] public void Accept_ReordersPartsAndSuppressesDuplicates_WhenPacketsAreOutOfOrder()
    {
        var receiver = new LiveProtocol(Session);
        byte[] last = Packet(1,2,0x1008,[3,4]);
        Assert.Null(receiver.Accept(last).Video); Assert.Null(receiver.Accept(last).Video);
        Assert.Equal(new byte[] {1,2,3,4},receiver.Accept(Packet(0,2,0x1000,[1,2])).Video);
        Assert.Null(receiver.Accept(last).Video); Assert.Equal(4,receiver.VideoBytes);
    }
    [Fact] public void Accept_ReassemblesAcrossSequenceWrap_WhenUnitCrosses65535()
    {
        var receiver = new LiveProtocol(Session);
        Assert.Null(receiver.Accept(Packet(0,2,0xfff8,[1])).Video);
        Assert.Equal(new byte[] {1,2},receiver.Accept(Packet(1,2,0,[2])).Video);
    }
    [Theory] [InlineData(2)] [InlineData(7)] [InlineData(0)]
    public void Accept_RejectsInvalidHeader_WhenHeaderIsCorrupted(int offset)
    {
        byte[] bytes = Packet(); bytes[offset] ^= 1;
        var receiver = new LiveProtocol(Session);
        Assert.Null(receiver.Accept(bytes).Video); Assert.Equal(0,receiver.VideoBytes);
    }
    [Fact] public void Accept_RejectsInvalidPartCounts_WithoutAllocatingVideo()
    {
        var receiver = new LiveProtocol(Session);
        Assert.Null(receiver.Accept(Packet(1,1)).Video);
        Assert.Null(receiver.Accept(Packet(0,0)).Video);
        Assert.Null(receiver.Accept(new byte[3]).Video);
    }
    [Fact] public void Accept_DoesNotEmitIncompleteUnits_WhenCacheHasToEvictThem()
    {
        var receiver = new LiveProtocol(Session);
        for (int i = 0; i < 50; i++) Assert.Null(receiver.Accept(Packet(0,2,(ushort)(i*16),[1])).Video);
        Assert.Null(receiver.Accept(Packet(1,2,8,[2])).Video);
    }
    [Fact] public async Task UdpReceiver_UsesSessionAndAcknowledgements_AndCancelsCleanly()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var target = (IPEndPoint)peer.Client.LocalEndPoint!;
        var receiver = new UdpVideoReceiver(IPAddress.Loopback,target,0);
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task running = receiver.RunAsync((bytes,_) => { received.TrySetResult(bytes); return ValueTask.CompletedTask; }, timeout.Token);
        UdpReceiveResult handshake = await peer.ReceiveAsync(timeout.Token);
        byte[] packet = Packet();
        packet[2] = handshake.Buffer[2]; packet[3] = handshake.Buffer[3]; packet[7] = LiveProtocol.HeaderXor(packet);
        await peer.SendAsync(packet,handshake.RemoteEndPoint,timeout.Token);
        Assert.Equal(new byte[] {0,0,0,1,0x65},await received.Task.WaitAsync(timeout.Token));
        UdpReceiveResult acknowledgement = await peer.ReceiveAsync(timeout.Token);
        Assert.Equal((byte)4,acknowledgement.Buffer[6]);
        timeout.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }
}
