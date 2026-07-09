using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using AutoduelTrainer.Memory;

namespace AutoduelTrainer;

public sealed class ProcessChoice
{
    public int Pid { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => $"{Name}  (PID {Pid})";
}

/// <summary>A city for the Location combo: <see cref="Id"/> is the game's city id
/// (0–15); the list is shown sorted by <see cref="Name"/>.</summary>
public sealed class CityChoice
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

/// <summary>A weapon type for the per-weapon Type combo: <see cref="Id"/> is the game's
/// weapon-type byte (0–11); <see cref="Name"/> is its catalog name.</summary>
public sealed class WeaponTypeChoice
{
    public byte Id { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

/// <summary>A weapon facing for the per-weapon Facing combo (0=Front, 1=Back, 2=Left, 3=Right).</summary>
public sealed class FacingChoice
{
    public byte Id { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly TrainerEngine _engine = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _freezeTimer;

    public ObservableCollection<ProcessChoice> Processes { get; } = new();
    // City ids stay 0–15 (CityIndex); only the display order is alphabetical.
    public ObservableCollection<CityChoice> Cities { get; } = new(
        GameData.Cities
            .Select((name, id) => new CityChoice { Id = id, Name = name })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase));
    public ObservableCollection<ComponentInfo> Weapons { get; } = new();
    public ObservableCollection<ComponentInfo> Armor { get; } = new();
    public ObservableCollection<ComponentInfo> Drivetrain { get; } = new();

    // Fixed pick-lists for the editable weapon rows.
    public IReadOnlyList<WeaponTypeChoice> WeaponTypes { get; } =
        GameData.WeaponNames
            .Select((name, id) => new WeaponTypeChoice { Id = (byte)id, Name = name })
            .ToArray();
    public IReadOnlyList<FacingChoice> Facings { get; } = new[]
    {
        new FacingChoice { Id = 0, Name = "Front" },
        new FacingChoice { Id = 1, Name = "Back" },
        new FacingChoice { Id = 2, Name = "Left" },
        new FacingChoice { Id = 3, Name = "Right" },
    };

    public RelayCommand RefreshProcessesCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand RescanCommand { get; }
    public RelayCommand ReadCommand { get; }

    public RelayCommand MaxMoneyCommand { get; }
    public RelayCommand MaxSkillsCommand { get; }
    public RelayCommand FullHealthCommand { get; }
    public RelayCommand ApplyPlayerCommand { get; }
    public RelayCommand TeleportCommand { get; }
    public RelayCommand ReloadGameCommand { get; }

    public RelayCommand ChargeBatteryCommand { get; }
    public RelayCommand ReloadWeaponsCommand { get; }
    public RelayCommand RepairAllCommand { get; }
    public RelayCommand ApplyCarCommand { get; }
    public RelayCommand ApplyWeaponsCommand { get; }
    public RelayCommand ApplyArmorCommand { get; }
    public RelayCommand ApplyDrivetrainCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach, () => Selected is not null && !Attached);
        DetachCommand = new RelayCommand(Detach, () => Attached);
        RescanCommand = new RelayCommand(Rescan, () => Attached);
        ReadCommand = new RelayCommand(ReadState, () => Attached);

        // Quick actions update the bound properties first so a value that is being
        // frozen adopts the new target instead of the freeze timer reverting it.
        MaxMoneyCommand = new RelayCommand(() => Guard(() =>
        {
            Money = GameData.MoneyMax; _engine.SetMoney(Money); ReadState();
        }), () => Attached);
        MaxSkillsCommand = new RelayCommand(() => Guard(() =>
        {
            Driving = Marksmanship = Mechanic = GameData.SkillMax;
            _engine.SetDriving(Driving); _engine.SetMarksmanship(Marksmanship); _engine.SetMechanic(Mechanic);
            ReadState();
        }), () => Attached);
        FullHealthCommand = new RelayCommand(() => Guard(() =>
        {
            Health = GameData.HealthMax; BodyArmor = 5;
            _engine.SetHealth(Health); _engine.SetBodyArmor(BodyArmor); ReadState();
        }), () => Attached);
        ApplyPlayerCommand = new RelayCommand(ApplyPlayer, () => Attached);
        TeleportCommand = new RelayCommand(() => Guard(() =>
        {
            _engine.Teleport(CityIndex);
            _cityPending = false;   // selection applied; let refresh follow the game again
            string city = GameData.Cities[Math.Clamp(CityIndex, 0, GameData.Cities.Length - 1)];
            ReadState();
            if (AutoReloadAfterTeleport)
            {
                Status = $"Teleported to {city}. Reloading the game to apply it…";
                _ = ReloadGameAsync();
            }
            else
            {
                Status = $"Teleported to {city}. Save then reload in the game to apply it there.";
            }
        }), () => Attached);
        ReloadGameCommand = new RelayCommand(() => { _ = ReloadGameAsync(); }, () => Attached && !_reloading);

        ChargeBatteryCommand = new RelayCommand(() => Guard(() => { _engine.ChargeBatteryFull(); ReadState(); }), () => Attached && HasCar);
        ReloadWeaponsCommand = new RelayCommand(() => Guard(() =>
        {
            int n = _engine.ReloadAllWeapons(); Status = $"Reloaded {n} weapon(s)."; ReadState();
        }), () => Attached && HasCar);
        RepairAllCommand = new RelayCommand(() => Guard(() =>
        {
            int n = _engine.RepairAll(); Status = $"Repaired {n} component(s)."; ReadState();
        }), () => Attached && HasCar);
        ApplyCarCommand = new RelayCommand(ApplyCar, () => Attached && HasCar);
        ApplyWeaponsCommand = new RelayCommand(ApplyWeapons, () => Attached && HasCar);
        ApplyArmorCommand = new RelayCommand(ApplyArmor, () => Attached && HasCar);
        ApplyDrivetrainCommand = new RelayCommand(ApplyDrivetrain, () => Attached && HasCar);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _timer.Tick += (_, _) => { if (Attached && AutoRefresh && !_reloading) ReadState(); };
        _timer.Start();

        // A faster, display-free timer re-applies any active "freeze" so values
        // that the game lowers during play (health, battery, ammo, armor DP) are
        // topped straight back up. It writes only — the 900 ms timer above owns
        // the read/refresh so the UI is not churned ~5x/second.
        _freezeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _freezeTimer.Tick += (_, _) => { if (Attached && AnyFreeze && !_reloading) ApplyFreezes(); };
        _freezeTimer.Start();

        RefreshProcesses();
    }

