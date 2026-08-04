namespace DarkDesigns1Trainer.Memory;

/// <summary>
/// The read-only slice of a target process that <see cref="MapLocator"/> needs.
///
/// It exists so the locator can be driven from a fixture: the verification harness builds synthetic
/// address spaces with the map buffer straddling a chunk seam, sitting beyond the first scan
/// window, placed near address zero, and backed by a region whose readable prefix stops short —
/// none of which a concrete <c>ProcessMemory</c> could be made to do on demand.
/// </summary>
public interface IMemorySource
{
    /// <summary>Committed, readable, non-guard regions of the target, in ascending address order.</summary>
    IEnumerable<MemoryRegion> EnumerateRegions();

    /// <summary>
    /// Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. All-or-nothing: returns
    /// either <paramref name="count"/> or 0, matching <c>ProcessMemory</c>.
    /// </summary>
    int Read(nuint address, byte[] buffer, int count);

    /// <summary>Reads <paramref name="count"/> bytes, returning a shorter array if the read failed.</summary>
    byte[] Read(nuint address, int count);
}

/// <summary>Adapts a live <see cref="ProcessMemory"/> to <see cref="IMemorySource"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource
{
    private readonly ProcessMemory _mem;

    /// <summary>Wraps an open <see cref="ProcessMemory"/>.</summary>
    public ProcessMemorySource(ProcessMemory mem)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
    }

    /// <inheritdoc/>
    public IEnumerable<MemoryRegion> EnumerateRegions() => _mem.EnumerateRegions();

    /// <inheritdoc/>
    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);

    /// <inheritdoc/>
    public byte[] Read(nuint address, int count) => _mem.Read(address, count);
}
