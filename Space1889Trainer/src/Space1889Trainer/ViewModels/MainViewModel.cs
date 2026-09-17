using System.Collections.ObjectModel;
using System.Windows.Threading;
using Space1889Trainer.Files;
using Space1889Trainer.Game;
using Space1889Trainer.Memory;

namespace Space1889Trainer.ViewModels;

/// <summary>An emulator the trainer could attach to.</summary>
public sealed record EmulatorChoice(int Pid, string Name, string Title)
{
    public override string ToString() => $"{Name} (pid {Pid})" + (string.IsNullOrEmpty(Title) ? "" : $" - {Title}");
}

/// <summary>
/// Owns the connection to the running game: finding DOSBox, locating the state block, re-reading it on a timer,
/// the freezes and the party-wide actions. It is the live <see cref="IStateHost"/> every editor writes through.
/// </summary>
public sealed class MainViewModel : ObservableObject, IStateHost, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private ProcessMemory? _memory;
    private LiveStateTarget? _target;
    private bool _suppressWriteBack;
    private string _gameFolder = "";

    public MainViewModel()
    {
        for (int i = 0; i < StateFormat.CharacterSlots; i++) Characters.Add(new CharacterViewModel(this, i));
        World = new WorldViewModel(this);
        Maps = new MapsViewModel(this);
        World.TeleportCheck = Maps.CheckTeleport;
        SaveEditor = new SaveEditorViewModel(this);
        Reference = new ReferenceViewModel();
        Cluebook = new CluebookViewModel(this);

        RefreshEmulatorsCommand = new RelayCommand(RefreshEmulators);
        AttachCommand = new RelayCommand(Attach, () => SelectedEmulator is not null && !IsAttached);
        DetachCommand = new RelayCommand(() => Detach(), () => IsAttached);
        RefreshNowCommand = new RelayCommand(() => Refresh(force: true), () => IsAttached);
        HealPartyCommand = new RelayCommand(() => PartyAction("Party healed", r => r.FullHeal()), () => CanEdit);
        MaxPartyCommand = new RelayCommand(() => PartyAction("Party maxed out",
            r => r.MaxAttributes() & r.MaxSkills() & r.FullHeal() & r.RefillAmmunition()), () => CanEdit);
        ReloadPartyCommand = new RelayCommand(() => PartyAction("Party's firearms reloaded", r => r.RefillAmmunition()), () => CanEdit);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => Tick();

        RefreshEmulators();
        GuessGameFolderInBackground(null);
    }

    // ---- children -------------------------------------------------------------------------------

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetField(ref _selectedCharacter, value);
    }

    public WorldViewModel World { get; }
    public MapsViewModel Maps { get; }
    public SaveEditorViewModel SaveEditor { get; }
    public ReferenceViewModel Reference { get; }
    public CluebookViewModel Cluebook { get; }

    /// <summary>The Space 1889 folder the Maps, Save Editor and Cluebook tabs read from.</summary>
    public string GameFolder
    {
        get => _gameFolder;
        set
        {
            if (!SetField(ref _gameFolder, value ?? "")) return;
            OnPropertyChanged(nameof(GameFolderStatus));
            Maps.OnGameFolderChanged();
            SaveEditor.OnGameFolderChanged();
        }
    }

    public string GameFolderStatus => Files.GameFolder.IsGameFolder(_gameFolder)
        ? "Game folder found: maps, NPCs and saves are read from it."
        : "Game folder not found — set it on the Maps or Save Editor tab (the folder with 1889.COM, A.SYS and B.SYS).";

    // ---- attach state ---------------------------------------------------------------------------

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
            RaiseActionCommands();
            RepaintEditors();
        }
    }

    private bool _isStale;
    /// <summary>Set when the block stops validating; every write path consults it through <see cref="CanEdit"/>.</summary>
    public bool IsStale
    {
        get => _isStale;
        private set
        {
            if (!SetField(ref _isStale, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            RaiseActionCommands();
            RepaintEditors();   // the editors' own CanEdit (and IsEnabled) follow this one
        }
    }

    public bool CanEdit => IsAttached && State is { IsLoaded: true } && !IsStale;

    private string _status = "Not attached. Start Space 1889 (1889.COM) in DOSBox, begin or load a game, then press Attach.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private string _locateDetail = "";
    public string LocateDetail { get => _locateDetail; private set => SetField(ref _locateDetail, value); }

    /// <summary>The live block, or null when detached.</summary>
    public GameState? State { get; private set; }

    public bool SuppressWriteBack => _suppressWriteBack;

    public RelayCommand RefreshEmulatorsCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand RefreshNowCommand { get; }
    public RelayCommand HealPartyCommand { get; }
    public RelayCommand MaxPartyCommand { get; }
    public RelayCommand ReloadPartyCommand { get; }

    // ---- freezes --------------------------------------------------------------------------------

    private bool _freezeHealth, _freezeFatigue, _freezeFood, _freezeAccount, _freezeWealth, _freezeAmmo;

    /// <summary>Keeps every character at full health.</summary>
    public bool FreezeHealth { get => _freezeHealth; set => SetField(ref _freezeHealth, value); }

    /// <summary>Keeps fatigue and insanity at zero.</summary>
    public bool FreezeFatigue { get => _freezeFatigue; set => SetField(ref _freezeFatigue, value); }

    /// <summary>Holds FOOD at the value it had when ticked (or last edited).</summary>
    public bool FreezeFood { get => _freezeFood; set { if (SetField(ref _freezeFood, value)) _frozenFood = null; } }

    /// <summary>Holds the party account at the value it had when ticked (or last edited).</summary>
    public bool FreezeAccount { get => _freezeAccount; set { if (SetField(ref _freezeAccount, value)) _frozenAccount = null; } }

    /// <summary>Holds each character's wealth at the value it had when ticked (or last edited).</summary>
    public bool FreezeWealth { get => _freezeWealth; set { if (SetField(ref _freezeWealth, value)) _frozenWealth = null; } }

    /// <summary>Keeps every firearm's IN GUN at a full load (reserve ROUNDS are left alone).</summary>
    public bool FreezeAmmo { get => _freezeAmmo; set => SetField(ref _freezeAmmo, value); }

    private int? _frozenFood;
    private long? _frozenAccount;
    private long[]? _frozenWealth;

    /// <summary>
    /// Every deliberate write re-seeds the value freezes from what the block now holds, so a freeze keeps what
    /// you last asked for instead of snapping it back to whatever it first saw.
    /// </summary>
    public void AfterWrite()
    {
        if (State is null) return;
        var world = new WorldRecord(State);
        if (FreezeFood) _frozenFood = world.Food;
        if (FreezeAccount) _frozenAccount = world.PartyAccount;
        if (FreezeWealth) _frozenWealth = Enumerable.Range(0, StateFormat.CharacterSlots).Select(i => new CharacterRecord(State, i).Wealth).ToArray();
    }

    private void ApplyFreezes()
    {
        if (State is null) return;
        var world = new WorldRecord(State);

        if (FreezeFood) { _frozenFood ??= world.Food; if (world.Food != _frozenFood) world.SetFood(_frozenFood.Value); }
        if (FreezeAccount) { _frozenAccount ??= world.PartyAccount; if (world.PartyAccount != _frozenAccount) world.SetPartyAccount(_frozenAccount.Value); }
        if (FreezeWealth)
            _frozenWealth ??= Enumerable.Range(0, StateFormat.CharacterSlots).Select(i => new CharacterRecord(State, i).Wealth).ToArray();

        for (int i = 0; i < StateFormat.CharacterSlots; i++)
        {
            var r = new CharacterRecord(State, i);
            if (r.IsEmpty) continue;
            if (FreezeHealth && r.Health < r.MaxHealth) r.SetHealth(r.MaxHealth);
            if (FreezeFatigue) { if (r.Fatigue != 0) r.SetFatigue(0); if (r.Mental != 0) r.SetMental(0); }
            if (FreezeWealth && _frozenWealth is { } w && r.Wealth != w[i]) r.SetWealth(w[i]);
            if (FreezeAmmo) r.RefillAmmunition(rounds: 0);
        }
    }

    // ---- party-wide actions ---------------------------------------------------------------------

    private void PartyAction(string what, Func<CharacterRecord, bool> action)
    {
        if (!CanEdit || State is null) return;
        if (!State.Refresh())   // act on what the game holds now, not the last poll's snapshot
        {
            Report("Could not re-read the game state; nothing was written.");
            return;
        }
        bool ok = true;
        int n = 0;
        for (int i = 0; i < StateFormat.CharacterSlots; i++)
        {
            var r = new CharacterRecord(State, i);
            if (r.IsEmpty) continue;
            ok &= action(r);
            n++;
        }
        AfterWrite();
        Report(ok ? $"{what} ({n} characters)." : $"{what} — some writes were refused.");
        Refresh(force: true);
    }

    private void RaiseActionCommands()
    {
        HealPartyCommand.RaiseCanExecuteChanged();
        MaxPartyCommand.RaiseCanExecuteChanged();
        ReloadPartyCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Repaints the live editors from the current snapshot (no writes) so their enablement follows <see cref="CanEdit"/>.</summary>
    private void RepaintEditors()
    {
        bool previous = _suppressWriteBack;
        _suppressWriteBack = true;
        try
        {
            foreach (var c in Characters) c.Reload();
            World.Reload();
        }
        finally { _suppressWriteBack = previous; }
        SaveEditor.OnAttachStateChanged();
    }

    /// <summary>
    /// Looks for the game folder off the UI thread (the DOSBox command line is read through WMI, and the common
    /// install locations are searched), then applies it unless a folder has been chosen in the meantime.
    /// </summary>
    private void GuessGameFolderInBackground(int? emulatorPid)
    {
        Task.Run(() =>
        {
            string? folder;
            try { folder = Files.GameFolder.Guess(emulatorPid); }
            catch (Exception) { return; }   // only a convenience; the folder can always be set by hand
            if (folder is null) return;
            _dispatcher.BeginInvoke(() =>
            {
                if (!Files.GameFolder.IsGameFolder(GameFolder)) GameFolder = folder;
            });
        });
    }

    // ---- attach / detach ------------------------------------------------------------------------

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
        if (Emulators.Count == 0 && !IsAttached)
            Status = "No DOSBox process found. Start Space 1889 in DOSBox and press Refresh list.";
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
            Status = "Could not open the emulator process: " + ex.Message + " (run the trainer as administrator).";
            return;
        }

        LocatedState? found;
        string status;
        try { found = GameLocator.Find(new ProcessMemorySource(_memory), out status); }
        catch (Exception ex) { Detach("Scan failed: " + ex.Message); return; }

        if (found is null)
        {
            _memory.Dispose();
            _memory = null;
            Status = status;
            return;
        }

        _target = new LiveStateTarget(_memory, found.BlockHost);
        State = new GameState(_target);
        if (!State.Refresh() || !State.LooksValid())
        {
            Detach("Found the game state but could not read a usable copy of it.");
            return;
        }

        IsStale = false;
        IsAttached = true;
        LocateDetail = $"{status} Host address 0x{found.BlockHost:X}.";
        Status = $"Attached to {choice.Name} (pid {choice.Pid}).";
        if (!Files.GameFolder.IsGameFolder(GameFolder)) GuessGameFolderInBackground(choice.Pid);

        SelectedCharacter ??= Characters[0];
        Refresh(force: true);
        SaveEditor.OnAttachStateChanged();
        _timer.Start();
    }

    /// <summary>Tears the session down, leaving <paramref name="reason"/> in the status line.</summary>
    private void Detach(string? reason = null)
    {
        _timer.Stop();
        _target?.Dispose();
        _target = null;
        _memory?.Dispose();
        _memory = null;
        State = null;
        _frozenFood = null;
        _frozenAccount = null;
        _frozenWealth = null;
        IsAttached = false;
        IsStale = false;
        LocateDetail = "";
        Status = reason ?? "Detached.";
        foreach (var c in Characters) c.Reload();
        World.Reload();
        Maps.OnStateRefreshed(force: true);
        SaveEditor.OnAttachStateChanged();
    }

    // ---- polling --------------------------------------------------------------------------------

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

    /// <summary>Re-reads the block, applies freezes against that snapshot, and repaints every editor.</summary>
    public void Refresh(bool force)
    {
        if (State is null) return;
        if (!State.Refresh())
        {
            IsStale = true;
            Status = "Lost contact with the game's memory; press Detach, then Attach again.";
            return;
        }
        if (!State.LooksValid())
        {
            IsStale = true;
            Status = "The located memory no longer looks like Space 1889 (the game may have quit to DOS). Detach and Attach again.";
            return;
        }
        if (IsStale)
        {
            // Whatever made the block stop validating (a quit to DOS, a load) may have replaced the game under us:
            // re-seed the value freezes from the block as it is now rather than writing the old game's values into it.
            _frozenFood = null;
            _frozenAccount = null;
            _frozenWealth = null;
            IsStale = false;
            Status = "Re-synchronised with the game.";
        }

        ApplyFreezes();

        _suppressWriteBack = true;
        try
        {
            foreach (var c in Characters) c.Reload();
            World.Reload();
            Maps.OnStateRefreshed(force);
        }
        finally { _suppressWriteBack = false; }
    }

    public void Report(string message) => Status = message;

    public void Dispose()
    {
        _timer.Stop();
        _target?.Dispose();
        _memory?.Dispose();
    }
}