    // ---------------------------------------------------------------- attach
    private ProcessChoice? _selected;
    public ProcessChoice? Selected { get => _selected; set { Set(ref _selected, value); RaiseCommands(); } }

    private bool _attached;
    public bool Attached
    {
        get => _attached;
        private set
        {
            // Dropping the session clears any freezes so a previous target value
            // can never be written into a freshly-attached process, and drops any
            // pending city pick so a re-attach starts by following the game.
            if (!value) { FreezeMoney = FreezeHealth = FreezeCarStats = FreezeTimeOfDay = false; _cityPending = _dayPending = false; _carStatsPending = _weaponsPending = _armorPending = _drivetrainPending = false; }
            Set(ref _attached, value);
            Raise(nameof(NotAttached));
            RaiseCommands();
        }
    }
    public bool NotAttached => !Attached;

    private bool _autoRefresh = true;
    public bool AutoRefresh { get => _autoRefresh; set => Set(ref _autoRefresh, value); }

    private string _status = "Not attached. Load AUTODUEL in DOSBox, get past the title screen, then Attach.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private string _addressInfo = "";
    public string AddressInfo { get => _addressInfo; set => Set(ref _addressInfo, value); }

    public void RefreshProcesses()
    {
        Processes.Clear();
        foreach (var p in ProcessMemory.FindDosBoxProcesses())
        {
            try { Processes.Add(new ProcessChoice { Pid = p.Id, Name = p.ProcessName }); }
            catch { /* process vanished */ }
        }
        Selected ??= Processes.FirstOrDefault();
        Status = Processes.Count == 0
            ? "No DOSBox process found. Start DOSBox-X running AUTODUEL, then Refresh."
            : $"Found {Processes.Count} DOSBox process(es). Select one and Attach.";
    }

