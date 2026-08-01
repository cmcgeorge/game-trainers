using System.Buffers.Binary;
using System.Text;

namespace HillsfarTrainer.Game;

/// <summary>
/// A typed, mutable view over the 188 bytes of a Hillsfar character.
///
/// <para>Every setter clamps to the range the game itself accepts and then reports the exact byte
/// range it touched through a <c>flush</c> delegate, so the live shell writes 1–4 bytes back into
/// the emulator rather than the whole record. That matters: the record sits next to bytes the game
/// rewrites constantly (the clock, the eighteen per-hour timers), and blind whole-record writes
/// would fight it.</para>
///
/// <para>The buffer is the caller's; this type never copies it. Construct it over a window read from
/// the game, or over a file's contents for offline editing — the two are the same 188 bytes.</para>
/// </summary>
public sealed class CharacterRecord
{
    private readonly byte[] _buf;
    private readonly int _start;
    private readonly Action<int, int>? _flush;

    /// <summary>
    /// Wraps a buffer holding a character record.
    /// </summary>
    /// <param name="buffer">Backing store; must hold a full record at <paramref name="start"/>.</param>
    /// <param name="start">Offset of the record within <paramref name="buffer"/>.</param>
    /// <param name="flush">
    /// Called with (offset-within-record, length) after every mutation. Null for a detached record —
    /// useful for file editing, where the whole buffer is written once at the end.
    /// </param>
    public CharacterRecord(byte[] buffer, int start = 0, Action<int, int>? flush = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (start < 0 || buffer.Length - start < CharacterFormat.RecordLength)
            throw new ArgumentException(
                $"buffer must hold {CharacterFormat.RecordLength} bytes at offset {start}",
                nameof(buffer));
        _buf = buffer;
        _start = start;
        _flush = flush;
    }

    /// <summary>The bytes behind this record.</summary>
    public ReadOnlySpan<byte> Bytes =>
        _buf.AsSpan(_start, CharacterFormat.RecordLength);

    // --- primitive access -----------------------------------------------------

    private byte Get(int off) => _buf[_start + off];

    private void Set(int off, byte value)
    {
        _buf[_start + off] = value;
        _flush?.Invoke(off, 1);
    }

