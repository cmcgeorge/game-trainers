using System.Collections.ObjectModel;
using System.Windows.Media;
using Questron2Trainer.Game;

namespace Questron2Trainer.ViewModels;

public sealed class AreaEntryViewModel
{
    public AreaLevel Area { get; }
    public AreaEntryViewModel(AreaLevel area) => Area = area;
    public string Name => $"Area {Area.Index + 1}: {Area.Name}";
    public string Description => Area.Description;
    public IReadOnlyList<AreaPoi> Pois => Area.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Area);
    private ImageSource? _image;
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
        Areas = AreaData.Areas.Select(area => new AreaEntryViewModel(area)).ToList();
        Selected = Areas.FirstOrDefault();
    }
}
