using System.Text;

namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// A typed, mutable view over a 422-byte Curse of the Azure Bonds character/monster record.
/// The backing <see cref="Bytes"/> array can be read from a <c>CHRDATAn.SAV</c> file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it back.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[CoabFormat.RecordSize];
        int n = Math.Min(CoabFormat.RecordSize, buffer.Length - offset);
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
            int len = Math.Clamp((int)Bytes[CoabFormat.OffNameLength], 0, CoabFormat.NameMaxLength);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                byte b = Bytes[CoabFormat.OffName + i];
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }
        set
        {
            string s = value ?? "";
            if (s.Length > CoabFormat.NameMaxLength) s = s[..CoabFormat.NameMaxLength];
            Bytes[CoabFormat.OffNameLength] = (byte)s.Length;
            for (int i = 0; i < CoabFormat.NameMaxLength; i++)
                Bytes[CoabFormat.OffName + i] = i < s.Length ? (byte)s[i] : (byte)0;
        }
    }

    // --- ability scores ------------------------------------------------------
    // Curse stores every score as a (current, maximum) pair. The game reads the *current* byte;
    // the maximum is what a Restoration puts back after a drain. Writing only one of the two is
    // the classic way for an edit to appear to work and then evaporate — set a drained Strength
    // and the next restore reverts it; raise the maximum alone and nothing changes at all — so
    // every setter here writes both halves. Read the pair separately with
    // <see cref="GetStatMax"/> when you want to see a drain rather than hide it.
    private int Pair(int off) => U8(off);
    private void Pair(int off, int v)
    {
        U8(off, v);
        U8(off + CoabFormat.StatMaxDelta, v);
    }

    public int Strength { get => Pair(CoabFormat.OffStr); set => Pair(CoabFormat.OffStr, value); }
    public int Intelligence { get => Pair(CoabFormat.OffInt); set => Pair(CoabFormat.OffInt, value); }
    public int Wisdom { get => Pair(CoabFormat.OffWis); set => Pair(CoabFormat.OffWis, value); }
    public int Dexterity { get => Pair(CoabFormat.OffDex); set => Pair(CoabFormat.OffDex, value); }
    public int Constitution { get => Pair(CoabFormat.OffCon); set => Pair(CoabFormat.OffCon, value); }
    public int Charisma { get => Pair(CoabFormat.OffCha); set => Pair(CoabFormat.OffCha, value); }
    public int StrengthPercent { get => Pair(CoabFormat.OffStrPercent); set => Pair(CoabFormat.OffStrPercent, value); }

    private static int StatOffset(int index) => CoabFormat.OffStats + index * CoabFormat.StatStride;

    /// <summary>The score the game is currently using (index 0..5 = STR..CHA).</summary>
    public int GetStat(int index) => U8(StatOffset(index));
    /// <summary>Sets both halves of a score's (current, maximum) pair.</summary>
    public void SetStat(int index, int value) => Pair(StatOffset(index), value);
    /// <summary>The un-drained score a Restoration returns this ability to.</summary>
    public int GetStatMax(int index) => U8(StatOffset(index) + CoabFormat.StatMaxDelta);

    /// <summary>True if any ability score currently reads below its stored maximum — the record's
    /// own evidence of a drain (a shadow, a Ray of Enfeeblement, a night with a wight).</summary>
    public bool IsDrained
    {
        get
        {
            for (int i = 0; i < CoabFormat.StatCount; i++)
                if (GetStat(i) < GetStatMax(i)) return true;
            return false;
        }
    }

    /// <summary>Restores every drained ability score to its stored maximum. Returns true if
    /// anything changed.</summary>
    public bool RestoreDrainedStats()
    {
        bool changed = false;
        for (int i = 0; i < CoabFormat.StatCount; i++)
        {
            int max = GetStatMax(i);
            if (GetStat(i) >= max) continue;
            U8(StatOffset(i), max);
            changed = true;
        }
        return changed;
    }

    // --- identity ------------------------------------------------------------
    public int Race { get => U8(CoabFormat.OffRace); set => U8(CoabFormat.OffRace, value); }
    public int Class { get => U8(CoabFormat.OffClass); set => U8(CoabFormat.OffClass, value); }
    public int Alignment { get => U8(CoabFormat.OffAlignment); set => U8(CoabFormat.OffAlignment, value); }
    public int Gender { get => U8(CoabFormat.OffGender); set => U8(CoabFormat.OffGender, value); }
    public int Age { get => U16(CoabFormat.OffAge); set => U16(CoabFormat.OffAge, value); }

    // --- hit points ----------------------------------------------------------
    public int HpMax { get => U8(CoabFormat.OffHpMax); set => U8(CoabFormat.OffHpMax, value); }
    public int HpCurrent { get => U8(CoabFormat.OffHpCur); set => U8(CoabFormat.OffHpCur, value); }
    public int HpRolled { get => U8(CoabFormat.OffHpRolled); set => U8(CoabFormat.OffHpRolled, value); }

    // --- combat (AC/THAC0 stored inverted: displayed = 60 - stored) ----------
    // The record holds a "base" AC/THAC0 (0xA9/0x2D — the unarmored 10/20 baseline) and a
    // "current" AC/THAC0 (0x111/0x110 — the effective value including armor and modifiers,
    // and what the game actually shows/uses). ArmorClass/Thac0 expose the *effective* value;
    // ArmorClassBase/Thac0Base expose the baseline.
    public int ArmorClass
    {
        get => CoabFormat.InvertBase - U8(CoabFormat.OffAcCur);
        set => U8(CoabFormat.OffAcCur, CoabFormat.InvertBase - value);
    }
    public int ArmorClassBase
    {
        get => CoabFormat.InvertBase - U8(CoabFormat.OffAcBase);
        set => U8(CoabFormat.OffAcBase, CoabFormat.InvertBase - value);
    }
    public int Thac0
    {
        get => CoabFormat.InvertBase - U8(CoabFormat.OffThac0Cur);
        set => U8(CoabFormat.OffThac0Cur, CoabFormat.InvertBase - value);
    }
    public int Thac0Base
    {
        get => CoabFormat.InvertBase - U8(CoabFormat.OffThac0Base);
        set => U8(CoabFormat.OffThac0Base, CoabFormat.InvertBase - value);
    }

    // --- progression ---------------------------------------------------------
    public long Experience { get => U32(CoabFormat.OffExperience); set => U32(CoabFormat.OffExperience, value); }
    public int Status { get => U8(CoabFormat.OffStatus); set => U8(CoabFormat.OffStatus, value); }

    public int GetClassLevel(int index) => U8(CoabFormat.OffClassLevels + index);
    public void SetClassLevel(int index, int value) => U8(CoabFormat.OffClassLevels + index, value);

    // --- money ---------------------------------------------------------------
    public int GetMoney(int index) => U16(CoabFormat.MoneyOffsets[index]);
    public void SetMoney(int index, int value) => U16(CoabFormat.MoneyOffsets[index], value);
    public int Gold { get => U16(CoabFormat.OffGold); set => U16(CoabFormat.OffGold, value); }
    public int Platinum { get => U16(CoabFormat.OffPlatinum); set => U16(CoabFormat.OffPlatinum, value); }
    public int Gems { get => U16(CoabFormat.OffGems); set => U16(CoabFormat.OffGems, value); }
    public int Jewelry { get => U16(CoabFormat.OffJewelry); set => U16(CoabFormat.OffJewelry, value); }

    // --- saving throws & thief skills ---------------------------------------
    public int GetSave(int index) => U8(CoabFormat.OffSaves + index);
    public void SetSave(int index, int value) => U8(CoabFormat.OffSaves + index, value);
    public int GetThiefSkill(int index) => U8(CoabFormat.OffThiefSkills + index);
    public void SetThiefSkill(int index, int value) => U8(CoabFormat.OffThiefSkills + index, value);

    // --- combat icon ---------------------------------------------------------
    /// <summary>Reads one of the six combat-icon color bytes (0..5); each packs two palette nibbles.</summary>
    public int GetIconColor(int index)
    {
        if (index < 0 || index >= CoabFormat.IconColorLen) throw new ArgumentOutOfRangeException(nameof(index));
        return U8(CoabFormat.OffIconColor + index);
    }
    /// <summary>Writes one of the six combat-icon color bytes (0..5).</summary>
    public void SetIconColor(int index, int value)
    {
        if (index < 0 || index >= CoabFormat.IconColorLen) throw new ArgumentOutOfRangeException(nameof(index));
        U8(CoabFormat.OffIconColor + index, value);
    }

    /// <summary>
    /// Randomizes all six combat-icon color bytes, giving the character's battle sprite a random
    /// palette. Each byte's low and high nibble is an independent 0..15 palette index, so this
    /// draws twelve random nibbles. Only the color bytes change; size and everything else are left
    /// untouched.
    /// </summary>
    public void RandomizeIconColors(Random rng)
    {
        for (int i = 0; i < CoabFormat.IconColorLen; i++)
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

    public string RaceName => CoabFormat.RaceName(Race);
    public string ClassName => CoabFormat.ClassName(Class);
    public string AlignmentName => CoabFormat.AlignmentName(Alignment);
    public string GenderName => CoabFormat.GenderName(Gender);
    public string StatusName => CoabFormat.StatusName(Status);

    /// <summary>Best guess of whether this record is a monster rather than a player character.</summary>
    public bool LooksLikeMonster => Race == 0 || Class == 17;

    // Bounds for LooksLikeLiveCombatant. AD&D 1st edition tops out at AC -10 (plate +5 and a
    // shield +5) and the best THAC0 in the game is 5; the ranges below leave headroom either side
    // and still exclude what a zero-filled or text-filled buffer decodes to, which is what they
    // exist for. A scratch buffer reads AC 60 / THAC0 60 (both bytes zero, and the record stores
    // 60 - value), and ASCII bytes decode to large negatives — neither is inside these bounds.
    public const int MinPlausibleAc = -12;
    public const int MaxPlausibleAc = 12;
    public const int MinPlausibleThac0 = 0;
    public const int MaxPlausibleThac0 = 26;

    /// <summary>
    /// Does this record hold a creature the game could actually be fighting with, as opposed to a
    /// scratch buffer that happens to match the record *shape*? The signature scan can straddle a
    /// live record (a stray name string a few bytes ahead of a real monster reads as a record of
    /// its own), and such overlaps decode to impossible combat numbers: AC/THAC0 are stored as
    /// <c>60 - value</c>, so the zero-filled bytes of a scratch buffer decode to AC 60 / THAC0 60.
    /// Real creatures sit comfortably inside AD&amp;D's ranges and never read above their max HP.
    /// </summary>
    public bool LooksLikeLiveCombatant =>
        HpMax > 0 && HpCurrent <= HpMax &&
        ArmorClass >= MinPlausibleAc && ArmorClass <= MaxPlausibleAc &&
        Thac0 >= MinPlausibleThac0 && Thac0 <= MaxPlausibleThac0;

    /// <summary>
    /// Do two records describe the same creature? Used by the poll loop to notice a heap slot the
    /// game has freed and handed to something else, rather than decoding it as the character who
    /// used to live there — and then writing that character's frozen HP into it.
    ///
    /// <para>Compares name, race, class and gender: the fields that identify a creature and that
    /// nothing short of the character-creation screen changes. Deliberately <i>not</i> max HP —
    /// that would look like a strong discriminator and is the obvious thing to add, but it moves
    /// legitimately (a level-up at a training hall, a Manual of Bodily Health), and a party member
    /// whose max HP had gone up would stop refreshing for the rest of the session. Everything else
    /// worth comparing — current HP, status, money, experience — is exactly what a fight or an
    /// errand changes.</para>
    /// </summary>
    public bool IsSameCreatureAs(CharacterRecord other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Name == other.Name && Race == other.Race &&
               Class == other.Class && Gender == other.Gender;
    }

    /// <summary>The single most-representative "level" — the highest non-zero class level.</summary>
    public int EffectiveLevel
    {
        get
        {
            int max = 0;
            for (int i = 0; i < CoabFormat.ClassLevelCount; i++) max = Math.Max(max, GetClassLevel(i));
            return max;
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() =>
        $"{Name} ({GenderName} {RaceName} {ClassName})";
}
