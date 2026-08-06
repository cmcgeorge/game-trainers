using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheQuestTrainer.Game;

namespace TheQuestTrainer.ViewModels;

/// <summary>
/// One square of the current map's schematic.
///
/// The fill is sampled from the game's own world map picture where there is one, so an outdoor cell
/// shows its coastline and its roads rather than an empty grid; an interior, which the picture does
/// not cover, is drawn blank.
/// </summary>
public sealed class MapTileViewModel : ObservableObject
{
    private bool _isPlayer;
    private bool _isTarget;

    /// <summary>Column within the map, counting from its north-west corner.</summary>
    public int X { get; }

    /// <summary>Row within the map.</summary>
    public int Y { get; }

    /// <summary>Terrain colour lifted from the world map picture, or a neutral fill.</summary>
    public Brush Fill { get; }

    /// <summary>Builds a tile.</summary>
    public MapTileViewModel(int x, int y, Brush fill)
    {
        X = x;
        Y = y;
        Fill = fill;
    }

    /// <summary>Whether the player is standing here.</summary>
    public bool IsPlayer { get => _isPlayer; set => SetField(ref _isPlayer, value); }

    /// <summary>Whether this is where Teleport would put them.</summary>
    public bool IsTarget { get => _isTarget; set => SetField(ref _isTarget, value); }

    /// <summary>"12, 7" — shown on hover.</summary>
    public string Label => $"{X}, {Y}";
}