    private void Attach()
    {
        if (Selected is null) return;
        try
        {
            _engine.Attach(Selected.Pid);
            Attached = true;
            AddressInfo = $"PID {_engine.ProcessId}   player @ 0x{_engine.PlayerAddress.ToInt64():X}";
            ReadState();
            Status = $"Attached to {DriverName}.";
        }
        catch (Exception ex)
        {
            Attached = false;
            Status = ex.Message;
        }
    }

    private void Rescan()
    {
        try
        {
            _engine.Rescan();
            AddressInfo = $"PID {_engine.ProcessId}   player @ 0x{_engine.PlayerAddress.ToInt64():X}";
            ReadState();
            Status = $"Re-scan found {DriverName}.";
        }
        catch (Exception ex)
        {
            HandleEngineFault(ex, "Re-scan failed: ");
        }
    }

    private void Detach()
    {
        _engine.Detach();
        Attached = false;
        AddressInfo = "";
        Status = "Detached.";
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { HandleEngineFault(ex, ""); }
    }

    /// <summary>
    /// Central handler for a failed memory operation: if the target process is gone,
    /// cleanly detach and say so; otherwise surface the error but keep the session.
    /// </summary>
    private void HandleEngineFault(Exception ex, string prefix)
    {
        if (_engine.TargetProcessExited())
        {
            _engine.Detach();
            Attached = false;
            AddressInfo = "";
            Status = "The DOSBox process has closed. Detached.";
            RefreshProcesses();
        }
        else
        {
            Status = prefix + ex.Message;
            Attached = _engine.Attached;
        }
    }

    // ---------------------------------------------------------------- read
    public void ReadState()
    {
        if (!_engine.Attached) { Attached = false; return; }
        if (_engine.TargetProcessExited())
        {
            HandleEngineFault(new InvalidOperationException("process exited"), "");
            return;
        }
        try
        {
            var s = _engine.Read();

            DriverName = s.DriverName;
            // Frozen fields are driven by the user's target value, so don't let a
            // refresh overwrite them with a momentary in-game reading.
            if (!FreezeMoney) Money = s.Money;
            Prestige = s.Prestige;
            if (!FreezeHealth) Health = s.Health;
            BodyArmor = s.BodyArmor;
            Driving = s.Driving;
            Marksmanship = s.Marksmanship;
            Mechanic = s.Mechanic;
            // Always show the game's real city; follow it in the target combo only
            // when the user has no pending selection.
            CurrentLocation = s.CityName;
            if (!_cityPending)
            {
                _syncingCity = true;
                CityIndex = s.CityId;
                _syncingCity = false;
            }
            if (!_dayPending)
            {
                _syncingDay = true;
                Day = s.Day;
                _syncingDay = false;
            }
            // Time-of-day is a frozen-hold target (like Money/Health): while frozen the
            // box keeps the user's daytime value and the freeze timer re-writes it.
            if (!FreezeTimeOfDay) TimeOfDay = s.TimeOfDay;

            HasCar = s.HasCar;
            CarName = s.HasCar ? s.CarName : "(no car with you)";
            CarInfo = s.HasCar
                ? $"Value ${s.CarValue:N0}   Weight left {s.WeightLeft}/{s.MaxWeight} lb   Spaces {s.SpacesLeft}/{s.MaxSpaces}"
                : "";
            // Like the driver's city/day fields, the editable car boxes are targets:
            // once the user changes one, hold their value instead of letting the
            // refresh snap it back before they click Apply Car. CarInfo above always
            // shows the game's live values, so the real state is still visible.
            if (!_carStatsPending)
            {
                _syncingCar = true;
                Battery = s.Battery;
                BatteryMax = s.BatteryMax;
                MaxWeight = s.MaxWeight;
                WeightLeft = s.WeightLeft;
                MaxSpaces = s.MaxSpaces;
                SpacesLeft = s.SpacesLeft;
                Handling = s.Handling;
                Acceleration = s.Acceleration;
                Suspension = s.Suspension;
                Chassis = s.Chassis;
                CarValue = s.CarValue;
                _syncingCar = false;
            }

            // Refresh the rows in place while the slot layout is unchanged so an
            // in-progress edit (and the focused control) is not torn down every tick;
            // only rebuild when the game adds or removes a part. Each list is held
            // independently: editing one kind of part does not freeze the others.
            if (!_weaponsPending)
            {
                _syncingCar = true;
                SyncComponents(Weapons, s.Weapons);
                _syncingCar = false;
            }
            if (!_armorPending)
            {
                _syncingCar = true;
                SyncComponents(Armor, s.Armor);
                _syncingCar = false;
            }
            if (!_drivetrainPending)
            {
                _syncingCar = true;
                SyncComponents(Drivetrain, s.Drivetrain);
                _syncingCar = false;
            }

            RaiseCommands();
        }
        catch (Exception ex)
        {
            HandleEngineFault(ex, "Lost the game state: ");
        }
    }

