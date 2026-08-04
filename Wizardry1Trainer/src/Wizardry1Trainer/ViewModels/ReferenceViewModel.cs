using System.ComponentModel;
using System.Windows.Data;
using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Spells). The spell collection is grouped
/// by school (Mage / Priest) for the grouped list template. Drives no memory writes.
/// </summary>
public sealed class ReferenceViewModel
{
    public ICollectionView Spells { get; }

    public ReferenceViewModel()
    {
        Spells = new CollectionViewSource { Source = SpellBook.Spells }.View;
        Spells.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellBook.SpellInfo.School)));
    }
}
