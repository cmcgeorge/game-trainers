namespace DarkDesigns1Trainer.Game;

/// <summary>
/// The ~40 items of Dark Designs I, transcribed from the unpacked EXE strings.
/// </summary>
public static class ItemBook
{
    public enum ItemCategory { Weapon, Armor, Shield, Wand, Potion, Ring, Scroll, Key, Quest }

    public sealed record Item(string Name, ItemCategory Category, string Notes);

    public static readonly Item[] All =
    {
        new("Dagger",           ItemCategory.Weapon,  "Basic dagger"),
        new("Staff",            ItemCategory.Weapon,  "Staff weapon"),
        new("Mace",             ItemCategory.Weapon,  "Blunt weapon (priest-usable)"),
        new("Short Sword",      ItemCategory.Weapon,  "Basic sword"),
        new("Long Sword",       ItemCategory.Weapon,  "Standard one-handed sword"),
        new("Battle Axe",       ItemCategory.Weapon,  "Two-handed axe"),
        new("Two Hand Sword",   ItemCategory.Weapon,  "Large two-handed sword"),
        new("Hell Dagger",      ItemCategory.Weapon,  "Magical dagger"),
        new("Gravedigger Axe",  ItemCategory.Weapon,  "Magical axe"),
        new("Mangling Mace",    ItemCategory.Weapon,  "Magical mace"),
        new("Vampiric Sword",   ItemCategory.Weapon,  "Drains life"),
        new("Holy Sword",       ItemCategory.Weapon,  "Holy weapon"),
        new("Old Dark Sword",   ItemCategory.Weapon,  "Dark weapon"),
        new("Trident of Pain",  ItemCategory.Weapon,  "Magical trident"),
        new("Electroblade",     ItemCategory.Weapon,  "Electrical weapon"),
        new("Boom Blade",       ItemCategory.Weapon,  "Explosive weapon"),
        new("Striking Staff",   ItemCategory.Weapon,  "Magical staff"),
        new("Bone Basher",      ItemCategory.Weapon,  "Magical mace"),
        new("Active Axe",       ItemCategory.Weapon,  "Magical axe"),

        new("Shield",           ItemCategory.Shield,  "Basic shield"),
        new("Spiked Shield",    ItemCategory.Shield,  "Shield with spikes"),
        new("Magic Shield",     ItemCategory.Shield,  "Magical shield"),

        new("Leather Armor",    ItemCategory.Armor,   "Light armor"),
        new("Chain Mail",       ItemCategory.Armor,   "Medium armor"),
        new("Magic Armor",      ItemCategory.Armor,   "Magical armor"),
        new("Plate Mail",       ItemCategory.Armor,   "Heavy armor"),
        new("Full Plate",       ItemCategory.Armor,   "Heaviest armor"),

        new("Paralyze Wand",    ItemCategory.Wand,    "Paralyzes target"),
        new("Wand of Evil",     ItemCategory.Wand,    "Evil wand"),

        new("Healing Potion",   ItemCategory.Potion,  "Restores Body points"),
        new("Extra Healing",    ItemCategory.Potion,  "Restores more Body points"),
        new("Cureall Potion",   ItemCategory.Potion,  "Restores to max Body"),

        new("Medusa Skull",     ItemCategory.Wand,    "Stone gaze effect"),
        new("Speed Ring",       ItemCategory.Ring,    "Raises Dexterity"),
        new("Strength Ring",    ItemCategory.Ring,    "Raises Strength"),

        new("Recall Scroll",    ItemCategory.Scroll,  "Word of Recall effect"),

        new("Key 1",            ItemCategory.Key,     "Unlocks door type 1"),
        new("Key 2",            ItemCategory.Key,     "Unlocks door type 2"),
        new("Key 3",            ItemCategory.Key,     "Unlocks door type 3"),

        new("The Staff",        ItemCategory.Quest,   "Grelminar's Staff (quest item)"),
    };

    public static string CategoryName(ItemCategory c) => c.ToString();
}
