namespace AlternateRealityTrainer.Game;

/// <summary>
/// A typed, mutable view over an <c>Alternate Reality: The City</c> character block.
///
/// The view wraps a caller-owned buffer (either a live read of the game's memory or the contents of
/// an <c>ARCCD</c><i>nn</i> file). Every setter writes into that buffer and then reports the exact
/// byte range it touched through the <c>flush</c> delegate supplied at construction, so the caller
/// can push just those bytes back into the game instead of rewriting all 12 KB. Pass <c>null</c> for
/// <c>flush</c> to get an offline view (which is what the verification harness uses).
/// </summary>
public sealed class CharacterRecord
{
    private readonly byte[] _buf;
    private readonly int _base;
    private readonly Action<int, int>? _flush;

    /// <summary>The backing buffer. Not copied — the caller keeps ownership.</summary>
    public byte[] Buffer => _buf;

    /// <summary>Offset of this record inside <see cref="Buffer"/>.</summary>
    public int BaseOffset => _base;

    /// <param name="buffer">Backing bytes; not copied.</param>
    /// <param name="baseOffset">Where the record starts inside <paramref name="buffer"/>.</param>
    /// <param name="flush">
    /// Called after each write with the <b>record-relative</b> offset and length that changed.
    /// Because the offset is record-relative and callers use it to index the buffer directly, a
    /// non-zero <paramref name="baseOffset"/> is rejected when a flush delegate is supplied — the
    /// two conventions would silently disagree and write the wrong bytes to the right address.
    /// </param>
    public CharacterRecord(byte[] buffer, int baseOffset = 0, Action<int, int>? flush = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (baseOffset < 0 || baseOffset + CharacterFormat.RecordSize > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(baseOffset),
                $"A character record needs {CharacterFormat.RecordSize} bytes at the given offset.");
        if (baseOffset != 0 && flush != null)
            throw new ArgumentException(
                "Flush offsets are record-relative, so a flushing record must start at offset 0.",
                nameof(baseOffset));
        _buf = buffer;
        _base = baseOffset;
        _flush = flush;
    }

    // --- primitive access ----------------------------------------------------

    private byte GetU8(int off) => CharacterFormat.ReadU8(_buf, _base + off);
    private ushort GetU16(int off) => CharacterFormat.ReadU16(_buf, _base + off);
    private uint GetU32(int off) => CharacterFormat.ReadU32(_buf, _base + off);

    private void SetU8(int off, byte v)
    {
        CharacterFormat.WriteU8(_buf, _base + off, v);
        _flush?.Invoke(off, 1);
    }

    private void SetU16(int off, ushort v)
    {
        CharacterFormat.WriteU16(_buf, _base + off, v);
        _flush?.Invoke(off, 2);
    }

    private void SetU32(int off, uint v)
    {
        CharacterFormat.WriteU32(_buf, _base + off, v);
        _flush?.Invoke(off, 4);
    }

    // --- identity ------------------------------------------------------------

    /// <summary>
    /// The character's name. Assigning a name that would not survive
    /// <see cref="CharacterFormat.IsWritableName"/> — empty, or not starting with a letter — is
    /// ignored, because <see cref="CharacterFormat.LooksLikeRecord"/> would then refuse to
    /// recognise the record and the trainer could no longer find it.
    /// </summary>
    public string Name
    {
        get => CharacterFormat.ReadName(_buf, _base);
        set
        {
            if (!CharacterFormat.IsWritableName(value)) return;
            CharacterFormat.WriteName(_buf, value, _base);
            _flush?.Invoke(CharacterFormat.OffName, CharacterFormat.NameLength);
        }
    }

    // --- calendar (read-only: the game rewrites these every tick) ------------

    public int Minute => GetU8(CharacterFormat.OffMinute);
    public int Hour => GetU8(CharacterFormat.OffHour);
    public int Day => GetU8(CharacterFormat.OffDay);
    public int MonthIndex => GetU8(CharacterFormat.OffMonth);
    public int Year => GetU16(CharacterFormat.OffYear);

    public string MonthName
    {
        get
        {
            int i = MonthIndex;
            return i >= 0 && i < GameFacts.Months.Count ? GameFacts.Months[i] : $"month {i}";
        }
    }

    /// <summary>The in-game date and time as the game itself would phrase it.</summary>
    public string DateTimeText =>
        $"Hour {Hour} of day {Day}, month of {MonthName}, year {Year} since abduction";

