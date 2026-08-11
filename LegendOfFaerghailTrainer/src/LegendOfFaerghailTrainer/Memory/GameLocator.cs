using System.Text;
using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.Memory;

/// <summary>A located character record: its live process address, slot index, and decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public LocatedCharacter(nuint address, int slot, CharacterRecord record)
    {
        Address = address;
        Slot = slot;
        Record = record;
    }

    public override string ToString() => $"{Record.Name} @ 0x{(ulong)Address:X}";
}

/// <summary>
/// Result of the roster/party adjacency cross-check. The three states are kept apart on purpose:
/// "there was no roster pointer to check" is reassuring, "the two pointers disagree" is not, and
/// reporting both as "not checked" would hide the one signal that says the locate may be wrong.
/// </summary>
public enum AdjacencyResult
{
    /// <summary>No roster pointer resolved, so there was nothing to compare against.</summary>
    NotChecked,

    /// <summary>Party and roster are exactly 32 x 410 + 2 bytes apart, as they should be.</summary>
    Holds,

    /// <summary>The two pointers resolved but are not adjacent — one of them is suspect.</summary>
    Failed,
}

/// <summary>Everything the locator resolved in one attach.</summary>
public sealed class LocatedGame
{
    /// <summary>Host address of the game's data group (DS:0000).</summary>
    public nuint DgroupAddress { get; init; }

    /// <summary>Host address that guest linear 0 maps to inside the emulator.</summary>
    public nuint GuestZero { get; init; }

    /// <summary>Host address of party slot 0.</summary>
    public nuint PartyAddress { get; init; }

    /// <summary>Host address of roster slot 0, or 0 if the roster pointer did not validate.</summary>
    public nuint RosterAddress { get; init; }

    /// <summary>How many of the four corroborating literals lined up at their own DGROUP offsets.</summary>
    public int ValidatorsMatched { get; init; }

    /// <summary>Outcome of the roster/party adjacency cross-check.</summary>
    public AdjacencyResult Adjacency { get; init; }

    /// <summary>Occupied party members, in slot order.</summary>
    public IReadOnlyList<LocatedCharacter> Party { get; init; } = Array.Empty<LocatedCharacter>();

    /// <summary>Occupied roster entries, in slot order (empty when the roster did not resolve).</summary>
    public IReadOnlyList<LocatedCharacter> Roster { get; init; } = Array.Empty<LocatedCharacter>();

    public long ElapsedMs { get; init; }
}

