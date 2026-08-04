namespace Wizardry1Trainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Wizardry 1
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write
/// it back. Names use UCSD Pascal STRING[15] encoding (byte 0 = length, bytes 1-15 = ASCII).
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

    // --- name (UCSD Pascal STRING[15]) --------------------------------------
    public string Name
    {
        get
        {
            int len = U8(CharacterFormat.OffName);
            if (len <= 0 || len > 15) return "";
            var chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = (char)Bytes[CharacterFormat.OffName + 1 + i];
            return new string(chars);
        }
        set
        {
            string s = (value ?? "").ToUpperInvariant();
            int len = Math.Min(s.Length, 15);
            U8(CharacterFormat.OffName, len);
            for (int i = 0; i < 15; i++)
            {
                int idx = CharacterFormat.OffName + 1 + i;
                Bytes[idx] = i < len ? (byte)s[i] : (byte)0;
            }
        }
    }

    // --- password (STRING[15], not normally edited) -------------------------
    public string Password
    {
        get
        {
            int len = U8(CharacterFormat.OffPassword);
            if (len <= 0 || len > 15) return "";
            var chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = (char)Bytes[CharacterFormat.OffPassword + 1 + i];
            return new string(chars);
        }
    }

    // --- identity ------------------------------------------------------------
    public int InMaze { get => U16(CharacterFormat.OffInMaze); set => U16(CharacterFormat.OffInMaze, value); }
    public int Race { get => U16(CharacterFormat.OffRace); set => U16(CharacterFormat.OffRace, Math.Clamp(value, 1, 5)); }
    public int Class { get => U16(CharacterFormat.OffClass); set => U16(CharacterFormat.OffClass, Math.Clamp(value, 0, 7)); }
    public int Status { get => U16(CharacterFormat.OffStatus); set => U16(CharacterFormat.OffStatus, value); }
    public int Alignment { get => U16(CharacterFormat.OffAlignment); set => U16(CharacterFormat.OffAlignment, Math.Clamp(value, 1, 3)); }

    // --- attributes (packed 5-bit) ------------------------------------------
    public int Strength
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).str;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, value, i, p, v, a, l); }
    }
    public int Intelligence
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).Int;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, s, value, p, v, a, l); }
    }
    public int Piety
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).pie;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, s, i, value, v, a, l); }
    }
    public int Vitality
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).vit;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, s, i, p, value, a, l); }
    }
    public int Agility
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).agi;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, s, i, p, v, value, l); }
    }
    public int Luck
    {
        get => CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes).luk;
        set { var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(Bytes, CharacterFormat.OffAttributes); CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, s, i, p, v, a, value); }
    }

    public int GetAttribute(int index) => index switch
    {
        0 => Strength, 1 => Intelligence, 2 => Piety, 3 => Vitality, 4 => Agility, 5 => Luck, _ => 0
    };

    public void SetAttribute(int index, int value)
    {
        switch (index)
        {
            case 0: Strength = value; break;
            case 1: Intelligence = value; break;
            case 2: Piety = value; break;
            case 3: Vitality = value; break;
            case 4: Agility = value; break;
            case 5: Luck = value; break;
        }
    }

    /// <summary>Sets all six attributes to the same value.</summary>
    public void SetAllAttributes(int value)
    {
        CharacterFormat.WriteAttributes(Bytes, CharacterFormat.OffAttributes, value, value, value, value, value, value);
    }

    // --- gold / experience (TWIZLONG) ---------------------------------------
    public long Gold
    {
        get => CharacterFormat.ReadWizLong(Bytes, CharacterFormat.OffGold);
        set => CharacterFormat.WriteWizLong(Bytes, CharacterFormat.OffGold, value);
    }
    public long Experience
    {
        get => CharacterFormat.ReadWizLong(Bytes, CharacterFormat.OffExperience);
        set => CharacterFormat.WriteWizLong(Bytes, CharacterFormat.OffExperience, value);
    }

    // --- progression --------------------------------------------------------
    public int LastLevel { get => U16(CharacterFormat.OffLastLevel); set => U16(CharacterFormat.OffLastLevel, Math.Clamp(value, 0, CharacterFormat.MaxLevel)); }
    public int Level { get => U16(CharacterFormat.OffLevel); set => U16(CharacterFormat.OffLevel, Math.Clamp(value, 1, CharacterFormat.MaxLevel)); }
    public int HpCurrent { get => U16(CharacterFormat.OffHpCurrent); set => U16(CharacterFormat.OffHpCurrent, Math.Clamp(value, 0, CharacterFormat.MaxHp)); }
    public int HpMax { get => U16(CharacterFormat.OffHpMax); set => U16(CharacterFormat.OffHpMax, Math.Clamp(value, 1, CharacterFormat.MaxHp)); }

    // --- spells --------------------------------------------------------------
    /// <summary>Returns true if spell bit N (0..49) is known.</summary>
    public bool GetSpellKnown(int index)
    {
        int byteIdx = index >> 3;
        int bit = index & 7;
        return (U8(CharacterFormat.OffSpellKnowledge + byteIdx) & (1 << bit)) != 0;
    }

    /// <summary>Sets the knowledge state of spell bit N (0..49).</summary>
    public void SetSpellKnown(int index, bool known)
    {
        int byteIdx = index >> 3;
        int bit = index & 7;
        int o = CharacterFormat.OffSpellKnowledge + byteIdx;
        int v = U8(o);
        v = known ? (v | (1 << bit)) : (v & ~(1 << bit));
        U8(o, v);
    }

    /// <summary>Learns all 50 spells (sets all 8 spell-knowledge bytes to 0xFF, with the
    /// unused bits 50-63 set as well -- harmless because the game only checks 0..49).</summary>
    public void LearnAllSpells()
    {
        for (int i = 0; i < CharacterFormat.SpellKnowledgeBytes; i++)
            U8(CharacterFormat.OffSpellKnowledge + i, 0xFF);
    }

    /// <summary>Gets the number of spell charges for mage spell level L (1..7).</summary>
    public int GetMageSpellCharges(int level) => U16(CharacterFormat.OffMageSpells + (level - 1) * 2);

    /// <summary>Sets the number of spell charges for mage spell level L (1..7).</summary>
    public void SetMageSpellCharges(int level, int charges) => U16(CharacterFormat.OffMageSpells + (level - 1) * 2, Math.Clamp(charges, 0, CharacterFormat.MaxSpellCharges));

    /// <summary>Gets the number of spell charges for priest spell level L (1..7).</summary>
    public int GetPriestSpellCharges(int level) => U16(CharacterFormat.OffPriestSpells + (level - 1) * 2);

    /// <summary>Sets the number of spell charges for priest spell level L (1..7).</summary>
    public void SetPriestSpellCharges(int level, int charges) => U16(CharacterFormat.OffPriestSpells + (level - 1) * 2, Math.Clamp(charges, 0, CharacterFormat.MaxSpellCharges));

    /// <summary>Sets all mage and priest spell charges to the given value.</summary>
    public void SetAllSpellCharges(int charges)
    {
        for (int lvl = 1; lvl <= CharacterFormat.SpellLevels; lvl++)
        {
            SetMageSpellCharges(lvl, charges);
            SetPriestSpellCharges(lvl, charges);
        }
    }

    // --- combat stats --------------------------------------------------------
    public int ArmorClass { get => U16(CharacterFormat.OffArmorClass); set => U16(CharacterFormat.OffArmorClass, value); }
    public int ArmorClassLast { get => U16(CharacterFormat.OffArmorClassLast); set => U16(CharacterFormat.OffArmorClassLast, value); }

    // --- position (lost location) -------------------------------------------
    public int PositionLevel { get => U16(CharacterFormat.OffPosition); set => U16(CharacterFormat.OffPosition, value); }
    public int PositionX { get => U16(CharacterFormat.OffPosition + 2); set => U16(CharacterFormat.OffPosition + 2, value); }
    public int PositionY { get => U16(CharacterFormat.OffPosition + 4); set => U16(CharacterFormat.OffPosition + 4, value); }
    public int PositionFacing { get => U16(CharacterFormat.OffPosition + 6); set => U16(CharacterFormat.OffPosition + 6, value); }

    // --- honors --------------------------------------------------------------
    public int Honors { get => U8(CharacterFormat.OffHonors); set => U8(CharacterFormat.OffHonors, value); }

    // --- derived / display ---------------------------------------------------
    public string RaceName => CharacterFormat.RaceName(Race);
    public string ClassName => CharacterFormat.ClassName(Class);
    public string AlignmentName => CharacterFormat.AlignmentName(Alignment);
    public string StatusName => CharacterFormat.StatusName(Status);

    /// <summary>
    /// True when this slot holds a real character rather than an empty slot: the name must
    /// start with a letter (length 1..15, first char A-Z), race must be 1..5, class 0..7,
    /// alignment 1..3, and HP max must be plausible.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            int len = Bytes[CharacterFormat.OffName];
            if (len < 1 || len > 15) return false;
            char first = (char)Bytes[CharacterFormat.OffName + 1];
            if (first < 'A' || first > 'Z') return false;
            int race = U16(CharacterFormat.OffRace);
            if (race < 1 || race > 5) return false;
            int cls = U16(CharacterFormat.OffClass);
            if (cls > 7) return false;
            int align = U16(CharacterFormat.OffAlignment);
            if (align < 1 || align > 3) return false;
            int hpMax = HpMax;
            return hpMax is > 0 and <= 999;
        }
    }

    public CharacterRecord Clone() => new(Bytes);
    public override string ToString() => $"{Name} (L{Level} {ClassName})";
}
