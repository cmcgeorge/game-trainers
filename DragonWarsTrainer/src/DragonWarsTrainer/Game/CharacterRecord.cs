using System.Text;

namespace DragonWarsTrainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="RosterFormat.RecordSize"/>-byte Dragon Wars
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it
/// back. Attributes and vitals are stored as (current, base/max) pairs; helpers expose each.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[RosterFormat.RecordSize];
        int n = Math.Min(RosterFormat.RecordSize, buffer.Length - offset);
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
    private long U32(int o) => (uint)(Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16) | (Bytes[o + 3] << 24));
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- name (Dragon Wars high-bit string encoding) -------------------------
    public string Name
    {
        get
        {
            var sb = new StringBuilder(RosterFormat.NameLength);
            for (int i = 0; i < RosterFormat.NameLength; i++)
            {
                byte b = Bytes[RosterFormat.OffName + i];
                if (b == 0) break;                       // padding: end of string
                sb.Append((char)(b & 0x7F));
                if ((b & 0x80) == 0) break;              // last character has its high bit clear
            }
            return sb.ToString();
        }
        set
        {
            string s = value ?? "";
            if (s.Length > RosterFormat.NameLength) s = s[..RosterFormat.NameLength];
            for (int i = 0; i < RosterFormat.NameLength; i++)
            {
                if (i >= s.Length) { Bytes[RosterFormat.OffName + i] = 0; continue; }
                byte b = (byte)(s[i] & 0x7F);
                // Every character but the last carries its high bit set.
                if (i < s.Length - 1) b |= 0x80;
                Bytes[RosterFormat.OffName + i] = b;
            }
        }
    }

    // --- attributes (current + base) -----------------------------------------
    public int GetAttribute(int index) => U8(RosterFormat.AttributeCurOffsets[index]);
    public int GetAttributeBase(int index) => U8(RosterFormat.AttributeCurOffsets[index] + 1);
    /// <summary>Sets both the current and base value of an attribute.</summary>
    public void SetAttribute(int index, int value)
    {
        int cur = RosterFormat.AttributeCurOffsets[index];
        U8(cur, value);
        U8(cur + 1, value);
    }

    public int Strength { get => GetAttribute(0); set => SetAttribute(0, value); }
    public int Dexterity { get => GetAttribute(1); set => SetAttribute(1, value); }
    public int Intelligence { get => GetAttribute(2); set => SetAttribute(2, value); }
    public int Spirit { get => GetAttribute(3); set => SetAttribute(3, value); }

    // --- vitals (current + max, UInt16) --------------------------------------
    public int HealthCurrent { get => U16(RosterFormat.OffHealthCur); set => U16(RosterFormat.OffHealthCur, value); }
    public int HealthMax { get => U16(RosterFormat.OffHealthMax); set => U16(RosterFormat.OffHealthMax, value); }
    public int StunCurrent { get => U16(RosterFormat.OffStunCur); set => U16(RosterFormat.OffStunCur, value); }
    public int StunMax { get => U16(RosterFormat.OffStunMax); set => U16(RosterFormat.OffStunMax, value); }
    public int PowerCurrent { get => U16(RosterFormat.OffPowerCur); set => U16(RosterFormat.OffPowerCur, value); }
    public int PowerMax { get => U16(RosterFormat.OffPowerMax); set => U16(RosterFormat.OffPowerMax, value); }

    // --- skills --------------------------------------------------------------
    public int GetSkill(int index) => U8(RosterFormat.OffSkills + index);
    public void SetSkill(int index, int value) => U8(RosterFormat.OffSkills + index, value);

    // --- spells (bitfield) ---------------------------------------------------
    public bool GetSpell(int byteIndex, int bit) => (U8(RosterFormat.OffSpells + byteIndex) & (1 << bit)) != 0;
    public void SetSpell(int byteIndex, int bit, bool known)
    {
        int o = RosterFormat.OffSpells + byteIndex;
        int v = U8(o);
        v = known ? (v | (1 << bit)) : (v & ~(1 << bit));
        U8(o, v);
    }
    /// <summary>Marks every spell known (all eight spell bytes = 0xFF).</summary>
    public void LearnAllSpells()
    {
        for (int i = 0; i < RosterFormat.SpellByteCount; i++)
            U8(RosterFormat.OffSpells + i, 0xFF);
    }

    // --- identity / progression ----------------------------------------------
    public int AttackPoints { get => U8(RosterFormat.OffAttackPoints); set => U8(RosterFormat.OffAttackPoints, value); }
    public int Status { get => U8(RosterFormat.OffStatus); set => U8(RosterFormat.OffStatus, value); }
    public int NpcId { get => U8(RosterFormat.OffNpcId); set => U8(RosterFormat.OffNpcId, value); }
    public int Gender { get => U8(RosterFormat.OffGender); set => U8(RosterFormat.OffGender, value); }
    public int Level { get => U16(RosterFormat.OffLevel); set => U16(RosterFormat.OffLevel, value); }
    public long Experience { get => U32(RosterFormat.OffExperience); set => U32(RosterFormat.OffExperience, value); }
    public long Gold { get => U32(RosterFormat.OffGold); set => U32(RosterFormat.OffGold, value); }
    public int ArmorValue { get => U8(RosterFormat.OffArmorValue); set => U8(RosterFormat.OffArmorValue, value); }
    public int DefenseValue { get => U8(RosterFormat.OffDefenseValue); set => U8(RosterFormat.OffDefenseValue, value); }
    public int ArmorClass { get => U8(RosterFormat.OffArmorClass); set => U8(RosterFormat.OffArmorClass, value); }
    public int Flags { get => U8(RosterFormat.OffFlags); set => U8(RosterFormat.OffFlags, value); }

    // --- derived -------------------------------------------------------------
    public string GenderName => RosterFormat.GenderName(Gender);
    public string StatusName => RosterFormat.StatusName(Status);

    /// <summary>
    /// True when this slot holds a real character rather than an empty (0xFF-filled) or blank
    /// slot: the name must start with a printable letter and health max must be plausible.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            byte first = Bytes[RosterFormat.OffName];
            if (first == 0x00 || first == 0xFF) return false;
            char c = (char)(first & 0x7F);
            if (!char.IsLetter(c)) return false;
            int hpMax = HealthMax;
            return hpMax is > 0 and < 10000;
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level} {GenderName})";
}
