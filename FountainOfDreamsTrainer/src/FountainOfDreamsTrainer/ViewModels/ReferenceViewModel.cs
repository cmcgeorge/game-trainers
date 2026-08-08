using System.ComponentModel;
using System.Windows.Data;
using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Attributes, Skills, Professions, Items).
/// All tables are static reference data from the <c>Game/</c> layer; items are grouped by
/// category. Drives no memory writes.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<AttributeInfo> Attributes => AttributeBook.Attributes;
    public IReadOnlyList<SkillInfo> Skills => SkillBook.Skills;
    public IReadOnlyList<ProfessionInfo> Professions => ProfessionBook.Professions;
    public ICollectionView Items { get; }

    public ReferenceViewModel()
    {
        Items = new CollectionViewSource
        {
            Source = ItemBook.Items.Where(i => i.Id != CharacterFormat.InventoryEmpty).ToList()
        }.View;
        Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemInfo.Category)));
    }
}
