using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One spell row in the reference tab.</summary>
public sealed class SpellEntryViewModel
{
    public Spell Spell { get; }
    public SpellEntryViewModel(Spell spell) => Spell = spell;

    public string LevelGroup => $"Level {Spell.Level}";
    public string Display => $"{Spell.Code} — {Spell.Name}";
    public string CostText => $"L{Spell.Level}";
}

/// <summary>
/// Read-only spell reference: the full Bard's Tale spell list (all four arts), grouped
/// by spell level, shown in the 📖 Spells tab. The 4-letter codes are exactly what you
/// type at the in-game "Cast a spell?" prompt.
/// </summary>
public sealed class SpellReferenceViewModel
{
    public ICollectionView Magician { get; }
    public ICollectionView Conjurer { get; }
    public ICollectionView Sorcerer { get; }
    public ICollectionView Wizard { get; }

    public string SongsText =>
        "The Bard plays one of six tunes (Play tune # 1–6): "
        + string.Join(", ", Spellbook.BardSongs.Select((s, i) => $"{i + 1}) {s}")) + ".";

    public SpellReferenceViewModel()
    {
        Magician = Grouped(SpellClass.Magician);
        Conjurer = Grouped(SpellClass.Conjurer);
        Sorcerer = Grouped(SpellClass.Sorcerer);
        Wizard = Grouped(SpellClass.Wizard);
    }

    private static ICollectionView Grouped(SpellClass cls)
    {
        var col = new ObservableCollection<SpellEntryViewModel>(
            Spellbook.For(cls).Select(s => new SpellEntryViewModel(s)));
        var view = CollectionViewSource.GetDefaultView(col);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellEntryViewModel.LevelGroup)));
        return view;
    }
}
