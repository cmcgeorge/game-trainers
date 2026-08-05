using System.Diagnostics;

namespace TheQuestTrainer.Game;

/// <summary>Where the game module is mapped, and how that was worked out.</summary>
/// <param name="Base">Mapped base address.</param>
/// <param name="Size">Mapped size, from the PE's <c>SizeOfImage</c> or the module list.</param>
/// <param name="Image">Parsed header, when it could be read.</param>
/// <param name="How">One line for the status bar.</param>
public readonly record struct ModuleLocation(uint Base, int Size, PeImage? Image, string How)
{
    /// <summary>Whether a module was found.</summary>
    public bool Found => Base != 0 && Size > 0;
}

/// <summary>
/// Finds <c>TheQuest.exe</c>'s mapped base in the target.
///
/// The obvious route is <see cref="Process.MainModule"/>, and it is tried first. It is not trusted
/// to be enough, though: the trainer is a 64-bit process reading a 32-bit (WOW64) target, and the
/// module list is exactly the thing that has historically been awkward to read across that boundary
/// — a launcher that starts the game suspended, or a security product that hides the module list,
/// produces a <see cref="Win32Exception"/> rather than an answer.
///
/// The fallback needs no module list at all: image mappings start on a 64 KB boundary, so every such
/// boundary is probed for an <c>MZ</c>/<c>PE</c> pair, parsed, and kept if it is a 32-bit
/// <i>executable</i> (not a DLL). A 32-bit process has exactly one of those.
/// </summary>
public static class ModuleResolver
{
    /// <summary>Image mappings are always aligned to this, so the sweep can stride by it.</summary>
    private const uint AllocationGranularity = 0x1_0000;

    /// <summary>A WOW64 process cannot map user pages above this.</summary>
    private const long UserSpaceLimit = 0x7FFF_0000L;

    /// <summary>Resolves the module, preferring the module list and falling back to a header sweep.</summary>
    public static ModuleLocation Resolve(Process process, ProcessMemory memory)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memory);

        var viaModuleList = FromModuleList(process, memory);
        if (viaModuleList.Found) return viaModuleList;

        var viaSweep = FromHeaderSweep(memory);
        if (viaSweep.Found) return viaSweep;

        return new ModuleLocation(0, 0, null, "Could not find the game's module in that process.");
    }

    private static ModuleLocation FromModuleList(Process process, ProcessMemory memory)
    {
        try
        {
            var main = process.MainModule;
            if (main is null) return default;

            long baseAddress = main.BaseAddress.ToInt64();
            if (baseAddress <= 0 || baseAddress > uint.MaxValue) return default;

            // The header's SizeOfImage is the target's own claim about itself, so it gets the same
            // range guard the sweep applies before it is narrowed to int; the module list's own
            // figure is the fallback.
            var image = ReadHeader(memory, (uint)baseAddress);
            int size = image is not null && image.SizeOfImage > 0 && image.SizeOfImage <= int.MaxValue
                ? (int)image.SizeOfImage
                : main.ModuleMemorySize;
            if (size <= 0) return default;
            return new ModuleLocation((uint)baseAddress, size, image, $"Module base 0x{baseAddress:X8} (module list).");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return default;
        }
    }

    private static ModuleLocation FromHeaderSweep(ProcessMemory memory)
    {
        foreach (var region in memory.EnumerateRegions((nuint)UserSpaceLimit))
        {
            if (region.Base == 0 || (ulong)region.Base % AllocationGranularity != 0) continue;
            if ((long)region.Base >= UserSpaceLimit) continue;

            var image = ReadHeader(memory, (uint)region.Base);
            if (image is null || !image.IsWin32X86 || image.IsDll) continue;
            if (image.SizeOfImage == 0 || image.SizeOfImage > int.MaxValue) continue;

            return new ModuleLocation((uint)region.Base, (int)image.SizeOfImage, image,
                $"Module base 0x{region.Base:X8} (header sweep — the module list was unreadable).");
        }
        return default;
    }

    private static PeImage? ReadHeader(ProcessMemory memory, uint baseAddress)
    {
        var header = new byte[PeImage.HeaderBytes];
        return memory.Read(baseAddress, header, header.Length) == header.Length
            ? PeImage.Parse(header)
            : null;
    }
}
