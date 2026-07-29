using AmberstarTrainer.Game;

namespace AmberstarTrainer.Memory;

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
/// Locates the Amberstar party roster inside the attached emulator's memory.
///
/// The party is an array of up to <see cref="CharacterFormat.MaxSlots"/> contiguous
/// <see cref="CharacterFormat.RecordSize"/>-byte records. Each record starts with the
/// big-endian magic header <c>00 FF</c> and has Type = 0 (Person). The roster address
/// changes every DOSBox session, so the locator scans every readable region for a window
/// that matches the party shape exactly: occupied slots hold validated characters that
/// pack from slot 0, followed by empty (uninitialised) slots.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private const int PartyBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;

    /// <summary>
    /// Finds the party and returns every occupied character slot, or an empty list if no
    /// party can be located (not attached to Amberstar, or the game isn't loaded yet).
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        int overlap = PartyBytes - 1;
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

                if (read >= PartyBytes)
                {
                    for (int i = 0; i + PartyBytes <= read; i++)
                    {
                        if (!IsValidCharacter(buf, i)) continue;
                        var slots = TryReadParty(buf, i, start);
                        if (slots != null) return slots;
                    }
                }
                else if (want > PageSize && read < readLen)
                {
                    // ProcessMemory.Read is all-or-nothing; a single unreadable page fails
                    // the whole chunk. Salvage the rest of the region page by page so one
                    // bad page doesn't cause us to skip the live party.
                    var hit = ScanByPage(mem, start, regionEnd, ct);
                    if (hit != null) return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
        return new List<LocatedCharacter>();
    }

    // Scan [start, regionEnd) one page at a time, skipping unreadable pages.
    private static List<LocatedCharacter>? ScanByPage(ProcessMemory mem, nuint start, nuint regionEnd, CancellationToken ct)
    {
        int readSize = Math.Max(PageSize, PartyBytes);
        int overlap = PartyBytes - 1;
        byte[] page = new byte[readSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(readSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < PartyBytes && readLen > readSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)readSize, remaining));
            if (read < PartyBytes) continue;

            for (int i = 0; i + PartyBytes <= read; i++)
            {
                if (!IsValidCharacter(page, i)) continue;
                var slots = TryReadParty(page, i, p);
                if (slots != null) return slots;
            }
        }
        return null;
    }

    // Validates the MaxSlots-slot window at <paramref name="offset"/> as a party roster
    // and, if it holds, returns its occupied members with live addresses; otherwise null.
    private static List<LocatedCharacter>? TryReadParty(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
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

    /// <summary>A party slot is empty when its magic header is not 00 FF.</summary>
    private static bool IsEmptySlot(byte[] buf, int off) =>
        buf[off] != 0x00 || buf[off + 1] != 0xFF;

    /// <summary>
    /// Validates a candidate as a real Amberstar party member: magic header 00 FF,
    /// type = Person, plausible gender/race/class, all 20 skill bytes in 0..99,
    /// level 1..99, big-endian attributes in a sane range, HP max > 0, and a
    /// well-formed ASCII name starting with a letter.
    /// </summary>
    private static bool IsValidCharacter(byte[] b, int o)
    {
        // magic header
        if (b[o] != 0x00 || b[o + 1] != 0xFF) return false;
        // type = Person
        if (b[o + CharacterFormat.OffType] != 0) return false;
        // gender
        int gender = b[o + CharacterFormat.OffGender];
        if (gender > 1) return false;
        // race (0..6 or 13)
        int race = b[o + CharacterFormat.OffRace];
        if (race > 6 && race != 13) return false;
        // class (0..8 or 9)
        int cls = b[o + CharacterFormat.OffClass];
        if (cls > 9) return false;
        // level
        int level = b[o + CharacterFormat.OffLevel];
        if (level < 1 || level > 99) return false;

        // skills: 20 bytes (10 current + 10 max), each 0..99
        for (int i = 0; i < CharacterFormat.SkillCount * 2; i++)
        {
            int s = b[o + CharacterFormat.OffSkillsCur + i];
            if (s > 99) return false;
        }

        // attributes: 9 big-endian Words (current), each in 1..999
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int hi = b[o + CharacterFormat.OffAttrCur + i * 2];
            int lo = b[o + CharacterFormat.OffAttrCur + i * 2 + 1];
            int attr = (hi << 8) | lo;
            if (attr < 1 || attr > 999) return false;
        }

        // HP max (big-endian Word) must be > 0
        int hpMaxHi = b[o + CharacterFormat.OffHpMax];
        int hpMaxLo = b[o + CharacterFormat.OffHpMax + 1];
        int hpMax = (hpMaxHi << 8) | hpMaxLo;
        if (hpMax < 1 || hpMax > 9999) return false;

        // name: first byte must be a letter (A-Z or a-z)
        byte first = b[o + CharacterFormat.OffName];
        if (first < 0x41 || first > 0x7A) return false;
        if (first > 0x5A && first < 0x61) return false;

        return true;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
