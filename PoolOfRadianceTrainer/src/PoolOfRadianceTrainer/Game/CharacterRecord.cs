using System.Text;

namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// A typed, mutable view over a 285-byte Pool of Radiance character/monster record.
/// The backing <see cref="Bytes"/> array can be read from a file, a memory dump, or live
/// process memory; edits mutate the buffer in place so the caller can write it back.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[PorFormat.RecordSize];
        int n = Math.Min(PorFormat.RecordSize, buffer.Length - offset);
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

    // --- name ---------------------------------------------------------------
    public string Name
    {
        get
        {
            int len = Math.Clamp((int)Bytes[PorFormat.OffNameLength], 0, PorFormat.NameMaxLength);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                byte b = Bytes[PorFormat.OffName + i];
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }
        set
        {
            string s = value ?? "";
            if (s.Length > PorFormat.NameMaxLength) s = s[..PorFormat.NameMaxLength];
            Bytes[PorFormat.OffNameLength] = (byte)s.Length;
            for (int i = 0; i < PorFormat.NameMaxLength; i++)
                Bytes[PorFormat.OffName + i] = i < s.Length ? (byte)s[i] : (byte)0;
        }
    }

    // --- ability scores ------------------------------------------------------
    public int Strength { get => U8(PorFormat.OffStr); set => U8(PorFormat.OffStr, value); }
    public int Intelligence { get => U8(PorFormat.OffInt); set => U8(PorFormat.OffInt, value); }
    public int Wisdom { get => U8(PorFormat.OffWis); set => U8(PorFormat.OffWis, value); }
    public int Dexterity { get => U8(PorFormat.OffDex); set => U8(PorFormat.OffDex, value); }
    public int Constitution { get => U8(PorFormat.OffCon); set => U8(PorFormat.OffCon, value); }
    public int Charisma { get => U8(PorFormat.OffCha); set => U8(PorFormat.OffCha, value); }
    public int StrengthPercent { get => U8(PorFormat.OffStrPercent); set => U8(PorFormat.OffStrPercent, value); }

    public int GetStat(int index) => U8(PorFormat.OffStr + index);
    public void SetStat(int index, int value) => U8(PorFormat.OffStr + index, value);

    // --- identity ------------------------------------------------------------
    public int Race { get => U8(PorFormat.OffRace); set => U8(PorFormat.OffRace, value); }
    public int Class { get => U8(PorFormat.OffClass); set => U8(PorFormat.OffClass, value); }
    public int Alignment { get => U8(PorFormat.OffAlignment); set => U8(PorFormat.OffAlignment, value); }
    public int Gender { get => U8(PorFormat.OffGender); set => U8(PorFormat.OffGender, value); }
    public int Age { get => U16(PorFormat.OffAge); set => U16(PorFormat.OffAge, value); }

    // --- hit points ----------------------------------------------------------
    public int HpMax { get => U8(PorFormat.OffHpMax); set => U8(PorFormat.OffHpMax, value); }
    public int HpCurrent { get => U8(PorFormat.OffHpCur); set => U8(PorFormat.OffHpCur, value); }
    public int HpRolled { get => U8(PorFormat.OffHpRolled); set => U8(PorFormat.OffHpRolled, value); }

    // --- combat (AC/THAC0 stored inverted: displayed = 60 - stored) ----------
    // The record holds a "base" AC/THAC0 (0xA9/0x2D — the unarmored 10/20 baseline) and a
    // "current" AC/THAC0 (0x111/0x110 — the effective value including armor and modifiers,
    // and what the game actually shows/uses). ArmorClass/Thac0 expose the *effective* value;
    // ArmorClassBase/Thac0Base expose the baseline.
    public int ArmorClass
    {
        get => PorFormat.InvertBase - U8(PorFormat.OffAcCur);
        set => U8(PorFormat.OffAcCur, PorFormat.InvertBase - value);
    }
    public int ArmorClassBase
    {
        get => PorFormat.InvertBase - U8(PorFormat.OffAcBase);
        set => U8(PorFormat.OffAcBase, PorFormat.InvertBase - value);
    }
    public int Thac0
    {
        get => PorFormat.InvertBase - U8(PorFormat.OffThac0Cur);
        set => U8(PorFormat.OffThac0Cur, PorFormat.InvertBase - value);
    }
    public int Thac0Base
    {
        get => PorFormat.InvertBase - U8(PorFormat.OffThac0Base);
        set => U8(PorFormat.OffThac0Base, PorFormat.InvertBase - value);
    }

    // --- progression ---------------------------------------------------------
    public long Experience { get => U32(PorFormat.OffExperience); set => U32(PorFormat.OffExperience, value); }
    public int Status { get => U8(PorFormat.OffStatus); set => U8(PorFormat.OffStatus, value); }

    public int GetClassLevel(int index) => U8(PorFormat.OffClassLevels + index);
    public void SetClassLevel(int index, int value) => U8(PorFormat.OffClassLevels + index, value);

    // --- money ---------------------------------------------------------------
    public int GetMoney(int index) => U16(PorFormat.MoneyOffsets[index]);
    public void SetMoney(int index, int value) => U16(PorFormat.MoneyOffsets[index], value);
    public int Gold { get => U16(PorFormat.OffGold); set => U16(PorFormat.OffGold, value); }
    public int Platinum { get => U16(PorFormat.OffPlatinum); set => U16(PorFormat.OffPlatinum, value); }
    public int Gems { get => U16(PorFormat.OffGems); set => U16(PorFormat.OffGems, value); }
    public int Jewelry { get => U16(PorFormat.OffJewelry); set => U16(PorFormat.OffJewelry, value); }

    // --- saving throws & thief skills ---------------------------------------
    public int GetSave(int index) => U8(PorFormat.OffSaves + index);
    public void SetSave(int index, int value) => U8(PorFormat.OffSaves + index, value);
    public int GetThiefSkill(int index) => U8(PorFormat.OffThiefSkills + index);
    public void SetThiefSkill(int index, int value) => U8(PorFormat.OffThiefSkills + index, value);

    // --- combat icon ---------------------------------------------------------
    /// <summary>Reads one of the six combat-icon color bytes (0..5); each packs two palette nibbles.</summary>
    public int GetIconColor(int index)
    {
        if (index < 0 || index >= PorFormat.IconColorLen) throw new ArgumentOutOfRangeException(nameof(index));
        return U8(PorFormat.OffIconColor + index);
    }
    /// <summary>Writes one of the six combat-icon color bytes (0..5).</summary>
    public void SetIconColor(int index, int value)
    {
        if (index < 0 || index >= PorFormat.IconColorLen) throw new ArgumentOutOfRangeException(nameof(index));
        U8(PorFormat.OffIconColor + index, value);
    }

    /// <summary>
    /// Randomizes all six combat-icon color bytes, giving the character's battle sprite a random
    /// palette. Each byte's low and high nibble is an independent 0..15 palette index, so this
    /// draws twelve random nibbles. Only the color bytes change; size and everything else are left
    /// untouched.
    /// </summary>
    public void RandomizeIconColors(Random rng)
    {
        for (int i = 0; i < PorFormat.IconColorLen; i++)
        {
            int lo = rng.Next(16);
            int hi = rng.Next(16);
            SetIconColor(i, lo | (hi << 4));
        }
    }

    // --- derived -------------------------------------------------------------
    /// <summary>Displayed 18/xx exceptional strength, or a plain number.</summary>
    public string StrengthDisplay =>
        Strength == 18 && StrengthPercent > 0
            ? $"18/{(StrengthPercent >= 100 ? "00" : StrengthPercent.ToString("D2"))}"
            : Strength.ToString();

    public string RaceName => PorFormat.RaceName(Race);
    public string ClassName => PorFormat.ClassName(Class);
    public string AlignmentName => PorFormat.AlignmentName(Alignment);
    public string GenderName => PorFormat.GenderName(Gender);
    public string StatusName => PorFormat.StatusName(Status);

    /// <summary>Best guess of whether this record is a monster rather than a player character.</summary>
    public bool LooksLikeMonster => Race == 0 || Class == 17;

    /// <summary>The single most-representative "level" — the highest non-zero class level.</summary>
    public int EffectiveLevel
    {
        get
        {
            int max = 0;
            for (int i = 0; i < PorFormat.ClassLevelCount; i++) max = Math.Max(max, GetClassLevel(i));
            return max;
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() =>
        $"{Name} ({GenderName} {RaceName} {ClassName})";
}
