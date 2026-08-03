using System.Collections.ObjectModel;
using DarkDesigns1Trainer.Game;

namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// Read-only reference data for the References tab: spells, items, and monsters.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public ObservableCollection<string> WizardSpells { get; } = new();
    public ObservableCollection<string> PriestSpells { get; } = new();
    public ObservableCollection<string> Items { get; } = new();
    public ObservableCollection<string> Monsters { get; } = new();

    public ReferenceViewModel()
    {
        foreach (var s in SpellBook.WizardSpells)
            WizardSpells.Add(SpellBook.SpellLabel(s));
        foreach (var s in SpellBook.PriestSpells)
            PriestSpells.Add(SpellBook.SpellLabel(s));
        foreach (var item in ItemBook.All)
            Items.Add($"{item.Name,-20} [{ItemBook.CategoryName(item.Category)}]  {item.Notes}");
        foreach (var m in MonsterBook.All)
            Monsters.Add($"{m.Name,-20} {m.Notes}");
    }
}
