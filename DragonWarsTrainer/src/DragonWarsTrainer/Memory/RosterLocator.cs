using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.Memory;

/// <summary>A located character record: its live process address and a decoded view.</summary>
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
/// Locates the Dragon Wars party roster inside the attached emulator's memory.
///
/// Two strategies, tried in order:
///
/// 1. <b>Anchor</b> — the roster sits a fixed delta past DATA1's chunk-0 header, a byte run that
///    loads verbatim into guest RAM (see <see cref="RosterFormat.AnchorBytes"/>). Fast and exact,
///    but the header is data-file/version specific, so it is absent for some DOS releases.
///
/// 2. <b>Structural</b> — a fallback used when the anchor is not present. The roster is an array of
///    <see cref="RosterFormat.MaxSlots"/> contiguous <see cref="RosterFormat.RecordSize"/>-byte
///    records: the occupied members pack from slot 0, followed by empty (0x00/0xFF) slots. This
///    scans for a window that matches that shape exactly (every slot is either a validated
///    character or an empty slot, occupied slots form a leading run, at least one is occupied),
///    which is specific enough to pin the live roster without a static anchor.
///
/// Either way, only occupied slots are returned; empty slots are skipped.
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private const int RosterBytes = RosterFormat.MaxSlots * RosterFormat.RecordSize;

    /// <summary>
    /// Finds the roster and returns every occupied character slot, or an empty list if no party
    /// can be located (not attached to Dragon Wars, or the game isn't loaded past the title yet).
    /// Tries the exact anchor first, then falls back to a structural scan.
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        var byAnchor = FindByAnchor(mem, ct);
        if (byAnchor.Count > 0) return byAnchor;
        return FindByStructure(mem, ct);
    }

    // --- strategy 1: exact anchor -------------------------------------------
    private static List<LocatedCharacter> FindByAnchor(ProcessMemory mem, CancellationToken ct)
    {
        foreach (var anchor in FindAnchors(mem, RosterFormat.AnchorBytes, ct))
        {
            nuint rosterBase = anchor + (nuint)RosterFormat.RosterAnchorDelta;
            var slots = ReadRoster(mem, rosterBase);
            if (slots.Count == 0) continue;    // anchor matched but no live party behind it
            return slots;                      // first anchor with a real party wins
        }
        return new List<LocatedCharacter>();
    }

    private static List<LocatedCharacter> ReadRoster(ProcessMemory mem, nuint rosterBase)
    {
        var slots = new List<LocatedCharacter>();
        for (int i = 0; i < RosterFormat.MaxSlots; i++)
        {
            nuint addr = rosterBase + (nuint)(i * RosterFormat.RecordSize);
            var buf = mem.Read(addr, RosterFormat.RecordSize);
            if (buf.Length != RosterFormat.RecordSize) break;
            var rec = new CharacterRecord(buf);
            if (rec.IsOccupied) slots.Add(new LocatedCharacter(addr, i, rec));
        }
        return slots;
    }

    // --- strategy 2: structural scan ----------------------------------------
    // Walks every readable region looking for a MaxSlots-record window shaped like a roster.
    private static List<LocatedCharacter> FindByStructure(ProcessMemory mem, CancellationToken ct)
    {
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
                    var slots = TryReadRoster(buf, i, start);
                    if (slots != null) return slots;
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
        return new List<LocatedCharacter>();
    }

    // Validates the MaxSlots-slot window at <paramref name="offset"/> as a roster and, if it holds,
    // returns its occupied members with live addresses; otherwise null.
    private static List<LocatedCharacter>? TryReadRoster(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < RosterFormat.MaxSlots; i++)
        {
            int off = offset + i * RosterFormat.RecordSize;
            if (IsValidCharacter(buf, off))
            {
                if (seenEmpty) return null;     // occupied slot after an empty one: not a packed roster
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (IsEmptySlot(buf, off))
            {
                seenEmpty = true;
            }
            else
            {
                return null;                    // neither a member nor an empty slot: not a roster
            }
        }
        return slots.Count > 0 ? slots : null;
    }

    /// <summary>A roster slot is empty when its name field begins with 0x00 or 0xFF padding.</summary>
    private static bool IsEmptySlot(byte[] buf, int off)
    {
        byte b = buf[off + RosterFormat.OffName];
        return b == 0x00 || b == 0xFF;
    }

    /// <summary>
    /// Stricter than <see cref="CharacterRecord.IsOccupied"/>: requires a well-formed Dragon Wars
    /// name (2..12 letters/spaces, high-bit set on all but the terminator), four current+base
    /// attribute pairs in 1..99, plausible Health, and a sane level — enough to reject the many
    /// stray byte runs that merely start with a letter.
    /// </summary>
    private static bool IsValidCharacter(byte[] b, int o)
    {
        int len = 0;
        for (int i = 0; i < RosterFormat.NameLength; i++)
        {
            byte ch = b[o + RosterFormat.OffName + i];
            if (ch == 0) break;
            int a = ch & 0x7F;
            bool letter = (a >= 'A' && a <= 'Z') || (a >= 'a' && a <= 'z') || a == ' ' || a == '\'';
            if (!letter) return false;
            len++;
            if ((ch & 0x80) == 0) break;                        // terminator (high bit clear)
            if (i == RosterFormat.NameLength - 1) return false; // ran off the field without terminating
        }
        if (len < 2) return false;

        for (int k = 0; k < RosterFormat.AttributeCount; k++)
        {
            int cur = b[o + RosterFormat.AttributeCurOffsets[k]];
            int bas = b[o + RosterFormat.AttributeCurOffsets[k] + 1];
            if (cur < 1 || cur > 99 || bas < 1 || bas > 99) return false;
        }

        int hpCur = b[o + RosterFormat.OffHealthCur] | (b[o + RosterFormat.OffHealthCur + 1] << 8);
        int hpMax = b[o + RosterFormat.OffHealthMax] | (b[o + RosterFormat.OffHealthMax + 1] << 8);
        if (hpMax < 1 || hpMax > 5000 || hpCur > hpMax) return false;

        int level = b[o + RosterFormat.OffLevel] | (b[o + RosterFormat.OffLevel + 1] << 8);
        return level is >= 1 and <= 200;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, RosterFormat.RecordSize) == RosterFormat.RecordSize;

    // --- anchor scan ---------------------------------------------------------
    private static IEnumerable<nuint> FindAnchors(ProcessMemory mem, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;   // re-read this much of the next window so a match that
                                           // straddles a window edge is still seen
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

                if (read >= needle.Length)
                {
                    for (int i = 0; i + needle.Length <= read; i++)
                        if (Matches(buf, i, needle))
                            yield return start + (nuint)i;
                }
                else if (want > PageSize)
                {
                    // ProcessMemory.Read is all-or-nothing, so a single unreadable page fails the
                    // whole chunk. Rather than abandon the region (which can hold the anchor past
                    // that page), salvage the rest of it page by page like MemoryDumper does.
                    foreach (var hit in ScanByPage(mem, start, regionEnd, needle, ct))
                        yield return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
    }

    // Scan [start, regionEnd) one page at a time, skipping only the pages that won't read, so one
    // unreadable page costs a page instead of the whole region. A page is read with a needle-sized
    // overlap into the next page so a straddling match is still caught; if that overlapping read
    // fails (next page unreadable) it retries the bare page.
    private static IEnumerable<nuint> ScanByPage(ProcessMemory mem, nuint start, nuint regionEnd, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < needle.Length && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < needle.Length) continue;   // unreadable page — skip it, keep scanning

            for (int i = 0; i + needle.Length <= read; i++)
                if (Matches(page, i, needle))
                    yield return p + (nuint)i;
        }
    }

    private static bool Matches(byte[] buf, int i, byte[] needle)
    {
        for (int k = 0; k < needle.Length; k++)
            if (buf[i + k] != needle[k]) return false;
        return true;
    }
}
