using System.Collections.ObjectModel;
using System.Windows.Input;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Paragraphs tab: the UMoria "monster memory" reference. The in-game <c>/</c> and <c>l</c>
/// commands render recall text from the live <c>c_recall[]</c> array (one <c>recall_type</c> per
/// creature, 279 of them). Until a COFF-base locator is built (Candidate — see
/// <c>.docs/ReverseEngineering.md</c> §7), this tab renders the **static** recall text from the
/// shipped roster in <see cref="MoriaTrainer.Game.MonsterBook"/>, so the user can look up any
/// creature's attacks, defenses, and tactics without a live attach.
/// </summary>
public sealed class ParagraphsViewModel : ObservableObject
{
    public ObservableCollection<CreatureInfo> Creatures { get; } = new(MonsterBook.Creatures);

    private CreatureInfo? _selected;
    public CreatureInfo? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            Paragraph = value == null ? "" : ParagraphBook.Render(value);
        }
    }

    private string _paragraph = "";
    public string Paragraph { get => _paragraph; private set => SetField(ref _paragraph, value); }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set
        {
            if (!SetField(ref _filter, value)) return;
            ApplyFilter();
        }
    }

    public ICommand ClearFilterCommand { get; }

    public ParagraphsViewModel()
    {
        ClearFilterCommand = new RelayCommand(_ => Filter = "");
        Selected = Creatures.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        var selected = Selected;
        Creatures.Clear();
        foreach (var c in ParagraphBook.Search(Filter))
            Creatures.Add(c);
        // Preserve the selection if it's still in the filtered list.
        Selected = Creatures.FirstOrDefault(c => selected != null && c.Id == selected.Id)
                   ?? Creatures.FirstOrDefault();
    }
}