    // --- attributes ----------------------------------------------------------

    /// <summary>Reads attribute <paramref name="index"/> (see <see cref="AttributeBook"/>).</summary>
    public byte GetAttribute(int index) => GetU8(CharacterFormat.AttributeOffset(index));

    /// <summary>
    /// Writes attribute <paramref name="index"/>. All three parallel copies (value / maximum /
    /// natural maximum) are set together, so a drained or temporarily boosted state cannot undo the
    /// edit on the game's next recalculation.
    /// </summary>
    public void SetAttribute(int index, int value)
    {
        int off = CharacterFormat.AttributeOffset(index);
        byte v = (byte)Math.Clamp(value, 1, CharacterFormat.AttributeCeiling);
        for (int i = 0; i < CharacterFormat.AttributeCopies; i++)
            CharacterFormat.WriteU8(_buf, _base + off + i, v);
        _flush?.Invoke(off, CharacterFormat.AttributeCopies);
    }

    /// <summary>Sub-point progress toward the next whole point of attribute <paramref name="index"/>. [Inferred]</summary>
    public byte GetAttributeFraction(int index) =>
        GetU8(CharacterFormat.AttributeOffset(index) + CharacterFormat.AttributeFractionOffset);

    // --- progression ---------------------------------------------------------

    public int Level
    {
        get => GetU8(CharacterFormat.OffLevel);
        set => SetU8(CharacterFormat.OffLevel, (byte)Math.Clamp(value, 0, CharacterFormat.LevelCeiling));
    }

    /// <summary>
    /// Total experience. Raising it past the next-level threshold carries the threshold up with it,
    /// for the same reason <see cref="HitPoints"/> carries its maximum: the game never holds a
    /// threshold below the experience, and <see cref="CharacterFormat.LooksLikeRecord"/> rejects a
    /// window that does — so an editor that could create one would lose the character it had just
    /// edited. This is the field most likely to be typed into, so it is the one that matters most.
    /// </summary>
    public uint Experience
    {
        get => GetU32(CharacterFormat.OffExperience);
        set
        {
            uint v = Math.Min(value, CharacterFormat.ExperienceCeiling);
            if (v > NextLevelExperience) SetU32(CharacterFormat.OffNextLevelExp, v);
            SetU32(CharacterFormat.OffExperience, v);
        }
    }

    /// <summary>
    /// Experience the game wants before the next level. It recomputes this itself on every level-up
    /// (to twice the current experience), so writing it only delays or brings forward one level.
    /// It is never allowed below <see cref="Experience"/>.
    /// </summary>
    public uint NextLevelExperience
    {
        get => GetU32(CharacterFormat.OffNextLevelExp);
        set
        {
            uint v = Math.Min(value, CharacterFormat.ExperienceCeiling);
            SetU32(CharacterFormat.OffNextLevelExp, Math.Max(v, Experience));
        }
    }

    /// <summary>
    /// Current hit points. Raising these past the maximum raises the maximum with them: the game's
    /// own records never hold <c>hp &gt; hpMax</c>, and <see cref="CharacterFormat.LooksLikeRecord"/>
    /// rejects a window that does — so letting the editor create one would make the trainer unable
    /// to find the very character it had just edited.
    /// </summary>
    public uint HitPoints
    {
        get => GetU32(CharacterFormat.OffHitPoints);
        set
        {
            uint v = Math.Min(value, CharacterFormat.HitPointCeiling);
            if (v > HitPointsMax) SetU32(CharacterFormat.OffHitPointsMax, v);
            SetU32(CharacterFormat.OffHitPoints, v);
        }
    }

    /// <summary>
    /// Maximum hit points. Lowering these below the current value pulls the current value down with
    /// them, for the same reason <see cref="HitPoints"/> pushes the maximum up.
    /// </summary>
    public uint HitPointsMax
    {
        get => GetU32(CharacterFormat.OffHitPointsMax);
        set
        {
            uint v = Math.Clamp(value, 1, CharacterFormat.HitPointCeiling);
            SetU32(CharacterFormat.OffHitPointsMax, v);
            if (HitPoints > v) SetU32(CharacterFormat.OffHitPoints, v);
        }
    }

    // --- money and carried goods ---------------------------------------------

