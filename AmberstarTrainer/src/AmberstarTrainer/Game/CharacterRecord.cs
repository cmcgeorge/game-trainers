namespace AmberstarTrainer.Game;

/// <summary>
/// A typed, mutable view over an <see cref="CharacterFormat.RecordSize"/>-byte Amberstar
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write
/// it back. All multi-byte values are stored big-endian (inherited from the Atari ST origin).
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

    // --- primitive accessors (big-endian) ------------------------------------
    private byte U8(int o) => Bytes[o];
    private void U8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, 0, 255);

    private int U16BE(int o) => (Bytes[o] << 8) | Bytes[o + 1];
    private void U16BE(int o, int v)
    {
        v = Math.Clamp(v, 0, 0xFFFF);
        Bytes[o] = (byte)((v >> 8) & 0xFF);
        Bytes[o + 1] = (byte)(v & 0xFF);
    }

    private long U32BE(int o) =>
        (uint)((Bytes[o] << 24) | (Bytes[o + 1] << 16) | (Bytes[o + 2] << 8) | Bytes[o + 3]);
    private void U32BE(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)((u >> 24) & 0xFF);
        Bytes[o + 1] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 3] = (byte)(u & 0xFF);
    }

    // --- identity ------------------------------------------------------------
    public ushort Magic => (ushort)U16BE(CharacterFormat.OffMagic);
    public int Type { get => U8(CharacterFormat.OffType); set => U8(CharacterFormat.OffType, value); }
    public int Gender { get => U8(CharacterFormat.OffGender); set => U8(CharacterFormat.OffGender, value); }
    public int Race { get => U8(CharacterFormat.OffRace); set => U8(CharacterFormat.OffRace, value); }
    public int Class { get => U8(CharacterFormat.OffClass); set => U8(CharacterFormat.OffClass, value); }
    public int Level { get => U8(CharacterFormat.OffLevel); set => U8(CharacterFormat.OffLevel, value); }

    // --- name (plain ASCII, null-terminated) ---------------------------------
    public string Name
    {
        get
        {
            int len = 0;
            while (len < CharacterFormat.NameLength && Bytes[CharacterFormat.OffName + len] != 0)
                len++;
            return System.Text.Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len);
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (string.IsNullOrEmpty(value)) return;
            int n = Math.Min(value.Length, CharacterFormat.NameLength - 1);
            System.Text.Encoding.ASCII.GetBytes(value, 0, n, Bytes, CharacterFormat.OffName);
        }
    }

    // --- skills (current + max) ----------------------------------------------
    public int GetSkillCur(int index) => U8(CharacterFormat.OffSkillsCur + index);
    public void SetSkillCur(int index, int value) => U8(CharacterFormat.OffSkillsCur + index, value);
    public int GetSkillMax(int index) => U8(CharacterFormat.OffSkillsMax + index);
    public void SetSkillMax(int index, int value) => U8(CharacterFormat.OffSkillsMax + index, value);

    /// <summary>Sets both the current and max value of a skill.</summary>
    public void SetSkill(int index, int value)
    {
        SetSkillCur(index, value);
        SetSkillMax(index, value);
    }

    // --- attributes (current + max, big-endian Words) ------------------------
    public int GetAttrCur(int index) => U16BE(CharacterFormat.OffAttrCur + index * 2);
    public void SetAttrCur(int index, int value) => U16BE(CharacterFormat.OffAttrCur + index * 2, value);
    public int GetAttrMax(int index) => U16BE(CharacterFormat.OffAttrMax + index * 2);
    public void SetAttrMax(int index, int value) => U16BE(CharacterFormat.OffAttrMax + index * 2, value);

    /// <summary>Sets both the current and max value of an attribute.</summary>
    public void SetAttribute(int index, int value)
    {
        SetAttrCur(index, value);
        SetAttrMax(index, value);
    }

    // --- vitals (big-endian Words) -------------------------------------------
    public int HpCur { get => U16BE(CharacterFormat.OffHpCur); set => U16BE(CharacterFormat.OffHpCur, value); }
    public int HpMax { get => U16BE(CharacterFormat.OffHpMax); set => U16BE(CharacterFormat.OffHpMax, value); }
    public int SpCur { get => U16BE(CharacterFormat.OffSpCur); set => U16BE(CharacterFormat.OffSpCur, value); }
    public int SpMax { get => U16BE(CharacterFormat.OffSpMax); set => U16BE(CharacterFormat.OffSpMax, value); }
    public int Slp { get => U16BE(CharacterFormat.OffSlp); set => U16BE(CharacterFormat.OffSlp, value); }

    // --- resources (big-endian Words) ----------------------------------------
    public int Gold { get => U16BE(CharacterFormat.OffGold); set => U16BE(CharacterFormat.OffGold, value); }
    public int Food { get => U16BE(CharacterFormat.OffFood); set => U16BE(CharacterFormat.OffFood, value); }

    // --- combat --------------------------------------------------------------
    public int BaseDef { get => U8(CharacterFormat.OffBaseDef); set => U8(CharacterFormat.OffBaseDef, value); }
    public int BaseDam { get => U8(CharacterFormat.OffBaseDam); set => U8(CharacterFormat.OffBaseDam, value); }
    public int MagicSchools { get => U8(CharacterFormat.OffMagicSchools); set => U8(CharacterFormat.OffMagicSchools, value); }

    // --- ailments ------------------------------------------------------------
    public int PhysicalAilments { get => U8(CharacterFormat.OffPhysicalAilments); set => U8(CharacterFormat.OffPhysicalAilments, value); }
    public int MentalAilments { get => U8(CharacterFormat.OffMentalAilments); set => U8(CharacterFormat.OffMentalAilments, value); }

    // --- experience (big-endian Long) ----------------------------------------
    public long Experience
    {
        get => (long)U32BE(CharacterFormat.OffExperience);
        set => U32BE(CharacterFormat.OffExperience, value);
    }

    // --- spells (big-endian Longs, bitfields) --------------------------------
    public long SpellsWhite { get => (long)U32BE(CharacterFormat.OffSpellsWhite); set => U32BE(CharacterFormat.OffSpellsWhite, value); }
    public long SpellsGrey { get => (long)U32BE(CharacterFormat.OffSpellsGrey); set => U32BE(CharacterFormat.OffSpellsGrey, value); }
    public long SpellsBlack { get => (long)U32BE(CharacterFormat.OffSpellsBlack); set => U32BE(CharacterFormat.OffSpellsBlack, value); }
    public long SpellsSpecial { get => (long)U32BE(CharacterFormat.OffSpellsSpecial); set => U32BE(CharacterFormat.OffSpellsSpecial, value); }

    /// <summary>Learns every spell in all four schools (all bitfields = 0xFFFFFFFF).</summary>
    public void LearnAllSpells()
    {
        SpellsWhite = 0xFFFFFFFF;
        SpellsGrey = 0xFFFFFFFF;
        SpellsBlack = 0xFFFFFFFF;
        SpellsSpecial = 0xFFFFFFFF;
    }

    // --- derived -------------------------------------------------------------
    public string RaceName => RaceBook.Name(Race);
    public string ClassName => ClassBook.Name(Class);
    public string GenderName => Gender == 0 ? "Male" : Gender == 1 ? "Female" : $"?({Gender})";
    public string PhysicalAilmentsName => CharacterFormat.PhysicalAilmentsName(PhysicalAilments);
    public string MentalAilmentsName => CharacterFormat.MentalAilmentsName(MentalAilments);

    /// <summary>
    /// True when this slot holds a real party member: magic header must be 00 FF,
    /// type must be Person (0), and the name must start with a printable letter.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            if (Magic != CharacterFormat.MagicValue) return false;
            if (Type != 0) return false;
            byte first = Bytes[CharacterFormat.OffName];
            if (first < 0x41 || first > 0x7A) return false;
            if (first > 0x5A && first < 0x61) return false;
            return HpMax > 0;
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level} {ClassName})";
}
