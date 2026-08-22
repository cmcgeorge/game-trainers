using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// Backs the "Map (drawn)" tab: renders all 55 of the game's mazes as crisp vector graphics
/// (no scanned images, no calibration), detects the <em>current</em> map automatically by
/// fingerprinting the game's live maze buffer against the known records, overlays the live
/// party cell, and teleports the party to a clicked cell (exact cells, no calibration).
///
/// <para>The maps come from <see cref="BuiltInMazes"/> out of the box, so every area is there
/// before the player has pointed at anything. Loading their own <c>Mazedata.dta</c> upgrades
/// them to the exact bytes, which is what makes current-map detection a byte-for-byte match
/// rather than the near-match the bundled layouts have to settle for.</para>
///
/// Position source: once the data segment is located and the X/Y address has been locked
/// once (📍 X/Y Search), the trainer learns the party-position DS offset and reads the
/// marker straight from the data segment thereafter — so the lock isn't needed again.
/// </summary>
public sealed class DrawnMapViewModel : ObservableObject
{
    public const int CellPx = 30;
    public double BoardSize => MazeMap.Size * CellPx;   // 480

    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<nuint?> _getPositionLock;
    private readonly Func<DataSegment?> _getDataSeg;
    private readonly Action<string> _setStatus;

    public DrawnMapViewModel(Func<ProcessMemory?> getMem, Func<nuint?> getPositionLock,
        Func<DataSegment?> getDataSeg, Action<string> setStatus)
    {
        _getMem = getMem;
        _getPositionLock = getPositionLock;
        _getDataSeg = getDataSeg;
        _setStatus = setStatus;

        TeleportToInputCommand = new RelayCommand(TeleportToInput,
            () => CurrentPositionAddress() != null
                  && InputParsing.TryParseByte(_goToX, 15, out _)
                  && InputParsing.TryParseByte(_goToY, 15, out _));

        Load();
    }

    public ObservableCollection<MazeMap> Maps { get; } = new();

    private MazeData? _maze;
    public bool HasMaze => _maze != null;

    private string _mazeStatus = "";
    public string MazeStatus { get => _mazeStatus; private set => SetField(ref _mazeStatus, value); }

