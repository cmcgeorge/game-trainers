using System.ComponentModel;
using System.Windows.Data;
using EyeOfTheBeholder1Trainer.Game;

namespace EyeOfTheBeholder1Trainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Spells, Classes, Races, Alignments).
/// Every collection is a static reference table from the <c>Game/</c> layer. Drives no
/// memory writes.
/// </summary>
public sealed class ReferenceViewModel
{
    public ICollectionView Spells { get; }
    public IReadOnlyList<string> Classes => CharacterFormat.ClassNames;
    public IReadOnlyList<string> Races => CharacterFormat.RaceNames;
    public IReadOnlyList<string> Alignments => CharacterFormat.AlignmentNames;

    public ReferenceViewModel()
    {
        Spells = new CollectionViewSource { Source = SpellBook.Spells.ToList() }.View;
        Spells.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellBook.SpellInfo.School)));
    }
}
