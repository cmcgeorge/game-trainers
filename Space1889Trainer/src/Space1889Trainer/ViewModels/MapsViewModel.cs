using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Space1889Trainer.Files;
using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>A label drawn over the map image (its description is shown by hovering the square).</summary>
public sealed record MapMarker(double X, double Y, string Text, string Kind);

/// <summary>
/// The Maps tab: every map in the player's own A.SYS/B.SYS/0.SYS, drawn with the game's tiles or as a schematic,
/// with NPCs, exits and shops marked. When attached it follows the party and a click on a walkable square of the
/// party's current map teleports there.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private MapLibrary? _library;
    private GameMap? _map;
    private int _renderedPlanet = -1;
    private int _lastPartyMapId = -1;
    private IReadOnlyList<MapAnnotation> _annotations = Array.Empty<MapAnnotation>();

    public MapsViewModel(MainViewModel main)
    {
        _main = main;
        BrowseCommand = new RelayCommand(Browse);
        GoToPartyCommand = new RelayCommand(() => SelectPartyMap(), () => _main.IsAttached);
    }

    public RelayCommand BrowseCommand { get; }
    public RelayCommand GoToPartyCommand { get; }

    public ObservableCollection<MapEntry> Maps { get; } = new();

    public ObservableCollection<MapMarker> Markers { get; } = new();

    public ObservableCollection<string> Details { get; } = new();

    public static IReadOnlyList<int> Zooms { get; } = new[] { 4, 8, 16 };

    public static IReadOnlyList<MapRenderMode> RenderModes { get; } = new[] { MapRenderMode.Tiles, MapRenderMode.Schematic };

    public string GameFolder
    {
        get => _main.GameFolder;
        set => _main.GameFolder = value;
    }

    private MapEntry? _selectedMap;
    public MapEntry? SelectedMap
    {
        get => _selectedMap;
        set { if (SetField(ref _selectedMap, value)) Render(); }
    }

    private MapRenderMode _mode = MapRenderMode.Tiles;
    public MapRenderMode Mode
    {
        get => _mode;
        set { if (SetField(ref _mode, value)) Render(); }
    }

    private int _zoom = 8;
    public int Zoom
    {
        get => _zoom;
        set { if (SetField(ref _zoom, value)) Render(); }
    }

    private bool _followParty = true;
    public bool FollowParty
    {
        get => _followParty;
        set { if (SetField(ref _followParty, value) && value) SelectPartyMap(); }
    }

    private bool _showNpcs = true;
    public bool ShowNpcs
    {
        get => _showNpcs;
        set { if (SetField(ref _showNpcs, value)) BuildMarkers(); }
    }

    private bool _allowUnsafeTeleport;
    /// <summary>Lets a click land on a wall, water or trigger square (the game may leave you stuck).</summary>
    public bool AllowUnsafeTeleport { get => _allowUnsafeTeleport; set => SetField(ref _allowUnsafeTeleport, value); }

    private BitmapSource? _image;
    public BitmapSource? Image { get => _image; private set => SetField(ref _image, value); }

    private string _info = "Set the game folder to browse the maps.";
    public string Info { get => _info; private set => SetField(ref _info, value); }

    private string _hover = "";
    public string Hover { get => _hover; private set => SetField(ref _hover, value); }

    private bool _partyVisible;
    public bool PartyVisible { get => _partyVisible; private set => SetField(ref _partyVisible, value); }

    private double _partyX, _partyY;
    public double PartyX { get => _partyX; private set => SetField(ref _partyX, value); }
    public double PartyY { get => _partyY; private set => SetField(ref _partyY, value); }

    public double CellSize => _map is null ? Zoom : Zoom;

    // ---- folder -----------------------------------------------------------------------------

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select the Space 1889 folder (the one with 1889.COM)" };
        if (Files.GameFolder.IsGameFolder(GameFolder)) dialog.InitialDirectory = GameFolder;
        if (dialog.ShowDialog() != true) return;
        if (!Files.GameFolder.IsGameFolder(dialog.FolderName))
        {
            Info = $"{dialog.FolderName} does not hold Space 1889 (M.EXE, A.SYS, B.SYS and ITEMS.DAT were not all found).";
            return;
        }
        GameFolder = dialog.FolderName;
    }

    internal void OnGameFolderChanged()
    {
        OnPropertyChanged(nameof(GameFolder));
        // Clear first: the new folder's entries compare equal to the old ones, so re-selecting "the same" map would
        // otherwise not re-render it from the new files.
        SelectedMap = null;
        Maps.Clear();
        _library = Files.GameFolder.IsGameFolder(GameFolder) ? new MapLibrary(GameFolder) : null;
        _map = null;
        if (_library is null)
        {
            Image = null;
            Markers.Clear();
            Info = "Set the game folder to browse the maps.";
            return;
        }
        foreach (var m in _library.Maps) Maps.Add(m);
        Info = $"{Maps.Count} maps in {GameFolder}.";
        if (!SelectPartyMap()) SelectedMap = Maps.FirstOrDefault(m => m.Id == 110) ?? Maps.FirstOrDefault();
    }

    // ---- following the party ------------------------------------------------------------------

    internal void OnStateRefreshed(bool force)
    {
        GoToPartyCommand.RaiseCanExecuteChanged();
        if (_main.State is null) { PartyVisible = false; return; }
        var world = new WorldRecord(_main.State);
        if (FollowParty && (force || world.MapId != _lastPartyMapId)) SelectPartyMap();
        _lastPartyMapId = world.MapId;
        PlaceParty();
    }

    private bool SelectPartyMap()
    {
        if (_main.State is null || _library is null) return false;
        var world = new WorldRecord(_main.State);
        if (world.Planet == 0) return false;   // in space the position is S.EXE's chart, not a map square
        var entry = Maps.FirstOrDefault(m => m.Id == world.MapId);
        if (entry is null) return false;
        if (ReferenceEquals(entry, SelectedMap))
        {
            // Already selected; the shared pub/inn still needs a redraw when the party has changed planet.
            if (_renderedPlanet == TilePlanet(entry)) PlaceParty(); else Render();
            return true;
        }
        SelectedMap = entry;
        return true;
    }

    private void PlaceParty()
    {
        if (_main.State is null || _map is null || SelectedMap is null) { PartyVisible = false; return; }
        var world = new WorldRecord(_main.State);
        bool here = world.Planet != 0 && world.MapId == SelectedMap.Id && _map.Contains(world.Row, world.Column);
        PartyVisible = here;
        if (!here) return;
        PartyX = world.Column * Zoom;
        PartyY = world.Row * Zoom;
    }

    private int TilePlanet(MapEntry entry)
    {
        if (entry.File == "0.SYS") return 0;
        if (entry.Id is 992 or 999)
            return _main.State is { } s && new WorldRecord(s).Planet is >= 1 and <= 6 and var p ? p : 1;
        return entry.Planet;
    }

    // ---- rendering --------------------------------------------------------------------------

    private void Render()
    {
        Details.Clear();
        _annotations = Array.Empty<MapAnnotation>();
        if (_library is null || SelectedMap is not { } entry) { _map = null; Image = null; Markers.Clear(); PartyVisible = false; return; }
        int planet = TilePlanet(entry);
        _map = entry.File == "0.SYS" ? _library.Load(0, space: true) : _library.Load(entry.Id, planet);
        if (_map is null)
        {
            Image = null;
            Markers.Clear();
            Info = $"Map {entry.Id} could not be decoded.";
            return;
        }
        var image = _library.Render(_map, Mode, Zoom, planet);
        var bmp = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
        bmp.Freeze();
        Image = bmp;
        _renderedPlanet = planet;
        OnPropertyChanged(nameof(CellSize));

        Info = $"{entry.Title} — map id {entry.Id}, {_map.Columns} × {_map.Rows} squares ({entry.File}).";
        if (entry.Planet is >= 1 and <= 6 && entry.Area > 0 && entry.File != "0.SYS" && entry.Id is not (992 or 999))
        {
            if (_library.Area(entry.Planet, entry.Area) is { } area)
            {
                if (area.DarkMaps.Contains(entry.MapNumber)) Details.Add("Dark: needs a MINERS HAT, LANTERN or ELECTRIC LAMP in use.");
                if (area.GoldMaps.Contains(entry.MapNumber)) Details.Add("Digging here can turn up gold.");
                var (r, c) = area.EntryPositions[entry.MapNumber];
                if (r != 0 || c != 0) Details.Add($"Entry square: row {r}, column {c}.");
            }
        }
        if (entry.Id is 992 or 999)
            Details.Add("Every city shares this layout; the NPCs marked are those of the city the party is in (attach to see them).");
        foreach (var (mapId, item, alt) in TileRules.LockedDoorMaps)
            if (mapId == entry.Id)
                Details.Add($"Doors are locked: carry {ItemBook.NameOf(item)}{(alt != 0 ? " or " + ItemBook.NameOf(alt) : "")}.");
        BuildMarkers();
        PlaceParty();
    }

    private void BuildMarkers()
    {
        Markers.Clear();
        _annotations = Array.Empty<MapAnnotation>();
        if (_map is null || SelectedMap is not { } entry || _library is null || entry.File == "0.SYS") return;

        IReadOnlyList<NpcInfo> npcs = Array.Empty<NpcInfo>();
        if (ShowNpcs)
        {
            if (entry.Id is not (992 or 999))
                npcs = _library.Npcs(entry.Id, entry.Planet, entry.Area);
            else if (_main.State is { } s && new WorldRecord(s) is { Planet: >= 1 and <= 6, Area: > 0 } world)
                npcs = _library.Npcs(entry.Id, world.Planet, world.Area);   // the shared pub/inn: only a city's own list
        }

        _annotations = MapAnnotations.For(_map, npcs);
        foreach (var a in _annotations)
        {
            bool npc = a.Kind == AnnotationKind.Npc;
            Markers.Add(new MapMarker(
                a.Column * Zoom,
                npc ? a.Row * Zoom : a.Row * Zoom - 14,
                npc ? "●" : a.Label,
                a.Kind.ToString()));
        }
    }

    // ---- mouse ---------------------------------------------------------------------------------

    /// <summary>Converts a point on the image into a (row, column), or null outside the map.</summary>
    public (int Row, int Column)? SquareAt(double x, double y)
    {
        if (_map is null || x < 0 || y < 0) return null;
        int r = (int)(y / Zoom), c = (int)(x / Zoom);
        return _map.Contains(r, c) ? (r, c) : null;
    }

    public void HoverAt(double x, double y)
    {
        if (SquareAt(x, y) is not { } sq || _map is null) { Hover = ""; return; }
        var cell = _map[sq.Row, sq.Column];
        string text = $"Row {sq.Row}, column {sq.Column}: {TileRules.Classify(cell)} (sheet {(cell.Sheet == 0 ? "DEF" : cell.Sheet == 1 ? "SPR" : "?")} tile {cell.Tile})";
        var notes = _annotations
            .Where(a => a.Row == sq.Row && a.Column == sq.Column)
            .Select(a => a.Description.Length > 0 ? a.Description : a.Label)
            .Distinct()
            .ToList();
        Hover = notes.Count == 0 ? text : text + " — " + string.Join("; ", notes);
    }

    /// <summary>
    /// Why teleporting to (<paramref name="row"/>, <paramref name="column"/>) on the party's current map would be
    /// unsafe, or null when it is fine. Squares off the map are always refused; walls, water and trigger squares are
    /// refused unless <see cref="AllowUnsafeTeleport"/> is ticked. With no game folder the square cannot be checked, so
    /// a square beyond the largest map is refused and any other needs "Allow teleporting onto any square".
    /// </summary>
    public string? CheckTeleport(GameState state, int row, int column)
    {
        var world = new WorldRecord(state);
        if (world.Planet == 0) return "the party is in space, where the position is S.EXE's chart rather than a map square.";
        if (_library?.Load(world.MapId, world.Planet) is not { } map)
        {
            if (row >= MapFile.LargestGroundRows || column >= MapFile.LargestGroundColumns)
                return $"row {row}, column {column} is beyond the largest map ({MapFile.LargestGroundColumns} × {MapFile.LargestGroundRows} squares).";
            return AllowUnsafeTeleport
                ? null
                : "the game folder is not set, so the square cannot be checked. Set it on the Maps tab, or tick "
                  + "\"Allow teleporting onto any square\" there to move without the check.";
        }
        if (!map.Contains(row, column))
            return $"row {row}, column {column} is off map {world.MapId} ({map.Columns} × {map.Rows} squares).";
        var cell = map[row, column];
        if (!TileRules.IsSafeLanding(cell) && !AllowUnsafeTeleport)
            return $"row {row}, column {column} is {TileRules.Classify(cell)}, not a walkable square. "
                 + "Tick \"Allow teleporting onto any square\" on the Maps tab to force it.";
        return null;
    }

    /// <summary>A click: teleports the party there if it is on this map and the square is safe.</summary>
    public void Pick(double x, double y)
    {
        if (SquareAt(x, y) is not { } sq || _map is null || SelectedMap is null) return;
        if (!_main.CanEdit || _main.State is null)
        {
            _main.Report("Attach to the game to teleport by clicking the map.");
            return;
        }
        if (!_main.State.Refresh())
        {
            _main.Report("Could not re-read the game state; nothing was written.");
            return;
        }
        var world = new WorldRecord(_main.State);
        if (world.Planet == 0 || world.MapId != SelectedMap.Id)
        {
            _main.Report($"The party is on map {world.MapId}, not this one; teleporting only works within the party's current map.");
            return;
        }
        if (CheckTeleport(_main.State, sq.Row, sq.Column) is { } unsafeReason)
        {
            _main.Report("Teleport refused: " + unsafeReason);
            return;
        }
        if (world.Teleport(sq.Row, sq.Column))
        {
            _main.AfterWrite();
            _main.Report($"Party teleported to row {sq.Row}, column {sq.Column}. Take a step in the game to redraw the view.");
            _main.Refresh(force: false);
        }
        else _main.Report("The teleport was refused.");
    }
}
