using System.Text;
using RedBaronTrainer.Game;

namespace RedBaronTrainer.Memory;

/// <summary>Everything one successful locate resolved.</summary>
public sealed class LocatedGame
{
    /// <summary>Which executable is in the emulator right now.</summary>
    public GameModule Module { get; init; }

    /// <summary>Host address that guest linear 0 maps to inside the emulator.</summary>
    public nuint GuestZero { get; init; }

    /// <summary>Host address of the module's data group (DS:0000).</summary>
    public nuint Dgroup { get; init; }

    /// <summary>Guest segment the data group sits at, for the status line and the docs.</summary>
    public int DgroupSegment { get; init; }

    /// <summary>How many corroborating literals landed on their own DS offsets (out of four).</summary>
    public int ValidatorsMatched { get; init; }

    /// <summary>Host address of the live realism panel, or 0 when it did not validate.</summary>
    public nuint RealismAddress { get; init; }

    /// <summary>Shell only: host address of roster slot 0, or 0 when it did not validate.</summary>
    public nuint RosterAddress { get; init; }

    /// <summary>Shell only: host address of the career currently being flown, or 0.</summary>
    public nuint ActivePilotAddress { get; init; }

    /// <summary>Simulator only: host address of the joystick/rudder enable flag, or 0.</summary>
    public nuint JoystickFlagAddress { get; init; }

    /// <summary>Simulator only: host address of the flag's second copy, or 0.</summary>
    public nuint JoystickMirrorAddress { get; init; }

    /// <summary>Set when another module's data group also corroborated, so this pick is a judgement call.</summary>
    public bool Ambiguous { get; init; }

    public long ElapsedMs { get; init; }

    public nuint AtOffset(int dsOffset) => Dgroup + (nuint)dsOffset;
}