/// <summary>
/// Finds Legend of Faerghail's live party and roster inside the attached emulator.
///
/// <para><b>How.</b> <c>LOF.EXE</c> is a Microsoft C large-model build, so it has exactly one data
/// group and every global has a constant <c>DS:</c> offset — only the load segment moves between
/// sessions. The party and roster buffers themselves are heap allocations at addresses that change
/// every run, but the game keeps far pointers to both in that data group
/// (<see cref="GameFacts.PartyPointerOffset"/> and <see cref="GameFacts.RosterPointerOffset"/>),
/// so the whole locate is: find DGROUP, then follow two pointers.</para>
///
/// <para>DGROUP is found by sweeping for the character sheet's abilities caption
/// (<see cref="GameFacts.PrimaryAnchorText"/>), which sits at DGROUP:0xF371 and occurs exactly once
/// in a live 16 MB guest. A candidate is accepted only when at least two of four further literals
/// also land on their own DGROUP offsets.</para>
///
/// <para>Following a far pointer needs one extra step that a native-Windows target would not: the
/// pointer holds a <i>guest</i> segment:offset, so the locator first pins where guest linear 0 sits
/// in the host process by finding the emulated BIOS data area (40:0000 holds the COM1 port 0x03F8
/// and 40:0013 the 640 KB conventional-memory size). DOSBox pads its guest allocation, so this is
/// not simply the base of the region.</para>
///
/// <para>There is deliberately <b>no blind structural fallback</b>. Six contiguous 410-byte records
/// is a shape that 16 MB of guest RAM will eventually match by accident, and a confident wrong
/// address would turn one "Max everything" click into a write into unrelated memory. What the
/// locator does instead is cross-check: the roster and party arrays are adjacent in one allocation,
/// so <c>party - roster</c> must come out as exactly 32 x 410 + 2 bytes.</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB sweep window
    private const int PageSize = 0x1000;

    /// <summary>Guest RAM is at least this big for any usable DOSBox configuration (memsize >= 1 MB).</summary>
    private const int MinGuestRegionBytes = 1 << 20;

    private static readonly byte[] PrimaryAnchor = Encoding.ASCII.GetBytes(GameFacts.PrimaryAnchorText);

    /// <summary>Locates the game, or returns null with a reason.</summary>
    public static LocatedGame? Find(IMemorySource mem, out string status, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        status = "";
        int anchorsSeen = 0;
        int bestValidators = -1;
        string bestReason = "";

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < MinGuestRegionBytes) continue;

            foreach (nuint hit in SweepForAnchor(mem, region, ct))
            {
                anchorsSeen++;
                if (hit < (nuint)GameFacts.PrimaryAnchorOffset
                    || hit - (nuint)GameFacts.PrimaryAnchorOffset < region.Base)
                {
                    // The literal turned up too close to the start of the region for the data group
                    // to fit in front of it, so it is not this game's copy. Say so rather than
                    // leaving the failure message blank.
                    if (bestValidators < 0)
                        bestReason = "found the anchor text, but too close to the start of the region "
                                   + "for the data group to sit in front of it";
                    continue;
                }
                nuint dgroup = hit - (nuint)GameFacts.PrimaryAnchorOffset;

                int validators = CountValidators(mem, dgroup);
                if (validators < 2)
                {
                    if (validators > bestValidators)
                    {
                        bestValidators = validators;
                        bestReason = $"found the anchor but only {validators} of 4 corroborating literals matched";
                    }
                    continue;
                }

                // A candidate that got this far outranks any weaker anchor hit found later, so its
                // reason must not be clobbered by one - otherwise the user is told "0 of 4 literals
                // matched" when what actually happened was a failure much further along.
                bestValidators = validators;

                if (!TryFindGuestZero(mem, region, out nuint guestZero))
                {
                    bestReason = "found the data group but could not pin the emulator's guest memory base";
                    continue;
                }

                var located = Resolve(mem, dgroup, guestZero, validators, sw, out string why);
                if (located != null)
                {
                    string note = located.Adjacency switch
                    {
                        AdjacencyResult.Holds => ", roster adjacency holds",
                        AdjacencyResult.Failed => ", but the roster pointer is NOT adjacent to the party "
                                                + "- the roster was not opened; treat the party with care",
                        _ => "",
                    };
                    status = $"Located in {sw.ElapsedMilliseconds} ms ({validators}/4 validators{note}).";
                    return located;
                }
                bestReason = why;
            }
        }

        if (anchorsSeen == 0)
            status = "Legend of Faerghail was not found in that process. Start the game with START.BAT "
                   + "in DOSBox and play past the intro, then Attach.";
        else if (bestReason.Length == 0)
            status = $"Legend of Faerghail's data group was not confirmed ({anchorsSeen} anchor "
                   + "candidate(s) examined, none usable).";
        else
            status = $"Legend of Faerghail's data group was not confirmed: {bestReason}.";
        return null;
    }

    // --- step 1: sweep for the anchor -------------------------------------------

    private static IEnumerable<nuint> SweepForAnchor(IMemorySource mem, MemoryRegion region, CancellationToken ct)
    {
        int overlap = PrimaryAnchor.Length - 1;
        byte[] buf = new byte[ChunkSize + overlap];
        nuint regionEnd = region.Base + region.Size;

        for (nuint start = region.Base; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)ChunkSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            for (int i = 0; i + PrimaryAnchor.Length <= read; i++)
            {
                if (buf[i] != PrimaryAnchor[0]) continue;
                bool match = true;
                for (int j = 1; j < PrimaryAnchor.Length; j++)
                {
                    if (buf[i + j] != PrimaryAnchor[j]) { match = false; break; }
                }
                if (match) yield return start + (nuint)i;
            }

            // A short read means an unreadable page inside the window; step past whatever did come
            // back, rounded up to the page that failed, and carry on rather than abandoning the
            // rest of the region.
            //
            // In practice against a live process this is always one page: ProcessMemory.Read
            // returns 0 rather than a partial count when ReadProcessMemory fails, so `read` is
            // only ever 0 or the whole window. The rounding matters for any source that does
            // report a partial count - the harness's fake guest is one - where restarting a single
            // page in would re-read and re-scan the same span on every iteration.
            int advance = read >= want
                ? want
                : Math.Max(PageSize, (read / PageSize + 1) * PageSize);
            start += (nuint)advance;
        }
    }

    // --- step 2: corroborate --------------------------------------------------

    private static int CountValidators(IMemorySource mem, nuint dgroup)
    {
        int matched = 0;
        foreach (var (text, offset) in GameFacts.SecondaryAnchors)
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

    // --- step 4: follow the pointers --------------------------------------------

    private static LocatedGame? Resolve(IMemorySource mem, nuint dgroup, nuint guestZero,
        int validators, System.Diagnostics.Stopwatch sw, out string why)
    {
        why = "";
        if (!TryReadFarPointer(mem, dgroup + (nuint)GameFacts.PartyPointerOffset, guestZero, out nuint partyAddr, out long partyGuest))
        {
            why = "the party pointer at DS:0x0030 is not set yet (start a game before attaching)";
            return null;
        }

        int partyBytes = CharacterFormat.PartySlots * CharacterFormat.RecordSize;
        var partyBuf = mem.Read(partyAddr, partyBytes);
        if (partyBuf.Length != partyBytes)
        {
            why = "the party pointer does not point at readable memory";
            return null;
        }
        if (!IsPlausibleArray(partyBuf, CharacterFormat.PartySlots))
        {
            why = "the six records the party pointer reaches do not look like characters";
            return null;
        }

        nuint rosterAddr = 0;
        var adjacency = AdjacencyResult.NotChecked;
        IReadOnlyList<LocatedCharacter> roster = Array.Empty<LocatedCharacter>();
        if (TryReadFarPointer(mem, dgroup + (nuint)GameFacts.RosterPointerOffset, guestZero, out nuint ra, out long rosterGuest))
        {
            int rosterBytes = CharacterFormat.RosterSlots * CharacterFormat.RecordSize;
            var rosterBuf = mem.Read(ra, rosterBytes);
            if (rosterBuf.Length == rosterBytes && IsPlausibleArray(rosterBuf, CharacterFormat.RosterSlots))
            {
                // The two arrays live in one allocation. If they are not adjacent, one of the two
                // pointers is not what it is supposed to be — surface the roster's records read-only
                // rather than handing back a writable tab aimed at an address that failed its check.
                if (partyGuest - rosterGuest == GameFacts.RosterToPartyDelta)
                {
                    adjacency = AdjacencyResult.Holds;
                    rosterAddr = ra;
                    roster = ReadSlots(rosterBuf, ra, CharacterFormat.RosterSlots);
                }
                else
                {
                    adjacency = AdjacencyResult.Failed;
                }
            }
        }

        return new LocatedGame
        {
            DgroupAddress = dgroup,
            GuestZero = guestZero,
            PartyAddress = partyAddr,
            RosterAddress = rosterAddr,
            ValidatorsMatched = validators,
            Adjacency = adjacency,
            Party = ReadSlots(partyBuf, partyAddr, CharacterFormat.PartySlots),
            Roster = roster,
            ElapsedMs = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>Reads a 16-bit DOS far pointer and converts <c>seg:off</c> to a host address.</summary>
    private static bool TryReadFarPointer(IMemorySource mem, nuint at, nuint guestZero,
        out nuint hostAddress, out long guestLinear)
    {
        hostAddress = 0;
        guestLinear = 0;
        var p = mem.Read(at, 4);
        if (p.Length != 4) return false;
        int off = p[0] | (p[1] << 8);
        int seg = p[2] | (p[3] << 8);
        if (seg == 0 && off == 0) return false;
        guestLinear = (long)seg * 16 + off;
        // Below the BIOS data area is not a pointer the game would ever hand out. There is no upper
        // bound worth testing: two 16-bit halves cannot resolve past 0x10FFEF anyway.
        if (guestLinear < 0x500) return false;
        hostAddress = guestZero + (nuint)guestLinear;
        return true;
    }

    /// <summary>
    /// An array of records is plausible when every slot is either a valid record or an empty slot,
    /// and no occupied slot follows an empty one (both arrays pack from slot 0).
    /// </summary>
    private static bool IsPlausibleArray(byte[] buf, int slots)
    {
        bool seenEmpty = false;
        for (int i = 0; i < slots; i++)
        {
            int off = i * CharacterFormat.RecordSize;
            if (CharacterRecord.IsValidRecord(buf, off))
            {
                if (seenEmpty) return false;
            }
            else if (CharacterRecord.IsEmptySlot(buf, off))
            {
                seenEmpty = true;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static List<LocatedCharacter> ReadSlots(byte[] buf, nuint baseAddress, int slots)
    {
        var list = new List<LocatedCharacter>();
        for (int i = 0; i < slots; i++)
        {
            int off = i * CharacterFormat.RecordSize;
            if (!CharacterRecord.IsValidRecord(buf, off)) continue;
            list.Add(new LocatedCharacter(baseAddress + (nuint)off, i, new CharacterRecord(buf, off)));
        }
        return list;
    }

    /// <summary>Re-reads one record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(IMemorySource mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
