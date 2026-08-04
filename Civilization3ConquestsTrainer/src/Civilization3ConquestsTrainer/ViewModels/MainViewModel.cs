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

    private string _status = "Start Civilization III: Conquests, load or begin a game, then Attach and Auto-locate.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand AutoLocateCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand MaxTreasuryCommand { get; }
    public ICommand FreezeTreasuryCommand { get; }
    public ICommand HealAllUnitsCommand { get; }
    public ICommand RefreshAllMovesCommand { get; }
    public ICommand EliteAllUnitsCommand { get; }
    public ICommand MaxCityStoresCommand { get; }
    public ICommand FinishResearchCommand { get; }

    public MainViewModel()
    {
        Map = new MapViewModel(this);

        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        AutoLocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached);
        RescanCommand = new RelayCommand(_ => Rescan(), _ => IsLocated);
        MaxTreasuryCommand = new RelayCommand(_ => ForHuman(p => p.MaxTreasury()), _ => IsLocated);
        FreezeTreasuryCommand = new RelayCommand(_ => ForHuman(p => p.FreezeTreasury = true), _ => IsLocated);
        HealAllUnitsCommand = new RelayCommand(_ => ForMyUnits(u => u.FullHeal()), _ => IsLocated);
        RefreshAllMovesCommand = new RelayCommand(_ => ForMyUnits(u => u.RefreshMoves()), _ => IsLocated);
        EliteAllUnitsCommand = new RelayCommand(_ => ForMyUnits(u => u.MakeElite()), _ => IsLocated);
        MaxCityStoresCommand = new RelayCommand(_ => MaxCityStores(), _ => IsLocated);
        FinishResearchCommand = new RelayCommand(_ => ForHuman(p => p.FinishResearch()), _ => IsLocated);

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
            Status = $"Attached to {SelectedProcess.Name} (pid {_targetPid}). Click Auto-locate.";
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

    private void MaxCityStores()
    {
        if (!CanApplyBulk()) return;
        int n = 0;
        foreach (var c in Cities)
        {
            if (!c.IsMine) continue;
            c.StoredFood = GameFacts.MaxCityStorePreset;
            c.StoredProduction = GameFacts.MaxCityStorePreset;
            n++;
        }
        Status = n == 0
            ? "You have no cities yet — found one first."
            : $"Maxed the food and shield stores of {n} of your cities. They will grow and finish " +
              "whatever they are building on their next turn.";
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
            return;
        }

        foreach (var p in Players) { p.Refresh(_tables); p.ApplyFreeze(); }

        // A row that stops validating means the object behind it is gone — a unit killed, a city
        // captured or razed. Note it and rebuild *after* the loops, never during: Rescan() replaces
        // the collections these foreach statements are walking.
        bool dropped = false;
        foreach (var c in Cities) { if (!c.Refresh(_tables, loc)) { dropped = true; continue; } c.ApplyFreeze(); }
        foreach (var u in Units) { if (!u.Refresh(_tables, loc)) { dropped = true; continue; } u.ApplyFreeze(); }

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
                     FreezeTreasuryCommand, HealAllUnitsCommand, RefreshAllMovesCommand, EliteAllUnitsCommand,
                     MaxCityStoresCommand, FinishResearchCommand,
                 })
            (c as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        Scanner.Dispose();
        _mem?.Dispose();
    }
}
