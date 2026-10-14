using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Goggles3.Core;

/// <summary>Own implementation of the same UDP session validated on macOS.</summary>
public sealed class LiveProtocol
{
    private readonly ushort session;
    private readonly Dictionary<ushort, Unit> units = [];
    private readonly Queue<ushort> order = [];
    private sealed class Unit(int count)
    {
        public byte[]?[] Parts { get; } = new byte[count][];
        public int Received { get; set; }
        public bool Complete { get; set; }
    }

    public LiveProtocol(ushort? sessionId = null) => session = sessionId ?? (ushort)RandomNumberGenerator.GetInt32(65536);
    public long VideoBytes { get; private set; }

    public byte[] CreateHandshake()
    {
        byte[] bytes = [
            0x30,0x80,0,0,0,0,0,0, 0xd0,0xe9,0x64,0,0x64,0,0xc0,0x05,
            0x14,0,0,0x0a,0,0x64,0,0x64, 0,0xc0,0x05,0x14,0,0,0x64,0,
            0x14,0,0x64,0,0xc0,0x05,0x14,0, 0,0x64,0,1,1,4,0x0a,2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), session);
        bytes[7] = HeaderXor(bytes);
        return bytes;
    }

    public static byte HeaderXor(ReadOnlySpan<byte> bytes)
    {
        byte result = 0;
        for (int i = 0; i < 7; i++) result ^= bytes[i];
        return result;
    }

    // The receiver is single-threaded. Completed units retain a dedup marker,
    // but not their payload. At most 32 units are retained.
    public PacketResult Accept(ReadOnlySpan<byte> message)
    {
        if (message.Length < 8 ||
            (BinaryPrimitives.ReadUInt16LittleEndian(message) & 0x7fff) != message.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(message[2..]) != session ||
            message[7] != HeaderXor(message)) return new(null, null, false);
        if (message[6] == 0) return new(null, null, true);
        if (message[6] != 2 || message.Length < 20) return new(null, null, false);
        int count = message[17] & 0x7f;
        int part = (message[17] >> 7) | (message[18] << 1) | (message[19] << 9);
        if (count == 0 || part >= count) return new(null, null, false);
        ushort sequence = BinaryPrimitives.ReadUInt16LittleEndian(message[4..]);
        ushort key = unchecked((ushort)((sequence & 0xfff8) - part * 8));
        if (!units.TryGetValue(key, out Unit? unit))
        {
            if (units.Count >= 32) units.Remove(order.Dequeue());
            unit = new Unit(count); units.Add(key, unit); order.Enqueue(key);
        }
        if (unit.Parts.Length != count) return new(null, null, false);
        byte[]? acknowledgement = part + 1 == count ? CreateAck(sequence) : null;
        if (unit.Complete || unit.Parts[part] != null) return new(null, acknowledgement, false);
        unit.Parts[part] = message[20..].ToArray();
        VideoBytes += message.Length - 20; unit.Received++;
        if (unit.Received != count) return new(null, acknowledgement, false);
        byte[] frame = new byte[unit.Parts.Sum(p => p!.Length)];
        int offset = 0;
        for (int i = 0; i < count; i++)
        {
            unit.Parts[i]!.CopyTo(frame, offset); offset += unit.Parts[i]!.Length;
            unit.Parts[i] = null;
        }
        unit.Complete = true;
        return new(frame, acknowledgement, false);
    }

    private byte[] CreateAck(ushort sequence)
    {
        byte[] bytes = [0x1e,0x80,0,0,0,0,4,0, 0,0,0,0,0,0,0xd0,0xe9,
                       0xd0,0xe9,0,0,0xd0,0xe9,0xd8,0xe9,0,0,0,0,0,0];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), session);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), sequence);
        bytes[7] = HeaderXor(bytes);
        return bytes;
    }
}

public sealed record PacketResult(byte[]? Video, byte[]? Acknowledgement, bool HandshakeConfirmed);
