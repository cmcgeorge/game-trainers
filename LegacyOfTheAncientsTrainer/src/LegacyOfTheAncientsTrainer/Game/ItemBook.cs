namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients special item.</summary>
public sealed record ItemInfo(int Id, string Name, string Category, string? Note = null);

/// <summary>
/// The special items of Legacy of the Ancients, from the game manual and walkthrough.
/// Includes keys, quest items, coins, and consumables.
/// </summary>
public static class ItemBook
{
    public static readonly ItemInfo[] Items =
    {
        // Keys
        new(0,  "Stone Key",     "Key", "Opens most castle doors"),
        new(1,  "Iron Key",      "Key", "Given by caretaker at level 3"),
        new(2,  "Copper Key",    "Key", "Found in Kelfor basement; access to Wizard of Potions"),
        new(3,  "Brass Key",     "Key", "Found in Kelfor basement; access to special chest"),

        // Quest items
        new(4,  "Compendium",    "Quest Item", "The Wizard's Compendium — the stolen scroll; your quest"),
        new(5,  "Scepter",       "Quest Item", "Locked in a chest in Kelfor Castle"),
        new(6,  "Crown",         "Quest Item", "Found in Pirates' Cave basement"),
        new(7,  "Guard Jewel",   "Quest Item", "Four needed to thwart the compendium"),
        new(8,  "Tulip",         "Quest Item", "In Kelfor Castle; return to museum fountain for +10 charm"),
        new(9,  "Magic Seeds",   "Quest Item", "Found in Kelfor; make guards oblivious (regenerate)"),
        new(10, "Magic Ice",     "Quest Item", "Return scepter & crown to caretaker; access to castle basement"),
        new(11, "Compass",       "Quest Item", "Found in Pirates' Cave level 2; shows direction"),

        // Equipment items
        new(12, "Gold Armband",  "Equipment", "Use near museum door to exit"),
        new(13, "Climbing Gear", "Equipment", "Allows crossing mountains"),
        new(14, "Healing Herbs", "Consumable", "Heal half max HP; up to 40 carried"),
        new(15, "Raft",          "Transport", "Buyable at general stores"),
        new(16, "Mail",          "Quest Item", "Carry between towns for gold reward"),

        // Museum coins
        new(17, "Jade Coin",     "Coin", "Basic exhibits; start with two"),
        new(18, "Topaz Coin",    "Coin", "More complex exhibits; greater payoffs"),
        new(19, "Amethyst Coin", "Coin", "Stones of Wisdom; tapestry exhibit"),
        new(20, "Sapphire Coin", "Coin", "Access to hidden/lost exhibits"),
        new(21, "Turquoise Coin","Coin", "Learn the guardians' password"),
        new(22, "Ruby Coin",     "Coin", "Enter the toughest dungeon"),
        new(23, "Diamond Coin",  "Coin", "Leads to the final confrontation (Pegasus)"),
    };

    public static int Count => Items.Length;
}
