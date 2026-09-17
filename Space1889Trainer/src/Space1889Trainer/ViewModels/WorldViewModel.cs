using System.Collections.ObjectModel;
using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>A story-flag checkbox.</summary>
public sealed class StoryFlagViewModel : ObservableObject
{
    private readonly WorldViewModel _owner;
    private bool _isSet;

    public StoryFlagViewModel(WorldViewModel owner, StoryFlag flag)
    {
        _owner = owner;
        Flag = flag;
    }

    public StoryFlag Flag { get; }

    public string Label => $"Bit {Flag.Bit}: {Flag.Name}";

    public bool IsSet
    {
        get => _isSet;
        set
        {
            if (_isSet == value) return;
            _isSet = value;
            OnPropertyChanged();
            _owner.WriteStoryFlag(Flag.Bit, value);
        }
    }

    internal void Load(bool value)
    {
        if (_isSet == value) return;
        _isSet = value;
        OnPropertyChanged(nameof(IsSet));
    }
}

/// <summary>An item choice for a flyer gun mount; index −1 stands for "none" (stored as 0).</summary>
public sealed record GunChoice(int Index, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Editor for the party-level and world fields of a state block.</summary>
public sealed class WorldViewModel : ObservableObject
{
    private readonly IStateHost _host;
    private bool _loading;

    private long _account, _day;
    private int _food, _row, _column;
    private int _hull, _lift, _propeller, _power, _engine, _armor, _topGun, _lowGun;
    private bool _ammonia, _glow;

    public WorldViewModel(IStateHost host)
    {
        _host = host;
        foreach (var flag in StoryFlagBook.All) StoryFlags.Add(new StoryFlagViewModel(this, flag));
        MaxFoodCommand = new RelayCommand(() => Act("Food topped up to 30,000", w => w.SetFood(30000)), () => _host.CanEdit);
        AddAccountCommand = new RelayCommand(() => Act("£10,000 deposited in the party account",
            w => w.SetPartyAccount(w.PartyAccount + 10_000L * GameFacts.PenniesPerPound)), () => _host.CanEdit);
        RepairFlyerCommand = new RelayCommand(() => Act("Flyer repaired", w => w.RepairFlyer()), () => _host.CanEdit && HasFlyer);
        TeleportCommand = new RelayCommand(Teleport, () => _host.CanEdit);
    }

    private WorldRecord? Record => _host.State is { IsLoaded: true } s ? new WorldRecord(s) : null;

    /// <summary>The editor panel's IsEnabled: a game or save is open and writable.</summary>
    public bool CanEdit => _host.CanEdit && Record is not null;

    public RelayCommand MaxFoodCommand { get; }
    public RelayCommand AddAccountCommand { get; }
    public RelayCommand RepairFlyerCommand { get; }
    public RelayCommand TeleportCommand { get; }

    public ObservableCollection<StoryFlagViewModel> StoryFlags { get; } = new();

    public static IReadOnlyList<string> LiftChoices => FlyerBook.LiftTypes;
    public static IReadOnlyList<string> PropellerChoices => FlyerBook.Propellers;

    /// <summary>"None" plus every ship gun (item types 10 and 11), by 0-based ITEMS.DAT index.</summary>
    public static IReadOnlyList<GunChoice> GunChoices { get; } =
        new[] { new GunChoice(0, "(none)") }.Concat(ItemBook.ShipGuns.Select(g => new GunChoice(g.Index, g.Name))).ToList();

    // ---- money, food, time -----------------------------------------------------------------------

    public long PartyAccount
    {
        get => _account;
        set
        {
            long v = Math.Clamp(value, 0, uint.MaxValue);
            if (!SetField(ref _account, v) || _loading) return;
            Write(w => w.SetPartyAccount(v));
            OnPropertyChanged(nameof(PartyAccountText));
        }
    }

    public string PartyAccountText => GameFacts.FormatMoney(_account);

    public int Food
    {
        get => _food;
        set
        {
            int v = Math.Clamp(value, 0, StateFormat.MaxFood);
            if (SetField(ref _food, v) && !_loading) Write(w => w.SetFood(v));
        }
    }

    public long Day
    {
        get => _day;
        set
        {
            long v = Math.Clamp(value, 0, uint.MaxValue);
            if (SetField(ref _day, v) && !_loading) Write(w => w.SetDay(v));
        }
    }

    // ---- location ----------------------------------------------------------------------------------

    public string Location { get; private set; } = "";

    public string MapKey { get; private set; } = "";

    public string Leader { get; private set; } = "";

    public string LightText { get; private set; } = "";

    /// <summary>Row to teleport to (squares). Shows the party's row after a refresh.</summary>
    public int Row { get => _row; set => SetField(ref _row, Math.Max(0, value)); }

    /// <summary>Column to teleport to (squares).</summary>
    public int Column { get => _column; set => SetField(ref _column, Math.Max(0, value)); }

    public int CurrentRow { get; private set; }

    public int CurrentColumn { get; private set; }

    /// <summary>
    /// Optional safety check for a teleport target on the party's current map: returns why the square is unsafe, or
    /// null when it is fine. The Maps tab supplies it (it has the decoded maps); without one the move is unchecked.
    /// </summary>
    public Func<GameState, int, int, string?>? TeleportCheck { get; set; }

    private void Teleport()
    {
        if (!_host.CanEdit || _host.State is not { IsLoaded: true } state || Record is not { } w || !Resync()) return;
        if (TeleportCheck?.Invoke(state, Row, Column) is { } unsafeReason)
        {
            _host.Report("Teleport refused: " + unsafeReason);
            return;
        }
        if (w.Teleport(Row, Column))
        {
            _host.AfterWrite();
            _host.Report($"Party moved to row {Row}, column {Column}. Take one step in the game to redraw the view.");
        }
        else _host.Report("The teleport was refused.");
        Reload();
    }

    // ---- ether flyer ------------------------------------------------------------------------------

    public bool HasFlyer { get; private set; }

    public string FlyerSummary { get; private set; } = "";

    public string DamageText { get; private set; } = "";

    public string BridgeCrew { get; private set; } = "";

    public int Hull { get => _hull; set => FlyerField(ref _hull, value, StateFormat.FlyerHull, 0, FlyerBook.MaxHull); }
    public int Lift { get => _lift; set => FlyerField(ref _lift, value, StateFormat.FlyerLift, 0, 1); }
    public int Propeller { get => _propeller; set => FlyerField(ref _propeller, value, StateFormat.FlyerPropeller, 0, 3); }
    public int Power { get => _power; set => FlyerField(ref _power, value, StateFormat.FlyerPower, 0, 255); }
    public int Engine { get => _engine; set => FlyerField(ref _engine, value, StateFormat.FlyerEngine, 0, 255); }
    public int Armor { get => _armor; set => FlyerField(ref _armor, value, StateFormat.FlyerArmor, 0, 255); }
    public int TopGun { get => _topGun; set => FlyerField(ref _topGun, value, StateFormat.FlyerTopGun, 0, 255); }
    public int LowGun { get => _lowGun; set => FlyerField(ref _lowGun, value, StateFormat.FlyerLowGun, 0, 255); }

    public bool AmmoniaLoaded
    {
        get => _ammonia;
        set { if (SetField(ref _ammonia, value) && !_loading) Write(w => w.SetAmmoniaLoaded(value)); }
    }

    public bool GlowCrystalsLoaded
    {
        get => _glow;
        set { if (SetField(ref _glow, value) && !_loading) Write(w => w.SetGlowCrystalsLoaded(value)); }
    }

    private void FlyerField(ref int field, int value, int offset, int min, int max)
    {
        int v = Math.Clamp(value, min, max);
        if (!SetField(ref field, v, FlyerPropertyName(offset)) || _loading) return;
        if (Write(w => w.SetFlyer(offset, v))) UpdateFlyerSummary();
    }

    private void UpdateFlyerSummary()
    {
        FlyerSummary = HasFlyer
            ? $"Flyer built: hull {Hull}, {FlyerBook.LiftTypes[Math.Min(Lift, 1)]} lift, {FlyerBook.Propellers[Math.Min(Propeller, 3)]} propeller."
            : "No ether flyer yet (build one at an ether port).";
        OnPropertyChanged(nameof(FlyerSummary));
    }

    private static string FlyerPropertyName(int offset) => offset switch
    {
        StateFormat.FlyerHull => nameof(Hull),
        StateFormat.FlyerLift => nameof(Lift),
        StateFormat.FlyerPropeller => nameof(Propeller),
        StateFormat.FlyerPower => nameof(Power),
        StateFormat.FlyerEngine => nameof(Engine),
        StateFormat.FlyerArmor => nameof(Armor),
        StateFormat.FlyerTopGun => nameof(TopGun),
        _ => nameof(LowGun),
    };

    internal void WriteStoryFlag(int bit, bool value)
    {
        if (_loading) return;
        Write(w => w.SetStoryFlag(bit, value));   // a refused write reloads the checkboxes from the block
    }

    // ---- plumbing ----------------------------------------------------------------------------------

    public void Reload()
    {
        _loading = true;
        try
        {
            OnPropertyChanged(nameof(CanEdit));
            if (Record is not { } w)
            {
                RaiseCommands();   // detached or closed: the buttons must grey out
                return;
            }
            PartyAccount = w.PartyAccount;
            Food = w.Food;
            Day = w.Day;

            Location = w.Describe;
            MapKey = $"planet {w.Planet}, area {w.Area}, map {w.Map} (map id {w.MapId})";
            LightText = w.IsLit ? "lit" : "dark — carry and USE a miner's hat, lantern or electric lamp";
            int leader = w.MarchingOrder(0);
            Leader = leader is >= 0 and < StateFormat.CharacterSlots && _host.State is { } s
                ? new CharacterRecord(s, leader).Name : "—";
            bool moved = CurrentRow != w.Row || CurrentColumn != w.Column;
            CurrentRow = w.Row;
            CurrentColumn = w.Column;
            if (moved) { Row = w.Row; Column = w.Column; }

            HasFlyer = w.HasFlyer;
            Hull = w.GetFlyer(StateFormat.FlyerHull);
            Lift = w.GetFlyer(StateFormat.FlyerLift);
            Propeller = w.GetFlyer(StateFormat.FlyerPropeller);
            Power = w.GetFlyer(StateFormat.FlyerPower);
            Engine = w.GetFlyer(StateFormat.FlyerEngine);
            Armor = w.GetFlyer(StateFormat.FlyerArmor);
            TopGun = w.GetFlyer(StateFormat.FlyerTopGun);
            LowGun = w.GetFlyer(StateFormat.FlyerLowGun);
            AmmoniaLoaded = w.AmmoniaLoaded;
            GlowCrystalsLoaded = w.GlowCrystalsLoaded;
            UpdateFlyerSummary();
            DamageText = $"Hull hits {w.HullHits}, component damage bits 0x{w.DamageBits:X3}, W damage {w.WDamage}";
            BridgeCrew = string.Join(", ", Enumerable.Range(0, 5).Select(i =>
            {
                int c = w.BridgeStation(i);
                string who = c is >= 0 and < StateFormat.CharacterSlots && _host.State is { } st ? new CharacterRecord(st, c).Name : "empty";
                return $"{FlyerBook.Stations[i]}: {who}";
            }));
            foreach (var f in StoryFlags) f.Load(w.HasStoryFlag(f.Flag.Bit));

            foreach (var name in new[]
                     {
                         nameof(Location), nameof(MapKey), nameof(LightText), nameof(Leader), nameof(CurrentRow),
                         nameof(CurrentColumn), nameof(HasFlyer), nameof(FlyerSummary), nameof(DamageText),
                         nameof(BridgeCrew), nameof(PartyAccountText),
                     })
                OnPropertyChanged(name);
            RaiseCommands();
        }
        finally { _loading = false; }
    }

    private void RaiseCommands()
    {
        MaxFoodCommand.RaiseCanExecuteChanged();
        AddAccountCommand.RaiseCanExecuteChanged();
        RepairFlyerCommand.RaiseCanExecuteChanged();
        TeleportCommand.RaiseCanExecuteChanged();
    }

    private bool Write(Func<WorldRecord, bool> write)
    {
        if (_loading || _host.SuppressWriteBack) return true;
        if (!_host.CanEdit || Record is not { } w)
        {
            _host.Report("Not connected to a game or save — nothing was written.");
            Reload();
            return false;
        }
        if (!Resync()) { Reload(); return false; }
        if (!write(w))
        {
            _host.Report("The write was refused (the game may have closed or moved on); values reloaded.");
            Reload();
            return false;
        }
        _host.AfterWrite();
        return true;
    }

    private void Act(string what, Func<WorldRecord, bool> action)
    {
        if (!_host.CanEdit || Record is not { } w || !Resync()) return;
        bool ok = action(w);
        _host.AfterWrite();
        _host.Report(ok ? what + "." : what + " — some writes were refused.");
        Reload();
    }

    /// <summary>Re-reads the block just before a write, so nothing is compared against a snapshot one poll old.</summary>
    private bool Resync()
    {
        if (_host.State is { } state && state.Refresh()) return true;
        _host.Report("Could not re-read the game state; nothing was written.");
        return false;
    }
}
