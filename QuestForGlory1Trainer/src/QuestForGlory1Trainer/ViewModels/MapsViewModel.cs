using System.Collections.ObjectModel;
using System.Windows.Media;
using QuestForGlory1Trainer.Game;

namespace QuestForGlory1Trainer.ViewModels;

public sealed class AreaEntryViewModel
{
    public AreaEntryViewModel(AreaLevel area) => Area = area;

    public AreaLevel Area { get; }
    public string Name => Area.Name;
    public string Description => Area.Description;
    public IReadOnlyList<AreaPoi> Pois => Area.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Area);
    private ImageSource? _image;

    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Area.Width || y >= Area.Height) return "";
        if (Area.Grid[x, y] == CellKind.Wall) return "Impassable terrain";
        var poi = Area.Pois.FirstOrDefault(p => p.X == x && p.Y == y);
        return poi == null ? "Open path" : $"{poi.Name} — {poi.Description}";
    }
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<AreaEntryViewModel> Areas { get; }
    public ObservableCollection<AreaPoi> Pois { get; } = new();

    private AreaEntryViewModel? _selected;
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

    public MapsViewModel()
    {
        Areas = AreaData.Levels.Select(area => new AreaEntryViewModel(area)).ToList();
        Selected = Areas.FirstOrDefault();
    }
}
