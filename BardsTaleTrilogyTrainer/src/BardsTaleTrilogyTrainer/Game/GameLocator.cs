using System.Diagnostics;
using System.IO;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// The result of a successful locate: the resolved IL2CPP classes, the party object and the
/// addresses of the individual character objects in <c>Party.m_members</c>.
/// </summary>
public sealed class GameLocation
{
    public GameClasses Classes { get; init; } = new();
    public nuint PartyObject { get; init; }
    public List<nuint> CharacterAddresses { get; init; } = new();
    public bool UsedFallback { get; init; }
    public string Summary { get; init; } = "";

    public int CharacterCount => CharacterAddresses.Count;
}

/// <summary>
/// Finds the game's data in the running process.
///
/// <para>Primary route: resolve each type's <c>Il2CppClass</c> (see
/// <see cref="Il2CppClassLocator"/>), then follow <c>Party</c>'s static <c>Instance</c> to
/// <c>m_members</c>. That is the same path the game's own code takes, so it needs no
/// heuristics and cannot land on a look-alike.</para>
///
/// <para>Fallback: if the classes cannot be resolved at all — an unexpected build, or the
/// runtime has not initialised those types yet — sweep committed memory for objects shaped
/// like a <c>Character</c>.</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;

    /// <summary>
    /// Find the game process by name. The caller owns the returned <see cref="Process"/>;
    /// the others the enumeration produced are disposed here rather than left to a finaliser.
    /// </summary>
    public static Process? FindGameProcess()
    {
        var all = Process.GetProcessesByName(GameFacts.ProcessName);
        for (int i = 1; i < all.Length; i++) all[i].Dispose();
        return all.Length > 0 ? all[0] : null;
    }

    /// <summary>Find the base address of a module in the target process.</summary>
    public static nuint FindModuleBase(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                return (nuint)module.BaseAddress.ToInt64();
        }
        return 0;
    }

    /// <summary>Size of a module's image in the target process, for bounded sweeps.</summary>
    public static nuint FindModuleSize(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                return (nuint)module.ModuleMemorySize;
        }
        return 0;
    }

    /// <summary>
    /// The folder the game is installed in, taken from the running process where possible so
    /// that a non-default Steam library still works, else the usual install locations.
    /// </summary>
    public static string? FindGameDirectory(Process? process)
    {
        try
        {
            string? exe = process?.MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                string? dir = Path.GetDirectoryName(exe);
                if (dir != null && Directory.Exists(Path.Combine(dir, "TheBardsTaleTrilogy_Data")))
                    return dir;
            }
        }
        catch (Exception)
        {
            // MainModule throws for a process we cannot fully open; fall through to the guesses.
        }

        foreach (var candidate in GameFacts.LikelyGameDirectories)
        {
            if (Directory.Exists(Path.Combine(candidate, "TheBardsTaleTrilogy_Data")))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Locates the party. Resolves the IL2CPP classes first — those are what the map and
    /// teleport features need — then walks <c>Party.Instance.m_members</c>.
    /// </summary>
    public static GameLocation? Locate(
        IMemorySource mem,
        nuint moduleBase,
        nuint moduleSize,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var classes = Il2CppClassLocator.Resolve(mem, moduleBase, moduleSize, ct);

        if (classes.Party != 0)
        {
            nuint party = mem.ReadStaticRef(classes.Party, CharacterFormat.PartyInstanceStatic);
            if (party != 0)
            {
                var members = ReadMembers(mem, party);
                return new GameLocation
                {
                    Classes = classes,
                    PartyObject = party,
                    CharacterAddresses = members,
                    UsedFallback = false,
                    Summary = members.Count > 0
                        ? $"Party.Instance found via {classes.Method}: {members.Count} member(s)"
                        : $"Party.Instance found via {classes.Method}, but no members are loaded yet",
                };
            }
        }

        // No class pointers (or no Party singleton): fall back to a shape scan so the character
        // editor still has something to work with.
        var hits = StructuralScan(mem, progress, ct);
        if (hits.Count == 0 && !classes.HasMapClasses) return null;

        return new GameLocation
        {
            Classes = classes,
            PartyObject = 0,
            CharacterAddresses = hits,
            UsedFallback = true,
            Summary = hits.Count > 0
                ? $"Structural scan: {hits.Count} character object(s) found"
                : "No party found, but the map classes resolved — location and teleport are available",
        };
    }

    /// <summary>
    /// Reads <c>Party.m_members</c>, keeping only slots that hold a real character. Each array
    /// element is a <c>PartyMember</c> — the slot's UI wrapper — so the character is one more
    /// hop away, and an empty slot is a wrapper whose character reference is null.
    /// </summary>
    private static List<nuint> ReadMembers(IMemorySource mem, nuint party)
    {
        var result = new List<nuint>();
        nuint members = mem.ReadPtr(party + (nuint)CharacterFormat.PartyMembers);
        int count = mem.ReadArrayLength(members);
        if (count <= 0 || count > GameFacts.PartySlots) return result;

        var buf = new byte[CharacterFormat.ProbeSize];
        for (int i = 0; i < count; i++)
        {
            nuint slot = mem.ReadArrayRef(members, i);
            if (slot == 0) continue;
            nuint character = mem.ReadPtr(slot + (nuint)CharacterFormat.PartyMemberCharacter);
            if (character == 0) continue;
            if (mem.Read(character, buf, buf.Length) != buf.Length) continue;
            if (CharacterFormat.LooksLikeCharacter(buf)) result.Add(character);
        }
        return result;
    }

    /// <summary>Sweeps committed memory for objects shaped like a <c>Character</c>.</summary>
    private static List<nuint> StructuralScan(
        IMemorySource mem,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var hits = new List<nuint>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var (_, size) in regions) totalBytes += size;
        nuint scanned = 0;

        var buf = new byte[ChunkSize];

        foreach (var (regionBase, regionSize) in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < regionSize;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, regionSize - offset);
                int read = mem.Read(regionBase + offset, buf, want);
                if (read < CharacterFormat.ProbeSize)
                {
                    scanned += (nuint)read;
                    break;
                }

                // IL2CPP objects are 8-byte aligned, so only those offsets can start one.
                for (int i = 0; i + CharacterFormat.ProbeSize <= read; i += 8)
                {
                    // Cheap gate first: hit points must be sane and no greater than the maximum.
                    int hpMax = CharacterFormat.ReadI32(buf.AsSpan(i), CharacterFormat.OffHpMax);
                    if (hpMax <= 0 || hpMax > 100_000) continue;
                    int hp = CharacterFormat.ReadI32(buf.AsSpan(i), CharacterFormat.OffHpCur);
                    if (hp < 0 || hp > hpMax) continue;

                    if (CharacterFormat.LooksLikeCharacter(buf.AsSpan(i)))
                    {
                        nuint addr = regionBase + offset + (nuint)i;
                        if (!hits.Contains(addr)) hits.Add(addr);
                        if (hits.Count >= GameFacts.PartySlots) return hits;
                    }
                }

                nuint advance = (nuint)Math.Max(1, read - CharacterFormat.ProbeSize);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        return hits;
    }
}
