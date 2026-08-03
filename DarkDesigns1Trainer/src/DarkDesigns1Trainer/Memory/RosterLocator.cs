namespace DarkDesigns1Trainer.Memory;

using DarkDesigns1Trainer.Game;

/// <summary>A located character record: its live process address and a decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    /// <summary>
    /// Addresses of the game's party working copies of this same character, if it is currently
    /// in the party. The game plays out of these and copies them back over the roster when it
    /// saves, so a write that misses them is undone on the next save.
    /// </summary>
    public List<nuint> Mirrors { get; } = new();

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
///    forward for the 15-record character pattern.
///
/// 2. <b>Structural</b> — a fallback that scans all readable memory for a contiguous block of
///    72-byte records matching the character pattern (occupied slots validated, empty slots
///    all-zero, occupied slots pack from slot 0).
///
/// Either way, only occupied slots are returned; empty slots are skipped. Each hit is then
/// checked for the game's separate party working copies (see <see cref="LocatedCharacter.Mirrors"/>).
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
        var found = FindByAnchor(mem, ct);
        if (found.Count == 0) found = FindByStructure(mem, ct);
        if (found.Count > 0) AttachPartyMirrors(mem, found);
        return found;
    }

    // --- party working copies -------------------------------------------------
    /// <summary>
    /// Bytes of data segment to sweep past the roster when hunting for party working copies.
    /// In the build we disassembled the roster sits at DGROUP <c>0x424</c> and the party array
    /// at <c>0x1360</c> — 0xF3C apart — but nothing here depends on that: the sweep matches on
    /// record content, so a differently laid out build still resolves (or finds nothing and
    /// simply skips mirroring).
    /// </summary>
    private const int PartySearchWindow = 0x2800;   // 10 KB

    /// <summary>
    /// The game keeps a 72-byte working copy of each party member separate from the roster, plays
    /// out of those copies, and writes them back over the roster when it saves. This finds any
    /// such copy of each located character — matched on the name bytes and class, never on a
    /// hard-coded offset — so writes can be applied to both and survive the game's own save.
    /// </summary>
    private static void AttachPartyMirrors(ProcessMemory mem, List<LocatedCharacter> found)
    {
        var first = found[0];
        nuint rosterBase = first.Address - (nuint)(first.Slot * CharacterFormat.RecordSize);

        byte[] window = new byte[PartySearchWindow];
        int read = ReadAsMuchAsPossible(mem, rosterBase, window);
        if (read < CharacterFormat.RecordSize) return;

        // The in-memory roster array carries a scratch slot the file does not, and the scan may
        // have anchored on either. Exclude a whole extra record so no roster slot is mistaken for
        // a working copy.
        nuint rosterEnd = rosterBase + (nuint)((CharacterFormat.MaxSlots + 1) * CharacterFormat.RecordSize);

        var candidates = new Dictionary<LocatedCharacter, List<nuint>>();
        var claimants = new Dictionary<nuint, int>();

        foreach (var lc in found)
        {
            var mine = new List<nuint>();
            for (int i = 0; i + CharacterFormat.RecordSize <= read; i++)
            {
                nuint addr = rosterBase + (nuint)i;
                if (addr < rosterEnd) continue;                          // that's the roster itself
                if (!CharacterFormat.LooksLikeRecord(window, i)) continue;
                if (!SameCharacter(window, i, lc.Record)) continue;
                mine.Add(addr);
                claimants[addr] = claimants.GetValueOrDefault(addr) + 1;
            }
            candidates[lc] = mine;
        }

        // A character has at most one working copy, and names are not unique in Dark Designs, so
        // anything ambiguous is dropped rather than guessed at: writing to the wrong copy would
        // corrupt a different character. Losing the mirror only costs the write-through.
        foreach (var (lc, mine) in candidates)
        {
            if (mine.Count != 1) continue;
            if (claimants[mine[0]] != 1) continue;
            lc.Mirrors.Add(mine[0]);
        }
    }

    /// <summary>
    /// Reads what it can of <paramref name="buffer"/>, falling back to page-sized reads when the
    /// whole span fails — <c>ProcessMemory.Read</c> reports a partial copy as 0, so one unreadable
    /// page at the end of the window would otherwise lose the entire sweep.
    /// </summary>
    private static int ReadAsMuchAsPossible(ProcessMemory mem, nuint start, byte[] buffer)
    {
        int read = mem.Read(start, buffer, buffer.Length);
        if (read > 0) return read;

        int total = 0;
        byte[] page = new byte[PageSize];
        for (int off = 0; off + PageSize <= buffer.Length; off += PageSize)
        {
            if (mem.Read(start + (nuint)off, page, PageSize) != PageSize) break;
            Array.Copy(page, 0, buffer, off, PageSize);
            total = off + PageSize;
        }
        return total;
    }

    /// <summary>
    /// True when the record at <paramref name="offset"/> is the same character as
    /// <paramref name="record"/>: same name bytes and same class. Vitals are deliberately
    /// not compared — a party copy diverges from the roster as soon as the character takes a hit.
    /// Callers gate on <see cref="CharacterFormat.LooksLikeRecord"/> first, so a display buffer
    /// that merely happens to contain the name is not mistaken for a copy of the character.
    /// </summary>
    private static bool SameCharacter(byte[] buf, int offset, CharacterRecord record)
    {
        if (buf[offset + CharacterFormat.OffClass] != record.Bytes[CharacterFormat.OffClass]) return false;
        if (buf[offset + CharacterFormat.OffNameLen] != record.Bytes[CharacterFormat.OffNameLen]) return false;
        if (buf[offset + CharacterFormat.OffNameLen] == 0) return false;

        for (int k = 0; k < CharacterFormat.NameLength; k++)
            if (buf[offset + CharacterFormat.OffName + k] != record.Bytes[CharacterFormat.OffName + k])
                return false;
        return true;
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
