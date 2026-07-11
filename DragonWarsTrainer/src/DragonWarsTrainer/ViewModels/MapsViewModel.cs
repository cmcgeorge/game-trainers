using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using DragonWarsTrainer.Game;
using DragonWarsTrainer.Memory;

namespace DragonWarsTrainer.ViewModels;

/// <summary>One drawable map square projected for the schematic: its grid position and terrain.</summary>
public sealed record TerrainCell(int X, int Y, FloorKind Floor, WallKind West, WallKind North);

/// <summary>
/// Backs the 🗺 Maps tab: an offline area/location reference plus a live "where am I / teleport me
/// there" helper. The party's live position lives in a 256-byte global "Heap" whose address changes
/// every session and cannot be anchored offline, so it is found by moving — a structural
/// <see cref="Snapshot"/> collects every plausible Heap address, then the user walks a known number
/// of squares in-game and <see cref="ApplyMoveNarrow"/> discards every candidate that did not shift
/// by that exact distance on each axis, repeating until a single address remains (the position
/// lock). Matching by magnitude means the Y axis direction is handled automatically. Teleport writes
/// only the 2 position bytes (X/Y) to that locked address; do it while exploring, never mid-combat.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;

    private List<(nuint Address, HeapReading Reading)> _candidates = new();
    private nuint? _lockedAddress;
    private int? _liveBoardId;
    private bool _isScanning;
    private CancellationTokenSource? _scanCts;

    private DataArchive? _archive;
    private BoardMap? _board;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLocation> Locations { get; } = new();

    private IReadOnlyList<TerrainCell> _terrain = Array.Empty<TerrainCell>();
    /// <summary>Drawable terrain squares (walls / water / abyss) for the selected area, if decoded.</summary>
    public IReadOnlyList<TerrainCell> Terrain { get => _terrain; private set => SetField(ref _terrain, value); }

    public MapsViewModel(Func<ProcessMemory?> getMem)
    {
        _getMem = getMem;
        SnapshotCommand = new RelayCommand(_ => Snapshot(), _ => IsAttached && !_isScanning);
        ApplyMoveCommand = new RelayCommand(_ => ApplyMoveNarrow(), _ => IsAttached && !_isScanning && _candidates.Count > 0);
        ResetCommand = new RelayCommand(_ => ResetSearch(), _ => !_isScanning && (_candidates.Count > 0 || _lockedAddress != null));
        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport());
        SelectLocationCommand = new RelayCommand(p => { if (p is MapLocation l) SelectedLocation = l; });
        LoadDataCommand = new RelayCommand(_ => LoadData());
        TryAutoLoad();
        SelectedArea = Areas.FirstOrDefault();
    }

    private bool IsAttached => _getMem() is { IsOpen: true };

    // --- reference selection -------------------------------------------------
    private MapArea? _selectedArea;
    public MapArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (!SetField(ref _selectedArea, value)) return;
            Locations.Clear();
            if (value != null) foreach (var l in value.Locations) Locations.Add(l);
            SelectedLocation = Locations.FirstOrDefault();
            RebuildTerrain();
        }
    }

    /// <summary>
    /// Grid dimensions used to size and Y-flip the schematic. Prefers the real decoded board size
    /// when the data archive is loaded, otherwise falls back to the reference area size.
    /// </summary>
    public int GridWidth => _board?.Width ?? (_selectedArea?.GridWidth ?? 1);
    public int GridHeight => _board?.Height ?? (_selectedArea?.GridHeight ?? 1);

    private MapLocation? _selectedLocation;
    public MapLocation? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (!SetField(ref _selectedLocation, value)) return;
            if (value != null)
            {
                TargetX = value.X; TargetY = value.Y;
            }
        }
    }

    private int _targetX;
    public int TargetX { get => _targetX; set => SetField(ref _targetX, Math.Clamp(value, 0, 255)); }

    private int _targetY;
    public int TargetY { get => _targetY; set => SetField(ref _targetY, Math.Clamp(value, 0, 255)); }

    // --- located live position (drives the green dot on the map) -------------
    private int _liveX;
    public int LiveX { get => _liveX; private set => SetField(ref _liveX, value); }

    private int _liveY;
    public int LiveY { get => _liveY; private set => SetField(ref _liveY, value); }

    // --- movement entered between snapshots to narrow the search -------------
    // Total squares walked in a straight line since the last snapshot. Direction is deliberately not
    // asked for: Dragon Wars moves the party forward relative to its facing, so matching by total
    // distance (X+Y shift) locates the party no matter which way it faced.
    private int _stepsMoved = 1;
    public int StepsMoved { get => _stepsMoved; set => SetField(ref _stepsMoved, Math.Max(0, value)); }

    // --- position lock -------------------------------------------------------
    public bool HasLock => _lockedAddress != null;

    private string _livePosition = "";
    /// <summary>"Purgatory — X 20 · Y 13 facing North" once locked; empty otherwise.</summary>
    public string LivePosition { get => _livePosition; private set => SetField(ref _livePosition, value); }

    private string _searchState = "Snapshot memory, then walk a known distance and Apply move to locate the party.";
    public string SearchState { get => _searchState; private set => SetField(ref _searchState, value); }

    private string _status =
        "Reference only until located. To teleport: attach on the Party tab, click Snapshot memory, walk a few squares in-game in a straight line, type how many, then Apply move until a single address remains.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand SnapshotCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TeleportCommand { get; }
    public ICommand ApplyMoveCommand { get; }
    public ICommand SelectLocationCommand { get; }
    public ICommand LoadDataCommand { get; }

    // --- terrain (walls / water / abyss from DATA1+DATA2) --------------------
    /// <summary>True once the data archive is loaded and terrain can be drawn.</summary>
    public bool HasTerrain => _archive != null;

    private string _dataStatus = "Terrain not loaded — click \"Load game folder\" and pick the folder holding DATA1 / DATA2 to draw walls and water.";
    /// <summary>Human-readable state of the terrain data load.</summary>
    public string DataStatus { get => _dataStatus; private set => SetField(ref _dataStatus, value); }

    private void LoadData()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select the Dragon Wars game folder (containing DATA1 and DATA2)",
        };
        if (dlg.ShowDialog() != true) return;
        LoadDataFrom(dlg.FolderName);
    }

    private bool LoadDataFrom(string folder)
    {
        if (!DataArchive.TryLoadFromFolder(folder, out var archive, out var error))
        {
            DataStatus = error;
            return false;
        }
        _archive = archive;
        SavedDataPath = folder;
        OnPropertyChanged(nameof(HasTerrain));
        RebuildTerrain();
        DataStatus = $"Terrain loaded from {folder}.";
        return true;
    }

    private void TryAutoLoad()
    {
        var saved = SavedDataPath;
        if (!string.IsNullOrEmpty(saved) && LoadDataFrom(saved)) return;

        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".game");
            if (Directory.Exists(candidate) && LoadDataFrom(candidate)) return;
        }
    }

    private void RebuildTerrain()
    {
        _board = null;
        if (_archive != null && _selectedArea is { } area)
        {
            var chunk = _archive.GetChunk(0x46 + area.Id);
            if (chunk != null) _board = BoardMap.TryParse(chunk);
        }
        var cells = new List<TerrainCell>();
        if (_board is { } board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var sq = board.Square(x, y);
                    if (sq.Floor == FloorKind.Normal && sq.West == WallKind.None && sq.North == WallKind.None)
                        continue;
                    cells.Add(new TerrainCell(x, y, sq.Floor, sq.West, sq.North));
                }
            }
        }
        Terrain = cells;
        OnPropertyChanged(nameof(GridWidth));
        OnPropertyChanged(nameof(GridHeight));
    }

    // --- remembered data folder ----------------------------------------------
    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DragonWarsTrainer", "datapath.txt");

    private static string? SavedDataPath
    {
        get
        {
            try { return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null; }
            catch { return null; }
        }
        set
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
                File.WriteAllText(SettingsFile, value);
            }
            catch { /* best effort — not critical */ }
        }
    }

    // --- position search -----------------------------------------------------
    private async void Snapshot()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_isScanning) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        _isScanning = true;
        _lockedAddress = null;
        _liveBoardId = null;
        _candidates = new();
        StepsMoved = 1;
        Status = "Reading memory for every plausible position…";
        RaiseSearchState();

        List<(nuint, HeapReading)> found;
        try
        {
            found = await Task.Run(() =>
            {
                var addrs = HeapLocator.ScanCandidates(mem, ct);
                var list = new List<(nuint, HeapReading)>(addrs.Count);
                foreach (var a in addrs)
                {
                    ct.ThrowIfCancellationRequested();
                    var r = HeapLocator.Read(mem, a);
                    if (r != null) list.Add((a, r.Value));
                }
                return list;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            return;   // detached/reset while scanning — OnDetached/ResetSearch already cleaned up
        }
        catch (Exception ex)
        {
            _isScanning = false;
            Status = "Snapshot error: " + ex.Message;
            RaiseSearchState();
            return;
        }

        _isScanning = false;

        if (_getMem() != mem) return;   // detached/re-attached while scanning

        _candidates = found;
        RaiseSearchState();
        Status = _candidates.Count == 0
            ? "No candidates found. Make sure the party is loaded and on a map, then Snapshot again."
            : $"Snapshot taken: {_candidates.Count} candidate(s). Walk a known number of squares in-game (straight line), enter how many, then Apply move.";
    }

    private async void ApplyMoveNarrow()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_isScanning) return;
        if (_candidates.Count == 0) { Status = "Click Snapshot memory first, then walk and Apply move."; return; }
        if (StepsMoved <= 0) { Status = "Enter how many squares you walked (a straight line, at least 1) before applying."; return; }

        int steps = StepsMoved;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        _isScanning = true;
        var previous = _candidates;
        RaiseSearchState();

        List<(nuint Address, HeapReading Reading)> survivors;
        try
        {
            survivors = await Task.Run(() => HeapLocator.NarrowBySteps(mem, previous, steps, ct), ct);
        }
        catch (OperationCanceledException)
        {
            return;   // detached/reset while narrowing — cleanup already done
        }
        catch (Exception ex)
        {
            _isScanning = false;
            Status = "Apply move error: " + ex.Message;
            RaiseSearchState();
            return;
        }

        _isScanning = false;

        if (_getMem() != mem) return;   // detached/re-attached while narrowing

        _candidates = survivors;
        StepsMoved = 1;
        RaiseSearchState();
        Status = _candidates.Count switch
        {
            0 => "No position matched that move. Click Reset, Snapshot again, and make sure the number of squares matches exactly how far you walked in a straight line (all within one map).",
            1 => "Position locked! The green dot on the map is your live position, and Teleport is now enabled.",
            _ => $"{_candidates.Count} candidate(s) left. Walk again in a straight line, enter the squares moved, then Apply move."
        };
    }

    private void ResetSearch()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        _candidates = new();
        _lockedAddress = null;
        _liveBoardId = null;
        StepsMoved = 1;
        RaiseSearchState();
        Status = "Search reset. Click Snapshot memory to begin again.";
    }

    /// <summary>
    /// Clears all position-search state when the trainer detaches so a fresh session starts from
    /// scratch (a locked address from the previous process is meaningless once detached). Also
    /// cancels any in-flight snapshot/narrow so the background sweep stops touching the disposed
    /// process.
    /// </summary>
    public void OnDetached()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        _candidates = new();
        _lockedAddress = null;
        _liveBoardId = null;
        LivePosition = "";
        RaiseSearchState();
        Status = "Detached. Attach on the Party tab, then Snapshot memory to locate the party again.";
    }

    public void OnAttached()
    {
        RaiseSearchState();
        Status = "Attached. Click Snapshot memory, walk a known distance in a straight line, enter how many squares, then Apply move — repeat until it locks.";
    }

    /// <summary>Poll-tick refresh: lock when one candidate remains and re-read the live position.</summary>
    public void Tick()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true })
        {
            if (HasLock || _candidates.Count > 0 || LivePosition.Length > 0) OnDetached();
            return;
        }

        if (_lockedAddress == null && _candidates.Count == 1)
            _lockedAddress = _candidates[0].Address;

        if (_lockedAddress == null) return;

        var reading = HeapLocator.Read(mem, _lockedAddress.Value);
        if (reading == null)
        {
            _lockedAddress = null;
            _liveBoardId = null;
            _candidates = new();
            LivePosition = "";
            RaiseSearchState();
            Status = "Lost the position lock (map changed or DOSBox restarted). Click Snapshot memory to re-locate.";
            return;
        }

        var r = reading.Value;
        LiveX = r.X; LiveY = r.Y;
        LivePosition = $"{MapBook.MapName(r.BoardId)} — X {r.X} · Y {r.Y} facing {MapBook.FacingName(r.Facing)}";

        // Follow the party onto whatever board it is standing on so the green dot lands on the right
        // map — but only when the board actually changes, so we don't clobber a map the user is
        // browsing on every poll tick.
        if (_liveBoardId != r.BoardId)
        {
            _liveBoardId = r.BoardId;
            var area = Areas.FirstOrDefault(a => a.Id == r.BoardId);
            if (area != null && !ReferenceEquals(area, SelectedArea)) SelectedArea = area;
        }
    }

    // --- teleport ------------------------------------------------------------
    private bool CanTeleport() => IsAttached && _lockedAddress != null;

    private void Teleport()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_lockedAddress == null) { Status = "No position lock yet — Snapshot and Apply move down to a single address first."; return; }

        var reading = HeapLocator.Read(mem, _lockedAddress.Value);
        if (reading == null) { Status = "The position lock went stale. Click Snapshot memory to re-locate."; return; }

        var r = reading.Value;
        if (SelectedArea != null && SelectedArea.Id != r.BoardId)
        {
            Status = $"Won't teleport: you're on {MapBook.MapName(r.BoardId)} but picked {SelectedArea.Name}. " +
                     "Teleport only moves the party within the current map — walk to the target map first.";
            return;
        }
        if (TargetX >= r.MaxX || TargetY >= r.MaxY)
        {
            Status = $"({TargetX}, {TargetY}) is outside this {r.MaxX}×{r.MaxY} map.";
            return;
        }

        // Coordinates are one byte each; write just the 2 position bytes so nothing else is touched.
        bool ok = mem.WriteRange(_lockedAddress.Value, new[] { (byte)TargetY, (byte)TargetX }, 0, 2);
        Status = ok
            ? $"Teleported to ({TargetX}, {TargetY}). Take one step in-game to redraw the map."
            : "Teleport write failed — click Snapshot memory to re-locate the position.";
    }

    private void RaiseSearchState()
    {
        _lockedAddress = _candidates.Count == 1 && _lockedAddress == null ? _candidates[0].Address : _lockedAddress;
        SearchState = _lockedAddress != null
            ? "Locked on a single address."
            : _candidates.Count == 0
                ? "No candidates — click Snapshot memory."
                : $"{_candidates.Count} candidate(s) — walk a known distance, then Apply move.";
        OnPropertyChanged(nameof(HasLock));
        (SnapshotCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyMoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
