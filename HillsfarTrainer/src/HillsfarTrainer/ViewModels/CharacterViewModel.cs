using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Windows.Input;
using HillsfarTrainer.Game;
using HillsfarTrainer.Memory;

namespace HillsfarTrainer.ViewModels;

/// <summary>One field that can be pinned to a value on every poll tick.</summary>
/// <param name="Label">Name shown in the freeze list.</param>
/// <param name="DgroupOffset">Offset inside the data segment.</param>
/// <param name="Width">1, 2 or 4 bytes.</param>
/// <param name="Min">
/// Lowest value the game itself accepts here. The freeze path writes raw bytes rather than going
/// through <see cref="CharacterRecord"/>, so without a range of its own it would be the one write
/// path in the trainer that can put an illegal value into the game.
/// </param>
/// <param name="Max">Highest value the game itself accepts here.</param>
/// <param name="CeilingRecordOffset">
/// Optional record offset of a byte that caps this field at run time, or -1. Hit points need it:
/// their real ceiling is the character's own maximum, not 255, and pinning current above maximum
/// produces a record <see cref="CharacterFormat.LooksLikeRecord"/> rejects — which would leave the
/// trainer unable to find the character it had just broken.
/// </param>
public readonly record struct FreezeTarget(
    string Label, int DgroupOffset, int Width, long Min, long Max,
    int CeilingRecordOffset = -1)
{
    /// <summary>Offset of the field within the 188-byte character record.</summary>
    public int RecordOffset => DgroupOffset - CharacterFormat.DgroupRecordOffset;

    /// <summary>Brings a value inside the static range the game accepts.</summary>
    public long Clamp(long value) => value < Min ? Min : value > Max ? Max : value;

    /// <summary>
    /// Brings a value inside the range the game accepts <i>for this record</i>, honouring
    /// <see cref="CeilingRecordOffset"/> when one is set.
    /// </summary>
    public long ClampFor(long value, ReadOnlySpan<byte> record)
    {
        long v = Clamp(value);
        if (CeilingRecordOffset < 0 || CeilingRecordOffset >= record.Length) return v;
        long ceiling = record[CeilingRecordOffset];
        if (ceiling < Min) return v;          // a nonsense ceiling must not push below the minimum
        return v > ceiling ? ceiling : v;
    }
}

