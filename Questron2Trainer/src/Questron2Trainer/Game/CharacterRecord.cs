using System.Text;

namespace Questron2Trainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Questron II
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it
/// back with a read-validate-write poke.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[CharacterFormat.RecordSize];
        int n = Math.Min(CharacterFormat.RecordSize, buffer.Length - offset);
        if (n > 0) Array.Copy(buffer, offset, Bytes, 0, n);
    }

    // --- primitive accessors -------------------------------------------------
    private byte U8(int o) => Bytes[o];
    private void U8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, 0, 255);
    private int U16(int o) => Bytes[o] | (Bytes[o + 1] << 8);
    private void U16(int o, int v)
    {
        v = Math.Clamp(v, 0, 0xFFFF);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    // --- name ---------------------------------------------------------------
    /// <summary>The character's name, as null-terminated ASCII from the 16-byte name field.</summary>
    public string Name
    {
        get
        {
            int len = 0;
            while (len < CharacterFormat.NameLength && Bytes[CharacterFormat.OffName + len] != 0)
                len++;
            return Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len);
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (string.IsNullOrEmpty(value)) return;
            var enc = Encoding.ASCII.GetBytes(value);
            int n = Math.Min(enc.Length, CharacterFormat.NameLength - 1);
            Array.Copy(enc, 0, Bytes, CharacterFormat.OffName, n);
        }
    }

    // --- vitals (uint16 LE) -------------------------------------------------
    public int HP { get => U16(CharacterFormat.OffHP); set => U16(CharacterFormat.OffHP, value); }
    public int Food { get => U16(CharacterFormat.OffFood); set => U16(CharacterFormat.OffFood, value); }
    public int Gold { get => U16(CharacterFormat.OffGold); set => U16(CharacterFormat.OffGold, value); }

    // --- attributes (one byte each) -----------------------------------------
    public int GetAttribute(int index) => U8(CharacterFormat.OffAttributes + index);

    /// <summary>Sets an attribute, clamped to 1..MaxAttribute.</summary>
    public void SetAttribute(int index, int value) =>
        U8(CharacterFormat.OffAttributes + index, Math.Clamp(value, 1, CharacterFormat.MaxAttribute));

    // --- equipment ----------------------------------------------------------
    public int Weapon { get => U8(CharacterFormat.OffWeapon); set => U8(CharacterFormat.OffWeapon, Math.Clamp(value, 0, WeaponBook.Count - 1)); }
    public int Armor { get => U8(CharacterFormat.OffArmor); set => U8(CharacterFormat.OffArmor, Math.Clamp(value, 0, ArmorBook.Count - 1)); }

    // --- progression --------------------------------------------------------
    public int Level
    {
        get => U8(CharacterFormat.OffLevel);
        set => U8(CharacterFormat.OffLevel, Math.Clamp(value, 0, CharacterFormat.MaxLevel));
    }

    // --- spells -------------------------------------------------------------
    public int GetSpellCharges(int slot) => U8(CharacterFormat.OffSpellCharges + slot);
    public void SetSpellCharges(int slot, int value) =>
        U8(CharacterFormat.OffSpellCharges + slot, Math.Clamp(value, 0, CharacterFormat.MaxSpellCharges));

    /// <summary>Sets all spell charges to the given value.</summary>
    public void SetAllSpellCharges(int value)
    {
        for (int i = 0; i < CharacterFormat.SpellSlotCount; i++)
            SetSpellCharges(i, value);
    }

    // --- derived ------------------------------------------------------------
    public string LevelName => CharacterFormat.LevelName(Level);

    /// <summary>
    /// True when this record holds a real character: a printable ASCII name starting with a
    /// letter, plausible HP/Food/Gold, five attributes in 1..25, and a level in 0..20.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            byte first = Bytes[CharacterFormat.OffName];
            if (first == 0x00 || first == 0xFF) return false;
            if (first < 'A' || (first > 'Z' && first < 'a') || first > 'z') return false;
            int hp = HP;
            if (hp < 1 || hp > CharacterFormat.MaxHP) return false;
            int food = Food;
            if (food > CharacterFormat.MaxFood) return false;
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            {
                int a = Bytes[CharacterFormat.OffAttributes + i];
                if (a < 1 || a > CharacterFormat.MaxAttribute) return false;
            }
            int lvl = Level;
            return lvl >= 0 && lvl <= CharacterFormat.MaxLevel;
        }
    }

    /// <summary>
    /// Static validation used by the structural scan: checks the same fields as
    /// <see cref="IsOccupied"/> but against a raw byte buffer at a given offset. Stricter
    /// than IsOccupied — requires a 2..15 character name, all attributes in 1..25, and
    /// plausible vitals — to reject stray byte runs that merely start with a letter.
    /// </summary>
    public static bool IsValidRecord(byte[] buf, int o)
    {
        if (o < 0 || o + CharacterFormat.RecordSize > buf.Length) return false;

        // Name: 2..15 printable ASCII chars starting with a letter, null-terminated
        int nameOff = o + CharacterFormat.OffName;
        byte first = buf[nameOff];
        if (first == 0x00 || first == 0xFF) return false;
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;
        int nameLen = 0;
        for (int i = 0; i < CharacterFormat.NameLength; i++)
        {
            byte ch = buf[nameOff + i];
            if (ch == 0) break;
            if (ch < 0x20 || ch > 0x7E) return false;
            nameLen++;
        }
        if (nameLen < 2 || nameLen > 15) return false;

        // HP: uint16 LE in 1..MaxHP (tightened from PlausibleHP, which uint16 can never exceed)
        int hp = buf[o + CharacterFormat.OffHP] | (buf[o + CharacterFormat.OffHP + 1] << 8);
        if (hp < 1 || hp > CharacterFormat.MaxHP) return false;

        // Food: uint16 LE in 0..MaxFood (tightened from PlausibleHP, which uint16 can never exceed)
        int food = buf[o + CharacterFormat.OffFood] | (buf[o + CharacterFormat.OffFood + 1] << 8);
        if (food > CharacterFormat.MaxFood) return false;

        // Gold: uint16 LE in 0..65535 (the full uint16 range is legitimate)
        int gold = buf[o + CharacterFormat.OffGold] | (buf[o + CharacterFormat.OffGold + 1] << 8);

        // Five attributes: each in 1..25
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int a = buf[o + CharacterFormat.OffAttributes + i];
            if (a < 1 || a > CharacterFormat.MaxAttribute) return false;
        }

        // Weapon and armor in valid table range
        int weapon = buf[o + CharacterFormat.OffWeapon];
        if (weapon >= WeaponBook.Count) return false;
        int armor = buf[o + CharacterFormat.OffArmor];
        if (armor >= ArmorBook.Count) return false;

        // Spell charges: each in 0..MaxSpellCharges
        for (int i = 0; i < CharacterFormat.SpellSlotCount; i++)
        {
            int sc = buf[o + CharacterFormat.OffSpellCharges + i];
            if (sc > CharacterFormat.MaxSpellCharges) return false;
        }

        // Level: 0..20
        int lvl = buf[o + CharacterFormat.OffLevel];
        return lvl <= CharacterFormat.MaxLevel;
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level} {LevelName})";
}
