using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.Memory;

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
/// Locates the Wizardry 1 party roster inside the attached emulator's memory.
///
/// The UCSD p-system (emulated by WIZDOS.COM) allocates the character array on its heap
/// at a session-specific address, so there is no static anchor to scan for. Instead this
/// locator uses a <b>structural scan</b>: it walks every readable region looking for a
/// window of contiguous <see cref="CharacterFormat.RecordSize"/>-byte records matching the
/// shape of a Wizardry 1 party -- occupied slots pack from slot 0, followed by empty slots,
/// and every occupied slot must pass <see cref="IsValidCharacter"/>.
///
/// This mirrors the approach used by <c>WastelandTrainer</c> and <c>AmberstarTrainer</c>
/// for games whose roster address changes every session.
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private static readonly int RosterBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;

    /// <summary>
    /// Finds the roster and returns every occupied character slot, or an empty list if no party
    /// can be located (not attached to Wizardry 1, or the game isn't loaded past the title yet).
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        return FindByStructure(mem, ct);
    }

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

                for (int i = 0; i + RosterBytes <= read; i++)
                {
                    if (!IsValidCharacter(buf, i)) continue;
                    var slots = TryReadRoster(buf, i, start);
                    if (slots != null) return slots;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
        return new List<LocatedCharacter>();
    }

    private static List<LocatedCharacter>? TryReadRoster(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            if (IsValidCharacter(buf, off))
            {
                if (seenEmpty) return null;
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (IsEmptySlot(buf, off))
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

    private static bool IsEmptySlot(byte[] buf, int off)
    {
        return buf[off + CharacterFormat.OffName] == 0x00;
    }

    /// <summary>
    /// Stricter than <see cref="CharacterRecord.IsOccupied"/>: requires a well-formed UCSD
    /// Pascal name (1..15 letters, first char A-Z), race 1..5, class 0..7, alignment 1..3,
    /// attributes 3..18, HP max 1..999, and level 1..99 -- enough to reject the many stray
    /// byte runs that merely start with a letter.
    /// </summary>
    public static bool IsValidCharacter(byte[] b, int o)
    {
        int len = b[o + CharacterFormat.OffName];
        if (len < 1 || len > 15) return false;
        for (int i = 0; i < len && i < 15; i++)
        {
            int ch = b[o + CharacterFormat.OffName + 1 + i];
            if (ch < 32 || ch > 126) return false;
            if (i == 0 && (ch < 'A' || ch > 'Z')) return false;
        }

        int race = b[o + CharacterFormat.OffRace] | (b[o + CharacterFormat.OffRace + 1] << 8);
        if (race < 1 || race > 5) return false;

        int cls = b[o + CharacterFormat.OffClass] | (b[o + CharacterFormat.OffClass + 1] << 8);
        if (cls > 7) return false;

        int align = b[o + CharacterFormat.OffAlignment] | (b[o + CharacterFormat.OffAlignment + 1] << 8);
        if (align < 1 || align > 3) return false;

        int status = b[o + CharacterFormat.OffStatus] | (b[o + CharacterFormat.OffStatus + 1] << 8);
        if ((uint)status > 7) return false;

        var (str, Int, pie, vit, agi, luk) = CharacterFormat.ReadAttributes(b, o + CharacterFormat.OffAttributes);
        if (str < 3 || str > 18) return false;
        if (Int < 3 || Int > 18) return false;
        if (pie < 3 || pie > 18) return false;
        if (vit < 3 || vit > 18) return false;
        if (agi < 3 || agi > 18) return false;
        if (luk < 3 || luk > 18) return false;

        int hpMax = b[o + CharacterFormat.OffHpMax] | (b[o + CharacterFormat.OffHpMax + 1] << 8);
        if (hpMax < 1 || hpMax > 999) return false;

        int level = b[o + CharacterFormat.OffLevel] | (b[o + CharacterFormat.OffLevel + 1] << 8);
        if (level < 1 || level > 99) return false;

        int equipCount = b[o + CharacterFormat.OffEquipmentCount] | (b[o + CharacterFormat.OffEquipmentCount + 1] << 8);
        return (uint)equipCount <= CharacterFormat.EquipmentSlotCount;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
