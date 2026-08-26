using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.Game;

/// <summary>The <c>Il2CppClass*</c> of each game type the trainer needs, once resolved.</summary>
public sealed class GameClasses
{
    /// <summary>The only class the primary locate path requires.</summary>
    public nuint Party { get; init; }

    /// <summary>
    /// Optional. Used to confirm the identity of objects found by the structural fallback.
    /// On the primary path the Player class is read straight off the first party member's
    /// object header instead, so the sweep does not have to find it.
    /// </summary>
    public nuint Player { get; init; }

    /// <summary>Optional — gateway to the live save block (map, position, clock).</summary>
    public nuint PartyManager { get; init; }

    /// <summary>Optional — the game-wide singleton that owns the party manager.</summary>
    public nuint Wasteland { get; init; }

    /// <summary>How the classes were found, for the status line.</summary>
    public string Method { get; init; } = "";

    /// <summary>True when the sweep gave up on its probe budget before covering everything.</summary>
    public bool ProbeBudgetExhausted { get; init; }

    /// <summary>True when the class the primary locate path needs was found.</summary>
    public bool IsValid => Party != 0;
}

/// <summary>
/// Finds the <c>Il2CppClass</c> pointers for the game's singleton types.
///
/// <para>Since no metadata-usage slot RVAs are known for this build, the locator sweeps the
/// loaded module for pointer-sized values that resolve to an <c>Il2CppClass</c> with the right
/// name and namespace. Each candidate is validated by reading the class's own name and
/// namespace before it is trusted.</para>
///
/// <para>The sweep covers the module's <b>readable, non-executable sections only</b>. The
/// metadata-usage slots that hold class pointers live in <c>.data</c>/<c>.rdata</c>; the
/// executable sections are tens of megabytes of instruction bytes that can only produce
/// false candidates and burn the probe budget. Skipping them is what keeps the sweep to a
/// few seconds. If the PE headers cannot be parsed the whole module is swept instead.</para>
/// </summary>
public static class Il2CppClassLocator
{
    private sealed record Wanted(string Name, bool Required);

    private static readonly Wanted[] Types =
    {
        new("Party", true),
        new("Player", false),
        new("PartyManager", false),
        new("Wasteland", false),
    };

    /// <summary>Bytes read per sweep chunk.</summary>
    private const int ChunkSize = 1 << 20;

    /// <summary>Upper bound on distinct pointers probed during the sweep.</summary>
    private const int MaxProbes = 2_000_000;

    /// <summary>Lowest address treated as a plausible heap pointer.</summary>
    private const ulong MinPlausiblePointer = 0x10000;

    /// <summary>User-mode address ceiling on x64 Windows.</summary>
    private const ulong MaxPlausiblePointer = 0x7FFFFFFFFFFF;

    /// <summary>
    /// How far to jump past an unreadable spot. A failed <see cref="IMemorySource.Read"/> tells
    /// us nothing about where the readable part resumes, so step a page rather than 8 bytes —
    /// stepping by 8 turns one unreadable megabyte into 131,072 more failing reads.
    /// </summary>
    private const int PageSize = 0x1000;

    /// <summary>
    /// Resolves every class the trainer uses by sweeping the module for Il2CppClass pointers.
    /// </summary>
    public static GameClasses Resolve(IMemorySource mem, nuint moduleBase, nuint moduleSize,
        CancellationToken ct = default)
    {
        var found = new Dictionary<string, nuint>(StringComparer.Ordinal);
        bool exhausted = false;
        string how = "not found";

        if (moduleBase != 0 && moduleSize > 0)
        {
            // "Could not parse the headers" and "parsed them, and nothing qualifies" are
            // different answers. The first means we do not know the layout, so sweep everything;
            // the second means we do know, and there is nowhere to look — falling back to a full
            // sweep there would scan the executable sections this whole exercise exists to avoid.
            List<(nuint Base, nuint Size)> ranges;
            if (TryReadDataSections(mem, moduleBase, moduleSize, out var parsed))
            {
                ranges = parsed;
                how = "data-section sweep";
            }
            else
            {
                ranges = new List<(nuint, nuint)> { (moduleBase, moduleSize) };
                how = "module sweep";
            }

            exhausted = Sweep(mem, ranges, moduleBase, moduleSize, found, ct);

            if (!found.ContainsKey("Party"))
                how = found.Count > 0 ? $"partial {how}" : "not found";
        }

        return new GameClasses
        {
            Party = found.GetValueOrDefault("Party"),
            Player = found.GetValueOrDefault("Player"),
            PartyManager = found.GetValueOrDefault("PartyManager"),
            Wasteland = found.GetValueOrDefault("Wasteland"),
            Method = how,
            ProbeBudgetExhausted = exhausted,
        };
    }

