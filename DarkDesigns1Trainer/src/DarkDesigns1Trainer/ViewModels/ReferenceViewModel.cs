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
        foreach (var item in ItemBook.All.Where(i => i.IsPlayerItem))
            Items.Add($"{item.Id,3}  {item.Name,-18} {item.Type,-10} {item.ClassLabel,-6} " +
                      (item.Protection > 0 ? $"prot {item.Protection,-4} " : $"pow {item.Power,-5} ") +
                      (item.Price > 0 ? $"{item.Price} gp" : ""));
        foreach (var m in MonsterBook.All)
            Monsters.Add($"{m.Name,-20} {m.Notes}");
    }
}
