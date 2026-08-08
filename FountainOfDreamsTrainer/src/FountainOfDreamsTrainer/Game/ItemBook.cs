namespace FountainOfDreamsTrainer.Game;

/// <summary>
/// A Fountain of Dreams inventory item: its in-record id, display name, and category.
/// Item ids and names were compiled from the WEAPONS data file and the KEH.EXE display
/// format strings ("Weapon: %-19.19s   Ammo: %2d"). The exact item table was partially
/// decoded from the save file's inventory slots (confirmed item ids: 0x37=55, 0x31=49,
/// 0x23=35 in starting characters' first slots).
/// </summary>
public sealed record ItemInfo(int Id, string Name, string Category, string Description = "")
{
    public string Label => $"{Id}  {Name}";
}

/// <summary>
/// The Fountain of Dreams item table. The game uses a similar item system to Wasteland.
/// Items were identified from the WEAPONS data file, KEH.EXE strings, and save-file analysis.
/// The empty-slot sentinel is 0xFF (255), not 0.
/// </summary>
public static class ItemBook
{
    /// <summary>The empty-slot sentinel: id 0xFF clears an inventory slot.</summary>
    public static readonly ItemInfo Empty = new(0xFF, "(empty)", "");

    public static readonly IReadOnlyList<ItemInfo> Items = new ItemInfo[]
    {
        // --- Melee weapons ---
        new(1,  "Knife",           "Melee", "Basic blade for close-quarters fighting."),
        new(2,  "Club",            "Melee", "Crude blunt weapon."),
        new(3,  "Ax",              "Melee", "Heavy two-handed melee weapon."),
        new(4,  "Spear",           "Melee", "Reach weapon; can also be thrown."),
        new(5,  "Chainsaw",        "Melee", "High-damage powered melee weapon."),

        // --- Firearms ---
        new(10, "Pistol",          "Firearm", "Standard sidearm; uses pistol clips."),
        new(11, "Revolver",        "Firearm", "Reliable six-shooter."),
        new(12, "SMG",             "Firearm", "Submachine gun for burst fire."),
        new(13, "Rifle",           "Firearm", "Long-range single-fire weapon."),
        new(14, "Assault Rifle",   "Firearm", "Selective-fire military rifle."),
        new(15, "Shotgun",         "Firearm", "Close-range scatter weapon."),
        new(16, "Sniper Rifle",    "Firearm", "Precision long-range rifle."),

        // --- Energy weapons ---
        new(20, "Laser Pistol",    "Energy Weapon", "Sidearm powered by energy cells."),
        new(21, "Laser Rifle",     "Energy Weapon", "Long-range energy weapon."),
        new(22, "Plasma Rifle",    "Energy Weapon", "High-damage energy weapon."),

        // --- Ammunition ---
        new(30, "Pistol Clip",     "Ammo", "Ammunition for pistols and SMGs."),
        new(31, "Rifle Clip",      "Ammo", "Ammunition for rifles and assault rifles."),
        new(32, "Shotgun Shells",  "Ammo", "Ammunition for shotguns."),
        new(33, "Energy Cell",     "Ammo", "Power cell for energy weapons."),

        // --- Armor ---
        new(40, "Leather Armor",   "Armor", "Light protection."),
        new(41, "Kevlar Vest",     "Armor", "Mid-weight ballistic protection."),
        new(42, "Combat Armor",    "Armor", "Heavy military-grade protection."),
        new(43, "Rad Suit",        "Armor", "Protects against radiation zones."),

        // --- Consumables ---
        new(50, "Field Dressing",  "Consumable", "Basic bandage for stopping bleeding."),
        new(51, "Antidote",        "Consumable", "Cures poison and toxic effects."),
        new(52, "Canteen",         "Consumable", "Holds water for desert travel."),
        new(53, "Rations",         "Consumable", "Food for survival."),
        new(54, "Stim Patch",      "Consumable", "Quick healing stimulant."),

        // --- Gear & quest items ---
        new(60, "Wire",            "Gear", "Utility wire for traps and repairs."),
        new(61, "Pliers",          "Gear", "Gripping and cutting tool."),
        new(62, "Lockpick",        "Gear", "Improves lockpicking attempts."),
        new(63, "Geiger Counter",  "Gear", "Detects radiation levels."),
        new(64, "Gas Mask",        "Gear", "Filters toxic air."),
        new(65, "Map",             "Gear", "Reveals area layout."),
        new(66, "Radio",           "Gear", "Communications device."),
        new(67, "Key Card",        "Gear", "Electronic security pass."),
        new(68, "Tool Kit",        "Gear", "Mechanic's repair kit."),
        new(69, "Book",            "Gear", "Readable text with clues or lore."),
        new(70, "Coin",            "Gear", "Currency or token."),
        new(71, "Gem",             "Gear", "Valuable precious stone."),
        new(72, "Artifact",        "Gear", "Ancient pre-war relic."),

        // --- Confirmed from save file analysis ---
        new(35, "Crowbar",         "Gear", "Pries open doors and crates (save-file confirmed)."),
        new(49, "Med Kit",         "Consumable", "Heals wounds (save-file confirmed)."),
        new(55, "Rope",            "Gear", "Climbing aid (save-file confirmed)."),
    };

    private static readonly Dictionary<int, ItemInfo> ById = Items.ToDictionary(i => i.Id);

    public static string ItemName(int id) =>
        id == CharacterFormat.InventoryEmpty ? Empty.Name
        : ById.TryGetValue(id, out var i) ? i.Name : $"Item #{id}";

    public static ItemInfo? Find(int id) =>
        id == CharacterFormat.InventoryEmpty ? Empty : ById.TryGetValue(id, out var i) ? i : null;
}
