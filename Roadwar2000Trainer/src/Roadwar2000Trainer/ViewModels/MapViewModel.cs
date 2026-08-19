using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>
/// The Maps tab: a schematic of the overland map the gang is on, drawn from the 2,016 terrain
/// bytes the engine keeps at <c>DS:0x03C7</c>, with the gang's square marked and click-to-teleport.
/// <para>
/// Live memory is the preferred source because it is what the gang is actually walking on. When
/// the trainer is not attached, the shipped <c>WEST.MAP</c>/<c>EAST.MAP</c> are read instead, so
/// the tab is still useful for planning with the game closed.
/// </para>
/// </summary>
public sealed class MapViewModel : ObservableObject
{
    /// <summary>Pixels per map square in the rendered schematic.</summary>
    public const int Cell = 14;

    private readonly MainViewModel _main;
    private OverlandMap? _map;
    private int _drawnMapId;

    public MapViewModel(MainViewModel main)
    {
        _main = main;
        TeleportCommand = new RelayCommand(Teleport, () => _main.CanEdit);
        ReloadCommand = new RelayCommand(Reload);
        ShowWestCommand = new RelayCommand(() => OfflineMap = 1, () => !_main.IsAttached);
        ShowEastCommand = new RelayCommand(() => OfflineMap = 2, () => !_main.IsAttached);
    }

    public RelayCommand TeleportCommand { get; }
    public RelayCommand ReloadCommand { get; }

    /// <summary>Offline only: which of the two shipped maps to draw.</summary>
    public RelayCommand ShowWestCommand { get; }
    public RelayCommand ShowEastCommand { get; }

    private int _offlineMap = 2;
    /// <summary>
    /// Which map the file fallback draws when the trainer is not attached. Attached, the map is
    /// whichever one the gang is actually on and this is ignored; detached there is nothing to ask,
    /// so without a selector the western half of the continent could never be looked at.
    /// </summary>
    public int OfflineMap
    {
        get => _offlineMap;
        set { if (SetField(ref _offlineMap, Math.Clamp(value, 1, 2))) Reload(); }
    }

    public int PixelWidth => OverlandMap.Width * Cell;
    public int PixelHeight => OverlandMap.Height * Cell;

    private ImageSource? _image;
    public ImageSource? Image { get => _image; private set => SetField(ref _image, value); }

    private string _source = "no map loaded";
    /// <summary>Where the drawn map came from, so the reading is never ambiguous.</summary>
    public string Source { get => _source; private set => SetField(ref _source, value); }

    private int _partyX = 1;
    public int PartyX
    {
        get => _partyX;
        set { if (SetField(ref _partyX, value)) OnPropertyChanged(nameof(SquareDescription)); }
    }

    private int _partyY;
    public int PartyY
    {
        get => _partyY;
        set { if (SetField(ref _partyY, value)) OnPropertyChanged(nameof(SquareDescription)); }
    }

    private int _targetX = 1;
    /// <summary>Teleport destination column, 1..48.</summary>
    public int TargetX
    {
        get => _targetX;
        set { if (SetField(ref _targetX, Math.Clamp(value, 1, OverlandMap.Width))) OnPropertyChanged(nameof(TargetDescription)); }
    }

    private int _targetY;
    /// <summary>Teleport destination row, 0..41.</summary>
    public int TargetY
    {
        get => _targetY;
        set { if (SetField(ref _targetY, Math.Clamp(value, 0, OverlandMap.Height - 1))) OnPropertyChanged(nameof(TargetDescription)); }
    }

    public string SquareDescription =>
        _map is null ? "" : $"Gang at {_partyX},{_partyY}: {_map.DescribeSquare(_partyX, _partyY)}";

    public string TargetDescription
    {
        get
        {
            if (_map is null) return "";
            string what = _map.DescribeSquare(_targetX, _targetY);
            bool ok = _map.IsPassable(_targetX, _targetY);
            return $"Target {_targetX},{_targetY}: {what}" + (ok ? "" : "  -- impassable, the gang cannot stand here");
        }
    }

    /// <summary>The folder the shipped .MAP files are read from when the trainer is not attached.</summary>
    private string? _gameFolder;
    public string? GameFolder
    {
        get => _gameFolder;
        set { if (SetField(ref _gameFolder, value)) Reload(); }
    }

