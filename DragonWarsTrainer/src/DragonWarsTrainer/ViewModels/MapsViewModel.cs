using System.Collections.ObjectModel;
using System.Windows.Input;
using DragonWarsTrainer.Game;
using DragonWarsTrainer.Memory;

namespace DragonWarsTrainer.ViewModels;

/// <summary>
/// Backs the 🗺 Maps tab: an offline area/location reference plus a live "where am I / teleport me
/// there" helper. The party's live position lives in a 256-byte global "Heap" whose address changes
/// every session and cannot be anchored offline, so it is found the MM1/BT1 way — an initial
/// structural scan collects every plausible Heap address, then the user takes a step in-game and
/// <see cref="Narrow"/> discards every candidate that did not move like the party, repeating until a
/// single address remains (the position lock). Teleport writes only the 2 position bytes (X/Y) to
/// that locked address; do it while exploring, never mid-combat.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;

    private List<(nuint Address, HeapReading Reading)> _candidates = new();
    private nuint? _lockedAddress;
    private bool _isScanning;
    private CancellationTokenSource? _scanCts;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLocation> Locations { get; } = new();

    public MapsViewModel(Func<ProcessMemory?> getMem)
    {
        _getMem = getMem;
        FindCommand = new RelayCommand(_ => Find(), _ => IsAttached && !_isScanning);
        NarrowCommand = new RelayCommand(_ => Narrow(), _ => IsAttached && !_isScanning && _candidates.Count > 1);
        ResetCommand = new RelayCommand(_ => ResetSearch(), _ => !_isScanning && (_candidates.Count > 0 || _lockedAddress != null));
        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport());
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
        }
    }

    private MapLocation? _selectedLocation;
    public MapLocation? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (!SetField(ref _selectedLocation, value)) return;
            if (value != null) { TargetX = value.X; TargetY = value.Y; }
        }
    }

    private int _targetX;
    public int TargetX { get => _targetX; set => SetField(ref _targetX, Math.Clamp(value, 0, 255)); }

    private int _targetY;
    public int TargetY { get => _targetY; set => SetField(ref _targetY, Math.Clamp(value, 0, 255)); }

    // --- position lock -------------------------------------------------------
    public bool HasLock => _lockedAddress != null;

    private string _livePosition = "";
    /// <summary>"Purgatory — X 20 · Y 13 facing North" once locked; empty otherwise.</summary>
    public string LivePosition { get => _livePosition; private set => SetField(ref _livePosition, value); }

    private string _searchState = "Take a step in-game between Find and Narrow so the party's position changes.";
    public string SearchState { get => _searchState; private set => SetField(ref _searchState, value); }

    private string _status =
        "Reference only until located. To teleport: attach on the Party tab, click Find here, take one step in-game, then Narrow until a single address remains.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand FindCommand { get; }
    public ICommand NarrowCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TeleportCommand { get; }

    // --- position search -----------------------------------------------------
    private async void Find()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_isScanning) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        _isScanning = true;
        _lockedAddress = null;
        _candidates = new();
        Status = "Scanning memory for the party's position…";
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
            Status = "Find error: " + ex.Message;
            RaiseSearchState();
            return;
        }

        _isScanning = false;

        if (_getMem() != mem) return;   // detached/re-attached while scanning

        _candidates = found;
        RaiseSearchState();
        Status = _candidates.Count == 0
            ? "No candidates found. Make sure the party is loaded and on a map, then Find again."
            : $"Found {_candidates.Count} candidate(s). Take one step in-game, then click Narrow.";
    }

    private async void Narrow()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_isScanning) return;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        _isScanning = true;
        var previous = _candidates;
        RaiseSearchState();

        List<(nuint Address, HeapReading Reading)> survivors;
        try
        {
            survivors = await Task.Run(() => HeapLocator.Narrow(mem, previous, ct), ct);
        }
        catch (OperationCanceledException)
        {
            return;   // detached/reset while narrowing — cleanup already done
        }
        catch (Exception ex)
        {
            _isScanning = false;
            Status = "Narrow error: " + ex.Message;
            RaiseSearchState();
            return;
        }

        _isScanning = false;

        if (_getMem() != mem) return;   // detached/re-attached while narrowing

        _candidates = survivors;
        RaiseSearchState();
        Status = _candidates.Count switch
        {
            0 => "All candidates dropped. Click Reset and Find again (be sure to actually move between Find and Narrow).",
            1 => "Position locked. Pick a location (or type X/Y) and Teleport.",
            _ => $"{_candidates.Count} candidate(s) remain. Take another step, then Narrow again."
        };
    }

    private void ResetSearch()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        _candidates = new();
        _lockedAddress = null;
        RaiseSearchState();
        Status = "Search reset. Click Find to begin again.";
    }

    /// <summary>
    /// Clears all position-search state when the trainer detaches so a fresh session starts from
    /// scratch (a locked address from the previous process is meaningless once detached). Also
    /// cancels any in-flight Find/Narrow so the background sweep stops touching the disposed process.
    /// </summary>
    public void OnDetached()
    {
        _scanCts?.Cancel();
        _isScanning = false;
        _candidates = new();
        _lockedAddress = null;
        LivePosition = "";
        RaiseSearchState();
        Status = "Detached. Attach on the Party tab, then Find to locate the party again.";
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
            _candidates = new();
            LivePosition = "";
            RaiseSearchState();
            Status = "Lost the position lock (map changed or DOSBox restarted). Click Find to re-locate.";
            return;
        }

        var r = reading.Value;
        LivePosition = $"{MapBook.MapName(r.BoardId)} — X {r.X} · Y {r.Y} facing {MapBook.FacingName(r.Facing)}";
    }

    // --- teleport ------------------------------------------------------------
    private bool CanTeleport() => IsAttached && _lockedAddress != null;

    private void Teleport()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Status = "Attach on the Party tab first."; return; }
        if (_lockedAddress == null) { Status = "No position lock yet — Find and Narrow to a single address first."; return; }

        var reading = HeapLocator.Read(mem, _lockedAddress.Value);
        if (reading == null) { Status = "The position lock went stale. Click Find to re-locate."; return; }

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
            : "Teleport write failed — click Find to re-locate the position.";
    }

    private void RaiseSearchState()
    {
        _lockedAddress = _candidates.Count == 1 && _lockedAddress == null ? _candidates[0].Address : _lockedAddress;
        SearchState = _lockedAddress != null
            ? "Locked on a single address."
            : _candidates.Count == 0
                ? "No candidates — click Find."
                : $"{_candidates.Count} candidate(s) — step in-game, then Narrow.";
        OnPropertyChanged(nameof(HasLock));
        (FindCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NarrowCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
