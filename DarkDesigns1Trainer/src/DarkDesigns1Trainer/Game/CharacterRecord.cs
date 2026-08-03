namespace DarkDesigns1Trainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Dark Designs I
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it
/// back.
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

    // --- name ----------------------------------------------------------------
    public string Name
    {
        get
        {
            int len = Math.Min((int)Bytes[CharacterFormat.OffNameLen], CharacterFormat.NameLength);
            if (len <= 0) return "";
            return System.Text.Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len).TrimEnd('\0');
        }
        set
        {
            string s = value ?? "";
            int len = Math.Min(s.Length, CharacterFormat.NameLength);
            Bytes[CharacterFormat.OffNameLen] = (byte)len;
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (len > 0)
                System.Text.Encoding.ASCII.GetBytes(s, 0, len, Bytes, CharacterFormat.OffName);
        }
    }

    // --- class / level -------------------------------------------------------
    public int Class
    {
        get => U8(CharacterFormat.OffClass);
        set => U8(CharacterFormat.OffClass, value);
    }

    public int Level
    {
        get => U8(CharacterFormat.OffLevel);
        set => U8(CharacterFormat.OffLevel, value);
    }

    // --- attributes (uint16 LE) ----------------------------------------------
    public int GetAttribute(int index) => U16(CharacterFormat.AttributeOffsets[index]);
    public void SetAttribute(int index, int value) => U16(CharacterFormat.AttributeOffsets[index], value);

    public int Strength { get => GetAttribute(0); set => SetAttribute(0, value); }
    public int Dexterity { get => GetAttribute(1); set => SetAttribute(1, value); }
    public int Constitution { get => GetAttribute(2); set => SetAttribute(2, value); }
    public int Intelligence { get => GetAttribute(3); set => SetAttribute(3, value); }
    public int Piety { get => GetAttribute(4); set => SetAttribute(4, value); }

    // --- vitals (uint16 LE) --------------------------------------------------
    public int BodyCurrent { get => U16(CharacterFormat.OffBodyCur); set => U16(CharacterFormat.OffBodyCur, value); }
    public int BodyMax { get => U16(CharacterFormat.OffBodyMax); set => U16(CharacterFormat.OffBodyMax, value); }
    public int MagicCurrent { get => U16(CharacterFormat.OffMagicCur); set => U16(CharacterFormat.OffMagicCur, value); }

    // --- progression ---------------------------------------------------------
    public int Experience { get => U16(CharacterFormat.OffExperience); set => U16(CharacterFormat.OffExperience, value); }
    public int Gold { get => U16(CharacterFormat.OffGold); set => U16(CharacterFormat.OffGold, value); }

    // --- status --------------------------------------------------------------
    public int Status { get => U16(CharacterFormat.OffStatus); set => U16(CharacterFormat.OffStatus, value); }

    // --- derived -------------------------------------------------------------
    public string ClassName => CharacterFormat.ClassName(Class);
    public string StatusName => CharacterFormat.StatusName(Status);

    /// <summary>
    /// True when this slot holds a real character: exists flag = 1 and name starts with a letter.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            if (Bytes[CharacterFormat.OffExists] != 1) return false;
            char c = (char)Bytes[CharacterFormat.OffName];
            return char.IsLetter(c);
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level} {ClassName})";
}
