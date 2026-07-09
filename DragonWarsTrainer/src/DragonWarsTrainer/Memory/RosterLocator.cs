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
/// The roster is not found by scanning for character records (empty slots and monsters would
/// confuse that); instead it is anchored to DATA1's chunk-0 header — a unique byte run that
/// loads verbatim into guest RAM (see <see cref="RosterFormat.AnchorBytes"/>). Roster slot 0
/// sits a fixed delta past the anchor, and each slot is a fixed-size record. Occupied slots
/// are returned; empty (0xFF-filled) slots are skipped.
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails

    /// <summary>
    /// Finds the roster and returns every occupied character slot, or an empty list if the
    /// anchor can't be found (not attached to Dragon Wars, or the game isn't loaded yet).
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        var hits = new List<LocatedCharacter>();
        foreach (var anchor in FindAnchors(mem, RosterFormat.AnchorBytes, ct))
        {
            nuint rosterBase = anchor + (nuint)RosterFormat.RosterAnchorDelta;
            var slots = ReadRoster(mem, rosterBase);
            if (slots.Count == 0) continue;    // anchor matched but no live party behind it
            return slots;                      // first anchor with a real party wins
        }
        return hits;
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
