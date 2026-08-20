using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BardsTaleTrilogyTrainer.Game;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>One map in the picker, with its grid drawn on demand and cached.</summary>
public sealed class MapEntryViewModel : ObservableObject
{
    private ImageSource? _image;
    private MapGrid? _grid;
    private string _error = "";

    public MapEntryViewModel(GameMapInfo info) => Info = info;

    public GameMapInfo Info { get; }

    public string Name => Info.Name;
    public string Category => Info.Category;
    public string Display => Info.Display;

    public string Description
    {
        get
        {
            var bits = new List<string> { $"{Info.Kind} · {Info.Width}×{Info.Height}" };
            if (Info.IsDungeon) bits.Add($"floor {Info.Level + 1}");
            if (Info.IsTower) bits.Add("tower (stairs run upward)");
            if (Info.WrapsAround) bits.Add("edges wrap around");
            if (Info.IsOutside) bits.Add("open sky");
            return string.Join(" · ", bits);
        }
    }

    /// <summary>The decoded grid, once <see cref="Load"/> has been given an archive.</summary>
    public MapGrid? Grid => _grid;

    /// <summary>Why the grid could not be decoded, if it could not.</summary>
    public string Error { get => _error; private set => SetField(ref _error, value); }

    public ImageSource? Image { get => _image; private set => SetField(ref _image, value); }

    public bool HasImage => _image != null;

    /// <summary>Decodes and draws this map. Cheap to call repeatedly — the result is cached.</summary>
    public void Load(MapArchive? archive)
    {
        if (_grid != null || archive == null) return;
        _grid = archive.TryGetMap(Info.Asset, out string error);
        if (_grid == null)
        {
            Error = error;
            return;
        }
        Error = "";
        Image = MapRenderer.Render(_grid);
        OnPropertyChanged(nameof(Grid));
        OnPropertyChanged(nameof(HasImage));
    }
}

