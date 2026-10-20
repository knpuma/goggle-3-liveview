namespace Goggles3.Multi;

public sealed class SlotAssignments
{
    public const int Capacity = 4;
    private readonly string?[] sources = new string?[Capacity];
    private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    public string? this[int slot] => sources[slot];
    public bool Available(string source, int slot) =>
        !sources.Where((_, index) => index != slot).Any(value => string.Equals(value, source, StringComparison.OrdinalIgnoreCase));
    public void Assign(int slot, string? source)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, Capacity);
        if (source != null && !Available(source, slot)) throw new InvalidOperationException("This USB is already assigned.");
        sources[slot] = source;
    }
    public void Discover(IEnumerable<string> present)
    {
        foreach (string id in present)
        {
            if (!seen.Add(id)) continue;
            if (sources.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase))) continue;
            int slot = Array.FindIndex(sources, source => source == null);
            if (slot >= 0) sources[slot] = id;
        }
    }
}
