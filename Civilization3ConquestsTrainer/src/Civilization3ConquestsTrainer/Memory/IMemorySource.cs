namespace Civilization3ConquestsTrainer.Memory;

/// <summary>
/// The slice of a target process <see cref="Game.GameLocator"/> needs.
///
/// It exists so the locator can be driven from a fixture: the verification harness builds a synthetic
/// address space with a planted <c>leaders</c> array, a deliberately corrupted one, a module base
/// somewhere other than 0x400000, and an unreadable window — none of which a concrete
/// <c>ProcessMemory</c> against a real game could be made to produce on demand.
/// </summary>
public interface IMemorySource
{
    /// <summary>Base address the game module is mapped at.</summary>
    nuint ModuleBase { get; }

    /// <summary>Mapped size of the game module, in bytes.</summary>
    int ModuleSize { get; }

    /// <summary>Reads <paramref name="count"/> bytes, returning a shorter array if the read failed.</summary>
    byte[] Read(nuint address, int count);

    /// <summary>
    /// Reads <paramref name="count"/> bytes into <paramref name="buffer"/>. All-or-nothing: returns
    /// either <paramref name="count"/> or 0, matching <c>ProcessMemory</c>.
    /// </summary>
    int Read(nuint address, byte[] buffer, int count);
}

/// <summary>Adapts a live <see cref="ProcessMemory"/> plus its module bounds to <see cref="IMemorySource"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource
{
    private readonly ProcessMemory _mem;

    /// <inheritdoc/>
    public nuint ModuleBase { get; }

    /// <inheritdoc/>
    public int ModuleSize { get; }

    /// <summary>Wraps an open <see cref="ProcessMemory"/> and the module it should be read relative to.</summary>
    public ProcessMemorySource(ProcessMemory mem, nuint moduleBase, int moduleSize)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
        ModuleBase = moduleBase;
        ModuleSize = moduleSize;
    }

    /// <inheritdoc/>
    public byte[] Read(nuint address, int count) => _mem.Read(address, count);

    /// <inheritdoc/>
    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);
}
