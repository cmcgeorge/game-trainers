using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AlternateRealityTrainer.Game;
using AlternateRealityTrainer.Memory;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>
/// The located character: editable fields on one side, a live read-only mirror on the other.
///
/// Editable properties write straight into the game and are only re-read on demand (locate, a bulk
/// action, or <b>Reload</b>). The <c>Live*</c> properties come from the poll loop instead, so the
/// panel showing what the game currently thinks never fights a half-typed value in a text box.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;
    private readonly byte[] _buffer;
    private readonly byte[] _liveBuffer = new byte[CharacterFormat.LiveFieldsLength];
    private readonly CharacterRecord _record;
    private readonly CharacterRecord _live;

    /// <summary>Live address of the record in the attached process.</summary>
    public nuint Address { get; }

    /// <summary>Live address of DGROUP:0000, or 0 if the structural fallback found the record.</summary>
    public nuint DgroupAddress { get; }

    /// <summary>How the record was found, for the status bar.</summary>
    public string LocateMethod { get; }

    public ObservableCollection<AttributeViewModel> Attributes { get; } = new();

    public CharacterViewModel(ICharacterHost host, LocateResult located)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!located.Found) throw new ArgumentException("Locate result holds no record.", nameof(located));

        _host = host;
        Address = located.RecordAddress;
        DgroupAddress = located.DgroupAddress;
        LocateMethod = located.Method;

        // Keep a full-size buffer even if the read came back short, so a setter that touches a late
        // field can never index outside the array.
        _buffer = new byte[CharacterFormat.RecordSize];
        Array.Copy(located.Buffer, _buffer, Math.Min(located.Buffer.Length, _buffer.Length));

        _record = new CharacterRecord(_buffer, 0, Flush);

        // The poll loop only reads the first LiveFieldsLength bytes, but CharacterRecord insists on
        // a full-size window so no accessor can run off the end — so the mirror gets its own
        // full-size buffer that OnPolled refreshes from the short read.
        _live = new CharacterRecord(new byte[CharacterFormat.RecordSize]);

        foreach (var info in AttributeBook.All)
            Attributes.Add(new AttributeViewModel(info, _record, OnAttributeEdited));

        FullHealCommand = new RelayCommand(() => Bulk(_record.FullHeal, Pins.None, "Healed to full."));
        MaxAttributesCommand = new RelayCommand(() => Bulk(_record.MaxAttributes, Pins.Attributes, $"All attributes set to {CharacterFormat.MaxAttribute}."));
        MaxHealthCommand = new RelayCommand(() => Bulk(_record.MaxHealth, Pins.None, $"Hit points set to {CharacterFormat.MaxHitPoints:N0}."));
        MaxMoneyCommand = new RelayCommand(() => Bulk(_record.MaxMoney, Pins.Money, $"Every coin and valuables field set to {CharacterFormat.MaxCoins:N0}."));
        MaxSuppliesCommand = new RelayCommand(() => Bulk(_record.MaxSupplies, Pins.Supplies, "Supplies filled; compass and watch granted."));
        LevelUpCommand = new RelayCommand(() =>
        {
            ReloadFromGame();
            bool advanced = _record.LevelUp();   // experience only -- no pinned field is touched
            RefreshAll();
            _host.ReportStatus(advanced
                ? "Experience raised to the next-level threshold — the game will level you on its next check."
                : "Experience is already at the ceiling; there is no further level to reach.");
        });
        MaxEverythingCommand = new RelayCommand(() => Bulk(_record.MaxEverything, Pins.All, "Attributes, hit points, money and supplies maxed."));
        ReloadCommand = new RelayCommand(() => _host.ReportStatus(
            ReloadFromGame()
                ? "Reloaded the editable fields from the game."
                : "Nothing polled yet — give it a moment and try again."));
    }

    // --- commands ------------------------------------------------------------

    public ICommand FullHealCommand { get; }
    public ICommand MaxAttributesCommand { get; }
    public ICommand MaxHealthCommand { get; }
    public ICommand MaxMoneyCommand { get; }
    public ICommand MaxSuppliesCommand { get; }
    public ICommand LevelUpCommand { get; }
    public ICommand MaxEverythingCommand { get; }
    public ICommand ReloadCommand { get; }

    // --- editable fields -----------------------------------------------------
    //
    // Every setter clamps, writes only when the stored value actually moves, and then notifies if
    // *either* the value moved or the caller's input had to be clamped — the second case is what
    // makes a text box that was handed an out-of-range number snap back to what was really written
    // instead of sitting there showing a value the game never received.

    /// <summary>
    /// The character's name. A name the locator would no longer recognise (empty, or not starting
    /// with a letter) is refused rather than written — otherwise the trainer would lose the very
    /// character it had just renamed.
    /// </summary>
    public string Name
    {
        get => _record.Name;
        set
        {
            string requested = value ?? string.Empty;
            if (requested == _record.Name) return;
            if (!CharacterFormat.IsWritableName(requested))
            {
                _host.ReportStatus("A name has to start with a letter — the trainer finds the character by it.");
                OnPropertyChanged();   // snap the box back to the name still in the game
                return;
            }
            _record.Name = requested;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public int Level
    {
        get => _record.Level;
        set => Commit(value, Math.Clamp(value, 0, CharacterFormat.LevelCeiling),
                      _record.Level, v => _record.Level = (int)v, derived: true);
    }

    public long Experience
    {
        get => _record.Experience;
        set => Commit(value, Math.Clamp(value, 0, CharacterFormat.ExperienceCeiling),
                      _record.Experience, v => _record.Experience = (uint)v, derived: true);
    }

    public long NextLevelExperience
    {
        get => _record.NextLevelExperience;
        set => Commit(value, Math.Clamp(value, 0, CharacterFormat.ExperienceCeiling),
                      _record.NextLevelExperience, v => _record.NextLevelExperience = (uint)v);
    }

    // Hit points and their maximum write each other in the record (raising one past the other drags
    // the other along), so each has to announce the sibling or the UI would show a number the game
    // does not hold.
    public long HitPoints
    {
        get => _record.HitPoints;
        set => Commit(value, Math.Clamp(value, 0, CharacterFormat.HitPointCeiling),
                      _record.HitPoints,
                      v => { _record.HitPoints = (uint)v; OnPropertyChanged(nameof(HitPointsMax)); },
                      derived: true);
    }

    public long HitPointsMax
    {
        get => _record.HitPointsMax;
        set => Commit(value, Math.Clamp(value, 1, CharacterFormat.HitPointCeiling),
                      _record.HitPointsMax,
                      v => { _record.HitPointsMax = (uint)v; OnPropertyChanged(nameof(HitPoints)); },
                      derived: true);
    }

    // The `repin` argument names the single pin this field owns, so an edit here cannot disturb any
    // other frozen field's pin. Gems, Jewelry, Crystals and Keys are not frozen by anything.
    public int Gold
    {
        get => _record.Gold;
        set => Coin(value, _record.Gold, v => _record.Gold = v, () => _pinnedGold = _record.Gold);
    }

    public int Silver
    {
        get => _record.Silver;
        set => Coin(value, _record.Silver, v => _record.Silver = v, () => _pinnedSilver = _record.Silver);
    }

    public int Copper
    {
        get => _record.Copper;
        set => Coin(value, _record.Copper, v => _record.Copper = v, () => _pinnedCopper = _record.Copper);
    }

    public int Gems { get => _record.Gems; set => Coin(value, _record.Gems, v => _record.Gems = v, null); }
    public int Jewelry { get => _record.Jewelry; set => Coin(value, _record.Jewelry, v => _record.Jewelry = v, null); }

    public int Food
    {
        get => _record.Food;
        set => Supply(value, _record.Food, v => _record.Food = v, () => _pinnedFood = _record.Food);
    }

    public int Water
    {
        get => _record.Water;
        set => Supply(value, _record.Water, v => _record.Water = v, () => _pinnedWater = _record.Water);
    }

    public int Crystals { get => _record.Crystals; set => Supply(value, _record.Crystals, v => _record.Crystals = v, null); }
    public int Keys { get => _record.Keys; set => Supply(value, _record.Keys, v => _record.Keys = v, null); }

    public bool HasCompass
    {
        get => _record.HasCompass;
        set { if (value != _record.HasCompass) { _record.HasCompass = value; OnPropertyChanged(); } }
    }

    public bool HasWatch
    {
        get => _record.HasWatch;
        set { if (value != _record.HasWatch) { _record.HasWatch = value; OnPropertyChanged(); } }
    }

    private void Commit(
        long requested, long clamped, long current, Action<long> write,
        bool derived = false, [CallerMemberName] string? name = null)
    {
        bool moved = clamped != current;
        if (moved) write(clamped);
        if (moved || clamped != requested) OnPropertyChanged(name);
        if (moved && derived) RaiseDerived();
    }

    private void Coin(int requested, int current, Action<ushort> write, Action? repin,
                      [CallerMemberName] string? name = null) =>
        Commit(requested, Math.Clamp(requested, 0, CharacterFormat.CoinCeiling), current,
               v => { write((ushort)v); if (_freezeMoney) repin?.Invoke(); }, name: name);

    private void Supply(int requested, int current, Action<byte> write, Action? repin,
                        [CallerMemberName] string? name = null) =>
        Commit(requested, Math.Clamp(requested, 0, CharacterFormat.SupplyCeiling), current,
               v => { write((byte)v); if (_freezeSupplies) repin?.Invoke(); }, name: name);

    // --- freeze toggles ------------------------------------------------------

    private bool _freezeHitPoints;
    /// <summary>Re-pins current hit points to their maximum on every poll tick.</summary>
    public bool FreezeHitPoints
    {
        get => _freezeHitPoints;
        set => SetField(ref _freezeHitPoints, value);
    }

    // A pin is taken from the LIVE mirror, not the editor — the editor's copy is only refreshed at
    // locate time, so pinning from it would rewind the game to whatever the character looked like
    // when you attached, wiping out everything earned since. Before the first poll the mirror is
    // still zeros, so the pin falls back to the editor's copy (which is the located snapshot).
    //
    // Editing a frozen field re-pins it, so a deliberate edit sticks instead of being reverted on
    // the next tick.

    private CharacterRecord Source => _hasPolled ? _live : _record;

    private bool _freezeAttributes;
    private readonly byte[] _pinnedAttributes = new byte[CharacterFormat.AttributeCount];
    /// <summary>
    /// Holds all seven attributes at the values they had when the toggle went on — the counter to a
    /// Ghost's permanent Strength drain.
    /// </summary>
    public bool FreezeAttributes
    {
        get => _freezeAttributes;
        set
        {
            if (!SetField(ref _freezeAttributes, value) || !value) return;
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
                _pinnedAttributes[i] = Source.GetAttribute(i);
        }
    }

    private bool _freezeSupplies;
    private byte _pinnedFood, _pinnedWater;
    /// <summary>Holds food packets and water flasks at the values they had when the toggle went on.</summary>
    public bool FreezeSupplies
    {
        get => _freezeSupplies;
        set
        {
            if (!SetField(ref _freezeSupplies, value) || !value) return;
            _pinnedFood = Source.Food;
            _pinnedWater = Source.Water;
        }
    }

    private bool _freezeMoney;
    private ushort _pinnedGold, _pinnedSilver, _pinnedCopper;
    /// <summary>Holds gold, silver and copper at the values they had when the toggle went on.</summary>
    public bool FreezeMoney
    {
        get => _freezeMoney;
        set
        {
            if (!SetField(ref _freezeMoney, value) || !value) return;
            _pinnedGold = Source.Gold;
            _pinnedSilver = Source.Silver;
            _pinnedCopper = Source.Copper;
        }
    }

    /// <summary>Called after the user edits an attribute: re-pin it, then refresh the title.</summary>
    private void OnAttributeEdited(int index)
    {
        if (_freezeAttributes) _pinnedAttributes[index] = _record.GetAttribute(index);
        RaiseDerived();
    }

    /// <summary>
    /// Which pins a write touched. Re-pinning is deliberately <b>scoped</b>: the editor's buffer is
    /// only as fresh as the last locate or reload, so re-pinning a field the write never touched
    /// would replace a good live pin with a stale editor value — and the next tick would then write
    /// that stale value into the game. Editing Water must not disturb the gold pin.
    /// </summary>
    [Flags]
    private enum Pins
    {
        None = 0,
        Attributes = 1,
        Money = 2,
        Supplies = 4,
        All = Attributes | Money | Supplies,
    }

    /// <summary>Re-pins exactly the frozen fields that <paramref name="touched"/> names.</summary>
    private void Repin(Pins touched)
    {
        if (_freezeAttributes && touched.HasFlag(Pins.Attributes))
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
                _pinnedAttributes[i] = _record.GetAttribute(i);

        if (_freezeMoney && touched.HasFlag(Pins.Money))
        {
            _pinnedGold = _record.Gold;
            _pinnedSilver = _record.Silver;
            _pinnedCopper = _record.Copper;
        }

        if (_freezeSupplies && touched.HasFlag(Pins.Supplies))
        {
            _pinnedFood = _record.Food;
            _pinnedWater = _record.Water;
        }
    }

    // --- live mirror ---------------------------------------------------------

    /// <summary>Buffer the poll loop reads the live fields into.</summary>
    public byte[] LiveBuffer => _liveBuffer;

    private string _liveSummary = "—";
    public string LiveSummary { get => _liveSummary; private set => SetField(ref _liveSummary, value); }

    private string _liveMoney = "—";
    public string LiveMoney { get => _liveMoney; private set => SetField(ref _liveMoney, value); }

    private string _liveSupplies = "—";
    public string LiveSupplies { get => _liveSupplies; private set => SetField(ref _liveSupplies, value); }

    private string _liveClock = "—";
    public string LiveClock { get => _liveClock; private set => SetField(ref _liveClock, value); }

    private string _liveAttributes = "—";
    public string LiveAttributes { get => _liveAttributes; private set => SetField(ref _liveAttributes, value); }

    private bool _hasPolled;

    /// <summary>
    /// Called by the poll loop after it has refreshed <see cref="LiveBuffer"/>. Updates the
    /// read-only mirror and then applies whatever freezes are on, using this tick's values.
    /// </summary>
    public void OnPolled()
    {
        // The live view is built over a padded copy, so refresh that copy from the raw read.
        Array.Copy(_liveBuffer, _live.Buffer, CharacterFormat.LiveFieldsLength);
        _hasPolled = true;

        LiveSummary = $"{_live.Name} — level {_live.Level}, {_live.HitPoints}/{_live.HitPointsMax} hp, " +
                      $"{_live.Experience:N0} exp (next at {_live.NextLevelExperience:N0})";
        LiveMoney = $"Gold {_live.Gold:N0}   Silver {_live.Silver:N0}   Copper {_live.Copper:N0}   " +
                    $"Gems {_live.Gems:N0}   Jewelry {_live.Jewelry:N0}";
        LiveSupplies = $"Food {_live.Food}   Water {_live.Water}   Crystals {_live.Crystals}   Keys {_live.Keys}   " +
                       $"Compass {(_live.HasCompass ? "yes" : "no")}   Watch {(_live.HasWatch ? "yes" : "no")}";
        LiveClock = _live.DateTimeText;
        LiveAttributes = string.Join("   ", AttributeBook.DisplayOrder
            .Select(i => $"{AttributeBook.At(i).Abbreviation} {_live.GetAttribute(i)}")
            .Concat(new[] { $"SPD {_live.GetAttribute(6)}" }));

        ApplyFreezes();
    }

    private void ApplyFreezes()
    {
        if (_freezeHitPoints)
        {
            uint max = _live.HitPointsMax;
            if (max > 0 && _live.HitPoints != max) WriteU32(CharacterFormat.OffHitPoints, max);
        }

        if (_freezeAttributes)
        {
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            {
                byte want = _pinnedAttributes[i];
                if (want == 0 || _live.GetAttribute(i) == want) continue;
                var copies = new byte[CharacterFormat.AttributeCopies];
                Array.Fill(copies, want);
                _host.WriteBytes(Address, CharacterFormat.AttributeOffset(i), copies);
            }
        }

        if (_freezeSupplies)
        {
            if (_live.Food != _pinnedFood) WriteU8(CharacterFormat.OffFood, _pinnedFood);
            if (_live.Water != _pinnedWater) WriteU8(CharacterFormat.OffWater, _pinnedWater);
        }

        if (_freezeMoney)
        {
            if (_live.Gold != _pinnedGold) WriteU16(CharacterFormat.OffGold, _pinnedGold);
            if (_live.Silver != _pinnedSilver) WriteU16(CharacterFormat.OffSilver, _pinnedSilver);
            if (_live.Copper != _pinnedCopper) WriteU16(CharacterFormat.OffCopper, _pinnedCopper);
        }
    }

    private void WriteU8(int offset, byte value) => Write(offset, new[] { value });

    private void WriteU16(int offset, ushort value) =>
        Write(offset, new[] { (byte)value, (byte)(value >> 8) });

    private void WriteU32(int offset, uint value) =>
        Write(offset, new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) });

    // --- plumbing ------------------------------------------------------------

    /// <summary>How many consecutive freeze writes may fail before the user is told.</summary>
    private const int FreezeFailuresBeforeReporting = 3;

    private int _consecutiveFreezeFailures;

    // Pushes bytes into the game and keeps count of failures. A freeze that starts failing —
    // because the game exited, the handle went bad, or the page turned read-only — would otherwise
    // fail silently every 600 ms with nothing on screen to show for it.
    private void Write(int offset, byte[] bytes)
    {
        if (_host.WriteBytes(Address, offset, bytes))
        {
            _consecutiveFreezeFailures = 0;
            return;
        }

        if (++_consecutiveFreezeFailures == FreezeFailuresBeforeReporting)
            _host.ReportStatus("Freeze writes are failing — has the game exited, or dropped its character?");
    }

    // Every CharacterRecord setter routes here with the exact range it touched. The record is always
    // constructed at baseOffset 0 (the constructor enforces it), so a record-relative offset is also
    // a buffer index.
    private void Flush(int offset, int length)
    {
        var slice = new byte[length];
        Array.Copy(_buffer, offset, slice, 0, length);
        if (!_host.WriteBytes(Address, offset, slice))
            _host.ReportStatus("Write failed — is the game still running and the trainer elevated?");
    }

    // Bulk actions work off the latest polled values, not the attach-time snapshot -- otherwise
    // Full Heal would restore the hit-point maximum the character had when you attached. `touched`
    // names only the pins this action actually wrote.
    private void Bulk(Action action, Pins touched, string message)
    {
        ReloadFromGame();
        action();
        Repin(touched);
        RefreshAll();
        _host.ReportStatus(message);
    }

    /// <summary>
    /// Copies the latest polled values over the editable fields and refreshes the bindings. Does
    /// nothing before the first poll tick has run — the live buffer is still all zeros then, and
    /// copying it would blank the editor.
    /// </summary>
    /// <returns>True if the editor was refreshed.</returns>
    public bool ReloadFromGame()
    {
        if (!_hasPolled) return false;
        Array.Copy(_liveBuffer, _buffer, CharacterFormat.LiveFieldsLength);
        Repin(Pins.All);   // the editor now holds exactly what the game holds, so every pin may follow
        RefreshAll();
        return true;
    }

    private void RefreshAll()
    {
        foreach (var a in Attributes) a.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(NextLevelExperience));
        OnPropertyChanged(nameof(HitPoints));
        OnPropertyChanged(nameof(HitPointsMax));
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Silver));
        OnPropertyChanged(nameof(Copper));
        OnPropertyChanged(nameof(Gems));
        OnPropertyChanged(nameof(Jewelry));
        OnPropertyChanged(nameof(Food));
        OnPropertyChanged(nameof(Water));
        OnPropertyChanged(nameof(Crystals));
        OnPropertyChanged(nameof(Keys));
        OnPropertyChanged(nameof(HasCompass));
        OnPropertyChanged(nameof(HasWatch));
        RaiseDerived();
    }

    private void RaiseDerived() => OnPropertyChanged(nameof(Title));

    public string Title => $"{_record.Name}  —  level {_record.Level}  @  0x{(ulong)Address:X}";
}