/// <summary>
/// The live character: an editable copy bound to the UI, plus a separate read-only mirror the poll
/// loop refreshes.
///
/// <para>Keeping <b>two</b> views of the same record is deliberate. The editable properties are
/// bound two-way to text boxes; if the background poll wrote straight into them, a refresh landing
/// between keystrokes would fight a half-typed value. So the poll updates
/// <see cref="LiveSummary"/> and the read-only mirror, and the editable side changes only when the
/// user edits it or presses Reload.</para>
///
/// <para>Every setter writes through immediately: the record's flush callback reports the exact byte
/// range that changed and only those 1–4 bytes go to the emulator. That matters because the record
/// sits next to bytes the game rewrites constantly — the clock and its eighteen per-hour timers — so
/// a whole-record write would fight the game.</para>
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    /// <summary>The editable copy, written through to the game on every change.</summary>
    private readonly byte[] _edit;

    private readonly CharacterRecord _record;

    /// <summary>The buffer the poll loop refreshes; never bound two-way.</summary>
    public byte[] LiveBuffer { get; }

    private readonly CharacterRecord _live;

    private bool _suppressWrites;

    /// <summary>Live address of <c>DGROUP:0000</c>.</summary>
    public nuint DgroupAddress { get; }

    /// <summary>Fields that may be frozen, with the offsets and widths to re-write.</summary>
    public ObservableCollection<FreezeEntry> Freezes { get; } = new();

    /// <summary>The twelve lock-pick slots, refreshed on each poll.</summary>
    public ObservableCollection<string> LockPicks { get; } = new();

    /// <summary>Builds the view-model over a located record.</summary>
    public CharacterViewModel(ICharacterHost host, LocateResult found)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!found.Found) throw new ArgumentException("the locate did not succeed", nameof(found));

        _host = host;
        DgroupAddress = found.DgroupAddress;

        _edit = found.Record.ToArray();
        LiveBuffer = found.Record.ToArray();
        _record = new CharacterRecord(_edit, 0, Flush);
        _live = new CharacterRecord(LiveBuffer);

        HealCommand = new RelayCommand(() => Bulk(r => r.HealFully(), "Healed to full."));
        MaxAbilitiesCommand = new RelayCommand(
            () => Bulk(r => r.MaxAbilities(), $"All abilities set to {CharacterFormat.MaxAbility}."));
        MaxConsumablesCommand = new RelayCommand(
            () => Bulk(r => r.MaxConsumables(),
                       $"Knock rings and healing potions set to {CharacterFormat.MaxConsumable}."));
        MaxArcheryCommand = new RelayCommand(
            () => Bulk(r => r.ArcheryLevel = CharacterFormat.MaxArcheryLevel,
                       $"Archery level set to the game's cap of {CharacterFormat.MaxArcheryLevel}."));
        LevelUpCommand = new RelayCommand(
            () => Bulk(r => r.AdvanceOwnClasses(),
                       "Advanced each of this character's own classes by one level."));
        RepairPicksCommand = new RelayCommand(RepairPicks);
        ReloadCommand = new RelayCommand(ReloadFromGame);

        // Seed every freeze from the character's own current value. Leaving them at the default 0
        // meant that simply ticking "Hit points" pinned the character to zero HP — i.e. killed it —
        // before the user had typed anything.
        foreach (var t in FreezeTargets) Freezes.Add(new FreezeEntry(t, ReadField(_edit, t)));
        RefreshLockPicks();
    }

    /// <summary>
    /// The fields worth pinning while the game is running, each with the range the game accepts.
    ///
    /// <para>Hit points start at 1, not 0: pinning a character to zero hit points would kill them on
    /// the next tick, which is the opposite of what anyone ticking that box wants.</para>
    /// </summary>
    public static readonly IReadOnlyList<FreezeTarget> FreezeTargets = new[]
    {
        new FreezeTarget("Hit points",
            CharacterFormat.DgroupRecordOffset + CharacterFormat.OffHitPoints, 1,
            1, CharacterFormat.MaxByte, CharacterFormat.OffHitPointsMax),
        new FreezeTarget("Gold",
            CharacterFormat.DgroupRecordOffset + CharacterFormat.OffGold, 4,
            0, CharacterFormat.MaxDword),
        new FreezeTarget("Knock rings",
            CharacterFormat.DgroupRecordOffset + CharacterFormat.OffKnockRings, 1,
            0, CharacterFormat.MaxConsumable),
        new FreezeTarget("Healing potions",
            CharacterFormat.DgroupRecordOffset + CharacterFormat.OffHealingPotions, 1,
            0, CharacterFormat.MaxConsumable),
        new FreezeTarget("Hour of day",
            CharacterFormat.DgroupRecordOffset + CharacterFormat.OffHour, 1,
            1, CharacterFormat.HoursPerDay),
    };

    /// <summary>Reads a freeze target's current value out of a record buffer.</summary>
    private static long ReadField(byte[] record, FreezeTarget t)
    {
        int at = t.RecordOffset;
        if (at < 0 || at + t.Width > CharacterFormat.RecordLength) return t.Min;
        return t.Width switch
        {
            1 => record[at],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(at, 2)),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(at, 4)),
            _ => t.Min,
        };
    }

    // --- writing through ------------------------------------------------------

    /// <summary>
    /// Sends the bytes a setter just changed to the game. Called by the record's flush callback, so
    /// only the 1–4 bytes that actually changed are written.
    /// </summary>
    private void Flush(int recordOffset, int length)
    {
        if (_suppressWrites) return;
        var slice = new byte[length];
        Array.Copy(_edit, recordOffset, slice, 0, length);
        int dgroupOffset = CharacterFormat.DgroupRecordOffset + recordOffset;
        if (!_host.WriteBytes(DgroupAddress, dgroupOffset, slice))
            _host.ReportStatus($"Write to DGROUP:0x{dgroupOffset:X4} failed — is the game still running?");
    }

    private void Bulk(Action<CharacterRecord> action, string message)
    {
        action(_record);
        RaiseAll();
        _host.ReportStatus(message);
    }

    private void RepairPicks()
    {
        int withGeometry = LockPickSet.CountWithGeometry(_edit);
        int changed = LockPickSet.RepairAll(_edit, Flush);
        RefreshLockPicks();

        // Three distinct outcomes, and telling a fully-equipped thief they own no picks is the one
        // to avoid: "changed == 0" happens both when there is nothing to repair and when everything
        // was already good.
        _host.ReportStatus(
            withGeometry == 0
                ? "This character has no picks to repair. Buy a set at the guild first: the trainer will "
                  + "not invent pick shapes, because the shapes decide which tumblers a pick fits."
                : changed == 0
                    ? $"All {withGeometry} of this character's picks are already in good condition."
                    : $"Set {changed} of {withGeometry} pick slot(s) to good condition.");
    }

    /// <summary>Copies the game's current bytes back over the editable copy.</summary>
    private void ReloadFromGame()
    {
        _suppressWrites = true;
        try
        {
            Array.Copy(LiveBuffer, _edit, CharacterFormat.RecordLength);
        }
        finally
        {
            _suppressWrites = false;
        }
        RaiseAll();
        RefreshLockPicks();
        _host.ReportStatus("Reloaded the editable copy from the game.");
    }

    // --- poll -----------------------------------------------------------------

    /// <summary>
    /// Called after the poll loop has refreshed <see cref="LiveBuffer"/>. Re-applies any frozen
    /// values and updates the read-only mirror.
    /// </summary>
    public void OnPolled()
    {
        foreach (var f in Freezes)
        {
            if (!f.IsFrozen) continue;
            // Re-clamp against the record as it stands now: hit points are capped by the character's
            // own maximum, which the user can change while a freeze is active.
            var want = f.BytesFor(LiveBuffer);
            if (want == null) continue;

            int recordOffset = f.Target.RecordOffset;
            if (recordOffset < 0 || recordOffset + want.Length > CharacterFormat.RecordLength) continue;

            // Only write when the game has actually moved the value. Comparing against LiveBuffer
            // rather than against a shadow copy is what makes this correct for a freeze: a shadow
            // could already hold the pinned value while the game has moved on.
            if (LiveBuffer.AsSpan(recordOffset, want.Length).SequenceEqual(want)) continue;
            if (!_host.WriteBytes(DgroupAddress, f.Target.DgroupOffset, want))
                _host.ReportStatus($"Freeze write for {f.Target.Label} failed.");
        }

        OnPropertyChanged(nameof(LiveSummary));
        OnPropertyChanged(nameof(LiveHour));
        OnPropertyChanged(nameof(LiveHitPoints));
        OnPropertyChanged(nameof(LiveGold));
        OnPropertyChanged(nameof(OpenNow));
        // No RefreshLockPicks here: it reads _edit, which the poll cannot change, so rebuilding the
        // twelve-item collection 2.5 times a second would only raise a Reset and re-template the list.
    }

    private void RefreshLockPicks()
    {
        // Read the editable copy, not the polled mirror: "Repair picks" mutates _edit and flushes it
        // to the game, so refreshing from LiveBuffer would redraw the pre-repair state and only
        // correct itself on the next tick.
        var picks = LockPickSet.Read(_edit);
        LockPicks.Clear();
        foreach (var p in picks)
        {
            bool empty = p.ShapeA == 0 && p.ShapeB == 0 && p.ShapeC == 0 && p.ShapeD == 0;
            LockPicks.Add(empty
                ? $"{p.Slot + 1,2}.  (empty)"
                : $"{p.Slot + 1,2}.  shapes {p.ShapeA,3} {p.ShapeB,3} {p.ShapeC,3} {p.ShapeD,3}   "
                  + $"state {p.State}{(p.IsPresent ? "" : "  (absent)")}"
                  + $"{(p.HasExpectedGeometry ? "" : "  [unexpected geometry]")}");
        }
        OnPropertyChanged(nameof(PicksPresent));
    }

    // --- read-only mirror -----------------------------------------------------

    /// <summary>The game's current character, as one line.</summary>
    public string LiveSummary => _live.Summary();

    /// <summary>The game's current clock.</summary>
    public string LiveHour => _live.HourText;

    /// <summary>The game's current hit points.</summary>
    public string LiveHitPoints => $"{_live.HitPoints} / {_live.HitPointsMax}";

    /// <summary>The game's current gold.</summary>
    public uint LiveGold => _live.Gold;

    /// <summary>How many lock-pick slots currently hold a usable pick.</summary>
    public int PicksPresent => LockPickSet.CountPresent(_edit);

    /// <summary>
    /// Which city locations are open at the game's current hour — the practical reason to edit the
    /// clock at all.
    /// </summary>
    public string OpenNow
    {
        get
        {
            int hour = _live.Hour;
            if (hour < 1 || hour > CharacterFormat.HoursPerDay) return "(clock not readable)";
            var names = LocationBook.OpenAt(hour).Select(l => l.Name).ToArray();
            return names.Length == 0 ? "(nothing open)" : string.Join(", ", names);
        }
    }

    // --- editable properties --------------------------------------------------

    /// <summary>Character name, up to 15 characters.</summary>
    public string Name
    {
        get => _record.Name;
        set { _record.Name = value; OnPropertyChanged(); }
    }

    /// <summary>Race index; bound to a combo box.</summary>
    public int RaceIndex
    {
        get => _record.Race;
        set { _record.Race = value; OnPropertyChanged(); }
    }

    /// <summary>Gender index; bound to a combo box.</summary>
    public int GenderIndex
    {
        get => _record.Gender;
        set { _record.Gender = value; OnPropertyChanged(); }
    }

    /// <summary>Alignment index; bound to a combo box.</summary>
    public int AlignmentIndex
    {
        get => _record.Alignment;
        set { _record.Alignment = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Selected index into <see cref="ClassBook.Classes"/>. Writing it sets the mask <i>and</i> the
    /// class index byte, so the game's two representations stay in step.
    /// </summary>
    public int ClassChoiceIndex
    {
        get
        {
            int mask = _record.ClassMask;
            for (int i = 0; i < ClassBook.Classes.Count; i++)
                if (ClassBook.Classes[i].Mask == mask) return i;
            return -1;
        }
        set
        {
            if (value < 0 || value >= ClassBook.Classes.Count) return;
            _record.ClassMask = ClassBook.Classes[value].Mask;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClericLevel));
            OnPropertyChanged(nameof(MagicUserLevel));
            OnPropertyChanged(nameof(FighterLevel));
            OnPropertyChanged(nameof(ThiefLevel));
        }
    }

    /// <summary>Age.</summary>
    public int Age
    {
        get => _record.Age;
        set { _record.Age = value; OnPropertyChanged(); }
    }

    /// <summary>Strength.</summary>
    public int Strength
    {
        get => _record.Strength;
        set { _record.Strength = value; OnPropertyChanged(); }
    }

    /// <summary>Exceptional-strength percentile; 0 means none.</summary>
    public int StrengthPercentile
    {
        get => _record.StrengthPercentile;
        set { _record.StrengthPercentile = value; OnPropertyChanged(); }
    }

    /// <summary>Intelligence.</summary>
    public int Intelligence
    {
        get => _record.Intelligence;
        set { _record.Intelligence = value; OnPropertyChanged(); }
    }

    /// <summary>Wisdom.</summary>
    public int Wisdom
    {
        get => _record.Wisdom;
        set { _record.Wisdom = value; OnPropertyChanged(); }
    }

    /// <summary>Dexterity.</summary>
    public int Dexterity
    {
        get => _record.Dexterity;
        set { _record.Dexterity = value; OnPropertyChanged(); OnPropertyChanged(nameof(HealingNote)); }
    }

    /// <summary>Constitution.</summary>
    public int Constitution
    {
        get => _record.Constitution;
        set { _record.Constitution = value; OnPropertyChanged(); OnPropertyChanged(nameof(HealingNote)); }
    }

    /// <summary>Charisma.</summary>
    public int Charisma
    {
        get => _record.Charisma;
        set { _record.Charisma = value; OnPropertyChanged(); }
    }

    /// <summary>Current hit points.</summary>
    public int HitPoints
    {
        get => _record.HitPoints;
        set { _record.HitPoints = value; OnPropertyChanged(); }
    }

    /// <summary>Maximum hit points.</summary>
    public int HitPointsMax
    {
        get => _record.HitPointsMax;
        set { _record.HitPointsMax = value; OnPropertyChanged(); OnPropertyChanged(nameof(HitPoints)); }
    }

    /// <summary>Gold carried.</summary>
    public uint Gold
    {
        get => _record.Gold;
        set { _record.Gold = value; OnPropertyChanged(); }
    }

    /// <summary>Experience points.</summary>
    public uint Experience
    {
        get => _record.Experience;
        set { _record.Experience = value; OnPropertyChanged(); }
    }

    /// <summary>Cleric level.</summary>
    public int ClericLevel
    {
        get => _record.ClericLevel;
        set { _record.ClericLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Magic-User level.</summary>
    public int MagicUserLevel
    {
        get => _record.MagicUserLevel;
        set { _record.MagicUserLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Fighter level.</summary>
    public int FighterLevel
    {
        get => _record.FighterLevel;
        set { _record.FighterLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Thief level.</summary>
    public int ThiefLevel
    {
        get => _record.ThiefLevel;
        set { _record.ThiefLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Knock rings carried.</summary>
    public int KnockRings
    {
        get => _record.KnockRings;
        set { _record.KnockRings = value; OnPropertyChanged(); }
    }

    /// <summary>Healing potions carried.</summary>
    public int HealingPotions
    {
        get => _record.HealingPotions;
        set { _record.HealingPotions = value; OnPropertyChanged(); }
    }

    /// <summary>Archery-range level, 0..15.</summary>
    public int ArcheryLevel
    {
        get => _record.ArcheryLevel;
        set { _record.ArcheryLevel = value; OnPropertyChanged(); }
    }

    /// <summary>Hour of day, 1..24.</summary>
    public int Hour
    {
        get => _record.Hour;
        set
        {
            _record.Hour = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HourText));
            OnPropertyChanged(nameof(EditOpenNow));
        }
    }

    /// <summary>The edited hour as the game would print it.</summary>
    public string HourText => _record.HourText;

    /// <summary>Day counter.</summary>
    public int Day
    {
        get => _record.Day;
        set { _record.Day = value; OnPropertyChanged(); }
    }

    /// <summary>What would be open at the hour currently in the editor.</summary>
    public string EditOpenNow
    {
        get
        {
            var names = LocationBook.OpenAt(_record.Hour).Select(l => l.Name).ToArray();
            return names.Length == 0 ? "(nothing open)" : string.Join(", ", names);
        }
    }

    /// <summary>Explains what the current Constitution buys in healing terms.</summary>
    public string HealingNote =>
        $"Constitution {_record.Constitution} heals "
        + $"{GameFacts.NaturalHealingPerDay(_record.Constitution)} hit point(s) per game day "
        + $"(a day is about {GameFacts.RealMinutesPerGameDay} real minutes).";

    // --- commands -------------------------------------------------------------

    /// <summary>Restores hit points to maximum.</summary>
    public ICommand HealCommand { get; }

    /// <summary>Sets every ability to 19.</summary>
    public ICommand MaxAbilitiesCommand { get; }

    /// <summary>Fills both consumables to 99.</summary>
    public ICommand MaxConsumablesCommand { get; }

    /// <summary>Sets the archery level to the game's cap.</summary>
    public ICommand MaxArcheryCommand { get; }

    /// <summary>Raises the levels of the classes the character has.</summary>
    public ICommand LevelUpCommand { get; }

    /// <summary>Sets every pick slot that has geometry to good condition.</summary>
    public ICommand RepairPicksCommand { get; }

    /// <summary>Copies the game's current values back into the editor.</summary>
    public ICommand ReloadCommand { get; }

    /// <summary>A snapshot of the editable record, for writing out to a <c>.HIL</c> file.</summary>
    public byte[] SnapshotEdited() => _edit.ToArray();

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Name), nameof(RaceIndex), nameof(GenderIndex), nameof(AlignmentIndex),
                     nameof(ClassChoiceIndex), nameof(Age), nameof(Strength), nameof(StrengthPercentile),
                     nameof(Intelligence), nameof(Wisdom), nameof(Dexterity), nameof(Constitution),
                     nameof(Charisma), nameof(HitPoints), nameof(HitPointsMax), nameof(Gold),
                     nameof(Experience), nameof(ClericLevel), nameof(MagicUserLevel),
                     nameof(FighterLevel), nameof(ThiefLevel), nameof(KnockRings),
                     nameof(HealingPotions), nameof(ArcheryLevel), nameof(Hour), nameof(HourText),
                     nameof(Day), nameof(EditOpenNow), nameof(HealingNote),
                 })
            OnPropertyChanged(name);
    }
}

/// <summary>A freezable field, with the value to pin it to.</summary>
public sealed class FreezeEntry : ObservableObject
{
    /// <summary>Which field this pins.</summary>
    public FreezeTarget Target { get; }

    /// <summary>Label for the checkbox, with the range the field accepts.</summary>
    public string Label => $"{Target.Label}  ({Target.Min}–{Target.Max})";

    private bool _isFrozen;

    /// <summary>True while the value is being re-written on every poll tick.</summary>
    public bool IsFrozen
    {
        get => _isFrozen;
        set => SetField(ref _isFrozen, value);
    }

    private long _value;

    /// <summary>
    /// The value to pin to, clamped to the field's range on the way in. Clamping here rather than
    /// refusing means the checkbox always does something sane: this is the one write path that does
    /// not go through <see cref="CharacterRecord"/>'s setters, so it has to enforce the game's limits
    /// itself.
    /// </summary>
    public long Value
    {
        get => _value;
        // Notify unconditionally rather than through SetField: when the clamp maps a rejected entry
        // onto the value already stored, SetField returns early and the text box keeps showing the
        // number the user typed while a different one is actually pinned.
        set { _value = Target.Clamp(value); OnPropertyChanged(); }
    }

    /// <summary>Builds an entry, seeded with the character's current value for that field.</summary>
    public FreezeEntry(FreezeTarget target, long initialValue)
    {
        Target = target;
        _value = target.Clamp(initialValue);
    }

    /// <summary>
    /// The little-endian bytes to write, or null when the target's width is not one this understands.
    /// <see cref="Value"/> is already inside the field's static range, so no truncation is possible.
    /// </summary>
    public byte[]? Bytes => Encode(Value);

    /// <summary>
    /// The bytes to write, clamped against <paramref name="record"/> as well as the static range —
    /// so a hit-point freeze can never exceed the character's current maximum.
    /// </summary>
    public byte[]? BytesFor(ReadOnlySpan<byte> record) => Encode(Target.ClampFor(Value, record));

    private byte[]? Encode(long v) => Target.Width switch
    {
        1 => new[] { (byte)v },
        2 => BitConverter.GetBytes((ushort)v),
        4 => BitConverter.GetBytes((uint)v),
        _ => null,
    };
}
