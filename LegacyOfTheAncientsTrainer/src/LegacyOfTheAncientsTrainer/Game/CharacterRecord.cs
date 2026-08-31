using System.Text;

namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Legacy of the
/// Ancients character record. The backing <see cref="Bytes"/> array can come from a file, a
/// memory dump, or live process memory; edits mutate the buffer in place so the caller can
/// write it back with a read-validate-write poke.
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

    private int I16(int o) => (short)(Bytes[o] | (Bytes[o + 1] << 8));
    private void I16(int o, int v)
    {
        v = Math.Clamp(v, short.MinValue, short.MaxValue);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    private int I32(int o) => (int)(Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16) | (Bytes[o + 3] << 24));
    private void I32(int o, int v)
    {
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((v >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((v >> 24) & 0xFF);
    }

    // --- header -------------------------------------------------------------
    /// <summary>The record-size field from the header (bytes 4-5). 382 for occupied, 0 for empty.</summary>
    public int HeaderRecordSize =>
        Bytes[CharacterFormat.OffRecordSize] | (Bytes[CharacterFormat.OffRecordSize + 1] << 8);

    /// <summary>True when the header's record-size field is non-zero (occupied slot).</summary>
    public bool IsHeaderOccupied => HeaderRecordSize == CharacterFormat.RecordSize;

    // --- name ---------------------------------------------------------------
    /// <summary>The character's name, as space-padded ASCII from the 15-byte name field.</summary>
    public string Name
    {
        get
        {
            int len = CharacterFormat.NameLength;
            while (len > 0 && Bytes[CharacterFormat.OffName + len - 1] == 0x20) len--;
            if (len == 0)
            {
                len = 0;
                while (len < CharacterFormat.NameLength && Bytes[CharacterFormat.OffName + len] != 0) len++;
            }
            return Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len).TrimEnd('\0');
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (string.IsNullOrEmpty(value)) return;
            var enc = Encoding.ASCII.GetBytes(value);
            int n = Math.Min(enc.Length, CharacterFormat.NameLength);
            Array.Copy(enc, 0, Bytes, CharacterFormat.OffName, n);
            for (int i = n; i < CharacterFormat.NameLength; i++)
                Bytes[CharacterFormat.OffName + i] = 0x20;
        }
    }

    // --- vitals -------------------------------------------------------------
    public int HP
    {
        get => I16(CharacterFormat.OffHP);
        set => I16(CharacterFormat.OffHP, Math.Clamp(value, 0, CharacterFormat.MaxHP));
    }

    public int Level
    {
        get => I16(CharacterFormat.OffLevel);
        set => I16(CharacterFormat.OffLevel, Math.Clamp(value, 1, CharacterFormat.MaxLevelValue));
    }

    // --- characteristics -----------------------------------------------------
    public int GetCharacteristic(int index)
    {
        int off = CharacterFormat.CharacteristicOffsets[index];
        int size = CharacterFormat.CharacteristicSizes[index];
        return size == 4 ? I32(off) : I16(off);
    }

    public void SetCharacteristic(int index, int value)
    {
        int off = CharacterFormat.CharacteristicOffsets[index];
        int size = CharacterFormat.CharacteristicSizes[index];
        value = Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic);
        if (size == 4) I32(off, value);
        else I16(off, value);
    }

    // --- convenience accessors for individual characteristics ----------------
    public int Strength
    {
        get => I32(CharacterFormat.OffStrength);
        set => I32(CharacterFormat.OffStrength, Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic));
    }

    public int Endurance
    {
        get => I32(CharacterFormat.OffEndurance);
        set => I32(CharacterFormat.OffEndurance, Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic));
    }

    public int Dexterity
    {
        get => I16(CharacterFormat.OffDexterity);
        set => I16(CharacterFormat.OffDexterity, Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic));
    }

    public int Intelligence
    {
        get => I32(CharacterFormat.OffIntelligence);
        set => I32(CharacterFormat.OffIntelligence, Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic));
    }

    public int Charm
    {
        get => I32(CharacterFormat.OffCharm);
        set => I32(CharacterFormat.OffCharm, Math.Clamp(value, 1, CharacterFormat.MaxCharacteristic));
    }

    // --- derived ------------------------------------------------------------
    /// <summary>
    /// True when this record holds a real character: the header's record-size field is
    /// 382 and the name starts with a letter.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            if (!IsHeaderOccupied) return false;
            byte first = Bytes[CharacterFormat.OffName];
            if (first == 0x00 || first == 0x20) return false;
            return (first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z');
        }
    }

    /// <summary>
    /// Static validation used by the structural scan: checks the header record-size field,
    /// a printable ASCII name starting with a letter, and plausible characteristic/HP/Level
    /// values against a raw byte buffer at a given offset.
    /// </summary>
    public static bool IsValidRecord(byte[] buf, int o)
    {
        if (o < 0 || o + CharacterFormat.RecordSize > buf.Length) return false;

        // Header: bytes 4-5 must be 0x7E, 0x01 (382 LE) for an occupied record
        int recSize = buf[o + CharacterFormat.OffRecordSize] | (buf[o + CharacterFormat.OffRecordSize + 1] << 8);
        if (recSize != CharacterFormat.RecordSize) return false;

        // Name: starts with a letter, contains printable ASCII or spaces
        int nameOff = o + CharacterFormat.OffName;
        byte first = buf[nameOff];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;
        int nameLen = 0;
        for (int i = 0; i < CharacterFormat.NameLength; i++)
        {
            byte ch = buf[nameOff + i];
            if (ch == 0) break;
            if (ch == 0x20) continue;
            if (ch < 0x20 || ch > 0x7E) return false;
            nameLen++;
        }
        if (nameLen < 2) return false;

        // HP: INTEGER (2-byte LE) in 1..MaxHP
        int hp = (short)(buf[o + CharacterFormat.OffHP] | (buf[o + CharacterFormat.OffHP + 1] << 8));
        if (hp < 1 || hp > CharacterFormat.MaxHP) return false;

        // Level: INTEGER (2-byte LE) in 1..MaxLevel
        int level = (short)(buf[o + CharacterFormat.OffLevel] | (buf[o + CharacterFormat.OffLevel + 1] << 8));
        if (level < 1 || level > CharacterFormat.MaxLevelValue * 2) return false;

        // Strength: LONG (4-byte LE) in 1..PlausibleCharacteristic
        int str = (int)(buf[o + CharacterFormat.OffStrength] | (buf[o + CharacterFormat.OffStrength + 1] << 8) |
                        (buf[o + CharacterFormat.OffStrength + 2] << 16) | (buf[o + CharacterFormat.OffStrength + 3] << 24));
        if (str < 1 || str > CharacterFormat.PlausibleCharacteristic) return false;

        // Endurance: LONG (4-byte LE) in 1..PlausibleCharacteristic
        int end = (int)(buf[o + CharacterFormat.OffEndurance] | (buf[o + CharacterFormat.OffEndurance + 1] << 8) |
                        (buf[o + CharacterFormat.OffEndurance + 2] << 16) | (buf[o + CharacterFormat.OffEndurance + 3] << 24));
        if (end < 1 || end > CharacterFormat.PlausibleCharacteristic) return false;

        // Dexterity: INTEGER (2-byte LE) in 1..PlausibleCharacteristic
        int dex = (short)(buf[o + CharacterFormat.OffDexterity] | (buf[o + CharacterFormat.OffDexterity + 1] << 8));
        if (dex < 1 || dex > CharacterFormat.PlausibleCharacteristic) return false;

        // Intelligence: LONG (4-byte LE) in 1..PlausibleCharacteristic
        int intl = (int)(buf[o + CharacterFormat.OffIntelligence] | (buf[o + CharacterFormat.OffIntelligence + 1] << 8) |
                         (buf[o + CharacterFormat.OffIntelligence + 2] << 16) | (buf[o + CharacterFormat.OffIntelligence + 3] << 24));
        if (intl < 1 || intl > CharacterFormat.PlausibleCharacteristic) return false;

        // Charm: LONG (4-byte LE) in 1..PlausibleCharacteristic
        int cha = (int)(buf[o + CharacterFormat.OffCharm] | (buf[o + CharacterFormat.OffCharm + 1] << 8) |
                        (buf[o + CharacterFormat.OffCharm + 2] << 16) | (buf[o + CharacterFormat.OffCharm + 3] << 24));
        if (cha < 1 || cha > CharacterFormat.PlausibleCharacteristic) return false;

        return true;
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level}, HP {HP})";
}
