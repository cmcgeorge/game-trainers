using System.Collections.ObjectModel;
using System.Windows.Input;
using CurseOfTheAzureBondsTrainer.Game;
using CurseOfTheAzureBondsTrainer.Memory;
using CurseOfTheAzureBondsTrainer.Mvvm;

namespace CurseOfTheAzureBondsTrainer.ViewModels;

/// <summary>One drawable map square projected for the schematic: its grid position and terrain.
/// East/South are only set on the map's outer edge — every interior edge is drawn once, as the
/// neighbouring square's West/North.</summary>
public sealed record TerrainCell(int X, int Y, FloorKind Floor, WallKind West, WallKind North,
    WallKind East, WallKind South);

/// <summary>
/// Backs the 🗺 Maps tab: an offline area/location reference plus a live "where am I / teleport
/// me there" helper. The party's map X/Y is not in the character record and its address changes
/// every DOSBox session, so it is found by a Snapshot + Narrow loop: Snapshot collects every
/// address that could hold the current (X, Y); the user walks to a different square, updates the
/// coordinates, and clicks Narrow to drop every candidate that no longer matches — repeating until
/// a single address remains (the position lock). Teleport writes the target back through it.
///
/// <para><b>Which level am I on?</b> Curse's sixteen levels are all 16×16, and the position lock
/// gives coordinates without saying which of the sixteen they are coordinates *in* — so a marker
/// painted on whichever map happens to be selected would be a guess. <see cref="Identify"/> answers
/// it properly: the game holds the current level's 512-byte wall array in memory verbatim, so
/// <see cref="MapLocator"/> reads the levels back out of the game's own <c>GEO*.DAX</c> archives and
/// looks for one of them resident. A 512-byte exact match is not something a wrong answer produces.
/// Until a level has been identified the marker is drawn on the selected map with the status line
/// saying it is unconfirmed; once identified it is drawn only on that map.</para>
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private ProcessMemory? _mem;

    private List<PositionCandidate> _candidates = new();
    private PositionCandidate? _locked;
    private bool _isScanning;
    private CancellationTokenSource? _scanCts;

    // The locked address is stable for the whole DOSBox session. A failed read is almost always
    // the game rewriting the position block mid map-load. Ride out this many consecutive bad reads
    // before surrendering the lock so crossing between areas does not force a re-locate.
    private const int MaxStaleReads = 5;
    private int _staleReads;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLocation> Locations { get; } = new();
    public ObservableCollection<TerrainCell> Terrain { get; } = new();

    public MapsViewModel()
    {
        SnapshotCommand       = new RelayCommand(_ => Snapshot(),      _ => IsAttached && !_isScanning);
        NarrowCommand         = new RelayCommand(_ => NarrowStep(),    _ => IsAttached && !_isScanning && _candidates.Count > 0);
        ResetCommand          = new RelayCommand(_ => ResetSearch(),   _ => !_isScanning && (_candidates.Count > 0 || _locked != null));
        TeleportCommand       = new RelayCommand(_ => Teleport(),      _ => CanTeleport());
        IdentifyCommand       = new RelayCommand(_ => Identify(),      _ => CanIdentify());
        SelectLocationCommand = new RelayCommand(p => { if (p is MapLocation l) SelectedLocation = l; });
        SelectedArea = Areas.FirstOrDefault();
    }

    private bool IsAttached => _mem is { IsOpen: true };

    // --- area / location selection -------------------------------------------
    private MapArea? _selectedArea;
    public MapArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (!SetProperty(ref _selectedArea, value)) return;
            Locations.Clear();
            if (value != null) foreach (var l in value.Locations) Locations.Add(l);
            SelectedLocation = Locations.FirstOrDefault();
            RebuildTerrain(value);
            OnPropertyChanged(nameof(GridWidth));
            OnPropertyChanged(nameof(GridHeight));
            OnPropertyChanged(nameof(ShowLiveMarker));
            OnPropertyChanged(nameof(ShowFacingArrow));
            OnPropertyChanged(nameof(ShowLiveDot));
        }
    }

    private void RebuildTerrain(MapArea? area)
    {
        Terrain.Clear();
        if (area?.Terrain == null) return;
        var t = area.Terrain;
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                Terrain.Add(new TerrainCell(x, y, t[x, y].Floor, t[x, y].West, t[x, y].North,
                                            t[x, y].East, t[x, y].South));
    }

    public int GridWidth  => _selectedArea?.GridWidth  ?? 1;
    public int GridHeight => _selectedArea?.GridHeight ?? 1;

    private MapLocation? _selectedLocation;
    public MapLocation? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (!SetProperty(ref _selectedLocation, value)) return;
            if (value != null) { TargetX = value.X; TargetY = value.Y; }
        }
    }

    // --- coordinate inputs ---------------------------------------------------
    // CurrentX / CurrentY: the player reads these from the game's own display and types them in
    // before clicking Snapshot (first time) or Narrow (after each move).
    private int _currentX;
    public int CurrentX { get => _currentX; set { if (SetProperty(ref _currentX, Math.Clamp(value, 0, 255))) RaiseSearchCommands(); } }

    private int _currentY;
    public int CurrentY { get => _currentY; set { if (SetProperty(ref _currentY, Math.Clamp(value, 0, 255))) RaiseSearchCommands(); } }

    // --- teleport target -----------------------------------------------------
    private int _targetX;
    public int TargetX { get => _targetX; set => SetProperty(ref _targetX, Math.Clamp(value, 0, 255)); }

    private int _targetY;
    public int TargetY { get => _targetY; set => SetProperty(ref _targetY, Math.Clamp(value, 0, 255)); }

    // --- live position (drives the facing arrow once locked) -----------------
    private int _liveX;
    public int LiveX { get => _liveX; private set => SetProperty(ref _liveX, value); }

    private int _liveY;
    public int LiveY { get => _liveY; private set => SetProperty(ref _liveY, value); }

    private int _liveFacing;
    /// <summary>Gold Box facing: 0=N 1=E 2=S 3=W. Only meaningful when <see cref="HasFacing"/>.</summary>
    public int LiveFacing { get => _liveFacing; private set => SetProperty(ref _liveFacing, value); }

    /// <summary>Rotation angle in degrees for a north-pointing arrow: N=0, E=90, S=180, W=270.</summary>
    public double LiveFacingDegrees => _liveFacing * 90.0;

    private bool _hasFacing;
    /// <summary>
    /// False on the overland map: the wilderness position words have no facing byte beside them, so
    /// the marker is drawn as a plain dot rather than an arrow that would be pointing at a guess.
    /// It is only known after the first live read, which is why the two marker flags hang off it.
    /// </summary>
    public bool HasFacing
    {
        get => _hasFacing;
        private set
        {
            if (!SetProperty(ref _hasFacing, value)) return;
            OnPropertyChanged(nameof(ShowFacingArrow));
            OnPropertyChanged(nameof(ShowLiveDot));
        }
    }

    // --- lock / search state -------------------------------------------------
    public bool HasLock => _locked != null;

    /// <summary>The level the game currently has loaded, once <see cref="Identify"/> has matched
    /// its resident wall data; null while unknown.</summary>
    private MapArea? _identifiedArea;
    public MapArea? IdentifiedArea
    {
        get => _identifiedArea;
        private set
        {
            if (!SetProperty(ref _identifiedArea, value)) return;
            OnPropertyChanged(nameof(IdentifiedName));
            OnPropertyChanged(nameof(ShowLiveMarker));
            OnPropertyChanged(nameof(ShowFacingArrow));
            OnPropertyChanged(nameof(ShowLiveDot));
        }
    }

    public string IdentifiedName => _identifiedArea?.Name ?? "not identified";

    /// <summary>
    /// Whether the live-position marker belongs on the map currently being viewed. Every level in
    /// Curse is 16×16, so coordinates alone cannot say which one you are on; the marker is therefore
    /// pinned to the identified level once there is one, and otherwise drawn wherever you are
    /// looking with the status line admitting it is unconfirmed.
    /// </summary>
    public bool ShowLiveMarker =>
        HasLock && (_identifiedArea == null || ReferenceEquals(SelectedArea, _identifiedArea));

    public bool ShowFacingArrow => ShowLiveMarker && HasFacing;
    public bool ShowLiveDot     => ShowLiveMarker && !HasFacing;

    private string _livePosition = "";
    /// <summary>"X 3 · Y 7" once locked; empty otherwise.</summary>
    public string LivePosition { get => _livePosition; private set => SetProperty(ref _livePosition, value); }

    private string _searchState = "Enter your current X and Y from the game, then click Snapshot.";
    public string SearchState { get => _searchState; private set => SetProperty(ref _searchState, value); }

    private string _status =
        "Reference only until located. To find your position: enter X and Y from the game display, " +
        "click Snapshot, walk to a different square, update X and Y, click Narrow — repeat until locked.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    // --- commands ------------------------------------------------------------
    public ICommand SnapshotCommand       { get; }
    public ICommand NarrowCommand         { get; }
    public ICommand ResetCommand          { get; }
    public ICommand TeleportCommand       { get; }
    public ICommand IdentifyCommand       { get; }
    public ICommand SelectLocationCommand { get; }

    // --- lifecycle -----------------------------------------------------------
    public void Attach(ProcessMemory mem)
    {
        _mem = mem;
        RaiseSearchCommands();
        Status = "Attached. Enter X and Y from the game display, then Snapshot.";
    }

    public void Detach()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        _mem = null;
        ClearLock();
        ClearPartyRange();
        Status = "Detached — position lost. Re-attach and Snapshot to locate again.";
    }

    private void ClearLock()
    {
        _candidates = new();
        _locked = null;
        _staleReads = 0;
        LivePosition = "";
        HasFacing = false;
        RaiseSearchCommands();
    }

    // --- snapshot / narrow ---------------------------------------------------
    private async void Snapshot()
    {
        if (_mem is not { IsOpen: true } || _isScanning) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        ClearLock();
        _isScanning = true;
        RaiseSearchCommands();
        Status = $"Scanning for [{CurrentX}, {CurrentY}]…";

        var mem = _mem;
        int x = CurrentX, y = CurrentY;
        List<PositionCandidate> found;
        try
        {
            found = await Task.Run(() => PositionLocator.ScanCandidates(mem, x, y, ct), ct);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Status = "Snapshot error: " + ex.Message; return; }
        finally { _isScanning = false; RaiseSearchCommands(); }

        if (_mem != mem) return;

        _candidates = found;
        RaiseSearchCommands();
        Status = _candidates.Count == 0
            ? "No candidates. Confirm X and Y match the game display, then Snapshot again."
            : $"Snapshot: {_candidates.Count:N0} candidate(s). Walk to a different square, update X and Y, then Narrow.";
        UpdateSearchState();
    }

    private async void NarrowStep()
    {
        if (_mem is not { IsOpen: true } || _isScanning || _candidates.Count == 0) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        _isScanning = true;
        RaiseSearchCommands();

        var mem = _mem;
        var prev = _candidates;
        int x = CurrentX, y = CurrentY;
        List<PositionCandidate> survivors;
        try
        {
            survivors = await Task.Run(() => PositionLocator.Narrow(mem, prev, x, y, ct), ct);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Status = "Narrow error: " + ex.Message; return; }
        finally { _isScanning = false; RaiseSearchCommands(); }

        if (_mem != mem) return;

        _candidates = survivors;
        RaiseSearchCommands();
        Status = _candidates.Count switch
        {
            0 => "No match. Click Reset, Snapshot again, and make sure X and Y exactly match the game display before Narrowing.",
            1 => "Position locked! The green marker tracks your live position, and Teleport is now enabled. " +
                 "Click 🧭 Identify to have the trainer work out which of the sixteen levels you are on.",
            _ => $"{_candidates.Count:N0} candidate(s) remain. Walk again, update X and Y, then Narrow."
        };
        UpdateSearchState();
    }

    private void ResetSearch()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        ClearLock();
        Status = "Reset. Enter X and Y from the game display, then Snapshot.";
        UpdateSearchState();
    }

    // --- poll tick -----------------------------------------------------------
    /// <summary>
    /// Called from the main poll loop. Promotes a sole surviving candidate to a locked address,
    /// then reads and publishes the live position. Rides out short read failures so map-load
    /// transitions don't invalidate the lock.
    /// </summary>
    public void Tick()
    {
        if (_mem is not { IsOpen: true }) return;

        if (_locked == null && _candidates.Count == 1)
        {
            _locked = _candidates[0];
            OnLocked();               // switch to the party's map *before* the state text is rebuilt,
            RaiseSearchCommands();    // or it reports the marker hidden on the map it just left
        }

        if (_locked == null) return;

        var pos = PositionLocator.Read(_mem, _locked.Value);
        if (pos == null)
        {
            if (++_staleReads < MaxStaleReads) return;
            ClearLock();
            Status = "Position lock lost (DOSBox restarted or area unloaded). Snapshot to re-locate.";
            UpdateSearchState();
            return;
        }

        _staleReads = 0;
        LiveX = pos.Value.X;
        LiveY = pos.Value.Y;
        HasFacing = pos.Value.Facing != null;
        if (pos.Value.Facing is int f && LiveFacing != f)
        {
            LiveFacing = f;
            OnPropertyChanged(nameof(LiveFacingDegrees));
        }
        LivePosition = $"X {pos.Value.X} · Y {pos.Value.Y}";
    }

    /// <summary>
    /// Runs once, when a single candidate is promoted to the lock. A lock gives coordinates but not
    /// which level they belong to, so this immediately tries to identify the level from the geometry
    /// the game has resident and opens that map.
    /// </summary>
    private void OnLocked()
    {
        Identify();
        OnPropertyChanged(nameof(ShowLiveMarker));
        OnPropertyChanged(nameof(ShowFacingArrow));
        OnPropertyChanged(nameof(ShowLiveDot));
    }

    // --- level identification ------------------------------------------------

    /// <summary>The folder holding the game's own files (<c>GEO*.DAX</c>, <c>GAME.OVR</c>). Set from
    /// the save folder's parent; the identify step reads the levels from here.</summary>
    private string _gameFolder = "";
    public string GameFolder
    {
        get => _gameFolder;
        set
        {
            if (!SetProperty(ref _gameFolder, value ?? "")) return;
            _levels = null;                       // re-read from the new folder on next identify
            RaiseSearchCommands();
        }
    }

    private IReadOnlyList<(string Geo, byte[] Walls)>? _levels;
    private nuint _partyLow, _partyHigh;
    private bool _hasPartyRange;
    private readonly byte[] _mapBuf = new byte[MapLocator.SweepBufferSize];

    /// <summary>Told by the poll loop where the party records are, which is the window the level
    /// data is swept for.</summary>
    public void SetPartyRange(nuint low, nuint high)
    {
        _partyLow = low;
        _partyHigh = high;
        if (_hasPartyRange) return;
        _hasPartyRange = true;
        RaiseSearchCommands();
    }

    public void ClearPartyRange()
    {
        _hasPartyRange = false;
        IdentifiedArea = null;
    }

    private bool CanIdentify() => IsAttached && _hasPartyRange && !_isScanning;

    /// <summary>
    /// Works out which level the game has loaded by looking for its wall data in the emulated RAM,
    /// and selects the matching map. Cheap enough to run from a button or on lock: the sweep is the
    /// megabyte around the party, not the whole process.
    /// </summary>
    public void Identify()
    {
        if (_mem is not { IsOpen: true }) { Status = "Attach first."; return; }
        if (!_hasPartyRange) { Status = "Scan for the party first — the level sweep starts from where its records are."; return; }

        _levels ??= DaxArchive.ReadLevels(GameFolder);
        if (_levels.Count == 0)
        {
            Status = $"No GEO*.DAX level files found in '{GameFolder}'. Point the Save folder box on the " +
                     "Powers/Inventory tab at your save folder — the game folder above it is where these live.";
            return;
        }

        List<LocatedLevel> hits;
        try { hits = MapLocator.Identify(_mem, _partyLow, _partyHigh, _levels, _mapBuf); }
        catch (Exception ex) { Status = "Identify error: " + ex.Message; return; }

        if (hits.Count == 0)
        {
            IdentifiedArea = null;
            Status = "No level matched — the game may be between areas, or in a menu. Take a step in-game and try again.";
            return;
        }

        // More than one match means two levels are resident at once (the one being left and the one
        // being entered). Prefer the one nearest the party records.
        var best = hits.OrderBy(h => Distance(h.Address, _partyLow)).First();
        var area = Areas.FirstOrDefault(a => string.Equals(a.Geo, best.Geo, StringComparison.OrdinalIgnoreCase));
        IdentifiedArea = area;
        if (area != null) SelectedArea = area;

        Status = area == null
            ? $"Level {best.Geo} is loaded, but no map in this trainer carries that block."
            : $"You are on “{area.Name}” ({best.Geo}) — matched by its wall data resident in the game." +
              (hits.Count > 1 ? $"  ({hits.Count} levels resident; picked the one nearest the party.)" : "");
        UpdateSearchState();
    }

    private static nuint Distance(nuint a, nuint b) => a > b ? a - b : b - a;

    // --- teleport ------------------------------------------------------------
    private bool CanTeleport() => IsAttached && _locked != null;

    private void Teleport()
    {
        if (_mem is not { IsOpen: true }) { Status = "Attach first."; return; }
        if (_locked == null) { Status = "No position lock yet — Snapshot and Narrow down to a single address first."; return; }

        bool ok = PositionLocator.Write(_mem, _locked.Value, TargetX, TargetY);
        if (!ok) { Status = "Teleport write failed — Snapshot to re-locate the position."; return; }

        // Landing inside a sealed block is survivable — the game simply won't let you step off it —
        // but it is worth saying so before the player thinks they are stuck.
        string warn = SealedWarning(TargetX, TargetY);
        if (PositionLocator.StoresNegativeX(_locked.Value, TargetX))
            warn += "  Note: this square stores a negative X for the current lock, which is further west " +
                    "than the lock was calibrated — if the position reads wrong, walk a square and Narrow again.";
        Status = $"Teleported to ({TargetX}, {TargetY}). Take one step in-game to redraw the map.{warn}";
    }

    /// <summary>Warns about teleporting into a square the level's own geometry says is unreachable —
    /// walled off on every side, or cut off from the level's main walkable region.</summary>
    private string SealedWarning(int x, int y)
    {
        var t = (_identifiedArea ?? SelectedArea)?.Terrain;
        if (t == null || x < 0 || y < 0 || x >= t.GetLength(0) || y >= t.GetLength(1)) return "";
        return t[x, y].Floor == FloorKind.Stone
            ? "  ⚠ That square is sealed off in this level's geometry — you may have to teleport back out."
            : "";
    }

    // --- helpers -------------------------------------------------------------
    private void UpdateSearchState()
    {
        SearchState = _locked != null
            ? "Locked — live position tracking active."
              + (_identifiedArea == null
                    ? " Level not identified yet: the marker is drawn on the map you have selected."
                    : ShowLiveMarker ? "" : $" Marker hidden: the party is on “{_identifiedArea.Name}”.")
            : _candidates.Count == 0
                ? "No candidates — enter X and Y and click Snapshot."
                : $"{_candidates.Count:N0} candidate(s) — walk, update X and Y, then Narrow.";
        OnPropertyChanged(nameof(HasLock));
        OnPropertyChanged(nameof(ShowLiveMarker));
        OnPropertyChanged(nameof(ShowFacingArrow));
        OnPropertyChanged(nameof(ShowLiveDot));
    }

    private void RaiseSearchCommands()
    {
        (SnapshotCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        (NarrowCommand     as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (TeleportCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        (IdentifyCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        UpdateSearchState();
    }
}