/// <summary>
/// Finds Red Baron inside the attached DOSBox/DOSBox-X process.
///
/// <para><b>How.</b> Both executables are 16-bit Borland Turbo C++ builds whose startup code loads a
/// single data group into <c>DS</c> and never changes it, so every global has a fixed <c>DS:</c>
/// offset and only the load segment moves between runs. The locator sweeps guest RAM for one literal
/// that lives at a known offset in that data group, subtracts the offset to get DS:0000, and then
/// requires at least two of four further literals to land on their own offsets before it believes
/// the candidate. A candidate that is not paragraph-aligned is rejected outright: a real DOS segment
/// base always is.</para>
///
/// <para><b>Two programs, not one.</b> <c>BARON.COM</c> chains <c>PS.EXE</c> (menus, career, roster)
/// which chains <c>RB.EXE</c> (the sim) and gets chained back to when the mission ends. They are
/// separate processes inside the guest with unrelated data groups, so the locator tries both anchor
/// sets and reports which one it found — the trainer's tabs enable and disable from that.</para>
///
/// <para><b>Both at once.</b> DOS does not scrub memory it frees, so in principle the previous
/// program's data group can still be lying in guest RAM. In practice the two overlap — <c>PS.EXE</c>
/// is the smaller image but its 64 KB data segment covers where <c>RB.EXE</c> keeps its own, and
/// <c>RB.EXE</c>'s larger image covers <c>PS.EXE</c> entirely — so the stale copy is normally gone
/// by the time anyone looks. "Normally" is not "always", so the locator keeps sweeping past the
/// first candidate that corroborates and takes the better-attested of everything it finds. It
/// cannot do better than that: nothing in guest RAM says which of two identical-looking data groups
/// DOS considers live. What it does instead is admit the doubt — <see cref="LocatedGame.Ambiguous"/>
/// is set whenever a second candidate stood up, and the status line tells the user to press
/// Reload if the wrong tab went live.</para>
///
/// <para><b>Guest linear 0.</b> Structures are addressed as <c>DS:offset</c>, which only becomes a
/// host address once we know where guest linear 0 sits inside the emulator's allocation. DOSBox pads
/// that allocation, so the base of the region is not it; the locator pins it on the emulated BIOS
/// data area instead (40:0000 holds the COM1 port 0x03F8 and 40:0013 the 640 KB size word).</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB sweep window
    private const int PageSize = 0x1000;

    /// <summary>Guest RAM is at least this big for any usable DOSBox configuration (memsize >= 1 MB).</summary>
    private const int MinGuestRegionBytes = 1 << 20;

    /// <summary>A 16-bit data group cannot be larger than one segment.</summary>
    private const int DataGroupSize = 0x10000;

    /// <summary>Locates the game, or returns null with a reason in <paramref name="status"/>.</summary>
    public static LocatedGame? Find(IMemorySource mem, out string status, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int anchorsSeen = 0;
        string bestReason = "";
        LocatedGame? best = null;
        bool ambiguous = false;

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < MinGuestRegionBytes) continue;
            if (!TryFindGuestZero(mem, region, out nuint guestZero)) continue;

            foreach (var candidate in Candidates())
            {
                ct.ThrowIfCancellationRequested();
                var anchor = Encoding.ASCII.GetBytes(candidate.AnchorText);

                foreach (nuint hit in Sweep(mem, region, anchor, ct))
                {
                    anchorsSeen++;
                    if (hit < (nuint)candidate.AnchorOffset) continue;
                    nuint dgroup = hit - (nuint)candidate.AnchorOffset;
                    if (dgroup < guestZero)
                    {
                        bestReason = "found the anchor text, but too close to the start of guest RAM "
                                   + "for a data group to sit in front of it";
                        continue;
                    }

                    long guestLinear = (long)(dgroup - guestZero);
                    if (guestLinear % 16 != 0)
                    {
                        // Not a paragraph boundary, so not a DOS segment base - this is a copy of the
                        // literal in a scratch buffer, not the data group itself.
                        continue;
                    }

                    int matched = CountValidators(mem, dgroup, candidate.Validators);
                    if (matched < 2)
                    {
                        bestReason = $"found the {candidate.Module} anchor but only {matched} of "
                                   + $"{candidate.Validators.Length} corroborating literals matched";
                        continue;
                    }

                    var located = Resolve(mem, candidate, dgroup, guestZero, matched, sw);
                    if (best == null)
                    {
                        best = located;
                    }
                    else
                    {
                        // A second data group corroborated. One of the two is a leftover the
                        // chain-loader has not overwritten yet - and it can be a second copy of the
                        // *same* module, since PS.EXE is chained to twice around every mission. Take
                        // the better-attested and say the pick was a judgement call either way;
                        // there is no signal in guest RAM that says which copy DOS considers live.
                        ambiguous = true;
                        if (located.ValidatorsMatched > best.ValidatorsMatched) best = located;
                    }
                }
            }
        }

        if (best != null)
        {
            var result = ambiguous ? Clone(best, ambiguous: true) : best;
            status = $"Attached to Red Baron's {Describe(result.Module)} at DS {result.DgroupSegment:X4} "
                   + $"in {sw.ElapsedMilliseconds} ms ({result.ValidatorsMatched}/4 validators)"
                   + (ambiguous ? ", but the other program's data group is still in guest RAM - "
                                + "if the wrong tab is live, press Reload." : ".");
            return result;
        }

        status = anchorsSeen == 0
            ? "Red Baron was not found in that process. Start it with BARON.COM in DOSBox and let the "
            + "main menu appear, then Attach."
            : bestReason.Length > 0
                ? $"Red Baron's data group was not confirmed: {bestReason}."
                : $"Red Baron's data group was not confirmed ({anchorsSeen} anchor candidate(s) examined).";
        return null;
    }

    private static LocatedGame Clone(LocatedGame from, bool ambiguous) => new()
    {
        Module = from.Module,
        GuestZero = from.GuestZero,
        Dgroup = from.Dgroup,
        DgroupSegment = from.DgroupSegment,
        ValidatorsMatched = from.ValidatorsMatched,
        RealismAddress = from.RealismAddress,
        RosterAddress = from.RosterAddress,
        ActivePilotAddress = from.ActivePilotAddress,
        JoystickFlagAddress = from.JoystickFlagAddress,
        JoystickMirrorAddress = from.JoystickMirrorAddress,
        ElapsedMs = from.ElapsedMs,
        Ambiguous = ambiguous,
    };

    private static string Describe(GameModule module) => module switch
    {
        GameModule.Shell => "shell (PS.EXE)",
        GameModule.Simulator => "simulator (RB.EXE)",
        _ => "unknown module",
    };

    private readonly record struct Candidate(
        GameModule Module, string AnchorText, int AnchorOffset, (string Text, int Offset)[] Validators);

    private static IEnumerable<Candidate> Candidates()
    {
        yield return new Candidate(GameModule.Simulator, GameFacts.SimAnchorText,
            GameFacts.SimAnchorOffset, GameFacts.SimValidators);
        yield return new Candidate(GameModule.Shell, GameFacts.ShellAnchorText,
            GameFacts.ShellAnchorOffset, GameFacts.ShellValidators);
    }

    /// <summary>
    /// Re-runs the structure resolution against a data group that is already known, without the
    /// sweep.
    ///
    /// <para>This is not redundant with <see cref="Find"/>. Both executables put the interesting
    /// structures in BSS — the roster at <c>DS:0x5610</c>, the realism panel at <c>DS:0x4FBE</c> —
    /// which is zeroed at load, while the literals the sweep anchors on live in initialised data and
    /// are valid the instant the image is mapped. So a locate can legitimately succeed a moment
    /// before the shell has read <c>ROSTER.DAT</c>, and every structure comes back unresolved. The
    /// anchor never stops matching after that, so without this the trainer would sit on a perfectly
    /// good data group with an empty Pilots tab for the whole life of the process.</para>
    /// </summary>
    public static LocatedGame Reresolve(IMemorySource mem, LocatedGame game)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(game);
        foreach (var candidate in Candidates())
        {
            if (candidate.Module != game.Module) continue;
            var fresh = Resolve(mem, candidate, game.Dgroup, game.GuestZero, game.ValidatorsMatched,
                System.Diagnostics.Stopwatch.StartNew());
            return game.Ambiguous ? Clone(fresh, ambiguous: true) : fresh;
        }
        return game;
    }

    /// <summary>True when <paramref name="game"/> still has a structure this module should expose but did not resolve.</summary>
    public static bool HasUnresolvedStructures(LocatedGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Module switch
        {
            GameModule.Shell => game.RealismAddress == 0 || game.RosterAddress == 0 || game.ActivePilotAddress == 0,
            GameModule.Simulator => game.JoystickFlagAddress == 0,
            _ => false,
        };
    }

    /// <summary>Confirms the anchor is still where a previous locate left it — the cheap liveness check.</summary>
    public static bool AnchorStillMatches(IMemorySource mem, LocatedGame game)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(game);
        var (text, offset) = game.Module == GameModule.Simulator
            ? (GameFacts.SimAnchorText, GameFacts.SimAnchorOffset)
            : (GameFacts.ShellAnchorText, GameFacts.ShellAnchorOffset);
        var want = Encoding.ASCII.GetBytes(text);
        var got = mem.Read(game.Dgroup + (nuint)offset, want.Length);
        return got.Length == want.Length && got.AsSpan().SequenceEqual(want);
    }

    // --- step 1: sweep for the anchor -------------------------------------------

    private static IEnumerable<nuint> Sweep(IMemorySource mem, MemoryRegion region, byte[] anchor,
        CancellationToken ct)
    {
        int overlap = anchor.Length - 1;
        byte[] buf = new byte[ChunkSize + overlap];
        nuint regionEnd = region.Base + region.Size;

        for (nuint start = region.Base; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)ChunkSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            if (read >= want)
            {
                foreach (int i in Matches(buf, read, anchor)) yield return start + (nuint)i;
                start += (nuint)want;
                continue;
            }

            // The window did not come back whole. ReadProcessMemory is all-or-nothing, so this is
            // normally read == 0 for a window that merely *contains* one unreadable page - stepping
            // past it would skip up to a megabyte of perfectly readable RAM without scanning it, and
            // the anchor is exactly the kind of thing that then goes missing. Salvage the window a
            // page at a time instead, and only skip the pages that genuinely refuse. The partial
            // `read` is deliberately not scanned here: the page loop covers the same range, and
            // scanning both would report every hit in it twice.
            nuint windowEnd = start + (nuint)want;
            for (nuint page = start; page < windowEnd;)
            {
                ct.ThrowIfCancellationRequested();
                nuint pageRemaining = regionEnd - page;
                int pageLen = (int)Math.Min((nuint)(PageSize + overlap), pageRemaining);
                int pageRead = mem.Read(page, buf, pageLen);
                if (pageRead == 0 && pageLen > PageSize)
                {
                    // The overlap reached into the next page, and that is the dead one - so this
                    // page failed only by association. Retry it alone. A match straddling the seam
                    // is unreachable either way, but the 4 KB in front of the hole is not.
                    pageRead = mem.Read(page, buf, PageSize);
                }
                foreach (int i in Matches(buf, pageRead, anchor)) yield return page + (nuint)i;
                page += (nuint)Math.Min((nuint)PageSize, pageRemaining);
            }
            start = windowEnd;
        }
    }

    /// <summary>Offsets of <paramref name="anchor"/> within the first <paramref name="length"/> bytes of the buffer.</summary>
    private static IEnumerable<int> Matches(byte[] buf, int length, byte[] anchor)
    {
        for (int i = 0; i + anchor.Length <= length; i++)
        {
            if (buf[i] != anchor[0]) continue;
            bool match = true;
            for (int j = 1; j < anchor.Length; j++)
            {
                if (buf[i + j] != anchor[j]) { match = false; break; }
            }
            if (match) yield return i;
        }
    }

    // --- step 2: corroborate --------------------------------------------------

    private static int CountValidators(IMemorySource mem, nuint dgroup, (string Text, int Offset)[] validators)
    {
        int matched = 0;
        foreach (var (text, offset) in validators)
        {
            var want = Encoding.ASCII.GetBytes(text);
            var got = mem.Read(dgroup + (nuint)offset, want.Length);
            if (got.Length == want.Length && got.AsSpan().SequenceEqual(want)) matched++;
        }
        return matched;
    }

    // --- step 3: pin guest linear 0 ---------------------------------------------

    /// <summary>
    /// Finds where guest linear 0 lands in the host process by looking for the emulated BIOS data
    /// area near the start of the guest allocation: 40:0000 holds the COM1 I/O port (0x03F8) and
    /// 40:0013 the conventional-memory size in KB (640).
    /// </summary>
    private static bool TryFindGuestZero(IMemorySource mem, MemoryRegion region, out nuint guestZero)
    {
        guestZero = 0;
        const int Window = 0x2000;   // the pad DOSBox adds is a handful of bytes, not kilobytes
        var buf = mem.Read(region.Base, Window);
        // The highest byte read below is buf[i + 0x14], so i + 0x14 must be a valid index.
        for (int i = 0x400; i + 0x14 < buf.Length; i++)
        {
            if (buf[i] != 0xF8 || buf[i + 1] != 0x03) continue;
            int kb = buf[i + 0x13] | (buf[i + 0x14] << 8);
            if (kb != 640) continue;
            guestZero = region.Base + (nuint)(i - 0x400);
            return true;
        }
        return false;
    }

    // --- step 4: resolve the structures -----------------------------------------

    private static LocatedGame Resolve(IMemorySource mem, Candidate candidate, nuint dgroup,
        nuint guestZero, int validators, System.Diagnostics.Stopwatch sw)
    {
        int segment = (int)(((long)(dgroup - guestZero)) / 16);

        nuint realism = 0, roster = 0, activePilot = 0, joystick = 0, joystickMirror = 0;

        if (candidate.Module == GameModule.Shell)
        {
            if (Fits(GameFacts.ShellRealismOffset, GameFacts.RealismBlockSize) &&
                RealismSettings.LooksPlausible(mem.Read(dgroup + GameFacts.ShellRealismOffset, GameFacts.RealismBlockSize)))
            {
                realism = dgroup + GameFacts.ShellRealismOffset;
            }

            int rosterBytes = GameFacts.RosterSlots * GameFacts.PilotRecordSize;
            if (Fits(GameFacts.RosterOffset, rosterBytes))
            {
                var buf = mem.Read(dgroup + GameFacts.RosterOffset, rosterBytes);
                if (buf.Length == rosterBytes && PilotRecord.IsPlausibleRoster(buf))
                    roster = dgroup + GameFacts.RosterOffset;
            }

            if (Fits(GameFacts.ActivePilotOffset, GameFacts.PilotRecordSize))
            {
                var buf = mem.Read(dgroup + GameFacts.ActivePilotOffset, GameFacts.PilotRecordSize);
                if (buf.Length == GameFacts.PilotRecordSize && PilotRecord.IsOccupiedSlot(buf, 0))
                    activePilot = dgroup + GameFacts.ActivePilotOffset;
            }
        }
        else if (candidate.Module == GameModule.Simulator)
        {
            // The flag is a plain 0/1 byte. Requiring both copies to be in range and to agree keeps a
            // shifted data group from presenting a toggle that writes into unrelated memory.
            var a = mem.Read(dgroup + GameFacts.SimJoystickFlagOffset, 1);
            var b = mem.Read(dgroup + GameFacts.SimJoystickFlagMirrorOffset, 1);
            if (a.Length == 1 && b.Length == 1 && a[0] <= 1 && b[0] <= 1 && a[0] == b[0])
            {
                joystick = dgroup + GameFacts.SimJoystickFlagOffset;
                joystickMirror = dgroup + GameFacts.SimJoystickFlagMirrorOffset;
            }
        }

        return new LocatedGame
        {
            Module = candidate.Module,
            GuestZero = guestZero,
            Dgroup = dgroup,
            DgroupSegment = segment,
            ValidatorsMatched = validators,
            RealismAddress = realism,
            RosterAddress = roster,
            ActivePilotAddress = activePilot,
            JoystickFlagAddress = joystick,
            JoystickMirrorAddress = joystickMirror,
            ElapsedMs = sw.ElapsedMilliseconds,
        };

        static bool Fits(int offset, int length) => offset >= 0 && offset + length <= DataGroupSize;
    }
}
