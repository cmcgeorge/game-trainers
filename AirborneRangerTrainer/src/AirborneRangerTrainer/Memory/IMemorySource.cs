namespace AirborneRangerTrainer.Memory;

/// <summary>
/// The read-only slice of a target process that <see cref="GameLocator"/> needs.
///
/// This exists so the locator — the riskiest code in the trainer, and the only part whose failure
/// mode is "finds nothing, silently" — can be driven from a fixture. The verification harness builds
/// a synthetic memory image with anchors placed on chunk seams and unreadable pages, which a
/// concrete <c>ProcessMemory</c> could never be made to do.
/// </summary>
public interface IMemorySource
{
    /// <summary>Committed, readable, non-guard regions of the target, in ascending address order.</summary>
    IEnumerable<MemoryRegion> EnumerateRegions();

    /// <summary>
    /// Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. All-or-nothing: returns
    /// the number of bytes read, which is either <paramref name="count"/> or 0.
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
