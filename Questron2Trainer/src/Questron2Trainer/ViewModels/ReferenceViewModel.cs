using Questron2Trainer.Game;

namespace Questron2Trainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Spells, Weapons, Armor, Items, Monsters,
/// Locations). Every collection is a static reference table from the <c>Game/</c> layer.
/// Drives no memory writes.
/// </summary>
public sealed class ReferenceViewModel
{
    public IReadOnlyList<SpellInfo> Spells => SpellBook.Spells;
    public IReadOnlyList<WeaponInfo> Weapons => WeaponBook.Weapons;
    public IReadOnlyList<ArmorInfo> Armors => ArmorBook.Armors;
    public IReadOnlyList<ItemInfo> Items => ItemBook.Items;
    public IReadOnlyList<MonsterInfo> Monsters => MonsterBook.Monsters;
    public IReadOnlyList<LocationInfo> Locations => LocationBook.Locations;
}