    private void ApplyPlayer() => Guard(() =>
    {
        _engine.SetMoney(Money);
        _engine.SetPrestige(Prestige);
        _engine.SetHealth(Health);
        _engine.SetBodyArmor(BodyArmor);
        _engine.SetDriving(Driving);
        _engine.SetMarksmanship(Marksmanship);
        _engine.SetMechanic(Mechanic);
        _engine.SetDay(Day);
        _dayPending = false;   // day written; let refresh follow the game again
        // Apply Driver only sets the current city (no route cancel / car drag);
        // the Teleport button performs the full relocation.
        _engine.SetCity(CityIndex);
        _cityPending = false;   // selection applied; let refresh follow the game again
        Status = "Player values written.";
        ReadState();
    });

    /// <summary>
    /// Push every mounted weapon's edited fields (type, facing, DP, ammo) back into
    /// the game. Self-powered weapons keep their infinite-ammo sentinel automatically.
    /// </summary>
    private void ApplyWeapons() => Guard(() =>
    {
        int n = 0;
        foreach (var w in Weapons)
        {
            _engine.SetComponent(w.Slot, w.Type, w.CurrentDp, w.MaxDp, w.Location, w.Ammo);
            n++;
        }
        _weaponsPending = false;   // edits written; let refresh follow the game again
        Status = $"Wrote {n} weapon(s).";
        ReadState();
    });

    /// <summary>Update <paramref name="target"/> from a fresh read: mutate matching rows in
    /// place (preserving bindings/focus), rebuilding only when the slot layout changed.
    /// Each row is watched via <see cref="OnComponentEdited"/> so a user edit flips the
    /// weapons-pending flag and refreshes stop clobbering it.</summary>
    private void SyncComponents(
        ObservableCollection<ComponentInfo> target, List<ComponentInfo> fresh)
    {
        bool sameLayout = target.Count == fresh.Count;
        for (int i = 0; sameLayout && i < fresh.Count; i++)
            if (target[i].Slot != fresh[i].Slot) sameLayout = false;

        if (!sameLayout)
        {
            foreach (var c in target) c.PropertyChanged -= OnComponentEdited;
            target.Clear();
            foreach (var c in fresh)
            {
                c.PropertyChanged += OnComponentEdited;
                target.Add(c);
            }
            return;
        }

        for (int i = 0; i < fresh.Count; i++) target[i].UpdateFrom(fresh[i]);
    }

