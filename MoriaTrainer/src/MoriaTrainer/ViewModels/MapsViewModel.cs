using System.Collections.ObjectModel;
using System.Windows.Input;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Maps tab: a read-only reference of UMoria's 51 descent levels (town + 50 dungeon
/// levels). Because dungeon layouts are **procedurally generated** each descent, there are no fixed
/// maps to draw — this tab documents what each depth contains, what items first appear there, and
/// which monsters dominate, so the player can plan a delve. The live teleport helper lives on the
/// Teleport tab (it needs a relative scan to locate <c>char_row</c>/<c>char_col</c> first).
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    public ObservableCollection<LevelInfo> Levels { get; } = new(LevelBook.Levels);

    private LevelInfo? _selected;
    public LevelInfo? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            Detail = value == null ? "" : FormatDetail(value);
        }
    }

    private string _detail = "";
    public string Detail { get => _detail; private set => SetField(ref _detail, value); }

    public ICommand SelectBalrogCommand { get; }

    public MapsViewModel()
    {
        SelectBalrogCommand = new RelayCommand(_ => Selected = LevelBook.BalrogLevel);
        Selected = Levels.FirstOrDefault();
    }

    private static string FormatDetail(LevelInfo l)
    {
        var lines = new List<string>
        {
            $"{l.Name} — depth {l.Depth} ({l.Feet} ft)",
            $"Notable monsters: {l.NotableMonsters}",
            $"Notable items: {l.NotableItems}",
            l.Notes,
        };
        if (l.IsTown)
            lines.Add("The town is the only fixed map in the game. Six stores, walled, one down-staircase.");
        if (l.IsBalrogLevel)
            lines.Add("BALROG LEVEL: the Balrog of Moria spawns here. Killing it wins the game and retires the character.");
        return string.Join(Environment.NewLine, lines);
    }
}
