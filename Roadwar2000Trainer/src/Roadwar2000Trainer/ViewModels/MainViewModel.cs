using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using GameTrainers.Common.Memory;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;
using Roadwar2000Trainer.Memory;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>An emulator the trainer could attach to.</summary>
public sealed record EmulatorChoice(int Pid, string Name, string Title)
{
    public override string ToString() => $"{Name} (pid {Pid})" + (string.IsNullOrEmpty(Title) ? "" : $" - {Title}");
}

/// <summary>
/// Owns the connection to the running game: finding the emulator, locating the data segment,
/// re-reading the slab on a timer, and holding the freeze loop. Every editing tab hangs off the
/// <see cref="Slab"/> this exposes.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly GameLocator _locator = new();

    private ProcessMemory? _memory;
    private LiveSlabTarget? _target;
    private bool _suppressWriteBack;

    public MainViewModel()
    {
        Gang = new GangViewModel(this);
        Vehicles = new VehiclesViewModel(this);
        Cities = new CitiesViewModel(this);
        Map = new MapViewModel(this);
        SaveEditor = new SaveEditorViewModel(this);
        Reference = new ReferenceViewModel();

        RefreshEmulatorsCommand = new RelayCommand(RefreshEmulators);
        AttachCommand = new RelayCommand(Attach, () => SelectedEmulator is not null && !IsAttached);
        DetachCommand = new RelayCommand(() => Detach(), () => IsAttached);
        RefreshNowCommand = new RelayCommand(() => Refresh(force: true), () => IsAttached);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => Tick();

        RefreshEmulators();
    }

    // ---- children ------------------------------------------------------------

    public GangViewModel Gang { get; }
    public VehiclesViewModel Vehicles { get; }
    public CitiesViewModel Cities { get; }
    public MapViewModel Map { get; }
    public SaveEditorViewModel SaveEditor { get; }
    public ReferenceViewModel Reference { get; }

    // ---- attach state --------------------------------------------------------

    public ObservableCollection<EmulatorChoice> Emulators { get; } = new();

    private EmulatorChoice? _selectedEmulator;
    public EmulatorChoice? SelectedEmulator
    {
        get => _selectedEmulator;
        set { if (SetField(ref _selectedEmulator, value)) AttachCommand.RaiseCanExecuteChanged(); }
    }

    private bool _isAttached;
    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (!SetField(ref _isAttached, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            AttachCommand.RaiseCanExecuteChanged();
            DetachCommand.RaiseCanExecuteChanged();
            RefreshNowCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>True when the trainer is attached and the slab it holds still looks like the game.</summary>
    public bool CanEdit => IsAttached && Slab is { IsLoaded: true } && !IsStale;

    private bool _isStale;
    /// <summary>Set when a re-locate fails; every write path consults it.</summary>
    public bool IsStale
    {
        get => _isStale;
        private set { if (SetField(ref _isStale, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    private string _status = "Not attached. Start Roadwar 2000 (START.EXE) in DOSBox, then press Attach.";
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    private string _locateDetail = "";
    public string LocateDetail
    {
        get => _locateDetail;
        private set => SetField(ref _locateDetail, value);
    }

    /// <summary>The live slab, or null when detached.</summary>
    public GameSlab? Slab { get; private set; }

    /// <summary>The live gang view, or null when detached.</summary>
    public GangRecord? GangRecord { get; private set; }

    public LiveSlabTarget? Target => _target;

    public RelayCommand RefreshEmulatorsCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand RefreshNowCommand { get; }

    // ---- freezes -------------------------------------------------------------

    private bool _freezeFood;
    public bool FreezeFood { get => _freezeFood; set => SetField(ref _freezeFood, value); }

    private bool _freezeFuel;
    public bool FreezeFuel { get => _freezeFuel; set => SetField(ref _freezeFuel, value); }

    private bool _freezeAmmo;
    public bool FreezeAmmo { get => _freezeAmmo; set => SetField(ref _freezeAmmo, value); }

    private bool _freezeCrew;
    public bool FreezeCrew { get => _freezeCrew; set => SetField(ref _freezeCrew, value); }

    private bool _freezeVehicles;
    /// <summary>Pins every vehicle's structure and tires to their maxima each tick.</summary>
    public bool FreezeVehicles { get => _freezeVehicles; set => SetField(ref _freezeVehicles, value); }

    private int[]? _frozenCrew;
    private int? _frozenFood, _frozenFuel, _frozenAmmo;

    /// <summary>
    /// Re-seeds the freeze snapshots from what the game holds right now.
    /// <para>
    /// Without this, a ticked freeze silently undid the user's own edits: the snapshot was taken
    /// once when the box was ticked and re-applied twice a second, so pressing "Top up supplies"
    /// wrote 9,999, reported success, and had the old value written back over it on the next tick.
    /// Every deliberate write -- a quick action or a typed value -- calls this, so a freeze holds
    /// whatever you last asked for rather than whatever it happened to see first.
    /// </para>
    /// </summary>
    public void ReseedFreezes()
    {
        if (GangRecord is not { } gang) return;
        if (FreezeFood) _frozenFood = gang.Food;
        if (FreezeFuel) _frozenFuel = gang.Fuel;
        if (FreezeAmmo) _frozenAmmo = gang.Ammo;
        if (FreezeCrew)
        {
            var crew = new int[SaveFormat.CrewRankCount];
            for (int r = 0; r < SaveFormat.CrewRankCount; r++) crew[r] = gang.GetCrew(r);
            _frozenCrew = crew;
        }
    }

    // ---- attach / detach -----------------------------------------------------

    public void RefreshEmulators()
    {
        var previous = SelectedEmulator?.Pid;
        Emulators.Clear();
        foreach (var p in GameLocator.FindEmulators())
        {
            string title;
            try { title = p.MainWindowTitle; } catch (InvalidOperationException) { title = ""; }
            Emulators.Add(new EmulatorChoice(p.Id, p.ProcessName, title));
            p.Dispose();
        }
        SelectedEmulator = Emulators.FirstOrDefault(e => e.Pid == previous) ?? Emulators.FirstOrDefault();
        if (Emulators.Count == 0)
            Status = "No DOSBox process found. Start Roadwar 2000 in DOSBox and press Refresh.";
    }

    private void Attach()
    {
        if (SelectedEmulator is not { } choice) return;
        try
        {
            _memory = ProcessMemory.Open(choice.Pid);
        }
        catch (Exception ex)
        {
            Status = "Could not open the emulator process: " + ex.Message +
                     " (the trainer must run as administrator).";
            return;
        }

        LocateResult? found;
        try { found = _locator.Locate(_memory); }
        catch (Exception ex) { Detach("Scan failed: " + ex.Message); return; }

        if (found is null)
        {
            Status = "Attached, but Roadwar 2000's data segment was not found. " +
                     "Make sure START.EXE is actually running inside this DOSBox, then Attach again.";
            _memory.Dispose();
            _memory = null;
            return;
        }

        _target = new LiveSlabTarget(_memory, found.DataSegmentHost);
        Slab = new GameSlab(_target);
        if (!Slab.Refresh() || !Slab.LooksValid())
        {
            Detach("Found the data segment but could not read a usable slab from it.");
            return;
        }

        GangRecord = new GangRecord(Slab);
        IsAttached = true;
        IsStale = false;
        LocateDetail = $"Data segment at 0x{found.DataSegmentHost:X}, slab at 0x{found.SlabHost:X} " +
                       $"({found.Detail}, {found.ElapsedMilliseconds} ms).";
        Status = $"Attached to {choice.Name} (pid {choice.Pid}).";

        Vehicles.Reload();
        Cities.Reload();
        Map.Reload();
        SaveEditor.OnAttachStateChanged();
        Refresh(force: true);
        _timer.Start();
    }

    /// <summary>
    /// Tears the session down. The reason is a parameter rather than something the caller sets
    /// beforehand, because this used to end by overwriting <see cref="Status"/> unconditionally --
    /// which meant every diagnostic set just before a teardown was replaced by "Detached." and the
    /// user was told nothing in exactly the cases where the message mattered.
    /// </summary>
    private void Detach(string? reason = null)
    {
        _timer.Stop();
        _memory?.Dispose();
        _memory = null;
        _target = null;
        Slab = null;
        GangRecord = null;
        _frozenCrew = null;
        IsAttached = false;
        IsStale = false;
        LocateDetail = "";
        Status = reason ?? "Detached.";
        Vehicles.Reload();
        Cities.Reload();
        Map.Reload();
        Gang.Reload();
        SaveEditor.OnAttachStateChanged();
    }

    // ---- polling -------------------------------------------------------------

    private void Tick()
    {
        if (!IsAttached || _target is null) return;

        if (!_target.IsAvailable)
        {
            Detach("The emulator has closed.");
            return;
        }

        Refresh(force: false);
    }

    /// <summary>Re-reads the slab and pushes the change through to every bound view.</summary>
    public void Refresh(bool force)
    {
        if (Slab is null) return;
        if (!Slab.Refresh())
        {
            IsStale = true;
            Status = "Lost contact with the game's memory; press Attach again.";
            return;
        }

        if (!Slab.LooksValid())
        {
            IsStale = true;
            Status = "The located memory no longer looks like Roadwar 2000 " +
                     "(the game may have exited inside DOSBox). Attach again.";
            return;
        }

        if (IsStale) { IsStale = false; Status = "Re-synchronised with the game."; }

        // Freezes run here, between the read and the repaint: against the snapshot that was just
        // taken, so a value the game has changed is restored on this tick rather than the next,
        // and before the views repopulate, so they never show the value being frozen away.
        ApplyFreezes();

        _suppressWriteBack = true;
        try
        {
            Gang.Reload();
            Vehicles.RefreshValues();
            if (force) { Cities.Reload(); Map.Reload(); }
            else Map.RefreshParty();
        }
        finally { _suppressWriteBack = false; }
    }

    /// <summary>True while a refresh is repopulating bound properties, so setters do not echo back.</summary>
    public bool SuppressWriteBack => _suppressWriteBack;

    private void ApplyFreezes()
    {
        if (GangRecord is not { } gang || Slab is null) return;

        // The sentinel is null, not zero: a gang that has genuinely run out of food is exactly when
        // freezing it at zero would be a legitimate thing to ask for, and a "<= 0 means unset" test
        // cannot express that.
        if (FreezeFood) { _frozenFood ??= gang.Food; gang.Food = _frozenFood.Value; }
        else _frozenFood = null;

        if (FreezeFuel) { _frozenFuel ??= gang.Fuel; gang.Fuel = _frozenFuel.Value; }
        else _frozenFuel = null;

        if (FreezeAmmo) { _frozenAmmo ??= gang.Ammo; gang.Ammo = _frozenAmmo.Value; }
        else _frozenAmmo = null;

        if (FreezeCrew)
        {
            if (_frozenCrew is null)
            {
                _frozenCrew = new int[SaveFormat.CrewRankCount];
                for (int r = 0; r < SaveFormat.CrewRankCount; r++) _frozenCrew[r] = gang.GetCrew(r);
            }
            for (int r = 0; r < SaveFormat.CrewRankCount; r++) gang.SetCrew(r, _frozenCrew[r]);
        }
        else _frozenCrew = null;

        if (FreezeVehicles)
        {
            for (int i = 0; i < gang.VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
            {
                var v = new VehicleRecord(Slab, i);
                if (v.LooksValid()) v.Repair();
            }
        }
    }

    /// <summary>Reports the outcome of an action in the status line.</summary>
    public void Report(string message) => Status = message;

    public void Dispose()
    {
        _timer.Stop();
        _memory?.Dispose();
    }
}