    public ushort Gold { get => GetU16(CharacterFormat.OffGold); set => SetU16(CharacterFormat.OffGold, value); }
    public ushort Silver { get => GetU16(CharacterFormat.OffSilver); set => SetU16(CharacterFormat.OffSilver, value); }
    public ushort Copper { get => GetU16(CharacterFormat.OffCopper); set => SetU16(CharacterFormat.OffCopper, value); }
    public ushort Gems { get => GetU16(CharacterFormat.OffGems); set => SetU16(CharacterFormat.OffGems, value); }
    public ushort Jewelry { get => GetU16(CharacterFormat.OffJewelry); set => SetU16(CharacterFormat.OffJewelry, value); }

    public byte Food { get => GetU8(CharacterFormat.OffFood); set => SetU8(CharacterFormat.OffFood, value); }
    public byte Water { get => GetU8(CharacterFormat.OffWater); set => SetU8(CharacterFormat.OffWater, value); }
    public byte Crystals { get => GetU8(CharacterFormat.OffCrystals); set => SetU8(CharacterFormat.OffCrystals, value); }
    public byte Keys { get => GetU8(CharacterFormat.OffKeys); set => SetU8(CharacterFormat.OffKeys, value); }

    public bool HasCompass
    {
        get => GetU8(CharacterFormat.OffCompass) != 0;
        set => SetU8(CharacterFormat.OffCompass, value ? (byte)1 : (byte)0);
    }

    public bool HasWatch
    {
        get => GetU8(CharacterFormat.OffWatch) != 0;
        set => SetU8(CharacterFormat.OffWatch, value ? (byte)1 : (byte)0);
    }

    // --- bulk actions --------------------------------------------------------

    /// <summary>Restores hit points to their maximum.</summary>
    public void FullHeal() => HitPoints = HitPointsMax;

    /// <summary>Raises every attribute to <see cref="CharacterFormat.MaxAttribute"/>.</summary>
    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            SetAttribute(i, CharacterFormat.MaxAttribute);
    }

    /// <summary>Sets hit points and their maximum to <see cref="CharacterFormat.MaxHitPoints"/>.</summary>
    public void MaxHealth()
    {
        HitPointsMax = CharacterFormat.MaxHitPoints;
        HitPoints = CharacterFormat.MaxHitPoints;
    }

    /// <summary>Fills every coin and valuables field to <see cref="CharacterFormat.MaxCoins"/>.</summary>
    public void MaxMoney()
    {
        Gold = CharacterFormat.MaxCoins;
        Silver = CharacterFormat.MaxCoins;
        Copper = CharacterFormat.MaxCoins;
        Gems = CharacterFormat.MaxCoins;
        Jewelry = CharacterFormat.MaxCoins;
    }

    /// <summary>Fills food, water, crystals and keys, and grants the compass and the watch.</summary>
    public void MaxSupplies()
    {
        Food = CharacterFormat.MaxSupply;
        Water = CharacterFormat.MaxSupply;
        Crystals = CharacterFormat.MaxSupply;
        Keys = CharacterFormat.MaxSupply;
        HasCompass = true;
        HasWatch = true;
    }

    /// <summary>
    /// Brings the next level within reach by setting experience to the threshold the game is
    /// waiting for. The game levels one step at a time and then recomputes the threshold, so this
    /// grants exactly one level rather than an unbounded run of them.
    /// </summary>
    /// <returns>
    /// False when experience could not be advanced — already at
    /// <see cref="CharacterFormat.ExperienceCeiling"/>, so there is nothing left to give.
    /// </returns>
    public bool LevelUp()
    {
        uint current = Experience;
        if (current >= CharacterFormat.ExperienceCeiling) return false;

        uint target = NextLevelExperience;
        // Saturating: a garbage read of uint.MaxValue must not wrap the increment round to zero and
        // wipe the character's experience.
        if (target <= current) target = current + 1;
        Experience = Math.Min(target, CharacterFormat.ExperienceCeiling);
        return Experience > current;
    }

    /// <summary>Everything above except <see cref="LevelUp"/>, which changes the character's pace.</summary>
    public void MaxEverything()
    {
        MaxAttributes();
        MaxHealth();
        MaxMoney();
        MaxSupplies();
    }

    /// <summary>A one-line summary for the character list and the status bar.</summary>
    public string Summary =>
        $"{Name}  —  level {Level}, {HitPoints}/{HitPointsMax} hp, {Experience:N0} exp";
}
