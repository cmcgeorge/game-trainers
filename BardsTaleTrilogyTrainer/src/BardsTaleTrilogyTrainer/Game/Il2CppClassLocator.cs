using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>The <c>Il2CppClass*</c> of each game type the trainer needs, once resolved.</summary>
public sealed class GameClasses
{
    public nuint Party { get; init; }
    public nuint Player { get; init; }
    public nuint GlobalMaps { get; init; }
    public nuint TeleportTarget { get; init; }

    /// <summary>The spell-table singleton's class, needed to read spell codes, schools and levels.</summary>
    public nuint GlobalSpells { get; init; }

    /// <summary>True when the two classes the map features need were found.</summary>
    public bool HasMapClasses => Player != 0 && GlobalMaps != 0;

    /// <summary>True when a teleport can fabricate its own <c>TeleportTarget</c>.</summary>
    public bool CanFabricateTeleport => TeleportTarget != 0;

    /// <summary>How the classes were found, for the status line.</summary>
    public string Method { get; init; } = "";
}

/// <summary>
/// Finds the <c>Il2CppClass</c> pointers for the game's singleton types.
///
/// <para>IL2CPP caches each type's class pointer in a metadata-usage slot inside
/// <c>GameAssembly.dll</c>'s data section, and the generated code reaches every static field
/// through it. The known slot RVAs (see <see cref="GameFacts"/>) make that a two-read lookup —
/// but they are build-specific, so each candidate is checked by reading the class's own name
/// and namespace before it is trusted.</para>
///
/// <para>When a slot does not check out — a different build, or a type the runtime has not
/// initialised yet — the module's data is swept for any pointer that does resolve to a class
/// with the right name. That is slower but survives a game update, which is the whole point of
/// not hard-coding addresses.</para>
/// </summary>
public static class Il2CppClassLocator
{
    private sealed record Wanted(string Name, int Rva);

    private static readonly Wanted[] Types =
    {
        new("Party", GameFacts.PartyClassRva),
        new("Player", GameFacts.PlayerClassRva),
        new("GlobalMaps", GameFacts.GlobalMapsClassRva),
        new("TeleportTarget", GameFacts.TeleportTargetClassRva),
        new("GlobalSpells", GameFacts.GlobalSpellsClassRva),
    };

    /// <summary>Bytes read per sweep chunk when the known slots miss.</summary>
    private const int ChunkSize = 1 << 20;

    /// <summary>
    /// Upper bound on distinct pointers probed during the sweep. The usage table holds far
    /// fewer than this; the cap only stops a pathological image from stalling the UI.
    /// </summary>
    private const int MaxProbes = 400_000;

    /// <summary>Lowest address treated as a plausible heap pointer.</summary>
    private const ulong MinPlausiblePointer = 0x10000;

    /// <summary>User-mode address ceiling on x64 Windows.</summary>
    private const ulong MaxPlausiblePointer = 0x7FFFFFFFFFFF;

    /// <summary>
    /// Resolves every class the trainer uses. <paramref name="moduleSize"/> may be 0, in which
    /// case only the known slots are tried (there is nothing to sweep).
    /// </summary>
    public static GameClasses Resolve(IMemorySource mem, nuint moduleBase, nuint moduleSize,
        CancellationToken ct = default)
    {
        var found = new Dictionary<string, nuint>(StringComparer.Ordinal);

        if (moduleBase != 0)
        {
            foreach (var t in Types)
            {
                nuint klass = mem.ReadPtr(moduleBase + (nuint)t.Rva);
                if (mem.ClassMatches(klass, t.Name, GameFacts.GameNamespace))
                    found[t.Name] = klass;
            }
        }

        string method = found.Count == Types.Length ? "known class slots" : "";
        if (found.Count < Types.Length && moduleBase != 0 && moduleSize > 0)
        {
            int before = found.Count;
            Sweep(mem, moduleBase, moduleSize, found, ct);
            if (found.Count > before)
                method = before == 0 ? "module sweep" : "known slots + module sweep";
        }

        return new GameClasses
        {
            Party = found.GetValueOrDefault("Party"),
            Player = found.GetValueOrDefault("Player"),
            GlobalMaps = found.GetValueOrDefault("GlobalMaps"),
            TeleportTarget = found.GetValueOrDefault("TeleportTarget"),
            GlobalSpells = found.GetValueOrDefault("GlobalSpells"),
            Method = method.Length > 0 ? method : "not found",
        };
    }

    /// <summary>
    /// Sweeps the loaded module for pointer-sized values that resolve to one of the wanted
    /// classes. Stops as soon as every type has been accounted for.
    /// </summary>
    private static void Sweep(IMemorySource mem, nuint moduleBase, nuint moduleSize,
        Dictionary<string, nuint> found, CancellationToken ct)
    {
        var wanted = Types.Where(t => !found.ContainsKey(t.Name)).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0) return;

        var seen = new HashSet<nuint>();
        var buf = new byte[ChunkSize];
        ulong moduleLow = moduleBase;
        ulong moduleHigh = moduleBase + moduleSize;
        int probes = 0;

        for (nuint offset = 0; offset < moduleSize; offset += ChunkSize)
        {
            ct.ThrowIfCancellationRequested();
            int want = (int)Math.Min((nuint)ChunkSize, moduleSize - offset);
            int read = mem.Read(moduleBase + offset, buf, want);
            if (read < 8) continue;

            for (int i = 0; i + 8 <= read; i += 8)
            {
                ulong value = BitConverter.ToUInt64(buf, i);
                if (value < MinPlausiblePointer || value > MaxPlausiblePointer) continue;
                if (value >= moduleLow && value < moduleHigh) continue;   // an internal pointer, not a class
                var candidate = (nuint)value;
                if (!seen.Add(candidate)) continue;
                if (++probes > MaxProbes) return;

                string? name = ReadClassName(mem, candidate);
                if (name == null || !wanted.Contains(name)) continue;

                found[name] = candidate;
                wanted.Remove(name);
                if (wanted.Count == 0) return;
            }
        }
    }

    /// <summary>
    /// Returns the class name when <paramref name="candidate"/> looks like an
    /// <c>Il2CppClass</c> in the game's namespace, else null. Cheap checks first: both name
    /// pointers must themselves be plausible before any string is read.
    /// </summary>
    private static string? ReadClassName(IMemorySource mem, nuint candidate)
    {
        var head = new byte[Il2Cpp.ClassNamespaceOffset + 8];
        if (mem.Read(candidate, head, head.Length) != head.Length) return null;

        ulong namePtr = BitConverter.ToUInt64(head, Il2Cpp.ClassNameOffset);
        ulong nsPtr = BitConverter.ToUInt64(head, Il2Cpp.ClassNamespaceOffset);
        if (namePtr < MinPlausiblePointer || namePtr > MaxPlausiblePointer) return null;
        if (nsPtr < MinPlausiblePointer || nsPtr > MaxPlausiblePointer) return null;

        if (mem.ReadNativeString((nuint)nsPtr) != GameFacts.GameNamespace) return null;
        string name = mem.ReadNativeString((nuint)namePtr);
        return name.Length == 0 ? null : name;
    }
}
