using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One map in the picker: its reference data plus the loaded image to display.</summary>
public sealed class MapEntryViewModel
{
    public GameMap Map { get; }

    public MapEntryViewModel(GameMap map) => Map = map;

    public string Name => Map.Name;
    public string Category => Map.Category;
    public string Description => Map.Description;

    /// <summary>The bundled PNG, loaded from the assembly via a pack URI (cached after first use).</summary>
    public ImageSource Image => _image ??= LoadImage(Map.Image);
    private ImageSource? _image;

    private static ImageSource LoadImage(string fileName)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri($"pack://application:,,,/Assets/Maps/{fileName}", UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // load now so the stream isn't held open
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}

/// <summary>
/// Backs the Maps tab: the full list of bundled reference maps (grouped by category for the
/// dropdown) and the currently selected map to display — plus, once the X/Y Search tab has
/// narrowed the party position down to a single address, a live party marker drawn over the
/// map and click-to-teleport.
///
/// Because the bundled maps are scans, the trainer can't know where game cell (x, y) sits in
/// the image; the user calibrates each map once by standing somewhere in-game and clicking
/// that spot on the map, twice from two different positions (different row AND column). The
/// two anchors define the linear transform (<see cref="MapCalibration"/>), persisted per map
/// to %APPDATA%\MM1Trainer\map-calibration.json so it survives restarts.
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

        // Build the command before selecting a map: assigning SelectedMap runs
        // RefreshCalibrationState(), which calls ClearCalibrationCommand.RaiseCanExecuteChanged() —
        // so the command must already exist or that first refresh throws a NullReferenceException.
        ClearCalibrationCommand = new RelayCommand(ClearCalibration, () => CurrentAnchors.Count > 0);
        TeleportToInputCommand = new RelayCommand(TeleportToInput,
            () => HasPositionLock
                && InputParsing.TryParseByte(_goToX, MaxAreaCoord, out _)
                && InputParsing.TryParseByte(_goToY, MaxAreaCoord, out _));
        SelectedMap = items.FirstOrDefault();
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
        private set
        {
            if (!SetField(ref _hasPositionLock, value)) return;
            OnPropertyChanged(nameof(HintText));
            TeleportToInputCommand.RaiseCanExecuteChanged();   // typed teleport needs the lock
        }
    }

    private int _liveX = -1, _liveY = -1;
    /// <summary>"X 12 · Y 5" while locked; empty otherwise.</summary>
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

    // --- typed teleport ----------------------------------------------------------
    // A reliable alternative to clicking: type the destination cell and write it straight to
    // the locked address. Needs no calibration (you give game coordinates, not pixels), so it
    // works on the composite maps where a single linear calibration can't place a click — the
    // game stores the party position per 16x16 area, while most bundled maps show several at once.
    public RelayCommand TeleportToInputCommand { get; }

    private string _goToX = "";
    /// <summary>Destination X typed by the user (0–15, decimal or 0x…).</summary>
    public string GoToX
    {
        get => _goToX;
        set { if (SetField(ref _goToX, value)) TeleportToInputCommand.RaiseCanExecuteChanged(); }
    }

    private string _goToY = "";
    /// <summary>Destination Y typed by the user (0–15, decimal or 0x…).</summary>
    public string GoToY
    {
        get => _goToY;
        set { if (SetField(ref _goToY, value)) TeleportToInputCommand.RaiseCanExecuteChanged(); }
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
                return "Now click the party's exact spot on the map below.";
            if (_calibration == null)
                return "Position locked. Type a destination X/Y below and Teleport — no calibration needed. "
                     + "Or calibrate for the live marker: click ✛ Mark party position, then the party's spot on "
                     + "the map; repeat once from a different position (different row and column).";
            return _teleportOnClick
                ? "Teleport armed: click a cell in the party's current area to move there. (Across areas, type the X/Y below instead.)"
                : "Live marker active. Tick 🚀 Teleport on click, or type a destination X/Y below and Teleport.";
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
            SeedTeleportInputsIfEmpty();   // pre-fill the typed boxes once, so they start at "here"
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
        if (_teleportOnClick) Teleport(pixelX, pixelY);
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

    // The game stores each coordinate per 16x16 area, so a valid cell is 0–15. Most bundled
    // maps show several areas at once; a calibrated linear transform will happily resolve a
    // click in a neighbouring area to something like X 23 — writing that would corrupt the
    // position (and wouldn't move you to that area anyway, which the game tracks separately).
    private const int MaxAreaCoord = 15;

    private void Teleport(double pixelX, double pixelY)
    {
        var addr = _getPositionAddress();
        var mem = _getMem();
        if (_calibration == null || addr == null || mem == null) return;
        var (gx, gy) = _calibration.ToGame(pixelX, pixelY);
        if (gx is < 0 or > MaxAreaCoord || gy is < 0 or > MaxAreaCoord)
        {
            _setStatus("That spot is outside the party's 16×16 area. These maps often show several areas "
                + "at once — type the destination X/Y below to teleport instead.");
            return;
        }
        WritePosition(addr.Value, mem, gx, gy);
    }

    /// <summary>Teleport to the typed destination — needs only the position lock, not calibration.</summary>
    private void TeleportToInput()
    {
        var addr = _getPositionAddress();
        var mem = _getMem();
        if (addr == null || mem == null) { _setStatus("Can't teleport: the party position isn't locked yet."); return; }
        if (!InputParsing.TryParseByte(_goToX, MaxAreaCoord, out int gx)
            || !InputParsing.TryParseByte(_goToY, MaxAreaCoord, out int gy))
        {
            _setStatus($"Enter X and Y first (each 0–{MaxAreaCoord}).");
            return;
        }
        WritePosition(addr.Value, mem, gx, gy);
    }

    // Callers validate addr/mem and surface their own status on failure; taking them as
    // parameters keeps this from silently dropping a write it can't explain.
    private void WritePosition(nuint addr, ProcessMemory mem, int gx, int gy)
    {
        bool ok = mem.Write(addr, new[] { (byte)gx, (byte)gy });
        _setStatus(ok ? $"Teleported the party to X {gx} · Y {gy}." : "Teleport write failed.");
        if (ok) Tick();   // move the marker right away rather than on the next poll
    }

    private void SeedTeleportInputsIfEmpty()
    {
        if (_liveX < 0) return;
        if (string.IsNullOrWhiteSpace(_goToX)) GoToX = _liveX.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(_goToY)) GoToY = _liveY.ToString(CultureInfo.InvariantCulture);
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
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MM1Trainer", "map-calibration.json");

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
        catch { /* best-effort; a failed save shouldn't break the UI */ }
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