    /// <summary>A component row changed. When the change is a user edit (not a
    /// refresh-driven <see cref="ComponentInfo.UpdateFrom"/>, which runs under
    /// <see cref="_syncingCar"/>), mark that part list pending so refreshes hold the
    /// edit. The row's slot picks which list it belongs to.</summary>
    private void OnComponentEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingCar || sender is not ComponentInfo c) return;
        if (c.Slot >= GameData.FirstWeaponSlot) _weaponsPending = true;
        else if (c.Slot >= GameData.FirstArmorSlot) _armorPending = true;
        else _drivetrainPending = true;
    }

    private void ApplyCar() => Guard(() =>
    {
        // Max first, so SetBattery clamps the current charge to the new ceiling.
        _engine.SetBatteryMax(BatteryMax);
        _engine.SetBattery(Battery);
        _engine.SetMaxWeight(MaxWeight);
        _engine.SetWeightLeft(WeightLeft);
        _engine.SetMaxSpaces(MaxSpaces);
        _engine.SetSpacesLeft(SpacesLeft);
        _engine.SetHandling(Handling);
        _engine.SetAcceleration(Acceleration);
        _engine.SetSuspension(Suspension);
        _engine.SetChassis(Chassis);
        _engine.SetCarValue(CarValue);
        _carStatsPending = false;   // edits written; let refresh follow the game again
        Status = "Car values written.";
        ReadState();
    });

    /// <summary>Write the edited armor DP (slots 5–9) back into the game.</summary>
    private void ApplyArmor() => Guard(() =>
    {
        foreach (var a in Armor)
            _engine.SetComponentDp(a.Slot, a.CurrentDp, a.MaxDp);
        _armorPending = false;   // edits written; let refresh follow the game again
        Status = $"Wrote {Armor.Count} armor facet(s).";
        ReadState();
    });

    /// <summary>Write the edited power-plant / tire DP (slots 0–4) back into the game.</summary>
    private void ApplyDrivetrain() => Guard(() =>
    {
        foreach (var d in Drivetrain)
            _engine.SetComponentDp(d.Slot, d.CurrentDp, d.MaxDp);
        _drivetrainPending = false;   // edits written; let refresh follow the game again
        Status = $"Wrote {Drivetrain.Count} power-plant / tire part(s).";
        ReadState();
    });

    // ---------------------------------------------------------------- reload
    private bool _reloading;

    /// <summary>
    /// Drive the game's own quit → reload flow so a teleport (or any edit persisted
    /// on quit) takes effect without the player saving/loading by hand. Sends
    /// q y y 1 4 = Quit, Yes, "another go" Yes, "continue saved driver" (1),
    /// driver #4 (4). Must be started from the game's main city menu.
    /// </summary>
    private async Task ReloadGameAsync()
    {
        if (_reloading) return;
        IntPtr hwnd = GetGameWindow();
        if (hwnd == IntPtr.Zero)
        {
            Status = "Couldn't find the DOSBox window to send keys to.";
            return;
        }
        _reloading = true;
        RaiseCommands();
        try
        {
            Status = "Reloading the game (Quit → reload driver 4)…";
            await GameInput.SendSequenceAsync(hwnd, "qyy14");
            await Task.Delay(1000); // let the driver finish loading before we read again
        }
        finally
        {
            _reloading = false;
            RaiseCommands();
        }
        Status = "Reload sequence sent — the game should now be in the new city.";
        ReadState();
    }

    private IntPtr GetGameWindow()
    {
        try
        {
            using var p = Process.GetProcessById(_engine.ProcessId);
            return p.MainWindowHandle;
        }
        catch { return IntPtr.Zero; }
    }

    // ---------------------------------------------------------------- freezing
    private bool AnyFreeze => FreezeMoney || FreezeHealth || FreezeCarStats || FreezeTimeOfDay;

    /// <summary>Re-assert every active freeze. Runs on the fast freeze timer.</summary>
    private void ApplyFreezes() => Guard(() =>
    {
        if (FreezeMoney) _engine.SetMoney(Money);
        if (FreezeHealth) _engine.SetHealth(Health);
        if (FreezeTimeOfDay) _engine.SetTimeOfDay(TimeOfDay);
        if (FreezeCarStats && HasCar)
        {
            _engine.ChargeBatteryFull();   // full battery
            _engine.ReloadAllWeapons();    // full ammo
            _engine.RepairAll();           // full armor + no component damage (tires, etc.)
        }
    });

    // ---------------------------------------------------------------- fields
    private string _driverName = "";
    public string DriverName { get => _driverName; set => Set(ref _driverName, value); }

    private int _money;
    public int Money { get => _money; set => Set(ref _money, value); }

    private int _prestige;
    public int Prestige { get => _prestige; set => Set(ref _prestige, value); }

    private int _health;
    // Health has a tiny valid domain (0..3); bound it here so the "(max 3)" label
    // is honest and an out-of-range value can never reach the write path.
    public int Health { get => _health; set => Set(ref _health, Math.Clamp(value, 0, GameData.HealthMax)); }

    private int _bodyArmor;
    public int BodyArmor { get => _bodyArmor; set => Set(ref _bodyArmor, value); }

    private int _driving;
    public int Driving { get => _driving; set => Set(ref _driving, value); }

    private int _marksmanship;
    public int Marksmanship { get => _marksmanship; set => Set(ref _marksmanship, value); }

    private int _mechanic;
    public int Mechanic { get => _mechanic; set => Set(ref _mechanic, value); }

    // The Location combo is a *target* selector. A user pick becomes "pending" so the
    // auto-refresh (which reads the game's real city) does not snap it back before the
    // user clicks Teleport / Apply Driver. _syncingCity marks refresh-driven updates.
    private bool _syncingCity;
    private bool _cityPending;

    private int _cityIndex;
    public int CityIndex
    {
        get => _cityIndex;
        set
        {
            if (Set(ref _cityIndex, value) && !_syncingCity)
            {
                _cityPending = true;
                if (Attached && value >= 0 && value < GameData.Cities.Length)
                    Status = $"Location set to {GameData.Cities[value]} — click Teleport to go there.";
            }
        }
    }

    private string _currentLocation = "";
    public string CurrentLocation { get => _currentLocation; set => Set(ref _currentLocation, value); }

    // Like the Location combo, the day field is a *target*: a user edit becomes
    // "pending" so the auto-refresh (which reads the game's real day) does not snap
    // the box back before the user clicks Apply Driver. _syncingDay marks the
    // refresh-driven updates. The counter is a single byte (days since 1 Jan 2030);
    // clamp so an out-of-range value can never reach the write path.
    private bool _syncingDay;
    private bool _dayPending;

    private int _day;
    public int Day
    {
        get => _day;
        set
        {
            if (Set(ref _day, Math.Clamp(value, 0, GameData.DayMax)))
            {
                Raise(nameof(DateInfo));
                if (!_syncingDay) _dayPending = true;
            }
        }
    }

    // Read-only companion to the editable Day: names the weekday and the epoch,
    // derived from Day via the documented (day + 5) % 7 rule so it tracks edits live.
    public string DateInfo => $"{GameData.DaysOfWeek[GameData.WeekdayForDay(_day)]} (since 1 Jan 2030)";

    private bool _hasCar;
    public bool HasCar { get => _hasCar; private set { Set(ref _hasCar, value); RaiseCommands(); } }

    private string _carName = "";
    public string CarName { get => _carName; set => Set(ref _carName, value); }

    private string _carInfo = "";
    public string CarInfo { get => _carInfo; set => Set(ref _carInfo, value); }

    // The editable car boxes are *targets*, like the driver's city/day fields: the
    // first user edit flips _carStatsPending so the auto-refresh stops overwriting
    // them until Apply Car is clicked. _weaponsPending does the same for weapon rows.
    // _syncingCar marks refresh-driven updates (scalar writes and ComponentInfo.UpdateFrom)
    // so those never count as user edits.
    private bool _syncingCar;
    private bool _carStatsPending;
    private bool _weaponsPending;
    private bool _armorPending;
    private bool _drivetrainPending;

    /// <summary>Setter helper for the car-stat targets: sets the field and, when the
    /// change comes from the user (not a refresh), marks the car stats pending.</summary>
    private bool SetCar<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Set(ref field, value, name)) return false;
        if (!_syncingCar) _carStatsPending = true;
        return true;
    }

    private int _battery;
    public int Battery { get => _battery; set => SetCar(ref _battery, value); }

    private int _batteryMax;
    public int BatteryMax { get => _batteryMax; set => SetCar(ref _batteryMax, value); }

    private int _maxWeight;
    public int MaxWeight { get => _maxWeight; set => SetCar(ref _maxWeight, value); }

    private int _weightLeft;
    public int WeightLeft { get => _weightLeft; set => SetCar(ref _weightLeft, value); }

    private int _maxSpaces;
    public int MaxSpaces { get => _maxSpaces; set => SetCar(ref _maxSpaces, value); }

    private int _spacesLeft;
    public int SpacesLeft { get => _spacesLeft; set => SetCar(ref _spacesLeft, value); }

    private int _handling;
    public int Handling { get => _handling; set => SetCar(ref _handling, value); }

    private int _acceleration;
    public int Acceleration { get => _acceleration; set => SetCar(ref _acceleration, value); }

    private int _suspension;
    public int Suspension { get => _suspension; set => SetCar(ref _suspension, value); }

    private int _chassis;
    public int Chassis { get => _chassis; set => SetCar(ref _chassis, value); }

    private int _carValue;
    public int CarValue { get => _carValue; set => SetCar(ref _carValue, value); }

    // Time-of-day is a frozen-hold field like Money: editable, and while its freeze is
    // on the value holds (the freeze timer re-writes it) so the game keeps thinking it
    // is daytime and shops never close "for the evening".
    private int _timeOfDay;
    public int TimeOfDay { get => _timeOfDay; set => Set(ref _timeOfDay, value); }

    // --- freeze toggles ------------------------------------------------------
    private bool _freezeMoney;
    public bool FreezeMoney { get => _freezeMoney; set => Set(ref _freezeMoney, value); }

    private bool _freezeHealth;
    public bool FreezeHealth { get => _freezeHealth; set => Set(ref _freezeHealth, value); }

    private bool _freezeCarStats;
    public bool FreezeCarStats { get => _freezeCarStats; set => Set(ref _freezeCarStats, value); }

    private bool _freezeTimeOfDay;
    public bool FreezeTimeOfDay { get => _freezeTimeOfDay; set => Set(ref _freezeTimeOfDay, value); }

    private bool _autoReloadAfterTeleport = true;
    public bool AutoReloadAfterTeleport { get => _autoReloadAfterTeleport; set => Set(ref _autoReloadAfterTeleport, value); }

    private void RaiseCommands()
    {
        AttachCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        RescanCommand.RaiseCanExecuteChanged();
        ReadCommand.RaiseCanExecuteChanged();
        MaxMoneyCommand.RaiseCanExecuteChanged();
        MaxSkillsCommand.RaiseCanExecuteChanged();
        FullHealthCommand.RaiseCanExecuteChanged();
        ApplyPlayerCommand.RaiseCanExecuteChanged();
        TeleportCommand.RaiseCanExecuteChanged();
        ReloadGameCommand.RaiseCanExecuteChanged();
        ChargeBatteryCommand.RaiseCanExecuteChanged();
        ReloadWeaponsCommand.RaiseCanExecuteChanged();
        RepairAllCommand.RaiseCanExecuteChanged();
        ApplyCarCommand.RaiseCanExecuteChanged();
        ApplyWeaponsCommand.RaiseCanExecuteChanged();
        ApplyArmorCommand.RaiseCanExecuteChanged();
        ApplyDrivetrainCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _timer.Stop();
        _freezeTimer.Stop();
        _engine.Dispose();
    }
}
