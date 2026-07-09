using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// Read-only reference for the spell-list pop-up: the complete Cleric and Sorcerer
/// spell tables, each grouped by level, with name / cost / description. Independent of
/// any character — it just shows everything the game offers. Reuses
/// <see cref="SpellEntryViewModel"/> (all marked castable, since nothing is gated here).
/// </summary>
public sealed class SpellReferenceViewModel
{
    public ICollectionView ClericSpells { get; }
    public ICollectionView SorcererSpells { get; }

    public SpellReferenceViewModel()
    {
        ClericSpells = GroupedByLevel(Spellbook.Cleric);
        SorcererSpells = GroupedByLevel(Spellbook.Sorcerer);
    }

    private static ICollectionView GroupedByLevel(IReadOnlyList<Spell> spells)
    {
        var items = new ObservableCollection<SpellEntryViewModel>(
            spells.Select(s => new SpellEntryViewModel(s, castable: true)));
        var view = CollectionViewSource.GetDefaultView(items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellEntryViewModel.LevelGroup)));
        return view;
    }
}
