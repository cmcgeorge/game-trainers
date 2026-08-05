using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using Civilization3ConquestsTrainer.Game;
using Civilization3ConquestsTrainer.Memory;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }

    /// <summary>How well this process's name matches the game.</summary>
    public ProcessMatch Match { get; }

    public bool IsLikelyTarget => Match != ProcessMatch.None;

    public string Display => Match == ProcessMatch.Exact
        ? $"{Name}  (pid {Id})  ← the game"
        : $"{Name}  (pid {Id})";

    public ProcessEntry(int id, string name, ProcessMatch match)
    {
        Id = id; Name = name; Match = match;
    }
}

/// <summary>
/// Root view-model: attaches to <c>Civ3Conquests.exe</c>, runs the one-click locator, and owns the
/// per-tab collections plus the poll/freeze loop.
///
/// Civ III: Conquests is a native 32-bit Windows program with no ASLR, so unlike the repo's DOSBox
/// trainers there is no emulator and no guest-address translation — and unlike its value-scanner
/// trainers there is no scanning either. <see cref="GameLocator"/> adds the recovered RVAs to the
/// module base, proves the result against all 32 leader slots, and everything else hangs off that.
/// The scanner tab stays as the build-independent fallback.
/// </summary>
public sealed class MainViewModel : ObservableObject, IGameHost, IDisposable
{
    private readonly DispatcherTimer _poll;

    private ProcessMemory? _mem;
    private ProcessMemorySource? _source;
    private Civ3Location? _location;
    private GameTables _tables = GameTables.Empty;
    private int _targetPid;
    private bool _multiplayer;
    private bool _suspendRefresh;

    // Container shape as of the last rebuild, so the poll loop can notice units and cities coming and
    // going without the user having to ask.
    private (uint Items, int Last) _unitsShape;
    private (uint Items, int Last) _citiesShape;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<PlayerRowViewModel> Players { get; } = new();
    public ObservableCollection<CityRowViewModel> Cities { get; } = new();
    public ObservableCollection<UnitRowViewModel> Units { get; } = new();