    /// <summary>Re-reads the terrain and redraws. Live memory wins; the shipped files are the fallback.</summary>
    public void Reload()
    {
        bool live = _main.IsAttached && _main.GangRecord is not null;
        int mapId = live ? _main.GangRecord!.CurrentMap : _offlineMap;

        if (live && _main.Target is { } target && target.ReadOverlandMap() is { } cells)
        {
            _map = OverlandMap.FromBytes(cells, mapId, mapId == 1 ? "WEST.MAP" : "EAST.MAP");
            Source = $"live memory (DS:0x{SaveFormat.DsOverlandMap:X4}, {(mapId == 1 ? "west" : "east")} map)";
        }
        else if (!string.IsNullOrWhiteSpace(_gameFolder))
        {
            var (west, east) = OverlandMap.LoadPair(_gameFolder!);
            _map = mapId == 1 ? west : east;
            Source = _map is null
                ? $"WEST.MAP/EAST.MAP not found in {_gameFolder}"
                : $"{_map.Name} in {_gameFolder}";
        }
        else
        {
            _map = null;
            Source = "attach to the game, or point the Save Editor at the game folder, to see the map";
        }

        _drawnMapId = mapId;
        RefreshParty();
        Redraw();
        TeleportCommand.RaiseCanExecuteChanged();
        ShowWestCommand.RaiseCanExecuteChanged();
        ShowEastCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// The cheap per-tick update: move the marker, and notice when the gang has crossed to the
    /// other overland map. The crossing check matters because a player who drives to the seam in
    /// the game itself changes which map is loaded without the trainer doing anything, and without
    /// it this tab would keep drawing the old continent's terrain with the marker on it.
    /// </summary>
    public void RefreshParty()
    {
        if (_main.GangRecord is not { } gang)
        {
            // Detached: park the marker off the grid rather than leave it where the last session
            // stopped, which would read as a live position.
            if (_partyX != 0 || _partyY != 0) { PartyX = 0; PartyY = 0; Redraw(); }
            return;
        }

        if (gang.CurrentMap != _drawnMapId) { Reload(); return; }

        bool moved = gang.X != _partyX || gang.Y != _partyY;
        PartyX = gang.X;
        PartyY = gang.Y;
        if (moved) Redraw();
    }

    /// <summary>The square under a click, in map coordinates.</summary>
    public (int X, int Y) SquareAt(double pixelX, double pixelY) =>
        (Math.Clamp((int)(pixelX / Cell) + 1, 1, OverlandMap.Width),
         Math.Clamp((int)(pixelY / Cell), 0, OverlandMap.Height - 1));

    /// <summary>Picks a square as the teleport target without moving the gang.</summary>
    public void Pick(int x, int y)
    {
        TargetX = x;
        TargetY = y;
    }

    private void Teleport()
    {
        if (_main.GangRecord is not { } gang) return;

        if (_map is null)
        {
            // No terrain means nothing to check the destination against, and an unchecked jump can
            // land the gang on water or on one of the scenery codes the engine has no name for.
            _main.Report("No overland terrain is loaded, so the destination cannot be checked. " +
                         "Press Redraw, or point the Save Editor at the game folder, and try again.");
            return;
        }

        if (!_map.IsPassable(_targetX, _targetY))
        {
            _main.Report($"{_targetX},{_targetY} is {_map.DescribeSquare(_targetX, _targetY)} -- " +
                         "the engine has no terrain name for it and the gang cannot stand there. " +
                         "Pick a land square.");
            return;
        }

        gang.X = _targetX;
        gang.Y = _targetY;
        _main.Report($"Gang moved to {_targetX},{_targetY} ({_map.DescribeSquare(_targetX, _targetY)}). " +
                     "The map redraws on the next move or command.");
        _main.Refresh(force: true);
    }

    // ---- drawing -------------------------------------------------------------

    private static readonly Color Plains = Color.FromRgb(0x4A, 0x52, 0x3A);
    private static readonly Color Farmland = Color.FromRgb(0x6E, 0x76, 0x37);
    private static readonly Color Desert = Color.FromRgb(0x8A, 0x77, 0x4A);
    private static readonly Color Forest = Color.FromRgb(0x2E, 0x4A, 0x33);
    private static readonly Color Ruins = Color.FromRgb(0x5A, 0x43, 0x43);
    private static readonly Color Road = Color.FromRgb(0xC9, 0xC2, 0xA8);
    private static readonly Color Oilfield = Color.FromRgb(0x3A, 0x3A, 0x50);
    private static readonly Color CityColor = Color.FromRgb(0xE0, 0xB3, 0x41);
    private static readonly Color Impassable = Color.FromRgb(0x1B, 0x24, 0x33);
    private static readonly Color PartyColor = Color.FromRgb(0xE8, 0x50, 0x3C);

    private static Color ColorOf(int code) => code switch
    {
        TerrainBook.Plains => Plains,
        TerrainBook.Farmland => Farmland,
        TerrainBook.Desert => Desert,
        TerrainBook.Forest => Forest,
        TerrainBook.Ruins => Ruins,
        TerrainBook.Oilfield => Oilfield,
        TerrainBook.CitySmall or TerrainBook.CityLarge or TerrainBook.CityMetroplex => CityColor,
        _ when TerrainBook.IsRoad(code) => Road,
        _ => Impassable,
    };

    private void Redraw()
    {
        if (_map is null) { Image = null; return; }

        int w = PixelWidth, h = PixelHeight;
        var pixels = new byte[w * h * 4];

        void Fill(int px, int py, int pw, int ph, Color c)
        {
            for (int y = py; y < py + ph && y < h; y++)
                for (int x = px; x < px + pw && x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    pixels[i + 0] = c.B;
                    pixels[i + 1] = c.G;
                    pixels[i + 2] = c.R;
                    pixels[i + 3] = 255;
                }
        }

        for (int y = 0; y < OverlandMap.Height; y++)
            for (int x = 1; x <= OverlandMap.Width; x++)
            {
                // One pixel of gap so the grid stays legible at 14 px a square.
                Fill((x - 1) * Cell, y * Cell, Cell - 1, Cell - 1, ColorOf(_map[x, y]));

                // A city gets a darker core so the three sizes are distinguishable from the tile alone.
                if (TerrainBook.IsCity(_map[x, y]))
                {
                    int inset = _map[x, y] == TerrainBook.CityMetroplex ? 2 : _map[x, y] == TerrainBook.CityLarge ? 3 : 4;
                    Fill((x - 1) * Cell + inset, y * Cell + inset, Cell - 1 - inset * 2, Cell - 1 - inset * 2,
                         Color.FromRgb(0x6B, 0x4E, 0x12));
                }
            }

        if (OverlandMap.IsInside(_partyX, _partyY))
            Fill((_partyX - 1) * Cell + 2, _partyY * Cell + 2, Cell - 5, Cell - 5, PartyColor);

        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bitmap.Freeze();
        Image = bitmap;
        OnPropertyChanged(nameof(SquareDescription));
        OnPropertyChanged(nameof(TargetDescription));
    }
}