    /// <summary>
    /// Reads the module's PE section table into <paramref name="result"/> — the readable,
    /// non-executable ranges, where IL2CPP keeps its class-pointer slots.
    /// </summary>
    /// <returns>
    /// True when the headers were parsed, even if no section qualified. False only when the
    /// headers could not be read, which is the caller's signal to sweep the whole module.
    /// </returns>
    private static bool TryReadDataSections(IMemorySource mem, nuint moduleBase, nuint moduleSize,
        out List<(nuint Base, nuint Size)> result)
    {
        result = new List<(nuint, nuint)>();

        var dos = new byte[0x40];
        if (mem.Read(moduleBase, dos, dos.Length) != dos.Length) return false;
        if (BitConverter.ToUInt16(dos, 0) != 0x5A4D) return false;               // 'MZ'

        int peOffset = BitConverter.ToInt32(dos, 0x3C);
        if (peOffset <= 0 || (ulong)peOffset + 0x18 > moduleSize) return false;

        var fileHeader = new byte[0x18];                                          // signature + IMAGE_FILE_HEADER
        if (mem.Read(moduleBase + (nuint)peOffset, fileHeader, fileHeader.Length) != fileHeader.Length) return false;
        if (BitConverter.ToUInt32(fileHeader, 0) != 0x00004550) return false;    // 'PE\0\0'

        int sectionCount = BitConverter.ToUInt16(fileHeader, 6);
        int optionalHeaderSize = BitConverter.ToUInt16(fileHeader, 20);
        if (sectionCount <= 0 || sectionCount > 96) return false;

        int tableOffset = peOffset + 4 + 20 + optionalHeaderSize;
        const int sectionHeaderSize = 40;
        var table = new byte[sectionCount * sectionHeaderSize];
        if (mem.Read(moduleBase + (nuint)tableOffset, table, table.Length) != table.Length) return false;

        const uint MemExecute = 0x20000000;
        const uint MemRead = 0x40000000;

        for (int i = 0; i < sectionCount; i++)
        {
            int at = i * sectionHeaderSize;
            uint virtualSize = BitConverter.ToUInt32(table, at + 8);
            uint virtualAddress = BitConverter.ToUInt32(table, at + 12);
            uint characteristics = BitConverter.ToUInt32(table, at + 36);

            if ((characteristics & MemRead) == 0) continue;
            if ((characteristics & MemExecute) != 0) continue;
            if (virtualSize == 0 || virtualAddress >= moduleSize) continue;

            nuint size = (nuint)Math.Min(virtualSize, (uint)(moduleSize - virtualAddress));
            if (size >= 8) result.Add((moduleBase + virtualAddress, size));
        }

        return true;
    }

    /// <summary>
    /// Sweeps the given ranges for pointer-sized values that resolve to one of the wanted
    /// classes. Stops as soon as every type has been accounted for. Returns true when the
    /// probe budget ran out before the sweep finished.
    /// </summary>
    private static bool Sweep(IMemorySource mem, List<(nuint Base, nuint Size)> ranges,
        nuint moduleBase, nuint moduleSize, Dictionary<string, nuint> found, CancellationToken ct)
    {
        var wanted = Types.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0) return false;

        // An Il2CppClass is heap-allocated, so a value pointing back into the module is
        // internal data (a vtable, a literal, a relocation) and never a class pointer.
        ulong moduleLow = moduleBase;
        ulong moduleHigh = moduleBase + moduleSize;

        var seen = new HashSet<nuint>();
        var buf = new byte[ChunkSize];
        int probes = 0;

        foreach (var (rangeBase, rangeSize) in ranges)
        {
            for (nuint offset = 0; offset < rangeSize;)
            {
                ct.ThrowIfCancellationRequested();

                int want = (int)Math.Min((nuint)ChunkSize, rangeSize - offset);
                int read = mem.Read(rangeBase + offset, buf, want);

                if (read < 8 && want > PageSize)
                {
                    // ReadProcessMemory fails the whole span if any page in it is inaccessible,
                    // so retry a page at a time before giving up on this offset — otherwise one
                    // bad page discards up to a megabyte of readable data, which in .rdata could
                    // be the very megabyte holding the class pointer.
                    want = PageSize;
                    read = mem.Read(rangeBase + offset, buf, want);
                }

                if (read < 8)
                {
                    // Genuinely unreadable; skip a page rather than crawling 8 bytes at a time.
                    offset += (nuint)PageSize;
                    continue;
                }

                for (int i = 0; i + 8 <= read; i += 8)
                {
                    ulong value = BitConverter.ToUInt64(buf, i);
                    if (value < MinPlausiblePointer || value > MaxPlausiblePointer) continue;
                    if (value >= moduleLow && value < moduleHigh) continue;
                    var candidate = (nuint)value;
                    if (!seen.Add(candidate)) continue;
                    if (++probes > MaxProbes) return true;

                    string? name = ReadClassName(mem, candidate, wanted);
                    if (name == null) continue;

                    found[name] = candidate;
                    wanted.Remove(name);
                    if (wanted.Count == 0) return false;
                }

                int advance = (read + 7) & ~7;
                offset += (nuint)Math.Max(advance, 8);
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the class name when <paramref name="candidate"/> looks like an
    /// <c>Il2CppClass</c> that is one of the <paramref name="wanted"/> types, else null.
    ///
    /// <para>The name is checked before the namespace because it is by far the more selective
    /// test — almost every candidate fails on it, and failing early is what keeps the sweep
    /// cheap. The namespace is then verified with a read that distinguishes "empty" from
    /// "unreadable", so an unreadable candidate cannot pass by matching the empty global
    /// namespace by accident.</para>
    /// </summary>
    private static string? ReadClassName(IMemorySource mem, nuint candidate, HashSet<string> wanted)
    {
        var head = new byte[Il2Cpp.ClassNamespaceOffset + 8];
        if (mem.Read(candidate, head, head.Length) != head.Length) return null;

        ulong namePtr = BitConverter.ToUInt64(head, Il2Cpp.ClassNameOffset);
        ulong nsPtr = BitConverter.ToUInt64(head, Il2Cpp.ClassNamespaceOffset);
        if (namePtr < MinPlausiblePointer || namePtr > MaxPlausiblePointer) return null;
        if (nsPtr < MinPlausiblePointer || nsPtr > MaxPlausiblePointer) return null;

        if (!mem.TryReadNativeString((nuint)namePtr, out string name)) return null;
        if (!wanted.Contains(name)) return null;

        // Game types are in the global namespace. A failed read must not pass as "".
        if (!mem.TryReadNativeString((nuint)nsPtr, out string ns)) return null;
        if (ns != GameFacts.GameNamespace) return null;

        return name;
    }
}
