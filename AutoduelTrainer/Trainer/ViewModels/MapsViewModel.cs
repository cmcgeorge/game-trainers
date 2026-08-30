using System.Collections.ObjectModel;
using System.Windows.Media;
using AutoduelTrainer.Game;

namespace AutoduelTrainer.ViewModels;

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
        var poi = Area.Pois.FirstOrDefault(item => item.X == x && item.Y == y);
        if (poi is not null) return $"{poi.Name} — {poi.Description}";
        return Area.Grid[x, y] switch { CellKind.Wall => "Building or obstacle", CellKind.Road => "Highway or road", _ => "Open space" };
    }
}

public sealed class MapsViewModel : ViewModelBase
{
    public IReadOnlyList<AreaEntryViewModel> Areas { get; }
    public ObservableCollection<AreaPoi> Pois { get; } = new();

    private AreaEntryViewModel? _selected;
    public AreaEntryViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            Pois.Clear();
            if (value is not null) foreach (var poi in value.Pois) Pois.Add(poi);
            Raise(nameof(SelectedImage));
            Raise(nameof(SelectedDescription));
        }
    }

    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Areas = AreaData.Areas.Select(area => new AreaEntryViewModel(area)).ToList();
        Selected = Areas.FirstOrDefault();
    }
}