/// <summary>
/// Backs the Maps tab: every area of the trilogy, the decoded grid for the selected one, a
/// live marker showing where the party is standing, and click-to-teleport.
///
/// <para>The catalogue in <see cref="MapBook"/> is always available, so the list can be
/// browsed before attaching. Grid terrain comes from the player's own installation through
/// <see cref="MapArchive"/>, and the marker and teleport need the game running.</para>
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly Func<MapNavigator?> _getNavigator;
    private readonly Func<MapArchive?> _getArchive;
    private readonly Action<string> _setStatus;
    private readonly ObservableCollection<MapEntryViewModel> _items;

    private MapEntryViewModel? _selectedMap;
    private PartyLocation? _location;
    private bool _teleportOnClick = true;
    private bool _followParty = true;
    private bool _journalTeleport;
    private int _targetX, _targetZ;
    private Facing _targetFacing = Facing.North;
    private TeleportType _teleportStyle = TeleportType.Fade;
    private double _markerX, _markerY;
    private bool _markerVisible;
    private string _archiveStatus = "";

    public MapsViewModel(Func<MapNavigator?> getNavigator, Func<MapArchive?> getArchive, Action<string> setStatus)
    {
        _getNavigator = getNavigator;
        _getArchive = getArchive;
        _setStatus = setStatus;

        _items = new ObservableCollection<MapEntryViewModel>(
            MapBook.Maps.Select(m => new MapEntryViewModel(m)));
        Maps = CollectionViewSource.GetDefaultView(_items);
        Maps.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MapEntryViewModel.Category)));

        foreach (var t in MapBook.DreamSpellTargets) DreamTargets.Add(t);

        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport);
        GoToEntryCommand = new RelayCommand(_ => GoToEntry(), _ => SelectedMap != null);
        GoToPartyCommand = new RelayCommand(_ => SelectPartyMap(), _ => _location != null);
        // The dream spell is BT2's, and its destinations are BT2 city indices, so the buttons
        // are only live while BT2 is loaded. MapNavigator refuses the jump anyway; greying them
        // out says why before the click rather than after. Read from the polled location so
        // this costs no extra memory reads per tick.
        TeleportToDreamTargetCommand = new RelayCommand(TeleportToDreamTarget,
            t => t is DreamSpellTarget && CanTeleport && _location?.Chapter == GameChapter.DestinyKnight);

        SelectedMap = _items.FirstOrDefault();
    }

    /// <summary>All 121 areas, grouped by chapter and kind for the picker.</summary>
    public ICollectionView Maps { get; }

    /// <summary>The BT2 dream spell (ZZGO) destinations, as the game lists them.</summary>
    public ObservableCollection<DreamSpellTarget> DreamTargets { get; } = new();

    public RelayCommand TeleportCommand { get; }
    public RelayCommand GoToEntryCommand { get; }
    public RelayCommand GoToPartyCommand { get; }
    public RelayCommand TeleportToDreamTargetCommand { get; }

    public MapEntryViewModel? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (!SetField(ref _selectedMap, value)) return;
            value?.Load(_getArchive());
            ClampTarget();
            UpdateMarker();
            OnPropertyChanged(nameof(SelectedGrid));
            OnPropertyChanged(nameof(HintText));
            RaiseCommands();
        }
    }

    public MapGrid? SelectedGrid => _selectedMap?.Grid;

    /// <summary>Where the party is, refreshed by the host's poll timer.</summary>
    public PartyLocation? Location
    {
        get => _location;
        private set
        {
            _location = value;
            OnPropertyChanged(nameof(Location));
            OnPropertyChanged(nameof(LocationText));
            OnPropertyChanged(nameof(HasLocation));
            OnPropertyChanged(nameof(HintText));
        }
    }

    public bool HasLocation => _location != null;

    public string LocationText => _location == null
        ? "Party position unknown — attach, locate, and be in a map in-game."
        : _location.Summary;

    /// <summary>How the map archive fared: which file it opened, or why it could not.</summary>
    public string ArchiveStatus { get => _archiveStatus; private set => SetField(ref _archiveStatus, value); }

    // --- teleport inputs --------------------------------------------------------
    public int TargetX { get => _targetX; set { if (SetField(ref _targetX, value)) RaiseCommands(); } }
    public int TargetZ { get => _targetZ; set { if (SetField(ref _targetZ, value)) RaiseCommands(); } }

    public Facing TargetFacing { get => _targetFacing; set => SetField(ref _targetFacing, value); }
    public IReadOnlyList<Facing> FacingChoices { get; } =
        new[] { Facing.North, Facing.East, Facing.South, Facing.West };

    public TeleportType TeleportStyle { get => _teleportStyle; set => SetField(ref _teleportStyle, value); }
    public IReadOnlyList<TeleportType> TeleportStyles { get; } =
        new[] { TeleportType.Fade, TeleportType.Dimensional, TeleportType.Quiet };

    /// <summary>When on, clicking a square teleports the party to it.</summary>
    public bool TeleportOnClick
    {
        get => _teleportOnClick;
        set { if (SetField(ref _teleportOnClick, value)) OnPropertyChanged(nameof(HintText)); }
    }

    /// <summary>When on, the picker jumps to whichever map the party walks into.</summary>
    public bool FollowParty { get => _followParty; set => SetField(ref _followParty, value); }

    /// <summary>Whether the jump is written into the in-game journal, as a scripted one would be.</summary>
    public bool JournalTeleport { get => _journalTeleport; set => SetField(ref _journalTeleport, value); }

    public bool CanTeleport =>
        _getNavigator() is { } nav && nav.PlayerInstance != 0 && _selectedMap != null;

    public string HintText
    {
        get
        {
            if (_selectedMap == null) return "Pick a map.";
            if (_selectedMap.Grid == null && _selectedMap.Error.Length > 0) return _selectedMap.Error;
            if (_location == null)
                return "Browsing offline. Attach and locate, with the party standing in a map, "
                     + "to see the live marker and teleport.";
            return _teleportOnClick
                ? "Teleport armed — click any square to send the party there."
                : "Click a square to set the destination, then press Teleport.";
        }
    }

    // --- live marker (in image pixels) ------------------------------------------
    /// <summary>Centre of the party's square, in image pixels.</summary>
    public double MarkerX
    {
        get => _markerX;
        private set { if (SetField(ref _markerX, value)) OnPropertyChanged(nameof(MarkerLeft)); }
    }

    public double MarkerY
    {
        get => _markerY;
        private set { if (SetField(ref _markerY, value)) OnPropertyChanged(nameof(MarkerTop)); }
    }

    /// <summary>Top-left of the marker, for positioning it on the overlay canvas.</summary>
    public double MarkerLeft => _markerX - MarkerSize / 2;
    public double MarkerTop => _markerY - MarkerSize / 2;

    public bool MarkerVisible { get => _markerVisible; private set => SetField(ref _markerVisible, value); }

    /// <summary>Size of the marker drawn over the party's square.</summary>
    public double MarkerSize => MapRenderer.Cell;

    // --- host callbacks ---------------------------------------------------------
    /// <summary>Called once the archive is opened, so already-selected maps can draw.</summary>
    public void OnArchiveOpened(MapArchive? archive, string error)
    {
        ArchiveStatus = archive != null
            ? $"Map terrain loaded from {archive.Path} ({archive.MapAssets.Count} maps)."
            : $"Map terrain unavailable: {error}";
        _selectedMap?.Load(archive);
        OnPropertyChanged(nameof(SelectedGrid));
        OnPropertyChanged(nameof(HintText));
    }

    /// <summary>Periodic poll from the host: re-read the party position and move the marker.</summary>
    public void Tick()
    {
        var nav = _getNavigator();
        var next = nav?.ReadLocation();
        // Chapter is part of a map's identity — SelectPartyMap and UpdateMarker both match on
        // it — so leaving it out here lets a jump between chapters that happens to keep the
        // same index and dungeon flag go unnoticed: the marker vanishes and the stale
        // foreign-chapter map stays selected and armed for teleport-on-click.
        bool mapChanged = next != null &&
            (_location == null || _location.Chapter != next.Chapter ||
             _location.MapIndex != next.MapIndex || _location.IsDungeon != next.IsDungeon);

        Location = next;

        if (next != null && mapChanged && _followParty) SelectPartyMap();
        UpdateMarker();
        RaiseCommands();
    }

    /// <summary>Selects the map the party is currently standing in.</summary>
    private void SelectPartyMap()
    {
        if (_location == null) return;
        var match = _items.FirstOrDefault(m =>
            m.Info.Chapter == _location.Chapter &&
            m.Info.IsDungeon == _location.IsDungeon &&
            m.Info.Index == _location.MapIndex);
        if (match != null && !ReferenceEquals(match, _selectedMap)) SelectedMap = match;
    }

    private void UpdateMarker()
    {
        if (_location == null || _selectedMap == null ||
            _selectedMap.Info.Chapter != _location.Chapter ||
            _selectedMap.Info.IsDungeon != _location.IsDungeon ||
            _selectedMap.Info.Index != _location.MapIndex ||
            _selectedMap.Grid == null)
        {
            MarkerVisible = false;
            return;
        }

        var (px, py) = MapRenderer.CellToPixel(_selectedMap.Grid, _location.X, _location.Z);
        MarkerX = px;
        MarkerY = py;
        MarkerVisible = true;
    }

    // --- interaction ------------------------------------------------------------
    /// <summary>A click on the map image, in image-pixel coordinates.</summary>
    public void OnMapClicked(double pixelX, double pixelY)
    {
        if (_selectedMap?.Grid is not { } grid) return;
        var (x, z) = MapRenderer.PixelToCell(grid, pixelX, pixelY);
        if (!grid.Contains(x, z)) return;

        TargetX = x;
        TargetZ = z;

        var cell = grid[x, z];
        string what = grid.IsDungeon
            ? DescribeDungeonCell(cell)
            : DescribeCityCell(cell);
        _setStatus($"Square X {x} · Z {z}{(what.Length > 0 ? " — " + what : "")}");

        if (_teleportOnClick) Teleport();
    }

    private static string DescribeDungeonCell(MapCell cell)
    {
        var bits = new List<string>();
        if (cell.Flags != CellFlags.None) bits.Add(cell.Flags.ToString().Replace(", ", " + "));
        var walls = new[] { ("N", cell.North), ("E", cell.East), ("S", cell.South), ("W", cell.West) }
            .Where(w => w.Item2 != WallKind.None && w.Item2 != WallKind.Solid)
            .Select(w => $"{w.Item1}:{w.Item2}");
        bits.AddRange(walls);
        return string.Join(", ", bits);
    }

    private static string DescribeCityCell(MapCell cell)
    {
        var bits = new List<string>();
        if (cell.Module != CityModule.None) bits.Add(cell.Module.ToString());
        if (cell.IsBlocked) bits.Add("blocked");
        return string.Join(", ", bits);
    }

    private void Teleport()
    {
        var nav = _getNavigator();
        if (nav == null || _selectedMap == null)
        {
            _setStatus("Attach and locate first — a teleport needs the running game.");
            return;
        }

        nav.TryTeleport(_selectedMap.Info, _targetX, _targetZ, _targetFacing,
            _teleportStyle, _journalTeleport, out string message);
        _setStatus(message);
    }

    /// <summary>
    /// Sends the party where the dream spell would: a dungeon's entrance out in the world.
    /// The table's map index is a city/wilderness map, not the dungeon itself.
    /// </summary>
    private void TeleportToDreamTarget(object? parameter)
    {
        if (parameter is not DreamSpellTarget target) return;
        var map = MapBook.Find(GameChapter.DestinyKnight, isDungeon: false, target.Map);
        if (map == null)
        {
            _setStatus($"Dream target '{target.Name}' points at BT2 city map {target.Map}, which is not in the catalogue.");
            return;
        }

        var entry = _items.FirstOrDefault(m => ReferenceEquals(m.Info, map));
        if (entry != null) SelectedMap = entry;
        TargetX = target.X;
        TargetZ = target.Z;
        TargetFacing = target.Facing;
        Teleport();
    }

    /// <summary>
    /// Moves the destination to the map's entry point. Multi-level areas share one entry point
    /// across their floors, so it can fall outside a smaller floor — clamp rather than refuse.
    /// </summary>
    private void GoToEntry()
    {
        if (_selectedMap == null) return;
        var info = _selectedMap.Info;
        TargetX = Math.Clamp(info.EntryX, 0, info.Width - 1);
        TargetZ = Math.Clamp(info.EntryZ, 0, info.Height - 1);

        bool clamped = TargetX != info.EntryX || TargetZ != info.EntryZ;
        _setStatus(clamped
            ? $"{_selectedMap.Name}: the area's entry point ({info.EntryX}, {info.EntryZ}) is outside this "
              + $"{info.Width}×{info.Height} floor, so it has been clamped to X {TargetX} · Z {TargetZ}."
            : $"{_selectedMap.Name}: entry point is X {TargetX} · Z {TargetZ}.");
    }

    private void ClampTarget()
    {
        if (_selectedMap == null) return;
        TargetX = Math.Clamp(_targetX, 0, Math.Max(0, _selectedMap.Info.Width - 1));
        TargetZ = Math.Clamp(_targetZ, 0, Math.Max(0, _selectedMap.Info.Height - 1));
    }

    private void RaiseCommands()
    {
        OnPropertyChanged(nameof(CanTeleport));
        TeleportCommand.RaiseCanExecuteChanged();
        GoToEntryCommand.RaiseCanExecuteChanged();
        GoToPartyCommand.RaiseCanExecuteChanged();
        TeleportToDreamTargetCommand.RaiseCanExecuteChanged();
    }
}
