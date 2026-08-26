using System.Diagnostics;
using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// The result of a successful locate: the resolved IL2CPP classes, the party object and the
/// addresses of the individual player objects in <c>Party.players</c>.
/// </summary>
public sealed class GameLocation
{
    public GameClasses Classes { get; init; } = new();
    public nuint PartyObject { get; init; }
    public List<nuint> CharacterAddresses { get; init; } = new();

    /// <summary>
    /// The <c>Player</c> class pointer, read off a party member's own object header. Available
    /// even when the sweep never found the class, and used to confirm identity.
    /// </summary>
    public nuint PlayerClass { get; init; }

    /// <summary>
    /// Entries that were in <c>Party.players</c> but could not be confirmed as Player objects.
    /// Surfaced in the status line so a silently short party is never mistaken for a real one.
    /// </summary>
    public int RejectedEntries { get; init; }

    public bool UsedFallback { get; init; }
    public string Summary { get; init; } = "";

    public int CharacterCount => CharacterAddresses.Count;
}

/// <summary>
/// Finds the game's data in the running process.
///
/// <para>Primary route: resolve <c>Party</c>'s <c>Il2CppClass</c> by sweeping the module's data
/// sections (see <see cref="Il2CppClassLocator"/>), then follow its static <c>m_instance</c> to
/// <c>players</c> — a <c>List&lt;Player&gt;</c>. That is the same path the game's own code
/// takes, so it needs no heuristics and cannot land on a look-alike.</para>
///
/// <para>Entries in that list are confirmed by <b>identity, not plausibility</b>: the Player
/// class pointer is read off a party member's own object header and every other entry must
/// carry the same one. A plausibility filter would be wrong here — a ranger at negative CON is
/// still a ranger, and dropping them silently would hide exactly the characters a player
/// reaches for a trainer to fix.</para>
///
/// <para>Fallback: if the party cannot be reached at all, sweep committed memory for objects
/// shaped like a <c>Player</c>. Hits are confirmed against the Player class pointer whenever
/// one is known.</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;

    /// <summary>How far to jump past an unreadable spot during the structural scan.</summary>
    private const int PageSize = 0x1000;

    /// <summary>
    /// Find the game process by name.
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

    /// <summary>Size of a module's image in the target process.</summary>
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
    /// Locates the party. Resolves the IL2CPP classes first, then walks
    /// <c>Party.m_instance.players</c>, falling back to a structural scan if that yields
    /// nothing.
    /// </summary>
    public static GameLocation? Locate(
        IMemorySource mem,
        nuint moduleBase,
        nuint moduleSize,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var classes = Il2CppClassLocator.Resolve(mem, moduleBase, moduleSize, ct);
        ct.ThrowIfCancellationRequested();

        nuint party = 0;
        var walk = new PartyWalk(WalkOutcome.Unreachable, new List<nuint>(), 0, 0);

        if (classes.Party != 0)
        {
            party = mem.ReadStaticRef(classes.Party, CharacterFormat.PartyInstanceStatic);
            if (party != 0)
            {
                walk = ReadPlayers(mem, party, classes.Player);

                if (walk.Outcome == WalkOutcome.Players)
                {
                    string note = walk.Rejected > 0
                        ? $", {walk.Rejected} list entry/entries could not be confirmed"
                        : "";
                    return new GameLocation
                    {
                        Classes = classes,
                        PartyObject = party,
                        CharacterAddresses = walk.Players,
                        PlayerClass = walk.PlayerClass,
                        RejectedEntries = walk.Rejected,
                        UsedFallback = false,
                        Summary = $"Party.m_instance found via {classes.Method}: " +
                                  $"{walk.Players.Count} player(s){note}",
                    };
                }

                // The roster read cleanly and is empty: the game is at the menu. Say so instead
                // of sweeping gigabytes of memory, which would be slow and could hand back
                // character-creation or save-preview objects that edit nothing real.
                if (walk.Outcome == WalkOutcome.EmptyRoster)
                {
                    return new GameLocation
                    {
                        Classes = classes,
                        PartyObject = party,
                        PlayerClass = classes.Player,
                        UsedFallback = false,
                        Summary = "Party.m_instance is present but holds no rangers yet — " +
                                  "load or start a game, then locate again",
                    };
                }
            }
        }

        // The typed walk could not be completed — the class pointer may be wrong, or a page was
        // unreadable. Fall back to a shape scan rather than reporting nothing at all.
        var hits = StructuralScan(mem, classes.Player, progress, ct);
        if (hits.Count == 0)
        {
            if (classes.ProbeBudgetExhausted)
            {
                return new GameLocation
                {
                    Classes = classes,
                    Summary = "Class sweep hit its probe budget before finding Party, and no " +
                              "player-shaped objects were found. Load a game, then locate again.",
                };
            }
            return null;
        }

        return new GameLocation
        {
            Classes = classes,
            PartyObject = party,
            CharacterAddresses = hits,
            PlayerClass = classes.Player,
            RejectedEntries = walk.Rejected,
            UsedFallback = true,
            Summary = party != 0
                ? $"Party.players could not be walked; structural scan found {hits.Count} player object(s)"
                : $"Structural scan: {hits.Count} player object(s) found",
        };
    }

    /// <summary>What a walk of <c>Party.players</c> found.</summary>
    private enum WalkOutcome
    {
        /// <summary>A link in the chain was missing or unreadable — fall back to a scan.</summary>
        Unreachable,

        /// <summary>The roster was read and is genuinely empty — no game loaded yet.</summary>
        EmptyRoster,

        /// <summary>Characters were found.</summary>
        Players,
    }

    private readonly record struct PartyWalk(
        WalkOutcome Outcome, List<nuint> Players, nuint PlayerClass, int Rejected);

    /// <summary>
    /// Reads <c>Party.players</c>. The players list is a <c>List&lt;Player&gt;</c> whose backing
    /// array holds direct Player references (unlike the Bard's Tale Trilogy, which wraps
    /// characters in slot objects).
    ///
    /// <para>The list's <c>_size</c> is clamped rather than rejected: if the roster is ever
    /// longer than the seven marching slots — recruitables, a preallocated list — bailing out
    /// would report an empty party instead of the first seven rangers.</para>
    /// </summary>
    private static PartyWalk ReadPlayers(IMemorySource mem, nuint party, nuint knownPlayerClass)
    {
        var result = new List<nuint>();
        var unreachable = new PartyWalk(WalkOutcome.Unreachable, result, 0, 0);

        // TryRead* throughout: a transient unreadable page must not masquerade as "the party is
        // empty", because the two lead to very different behaviour.
        if (!mem.TryReadPtr(party + (nuint)CharacterFormat.PartyPlayers, out nuint playersList)) return unreachable;
        if (playersList == 0) return unreachable;

        if (!mem.TryReadPtr(playersList + (nuint)Il2Cpp.ListItemsOffset, out nuint items)) return unreachable;
        if (!mem.TryReadI32(playersList + (nuint)Il2Cpp.ListSizeOffset, out int count)) return unreachable;

        // A readable list holding nothing is a real answer: the game is at the title screen.
        if (count == 0) return new PartyWalk(WalkOutcome.EmptyRoster, result, 0, 0);
        if (items == 0 || count < 0) return unreachable;
        count = Math.Min(count, GameFacts.MaxPartyListEntries);

        // Collect the candidate references first, then settle on what a Player looks like.
        var refs = new List<nuint>(count);
        for (int i = 0; i < count; i++)
        {
            nuint player = mem.ReadArrayRef(items, i);
            if (player != 0) refs.Add(player);
        }
        if (refs.Count == 0) return new PartyWalk(WalkOutcome.EmptyRoster, result, 0, 0);

        // Establish the Player class pointer from a list entry's own object header. That is the
        // authoritative answer: these objects are, by construction, whatever Party.players holds.
        // The swept pointer is only a fallback — it is the first thing in .data whose name reads
        // "Player", and if a second global-namespace type shares that name the sweep could pick
        // the wrong one and IsInstanceOf would then reject the entire real party.
        nuint playerClass = 0;
        foreach (var candidate in refs)
        {
            nuint klass = mem.ReadObjectClass(candidate);
            if (mem.ClassMatches(klass, GameFacts.PlayerTypeName, GameFacts.GameNamespace))
            {
                playerClass = klass;
                break;
            }
        }
        if (playerClass == 0) playerClass = knownPlayerClass;

        int rejected = 0;
        foreach (var candidate in refs)
        {
            if (playerClass != 0)
            {
                // Identity check: exact class match, nothing about the character's condition.
                if (mem.IsInstanceOf(candidate, playerClass)) result.Add(candidate);
                else rejected++;
                continue;
            }

            // No class pointer available at all. Fall back to a shape check, but only to
            // confirm the object is readable and structurally sane.
            var buf = new byte[CharacterFormat.ProbeSize];
            if (mem.Read(candidate, buf, buf.Length) == buf.Length && CharacterFormat.LooksLikePlayer(buf))
                result.Add(candidate);
            else rejected++;
        }

        return new PartyWalk(
            result.Count > 0 ? WalkOutcome.Players : WalkOutcome.Unreachable,
            result, playerClass, rejected);
    }

    /// <summary>
    /// Sweeps committed memory for objects shaped like a <c>Player</c>. When
    /// <paramref name="playerClass"/> is known every hit is additionally confirmed against it,
    /// which removes the false positives a shape-only scan would otherwise hand to the editor.
    /// </summary>
    private static List<nuint> StructuralScan(
        IMemorySource mem,
        nuint playerClass,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var hits = new List<nuint>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var (_, size) in regions) totalBytes += size;
        nuint scanned = 0;

        var buf = new byte[ChunkSize];

        double lastReported = -1;

        foreach (var (regionBase, regionSize) in regions)
        {
            for (nuint offset = 0; offset < regionSize;)
            {
                // Per chunk, not per region: a Unity process has committed regions of hundreds of
                // megabytes, and checking only at the region boundary makes Cancel do nothing for
                // tens of seconds.
                ct.ThrowIfCancellationRequested();

                int want = (int)Math.Min((nuint)ChunkSize, regionSize - offset);
                int read = mem.Read(regionBase + offset, buf, want);

                if (read < CharacterFormat.ProbeSize && want > PageSize)
                {
                    // ReadProcessMemory fails the whole span if any page in it is inaccessible,
                    // so a wide read tells us nothing about where the bad page is. Retry this
                    // same offset a page at a time rather than skipping up to a megabyte of
                    // perfectly readable memory that happened to share a chunk with it.
                    want = PageSize;
                    read = mem.Read(regionBase + offset, buf, want);
                }

                if (read < CharacterFormat.ProbeSize)
                {
                    nuint skip = (nuint)Math.Min((ulong)PageSize, (ulong)(regionSize - offset));
                    offset += skip;
                    scanned += skip;
                    Report();
                    continue;
                }

                for (int i = 0; i + CharacterFormat.ProbeSize <= read; i += 8)
                {
                    if (!CharacterFormat.LooksLikePlayer(buf.AsSpan(i))) continue;

                    nuint addr = regionBase + offset + (nuint)i;
                    if (playerClass != 0 && !mem.IsInstanceOf(addr, playerClass)) continue;
                    if (hits.Contains(addr)) continue;

                    hits.Add(addr);
                    if (hits.Count >= GameFacts.PartySlots) return hits;
                }

                nuint advance = (nuint)Math.Max(1, read - CharacterFormat.ProbeSize);
                offset += advance;
                scanned += advance;
                Report();
            }
        }

        return hits;

        // Throttled: a region full of unreadable pages would otherwise post one dispatcher
        // callback per 4 KB, which is a UI stall rather than a progress bar.
        void Report()
        {
            if (progress == null || totalBytes == 0) return;
            double fraction = Math.Min(1.0, (double)scanned / totalBytes);
            if (fraction < lastReported + 0.005 && fraction < 1.0) return;
            lastReported = fraction;
            progress.Report(fraction);
        }
    }
}
