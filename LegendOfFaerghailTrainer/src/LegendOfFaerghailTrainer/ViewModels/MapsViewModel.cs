using System.Collections.ObjectModel;
using System.Windows.Media;
using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.ViewModels;

public sealed class MapEntryViewModel
{
    public MapEntryViewModel(AreaLevel map) => Map = map;
    public AreaLevel Map { get; }
    public string Name => Map.Name;
    public string Description => Map.Description;
    public IReadOnlyList<AreaPoi> Pois => Map.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Map);
    private ImageSource? _image;
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<MapEntryViewModel> Maps { get; }
    public ObservableCollection<AreaPoi> Pois { get; } = new();

    private MapEntryViewModel? _selected;
    public MapEntryViewModel? Selected
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

    public ImageSource? SelectedImage => Selected?.Image;
    public string SelectedDescription => Selected?.Description ?? "";

    public MapsViewModel()
    {
        Maps = AreaData.Levels.Select(map => new MapEntryViewModel(map)).ToList();
        Selected = Maps.FirstOrDefault();
    }
}
