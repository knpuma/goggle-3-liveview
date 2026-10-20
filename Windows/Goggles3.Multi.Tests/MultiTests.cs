using System.Diagnostics;
using System.Runtime.InteropServices;
using Goggles3.Core;
using Goggles3.Multi;
using Xunit;

public sealed class MultiTests
{
    [Fact] public void AssignmentIsExclusiveAndCaseInsensitive()
    {
        var slots = new SlotAssignments();
        slots.Assign(0, "USB-A");
        Assert.True(slots.Available("usb-a", 0));
        Assert.False(slots.Available("usb-a", 1));
        Assert.Throws<InvalidOperationException>(() => slots.Assign(1, "usb-a"));
        slots.Assign(0, null); slots.Assign(1, "usb-a");
        Assert.Equal("usb-a", slots[1]);
    }
    [Fact] public void DiscoveryDoesNotReplaceDisconnectedOrReleasedSources()
    {
        var slots = new SlotAssignments();
        slots.Discover(["A", "B", "C", "D", "E"]);
        slots.Discover([]);
        Assert.Equal("A", slots[0]);
        slots.Assign(0, null); slots.Discover(["A", "B", "E"]);
        Assert.Null(slots[0]);
        slots.Assign(0, "E");
        Assert.Equal("E", slots[0]);
    }
    [Fact] public void MosaicKeepsFourSourcesInCorrectQuadrants()
    {
        var mosaic = new Mosaic();
        uint[] colors = [0xff112233, 0xff445566, 0xff778899, 0xffaabbcc];
        for (int slot = 0; slot < 4; slot++)
        {
            byte[] frame = new byte[FfmpegVideoSession.FrameSize];
            MemoryMarshal.Cast<byte, uint>(frame.AsSpan()).Fill(colors[slot]);
            mosaic.Update(slot, mosaic.Reset(slot), frame, 0);
        }
        var result = MemoryMarshal.Cast<byte, uint>(mosaic.Render(0));
        for (int slot = 0; slot < 4; slot++)
            Assert.Equal(colors[slot], result[(slot / 2 * 540 + 200) * 1920 + slot % 2 * 960 + 200]);
        Assert.True(mosaic.HasFrames(0));
    }
    [Fact] public void MosaicRejectsOldSessionsAndBlacksStaleFrames()
    {
        var mosaic = new Mosaic();
        long generation = mosaic.Reset(0);
        byte[] frame = new byte[FfmpegVideoSession.FrameSize];
        Array.Fill(frame, (byte)255);
        mosaic.Update(0, generation, frame, 0);
        Assert.False(mosaic.HasFrames(Stopwatch.Frequency * 4));
        Assert.Equal(0xff000000u, MemoryMarshal.Cast<byte, uint>(mosaic.Render(Stopwatch.Frequency * 4))[0]);
        mosaic.Reset(0); mosaic.Update(0, generation, frame);
        Assert.False(mosaic.HasFrames());
    }
    [Fact] public void MosaicRejectsMalformedFrames()
    {
        var mosaic = new Mosaic();
        Assert.Throws<ArgumentException>(() => mosaic.Update(0, mosaic.Reset(0), new byte[12]));
    }
}
