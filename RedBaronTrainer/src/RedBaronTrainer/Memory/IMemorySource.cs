namespace RedBaronTrainer.Memory;

/// <summary>
/// The slice of a target process that <see cref="GameLocator"/> needs. It exists so the locator can
/// be driven from a synthetic address space in the verification harness, with no emulator and no
/// copyrighted game files present.
/// </summary>
public interface IMemorySource
{
    /// <summary>Committed, readable, non-guard regions of the target, in ascending address order.</summary>
    IEnumerable<MemoryRegion> EnumerateRegions();

    /// <summary>Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. Returns bytes actually read.</summary>
    int Read(nuint address, byte[] buffer, int count);

    /// <summary>Reads <paramref name="count"/> bytes, returning a shorter array if the read failed.</summary>
    byte[] Read(nuint address, int count);

    /// <summary>Writes <paramref name="buffer"/> at <paramref name="address"/>. Returns true if all bytes landed.</summary>
    bool Write(nuint address, byte[] buffer);
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

    public byte[] Read(nuint address, int count) => _mem.Read(address, count);

    public bool Write(nuint address, byte[] buffer) => _mem.Write(address, buffer);
}
