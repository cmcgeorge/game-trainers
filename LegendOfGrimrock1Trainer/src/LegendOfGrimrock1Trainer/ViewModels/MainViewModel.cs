using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using LegendOfGrimrock1Trainer.Game;
using LegendOfGrimrock1Trainer.Lua;
using LegendOfGrimrock1Trainer.Memory;

namespace LegendOfGrimrock1Trainer.ViewModels;

/// <summary>
/// The trainer session: pick a process, attach, locate the Lua VM, then keep the bound values in
/// step with the game.
///
/// Attaching and locating are one step. There is no scan phase for the user to run, no address to
/// paste and no value to search for — <see cref="GameLocator"/> either finds LuaJIT's main thread or
/// says why it could not, and everything else is a table lookup by name from there. Refreshing is
/// pull-based at <see cref="RefreshMilliseconds"/> and re-resolves every slot it writes to, so a
/// table that rehashed between ticks cannot send a write to a stale address.
/// </summary>
public sealed class MainViewModel : ObservableObject, IGameHost, IDisposable
{
    /// <summary>How often bound values are re-read from the game.</summary>
    public const int RefreshMilliseconds = 250;

    private readonly DispatcherTimer _timer;
    private readonly int _ownProcessId = Environment.ProcessId;

    private ProcessMemory? _memory;
    private ProcessMemorySource? _source;
    private LuaHeap? _heap;
    private PartyReader? _reader;
    private TrainerActions? _actions;
    private LocateResult? _located;
    private Process? _target;
    private bool _refreshing;

