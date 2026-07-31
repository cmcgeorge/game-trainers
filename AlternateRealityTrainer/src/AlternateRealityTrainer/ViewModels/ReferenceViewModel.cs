using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using AlternateRealityTrainer.Game;
using Microsoft.Win32;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>A location row for the reference grid.</summary>
public sealed record PlaceRow(string Kind, string Coordinate, int North, int East, string Note);

/// <summary>A potion row for the reference grid.</summary>
public sealed record PotionRow(string Colour, string Taste, string Sip, string Effect);

/// <summary>A control row for the reference grid.</summary>
public sealed record ControlRow(string Key, string Action);

/// <summary>
/// Read-only game knowledge: the drawn City map, where everything is, the potion table, the
/// controls, the item ladders and the survival notes. Nothing here touches the game — it is the
/// strategy guide with the trainer wrapped around it.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public ObservableCollection<PlaceRow> Places { get; } = new();
    public ObservableCollection<PotionRow> Potions { get; } = new();
    public ObservableCollection<ControlRow> Controls { get; } = new();

    public IReadOnlyList<string> Kinds { get; }

    // --- the map -------------------------------------------------------------

    public IReadOnlyList<MapMarkerViewModel> Markers { get; }
    public IReadOnlyList<MapTickViewModel> Ticks { get; }
    public IReadOnlyList<MapLegendViewModel> MapLegend { get; }

    /// <summary>
    /// The terrain swatches, taken from <see cref="CityMap"/> so the on-screen legend cannot show a
    /// different palette from the map itself or the exported SVG.
    /// </summary>
    public IReadOnlyList<TerrainLegendViewModel> TerrainLegend { get; } =
        new[] { TerrainKind.Building, TerrainKind.Wall, TerrainKind.Scenery, TerrainKind.Street }
            .Select(k => new TerrainLegendViewModel(k)).ToList();

    /// <summary>Overall map size, so the canvas can be laid out without hard-coded numbers.</summary>
    public double MapWidth => CityMap.Width;
    public double MapHeight => CityMap.Height;

    /// <summary>Where the grid starts and how big it is — used to place the grid rectangle.</summary>
    public double GridSize => CityMap.GridSize;

    /// <summary>Offset of the grid inside the map, as a margin the grid rectangle can bind to.</summary>
    public System.Windows.Thickness GridInset => new(CityMap.Margin, CityMap.Margin, 0, 0);

    public double CellSize => CityMap.CellSize;
    public double MajorCellSize => CityMap.CellSize * CityMap.MajorEvery;

    private double _zoom = 1.0;
    /// <summary>Map zoom: 0.45× shows the whole 64 × 64 grid at once, 2× makes it easy to read.</summary>
    public double Zoom
    {
        get => _zoom;
        set => SetField(ref _zoom, Math.Clamp(value, 0.45, 2.0));
    }

    // --- the street map (walls) ----------------------------------------------

    private CityTerrain? _terrain;

    /// <summary>The city's street map, once it has been read from the game or from CITY.EXE.</summary>
    public CityTerrain? Terrain
    {
        get => _terrain;
        private set
        {
            _terrain = value;
            TerrainDrawing = TerrainImage.Build(value);
            OnPropertyChanged(nameof(Terrain));
            OnPropertyChanged(nameof(HasTerrain));
            OnPropertyChanged(nameof(TerrainSummary));
        }
    }

    private ImageSource? _terrainDrawing;
    /// <summary>The whole street map as one drawing, or null while it is unknown.</summary>
    public ImageSource? TerrainDrawing
    {
        get => _terrainDrawing;
        private set => SetField(ref _terrainDrawing, value);
    }

    public bool HasTerrain => _terrain != null;

    /// <summary>What the loaded map contains, or how to get one.</summary>
    public string TerrainSummary
    {
        get
        {
            if (_terrain == null)
                return "Streets and walls are the game's own data, so they are not shipped with the trainer. "
                     + "Attach to the running game, or load them from your copy of CITY.EXE.";
            var census = _terrain.Census();
            return $"{census[TerrainKind.Street]} street squares, {census[TerrainKind.Building]} building, "
                 + $"{census[TerrainKind.Wall]} wall, {census[TerrainKind.Scenery]} open ground, "
                 + $"{census[TerrainKind.Doorway]} doorways — "
                 + $"{_terrain.MatchingKnownPlaces()} of {CityBook.Places.Count} known locations line up.";
        }
    }

    /// <summary>True when the loaded map came from a file the user chose, not from the game.</summary>
    private bool _terrainFromFile;

    /// <summary>Called by the shell once it has read the map out of the attached game.</summary>
    public void SetTerrain(CityTerrain? terrain)
    {
        if (terrain == null) return;      // never drop a good map for a failed read
        Terrain = terrain;
        _terrainFromFile = false;
        MapStatus = "Streets and walls read from the running game.";
    }

    /// <summary>
    /// Forgets a map that came from the game, on detach. A map the user loaded from their own
    /// <c>CITY.EXE</c> is kept: "without the game running" is exactly the case that feature exists
    /// for, so detaching must not throw it away.
    /// </summary>
    public void ClearTerrain()
    {
        if (_terrainFromFile) return;
        Terrain = null;
        MapStatus = "";
    }

    public ICommand LoadTerrainCommand { get; }
    public ICommand SaveMapCommand { get; }

    private string _mapStatus = "";
    public string MapStatus { get => _mapStatus; private set => SetField(ref _mapStatus, value); }

    public ReferenceViewModel()
    {
        Markers = CityMap.Markers().Select(m => new MapMarkerViewModel(m)).ToList();
        Ticks = CityMap.Ticks().Select(t => new MapTickViewModel(t)).ToList();
        MapLegend = CityMap.Legend().Select(e => new MapLegendViewModel(e)).ToList();

        var kinds = new List<string> { "All" };
        kinds.AddRange(Enum.GetNames<PlaceKind>());
        Kinds = kinds;
        _selectedKind = kinds[0];

        foreach (var p in PotionBook.All)
            Potions.Add(new PotionRow(p.Colour, p.Taste, p.SipLabel, p.Effect));

        foreach (var c in GameFacts.Controls)
            Controls.Add(new ControlRow(c.Key, c.Action));

        LoadTerrainCommand = new RelayCommand(LoadTerrain);
        SaveMapCommand = new RelayCommand(SaveMap);

        RebuildPlaces();
    }

    private string _selectedKind;
    /// <summary>
    /// Filters the location list, and highlights the same kind on the map; "All" shows everything.
    /// </summary>
    public string SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (!SetField(ref _selectedKind, value)) return;
            RebuildPlaces();
            ApplyMapFilter();
        }
    }

    private PlaceKind? SelectedPlaceKind =>
        Enum.TryParse<PlaceKind>(_selectedKind, out var kind) ? kind : null;

    private void ApplyMapFilter()
    {
        var only = SelectedPlaceKind;
        foreach (var m in Markers) m.ApplyFilter(only);
    }

    private void RebuildPlaces()
    {
        Places.Clear();
        var only = SelectedPlaceKind;
        foreach (var p in CityBook.Places
                     .Where(p => only == null || p.Kind == only)
                     .OrderBy(p => p.Kind)
                     .ThenBy(p => p.North)
                     .ThenBy(p => p.East))
        {
            Places.Add(new PlaceRow(p.Kind.ToString(), p.Coordinate, p.North, p.East, p.Note));
        }
        OnPropertyChanged(nameof(PlaceCountText));
    }

    public string PlaceCountText => $"{Places.Count} location(s)";

    // Writes the same map out as a standalone SVG — the version that goes in the strategy guide.
    private void SaveMap()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save the City map",
            FileName = "city-map.svg",
            DefaultExt = ".svg",
            Filter = "SVG image (*.svg)|*.svg",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, CityMap.RenderSvg(_terrain));
            MapStatus = $"Saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MapStatus = "Could not save the map: " + ex.Message;
        }
    }

    // Reads the street map out of the player's own CITY.EXE, so the map tab is useful without the
    // game running. The map is the game's data and is deliberately not shipped with the trainer.
    private void LoadTerrain()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load the street map from CITY.EXE",
            FileName = "CITY.EXE",
            Filter = "Alternate Reality executable (CITY.EXE)|CITY.EXE|Executables (*.exe)|*.exe",
        };
        if (dialog.ShowDialog() != true) return;

        LoadTerrainFrom(dialog.FileName);
    }

    /// <summary>Largest file worth sweeping. <c>CITY.EXE</c> is 332 KB; the sweep is O(file length).</summary>
    private const long MaxTerrainFileBytes = 8L * 1024 * 1024;

    private async void LoadTerrainFrom(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxTerrainFileBytes)
            {
                MapStatus = $"{Path.GetFileName(path)} is too big to search " +
                            $"({info.Length / 1024 / 1024} MB); CITY.EXE is about 324 KB.";
                return;
            }

            MapStatus = $"Reading {Path.GetFileName(path)}…";
            // Off the UI thread: when the map is not at its usual offset this sweeps every byte of
            // the file, which is long enough to freeze the window.
            var terrain = await Task.Run(() => CityTerrain.FromCityExe(File.ReadAllBytes(path)));
            if (terrain == null)
            {
                MapStatus = "That file does not contain a City map the trainer recognises.";
                return;
            }
            Terrain = terrain;
            _terrainFromFile = true;
            MapStatus = $"Streets and walls loaded from {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            MapStatus = "Could not read that file: " + ex.Message;
        }
    }
}
