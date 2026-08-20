using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using BardsTale1Trainer.Game;
using BardsTale1Trainer.Memory;

namespace BardsTale1Trainer.ViewModels;

/// <summary>
/// One map in the picker: its reference data plus the map image to display. Bard's Tale 1 has
/// no bundled scans, so the image is drawn from the area's own wall grid
/// (<see cref="MapTerrainData"/>) by <see cref="MapRenderer"/>, once on first use — the user's
/// calibration anchors give that picture its game meaning.
/// </summary>
public sealed class MapEntryViewModel
{
    public GameMap Map { get; }

    public MapEntryViewModel(GameMap map) => Map = map;

    public string Name => Map.Name;
    public string Category => Map.Category;
    public string Description => $"{Map.Description} ({Map.Width}×{Map.Height} cells)";

    public ImageSource Image => _image ??= MapRenderer.Render(Map.Terrain, Map.Width, Map.Height);
    private ImageSource? _image;

    /// <summary>
    /// What is on the given square and what walls it in, for the status line — empty when it is
    /// plain ground in the open. A square only records its own west and north edges, so the east
    /// and south ones are read off the neighbour that does (the map's own rim excepted).
    /// </summary>
    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return "";
        var here = Map.Terrain[x, y];

        var bits = new List<string>();
        if (here.Description is { } what) bits.Add(what);
        Add("N", here.North);
        Add("E", x + 1 < Map.Width ? Map.Terrain[x + 1, y].West : here.East);
        Add("S", y + 1 < Map.Height ? Map.Terrain[x, y + 1].North : here.South);
        Add("W", here.West);
        return string.Join(", ", bits);

        void Add(string side, WallKind kind)
        {
            if (kind.Describe() is { } name) bits.Add($"{side} {name}");
        }
    }
}

