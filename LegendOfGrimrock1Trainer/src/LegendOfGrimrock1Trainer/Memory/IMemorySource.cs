namespace LegendOfGrimrock1Trainer.Memory;

/// <summary>A committed, readable span of the target's address space.</summary>
public readonly record struct SourceRegion(uint Base, uint Size)
{
    /// <summary>One past the last byte of the region.</summary>
    public long End => (long)Base + Size;
}

/// <summary>
/// The slice of a target process the locator and the Lua reader need.
///
/// It exists so both can be driven from a fixture. The verification harness builds a synthetic
/// 32-bit address space containing a hand-assembled LuaJIT heap — a <c>GG_State</c>, a globals
/// table, a <c>party</c> with champions and stat tables — plus the failure cases a real game cannot
/// be asked to produce: a module relocated away from its preferred base, a stale static pointer,
/// a thread object that is not the main thread, and an unreadable window in the middle of the heap.
///
/// Addresses are <see cref="uint"/> rather than <see cref="nuint"/> because the target is always a
/// 32-bit process: Grimrock 1 ships x86 only, and LuaJIT's non-GC64 object layout — which this whole
/// reader is built on — is itself 32-bit-only.
/// </summary>
public interface IMemorySource
{
    /// <summary>Base address the game module is mapped at. Grimrock opts in to ASLR, so this moves.</summary>
    uint ModuleBase { get; }

    /// <summary>Mapped size of the game module, in bytes.</summary>
    int ModuleSize { get; }

    /// <summary>
    /// Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. All-or-nothing: returns
    /// either <paramref name="count"/> or 0, matching <c>ProcessMemory</c>'s behaviour on a partial
    /// or failed read.
    /// </summary>
    int Read(uint address, byte[] buffer, int count);

    /// <summary>Writes <paramref name="data"/> at <paramref name="address"/>; true only if all bytes landed.</summary>
    bool Write(uint address, byte[] data);

    /// <summary>Committed, readable, non-guard regions of the target, in ascending address order.</summary>
    IEnumerable<SourceRegion> Regions();
}

/// <summary>Adapts a live <see cref="ProcessMemory"/> plus its module bounds to <see cref="IMemorySource"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource
{
    /// <summary>Regions above this are never scanned; a WOW64 process cannot map user pages there.</summary>
    private const long UserSpaceLimit = 0x7FFF_0000L;

    /// <summary>
    /// Regions larger than this are skipped by the heap scan. LuaJIT's own allocator hands out
    /// 1 MB-ish chunks, so the VM never lives in a reservation this big, while Grimrock's texture and
    /// mesh arenas do — skipping them removes hundreds of megabytes of hopeless scanning.
    /// </summary>
    private const uint ScanRegionLimit = 64 * 1024 * 1024;

    private readonly ProcessMemory _mem;

    /// <inheritdoc/>
    public uint ModuleBase { get; }

    /// <inheritdoc/>
    public int ModuleSize { get; }

    /// <summary>Wraps an open <see cref="ProcessMemory"/> and the module it should be read relative to.</summary>
    public ProcessMemorySource(ProcessMemory mem, uint moduleBase, int moduleSize)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
        ModuleBase = moduleBase;
        ModuleSize = moduleSize;
    }

    /// <inheritdoc/>
    public int Read(uint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);

    /// <inheritdoc/>
    public bool Write(uint address, byte[] data) => _mem.Write(address, data);

    /// <inheritdoc/>
    public IEnumerable<SourceRegion> Regions()
    {
        foreach (var r in _mem.EnumerateRegions((nuint)UserSpaceLimit))
        {
            if (r.Base >= UserSpaceLimit) yield break;
            if (r.Size == 0 || r.Size > ScanRegionLimit) continue;
            yield return new SourceRegion((uint)r.Base, (uint)r.Size);
        }
    }
}
