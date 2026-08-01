using EyeOfTheBeholder1Trainer.Game;

namespace EyeOfTheBeholder1Trainer.Memory;

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
/// Locates the Eye of the Beholder I party roster inside the attached emulator's memory.
///
/// The roster's live address changes every DOSBox session, so the trainer never hard-codes it.
/// Instead it uses a <b>structural scan</b>: the party is an array of six contiguous
/// <see cref="CharacterFormat.RecordSize"/>-byte records. The scan walks every readable region
/// looking for a window that matches the party shape — each slot's Character ID matches its index,
/// active characters have a plausible name, ability scores, and hit points, and at least one
/// slot is occupied. This is specific enough to pin the live roster without a static anchor.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private const int PartyBytes = CharacterFormat.PartySize;

    /// <summary>
    /// Finds the party and returns every occupied character slot, or an empty list if no party
    /// can be located (not attached to EOB1, or the game isn't loaded past the title yet).
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        byte[] buf = new byte[ChunkSize + PartyBytes - 1];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + PartyBytes - 1), remaining);
                int read = mem.Read(start, buf, readLen);

                for (int i = 0; i + PartyBytes <= read; i++)
                {
                    var slots = TryReadParty(buf, i, start);
                    if (slots != null) return slots;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
        return new List<LocatedCharacter>();
    }

    /// <summary>
    /// Validates the six-slot window at <paramref name="offset"/> as a party and, if it holds,
    /// returns its occupied members with live addresses; otherwise null.
    /// </summary>
    private static List<LocatedCharacter>? TryReadParty(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            int charId = buf[off + CharacterFormat.OffCharId];

            // The Character ID must match the slot index.
            if (charId != i) return null;

            int active = buf[off + CharacterFormat.OffActive];
            if (active == 1)
            {
                if (!IsValidCharacter(buf, off)) return null;
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (active == 0)
            {
                // Empty slot — must be mostly zeroed.
                if (!IsEmptySlot(buf, off)) return null;
            }
            else
            {
                return null; // invalid active flag
            }
        }
        return slots.Count > 0 ? slots : null;
    }

    /// <summary>
    /// Strict validation of an active character slot: name starts with a letter, all six ability
    /// scores are 3..25, exceptional strength is 0..100, HP max is 1..255, race/class/alignment
    /// are in their valid ranges, and level is 1..40. Strict enough to reject the many stray byte
    /// runs that merely start with a small integer.
    /// </summary>
    private static bool IsValidCharacter(byte[] b, int o)
    {
        // Name: first char must be a letter
        byte first = b[o + CharacterFormat.OffName];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;

        // Name: remaining chars (up to NameLength) must be letters, spaces, or null
        for (int i = 1; i < CharacterFormat.NameLength; i++)
        {
            byte ch = b[o + CharacterFormat.OffName + i];
            if (ch == 0) break;
            if (!((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || ch == ' ' || ch == '-' || ch == '\''))
                return false;
        }

        // Abilities: 3..25 (AD&D range)
        for (int k = 0; k < CharacterFormat.AbilityCount; k++)
        {
            int mod = b[o + CharacterFormat.AbilityModOffsets[k]];
            int base_ = b[o + CharacterFormat.AbilityModOffsets[k] + 1];
            if (mod < 3 || mod > 25) return false;
            if (base_ < 3 || base_ > 25) return false;
        }

        // Exceptional strength: 0..100 (only meaningful for fighters with STR 18)
        int excMod = b[o + CharacterFormat.OffStrExcMod];
        int excBase = b[o + CharacterFormat.OffStrExcBase];
        if (excMod > 100 || excBase > 100) return false;

        // HP: max 1..255, current 0..255
        int hpMax = b[o + CharacterFormat.OffHpMax];
        int hpCur = b[o + CharacterFormat.OffHpCur];
        if (hpMax < 1 || hpMax > 255) return false;
        if (hpCur > hpMax) return false;

        // Race: 0..11
        int race = b[o + CharacterFormat.OffRace];
        if (race > 11) return false;

        // Class: 0..14
        int cls = b[o + CharacterFormat.OffClass];
        if (cls > 14) return false;

        // Alignment: 0..8
        int align = b[o + CharacterFormat.OffAlignment];
        if (align > 8) return false;

        // Food: 0..100
        int food = b[o + CharacterFormat.OffFood];
        if (food > 100) return false;

        // Level: at least one level must be 1..40
        int lvl1 = b[o + CharacterFormat.OffLevel1];
        int lvl2 = b[o + CharacterFormat.OffLevel2];
        int lvl3 = b[o + CharacterFormat.OffLevel3];
        int maxLvl = Math.Max(lvl1, Math.Max(lvl2, lvl3));
        if (maxLvl < 1 || maxLvl > 40) return false;

        return true;
    }

    /// <summary>An empty slot is mostly zeroed (at least the name and ability fields).</summary>
    private static bool IsEmptySlot(byte[] buf, int off)
    {
        // Check that the name field is all zeros
        for (int i = 0; i < CharacterFormat.NameLength; i++)
            if (buf[off + CharacterFormat.OffName + i] != 0) return false;
        // Check that abilities are zero
        for (int k = 0; k < CharacterFormat.AbilityCount; k++)
            if (buf[off + CharacterFormat.AbilityModOffsets[k]] != 0) return false;
        return true;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