    /// <summary>Builds the session and starts the refresh timer.</summary>
    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach, () => SelectedProcess is not null && !IsAttached);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        RelocateCommand = new RelayCommand(Relocate, () => IsAttached);

        RestorePartyCommand = new RelayCommand(() => ForParty(a => a.Restore, "restored the party"));
        FeedPartyCommand = new RelayCommand(() => ForParty(a => a.Feed, "fed the party"));
        CurePartyCommand = new RelayCommand(() => ForParty(a => a.Cure, "cured the party"));
        BlessPartyCommand = new RelayCommand(() => ForParty(a => c => a.Bless(c, BlessSeconds), $"blessed the party"));
        MaxPartyCommand = new RelayCommand(() => ForParty(a => c => a.MaxStats(c, MaxStatTarget), "maxed the party"));
        GiveSkillPointsCommand = new RelayCommand(() => ForParty(a => c => a.SetSkillPoints(c, SkillPointGrant), "granted skill points"));

        TeleportCommand = new RelayCommand(Teleport, () => HasGame);
        FaceCommand = new RelayCommand(p => SetFacing(p), _ => HasGame);
        RevealLevelCommand = new RelayCommand(RevealLevel, () => HasGame);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        RefreshProcesses();
        Status = "Start Legend of Grimrock, load or begin a game, then pick grimrock and press Attach.";
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process is the game. Stays a no-op (just the populated process list) when the game is not running, rather than attaching to some unrelated process and scanning it fruitlessly.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.Match == ProcessMatch.Exact) Attach();
    }

    // --- attach ------------------------------------------------------------------------------------

    /// <summary>Attachable processes, best matches first.</summary>
    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    private ProcessEntry? _selectedProcess;
    /// <summary>The process Attach will open.</summary>
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { if (SetField(ref _selectedProcess, value)) RaiseCommandStates(); }
    }

    private bool _isAttached;
    /// <summary>Whether a process is open.</summary>
    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (!SetField(ref _isAttached, value)) return;
            OnPropertyChanged(nameof(HasGame));
            RaiseCommandStates();
        }
    }

    private bool _hasGame;
    /// <summary>Whether a dungeon is actually loaded — false at the main menu.</summary>
    public bool HasGame
    {
        get => _hasGame && _isAttached;
        private set
        {
            if (!SetField(ref _hasGame, value)) return;
            RaiseCommandStates();
        }
    }

    private bool _writesAllowed = true;
    /// <summary>Master switch for every edit. Off makes the trainer read-only.</summary>
    public bool WritesAllowed { get => _writesAllowed; set => SetField(ref _writesAllowed, value); }

    private Func<bool>? _editorProbe;

    /// <inheritdoc/>
    public bool EditorHasFocus => _editorProbe?.Invoke() ?? false;

    /// <summary>
    /// Supplies the window's answer to "is a text editor being typed into right now?".
    ///
    /// A <i>probe</i> rather than a tracked flag, because both ways of tracking it are wrong. Setting
    /// a flag from GotKeyboardFocus/LostKeyboardFocus latches on forever if the focused editor is
    /// destroyed rather than blurred — which is exactly what a champion-list rebuild does — and
    /// clearing it when keyboard focus leaves the application discards a half-typed value the moment
    /// the user alt-tabs away, because a <c>LostFocus</c> binding has not committed it yet. Asking the
    /// window for its <i>logical</i> focus each time answers both correctly: logical focus survives
    /// deactivation and cannot outlive the element that holds it.
    /// </summary>
    public void SetEditorProbe(Func<bool>? probe) => _editorProbe = probe;

    private string _status = "";
    /// <summary>The status bar.</summary>
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private string _sessionSummary = "Not attached.";
    /// <summary>One line describing the located VM and the loaded game.</summary>
    public string SessionSummary { get => _sessionSummary; private set => SetField(ref _sessionSummary, value); }

    private string _locatorDetail = "";
    /// <summary>How the VM was found, and how long it took.</summary>
    public string LocatorDetail { get => _locatorDetail; private set => SetField(ref _locatorDetail, value); }

    /// <summary>Re-lists attachable processes.</summary>
    public RelayCommand RefreshProcessesCommand { get; }

    /// <summary>Opens the selected process and locates the VM.</summary>
    public RelayCommand AttachCommand { get; }

    /// <summary>Closes the process handle.</summary>
    public RelayCommand DetachCommand { get; }

    /// <summary>Runs the locator again without detaching.</summary>
    public RelayCommand RelocateCommand { get; }

    private void RaiseCommandStates()
    {
        AttachCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        RelocateCommand.RaiseCanExecuteChanged();
        TeleportCommand.RaiseCanExecuteChanged();
        FaceCommand.RaiseCanExecuteChanged();
        RevealLevelCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Rebuilds the process list, keeping the current selection when it survives.</summary>
    public void RefreshProcesses()
    {
        int? previous = SelectedProcess?.Id;
        var entries = new List<ProcessEntry>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!ProcessPicker.IsSelectable(p.Id, _ownProcessId)) continue;
                entries.Add(new ProcessEntry(p.Id, p.ProcessName, p.MainWindowTitle));
            }
            catch (InvalidOperationException) { /* exited between enumeration and read */ }
            finally { p.Dispose(); }
        }

        var ordered = ProcessPicker.Order(entries, e => e.Match, e => e.Name).ToList();

        Processes.Clear();
        foreach (var e in ordered) Processes.Add(e);

        SelectedProcess = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, previous);
    }

    /// <summary>Opens the selected process, then locates and validates the Lua VM.</summary>
    public void Attach()
    {
        if (SelectedProcess is not { } entry) return;
        Detach();

        try
        {
            _target = Process.GetProcessById(entry.Id);
            var module = _target.MainModule
                ?? throw new InvalidOperationException("the process has no main module.");

            long moduleBase = module.BaseAddress.ToInt64();
            if (moduleBase is <= 0 or > uint.MaxValue)
                throw new InvalidOperationException(
                    "the module is mapped above 4 GB, so this is a 64-bit process — Legend of Grimrock 1 is 32-bit.");

            _memory = ProcessMemory.Open(entry.Id);
            _source = new ProcessMemorySource(_memory, (uint)moduleBase, module.ModuleMemorySize);
            _heap = new LuaHeap(_source);
            _reader = new PartyReader(_heap);
            _actions = new TrainerActions(_reader);
            IsAttached = true;

            Relocate();
        }
        catch (Exception ex)
        {
            Detach();
            Status = $"Attach failed: {ex.Message}";
        }
    }

    /// <summary>Runs both locator chains against the attached process.</summary>
    public void Relocate()
    {
        if (_source is null || _heap is null) return;

        _heap.ResetCache();
        var locator = new GameLocator(_source, _heap);
        _located = locator.Locate();

        if (!_located.Found)
        {
            HasGame = false;
            LocatorDetail = _located.Detail;
            Status = $"No Lua VM found in the attached process ({_located.RegionsScanned} regions, " +
                     $"{_located.BytesScanned / (1024 * 1024)} MB swept in {_located.ElapsedMs:0} ms). " +
                     "Is that really grimrock.exe?";
            return;
        }

        string chain = _located.Chain == LocateChain.StaticPointer ? "static pointer" : "heap signature";
        LocatorDetail = $"{chain}: {_located.Detail}; lua_State 0x{_located.LuaState:X8}, " +
                        $"_G 0x{_located.Globals:X8}; module 0x{_located.ModuleBase:X8}; {_located.ElapsedMs:0.0} ms" +
                        (_located.Chain == LocateChain.HeapSignature
                            ? $"; {_located.RegionsScanned} regions, {_located.BytesScanned / (1024 * 1024)} MB"
                            : "") +
                        $"; {DescribeBuild()}";

        Refresh(force: true);
        Status = HasGame
            ? $"Located the Lua VM via the {chain} and read the party. No value searching was needed."
            : $"Located the Lua VM via the {chain}. No dungeon is loaded yet — start or load a game and " +
              "the party appears on its own; nothing else to press.";
    }

    /// <summary>
    /// Describes the build actually attached to, so a mismatch is visible rather than implied.
    ///
    /// The version comes from the game's own Lua global <c>config.gameVersion</c> when it can be
    /// read, which is a far better answer than the PE timestamp: it is what the game itself believes
    /// it is. The timestamp is the fallback and the tie-breaker.
    /// </summary>
    private string DescribeBuild()
    {
        string version = _heap is not null && _located is { Found: true } located
            ? _heap.StringOf(_heap.GetPath(located.Globals, GrimrockLayout.ConfigKey, GameVersionKey)) ?? "unknown"
            : "unknown";

        if (version == GameFacts.KnownGameVersion && (_located?.BuildMatches ?? false))
            return $"build {version} (matches the one these offsets were taken against)";

        string stamp = _located?.Image is { } image ? $"0x{image.TimeDateStamp:X8}" : "unreadable";
        return $"build {version}, PE stamp {stamp} — NOT the {GameFacts.KnownBuildName} these notes " +
               "were taken against, so treat the numbers with suspicion";
    }

    /// <summary>Key of the version string inside the game's <c>config</c> table.</summary>
    private const string GameVersionKey = "gameVersion";

    /// <summary>Closes the handle and clears every derived value.</summary>
    public void Detach()
    {
        _memory?.Dispose();
        _memory = null;
        _source = null;
        _heap = null;
        _reader = null;
        _actions = null;
        _located = null;
        _target?.Dispose();
        _target = null;

        IsAttached = false;
        HasGame = false;
        Champions.Clear();
        Statistics.Clear();
        Maps.Clear();
        SessionSummary = "Not attached.";
        LocatorDetail = "";
    }

    // --- party -------------------------------------------------------------------------------------

    /// <summary>The four champion slots.</summary>
    public ObservableCollection<ChampionViewModel> Champions { get; } = new();

    private int _selectedChampion;
    /// <summary>
    /// Index of the champion tab on screen. Bound so that rebuilding the collection — which a single
    /// tick where one champion failed to parse is enough to trigger — does not snap the user back to
    /// the first champion.
    /// </summary>
    public int SelectedChampion
    {
        get => _selectedChampion;
        set => SetField(ref _selectedChampion, value);
    }

    /// <summary>Run statistics.</summary>
    public ObservableCollection<StatisticRowViewModel> Statistics { get; } = new();

    /// <summary>Levels of the loaded dungeon.</summary>
    public ObservableCollection<MapRowViewModel> Maps { get; } = new();

    public CluebookViewModel Cluebook { get; } = new();

    private string _partyPosition = "";
    /// <summary>Where the party is standing, for the header.</summary>
    public string PartyPosition { get => _partyPosition; private set => SetField(ref _partyPosition, value); }

    private int _teleportX;
    /// <summary>Destination tile X for a teleport.</summary>
    public int TeleportX { get => _teleportX; set => SetField(ref _teleportX, value); }

    private int _teleportY;
    /// <summary>Destination tile Y for a teleport.</summary>
    public int TeleportY { get => _teleportY; set => SetField(ref _teleportY, value); }

    private double _blessSeconds = 300;
    /// <summary>How long the party-wide bless lasts, in seconds.</summary>
    public double BlessSeconds
    {
        get => _blessSeconds;
        set => SetField(ref _blessSeconds, Math.Clamp(value, 1, ConditionRowViewModel.MaxTimer));
    }

    private double _maxStatTarget = 100;
    /// <summary>Value the "max the party" button raises every stat to.</summary>
    public double MaxStatTarget
    {
        get => _maxStatTarget;
        set => SetField(ref _maxStatTarget, Math.Clamp(value, 1, GameFacts.MaxStatValue));
    }

    private int _skillPointGrant = 20;
    /// <summary>How many unspent skill points the grant button hands each champion.</summary>
    public int SkillPointGrant
    {
        get => _skillPointGrant;
        set => SetField(ref _skillPointGrant, Math.Clamp(value, 0, 999));
    }

    /// <summary>Restores health and energy for every living champion.</summary>
    public RelayCommand RestorePartyCommand { get; }

    /// <summary>Fills every living champion's food bar.</summary>
    public RelayCommand FeedPartyCommand { get; }

    /// <summary>Clears every harmful condition from the party.</summary>
    public RelayCommand CurePartyCommand { get; }

    /// <summary>Sets every beneficial condition on the party for <see cref="BlessSeconds"/>.</summary>
    public RelayCommand BlessPartyCommand { get; }

    /// <summary>Raises every stat below <see cref="MaxStatTarget"/> to it.</summary>
    public RelayCommand MaxPartyCommand { get; }

    /// <summary>Gives every champion <see cref="SkillPointGrant"/> unspent skill points.</summary>
    public RelayCommand GiveSkillPointsCommand { get; }

    /// <summary>Moves the party to <see cref="TeleportX"/>, <see cref="TeleportY"/> on its current level.</summary>
    public RelayCommand TeleportCommand { get; }

    /// <summary>Turns the party; the command parameter is the compass index 0..3.</summary>
    public RelayCommand FaceCommand { get; }

    /// <summary>Fills in the automap for the level the party is on.</summary>
    public RelayCommand RevealLevelCommand { get; }

    // --- refresh -----------------------------------------------------------------------------------

    /// <inheritdoc/>
    public TrainerActions? Actions => _actions;

    /// <inheritdoc/>
    public void Report(string message) => Status = message;

    /// <inheritdoc/>
    public void RequestRefresh() => Refresh(force: true);

    /// <inheritdoc/>
    public PartySnapshot? ResolveParty()
    {
        if (_reader is null || _located is not { Found: true } located) return null;
        return _reader.ReadParty(located.Globals);
    }

    /// <inheritdoc/>
    public ChampionSnapshot? ResolveChampion(int index) =>
        ResolveParty()?.Champions.FirstOrDefault(c => c.Index == index);

    /// <summary>
    /// The timer's entry point. A throw here would otherwise reach WPF's dispatcher and tear the
    /// window down mid-session — and the plausible throws are all environmental (the game exiting
    /// between the <c>HasExited</c> check and the read, a handle query refused by security software),
    /// not bugs worth crashing over. Detach and say so instead.
    /// </summary>
    private void Tick()
    {
        try
        {
            Refresh();
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                      or System.ComponentModel.Win32Exception
                                      or ObjectDisposedException)
        {
            // Only the environmental races are swallowed — the game exiting between the HasExited
            // check and the read, a handle query refused by security software. A defect in the reader
            // is left to reach the dispatcher handler, where it is reported as what it is rather than
            // misattributed to the game and used to tear down a working session.
            Detach();
            Status = $"Lost the game process: {ex.Message}";
        }
    }

    /// <summary>Re-reads the game and pushes the result into every bound value.</summary>
    public void Refresh(bool force = false)
    {
        if (_refreshing) return;
        if (!IsAttached) return;
        if (_target is { HasExited: true })
        {
            Detach();
            Status = "The game exited.";
            return;
        }
        if (_located is not { Found: true }) return;

        _refreshing = true;
        try
        {
            var party = ResolveParty();
            if (party is null)
            {
                if (HasGame || force)
                {
                    HasGame = false;
                    Champions.Clear();
                    Statistics.Clear();
                    Maps.Clear();
                    SessionSummary = "Attached; no dungeon loaded (main menu or character creation).";
                }
                return;
            }

            ApplyFreezes(party);
            SyncChampions(party);
            SyncStatistics(party);
            SyncMaps(party);

            var map = party.CurrentMap;
            string facing = party.Facing is >= 0 and < 4 ? GameTables.FacingNames[party.Facing] : $"{party.Facing}";
            PartyPosition = map is null
                ? $"Level {party.Level} — tile ({party.X}, {party.Y}) facing {facing}"
                : $"Level {party.Level}: {map.Name} — tile ({party.X}, {party.Y}) of {map.Width}x{map.Height}, facing {facing}";

            int living = party.Champions.Count(c => c.Enabled);
            SessionSummary = $"{PartyPosition}   |   {living}/{party.Champions.Count} champions standing";

            if (!HasGame)
            {
                HasGame = true;
                TeleportX = party.X;
                TeleportY = party.Y;
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Re-applies every frozen stat. The value is re-written each tick against a freshly resolved
    /// slot rather than replayed into a cached address, so a freeze survives anything that moves the
    /// table it lives in.
    /// </summary>
    private void ApplyFreezes(PartySnapshot party)
    {
        if (_actions is null || !WritesAllowed) return;
        foreach (var championVm in Champions)
            FreezeWriter.Apply(_actions, party, championVm.Index, championVm.FrozenStats);
    }

    private void SyncChampions(PartySnapshot party)
    {
        bool sameShape = Champions.Count == party.Champions.Count;
        if (sameShape)
        {
            for (int i = 0; i < Champions.Count; i++)
            {
                if (Champions[i].Index == party.Champions[i].Index) continue;
                sameShape = false;
                break;
            }
        }

        if (!sameShape)
        {
            // A rebuild is triggered by any tick where a champion momentarily failed to parse, not
            // only by the party really changing, so both the freezes and the tab the user is looking
            // at are carried across rather than silently resetting.
            var frozen = Champions.ToDictionary(c => c.Index, c => c.FrozenStats.ToList());
            int wasSelected = SelectedChampion >= 0 && SelectedChampion < Champions.Count
                ? Champions[SelectedChampion].Index
                : -1;

            Champions.Clear();
            foreach (var c in party.Champions)
            {
                var vm = new ChampionViewModel(this, c);
                if (frozen.TryGetValue(c.Index, out var carried)) vm.RestoreFreezes(carried);
                Champions.Add(vm);
            }

            int restored = Champions.ToList().FindIndex(c => c.Index == wasSelected);
            SelectedChampion = restored >= 0 ? restored : 0;
            return;
        }

        for (int i = 0; i < Champions.Count; i++) Champions[i].Update(party.Champions[i]);
    }

    private void SyncStatistics(PartySnapshot party)
    {
        if (Statistics.Count != party.Statistics.Count)
        {
            Statistics.Clear();
            foreach (var (_, uiName, value, _) in party.Statistics)
                Statistics.Add(new StatisticRowViewModel(uiName, value));
            return;
        }

        for (int i = 0; i < Statistics.Count; i++) Statistics[i].Update(party.Statistics[i].Value);
    }

    private void SyncMaps(PartySnapshot party)
    {
        if (Maps.Count != party.Maps.Count)
        {
            Maps.Clear();
            foreach (var m in party.Maps) Maps.Add(new MapRowViewModel(m));
        }

        for (int i = 0; i < Maps.Count && i < party.Maps.Count; i++)
            Maps[i].Update(party.Maps[i], party.Level);
    }

    // --- party-wide actions -------------------------------------------------------------------------

    private void ForParty(Func<TrainerActions, Func<ChampionSnapshot, ActionResult>> pick, string label)
    {
        if (!WritesAllowed) { Status = "Writes are disabled."; return; }
        if (_actions is null) { Status = "Not attached."; return; }
        var party = ResolveParty();
        if (party is null) { Status = "No dungeon is loaded."; return; }

        var result = _actions.ForEachChampion(party, pick(_actions), label);
        Status = result.Attempted == 0 ? result.Summary : $"{result.Summary} ({result.Applied}/{result.Attempted} written)";
        Refresh(force: true);
    }

    private void Teleport()
    {
        if (!WritesAllowed) { Status = "Writes are disabled."; return; }
        if (_actions is null) { Status = "Not attached."; return; }
        var party = ResolveParty();
        var map = party?.CurrentMap;
        if (party is null || map is null) { Status = "No dungeon is loaded."; return; }

        var result = _actions.Teleport(party, map, TeleportX, TeleportY);
        Status = result.Attempted == 0 ? result.Summary : $"{result.Summary} ({result.Applied}/{result.Attempted} written)";
        Refresh(force: true);
    }

    private void SetFacing(object? parameter)
    {
        if (!WritesAllowed) { Status = "Writes are disabled."; return; }
        if (_actions is null) { Status = "Not attached."; return; }
        var party = ResolveParty();
        if (party is null) { Status = "No dungeon is loaded."; return; }
        if (parameter is null || !int.TryParse(parameter.ToString(), out int facing)) return;

        var result = _actions.SetFacing(party, facing);
        Status = result.Summary;
        Refresh(force: true);
    }

    private void RevealLevel()
    {
        if (!WritesAllowed) { Status = "Writes are disabled."; return; }
        if (_actions is null) { Status = "Not attached."; return; }
        var party = ResolveParty();
        var map = party?.CurrentMap;
        if (map is null) { Status = "No dungeon is loaded."; return; }

        var result = _actions.RevealMap(map);
        Status = result.Attempted == 0 ? result.Summary : $"{result.Summary} ({result.Applied}/{result.Attempted} tiles)";
        Refresh(force: true);
    }

    /// <summary>Stops the timer and releases the process handle.</summary>
    public void Dispose()
    {
        _timer.Stop();
        Detach();
    }
}
