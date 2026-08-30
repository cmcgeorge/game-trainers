using System.Collections.ObjectModel;
using System.Windows.Media;
using DarklandsTrainer.Game;

namespace DarklandsTrainer.ViewModels;

public sealed class LevelEntryViewModel
{
    public AreaLevel Level { get; }

    public LevelEntryViewModel(AreaLevel level) => Level = level;

    public string Name => $"Area {Level.Index + 1}: {Level.Name}";
    public string Description => Level.Description;
    public IReadOnlyList<AreaPoi> Pois => Level.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Level);

    private ImageSource? _image;

    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Level.Width || y >= Level.Height) return "";
        var poi = Level.Pois.FirstOrDefault(value => value.X == x && value.Y == y);
        return poi != null ? $"{poi.Name} — {poi.Description}" : CellDescription(Level.Grid[x, y]);
    }

    private static string CellDescription(CellKind kind) => kind switch
    {
        CellKind.Wall => "Mountains or impassable terrain",
        CellKind.Road => "Open road or countryside",
        CellKind.City => "City",
        CellKind.Town => "Town",
        CellKind.Village => "Village",
        CellKind.Monastery => "Monastery",
        CellKind.Forest => "Forest",
        CellKind.Inn => "Inn",
        CellKind.Castle => "Castle",
        CellKind.Dungeon => "Dungeon or cave",
        CellKind.Start => "Starting area",
        _ => "",
    };
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<LevelEntryViewModel> Levels { get; }
    public ObservableCollection<AreaPoi> Pois { get; } = new();

    private LevelEntryViewModel? _selected;
    public LevelEntryViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            Pois.Clear();
            if (value != null) foreach (var poi in value.Pois) Pois.Add(poi);
            OnPropertyChanged(nameof(SelectedImage));
            OnPropertyChanged(nameof(SelectedDescription));
        }
    }

    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Levels = AreaData.Levels.Select(level => new LevelEntryViewModel(level)).ToList();
        Selected = Levels.FirstOrDefault();
    }
}
