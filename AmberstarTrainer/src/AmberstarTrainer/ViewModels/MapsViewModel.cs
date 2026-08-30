using System.Collections.ObjectModel;
using System.Windows.Media;
using AmberstarTrainer.Game;

namespace AmberstarTrainer.ViewModels;

public sealed class AreaEntryViewModel
{
    public AreaLevel Area { get; }

    public AreaEntryViewModel(AreaLevel area) => Area = area;

    public string Name => $"{Area.Index + 1}. {Area.Name}";
    public string Description => Area.Description;
    public IReadOnlyList<AreaPoi> Pois => Area.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Area);
    private ImageSource? _image;

    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Area.Width || y >= Area.Height) return "";
        var poi = Area.Pois.FirstOrDefault(item => item.X == x && item.Y == y);
        return poi is not null ? $"{poi.Name} — {poi.Description}" : Area.AreaTerrainName(Area.Grid[x, y]);
    }
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<AreaEntryViewModel> Areas { get; }

    private AreaEntryViewModel? _selected;
    public AreaEntryViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            Pois.Clear();
            if (value is not null)
                foreach (var poi in value.Pois) Pois.Add(poi);
            OnPropertyChanged(nameof(SelectedImage));
            OnPropertyChanged(nameof(SelectedDescription));
        }
    }

    public ObservableCollection<AreaPoi> Pois { get; } = new();
    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Areas = AreaData.Levels.Select(area => new AreaEntryViewModel(area)).ToList();
        Selected = Areas.FirstOrDefault();
    }
}

file static class AreaLevelExtensions
{
    public static string AreaTerrainName(this AreaLevel _, AreaCellKind kind) => kind switch
    {
        AreaCellKind.Floor => "Open ground",
        AreaCellKind.Water => "Water",
        AreaCellKind.Mountain => "Mountain",
        AreaCellKind.Forest => "Forest",
        AreaCellKind.Desert => "Desert",
        _ => "Wall",
    };
}