    private MazeMap? _selectedMap;
    public MazeMap? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (!SetField(ref _selectedMap, value)) return;
            Render();
            UpdateMarker();
            SaveSettings();
        }
    }

    private ImageSource? _board;
    public ImageSource? Board { get => _board; private set => SetField(ref _board, value); }

    // --- current-map auto-detection ---------------------------------------------
    private bool _autoFollow = true;
    /// <summary>When on, the selected map tracks the game's live maze (fingerprint match).</summary>
    public bool AutoFollowCurrentMap
    {
        get => _autoFollow;
        set { if (SetField(ref _autoFollow, value)) SaveSettings(); }
    }

    private string _detectedText = "";
    public string DetectedText { get => _detectedText; private set => SetField(ref _detectedText, value); }

    // Upper bound for a DS-relative offset we'll trust (the data segment is well under this).
    // Anything outside [0, MaxDsOffset) is rejected so a persisted/learned offset can never be
    // turned into a write to an arbitrary process address.
    private const int MaxDsOffset = 0x20000;
    private static bool IsValidDsOffset(int? off) => off is int o && o >= 0 && o < MaxDsOffset;

    private int? _mazeBufferOffset;   // DS offset where the live maze buffer was found
    private int _detCounter;

    // --- live position ----------------------------------------------------------
    private int? _learnedPosOffset;   // DS offset of the party X/Y pair (self-derived)
    private int? _rejectedPosOffset;  // a learned offset that read junk — suppressed until a valid coord reads again

    private bool _hasPositionLock;
    public bool HasPositionLock
    {
        get => _hasPositionLock;
        private set
        {
            if (!SetField(ref _hasPositionLock, value)) return;
            OnPropertyChanged(nameof(HintText));
            TeleportToInputCommand.RaiseCanExecuteChanged();
        }
    }

    private int _liveX = -1, _liveY = -1;
    public string LivePositionText =>
        _hasPositionLock && _liveX >= 0 ? $"party at X {_liveX} · Y {_liveY}" : "";

    private bool _exportPassability = true;
    public bool ExportPassability
    {
        get => _exportPassability;
        set
        {
            if (SetField(ref _exportPassability, value) && value)
            {
                ExportGraphics = false;
            }
        }
    }

    private bool _exportGraphics;
    public bool ExportGraphics
    {
        get => _exportGraphics;
        set
        {
            if (SetField(ref _exportGraphics, value) && value)
            {
                ExportPassability = false;
            }
        }
    }

    private double _markerX, _markerY;
    public double MarkerX { get => _markerX; private set => SetField(ref _markerX, value); }
    public double MarkerY { get => _markerY; private set => SetField(ref _markerY, value); }

    private bool _markerVisible;
    public bool MarkerVisible { get => _markerVisible; private set => SetField(ref _markerVisible, value); }

    private bool _teleportOnClick;
    public bool TeleportOnClick
    {
        get => _teleportOnClick;
        set { if (SetField(ref _teleportOnClick, value)) OnPropertyChanged(nameof(HintText)); }
    }

    public string HintText => !_hasPositionLock
        ? "Auto-map needs the game attached. For the live cell + teleport, lock the party position once via 📍 X / Y Search — then it's remembered."
        : _teleportOnClick
            ? "Teleport armed: click a cell to move the party there (exact cell — no calibration)."
            : "Live cell shown below. Tick 🚀 Teleport on click, or type a destination X/Y and Teleport.";

    public RelayCommand TeleportToInputCommand { get; }

    private string _goToX = "";
    public string GoToX { get => _goToX; set { if (SetField(ref _goToX, value)) TeleportToInputCommand.RaiseCanExecuteChanged(); } }

    private string _goToY = "";
    public string GoToY { get => _goToY; set { if (SetField(ref _goToY, value)) TeleportToInputCommand.RaiseCanExecuteChanged(); } }

    // --- file loading -----------------------------------------------------------
    public void LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) { MazeStatus = $"Not found: {path}"; return; }
            var data = MazeData.FromBytes(File.ReadAllBytes(path));
            if (data == null) { MazeStatus = "That file isn't a 28,160-byte Mazedata.dta."; return; }

            _mazedataPath = path;
            Adopt(data);
            MazeStatus = $"Loaded all {Maps.Count} maps from {Path.GetFileName(path)} — exact data, "
                + "so the current map is detected byte for byte.";
            SaveSettings();
        }
        catch (Exception ex)
        {
            MazeStatus = "Failed to read Mazedata.dta: " + ex.Message;
        }
    }

    /// <summary>
    /// Falls back to the bundled layouts, which cover every area of the game. They draw the
    /// same walls, doors and secret edges; what they cannot do is fingerprint the live maze
    /// byte for byte, so current-map detection settles for a near-match.
    ///
    /// <para>Whatever <c>Mazedata.dta</c> the user last chose is deliberately left remembered.
    /// A path can be missing for a session — an unmounted drive, a game folder being moved —
    /// and forgetting it here would persist that as "no file", silently downgrading them to
    /// the bundled data for good.</para>
    /// </summary>
    private void UseBuiltIn()
    {
        Adopt(MazeData.BuiltIn());
        MazeStatus = $"Showing the bundled layouts of all {Maps.Count} areas. Load your own "
            + "Mazedata.dta for the exact data and byte-for-byte current-map detection.";
    }

    private void Adopt(MazeData data)
    {
        _maze = data;
        _mazeBufferOffset = null;   // re-fingerprint against the freshly adopted set
        Maps.Clear();
        foreach (var m in data.Maps) Maps.Add(m);
        OnPropertyChanged(nameof(HasMaze));
        SelectedMap = Maps.ElementAtOrDefault(_savedIndex) ?? Maps.FirstOrDefault();
    }

    // --- per-tick poll (driven by the main timer) -------------------------------
    public void Tick()
    {
        LearnPositionOffsetIfPossible();

        var addr = CurrentPositionAddress();
        var mem = _getMem();
        HasPositionLock = addr != null && mem != null;
        if (HasPositionLock)
        {
            var b = mem!.Read(addr!.Value, 2);
            if (b.Length == 2)
            {
                // MM1 cell coords are 0–15, so a wildly out-of-range read means the address is
                // wrong. If it came from a learned offset, that offset is stale — drop it AND
                // remember it so LearnPositionOffsetIfPossible doesn't immediately re-derive the
                // same bad value (which would spam SaveSettings/status every tick). Either way,
                // show no live cell rather than a stale marker.
                if (b[0] > 200 || b[1] > 200)
                {
                    if (_learnedPosOffset is int stale) { _rejectedPosOffset = stale; _learnedPosOffset = null; }
                    if (_liveX != -1) { _liveX = _liveY = -1; OnPropertyChanged(nameof(LivePositionText)); }
                }
                else
                {
                    // A valid in-range read clears any prior rejection, so a transient junk read (or a
                    // re-attach at a new segment base) can recover and re-learn rather than stay blocked.
                    _rejectedPosOffset = null;
                    if (b[0] != _liveX || b[1] != _liveY)
                    {
                        _liveX = b[0];
                        _liveY = b[1];
                        OnPropertyChanged(nameof(LivePositionText));
                        SeedInputsIfEmpty();
                    }
                }
            }
            else if (_liveX != -1)
            {
                // Short/failed read: the current cell is unknown, so don't keep showing the
                // last position as if it were live.
                _liveX = _liveY = -1;
                OnPropertyChanged(nameof(LivePositionText));
            }
            UpdateMarker();
        }
        else
        {
            if (_liveX != -1) { _liveX = _liveY = -1; OnPropertyChanged(nameof(LivePositionText)); }
            MarkerVisible = false;
        }

        if (++_detCounter >= 13)   // ~2 s at the 150 ms timer
        {
            _detCounter = 0;
            DetectCurrentMap();
        }
    }

    // Once the data segment is located and the X/Y address has been locked, the party-position
    // DS offset is just (locked address − segment base). Learn and remember it.
    private void LearnPositionOffsetIfPossible()
    {
        if (_learnedPosOffset != null) return;
        var ds = _getDataSeg();
        var lockAddr = _getPositionLock();
        if (ds == null || lockAddr == null) return;
        long off = (long)lockAddr.Value - (long)ds.BaseAddress;
        // Skip an offset we already rejected this session as junk-reading, so a bad lock can't
        // oscillate (drop → re-learn → drop) and spam the settings file + status line.
        if (off >= 0 && off < MaxDsOffset && (int)off != _rejectedPosOffset)
        {
            _learnedPosOffset = (int)off;
            SaveSettings();
            _setStatus($"Drawn map: learned the party-position offset (DS 0x{off:X}) — the live cell is now automatic.");
        }
    }

    private nuint? CurrentPositionAddress()
    {
        var ds = _getDataSeg();
        if (ds != null && _learnedPosOffset is int o) return ds.BaseAddress + (nuint)o;
        return _getPositionLock();
    }

    private bool _isScanning;

    // Identify the current map by matching the live maze buffer to a known record.
    // Runs on the UI thread (timer); the cheap cached re-check stays here, but the full
    // ~96 KB read + fingerprint scan is offloaded so it can't hitch the UI.
    private void DetectCurrentMap()
    {
        var ds = _getDataSeg();
        var mem = _getMem();
        if (_maze == null || ds == null || mem == null) return;

        // Fast path: re-check the cached buffer location first (256 bytes — cheap).
        if (_mazeBufferOffset is int mo)
        {
            var win = mem.Read(ds.BaseAddress + (nuint)mo, MazeData.FingerprintLength);
            int hit = win.Length == MazeData.FingerprintLength ? _maze.MatchAt(win, 0) : -1;
            if (hit >= 0) { SetDetected(hit); return; }
            _mazeBufferOffset = null;   // moved / mid-load — fall through to a rescan
        }

        // Slow path: scan the whole data segment for the live maze on a pool thread; only the
        // result is marshalled back. One scan at a time. (mem.Read is detach-safe.)
        if (_isScanning) return;
        _isScanning = true;
        var maze = _maze;
        var baseAddr = ds.BaseAddress;
        Task.Run(() =>
        {
            var buf = mem.Read(baseAddr, 0x18000);
            return buf.Length < MazeData.FingerprintLength ? (-1, -1) : maze.FindInBuffer(buf);
        }).ContinueWith(t =>
        {
            _isScanning = false;
            if (!t.IsCompletedSuccessfully) return;
            var (map, off) = t.Result;
            if (map >= 0) { _mazeBufferOffset = off; SaveSettings(); SetDetected(map); }
            else DetectedText = maze.IsExact
                ? "current map not detected (move a step, or you're on a sub-screen)"
                : "current map not detected (move a step, or you're on a sub-screen) — loading Mazedata.dta detects it exactly";
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SetDetected(int index)
    {
        var m = Maps.ElementAtOrDefault(index);
        if (m == null) return;
        DetectedText = $"detected current map: {m.DisplayName}";
        if (_autoFollow && _selectedMap?.Index != index) SelectedMap = m;
    }

    private void UpdateMarker()
    {
        if (_selectedMap == null || _liveX is < 0 or >= MazeMap.Size || _liveY is < 0 or >= MazeMap.Size)
        {
            MarkerVisible = false;
            return;
        }
        MarkerX = (_liveX + 0.5) * CellPx;
        MarkerY = (MazeMap.Size - 1 - _liveY + 0.5) * CellPx;   // y=0 at the bottom
        MarkerVisible = true;
    }

    public void OnBoardClicked(double px, double py)
    {
        if (_selectedMap == null) return;
        int x = Math.Clamp((int)(px / CellPx), 0, MazeMap.Size - 1);
        int yFromTop = Math.Clamp((int)(py / CellPx), 0, MazeMap.Size - 1);
        int y = MazeMap.Size - 1 - yFromTop;
        if (_teleportOnClick) WritePosition(x, y);
        else _setStatus($"Cell X {x} · Y {y} on {_selectedMap.DisplayName}. (Tick 🚀 Teleport on click to jump here.)");
    }

    private void TeleportToInput()
    {
        if (!InputParsing.TryParseByte(_goToX, 15, out int x) || !InputParsing.TryParseByte(_goToY, 15, out int y))
        {
            _setStatus("Enter X and Y first (each 0–15).");
            return;
        }
        WritePosition(x, y);
    }

    private void WritePosition(int x, int y)
    {
        var addr = CurrentPositionAddress();
        var mem = _getMem();
        if (addr == null || mem == null) { _setStatus("Can't teleport: lock the party position once via 📍 X / Y Search first."); return; }
        bool ok = mem.Write(addr.Value, new[] { (byte)x, (byte)y });
        _setStatus(ok ? $"Teleported the party to X {x} · Y {y}." : "Teleport write failed.");
        if (ok)
        {
            // Reflect the move immediately without re-running the whole Tick (which would also
            // advance the map-detection counter off-cadence).
            _liveX = x; _liveY = y;
            OnPropertyChanged(nameof(LivePositionText));
            UpdateMarker();
        }
    }

    private void SeedInputsIfEmpty()
    {
        if (_liveX < 0) return;
        if (string.IsNullOrWhiteSpace(_goToX)) GoToX = _liveX.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(_goToY)) GoToY = _liveY.ToString(CultureInfo.InvariantCulture);
    }

    // --- rendering --------------------------------------------------------------
    private static readonly Brush BgBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x21)));
    private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x2A, 0x2C, 0x36), 1);
    private static readonly Pen WallPen = FrozenPen(Color.FromRgb(0xC8, 0xCC, 0xD4), 2.4);
    private static readonly Pen DoorPen = FrozenPen(Color.FromRgb(0xE0, 0xB3, 0x41), 3.0);
    private static readonly Pen SpecialPen = FrozenPen(Color.FromRgb(0x5A, 0xD1, 0xC8), 2.4, dashed: true);
    private static readonly Pen IllusoryPen = FrozenPen(Color.FromRgb(0x6A, 0x70, 0x80), 1.4, dashed: true);

    private void Render()
    {
        if (_selectedMap is not { } map) { Board = null; return; }
        int n = MazeMap.Size;
        double size = n * CellPx;

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(BgBrush, null, new RectangleGeometry(new Rect(0, 0, size, size))));

        var grid = new GeometryGroup();
        for (int i = 0; i <= n; i++)
        {
            grid.Children.Add(new LineGeometry(new Point(i * CellPx, 0), new Point(i * CellPx, size)));
            grid.Children.Add(new LineGeometry(new Point(0, i * CellPx), new Point(size, i * CellPx)));
        }
        group.Children.Add(new GeometryDrawing(null, GridPen, grid));

        var walls = new GeometryGroup();
        var doors = new GeometryGroup();
        var specials = new GeometryGroup();
        var illusory = new GeometryGroup();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                double left = x * CellPx;
                double top = (n - 1 - y) * CellPx;   // y=0 at the bottom
                double right = left + CellPx;
                double bottom = top + CellPx;

                for (int dir = 0; dir < 4; dir++)
                {
                    var (a, b) = dir switch
                    {
                        0 => (new Point(left, top), new Point(left, bottom)),     // W
                        1 => (new Point(left, top), new Point(right, top)),       // N
                        2 => (new Point(right, top), new Point(right, bottom)),   // E
                        _ => (new Point(left, bottom), new Point(right, bottom)), // S
                    };
                    var line = new LineGeometry(a, b);
                    switch (map.Edge(x, y, dir))
                    {
                        case EdgeKind.Wall: walls.Children.Add(line); break;
                        case EdgeKind.Door: doors.Children.Add(line); break;
                        case EdgeKind.Special: specials.Children.Add(line); break;
                        default:
                            if (map.IsIllusory(x, y, dir)) illusory.Children.Add(line);
                            break;
                    }
                }
            }
        }

        group.Children.Add(new GeometryDrawing(null, IllusoryPen, illusory));
        group.Children.Add(new GeometryDrawing(null, WallPen, walls));
        group.Children.Add(new GeometryDrawing(null, DoorPen, doors));
        group.Children.Add(new GeometryDrawing(null, SpecialPen, specials));

        var img = new DrawingImage(group);
        img.Freeze();
        Board = img;
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }

    private static Pen FrozenPen(Color c, double thickness, bool dashed = false)
    {
        var pen = new Pen(new SolidColorBrush(c), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (dashed) pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
        pen.Freeze();
        return pen;
    }

    public void ExportCurrentMap(string filePath)
    {
        if (_selectedMap == null) return;

        var ds = _getDataSeg();
        var mem = _getMem();
        byte[]? buffer = null;
        bool isFromMemory = false;

        if (ds != null && mem != null && _mazeBufferOffset is int mo)
        {
            var readBytes = mem.Read(ds.BaseAddress + (nuint)mo, 512);
            if (readBytes.Length == 512)
            {
                buffer = readBytes;
                isFromMemory = true;
            }
        }

        if (buffer == null)
        {
            buffer = new byte[512];
            if (_selectedMap.Plane1 != null)
            {
                Array.Copy(_selectedMap.Plane1, 0, buffer, 0, 256);
            }
            if (_selectedMap.Plane2 != null)
            {
                Array.Copy(_selectedMap.Plane2, 0, buffer, 256, 256);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======================================================================");
        sb.AppendLine("MIGHT & MAGIC 1 - MAZE LAYOUT EXPORT");
        sb.AppendLine("======================================================================");
        sb.AppendLine($"Map Index:      {_selectedMap.Index}");
        sb.AppendLine($"Map Name:       {_selectedMap.DisplayName}");
        sb.AppendLine($"Export Type:    {(ExportPassability ? "Passability Plane (Plane 2)" : "Graphics Plane (Plane 1)")}");
        sb.AppendLine($"Source:         {(isFromMemory ? "Live Game Memory (DOSBox)" : "Parsed Trainer Database")}");
        sb.AppendLine($"Date/Time:      {DateTime.Now.ToString("g", CultureInfo.InvariantCulture)}");
        sb.AppendLine("======================================================================");
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("1. 16x16 Hex Grid (arranged North-up: Y=15 at the top, Y=0 at the bottom)");
        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("    X=  0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15");
        sb.AppendLine("       -----------------------------------------------------------");

        byte[] plane1 = new byte[256];
        byte[] plane2 = new byte[256];
        Array.Copy(buffer, 0, plane1, 0, 256);
        Array.Copy(buffer, 256, plane2, 0, 256);

        byte[] selectedPlane = ExportPassability ? plane2 : plane1;

        for (int y = 15; y >= 0; y--)
        {
            sb.Append($"Y={y:D2} |");
            for (int x = 0; x < 16; x++)
            {
                byte val = selectedPlane[y * 16 + x];
                sb.Append($" {val:X2} ");
            }
            sb.AppendLine();
        }
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("2. 33x33 ASCII Art Map");
        sb.AppendLine("----------------------------------------------------------------------");

        if (ExportGraphics && !isFromMemory && _selectedMap.Plane1 == null)
        {
            sb.AppendLine("[Notice: Raw graphic data (Plane 1) is not available for this bundled layout.]");
            sb.AppendLine("[Load your own Mazedata.dta or attach to the live game to view/export it.]");
        }
        else
        {
            char[,] grid = new char[33, 33];
            for (int r = 0; r < 33; r++)
            {
                for (int c = 0; c < 33; c++)
                {
                    grid[r, c] = ' ';
                }
            }

            for (int r = 0; r <= 16; r++)
            {
                for (int c = 0; c <= 16; c++)
                {
                    grid[r * 2, c * 2] = '+';
                }
            }

            for (int y = 0; y < 16; y++)
            {
                int r = 15 - y;
                for (int x = 0; x < 16; x++)
                {
                    byte v1 = plane1[y * 16 + x];
                    byte v2 = plane2[y * 16 + x];

                    byte val = ExportPassability ? v2 : v1;

                    byte w = (byte)((val >> 0) & 3);
                    byte n = (byte)((val >> 2) & 3);
                    byte e = (byte)((val >> 4) & 3);
                    byte s = (byte)((val >> 6) & 3);

                    grid[2 * r + 1, 2 * x] = GetGlyph(w, (byte)((v1 >> 0) & 3), (byte)((v2 >> 0) & 3), ExportPassability);
                    grid[2 * r, 2 * x + 1] = GetGlyph(n, (byte)((v1 >> 2) & 3), (byte)((v2 >> 2) & 3), ExportPassability);
                    grid[2 * r + 1, 2 * x + 2] = GetGlyph(e, (byte)((v1 >> 4) & 3), (byte)((v2 >> 4) & 3), ExportPassability);
                    grid[2 * r + 2, 2 * x + 1] = GetGlyph(s, (byte)((v1 >> 6) & 3), (byte)((v2 >> 6) & 3), ExportPassability);
                }
            }

            for (int r = 0; r < 33; r++)
            {
                for (int c = 0; c < 33; c++)
                {
                    sb.Append(grid[r, c]);
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Legend:");
            sb.AppendLine("  + : Grid corners");
            if (ExportPassability)
            {
                sb.AppendLine("  # : Solid Wall");
                sb.AppendLine("  D : Door (passable)");
                sb.AppendLine("  S : Special Edge (secret door, one-way, or trigger)");
                sb.AppendLine("  o : Illusory Wall (drawn as wall, but passable/open)");
                sb.AppendLine("    : Open / passable space");
            }
            else
            {
                sb.AppendLine("  1 : Graphic Type 1");
                sb.AppendLine("  2 : Graphic Type 2");
                sb.AppendLine("  3 : Graphic Type 3");
                sb.AppendLine("    : No wall graphic drawn");
            }
        }
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine("3. Cell-by-Cell Detailed Breakdown");
        sb.AppendLine("----------------------------------------------------------------------");

        for (int y = 15; y >= 0; y--)
        {
            for (int x = 0; x < 16; x++)
            {
                byte v1 = plane1[y * 16 + x];
                byte v2 = plane2[y * 16 + x];

                byte val = ExportPassability ? v2 : v1;

                byte w = (byte)((val >> 0) & 3);
                byte n = (byte)((val >> 2) & 3);
                byte e = (byte)((val >> 4) & 3);
                byte s = (byte)((val >> 6) & 3);

                sb.AppendLine($"[X={x:D2}, Y={y:D2}] Byte: 0x{val:X2}");
                if (ExportPassability)
                {
                    sb.AppendLine($"  - West:  {DescribePassabilityEdge(w, (byte)((v1 >> 0) & 3))}");
                    sb.AppendLine($"  - North: {DescribePassabilityEdge(n, (byte)((v1 >> 2) & 3))}");
                    sb.AppendLine($"  - East:  {DescribePassabilityEdge(e, (byte)((v1 >> 4) & 3))}");
                    sb.AppendLine($"  - South: {DescribePassabilityEdge(s, (byte)((v1 >> 6) & 3))}");
                }
                else
                {
                    sb.AppendLine($"  - West:  {DescribeGraphicsEdge(w)}");
                    sb.AppendLine($"  - North: {DescribeGraphicsEdge(n)}");
                    sb.AppendLine($"  - East:  {DescribeGraphicsEdge(e)}");
                    sb.AppendLine($"  - South: {DescribeGraphicsEdge(s)}");
                }
            }
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private char GetGlyph(byte val, byte gVal, byte pVal, bool isPassability)
    {
        if (isPassability)
        {
            return val switch
            {
                1 => '#',
                2 => 'D',
                3 => 'S',
                _ => gVal != 0 ? 'o' : ' '
            };
        }
        else
        {
            return val switch
            {
                1 => '1',
                2 => '2',
                3 => '3',
                _ => ' '
            };
        }
    }

    private string DescribePassabilityEdge(byte val, byte gVal)
    {
        return val switch
        {
            1 => "Solid Wall",
            2 => "Door (passable)",
            3 => "Special Edge (secret door, one-way, or trigger)",
            _ => gVal != 0 ? "Open (Illusory Wall - graphic drawn but passable)" : "Open Space"
        };
    }

    private string DescribeGraphicsEdge(byte val)
    {
        return val switch
        {
            1 => "Wall Graphic Type 1",
            2 => "Wall Graphic Type 2",
            3 => "Wall Graphic Type 3",
            _ => "No wall graphic drawn"
        };
    }

    // --- persistence ------------------------------------------------------------
    private string? _mazedataPath;
    private int _savedIndex;

    private sealed class Persisted
    {
        public string? Path { get; set; }
        public int Index { get; set; }
        public bool AutoFollow { get; set; } = true;
        public int? PosOffset { get; set; }
        public int? MazeBufferOffset { get; set; }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MM1Trainer", "drawnmap.json");

    private void Load()
    {
        string? path = null;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var p = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(SettingsPath));
                if (p != null)
                {
                    path = p.Path;
                    _savedIndex = Math.Max(0, p.Index);
                    _autoFollow = p.AutoFollow;
                    // Only trust offsets that fall inside the data segment. The settings file
                    // lives in user-writable %APPDATA%, so an out-of-range value here must never
                    // be allowed to become a teleport-write to an arbitrary address.
                    _learnedPosOffset = IsValidDsOffset(p.PosOffset) ? p.PosOffset : null;
                    _mazeBufferOffset = IsValidDsOffset(p.MazeBufferOffset) ? p.MazeBufferOffset : null;
                }
            }
        }
        catch { /* fall through to default */ }

        // Adopt() clears the buffer offset, since a freshly adopted set has to be re-fingerprinted;
        // hold the remembered one across that so a returning session skips the first full scan.
        // If it turns out to be stale — the game relaid its segment, or the offset was recorded
        // against the other kind of plane — the fast-path re-check simply misses and rescans.
        int? rememberedBufferOffset = _mazeBufferOffset;

        path ??= DefaultMazedataPath();
        if (path != null && File.Exists(path)) LoadFrom(path);

        // A remembered path that has since been deleted, replaced or truncated leaves LoadFrom
        // with nothing to show; keep whatever it complained about and fall back rather than
        // opening on an empty picker.
        if (_maze == null)
        {
            string complaint = MazeStatus;
            UseBuiltIn();
            if (complaint.Length > 0) MazeStatus = $"{complaint} {MazeStatus}";
        }

        _mazeBufferOffset = rememberedBufferOffset;
    }

    private static string? DefaultMazedataPath() => null;

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Persisted
            {
                Path = _mazedataPath,
                Index = _selectedMap?.Index ?? 0,
                AutoFollow = _autoFollow,
                PosOffset = _learnedPosOffset,
                MazeBufferOffset = _mazeBufferOffset,
            }));
        }
        catch { /* best-effort */ }
    }
}
