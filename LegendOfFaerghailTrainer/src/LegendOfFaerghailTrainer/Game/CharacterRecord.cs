using System.Text;

namespace LegendOfFaerghailTrainer.Game;

/// <summary>One inventory slot: item id, "in use" flag, an unidentified byte, condition percent.</summary>
public readonly record struct ItemSlot(int Slot, int ItemId, bool Equipped, int Unknown, int Condition)
{
    public bool IsEmpty => ItemId == 0;
}

/// <summary>One spell slot: spell id and uses remaining today.</summary>
public readonly record struct SpellSlot(int Slot, int SpellId, int Uses)
{
    public bool IsEmpty => SpellId == 0;
}

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Legend of Faerghail
/// character record. The backing <see cref="Bytes"/> array may come from live process memory, from
/// <c>ROST\ROST</c>, or from a <c>GAMES\GAMEn</c> save; edits mutate it in place so the caller can
/// write back just the bytes that changed.
///
/// Every multi-byte field is little-endian: the game is an Amiga original, but the PC conversion is
/// a native Microsoft C 8086 build, so nothing here is byte-swapped.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord() => Bytes = new byte[CharacterFormat.RecordSize];

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Bytes = new byte[CharacterFormat.RecordSize];
        int n = Math.Min(CharacterFormat.RecordSize, buffer.Length - offset);
        if (n > 0) Array.Copy(buffer, offset, Bytes, 0, n);
    }

    // --- primitive accessors ----------------------------------------------------
    private byte U8(int o) => Bytes[o];
    private void U8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, 0, 255);

    private int U16(int o) => Bytes[o] | (Bytes[o + 1] << 8);
    private void U16(int o, int v)
    {
        v = Math.Clamp(v, 0, 0xFFFF);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    private long U32(int o) =>
        (uint)Bytes[o] | ((uint)Bytes[o + 1] << 8) | ((uint)Bytes[o + 2] << 16) | ((uint)Bytes[o + 3] << 24);
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- identity ---------------------------------------------------------------

    public bool Occupied
    {
        get => Bytes[CharacterFormat.OffOccupied] != 0;
        set => Bytes[CharacterFormat.OffOccupied] = (byte)(value ? 1 : 0);
    }

    /// <summary>
    /// The character's name. The field is 14 bytes but the game only ever prints ten, and its own
    /// entry screen never writes more, so the setter stops at <see cref="CharacterFormat.MaxNameLength"/>.
    /// The setter clears the whole field first. The shipped records keep stale fragments of earlier
    /// names after the terminator ("Connar\0er") and the game does not care either way, but writing
    /// a clean field means the stored bytes always match what the trainer displays.
    /// </summary>
    public string Name
    {
        get
        {
            int len = 0;
            while (len < CharacterFormat.NameFieldLength && Bytes[CharacterFormat.OffName + len] != 0)
                len++;
            return Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len);
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameFieldLength);
            if (string.IsNullOrEmpty(value)) return;
            var enc = Encoding.ASCII.GetBytes(value);
            int n = Math.Min(enc.Length, CharacterFormat.MaxNameLength);
            Array.Copy(enc, 0, Bytes, CharacterFormat.OffName, n);
        }
    }

    /// <summary>Experience level ("Rnk"). 0 marks a non-player character the party picked up.</summary>
    public int Level
    {
        get => U8(CharacterFormat.OffLevel);
        set => U8(CharacterFormat.OffLevel, Math.Clamp(value, 0, 99));
    }

    public int Sex
    {
        get => U8(CharacterFormat.OffSex);
        set => U8(CharacterFormat.OffSex, Math.Clamp(value, 0, 1));
    }

    public int Alignment
    {
        get => U8(CharacterFormat.OffAlignment);
        set => U8(CharacterFormat.OffAlignment, Math.Clamp(value, 0, 1));
    }

    public int Race
    {
        get => U8(CharacterFormat.OffRace);
        set => U8(CharacterFormat.OffRace, Math.Clamp(value, 0, RaceBook.Count - 1));
    }

    public int Class
    {
        get => U8(CharacterFormat.OffClass);
        set => U8(CharacterFormat.OffClass, Math.Clamp(value, 0, ClassBook.Count - 1));
    }

    public int Status
    {
        get => U8(CharacterFormat.OffStatus);
        set => U8(CharacterFormat.OffStatus, Math.Clamp(value, 0, StatusBook.Count - 1));
    }

    public int ArmourPercent
    {
        get => U8(CharacterFormat.OffArmourPercent);
        set => U8(CharacterFormat.OffArmourPercent, Math.Clamp(value, 0, 255));
    }

    public string RaceName => RaceBook.NameOf(Race);
    public string ClassName => ClassBook.NameOf(Class);
    public string StatusName => StatusBook.NameOf(Status);
    public string SexName => Sex == 0 ? "Female" : "Male";
    public string AlignmentName => Alignment == 0 ? "Lawful" : "Chaotic";

    // --- pools ------------------------------------------------------------------

    public int MaxHp
    {
        get => U16(CharacterFormat.OffMaxHp);
        set => U16(CharacterFormat.OffMaxHp, Math.Clamp(value, 1, CharacterFormat.MaxHitPoints));
    }

    public int CurHp
    {
        get => U16(CharacterFormat.OffCurHp);
        set => U16(CharacterFormat.OffCurHp, Math.Clamp(value, 0, CharacterFormat.MaxHitPoints));
    }

    public int MaxMagic
    {
        get => U8(CharacterFormat.OffMaxMagic);
        set => U8(CharacterFormat.OffMaxMagic, Math.Clamp(value, 0, 255));
    }

    public int CurMagic
    {
        get => U8(CharacterFormat.OffCurMagic);
        set => U8(CharacterFormat.OffCurMagic, Math.Clamp(value, 0, 255));
    }

    // --- abilities --------------------------------------------------------------

    public int GetAbility(int index) => U8(CharacterFormat.AbilityOffsets[index]);

    public void SetAbility(int index, int value) =>
        U8(CharacterFormat.AbilityOffsets[index], Math.Clamp(value, 0, CharacterFormat.MaxAbility));

    // --- attributes -------------------------------------------------------------

    public int GetAttribute(int index) => U8(CharacterFormat.AttributeOffsets[index]);

    public void SetAttribute(int index, int value) =>
        U8(CharacterFormat.AttributeOffsets[index], Math.Clamp(value, 1, CharacterFormat.MaxAttribute));

    public int Constitution { get => U8(CharacterFormat.OffConstitution); set => U8(CharacterFormat.OffConstitution, Math.Clamp(value, 1, CharacterFormat.MaxAttribute)); }
    public int Strength { get => U8(CharacterFormat.OffStrength); set => U8(CharacterFormat.OffStrength, Math.Clamp(value, 1, CharacterFormat.MaxAttribute)); }
    public int Dexterity { get => U8(CharacterFormat.OffDexterity); set => U8(CharacterFormat.OffDexterity, Math.Clamp(value, 1, CharacterFormat.MaxAttribute)); }
    public int Intelligence { get => U8(CharacterFormat.OffIntelligence); set => U8(CharacterFormat.OffIntelligence, Math.Clamp(value, 1, CharacterFormat.MaxAttribute)); }
    public int Wisdom { get => U8(CharacterFormat.OffWisdom); set => U8(CharacterFormat.OffWisdom, Math.Clamp(value, 1, CharacterFormat.MaxAttribute)); }

    // --- load, purse, progress --------------------------------------------------

    /// <summary>Maximum load in pounds (the record stores tenths).</summary>
    public int MaxWeight
    {
        get => U16(CharacterFormat.OffMaxWeight) / 10;
        set => U16(CharacterFormat.OffMaxWeight, Math.Clamp(value, 0, CharacterFormat.MaxLoadPounds) * 10);
    }

    /// <summary>Carried load in pounds (the record stores tenths; the game truncates when printing).</summary>
    public int CurWeight => U16(CharacterFormat.OffCurWeight) / 10;

    public long Experience
    {
        get => U32(CharacterFormat.OffExperience);
        set => U32(CharacterFormat.OffExperience, value);
    }

    public int Rations
    {
        get => U16(CharacterFormat.OffRations);
        set => U16(CharacterFormat.OffRations, Math.Clamp(value, 0, CharacterFormat.MaxRations));
    }

    public long Gold
    {
        get => U32(CharacterFormat.OffGold);
        set => U32(CharacterFormat.OffGold, Math.Clamp(value, 0, CharacterFormat.MaxGold));
    }

    /// <summary>The unidentified 32-bit counter at +0x76. Exposed read-only.</summary>
    public long UnknownCounter => U32(CharacterFormat.OffUnknownCounter);

    public int SpellCount { get => U8(CharacterFormat.OffSpellCount); set => U8(CharacterFormat.OffSpellCount, Math.Clamp(value, 0, CharacterFormat.SpellSlots)); }
    public int ItemCount { get => U8(CharacterFormat.OffItemCount); set => U8(CharacterFormat.OffItemCount, Math.Clamp(value, 0, CharacterFormat.InventorySlots)); }

    // --- languages --------------------------------------------------------------

    public bool GetLanguage(int index) => Bytes[CharacterFormat.OffLanguages + index] != 0;

    /// <summary>
    /// Sets a language flag. The shipped records use 2 rather than 1 for "speaks", so that is what
    /// is written; the display only tests for non-zero.
    /// </summary>
    public void SetLanguage(int index, bool value) =>
        Bytes[CharacterFormat.OffLanguages + index] = (byte)(value ? 2 : 0);

    // --- inventory --------------------------------------------------------------

    public ItemSlot GetItem(int slot)
    {
        int o = InventoryOffset(slot);
        return new ItemSlot(slot, Bytes[o + CharacterFormat.InvId],
            Bytes[o + CharacterFormat.InvEquipped] != 0,
            Bytes[o + CharacterFormat.InvUnknown],
            Bytes[o + CharacterFormat.InvCondition]);
    }

    /// <summary>
    /// Writes an inventory slot. An id the item table does not cover is stored <b>unchanged</b>
    /// rather than clamped into the table: the id is a raw byte the game owns, and clamping a
    /// value of 200 down to the last table entry on a read-modify-write (ticking "in use", or a
    /// repair pass) would silently turn an item the teardown never catalogued into something else.
    /// </summary>
    public void SetItem(int slot, int itemId, bool equipped, int condition)
    {
        int o = InventoryOffset(slot);
        // Decide on the value that will actually be stored, not the one passed in: a negative id
        // clamps to 0, and testing the argument instead would leave an "empty" slot still carrying
        // its equipped flag and condition.
        int stored = Math.Clamp(itemId, 0, byte.MaxValue);
        Bytes[o + CharacterFormat.InvId] = (byte)stored;
        if (stored == 0)
        {
            Bytes[o + CharacterFormat.InvEquipped] = 0;
            Bytes[o + CharacterFormat.InvUnknown] = 0;
            Bytes[o + CharacterFormat.InvCondition] = 0;
            return;
        }
        Bytes[o + CharacterFormat.InvEquipped] = (byte)(equipped ? 1 : 0);
        Bytes[o + CharacterFormat.InvCondition] = (byte)Math.Clamp(condition, 0, 100);
    }

    /// <summary>
    /// Byte offset of an inventory slot. Bounds-checked loudly: slot 48 lands on <c>+0x142</c>,
    /// which is still inside the record — it would overwrite the first two spell slots instead of
    /// throwing.
    /// </summary>
    private static int InventoryOffset(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, CharacterFormat.InventorySlots);
        return CharacterFormat.OffInventory + slot * CharacterFormat.InventoryEntrySize;
    }

    /// <summary>Number of inventory slots holding a non-zero item id.</summary>
    public int UsedItemSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < CharacterFormat.InventorySlots; i++)
                if (!GetItem(i).IsEmpty) n++;
            return n;
        }
    }

    /// <summary>
    /// One past the highest occupied inventory slot — the value the game keeps at
    /// <see cref="CharacterFormat.OffItemCount"/>, and how far it scans when listing the pack.
    /// It is a high-water mark, not a population count: the game handed a quest item to slot 9 of a
    /// character carrying three items and wrote 10 here, not 4.
    /// </summary>
    public int InventoryHighWater
    {
        get
        {
            for (int i = CharacterFormat.InventorySlots - 1; i >= 0; i--)
                if (!GetItem(i).IsEmpty) return i + 1;
            return 0;
        }
    }

    // --- spells -----------------------------------------------------------------

    public SpellSlot GetSpell(int slot)
    {
        int o = SpellOffset(slot);
        return new SpellSlot(slot, Bytes[o + CharacterFormat.SpellId], Bytes[o + CharacterFormat.SpellUses]);
    }

    /// <summary>
    /// Writes a spell slot. As with <see cref="SetItem"/>, an id outside the spell table is stored
    /// unchanged rather than clamped — the table's own tail is only inferred, so an unknown id is
    /// not hypothetical and must not be quietly rewritten.
    /// </summary>
    public void SetSpell(int slot, int spellId, int uses)
    {
        int o = SpellOffset(slot);
        // As in SetItem: test the stored value, or a negative id leaves an empty slot with a
        // non-zero use count, which is not a shape the game ever writes.
        int stored = Math.Clamp(spellId, 0, byte.MaxValue);
        Bytes[o + CharacterFormat.SpellId] = (byte)stored;
        Bytes[o + CharacterFormat.SpellUses] = (byte)(stored == 0 ? 0 : Math.Clamp(uses, 0, 255));
    }

    private static int SpellOffset(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, CharacterFormat.SpellSlots);
        return CharacterFormat.OffSpells + slot * CharacterFormat.SpellEntrySize;
    }

    public int UsedSpellSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < CharacterFormat.SpellSlots; i++)
                if (!GetSpell(i).IsEmpty) n++;
            return n;
        }
    }

    /// <summary>
    /// One past the highest occupied spell slot — the value at
    /// <see cref="CharacterFormat.OffSpellCount"/>. Same high-water semantics as
    /// <see cref="InventoryHighWater"/>.
    /// </summary>
    public int SpellHighWater
    {
        get
        {
            for (int i = CharacterFormat.SpellSlots - 1; i >= 0; i--)
                if (!GetSpell(i).IsEmpty) return i + 1;
            return 0;
        }
    }

    // --- validation -------------------------------------------------------------

    /// <summary>
    /// Structural check used by the locator and by the file readers. Deliberately strict, because a
    /// confident wrong address turns one "Max everything" click into a write into unrelated memory:
    /// the occupied flag must be 1, the name must be 1..10 printable characters starting with a
    /// letter and NUL-terminated inside the field, race/class/status must be in range, the level
    /// must be at most 99 (0 is legal — see the note on non-player characters below), current hit
    /// points must not exceed the maximum, and both weights must be plausible.
    /// </summary>
    public static bool IsValidRecord(byte[] buf, int off)
    {
        if (buf == null || off < 0 || off + CharacterFormat.RecordSize > buf.Length) return false;
        if (buf[off + CharacterFormat.OffOccupied] != 1) return false;

        // name: letter first, printable after, NUL inside the field, 1..10 characters
        int len = 0;
        while (len < CharacterFormat.NameFieldLength && buf[off + CharacterFormat.OffName + len] != 0) len++;
        if (len is < 1 or > CharacterFormat.MaxNameLength) return false;
        byte first = buf[off + CharacterFormat.OffName];
        if (!(first is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z')) return false;
        for (int i = 1; i < len; i++)
        {
            byte c = buf[off + CharacterFormat.OffName + i];
            if (c is < 0x20 or > 0x7E) return false;
        }

        if (buf[off + CharacterFormat.OffRace] >= RaceBook.Count) return false;
        if (buf[off + CharacterFormat.OffClass] >= ClassBook.Count) return false;
        if (buf[off + CharacterFormat.OffStatus] >= StatusBook.Count) return false;

        // Rnk 0 is legal: the non-player characters you pick up in the world (the shipped roster's
        // Siegurd is one) carry level 0 and trade 12, which the game prints as "??".
        int level = buf[off + CharacterFormat.OffLevel];
        if (level > 99) return false;
        if (buf[off + CharacterFormat.OffSex] > 1) return false;
        if (buf[off + CharacterFormat.OffAlignment] > 1) return false;

        int maxHp = buf[off + CharacterFormat.OffMaxHp] | (buf[off + CharacterFormat.OffMaxHp + 1] << 8);
        int curHp = buf[off + CharacterFormat.OffCurHp] | (buf[off + CharacterFormat.OffCurHp + 1] << 8);
        if (maxHp is < 1 or > 9999) return false;
        if (curHp > maxHp) return false;

        int maxWeight = buf[off + CharacterFormat.OffMaxWeight] | (buf[off + CharacterFormat.OffMaxWeight + 1] << 8);
        int curWeight = buf[off + CharacterFormat.OffCurWeight] | (buf[off + CharacterFormat.OffCurWeight + 1] << 8);
        if (maxWeight is < 1 or > 30000) return false;
        if (curWeight > maxWeight) return false;

        return true;
    }

    /// <summary>An unused slot: the occupied byte is zero (the game clears it when a slot frees up).</summary>
    public static bool IsEmptySlot(byte[] buf, int off) =>
        buf != null && off >= 0 && off + CharacterFormat.RecordSize <= buf.Length
        && buf[off + CharacterFormat.OffOccupied] == 0;
}
