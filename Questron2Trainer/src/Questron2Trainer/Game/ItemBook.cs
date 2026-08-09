namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II item.</summary>
public sealed record ItemInfo(int Id, string Name, string Category);

/// <summary>
/// The key items of Questron II, extracted from START.EXE strings.
/// Includes the twelve keys, special quest items, and transports.
/// </summary>
public static class ItemBook
{
    public static readonly ItemInfo[] Items =
    {
        // Keys
        new(0,  "Gold Key",       "Key"),
        new(1,  "Opal Key",       "Key"),
        new(2,  "Iron Key",       "Key"),
        new(3,  "Brass Key",      "Key"),
        new(4,  "Copper Key",     "Key"),
        new(5,  "Silver Key",     "Key"),
        new(6,  "Emerald Key",    "Key"),
        new(7,  "Onyx Key",       "Key"),
        new(8,  "Ruby Key",       "Key"),
        new(9,  "Agate Key",      "Key"),
        new(10, "Sapphire Key",   "Key"),
        new(11, "Black Key",      "Key"),
        // Special quest items
        new(12, "Unicorn Horn",        "Quest Item"),
        new(13, "Wand of Power",       "Quest Item"),
        new(14, "Eternal Flame",       "Quest Item"),
        new(15, "Book of Magic",       "Quest Item"),
        new(16, "Crystal Goblet",      "Quest Item"),
        new(17, "Chalice of Arvyl",    "Quest Item"),
        new(18, "Moonstone Amulet",    "Quest Item"),
        new(19, "Orb of Enchantment",  "Quest Item"),
        new(20, "Scroll of Scalna",    "Quest Item"),
        new(21, "Rope & Hooks",        "Quest Item"),
        new(22, "Bread of Life",       "Quest Item"),
        // Transports
        new(23, "Camalon",             "Transport"),
        new(24, "Trained Eagle",       "Transport"),
    };

    public static int Count => Items.Length;
}
