using MinesOfTitanTrainer.Game;

namespace MinesOfTitanTrainer.Memory;

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
/// Locates the Mines of Titan party inside the attached emulator's memory.
///
/// Two strategies, tried in order:
///
/// 1. <b>Anchor</b> — a save slot begins with the ASCII magic <c>IJKM</c>
///    (<see cref="CharacterFormat.SlotMagic"/>); the character array starts
///    <see cref="CharacterFormat.SlotToFirstRecord"/> bytes past it and packs by
///    <see cref="CharacterFormat.RecordSize"/>. Fast and exact once a game has been loaded/saved.
///
/// 2. <b>Structural</b> — a fallback that walks memory for a run of contiguous windows shaped like
///    a valid record (printable ASCII name, <c>M</c>/<c>F</c> sex, sane age, attributes in 0..15).
///    This finds a freshly-created party that has never touched <c>SAVEGAME.DAT</c>.
///
/// Either way, only occupied slots are returned; the read-validate-write rule means an edit is only
/// ever pushed back to a window that first validates as a real character.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;      // salvage granularity when a chunk read fails

    /// <summary>
    /// Finds the party and returns every occupied character slot, or an empty list if none can be
    /// located. Tries the exact <c>IJKM</c> anchor first, then falls back to a structural scan.
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        var byAnchor = FindByAnchor(mem, ct);
        if (byAnchor.Count > 0) return byAnchor;
        return FindByStructure(mem, ct);
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;

    // --- strategy 1: exact anchor -------------------------------------------
    private static List<LocatedCharacter> FindByAnchor(ProcessMemory mem, CancellationToken ct)
    {
        foreach (var anchor in FindAnchors(mem, CharacterFormat.SlotMagic, ct))
        {
            nuint partyBase = anchor + (nuint)CharacterFormat.SlotToFirstRecord;
            var slots = ReadParty(mem, partyBase);
            if (slots.Count == 0) continue;    // magic matched (e.g. the EXE image) but no live party behind it
            return slots;                      // first anchor with a real party wins
        }
        return new List<LocatedCharacter>();
    }

    private static List<LocatedCharacter> ReadParty(ProcessMemory mem, nuint partyBase)
    {
        var slots = new List<LocatedCharacter>();
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            nuint addr = partyBase + (nuint)(i * CharacterFormat.RecordSize);
            var buf = mem.Read(addr, CharacterFormat.RecordSize);
            if (buf.Length != CharacterFormat.RecordSize) break;
            var rec = new CharacterRecord(buf);
            if (!rec.IsOccupied) break;        // records pack from slot 0; first empty ends the party
            slots.Add(new LocatedCharacter(addr, i, rec));
        }
        return slots;
    }

    // --- strategy 2: structural scan ----------------------------------------
    // Walks every readable region for the first valid record, then extends the run forward while
    // consecutive RecordSize-stride windows also validate (a packed party), up to MaxSlots.
    private static List<LocatedCharacter> FindByStructure(ProcessMemory mem, CancellationToken ct)
    {
        int span = CharacterFormat.RecordSize * CharacterFormat.MaxSlots;
        int overlap = span - 1;   // so a party straddling a window edge is still seen whole
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

                if (read >= CharacterFormat.RecordSize)
                {
                    var slots = ScanBufferForParty(buf, read, want, start);
                    if (slots.Count > 0) return slots;
                }
                else if (want > PageSize)
                {
                    // ProcessMemory.Read is all-or-nothing, so one unreadable page fails the whole
                    // chunk. Salvage the rest of the region page by page (mirroring the anchor scan)
                    // rather than skipping up to a megabyte that may still hold the party.
                    var salvaged = ScanStructureByPage(mem, start, regionEnd, ct);
                    if (salvaged.Count > 0) return salvaged;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
        return new List<LocatedCharacter>();
    }

    // Scans a filled buffer for the first valid record and returns the packed run starting there.
    // Matches are only *initiated* within the chunk's base span (<paramref name="scanLimit"/>); the
    // extra overlap bytes past it exist solely so a party that begins inside the base span but runs
    // over the seam is read whole. A record that *starts* in the overlap tail belongs to the next
    // chunk's base span — initiating it here would read only the part that fits and return a
    // truncated party, so it must be left for the next window.
    private static List<LocatedCharacter> ScanBufferForParty(byte[] buf, int read, int scanLimit, nuint windowBase)
    {
        for (int i = 0; i < scanLimit && i + CharacterFormat.RecordSize <= read; i++)
        {
            if (!IsValidCharacter(buf, i)) continue;
            var slots = ReadPartyRun(buf, i, read, windowBase);
            if (slots.Count > 0) return slots;
        }
        return new List<LocatedCharacter>();
    }

    private static List<LocatedCharacter> ScanStructureByPage(ProcessMemory mem, nuint start, nuint regionEnd, CancellationToken ct)
    {
        int overlap = CharacterFormat.RecordSize * CharacterFormat.MaxSlots - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < CharacterFormat.RecordSize && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < CharacterFormat.RecordSize) continue;   // unreadable page — skip it, keep scanning

            int limit = (int)Math.Min((nuint)PageSize, remaining);
            var slots = ScanBufferForParty(page, read, limit, p);
            if (slots.Count > 0) return slots;
        }
        return new List<LocatedCharacter>();
    }

    // Collects the packed run of valid records starting at <paramref name="offset"/> in the buffer.
    private static List<LocatedCharacter> ReadPartyRun(byte[] buf, int offset, int read, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            if (off + CharacterFormat.RecordSize > read) break;
            if (!IsValidCharacter(buf, off)) break;
            slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, new CharacterRecord(buf, off)));
        }
        return slots;
    }

    // Allocation-free: probes the buffer window directly instead of copying an 86-byte record for
    // every candidate offset in the scan's hot loop.
    private static bool IsValidCharacter(byte[] buf, int offset) =>
        CharacterRecord.IsOccupiedAt(buf, offset);

    // --- anchor scan ---------------------------------------------------------
    private static IEnumerable<nuint> FindAnchors(ProcessMemory mem, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;   // re-read this much of the next window so a straddling match is still seen
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
                    // Only initiate matches within the base span; the overlap tail is re-covered as
                    // the next window's base span, so bounding here avoids yielding a seam match twice.
                    for (int i = 0; i < want && i + needle.Length <= read; i++)
                        if (Matches(buf, i, needle))
                            yield return start + (nuint)i;
                }
                else if (want > PageSize)
                {
                    // ProcessMemory.Read is all-or-nothing, so one unreadable page fails the whole
                    // chunk. Salvage the rest of the region page by page rather than abandoning it.
                    foreach (var hit in ScanByPage(mem, start, regionEnd, needle, ct))
                        yield return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
    }

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

            int limit = (int)Math.Min((nuint)PageSize, remaining);
            for (int i = 0; i < limit && i + needle.Length <= read; i++)
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
