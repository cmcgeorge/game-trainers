using System.ComponentModel;
using System.Windows.Data;
using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Spells, Skills, Items, Monsters, Paragraphs,
/// Strategy). Every collection is a static reference table from the <c>Game/</c> layer; the spell and
/// item views are grouped (by magic school / item category) for the grouped list templates. Drives no
/// memory writes.
/// </summary>
public sealed class ReferenceViewModel
{
    public ICollectionView Spells { get; }
    public IReadOnlyList<SkillInfo> Skills => SkillBook.Skills;
    public ICollectionView Items { get; }
    public IReadOnlyList<MonsterInfo> Monsters => MonsterBook.Monsters;
    public IReadOnlyList<ParagraphEntry> Paragraphs => ParagraphBook.Paragraphs;
    public IReadOnlyList<WalkthroughSection> Strategy => Walkthrough.Sections;

    public ReferenceViewModel()
    {
        // Dedicated views (not the shared default view) so this tab's grouping is independent of
        // any other consumer of the same underlying reference tables.
        Spells = new CollectionViewSource { Source = SpellBook.Spells }.View;
        Spells.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellInfo.School)));

        Items = new CollectionViewSource { Source = ItemCatalog.Items }.View;
        Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemInfo.Category)));
    }
}
