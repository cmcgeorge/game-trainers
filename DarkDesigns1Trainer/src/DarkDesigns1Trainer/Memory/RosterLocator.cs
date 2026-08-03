namespace DarkDesigns1Trainer.Memory;

using DarkDesigns1Trainer.Game;

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
/// Locates the Dark Designs I character roster inside the attached emulator's memory.
///
/// Two strategies, tried in order:
///
/// 1. <b>Anchor</b> — the 34-byte title string <c>"Dark Designs I : Grelminar's Staff"</c>
///    lives in the game's code/data segment as plain ASCII and is unique in DOSBox guest RAM.
///    The character buffer (loaded from <c>DDCHARS.DAT</c>) sits in BSS, allocated contiguously
///    after the loaded image. The locator finds the anchor, then searches a 256 KB window
///    forward for the 20-record character pattern.
///
/// 2. <b>Structural</b> — a fallback that scans all readable memory for a contiguous block of
///    54-byte records matching the character pattern (occupied slots validated, empty slots
///    all-zero, occupied slots pack from slot 0).
///
/// Either way, only occupied slots are returned; empty slots are skipped.
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private static readonly byte[] AnchorBytes = System.Text.Encoding.ASCII.GetBytes(GameFacts.AnchorString);
    private const int AnchorSearchWindow = 256 * 1024;  // 256 KB forward from anchor
    private const int RosterBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;

    /// <summary>
    /// Finds the roster and returns every occupied character slot, or an empty list if no
    /// roster can be located. Tries the string anchor first, then falls back to a structural scan.
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        var byAnchor = FindByAnchor(mem, ct);
        if (byAnchor.Count > 0) return byAnchor;
        return FindByStructure(mem, ct);
    }

    // --- strategy 1: string anchor + nearby structural search ----------------
    private static List<LocatedCharacter> FindByAnchor(ProcessMemory mem, CancellationToken ct)
    {
        foreach (var anchor in FindAnchors(mem, AnchorBytes, ct))
        {
            // Search a window forward from the anchor for the character records.
            int windowSize = AnchorSearchWindow + RosterBytes;
            byte[] buf = new byte[windowSize];
            int read = mem.Read(anchor, buf, windowSize);
            if (read < RosterBytes) continue;

            // Validate the anchor by checking for at least one validator string in the window.
            if (!HasValidator(buf, read)) continue;

            for (int i = 0; i + RosterBytes <= read; i++)
            {
                if (!CharacterFormat.LooksLikeRecord(buf, i)) continue;
                var slots = TryReadRoster(buf, i, anchor);
                if (slots != null) return slots;
            }
        }
        return new List<LocatedCharacter>();
    }

    private static bool HasValidator(byte[] buf, int len)
    {
        foreach (var vs in GameFacts.ValidatorStrings)
        {
            var needle = System.Text.Encoding.ASCII.GetBytes(vs);
            if (needle.Length == 0) continue;
            for (int i = 0; i + needle.Length <= len; i++)
                if (Matches(buf, i, needle)) return true;
        }
        return false;
    }

    // --- strategy 2: full structural scan ------------------------------------
    private static List<LocatedCharacter> FindByStructure(ProcessMemory mem, CancellationToken ct)
    {
        int overlap = RosterBytes - 1;
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

                if (read >= RosterBytes)
                {
                    for (int i = 0; i + RosterBytes <= read; i++)
                    {
                        if (!CharacterFormat.LooksLikeRecord(buf, i)) continue;
                        var slots = TryReadRoster(buf, i, start);
                        if (slots != null) return slots;
                    }
                }
                else if (want > PageSize)
                {
                    foreach (var hit in ScanStructureByPage(mem, start, regionEnd, ct))
                        if (hit != null) return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
        return new List<LocatedCharacter>();
    }

    private static IEnumerable<List<LocatedCharacter>?> ScanStructureByPage(ProcessMemory mem, nuint start, nuint regionEnd, CancellationToken ct)
    {
        int overlap = RosterBytes - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < RosterBytes) continue;

            for (int i = 0; i + RosterBytes <= read; i++)
            {
                if (!CharacterFormat.LooksLikeRecord(page, i)) continue;
                var slots = TryReadRoster(page, i, p);
                if (slots != null) { yield return slots; yield break; }
            }
        }
    }

    // --- shared roster validation --------------------------------------------
    private static List<LocatedCharacter>? TryReadRoster(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            if (off + CharacterFormat.RecordSize > buf.Length) return null;

            if (CharacterFormat.LooksLikeRecord(buf, off))
            {
                if (seenEmpty) return null;
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (CharacterFormat.IsEmptySlot(buf, off))
            {
                seenEmpty = true;
            }
            else
            {
                return null;
            }
        }
        return slots.Count > 0 ? slots : null;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;

    // --- anchor byte-pattern scan --------------------------------------------
    private static IEnumerable<nuint> FindAnchors(ProcessMemory mem, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
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
                    foreach (var hit in ScanByPage(mem, start, regionEnd, needle, ct))
                        yield return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
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
            if (read < needle.Length) continue;

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
