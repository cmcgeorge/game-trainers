using System.Collections.ObjectModel;
using System.Windows.Media;
using WastelandRemasteredTrainer.Game;

namespace WastelandRemasteredTrainer.ViewModels;

public sealed class AreaLevelEntryViewModel
{
    public AreaLevel Level { get; }

    public AreaLevelEntryViewModel(AreaLevel level) => Level = level;

    public string Name => Level.Name;
    public string Description => Level.Description;
    public IReadOnlyList<AreaPoi> Pois => Level.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Level);

    private ImageSource? _image;
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<AreaLevelEntryViewModel> Areas { get; }
    public ObservableCollection<AreaPoi> Landmarks { get; } = new();

    private AreaLevelEntryViewModel? _selected;
    public AreaLevelEntryViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            Landmarks.Clear();
            if (value != null) foreach (var poi in value.Pois) Landmarks.Add(poi);
            OnPropertyChanged(nameof(SelectedImage));
            OnPropertyChanged(nameof(SelectedDescription));
        }
    }

    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Areas = AreaData.Areas.Select(area => new AreaLevelEntryViewModel(area)).ToList();
        Selected = Areas.FirstOrDefault();
    }
}
