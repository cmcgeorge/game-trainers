namespace EyeOfTheBeholder1Trainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Eye of the Beholder I
/// character record. The backing <see cref="Bytes"/> array can come from a save file, a memory dump,
/// or live process memory; edits mutate the buffer in place so the caller can write it back.
///
/// Abilities are stored as (modified, base) byte pairs; setters update both together so the
/// game's recalculated modified value is never left stale. Hit points are single bytes (max 255).
/// Armor class is a signed byte (lower is better; -10 is the AD&D best).
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
    private int I8(int o) => (sbyte)Bytes[o];
    private void I8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, -128, 127);
    private int U16(int o) => Bytes[o] | (Bytes[o + 1] << 8);
    private void U16(int o, int v)
    {
        v = Math.Clamp(v, 0, 0xFFFF);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
    }
    private long U32(int o) => (uint)(Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16) | (Bytes[o + 3] << 24));
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- identity ------------------------------------------------------------
    public int CharId { get => U8(CharacterFormat.OffCharId); set => U8(CharacterFormat.OffCharId, value); }
    public int Active { get => U8(CharacterFormat.OffActive); set => U8(CharacterFormat.OffActive, value); }

    // --- name (plain ASCII, null-terminated, max 10 chars) -------------------
    public string Name
    {
        get
        {
            int len = 0;
            for (int i = 0; i < CharacterFormat.NameLength; i++)
            {
                if (Bytes[CharacterFormat.OffName + i] == 0) break;
                len++;
            }
            return System.Text.Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len);
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (string.IsNullOrEmpty(value)) return;
            int len = Math.Min(value.Length, CharacterFormat.NameLength);
            System.Text.Encoding.ASCII.GetBytes(value, 0, len, Bytes, CharacterFormat.OffName);
        }
    }

    // --- abilities (modified + base, set together) --------------------------
    public int GetAbility(int index) => U8(CharacterFormat.AbilityModOffsets[index]);
    public int GetAbilityBase(int index) => U8(CharacterFormat.AbilityModOffsets[index] + 1);
    public void SetAbility(int index, int value)
    {
        int mod = CharacterFormat.AbilityModOffsets[index];
        U8(mod, value);
        U8(mod + 1, value);
    }

    public int Strength { get => GetAbility(0); set => SetAttributeWithExc(0, value); }
    public int Intelligence { get => GetAbility(1); set => SetAbility(1, value); }
    public int Wisdom { get => GetAbility(2); set => SetAbility(2, value); }
    public int Dexterity { get => GetAbility(3); set => SetAbility(3, value); }
    public int Constitution { get => GetAbility(4); set => SetAbility(4, value); }
    public int Charisma { get => GetAbility(5); set => SetAbility(5, value); }

    /// <summary>Sets Strength and clears exceptional strength when Strength is not 18.</summary>
    private void SetAttributeWithExc(int index, int value)
    {
        SetAbility(index, value);
        if (value != 18)
        {
            U8(CharacterFormat.OffStrExcMod, 0);
            U8(CharacterFormat.OffStrExcBase, 0);
        }
    }

    // --- exceptional strength (fighters only) -------------------------------
    public int StrExcModified { get => U8(CharacterFormat.OffStrExcMod); set => U8(CharacterFormat.OffStrExcMod, value); }
    public int StrExcBase { get => U8(CharacterFormat.OffStrExcBase); set => U8(CharacterFormat.OffStrExcBase, value); }

    // --- vitals --------------------------------------------------------------
    public int HpCurrent { get => U8(CharacterFormat.OffHpCur); set => U8(CharacterFormat.OffHpCur, value); }
    public int HpMax { get => U8(CharacterFormat.OffHpMax); set => U8(CharacterFormat.OffHpMax, value); }
    public int ArmorClass { get => I8(CharacterFormat.OffAC); set => I8(CharacterFormat.OffAC, value); }
    public int Food { get => U8(CharacterFormat.OffFood); set => U8(CharacterFormat.OffFood, value); }

    // --- identity / progression ----------------------------------------------
    public int Race { get => U8(CharacterFormat.OffRace); set => U8(CharacterFormat.OffRace, value); }
    public int Class { get => U8(CharacterFormat.OffClass); set => U8(CharacterFormat.OffClass, value); }
    public int Alignment { get => U8(CharacterFormat.OffAlignment); set => U8(CharacterFormat.OffAlignment, value); }
    public int Portrait { get => U8(CharacterFormat.OffPortrait); set => U8(CharacterFormat.OffPortrait, value); }

    public int Level1 { get => U8(CharacterFormat.OffLevel1); set => U8(CharacterFormat.OffLevel1, value); }
    public int Level2 { get => U8(CharacterFormat.OffLevel2); set => U8(CharacterFormat.OffLevel2, value); }
    public int Level3 { get => U8(CharacterFormat.OffLevel3); set => U8(CharacterFormat.OffLevel3, value); }

    public long Xp1 { get => U32(CharacterFormat.OffXp1); set => U32(CharacterFormat.OffXp1, value); }
    public long Xp2 { get => U32(CharacterFormat.OffXp2); set => U32(CharacterFormat.OffXp2, value); }
    public long Xp3 { get => U32(CharacterFormat.OffXp3); set => U32(CharacterFormat.OffXp3, value); }

    // --- derived convenience -------------------------------------------------
    public int EffectiveLevel => Math.Max(Level1, Math.Max(Level2, Level3));
    public long TotalXp => Xp1 + Xp2 + Xp3;

    public string RaceName => CharacterFormat.RaceName(Race);
    public string ClassName => CharacterFormat.ClassName(Class);
    public string AlignmentName => CharacterFormat.AlignmentName(Alignment);

    // --- occupancy check -----------------------------------------------------
    /// <summary>
    /// True when this slot holds an active party member: Active flag is 1 and the name starts
    /// with a printable letter. Empty slots have Active = 0 and a zeroed or blank name.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            if (Active != 1) return false;
            byte first = Bytes[CharacterFormat.OffName];
            return first >= 'A' && first <= 'z';
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{EffectiveLevel} {ClassName})";
}
