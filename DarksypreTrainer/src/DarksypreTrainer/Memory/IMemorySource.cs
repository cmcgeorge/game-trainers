namespace DarksypreTrainer.Memory;

/// <summary>
/// The read-only slice of a target process that <see cref="CharacterLocator"/> needs. It exists
/// so the locator can also be driven from a fixture in the verification harness, with no live
/// game attached.
/// </summary>
public interface IMemorySource
{
    /// <summary>Committed, readable, non-guard regions of the target, in ascending address order.</summary>
    IEnumerable<MemoryRegion> EnumerateRegions();

    /// <summary>Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. Returns bytes actually read.</summary>
    int Read(nuint address, byte[] buffer, int count);
}

/// <summary>Adapts a live <see cref="ProcessMemory"/> to <see cref="IMemorySource"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource
{
    private readonly ProcessMemory _mem;

    public ProcessMemorySource(ProcessMemory mem)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
    }

    public IEnumerable<MemoryRegion> EnumerateRegions() => _mem.EnumerateRegions();

    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);
}