/// <summary>
/// Backs the 🗺 Map tab: where the player is, everywhere they could be, and one write that moves
/// them.
///
/// The three parts are deliberately separate, because they are read at very different rates and
/// mean different things:
///
/// <list type="bullet">
/// <item><b>The position</b> comes off the engine manager on every refresh. It is four numbers and
///   costs eight reads.</item>
/// <item><b>The atlas</b> — every map in the world, with its name, its cell and its flags — is read
///   out of the running game once on attach, the same way the item catalog is. It is a reference:
///   nothing on the tab writes to another map.</item>
/// <item><b>The picture</b> is the game's own world map, read out of the player's own install and
///   decoded. Everything works without it.</item>
/// </list>
///
/// <b>Teleport moves the player within the map they are on and nowhere else.</b> That is not
/// timidity — see <see cref="TrainerActions.Teleport"/>: a coordinate outside the current map lands
/// on a real tile of a neighbour while the engine goes on believing you are where you were, and
/// everything downstream of that belief is then wrong.
/// </summary>
public sealed class MapViewModel : ObservableObject
{
    private static readonly Brush Blank = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xDD, 0xC0)));

    private readonly IGameHost _host;

    private MapSnapshot? _where;
    private WorldPicture? _picture;
    private IReadOnlyList<WorldMap> _atlas = Array.Empty<WorldMap>();
    private string _builtFor = "";
    private int _builtWidth, _builtHeight;

    private string _worldName = "—";
    private string _mapName = "—";
    private string _mapId = "";
    private string _cellLabel = "—";
    private string _tileLabel = "—";
    private string _globalLabel = "—";
    private string _headingLabel = "—";
    private string _mapNote = "";
    private string _pictureNote = "The world map picture is read from your own copy of the game once you attach.";
    private string _atlasFilter = "";
    private WorldMap? _selectedMap;
    private BitmapSource? _pictureSource;
    private int _targetX;
    private int _targetY;
    private bool _hasPosition;

    /// <summary>Binds the tab to the session it writes through.</summary>
    public MapViewModel(IGameHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;

        Tiles = new ObservableCollection<MapTileViewModel>();
        AtlasView = new ObservableCollection<WorldMap>();

        TeleportCommand = new RelayCommand(Teleport, () => CanTeleport);
        HereCommand = new RelayCommand(SetTargetToHere, () => _hasPosition);
        SelectTileCommand = new RelayCommand(p => { if (p is MapTileViewModel t) SetTarget(t.X, t.Y); });
    }

    // ---- what the tab shows ----------------------------------------------------------------

    /// <summary>The current map's tiles, row by row from the north-west corner.</summary>
    public ObservableCollection<MapTileViewModel> Tiles { get; }

    /// <summary>Every map in the world, narrowed by <see cref="AtlasFilter"/>.</summary>
    public ObservableCollection<WorldMap> AtlasView { get; }

    /// <summary>Moves the player to (<see cref="TargetX"/>, <see cref="TargetY"/>).</summary>
    public RelayCommand TeleportCommand { get; }

    /// <summary>Puts the target back on the tile the player is standing on.</summary>
    public RelayCommand HereCommand { get; }

    /// <summary>Picks a tile out of the schematic as the target.</summary>
    public RelayCommand SelectTileCommand { get; }

    /// <summary>The world's name, e.g. <c>Freymore</c>.</summary>
    public string WorldName { get => _worldName; private set => SetField(ref _worldName, value); }

    /// <summary>The current map's name, e.g. <c>Port of Mithria</c>.</summary>
    public string MapName { get => _mapName; private set => SetField(ref _mapName, value); }

    /// <summary>The current map's internal id.</summary>
    public string MapId { get => _mapId; private set => SetField(ref _mapId, value); }

    /// <summary>"column 8, row 4" for an outdoor cell, or a note that this is an interior.</summary>
    public string CellLabel { get => _cellLabel; private set => SetField(ref _cellLabel, value); }

    /// <summary>The player's tile within the map, and how big the map is.</summary>
    public string TileLabel { get => _tileLabel; private set => SetField(ref _tileLabel, value); }

    /// <summary>The world-absolute tile, outdoors.</summary>
    public string GlobalLabel { get => _globalLabel; private set => SetField(ref _globalLabel, value); }

    /// <summary>Which way the player faces.</summary>
    public string HeadingLabel { get => _headingLabel; private set => SetField(ref _headingLabel, value); }

    /// <summary>What the current map's flags say, plus anything odd about the position.</summary>
    public string MapNote { get => _mapNote; private set => SetField(ref _mapNote, value); }

    /// <summary>Where the world map picture came from, or why there is not one.</summary>
    public string PictureNote { get => _pictureNote; private set => SetField(ref _pictureNote, value); }

    /// <summary>Whether a validated position is being shown.</summary>
    public bool HasPosition { get => _hasPosition; private set => SetField(ref _hasPosition, value); }

    /// <summary>Whether Teleport would do anything.</summary>
    public bool CanTeleport => _hasPosition && _host is { IsAttached: true, IsReadOnly: false };

    /// <summary>Columns in the schematic.</summary>
    public int TileColumns { get => _builtWidth; private set => SetField(ref _builtWidth, value); }

    /// <summary>Rows in the schematic.</summary>
    public int TileRows { get => _builtHeight; private set => SetField(ref _builtHeight, value); }

    // ---- the world map picture ---------------------------------------------------------------

    /// <summary>The game's own world map, decoded, or null when it could not be read.</summary>
    public BitmapSource? PictureSource { get => _pictureSource; private set => SetField(ref _pictureSource, value); }

    /// <summary>Whether there is a picture to draw the markers on.</summary>
    public bool HasPicture => _pictureSource is not null;

    /// <summary>Picture width in pixels, so the overlay can be laid out in picture coordinates.</summary>
    public double PictureWidth => _picture?.Image.Width ?? 0;

    /// <summary>Picture height in pixels.</summary>
    public double PictureHeight => _picture?.Image.Height ?? 0;

    /// <summary>
    /// Diameter of the "you are here" dot, in picture pixels. A tile is two pixels on Freymore's
    /// map, so the dot is deliberately several tiles wide — it has to stay findable once the whole
    /// 588-pixel picture is scaled into a panel a few hundred pixels tall.
    /// </summary>
    public double MarkerSize => Math.Max(10, (_picture?.PixelsPerTile ?? 2) * 5);

    /// <summary>Left edge of the "you are here" dot.</summary>
    public double PlayerPixelX => (_picture is { } p && _where?.GlobalX is { } g ? p.PixelX(g) : 0) - MarkerSize / 2;

    /// <summary>Top edge of the "you are here" dot.</summary>
    public double PlayerPixelY => (_picture is { } p && _where?.GlobalY is { } g ? p.PixelY(g) : 0) - MarkerSize / 2;

    /// <summary>Whether the player's cell can be drawn on the picture.</summary>
    public bool PlayerOnPicture => _picture is not null && _where?.GlobalX is not null;

    /// <summary>Left edge of the box around the map the player is on.</summary>
    public double HereBoxX => CellBox(_where?.Here).X;

    /// <summary>Top edge of that box.</summary>
    public double HereBoxY => CellBox(_where?.Here).Y;

    /// <summary>Side of a cell box, in picture pixels.</summary>
    public double CellBoxSize => (_picture?.PixelsPerTile ?? 2) * MapLayout.GridMapTiles;

    /// <summary>Whether a box can be drawn around the selected map.</summary>
    public bool SelectedOnPicture => _picture is not null && _selectedMap is { IsOutdoorCell: true };

    /// <summary>Left edge of the box around the selected map.</summary>
    public double SelectedBoxX => CellBox(_selectedMap).X;

    /// <summary>Top edge of that box.</summary>
    public double SelectedBoxY => CellBox(_selectedMap).Y;

    // ---- the atlas -----------------------------------------------------------------------------

    /// <summary>Substring the atlas is narrowed by; matched against the name and the internal id.</summary>
    public string AtlasFilter
    {
        get => _atlasFilter;
        set
        {
            if (!SetField(ref _atlasFilter, value)) return;
            ApplyAtlasFilter();
        }
    }

    /// <summary>The map picked in the atlas. Reference only — nothing teleports to another map.</summary>
    public WorldMap? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (!SetField(ref _selectedMap, value)) return;
            OnPropertyChanged(nameof(SelectedOnPicture));
            OnPropertyChanged(nameof(SelectedBoxX));
            OnPropertyChanged(nameof(SelectedBoxY));
        }
    }

    /// <summary>How many maps the world has, and how many the filter is showing.</summary>
    public string AtlasNote => _atlas.Count == 0
        ? "The world's maps are read from the game when you attach."
        : AtlasView.Count == _atlas.Count
            ? $"{_atlas.Count:N0} map(s) in this world."
            : $"{AtlasView.Count:N0} of {_atlas.Count:N0} map(s).";

    // ---- the teleport target ---------------------------------------------------------------------

    /// <summary>Column within the current map that Teleport would move the player to.</summary>
    public int TargetX
    {
        get => _targetX;
        set
        {
            if (!SetField(ref _targetX, Math.Max(0, value))) return;
            MarkTiles();
        }
    }

    /// <summary>Row within the current map.</summary>
    public int TargetY
    {
        get => _targetY;
        set
        {
            if (!SetField(ref _targetY, Math.Max(0, value))) return;
            MarkTiles();
        }
    }

    private void SetTarget(int x, int y)
    {
        SetField(ref _targetX, Math.Max(0, x), nameof(TargetX));
        SetField(ref _targetY, Math.Max(0, y), nameof(TargetY));
        MarkTiles();
    }

    private void SetTargetToHere()
    {
        if (_where is not { } here) return;
        SetTarget(here.LocalX, here.LocalY);
    }

    private void Teleport()
    {
        _host.Report(_host.Teleport(TargetX, TargetY).Message);
    }

    // ---- the session drives these -------------------------------------------------------------

    /// <summary>
    /// Takes a fresh position. Called on every refresh, so it does as little as it can get away with:
    /// the schematic is only rebuilt when the map underneath it actually changes.
    /// </summary>
    public void Update(MapSnapshot? where)
    {
        bool had = _hasPosition;
        _where = where;
        HasPosition = where is not null;

        if (where is null)
        {
            if (had) Clear();
            return;
        }

        WorldName = where.WorldName;
        MapName = where.Here.Name;
        MapId = where.Here.Id;
        CellLabel = where.Here.IsOutdoorCell
            ? $"column {where.Here.Column}, row {where.Here.Row}"
            : "an interior — not a cell of the outdoor grid";
        TileLabel = $"{where.LocalX}, {where.LocalY}   (of {where.Here.Width}×{where.Here.Height})";
        GlobalLabel = where.GlobalX is { } gx && where.GlobalY is { } gy
            ? $"{gx}, {gy}"
            : "—";
        HeadingLabel = where.HeadingLabel;
        MapNote = DescribeMap(where);

        // A first position, or one on a different map, arrives with the target still pointing at
        // wherever the last one was — which on a new map is a coordinate the player never chose and
        // may not even exist. Re-aiming it at their feet makes Teleport-with-nothing-typed a no-op.
        bool newMap = EnsureTiles(where);
        if (!had || newMap) SetTargetToHere();
        MarkTiles();

        OnPropertyChanged(nameof(PlayerOnPicture));
        OnPropertyChanged(nameof(PlayerPixelX));
        OnPropertyChanged(nameof(PlayerPixelY));
        OnPropertyChanged(nameof(HereBoxX));
        OnPropertyChanged(nameof(HereBoxY));
        OnPropertyChanged(nameof(CanTeleport));
        TeleportCommand.RaiseCanExecuteChanged();
        HereCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Takes the world's map list, read once on attach.</summary>
    public void SetAtlas(IReadOnlyList<WorldMap> atlas)
    {
        _atlas = atlas ?? Array.Empty<WorldMap>();
        ApplyAtlasFilter();
    }

    /// <summary>Takes the decoded world map picture, or null when there is not one.</summary>
    public void SetPicture(WorldPicture? picture, string note)
    {
        _picture = picture;
        PictureNote = note;
        PictureSource = picture is null ? null : ToBitmap(picture.Image);

        // The schematic's fills come out of the picture, so it has to be rebuilt when one arrives.
        _builtFor = "";
        if (_where is not null) { EnsureTiles(_where); MarkTiles(); }

        OnPropertyChanged(nameof(HasPicture));
        OnPropertyChanged(nameof(PictureWidth));
        OnPropertyChanged(nameof(PictureHeight));
        OnPropertyChanged(nameof(MarkerSize));
        OnPropertyChanged(nameof(CellBoxSize));
        OnPropertyChanged(nameof(PlayerOnPicture));
        OnPropertyChanged(nameof(PlayerPixelX));
        OnPropertyChanged(nameof(PlayerPixelY));
        OnPropertyChanged(nameof(HereBoxX));
        OnPropertyChanged(nameof(HereBoxY));
        OnPropertyChanged(nameof(SelectedOnPicture));
    }

    /// <summary>Drops everything that names a process that is no longer open.</summary>
    public void Clear()
    {
        _where = null;
        _atlas = Array.Empty<WorldMap>();
        _builtFor = "";
        HasPosition = false;
        Tiles.Clear();
        TileColumns = 0;
        TileRows = 0;
        AtlasView.Clear();
        SelectedMap = null;
        WorldName = "—";
        MapName = "—";
        MapId = "";
        CellLabel = "—";
        TileLabel = "—";
        GlobalLabel = "—";
        HeadingLabel = "—";
        MapNote = "";
        SetPicture(null, "The world map picture is read from your own copy of the game once you attach.");
        OnPropertyChanged(nameof(AtlasNote));
        OnPropertyChanged(nameof(CanTeleport));
        TeleportCommand.RaiseCanExecuteChanged();
        HereCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Re-queries the commands after the session's attached or read-only state changed.</summary>
    public void RaiseCommandStates()
    {
        OnPropertyChanged(nameof(CanTeleport));
        TeleportCommand.RaiseCanExecuteChanged();
        HereCommand.RaiseCanExecuteChanged();
    }

    // ---- plumbing --------------------------------------------------------------------------------

    private static string DescribeMap(MapSnapshot where)
    {
        var parts = new List<string>();
        if (where.Here.Notes.Length > 0) parts.Add(where.Here.Notes);
        parts.Add(where.Outdoors
            ? "Outdoors: the engine has this map and its eight neighbours loaded."
            : "Indoors: this map is loaded on its own.");
        if (!where.IsOnMap)
            parts.Add("You are standing outside this map's own tiles — walk a step to let the game " +
                      "work out which map you are really on.");
        return string.Join("  ·  ", parts);
    }

    /// <summary>
    /// Rebuilds the schematic when the map changes, and says whether it did. Keyed on the id and the
    /// size rather than done every tick: this is 441 squares for an outdoor cell and 1,225 for an
    /// interior, and the refresh runs four times a second.
    /// </summary>
    private bool EnsureTiles(MapSnapshot where)
    {
        string key = $"{where.Here.Id}:{where.Here.Width}x{where.Here.Height}";
        if (key == _builtFor) return false;

        _builtFor = key;
        Tiles.Clear();
        TileColumns = where.Here.Width;
        TileRows = where.Here.Height;

        for (int y = 0; y < where.Here.Height; y++)
            for (int x = 0; x < where.Here.Width; x++)
                Tiles.Add(new MapTileViewModel(x, y, TileFill(where.Here, x, y)));

        return true;
    }

    /// <summary>
    /// The colour of one tile, lifted from the world map picture where it covers this map. The
    /// picture is a plan of the outdoor grid at a fixed number of pixels per tile, so the sample is
    /// the pixel at the middle of the tile.
    /// </summary>
    private Brush TileFill(WorldMap map, int x, int y)
    {
        if (_picture is not { } picture || map.OriginX is not { } ox || map.OriginY is not { } oy)
            return Blank;

        int px = (int)picture.PixelX(ox + x);
        int py = (int)picture.PixelY(oy + y);
        uint argb = picture.Image.Pixel(px, py);
        if (argb == 0) return Blank;

        return Freeze(new SolidColorBrush(Color.FromRgb(
            (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)));
    }

    private void MarkTiles()
    {
        int hereX = _where?.LocalX ?? -1;
        int hereY = _where?.LocalY ?? -1;
        foreach (var tile in Tiles)
        {
            tile.IsPlayer = tile.X == hereX && tile.Y == hereY;
            tile.IsTarget = tile.X == _targetX && tile.Y == _targetY;
        }
    }

    private (double X, double Y) CellBox(WorldMap? map)
    {
        if (_picture is not { } picture || map?.OriginX is not { } ox || map.OriginY is not { } oy)
            return (0, 0);
        return (ox * picture.PixelsPerTile, oy * picture.PixelsPerTile);
    }

    private void ApplyAtlasFilter()
    {
        var previous = SelectedMap;
        string needle = _atlasFilter.Trim();

        var matches = _atlas
            .Where(m => needle.Length == 0
                     || m.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                     || m.Id.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.IsOutdoorCell ? 0 : 1)
            .ThenBy(m => m.Row ?? 0)
            .ThenBy(m => m.Column ?? 0)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase);

        AtlasView.Clear();
        foreach (var map in matches) AtlasView.Add(map);

        SelectedMap = previous is not null && AtlasView.Contains(previous) ? previous : null;
        OnPropertyChanged(nameof(AtlasNote));
    }

    private static BitmapSource ToBitmap(DecodedImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96,
            PixelFormats.Bgra32, null, image.Bgra, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