    public MapViewModel Map { get; }
    public LiveScannerViewModel Scanner { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private bool _minesOnly = true;
    /// <summary>Filters the city and unit grids to the human player's own. On by default.</summary>
    public bool MineOnly
    {
        get => _minesOnly;
        set { if (SetField(ref _minesOnly, value)) Rescan(); }
    }

    public bool IsAttached => _mem is { IsOpen: true };
    public bool IsLocated => _location != null;

    private string _sessionSummary = "";
    /// <summary>Turn / player / map line shown next to the toolbar.</summary>
    public string SessionSummary { get => _sessionSummary; private set => SetField(ref _sessionSummary, value); }

    private string _status = "Start Civilization III: Conquests, load or begin a game, then click Attach — " +
                             "the trainer finds the game state by itself.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    private long _maxTreasuryAmount = GameFacts.MaxTreasuryPreset;

    /// <summary>
    /// How much gold "Max treasury" writes. The preset is only the starting offer: a player who wants a
    /// plausible-looking 5,000 rather than a hundred million should be able to say so once and keep
    /// clicking the button. An amount Civ3 cannot hold is refused rather than clamped, so the box never
    /// shows a number the button would not actually write.
    /// </summary>
    public long MaxTreasuryAmount
    {
        get => _maxTreasuryAmount;
        set
        {
            if (!Civ3Layout.IsPlausibleTreasury(value))
            {
                Status = $"{value:N0} is outside the range Civ3 can hold — keeping {_maxTreasuryAmount:N0}.";
                OnPropertyChanged();                    // put the accepted amount back in the box
                return;
            }
            SetField(ref _maxTreasuryAmount, value);
        }
    }

    private bool _holdMyUnitMoves;

    /// <summary>
    /// Re-zeroes spent movement on every one of your units, every poll — the standing version of
    /// "Refresh all moves", which only fires once.
    ///
    /// <para>For workers this is more than convenience. Civ3 spends a worker's entire move when it puts a
    /// turn of work into a job, and the "is this job finished?" test runs <i>only</i> during that work
    /// tick (<c>Unit_work_simple_job</c> @ <c>0x4638C0</c>). One tick per turn is therefore one completion
    /// check per turn, which is why banked work lands next turn rather than immediately. Handing the
    /// movement back lets the job be re-ordered in the same turn, forcing a second tick — and with the
    /// work already banked, that tick finishes the job on the spot.</para>
    /// </summary>
    public bool HoldMyUnitMoves
    {
        get => _holdMyUnitMoves;
        set
        {
            if (value && !WritesAllowed) { ReportBlocked(); OnPropertyChanged(); return; }
            if (!SetField(ref _holdMyUnitMoves, value)) return;
            if (!value) { Status = "Movement hold off — your units spend movement normally again."; return; }

            ApplyMovementHold();
            Status = "Holding your units' spent movement at zero every poll. For a worker this also means " +
                     "you can re-issue its job in the same turn: that forces the game to re-check whether " +
                     "the job is done, which is what makes Finish worker jobs land immediately.";
        }
    }

    /// <summary>Re-zeroes movement on every unit that is still yours. Called from the poll loop.</summary>
    private void ApplyMovementHold()
    {
        if (!_holdMyUnitMoves || !WritesAllowed) return;
        foreach (var u in Units) if (u.IsMine) u.HoldMoves();
    }

    private bool _keepWorkerJobsBanked;

    /// <summary>
    /// Keeps every working unit of yours topped up with enough banked work to finish its current job, so
    /// the one-click action does not have to be repeated for each new job.
    ///
    /// <para>Finishing a job <b>wipes</b> the unit's <c>Job_Value</c> and <c>Job_ID</c> — the game clears
    /// both for every unit on the tile — so banked work never carries into the next job, and without this
    /// each new job needs its own click. With it on, the loop is just "order it, then order it again".</para>
    ///
    /// <para>It costs almost nothing per poll: the row only writes when the banked figure differs from
    /// what the game currently holds, so a worker already topped up is skipped.</para>
    /// </summary>
    public bool KeepWorkerJobsBanked
    {
        get => _keepWorkerJobsBanked;
        set
        {
            if (value && !WritesAllowed) { ReportBlocked(); OnPropertyChanged(); return; }
            if (!SetField(ref _keepWorkerJobsBanked, value)) return;
            if (!value) { Status = "Job banking off — worker jobs progress at their normal rate again."; return; }

            int n = ApplyJobBanking();
            Status = n == 0
                ? "Job banking on. None of your workers is mid-job yet — order one, and it will be banked " +
                  "automatically from the next poll."
                : $"Job banking on, and applied to {n} working " + (n == 1 ? "unit" : "units") + ". Each job " +
                  "still completes on a worker's next turn of work, so with the movement hold ticked you can " +
                  "collect it now by re-issuing the order.";
        }
    }

    /// <summary>Re-banks the current job of every working unit that is still yours; returns how many.</summary>
    private int ApplyJobBanking()
    {
        if (!_keepWorkerJobsBanked || !WritesAllowed) return 0;
        int n = 0;
        foreach (var u in Units) if (u.IsMine && u.FinishJob()) n++;
        return n;
    }

    private bool _instantWorkerJobs;

    /// <summary>
    /// Rewrites every terrain job's cost in the loaded ruleset to a single worker-turn, and puts the
    /// original costs back when switched off.
    ///
    /// <para><b>This one is not yours alone.</b> The job table belongs to the ruleset, not to a player,
    /// so every civ's workers speed up — the same objection that rules out buffing
    /// <c>UnitType.Defence</c> for invincibility. It is a toggle rather than a button for exactly that
    /// reason: the original costs are captured on the way in and restored on the way out, including when
    /// the trainer detaches, so nothing is left patched behind you.</para>
    /// </summary>
    public bool InstantWorkerJobs
    {
        get => _instantWorkerJobs;
        set
        {
            if (value == _instantWorkerJobs) return;
            if (!(value ? EnableInstantWorkerJobs() : RestoreWorkerJobCosts()))
            {
                OnPropertyChanged();                    // snap the checkbox back — the write did not happen
                return;
            }
            SetField(ref _instantWorkerJobs, value);
        }
    }

    /// <summary>The job costs as they were before <see cref="InstantWorkerJobs"/> overwrote them.</summary>
    private int[]? _workerJobCostsBefore;

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand AutoLocateCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand MaxTreasuryCommand { get; }
    public ICommand HealAllUnitsCommand { get; }
    public ICommand RefreshAllMovesCommand { get; }
    public ICommand EliteAllUnitsCommand { get; }
    public ICommand FinishWorkerJobsCommand { get; }
    public ICommand MaxCityFoodCommand { get; }
    public ICommand MaxCityShieldsCommand { get; }
    public ICommand MaxCityCultureCommand { get; }
    public ICommand FinishResearchCommand { get; }
    public ICommand MaxAllCommand { get; }

    public MainViewModel()
    {
        Map = new MapViewModel(this);

        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        AutoLocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached);
        RescanCommand = new RelayCommand(_ => Rescan(), _ => IsLocated);
        MaxTreasuryCommand = new RelayCommand(_ => ForHuman(p => p.MaxTreasury(MaxTreasuryAmount)), _ => IsLocated);
        HealAllUnitsCommand = new RelayCommand(_ => ForMyUnits(u => u.FullHeal()), _ => IsLocated);
        RefreshAllMovesCommand = new RelayCommand(_ => ForMyUnits(u => u.RefreshMoves()), _ => IsLocated);
        EliteAllUnitsCommand = new RelayCommand(_ => ForMyUnits(u => u.MakeElite()), _ => IsLocated);
        FinishWorkerJobsCommand = new RelayCommand(_ => FinishWorkerJobs(), _ => IsLocated);
        MaxCityFoodCommand = new RelayCommand(_ => MaxCityFood(), _ => IsLocated);
        MaxCityShieldsCommand = new RelayCommand(_ => MaxCityShields(), _ => IsLocated);
        MaxCityCultureCommand = new RelayCommand(_ => MaxCityCulture(), _ => IsLocated);
        FinishResearchCommand = new RelayCommand(_ => ForHuman(p => p.FinishResearch()), _ => IsLocated);
        MaxAllCommand = new RelayCommand(_ => MaxAll(), _ => IsLocated);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GameFacts.PollIntervalMs) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
    }

    // --- process management ---------------------------------------------------------------------

    public void RefreshProcesses()
    {
        int? previous = SelectedProcess?.Id;
        int self = Environment.ProcessId;

        Processes.Clear();
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!ProcessPicker.IsSelectable(p.Id, self)) continue;
                string name = p.ProcessName;
                list.Add(new ProcessEntry(p.Id, name, ProcessPicker.Rank(name)));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                /* exited or inaccessible between enumeration and query */
            }
            finally { p.Dispose(); }
        }

        foreach (var e in ProcessPicker.Order(list, e => e.Match, e => e.Name))
            Processes.Add(e);

        SelectedProcess = ProcessPicker.ChooseDefault(Processes, e => e.Match, e => e.Id, previous)
                          ?? Processes.FirstOrDefault(e => e.Id == previous);

        if (SelectedProcess == null)
            Status = $"{GameFacts.ProcessName}.exe is not running. Start Civilization III: Conquests, " +
                     "load or begin a game, then click Refresh.";
    }

    private void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            using var proc = Process.GetProcessById(SelectedProcess.Id);
            var module = proc.MainModule;
            if (module == null)
            {
                Status = "Could not read the target's main module — try running the trainer as administrator.";
                return;
            }

            // ProcessMemory.Open throws (with an admin-rights hint) rather than returning a closed
            // handle, so there is no "failed but non-null" case to check for here.
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            _targetPid = SelectedProcess.Id;
            _source = new ProcessMemorySource(_mem, (nuint)(long)module.BaseAddress, module.ModuleMemorySize);
            Scanner.AttachTo(_mem, _targetPid);

            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            _poll.Start();

            // Locating is part of attaching, not a second step the user has to know about — the button
            // exists only to run it again later. This status is a transient: Locate() overwrites it on
            // both its success and failure paths, and is only visible if it somehow returns early.
            Status = $"Attached to {SelectedProcess.Name} (pid {_targetPid}) — locating…";
            Locate();
        }
        catch (Exception ex)
        {
            // Tear down completely rather than just dropping the field: a half-attached shell would
            // leak the handle and leave the Scanner tab reading and writing through it.
            Teardown();
            Status = "Attach failed: " + ex.Message;
        }
    }

    private void Detach()
    {
        Teardown();
        Status = "Detached. The game keeps running; nothing was left patched.";
    }

    /// <summary>Returns the shell to its detached state. Safe to call from any partially-attached state.</summary>
    private void Teardown()
    {
        // Before anything else, and before _tables is dropped: the job costs are the only thing this
        // trainer leaves changed in the game between clicks, and Detach promises it leaves nothing.
        RestoreWorkerJobCosts();
        if (_instantWorkerJobs) { _instantWorkerJobs = false; OnPropertyChanged(nameof(InstantWorkerJobs)); }

        // The other two leave nothing behind in the game, but they are standing instructions to keep
        // writing — so a fresh attach should start with none of them armed.
        if (_holdMyUnitMoves) { _holdMyUnitMoves = false; OnPropertyChanged(nameof(HoldMyUnitMoves)); }
        if (_keepWorkerJobsBanked) { _keepWorkerJobsBanked = false; OnPropertyChanged(nameof(KeepWorkerJobsBanked)); }

        _poll.Stop();
        Scanner.DetachFrom();
        _mem?.Dispose();
        _mem = null;
        _source = null;
        _location = null;
        _tables = GameTables.Empty;
        _targetPid = 0;
        _multiplayer = false;
        _suspendRefresh = false;
        Players.Clear();
        Cities.Clear();
        Units.Clear();
        Map.Clear();
        Reference.Clear();
        SessionSummary = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(IsLocated));
        RaiseCommands();
    }

    // --- locate ---------------------------------------------------------------------------------

    private void Locate()
    {
        if (_source == null) return;
        var locator = new GameLocator(_source);
        Civ3Location? loc;
        try { loc = locator.Locate(); }
        catch (Exception ex) { Status = "Auto-locate error: " + ex.Message; return; }

        // The multiplayer flags are plain static globals, so they can be read whether or not the
        // locate succeeded — and they must be, because the Scanner tab (the documented fallback for a
        // failed locate) is exactly where an unguarded write would otherwise get through.
        _multiplayer = ReadFlag(Civ3Layout.RvaIsPbemGame) || ReadFlag(Civ3Layout.RvaIsOfflineMpGame);
        Scanner.WritesAllowed = !_multiplayer;

        if (loc == null)
        {
            // Drop every row too: they hold heap pointers into a game that may no longer exist, and
            // the poll loop stops re-validating them once there is no location.
            _location = null;
            _tables = GameTables.Empty;
            Players.Clear();
            Cities.Clear();
            Units.Clear();
            Map.Clear();
            Reference.Clear();
            SessionSummary = "";
            OnPropertyChanged(nameof(IsLocated));
            RaiseCommands();
            Status = locator.LastError +
                     "  Use the Scanner tab as a fallback — but note that a treasury scan cannot work " +
                     "in Civ3 (see the References tab)." +
                     (_multiplayer ? "  Multiplayer session detected — writes are disabled." : "");
            return;
        }

        _location = loc;
        _tables = GameTables.Read(_source, loc);
        Map.Adopt(loc, _tables);
        Reference.Adopt(_tables);

        Rescan();
        OnPropertyChanged(nameof(IsLocated));
        RaiseCommands();

        string build = loc.IsKnownBuild
            ? GameFacts.KnownBuildName
            : $"an unrecognised build (PE timestamp 0x{loc.Pe.TimeDateStamp:X8}) — treat every value with suspicion";
        string chain = loc.Chain == LocateChain.StaticGlobals
            ? "static globals"
            : "a signature scan of the game's own array-walk code";
        Status = $"Located via {chain}: {loc.ValidatedLeaders}/{GameFacts.MaxPlayers} leader slots validated, " +
                 $"playing civ {loc.HumanCivId}. Build: {build}." +
                 (loc.Warning.Length > 0 ? "  " + loc.Warning : "") +
                 (_multiplayer ? "  Multiplayer session detected — writes are disabled." : "");
    }

    private bool ReadFlag(uint rva)
    {
        if (_source == null) return false;
        byte[] b = _source.Read(_source.ModuleBase + (nuint)rva, 1);
        return b.Length == 1 && b[0] != 0;
    }

    /// <summary>Rebuilds the player, city and unit rows. Cities and units come and go every turn.</summary>
    private void Rescan()
    {
        if (_location is not { } loc || _source == null) return;

        // Rebuilding the collections tears down any open cell editor without raising CellEditEnding,
        // so clear the latch here rather than leaving refreshes suspended forever.
        _suspendRefresh = false;

        Players.Clear();
        for (int civ = 0; civ < GameFacts.MaxPlayers; civ++)
        {
            if (!Civ3Layout.IsBitSet(loc.PlayerBits, civ)) continue;
            var row = new PlayerRowViewModel(this, loc.Leader(civ), civ, civ == loc.HumanCivId);
            row.Refresh(_tables);
            Players.Add(row);
        }

        Cities.Clear();
        foreach ((nuint body, int slot) in EnumerateContainer(loc.CitiesContainer))
        {
            var row = new CityRowViewModel(this, body, slot);
            if (!row.Refresh(_tables, loc)) continue;
            if (MineOnly && !row.IsMine) continue;
            Cities.Add(row);
        }

        Units.Clear();
        foreach ((nuint body, int slot) in EnumerateContainer(loc.UnitsContainer))
        {
            var row = new UnitRowViewModel(this, body, slot);
            if (!row.Refresh(_tables, loc)) continue;
            if (MineOnly && !row.IsMine) continue;
            Units.Add(row);
        }

        _unitsShape = ContainerShape(loc.UnitsContainer);
        _citiesShape = ContainerShape(loc.CitiesContainer);
        UpdateSummary();
    }

    /// <summary>
    /// The two header fields that change when a container gains or loses entries: the item array
    /// pointer (which moves when the game grows the array — it went from 100 to 400 slots in one
    /// observed game) and the highest used index.
    /// </summary>
    private (uint Items, int Last) ContainerShape(nuint container)
    {
        if (_source == null) return default;
        byte[] head = _source.Read(container, Civ3Layout.ContainerCapacity + 4);
        if (head.Length < Civ3Layout.ContainerCapacity + 4) return default;
        return (BitConverter.ToUInt32(head, Civ3Layout.ContainerItems),
                BitConverter.ToInt32(head, Civ3Layout.ContainerLastIndex));
    }

    /// <summary>
    /// Walks a Cities/Units container. Entries are <c>{ int, Body* }</c> pairs and a slot can be null
    /// after the object it held was destroyed, so a null is skipped rather than treated as the end.
    /// </summary>
    private IEnumerable<(nuint Body, int Slot)> EnumerateContainer(nuint container)
    {
        if (_source == null) yield break;
        byte[] head = _source.Read(container, Civ3Layout.ContainerCapacity + 4);
        if (head.Length < Civ3Layout.ContainerCapacity + 4) yield break;

        uint items = BitConverter.ToUInt32(head, Civ3Layout.ContainerItems);
        int last = BitConverter.ToInt32(head, Civ3Layout.ContainerLastIndex);
        int capacity = BitConverter.ToInt32(head, Civ3Layout.ContainerCapacity);
        // The header bytes are whatever the target holds, so bound them before they reach an
        // allocation size — an unvalidated `last` would overflow the multiply and throw.
        if (last < 0 || capacity <= 0 || last >= capacity) yield break;
        if (capacity > Civ3Layout.MaxContainerSlots || last >= Civ3Layout.MaxContainerSlots) yield break;
        if (!Civ3Layout.LooksLikeHeapPointer(items)) yield break;

        byte[] table = _source.Read((nuint)items, (last + 1) * Civ3Layout.ItemStride);
        if (table.Length < (last + 1) * Civ3Layout.ItemStride) yield break;

        for (int i = 0; i <= last; i++)
        {
            uint body = BitConverter.ToUInt32(table, i * Civ3Layout.ItemStride + Civ3Layout.ItemBodyPointer);
            if (!Civ3Layout.LooksLikeHeapPointer(body)) continue;
            yield return ((nuint)body, i);
        }
    }

    private void UpdateSummary()
    {
        if (_location is not { } loc) { SessionSummary = ""; return; }
        ReadInt32(loc.Global(Civ3Layout.RvaCurrentTurn), out int turn);
        var me = Players.FirstOrDefault(p => p.IsHuman);
        SessionSummary = $"Turn {turn}   {me?.CivName ?? $"civ {loc.HumanCivId}"}   " +
                         $"map {loc.MapWidth}×{loc.MapHeight}   {Cities.Count} cities, {Units.Count} units shown";
    }

    /// <summary>
    /// Called by the shell while a grid cell is open for editing, so the poll loop stops pushing
    /// fresh values into the bound TextBox underneath the user's cursor.
    /// </summary>
    public void SetEditing(bool editing) => _suspendRefresh = editing;

    // --- bulk actions ---------------------------------------------------------------------------

    /// <summary>
    /// Whether a bulk action can run at all. Checked up front so the action's own "applied to N rows"
    /// message cannot overwrite the row setters' "writes are disabled" report and claim success.
    /// </summary>
    private bool CanApplyBulk()
    {
        if (WritesAllowed) return true;
        ReportBlocked();
        return false;
    }

    private void ForHuman(Action<PlayerRowViewModel> action)
    {
        if (!CanApplyBulk()) return;
        var me = Players.FirstOrDefault(p => p.IsHuman);
        if (me == null) { Status = "No human player row — re-locate."; return; }
        action(me);
        Status = $"Applied to {me.CivName}.";
    }

    // These test ownership unconditionally rather than trusting the MineOnly filter: rows are not
    // re-filtered between re-scans, so a unit or city captured since the last one is still in the
    // grid with IsMine now false, and a bulk action must not reach it.
    private void ForMyUnits(Action<UnitRowViewModel> action)
    {
        if (!CanApplyBulk()) return;
        int n = 0;
        foreach (var u in Units) { if (!u.IsMine) continue; action(u); n++; }
        Status = n == 0 ? "You have no units in the list — click Re-scan." : $"Applied to {n} of your unit(s).";
    }

    /// <summary>
    /// Banks enough worker-turns to complete the current job of every worker of yours that has one.
    ///
    /// <para>Unlike <see cref="InstantWorkerJobs"/> this touches nothing but your own units, so the AI's
    /// workers are unaffected. The improvements appear at the turn boundary rather than immediately —
    /// Civ3 applies accumulated work during the interturn — and workers standing idle are skipped,
    /// because there is no job on them to finish.</para>
    /// </summary>
    private void FinishWorkerJobs()
    {
        if (!CanApplyBulk()) return;

        int mine = 0, working = 0, finished = 0;
        foreach (var u in Units)
        {
            if (!u.IsMine) continue;
            mine++;
            if (u.IsWorking) working++;
            if (u.FinishJob()) finished++;
        }

        if (finished > 0)
        {
            // Be exact about when this lands. Civ3 only tests "is this job done?" while a worker is
            // putting a turn of work in, and that costs the worker its whole move — so one tick per turn
            // means one check per turn, and a job already due next turn cannot get any shorter than that.
            Status = $"Banked enough work to finish {finished} worker " + (finished == 1 ? "job" : "jobs") +
                     ". This lands at the start of your next turn: the game only checks whether a job is " +
                     "done while a worker is working, and working spends its whole move. To collect it now, " +
                     "tick \"Hold my units' moves at 0\" and re-issue the worker's order — that forces the " +
                     "check to run again this turn." +
                     (working > finished
                         ? $"  {working - finished} more could not be costed — the ruleset's job table " +
                           "was not read."
                         : "");
            return;
        }

        // A working unit that could not be finished means the ruleset's job table is missing, not that
        // the worker is idle — saying "nothing was under way" there would be a wrong diagnosis.
        Status = working > 0
            ? "The ruleset's worker-job table could not be read, so there is no cost to bank against. " +
              "Re-locate, or edit the Job done column by hand."
            : mine > 0
                ? $"None of your {mine} unit(s) is mid-job. Set a worker building something first — this " +
                  "finishes work already under way rather than starting it."
                : "You have no units in the list — click Re-scan.";
    }

    // --- worker job costs (ruleset data, shared with every civ) ------------------------------------

    /// <summary>
    /// Overwrites every job's <c>TurnToComplete</c> with one worker-turn, remembering what was there.
    /// Returns false without writing anything if the table was not read or a write is refused, so the
    /// toggle cannot show as on while the game is untouched.
    /// </summary>
    private bool EnableInstantWorkerJobs()
    {
        if (!CanApplyBulk()) return false;
        if (_tables.WorkerJobsTable == 0 || _tables.WorkerJobs.Count == 0)
        {
            Status = "The ruleset's worker-job table could not be read, so its costs cannot be edited. " +
                     "Use Finish worker jobs instead — it works per unit and needs no rules data.";
            return false;
        }

        var before = new int[_tables.WorkerJobs.Count];
        for (int i = 0; i < _tables.WorkerJobs.Count; i++)
        {
            before[i] = _tables.WorkerJobs[i].TurnToComplete;
            if (WriteInt32(WorkerJobCostAddress(i), GameFacts.InstantWorkerJobTurns)) continue;

            // Put back whatever did land before giving up, rather than leaving the table half-rewritten.
            for (int j = 0; j < i; j++) WriteInt32(WorkerJobCostAddress(j), before[j]);
            Status = "Could not write the worker-job costs — nothing was changed.";
            return false;
        }

        _workerJobCostsBefore = before;
        Status = $"All {before.Length} terrain jobs now cost {GameFacts.InstantWorkerJobTurns} worker-turn " +
                 "before terrain. This is ruleset data, so the AI's workers are just as fast — switch it " +
                 "off (or detach) to put the original costs back.";
        return true;
    }

    /// <summary>
    /// Puts the original job costs back. Reports success when there is nothing to restore, so a toggle
    /// switched off after a detach does not fail on the way to the state it is already in.
    /// </summary>
    private bool RestoreWorkerJobCosts()
    {
        if (_workerJobCostsBefore is not { } before) return true;
        if (_tables.WorkerJobsTable == 0) { _workerJobCostsBefore = null; return true; }

        int restored = 0;
        for (int i = 0; i < before.Length && i < _tables.WorkerJobs.Count; i++)
            if (WriteInt32(WorkerJobCostAddress(i), before[i])) restored++;

        _workerJobCostsBefore = null;
        Status = $"Restored the original cost of {restored} terrain " + (restored == 1 ? "job" : "jobs") + ".";
        return true;
    }

    private nuint WorkerJobCostAddress(int jobId)
        => _tables.WorkerJobsTable + (nuint)(jobId * _tables.WorkerJobStride)
           + (nuint)Civ3Layout.WorkerJobTurnToComplete;

    /// <summary>Applies an action to every city that is still yours; returns how many it reached.</summary>
    private int ForMyCities(Action<CityRowViewModel> action)
    {
        int n = 0;
        foreach (var c in Cities) { if (!c.IsMine) continue; action(c); n++; }
        return n;
    }

    private static void FillFood(CityRowViewModel city) => city.StoredFood = GameFacts.MaxCityStorePreset;

    private static void FillShields(CityRowViewModel city) => city.StoredProduction = GameFacts.MaxCityStorePreset;

    // Food and shields are separate actions, and only shields are part of the combined one: a full
    // granary makes a city grow every single turn, and growth outruns happiness — the new citizens
    // arrive discontented and the city can tip into disorder, which produces nothing at all. So food
    // is something you ask for when you want it, city by city or all at once, rather than something a
    // "max everything" button does to you.
    private void MaxCityFood()
    {
        if (!CanApplyBulk()) return;
        int n = ForMyCities(FillFood);
        Status = n == 0
            ? "You have no cities yet — found one first."
            : $"Filled the food store of {n} of your cities. They will grow on their next turn — watch " +
              "happiness, because a city that grows several sizes in a row can riot.";
    }

    private void MaxCityShields()
    {
        if (!CanApplyBulk()) return;
        int n = ForMyCities(FillShields);
        Status = n == 0
            ? "You have no cities yet — found one first."
            : $"Filled the shield store of {n} of your cities. They will finish whatever they are " +
              "building on their next turn.";
    }

    private void MaxCityCulture()
    {
        if (!CanApplyBulk()) return;
        int n = ForMyCities(c => c.CulturalLevel = GameFacts.MaxCityCulturePreset);
        Status = n == 0
            ? "You have no cities yet — found one first."
            : $"Raised {n} of your cities to cultural level {GameFacts.MaxCityCulturePreset}, which is " +
              "what expands their borders. That is the per-city level, not accumulated culture — for a " +
              "cultural victory edit the Culture column on the Players tab. The offset is inferred " +
              "rather than confirmed, so check the effect in game.";
    }

    /// <summary>
    /// Three "max" actions in one click: treasury, research and every city's shield store — the ones
    /// that are almost always wanted together at the start of a session. Food is deliberately left out
    /// (see <see cref="MaxCityFood"/>). Doing the three here rather than chaining the commands keeps a
    /// single, honest status line instead of three that overwrite each other.
    /// </summary>
    private void MaxAll()
    {
        if (!CanApplyBulk()) return;
        var me = Players.FirstOrDefault(p => p.IsHuman);
        if (me == null) { Status = "No human player row — re-locate."; return; }

        me.MaxTreasury(MaxTreasuryAmount);
        me.FinishResearch();
        int n = ForMyCities(FillShields);

        Status = $"{me.CivName}: treasury set to {MaxTreasuryAmount:N0}, research banked (the advance " +
                 $"arrives at a turn boundary, not instantly), shields maxed in {n} " +
                 (n == 1 ? "city" : "cities") + ". Food is left alone — use Max food if you want it.";
    }

    // --- poll loop ------------------------------------------------------------------------------

    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen || HasTargetExited())
        {
            Detach();
            Status = "The game exited (Civ3Restarter relaunches under a new pid — Refresh and re-attach).";
            return;
        }

        Scanner.PollTick();
        if (_location is not { } loc) return;

        // While a grid cell is open for editing, keep applying freezes but do not refresh: a
        // refresh raises PropertyChanged on the bound property, and WPF pushes that straight into
        // the open TextBox, wiping out whatever the user has typed so far.
        if (_suspendRefresh)
        {
            foreach (var p in Players) p.ApplyFreeze();
            foreach (var c in Cities) c.ApplyFreeze();
            foreach (var u in Units) u.ApplyFreeze();
            ApplyJobBanking();
            ApplyMovementHold();
            return;
        }

        foreach (var p in Players) { p.Refresh(_tables); p.ApplyFreeze(); }

        // A row that stops validating means the object behind it is gone — a unit killed, a city
        // captured or razed. Note it and rebuild *after* the loops, never during: Rescan() replaces
        // the collections these foreach statements are walking.
        bool dropped = false;
        foreach (var c in Cities) { if (!c.Refresh(_tables, loc)) { dropped = true; continue; } c.ApplyFreeze(); }

        // The movement hold is applied inside this loop rather than after it, so it reaches only rows
        // that just re-validated — a unit killed this tick has left a dangling body pointer behind, and
        // it stays in the collection until the rebuild below.
        bool holdMoves = _holdMyUnitMoves && WritesAllowed;
        bool bankJobs = _keepWorkerJobsBanked && WritesAllowed;
        foreach (var u in Units)
        {
            if (!u.Refresh(_tables, loc)) { dropped = true; continue; }
            u.ApplyFreeze();
            if (!u.IsMine) continue;
            // Bank first, hold second: banking is what the next work tick consumes, and the returned
            // movement is what lets that tick happen this turn rather than the next.
            if (bankJobs) u.FinishJob();
            if (holdMoves) u.HoldMoves();
        }

        // Gains show up as a changed container shape; losses show up as a dropped row (a unit dying
        // mid-array nulls its slot without moving LastIndex, so the shape alone would miss it).
        bool grew = ContainerShape(loc.UnitsContainer) != _unitsShape
                    || ContainerShape(loc.CitiesContainer) != _citiesShape;

        if (grew || dropped)
        {
            int unitsBefore = Units.Count, citiesBefore = Cities.Count;
            Rescan();
            if (Units.Count != unitsBefore || Cities.Count != citiesBefore)
                Status = $"Rebuilt: {Cities.Count} cities, {Units.Count} units " +
                         (MineOnly ? "(yours)" : "(all civs)") + ".";
            return;
        }

        UpdateSummary();
    }

    private bool HasTargetExited()
    {
        if (_targetPid == 0) return false;
        try
        {
            using var p = Process.GetProcessById(_targetPid);
            return p.HasExited;
        }
        catch (ArgumentException) { return true; }
    }

    // --- IGameHost -------------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool WritesAllowed => _mem is { IsOpen: true } && !_multiplayer;

    /// <inheritdoc/>
    public byte[] Read(nuint address, int count)
        => _mem is { IsOpen: true } ? _mem.Read(address, count) : Array.Empty<byte>();

    // Every read and write allocates its own four-byte buffer rather than sharing one field. The
    // map sweep runs on a background thread while the poll loop keeps ticking on the UI thread, and
    // a shared buffer would let one of them write the other's bytes to the other's address.

    /// <inheritdoc/>
    public bool ReadInt32(nuint address, out int value)
    {
        value = 0;
        if (_mem is not { IsOpen: true }) return false;
        byte[] buf = new byte[4];
        if (_mem.Read(address, buf, 4) < 4) return false;
        value = BitConverter.ToInt32(buf);
        return true;
    }

    /// <inheritdoc/>
    public bool WriteInt32(nuint address, int value)
    {
        var mem = _mem;
        if (!WritesAllowed || mem is null) { ReportBlocked(); return false; }
        byte[] buf = new byte[4];
        BitConverter.TryWriteBytes(buf, value);
        return mem.WriteRange(address, buf, 0, 4);
    }

    /// <inheritdoc/>
    public void Report(string message) => Status = message;

    private void ReportBlocked()
        => Status = _multiplayer
            ? "Writes are disabled: this is a multiplayer or play-by-email game, and editing one " +
              "side of a shared game desynchronises it."
            : "Writes are disabled — the trainer is not attached to a game.";

    // --- plumbing --------------------------------------------------------------------------------

    private void RaiseCommands()
    {
        foreach (var c in new[]
                 {
                     AttachCommand, DetachCommand, AutoLocateCommand, RescanCommand, MaxTreasuryCommand,
                     HealAllUnitsCommand, RefreshAllMovesCommand, EliteAllUnitsCommand,
                     FinishWorkerJobsCommand,
                     MaxCityFoodCommand, MaxCityShieldsCommand, MaxCityCultureCommand,
                     FinishResearchCommand, MaxAllCommand,
                 })
            (c as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        // Closing the trainer has to un-patch the ruleset just as Detach does — the game keeps running
        // after the window shuts, and a jobs table left at 1 would silently outlive the trainer.
        RestoreWorkerJobCosts();
        _poll.Stop();
        Scanner.Dispose();
        _mem?.Dispose();
    }
}
