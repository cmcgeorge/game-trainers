using System.Collections.ObjectModel;
using System.Windows.Media;
using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.ViewModels;

/// <summary>
/// One dungeon level in the picker: its reference data plus the rendered image.
/// The image is created once on first use.
/// </summary>
public sealed class LevelEntryViewModel
{
    public DungeonLevel Level { get; }

    public LevelEntryViewModel(DungeonLevel level) => Level = level;

    public string Name => $"Level {Level.Index + 1}: {Level.Name}";
    public string Description => Level.Description;
    public IReadOnlyList<DungeonPoi> Pois => Level.Pois;

    public ImageSource Image => _image ??= MapRenderer.Render(Level);
    private ImageSource? _image;

    /// <summary>What is on the given square, for the status line.</summary>
    public string Describe(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Level.Width || y >= Level.Height) return "";
        var cell = Level.Grid[x, y];
        if (cell == CellKind.Wall) return "Wall";
        var poi = Level.Pois.FirstOrDefault(p => p.X == x && p.Y == y);
        return poi != null ? $"{poi.Name} — {poi.Description}" : "Open floor";
    }
}

/// <summary>
/// Backs the Maps tab: the full list of Wizardry 1's ten dungeon levels and the
/// currently selected level to display. Purely a reference — Wizardry 1's maze
/// position is inside the p-system heap at an unknown offset, so there is no
/// live position tracking or teleport.
/// </summary>
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
            if (value != null) foreach (var p in value.Pois) Pois.Add(p);
            OnPropertyChanged(nameof(SelectedImage));
            OnPropertyChanged(nameof(SelectedDescription));
        }
    }

    public ObservableCollection<DungeonPoi> Pois { get; } = new();

    public ImageSource? SelectedImage => _selected?.Image;
    public string SelectedDescription => _selected?.Description ?? "";

    public MapsViewModel()
    {
        Levels = DungeonData.Levels.Select(l => new LevelEntryViewModel(l)).ToList();
        Selected = Levels.FirstOrDefault();
    }
}
