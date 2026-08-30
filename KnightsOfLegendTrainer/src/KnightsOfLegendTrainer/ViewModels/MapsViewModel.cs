using System.Collections.ObjectModel;
using System.Windows.Media;
using KnightsOfLegendTrainer.Game;

namespace KnightsOfLegendTrainer.ViewModels;

public sealed class AreaEntryViewModel
{
    private ImageSource? _image;

    public AreaEntryViewModel(AreaLevel level) => Level = level;

    public AreaLevel Level { get; }
    public string Name => Level.Name;
    public string Description => Level.Description;
    public IReadOnlyList<AreaPoi> Pois => Level.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Level);

    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Level.Width || y >= Level.Height) return "";
        if (Level.Grid[x, y] == CellKind.Wall) return "Wall";
        var poi = Level.Pois.FirstOrDefault(point => point.X == x && point.Y == y);
        return poi == null ? "Open area" : $"{poi.Name} — {poi.Description}";
    }
}

public sealed class MapsViewModel : ObservableObject
{
    private AreaEntryViewModel? _selected;

    public MapsViewModel()
    {
        Levels = AreaData.Levels.Select(level => new AreaEntryViewModel(level)).ToList();
        Selected = Levels.FirstOrDefault();
    }

    public IReadOnlyList<AreaEntryViewModel> Levels { get; }
    public ObservableCollection<AreaPoi> Pois { get; } = new();

    public AreaEntryViewModel? Selected
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
}