    private ushort GetWord(int off) =>
        BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(_start + off, 2));

    private void SetWord(int off, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(_start + off, 2), value);
        _flush?.Invoke(off, 2);
    }

    private uint GetDword(int off) =>
        BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(_start + off, 4));

    private void SetDword(int off, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(_start + off, 4), value);
        _flush?.Invoke(off, 4);
    }

    private static byte Clamp(int value, int lo, int hi) =>
        (byte)(value < lo ? lo : value > hi ? hi : value);

    // --- identity -------------------------------------------------------------

    /// <summary>
    /// The character's name, up to <see cref="CharacterFormat.MaxNameLength"/> characters.
    ///
    /// <para>The setter rewrites the <b>whole</b> 16-byte field — NUL terminator, then spaces, then
    /// the final NUL — rather than just the new text. That is deliberate: the game builds the save
    /// filename from the raw leading bytes and ignores the terminator, so overwriting "Christopher"
    /// with a short name and leaving the tail intact produces a file called <c>ZZTOPOPH.HIL</c>.
    /// Observed, not theorised.</para>
    /// </summary>
    public string Name
    {
        get
        {
            var span = _buf.AsSpan(_start + CharacterFormat.OffName, CharacterFormat.NameFieldLength);
            int end = span.IndexOf((byte)0);
            if (end < 0) end = span.Length;
            return Encoding.ASCII.GetString(span[..end]).TrimEnd();
        }
        set
        {
            string text = (value ?? string.Empty).Trim();
            if (text.Length > CharacterFormat.MaxNameLength)
                text = text[..CharacterFormat.MaxNameLength];

            var span = _buf.AsSpan(_start + CharacterFormat.OffName, CharacterFormat.NameFieldLength);
            span.Fill((byte)' ');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                span[i] = (byte)(c >= 0x20 && c <= 0x7E ? c : '?');
            }
            span[text.Length] = 0;                          // terminator the game reads
            span[CharacterFormat.NameFieldLength - 1] = 0;   // the field's own guaranteed NUL
            _flush?.Invoke(CharacterFormat.OffName, CharacterFormat.NameFieldLength);
        }
    }

    /// <summary>Race index, 0..5. See <see cref="RaceBook"/>.</summary>
    public int Race
    {
        get => Get(CharacterFormat.OffRace);
        set => Set(CharacterFormat.OffRace, Clamp(value, 0, RaceBook.Races.Count - 1));
    }

    /// <summary>Gender index: 0 male, 1 female.</summary>
    public int Gender
    {
        get => Get(CharacterFormat.OffGender);
        set => Set(CharacterFormat.OffGender, Clamp(value, 0, RaceBook.Genders.Count - 1));
    }

    /// <summary>Alignment index, 0..8. See <see cref="AlignmentBook"/>.</summary>
    public int Alignment
    {
        get => Get(CharacterFormat.OffAlignment);
        set => Set(CharacterFormat.OffAlignment, Clamp(value, 0, AlignmentBook.Alignments.Count - 1));
    }

    /// <summary>Age, as the game stores it — a 16-bit word.</summary>
    public int Age
    {
        get => GetWord(CharacterFormat.OffAge);
        set => SetWord(CharacterFormat.OffAge, (ushort)Math.Clamp(value, 0, ushort.MaxValue));
    }

    /// <summary>
    /// The class bitmask as stored — the low nibble, with the high nibble kept in step.
    /// Writing this also updates the class index at <see cref="CharacterFormat.OffClassIndex"/>
    /// so the two representations cannot disagree.
    /// </summary>
    public int ClassMask
    {
        get => Get(CharacterFormat.OffClassMask) & 0x0F;
        set
        {
            int mask = value & 0x0F;
            if (!ClassBook.IsLegalMask(mask)) return;   // never store a combination the game rejects
            Set(CharacterFormat.OffClassMask, ClassBook.PackMask(mask));
            Set(CharacterFormat.OffClassIndex, ClassBook.IndexForMask(mask));
        }
    }

    /// <summary>The class index as stored. Read-only here — set <see cref="ClassMask"/> instead.</summary>
    public int ClassIndex => Get(CharacterFormat.OffClassIndex);

    /// <summary>The game's name for the current class combination.</summary>
    public string ClassName => ClassBook.NameForMask(ClassMask);

    // --- abilities ------------------------------------------------------------

    /// <summary>Strength, clamped to the range the game rolls.</summary>
    public int Strength
    {
        get => Get(CharacterFormat.OffStrength);
        set => Set(CharacterFormat.OffStrength,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    /// <summary>
    /// Exceptional-strength percentile — the <c>(nn)</c> the sheet prints after an 18. The game only
    /// gives this to fighters; 0 means "none".
    /// </summary>
    public int StrengthPercentile
    {
        get => Get(CharacterFormat.OffStrengthPercentile);
        set => Set(CharacterFormat.OffStrengthPercentile,
                   Clamp(value, 0, CharacterFormat.MaxStrengthPercentile));
    }

    /// <summary>Intelligence.</summary>
    public int Intelligence
    {
        get => Get(CharacterFormat.OffIntelligence);
        set => Set(CharacterFormat.OffIntelligence,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    /// <summary>Wisdom.</summary>
    public int Wisdom
    {
        get => Get(CharacterFormat.OffWisdom);
        set => Set(CharacterFormat.OffWisdom,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    /// <summary>Dexterity — the best stat in the game; drives aim drift and thief skills.</summary>
    public int Dexterity
    {
        get => Get(CharacterFormat.OffDexterity);
        set => Set(CharacterFormat.OffDexterity,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    /// <summary>Constitution — also sets the natural healing rate.</summary>
    public int Constitution
    {
        get => Get(CharacterFormat.OffConstitution);
        set => Set(CharacterFormat.OffConstitution,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    /// <summary>Charisma.</summary>
    public int Charisma
    {
        get => Get(CharacterFormat.OffCharisma);
        set => Set(CharacterFormat.OffCharisma,
                   Clamp(value, CharacterFormat.MinAbility, CharacterFormat.MaxAbility));
    }

    // --- vitals ---------------------------------------------------------------

    /// <summary>
    /// Current hit points. Clamped to <see cref="HitPointsMax"/>, because the clock's own healing
    /// code does exactly that and a current above max would be undone on the next tick anyway.
    /// </summary>
    public int HitPoints
    {
        get => Get(CharacterFormat.OffHitPoints);
        set => Set(CharacterFormat.OffHitPoints, Clamp(value, 0, HitPointsMax));
    }

    /// <summary>
    /// Maximum hit points. Raising this does not raise the current total — use
    /// <see cref="HealFully"/> for that.
    ///
    /// <para>Lowering it <b>does</b> bring the current total down with it. Leaving current above
    /// maximum would produce a record that
    /// <see cref="CharacterFormat.LooksLikeRecord"/> itself rejects, so the next auto-locate would
    /// report "no character loaded" for a character that is plainly on screen, and the offline editor
    /// would quietly drop the file from its list.</para>
    /// </summary>
    public int HitPointsMax
    {
        get => Get(CharacterFormat.OffHitPointsMax);
        set
        {
            byte max = Clamp(value, 1, CharacterFormat.MaxByte);
            Set(CharacterFormat.OffHitPointsMax, max);
            if (Get(CharacterFormat.OffHitPoints) > max)
                Set(CharacterFormat.OffHitPoints, max);
        }
    }

    /// <summary>Gold carried.</summary>
    public uint Gold
    {
        get => GetDword(CharacterFormat.OffGold);
        set => SetDword(CharacterFormat.OffGold, value);
    }

    /// <summary>Experience points.</summary>
    public uint Experience
    {
        get => GetDword(CharacterFormat.OffExperience);
        set => SetDword(CharacterFormat.OffExperience, value);
    }

    // --- levels ---------------------------------------------------------------

    /// <summary>Cleric level.</summary>
    public int ClericLevel
    {
        get => Get(CharacterFormat.OffLevelCleric);
        set => Set(CharacterFormat.OffLevelCleric, Clamp(value, 0, CharacterFormat.MaxByte));
    }

    /// <summary>Magic-User level.</summary>
    public int MagicUserLevel
    {
        get => Get(CharacterFormat.OffLevelMagicUser);
        set => Set(CharacterFormat.OffLevelMagicUser, Clamp(value, 0, CharacterFormat.MaxByte));
    }

    /// <summary>Fighter level.</summary>
    public int FighterLevel
    {
        get => Get(CharacterFormat.OffLevelFighter);
        set => Set(CharacterFormat.OffLevelFighter, Clamp(value, 0, CharacterFormat.MaxByte));
    }

    /// <summary>Thief level.</summary>
    public int ThiefLevel
    {
        get => Get(CharacterFormat.OffLevelThief);
        set => Set(CharacterFormat.OffLevelThief, Clamp(value, 0, CharacterFormat.MaxByte));
    }

    /// <summary>
    /// The level the character sheet shows — the level of the highest-numbered class the character
    /// actually has, matching what a single-class character sees.
    /// </summary>
    public int DisplayLevel
    {
        get
        {
            var info = ClassBook.ForMask(ClassMask);
            if (info is null) return 0;
            int best = 0;
            if (info.Value.IsCleric) best = Math.Max(best, ClericLevel);
            if (info.Value.IsMagicUser) best = Math.Max(best, MagicUserLevel);
            if (info.Value.IsFighter) best = Math.Max(best, FighterLevel);
            if (info.Value.IsThief) best = Math.Max(best, ThiefLevel);
            return best;
        }
    }

    // --- consumables ----------------------------------------------------------

    /// <summary>Knock rings carried. The game's own purchase code caps this at 99.</summary>
    public int KnockRings
    {
        get => Get(CharacterFormat.OffKnockRings);
        set => Set(CharacterFormat.OffKnockRings, Clamp(value, 0, CharacterFormat.MaxConsumable));
    }

    /// <summary>Healing potions carried. Capped at 99 by the game.</summary>
    public int HealingPotions
    {
        get => Get(CharacterFormat.OffHealingPotions);
        set => Set(CharacterFormat.OffHealingPotions, Clamp(value, 0, CharacterFormat.MaxConsumable));
    }

    /// <summary>Archery-range level. The game caps this at 15, and five mission steps gate on it.</summary>
    public int ArcheryLevel
    {
        get => Get(CharacterFormat.OffArcheryLevel);
        set => Set(CharacterFormat.OffArcheryLevel,
                   Clamp(value, 0, CharacterFormat.MaxArcheryLevel));
    }

    // --- clock ----------------------------------------------------------------

    /// <summary>
    /// Hour of day, 1..24 — the value the status panel prints. Hour 24 is midnight and shows as
    /// "am". This is the field to write when a building you need is shut.
    /// </summary>
    public int Hour
    {
        get => Get(CharacterFormat.OffHour);
        set => Set(CharacterFormat.OffHour, Clamp(value, 1, CharacterFormat.HoursPerDay));
    }

    /// <summary>Day counter; the clock bumps it when the hour reaches 24.</summary>
    public int Day
    {
        get => GetWord(CharacterFormat.OffDay);
        set => SetWord(CharacterFormat.OffDay, (ushort)Math.Clamp(value, 0, ushort.MaxValue));
    }

    /// <summary>The clock as the game would print it, e.g. <c>"3 pm"</c>.</summary>
    public string HourText => GameFacts.FormatHour(Hour);

    /// <summary>Hours until the next natural heal — the game resets this to 24 after each one.</summary>
    public int HealCountdown
    {
        get => Get(CharacterFormat.OffHealCountdown);
        set => Set(CharacterFormat.OffHealCountdown,
                   Clamp(value, 0, CharacterFormat.HoursPerDay));
    }

    // --- thief skills ---------------------------------------------------------

    /// <summary>
    /// The three thief-skill percentages at <see cref="CharacterFormat.OffThiefSkills"/>. These are
    /// Inferred, not Confirmed — they vary with Dexterity across the two shipped thieves and the
    /// third matches AD&amp;D <i>Climb Walls</i> at level 6 — so they are decoded and round-tripped
    /// here but deliberately <b>not</b> surfaced in any view-model or in the XAML. Do not add an edit
    /// box for a field no live write test has confirmed.
    /// </summary>
    public IReadOnlyList<int> ThiefSkills
    {
        get
        {
            var v = new int[CharacterFormat.ThiefSkillCount];
            for (int i = 0; i < v.Length; i++) v[i] = Get(CharacterFormat.OffThiefSkills + i);
            return v;
        }
    }

    /// <summary>Sets one thief-skill percentage, clamped to 0..99.</summary>
    public void SetThiefSkill(int index, int value)
    {
        if (index < 0 || index >= CharacterFormat.ThiefSkillCount) return;
        Set(CharacterFormat.OffThiefSkills + index, Clamp(value, 0, 99));
    }

    // --- bulk actions ---------------------------------------------------------

    /// <summary>Restores current hit points to the maximum.</summary>
    public void HealFully() => HitPoints = HitPointsMax;

    /// <summary>Sets every ability score to the highest the game rolls.</summary>
    public void MaxAbilities()
    {
        Strength = CharacterFormat.MaxAbility;
        Intelligence = CharacterFormat.MaxAbility;
        Wisdom = CharacterFormat.MaxAbility;
        Dexterity = CharacterFormat.MaxAbility;
        Constitution = CharacterFormat.MaxAbility;
        Charisma = CharacterFormat.MaxAbility;
    }

    /// <summary>Fills both consumable counters to the game's own cap of 99.</summary>
    public void MaxConsumables()
    {
        KnockRings = CharacterFormat.MaxConsumable;
        HealingPotions = CharacterFormat.MaxConsumable;
    }

    /// <summary>
    /// Sets the levels of the classes the character actually has to <paramref name="level"/>, leaving
    /// the others alone. Levels the character does not have are meaningless — the game reads only the
    /// entries the class mask selects.
    /// </summary>
    public void SetLevelsForOwnClasses(int level)
    {
        var info = ClassBook.ForMask(ClassMask);
        if (info is null) return;
        if (info.Value.IsCleric) ClericLevel = level;
        if (info.Value.IsMagicUser) MagicUserLevel = level;
        if (info.Value.IsFighter) FighterLevel = level;
        if (info.Value.IsThief) ThiefLevel = level;
    }

    /// <summary>
    /// Adds one level to each class the character actually has, leaving the others alone.
    ///
    /// <para>This increments each class's own byte rather than levelling every class up to the
    /// highest. A multi-class character normally has <i>different</i> levels — the game splits
    /// experience between the classes and has a separate level-up path per class — so a
    /// Cleric 9 / Fighter 3 character advancing to Cleric 10 / Fighter 4 is right, and to
    /// Cleric 10 / Fighter 10 is not.</para>
    /// </summary>
    public void AdvanceOwnClasses()
    {
        var info = ClassBook.ForMask(ClassMask);
        if (info is null) return;
        if (info.Value.IsCleric) ClericLevel = Math.Min(ClericLevel + 1, CharacterFormat.MaxByte);
        if (info.Value.IsMagicUser) MagicUserLevel = Math.Min(MagicUserLevel + 1, CharacterFormat.MaxByte);
        if (info.Value.IsFighter) FighterLevel = Math.Min(FighterLevel + 1, CharacterFormat.MaxByte);
        if (info.Value.IsThief) ThiefLevel = Math.Min(ThiefLevel + 1, CharacterFormat.MaxByte);
    }

    /// <summary>
    /// A one-line summary for the auto-locate report, so the user can check it against the game's
    /// own status panel before trusting the attach.
    /// </summary>
    public string Summary() =>
        $"{Name} — {RaceBook.NameForGender(Gender)} {RaceBook.NameForRace(Race)} " +
        $"{ClassName}, level {DisplayLevel}, {AlignmentBook.NameFor(Alignment)}, " +
        $"HP {HitPoints}/{HitPointsMax}, {Gold} gold, {Experience} exp, {HourText}";
}