/// <summary>
/// Backs the Maps tab: the full list of Bard's Tale 1 areas (grouped by category for the
/// dropdown) and the currently selected grid to display — plus, once the X/Y Search tab has
/// narrowed the party position down to a single address, a live party marker drawn over the
/// grid and click-to-teleport.
///
/// The trainer can't know how the position bytes map onto a given area's grid, so the user
/// calibrates each map once by standing somewhere in-game and clicking that spot on the
/// grid, twice from two different positions (different row AND column). The two anchors
/// define the linear transform (<see cref="MapCalibration"/>), persisted per map to
/// %APPDATA%\BT1Trainer\map-calibration.json so it survives restarts.
/// </summary>
public sealed class MapReferenceViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<nuint?> _getPositionAddress;
    private readonly Action<string> _setStatus;
    private readonly Dictionary<string, List<MapAnchor>> _anchorsByMap;

    public ICollectionView Maps { get; }

    public MapReferenceViewModel(Func<ProcessMemory?> getMem, Func<nuint?> getPositionAddress,
        Action<string> setStatus)
    {
        _getMem = getMem;
        _getPositionAddress = getPositionAddress;
        _setStatus = setStatus;
        _anchorsByMap = LoadAnchors();

        var items = new ObservableCollection<MapEntryViewModel>(
            MapBook.Maps.Select(m => new MapEntryViewModel(m)));
        Maps = CollectionViewSource.GetDefaultView(items);
        Maps.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MapEntryViewModel.Category)));
        SelectedMap = items.FirstOrDefault();

        ClearCalibrationCommand = new RelayCommand(ClearCalibration, () => CurrentAnchors.Count > 0);
    }

    private MapEntryViewModel? _selectedMap;
    public MapEntryViewModel? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (!SetField(ref _selectedMap, value)) return;
            _isMarkingAnchor = false;        // a mark-in-progress belongs to the previous map
            OnPropertyChanged(nameof(IsMarkingAnchor));
            RefreshCalibrationState();
        }
    }

    public RelayCommand ClearCalibrationCommand { get; }

    // --- live position -----------------------------------------------------------
    private bool _hasPositionLock;
    /// <summary>True while the X/Y Search tab holds exactly one candidate address.</summary>
    public bool HasPositionLock
    {
        get => _hasPositionLock;
        private set { if (SetField(ref _hasPositionLock, value)) OnPropertyChanged(nameof(HintText)); }
    }

    private int _liveX = -1, _liveY = -1;
    /// <summary>"party at X 12 · Y 5" while locked; empty otherwise.</summary>
    public string LivePositionText =>
        _hasPositionLock && _liveX >= 0 ? $"party at X {_liveX} · Y {_liveY}" : "";

    // --- calibration -------------------------------------------------------------
    private List<MapAnchor> CurrentAnchors =>
        _selectedMap != null && _anchorsByMap.TryGetValue(_selectedMap.Name, out var list)
            ? list : new List<MapAnchor>();

    private MapCalibration? _calibration;
    public bool IsCalibrated => _calibration != null;

    private bool _isMarkingAnchor;
    /// <summary>Armed by the "Mark party position" toggle: the next map click records an anchor.</summary>
    public bool IsMarkingAnchor
    {
        get => _isMarkingAnchor;
        set { if (SetField(ref _isMarkingAnchor, value)) OnPropertyChanged(nameof(HintText)); }
    }

    private bool _teleportOnClick;
    /// <summary>When on (and calibrated and locked), clicking the map teleports the party there.</summary>
    public bool TeleportOnClick
    {
        get => _teleportOnClick;
        set { if (SetField(ref _teleportOnClick, value)) OnPropertyChanged(nameof(HintText)); }
    }

    public string CalibrationText
    {
        get
        {
            int n = CurrentAnchors.Count;
            return _calibration != null
                ? $"Calibrated ({n} anchors)."
                : n == 0 ? "Not calibrated."
                : "1 anchor set — mark the party once more from a spot on a different row AND column.";
        }
    }

    public string HintText
    {
        get
        {
            if (!_hasPositionLock)
                return "To use the live marker: find the party's position with the 📍 X / Y Search tab first "
                     + "(narrow until a single address remains).";
            if (_isMarkingAnchor)
                return "Now click the party's exact spot on the grid below.";
            if (_calibration == null)
                return "Position locked. Calibrate this map: click ✛ Mark party position, then click the party's "
                     + "spot on the grid. Repeat once from a different position (different row and column).";
            return _teleportOnClick
                ? "Teleport armed: click anywhere on the grid to move the party there."
                : "Live marker active. Tick 🚀 Teleport on click to move the party by clicking the grid.";
        }
    }

    // --- marker (canvas-coordinates in image pixels) ------------------------------
    private double _markerX, _markerY;
    public double MarkerX { get => _markerX; private set => SetField(ref _markerX, value); }
    public double MarkerY { get => _markerY; private set => SetField(ref _markerY, value); }

    private bool _markerVisible;
    public bool MarkerVisible { get => _markerVisible; private set => SetField(ref _markerVisible, value); }

    /// <summary>Periodic poll from the owner's timer: track the lock and re-read the position.</summary>
    public void Tick()
    {
        var addr = _getPositionAddress();
        var mem = _getMem();
        bool locked = addr != null && mem != null;
        HasPositionLock = locked;
        if (!locked)
        {
            _liveX = _liveY = -1;
            MarkerVisible = false;
            OnPropertyChanged(nameof(LivePositionText));
            return;
        }

        var bytes = mem!.Read(addr!.Value, 2);
        if (bytes.Length < 2) return;   // transient read failure; keep the last marker
        if (bytes[0] != _liveX || bytes[1] != _liveY)
        {
            _liveX = bytes[0];
            _liveY = bytes[1];
            OnPropertyChanged(nameof(LivePositionText));
        }
        UpdateMarker();
    }

    private void UpdateMarker()
    {
        if (_calibration == null || _liveX < 0)
        {
            MarkerVisible = false;
            return;
        }
        var (px, py) = _calibration.ToPixel(_liveX, _liveY);
        MarkerX = px;
        MarkerY = py;
        MarkerVisible = true;
    }

    /// <summary>Map click from the view, in image-pixel coordinates (the image isn't scaled).</summary>
    public void OnMapClicked(double pixelX, double pixelY)
    {
        if (_selectedMap == null) return;
        if (_isMarkingAnchor) { AddAnchor(pixelX, pixelY); return; }
        if (_teleportOnClick) { Teleport(pixelX, pixelY); return; }
        DescribeSquare(pixelX, pixelY);
    }

    /// <summary>
    /// With nothing else armed, a click just reads out the square. This needs no calibration —
    /// the drawn grid's own geometry says which square a pixel is in — so the numbers are the
    /// picture's rows and columns, not the game's X/Y, which only calibration establishes.
    /// </summary>
    private void DescribeSquare(double pixelX, double pixelY)
    {
        int col = (int)Math.Floor((pixelX - MapRenderer.Border) / MapRenderer.Cell);
        int row = (int)Math.Floor((pixelY - MapRenderer.Border) / MapRenderer.Cell);
        if (col < 0 || row < 0 || col >= _selectedMap!.Map.Width || row >= _selectedMap.Map.Height) return;

        string what = _selectedMap.Describe(col, row);
        _setStatus($"Grid square {col}, {row} (counted from the top-left) on “{_selectedMap.Name}”"
            + (what.Length == 0 ? "." : $" — {what}."));
    }

    private void AddAnchor(double pixelX, double pixelY)
    {
        IsMarkingAnchor = false;
        if (_liveX < 0) { _setStatus("Can't mark: the party position can't be read right now."); return; }

        if (!_anchorsByMap.TryGetValue(_selectedMap!.Name, out var anchors))
            _anchorsByMap[_selectedMap.Name] = anchors = new List<MapAnchor>();

        // Keep at most two anchors: a third replaces the older one, so a misplaced anchor is
        // fixed by simply marking again (no clear-and-redo).
        anchors.Add(new MapAnchor(pixelX, pixelY, _liveX, _liveY));
        if (anchors.Count > 2) anchors.RemoveAt(0);

        // Two anchors sharing a row/column can't define the transform — drop the older.
        if (anchors.Count == 2 && MapCalibration.FromAnchors(anchors[0], anchors[1]) == null)
        {
            anchors.RemoveAt(0);
            _setStatus($"Anchor recorded at X {_liveX} · Y {_liveY}. The second anchor must be on a different "
                + "row AND column — move the party and mark again.");
        }
        else
        {
            _setStatus(anchors.Count < 2
                ? $"Anchor recorded at X {_liveX} · Y {_liveY}. Move the party (different row and column) and mark once more."
                : $"Anchor recorded at X {_liveX} · Y {_liveY}. Map calibrated.");
        }
        SaveAnchors();
        RefreshCalibrationState();
    }

    private void Teleport(double pixelX, double pixelY)
    {
        var addr = _getPositionAddress();
        var mem = _getMem();
        if (_calibration == null || addr == null || mem == null || _selectedMap == null) return;
        var (gx, gy) = _calibration.ToGame(pixelX, pixelY);
        if (gx < 0 || gx >= _selectedMap.Map.Width || gy < 0 || gy >= _selectedMap.Map.Height)
        {
            _setStatus($"That click lands outside this map's {_selectedMap.Map.Width}×{_selectedMap.Map.Height} grid.");
            return;
        }
        bool ok = mem.Write(addr.Value, new[] { (byte)gx, (byte)gy });
        _setStatus(ok ? $"Teleported the party to X {gx} · Y {gy}." : "Teleport write failed.");
        if (ok) Tick();   // move the marker right away rather than on the next poll
    }

    private void ClearCalibration()
    {
        if (_selectedMap == null) return;
        _anchorsByMap.Remove(_selectedMap.Name);
        SaveAnchors();
        RefreshCalibrationState();
        _setStatus($"Calibration cleared for “{_selectedMap.Name}”.");
    }

    private void RefreshCalibrationState()
    {
        var anchors = CurrentAnchors;
        _calibration = anchors.Count == 2 ? MapCalibration.FromAnchors(anchors[0], anchors[1]) : null;
        OnPropertyChanged(nameof(IsCalibrated));
        OnPropertyChanged(nameof(CalibrationText));
        OnPropertyChanged(nameof(HintText));
        ClearCalibrationCommand.RaiseCanExecuteChanged();
        UpdateMarker();
    }

    // --- persistence (per-map anchors; the transform is recomputed on load) --------
    private sealed class AnchorDto
    {
        public double PixelX { get; set; }
        public double PixelY { get; set; }
        public int GameX { get; set; }
        public int GameY { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BT1Trainer", "map-calibration.json");

    private void SaveAnchors()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var data = _anchorsByMap.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(a => new AnchorDto
                { PixelX = a.PixelX, PixelY = a.PixelY, GameX = a.GameX, GameY = a.GameY }).ToList());
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch (Exception ex)
        {
            // Best-effort: a failed save shouldn't break the UI, but tell the user their
            // calibration won't persist rather than failing silently.
            _setStatus($"Couldn't save map calibration: {ex.Message}");
        }
    }

    private static Dictionary<string, List<MapAnchor>> LoadAnchors()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, List<AnchorDto>>>(File.ReadAllText(FilePath));
                if (data != null)
                    return data.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.Take(2).Select(a => new MapAnchor(a.PixelX, a.PixelY, a.GameX, a.GameY)).ToList());
            }
        }
        catch { /* corrupt file: start uncalibrated rather than failing the app */ }
        return new Dictionary<string, List<MapAnchor>>();
    }
}
