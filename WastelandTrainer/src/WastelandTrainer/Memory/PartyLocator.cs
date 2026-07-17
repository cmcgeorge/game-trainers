using WastelandTrainer.Game;

namespace WastelandTrainer.Memory;

/// <summary>A located character record: its live process address, roster slot, and decoded view.</summary>
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

/// <summary>The located party: the base address of roster slot 0 plus every occupied member.</summary>
public sealed class LocatedParty
{
    public nuint RosterBase { get; }
    public IReadOnlyList<LocatedCharacter> Members { get; }

    public LocatedParty(nuint rosterBase, IReadOnlyList<LocatedCharacter> members)
    {
        RosterBase = rosterBase;
        Members = members;
    }
}

/// <summary>
/// Locates the Wasteland party roster inside the attached emulator's memory.
///
/// Wasteland's record allocation moves every session and has no stable static byte-run adjacent to
/// it to anchor to, so the roster is found by <b>structure</b>: it is an array of
/// <see cref="CharacterFormat.MaxSlots"/> contiguous <see cref="CharacterFormat.RecordSize"/>-byte
/// records where the occupied members pack from slot 0 (an occupied slot never follows an empty
/// one), at least one slot is occupied, and every occupied slot passes <see cref="IsValidCharacter"/>
/// — a 2..13-char NUL-terminated printable-ASCII name starting with a letter, seven attribute bytes
/// each in 1..100, a plausible MAXCON, current CON not exceeding MAXCON, a valid gender (0/1) and
/// nationality (0..4). Those extra field checks reject stray byte runs that merely look name-like.
///
/// The whole address space is swept and candidates are ranked by <see cref="Outranks"/>. Two kinds of
/// decoy roster haunt Wasteland's memory, and the ranking is built to reject both:
///
/// <list type="number">
/// <item><b>Headerless stale copies.</b> A deleted-but-not-cleared copy of old rangers lingers ~18 KB
/// before the live roster with no valid <b>party-state header</b> in front of it. A roster preceded by
/// a plausible header (<see cref="PartyHeader.IsPlausible"/>) always outranks one that is not, however
/// many members the headerless copy holds — so a lingering 2-member copy never beats a freshly-created
/// 1-member live party.</item>
/// <item><b>The pre-made template.</b> The four factory rangers (Hell Razor, Angela Deth, Thrasher,
/// Snake Vargas) are a read-only template that is <i>always</i> loaded, complete with its own valid
/// header frozen at the Ranger Center spawn (X 55, Y 62) — so header validity alone can't tell it from
/// the live party. But the game loads that template first and allocates the working/active party at a
/// fixed offset <i>above</i> it (confirmed at a constant <c>+0x4A31</c> across sessions, and the active
/// party sat above its decoy in every capture). So among header-backed candidates the one at the
/// <b>highest base address</b> is the live party.</item>
/// </list>
///
/// Ranking therefore is: header-backed beats headerless; among header-backed, higher base address wins
/// (the active party above its template); among headerless, more members wins (rejecting a one-record
/// fluke). Ties go to the first found. This pins the live party without a static anchor. See
/// <c>.docs\Wasteland-Reverse-Engineering.md §2</c>.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;      // salvage granularity
    private const int RosterBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;

    /// <summary>Finds the roster, or null if no party can be located (not attached, or not in-game yet).</summary>
    public static LocatedParty? Find(ProcessMemory mem, CancellationToken ct = default)
    {
        LocatedParty? best = null;
        bool bestHasHeader = false;
        byte[] headerScratch = new byte[CharacterFormat.PartyHeaderSize];
        int overlap = RosterBytes - 1;   // so a roster straddling a window edge is still seen whole
        byte[] buf = new byte[ChunkSize + overlap];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
                int read = mem.Read(start, buf, readLen);

                for (int i = 0; i + RosterBytes <= read; i++)
                {
                    if (!IsValidCharacter(buf, i)) continue;   // cheap gate: slot 0 must be a real member
                    var party = TryReadRoster(buf, i, start);
                    if (party == null) continue;
                    bool hasHeader = HasValidHeader(mem, party.RosterBase, headerScratch);
                    if (best == null || Outranks(hasHeader, party.Members.Count, (ulong)party.RosterBase,
                                                 bestHasHeader, best.Members.Count, (ulong)best.RosterBase))
                    {
                        best = party;
                        bestHasHeader = hasHeader;
                    }
                    // No early-out: the active party is the highest-addressed header-backed roster, so a
                    // later (higher) candidate can still beat the current best — the whole space is swept.
                }

                // ProcessMemory.Read is all-or-nothing: one unreadable page fails the whole chunk
                // read (read < readLen). Salvage the rest of the region page by page rather than
                // skipping up to a megabyte that may still hold the roster.
                if (read < readLen && want > PageSize)
                {
                    ScanByPage(mem, start, regionEnd, ct, ref best, ref bestHasHeader, headerScratch);
                    break;   // the remainder of the region has now been scanned page by page
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
        return best;
    }

    /// <summary>
    /// Candidate-ranking rule (exposed for testing). Returns true when candidate <c>(a…)</c> should
    /// replace incumbent <c>(b…)</c>:
    /// <list type="bullet">
    /// <item>A roster preceded by a valid party-state header always outranks one that is not — this keeps
    /// the locator off a headerless stale copy of deleted rangers, however many members it holds.</item>
    /// <item>Among two header-backed rosters, the one at the <b>higher base address</b> wins: the live
    /// party is allocated above the always-loaded pre-made template (see the type remarks), so the
    /// higher address is the active party even when the template holds more members.</item>
    /// <item>Among two headerless rosters, more members wins (a real party has several; a fluke has one).</item>
    /// </list>
    /// </summary>
    public static bool Outranks(bool aHasHeader, int aCount, ulong aBase, bool bHasHeader, int bCount, ulong bBase) =>
        aHasHeader != bHasHeader ? aHasHeader
        : aHasHeader ? aBase > bBase
        : aCount > bCount;

    /// <summary>
    /// Reads the 256-byte party-state header that precedes a candidate roster and reports whether it is
    /// plausible (<see cref="PartyHeader.IsPlausible"/>). A failed read — or a roster too close to
    /// address 0 to have a header before it — counts as no valid header.
    /// </summary>
    private static bool HasValidHeader(ProcessMemory mem, nuint rosterBase, byte[] scratch)
    {
        if (rosterBase < (nuint)CharacterFormat.PartyHeaderSize) return false;
        nuint headerBase = rosterBase - (nuint)CharacterFormat.PartyHeaderSize;
        if (mem.Read(headerBase, scratch, CharacterFormat.PartyHeaderSize) != CharacterFormat.PartyHeaderSize)
            return false;
        return PartyHeader.IsPlausible(scratch, 0);
    }

    /// <summary>
    /// Page-granular fallback for a region whose bulk read failed on an unreadable page: reads one
    /// <see cref="PageSize"/> page at a time (plus a roster-length overlap so a straddling roster is
    /// still seen whole) and simply skips any page that cannot be read, keeping the best candidate.
    /// </summary>
    private static void ScanByPage(ProcessMemory mem, nuint regionStart, nuint regionEnd, CancellationToken ct,
        ref LocatedParty? best, ref bool bestHasHeader, byte[] headerScratch)
    {
        int overlap = RosterBytes - 1;
        byte[] buf = new byte[PageSize + overlap];
        for (nuint start = regionStart; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)PageSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            // The overlap tail reaches into the following page(s); if one of those is unreadable the
            // all-or-nothing read fails outright and this page would be skipped even though a roster
            // wholly inside it is still readable. Retry without the overlap so the readable page is
            // salvaged (a roster straddling into the unreadable page genuinely can't be read anyway).
            if (read == 0 && readLen > want)
                read = mem.Read(start, buf, want);

            for (int i = 0; i + RosterBytes <= read; i++)
            {
                if (!IsValidCharacter(buf, i)) continue;
                var party = TryReadRoster(buf, i, start);
                if (party == null) continue;
                bool hasHeader = HasValidHeader(mem, party.RosterBase, headerScratch);
                if (best == null || Outranks(hasHeader, party.Members.Count, (ulong)party.RosterBase,
                                             bestHasHeader, best.Members.Count, (ulong)best.RosterBase))
                {
                    best = party;
                    bestHasHeader = hasHeader;
                }
            }

            start += (nuint)want;   // advance one page; overlap re-covers the seam
        }
    }

    // Validates the MaxSlots-slot window and, if it holds, returns its occupied members with live
    // addresses; otherwise null.
    private static LocatedParty? TryReadRoster(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            if (IsValidCharacter(buf, off))
            {
                if (seenEmpty) return null;    // occupied slot after an empty one: not a packed roster
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (IsEmptySlot(buf, off))
            {
                seenEmpty = true;
            }
            else
            {
                return null;                   // neither a member nor an empty slot: not a roster
            }
        }
        return slots.Count > 0 ? new LocatedParty(windowBase + (nuint)offset, slots) : null;
    }

    /// <summary>A roster slot is empty when its name field begins with a 0x00 pad byte.</summary>
    private static bool IsEmptySlot(byte[] buf, int off) => buf[off + CharacterFormat.OffName] == 0x00;

    /// <summary>
    /// The raw-buffer occupancy test the scan runs on every candidate offset. Delegates to
    /// <see cref="CharacterRecord.IsValidRecord"/> — the single shared occupancy test — so the scan
    /// gate and <see cref="CharacterRecord.IsOccupied"/> can never fall out of step: a well-formed
    /// name (2..13 printable ASCII, NUL-terminated, starting with a letter), seven attribute bytes in
    /// 1..100, a plausible MAXCON, current CON not exceeding MAXCON, gender 0/1 and nationality 0..4.
    /// </summary>
    private static bool IsValidCharacter(byte[] b, int o) => CharacterRecord.IsValidRecord(b, o);

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
