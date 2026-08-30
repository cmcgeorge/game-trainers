using System.Collections.ObjectModel;
using System.Windows.Media;
using EyeOfTheBeholder1Trainer.Game;

namespace EyeOfTheBeholder1Trainer.ViewModels;

public sealed class LevelEntryViewModel
{
    public DungeonLevel Level { get; }

    public LevelEntryViewModel(DungeonLevel level) => Level = level;

    public string Name => $"Level {Level.Index + 1}: {Level.Name}";
    public string Description => Level.Description;
    public IReadOnlyList<DungeonPoi> Pois => Level.Pois;
    public ImageSource Image => _image ??= MapRenderer.Render(Level);
    private ImageSource? _image;

    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Level.Width || y >= Level.Height) return "";
        if (Level.Grid[x, y] == CellKind.Wall) return "Wall";
        var poi = Level.Pois.FirstOrDefault(p => p.X == x && p.Y == y);
        return poi != null ? $"{poi.Name} — {poi.Description}" : "Open floor";
    }
}

public sealed class MapsViewModel : ObservableObject
{
    public IReadOnlyList<LevelEntryViewModel> Levels { get; }

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

    public ObservableCollection<DungeonPoi> Pois { get; } = new();
    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Levels = DungeonData.Levels.Select(level => new LevelEntryViewModel(level)).ToList();
        Selected = Levels.FirstOrDefault();
    }
}
