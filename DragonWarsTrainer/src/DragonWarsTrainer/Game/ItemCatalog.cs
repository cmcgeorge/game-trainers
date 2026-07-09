namespace DragonWarsTrainer.Game;

/// <summary>One reference item: its category, name, price, effect stats, and notes.</summary>
public sealed record ItemInfo(string Category, string Name, string Price, string Stats, string Notes = "");

/// <summary>
/// Transcribed item reference for Dragon Wars, sourced from the hitchhikerprod "things and stuff"
/// walkthrough. Grouped by category for the References tab. Reference only — this drives no memory
/// writes; it complements the live-editable inventory on the Inventory tab.
/// </summary>
public static class ItemCatalog
{
    public static readonly IReadOnlyList<ItemInfo> Items = new ItemInfo[]
    {
        // --- Axes ---
        new("Axes", "Small Pick", "$50", "1d4", "Requires STR 4."),
        new("Axes", "Pick", "$60", "1d6", "Requires STR 7."),
        new("Axes", "Hand Axe", "$60", "1d6", "Requires STR 5."),
        new("Axes", "Battle Axe", "$70", "1d12, -1 AV", "Requires STR 17."),
        new("Axes", "War Axe", "$700", "1d12, -3 AV", "Requires STR 18."),
        new("Axes", "Rusty Axe", "$2000", "1d20, -3 AV", "Requires STR 18."),
        new("Axes", "Axe of Kalah", "$7000", "1d12 / 1d20 @50', +4 AV", "Requires STR 18."),
        new("Axes", "Magic Axe", "$700", "1d30, +1 AV", "Requires STR 20."),
        new("Axes", "Nature Axe", "$1500", "1d30, -6 AV", "Requires STR 18."),

        // --- Flails ---
        new("Flails", "Flail", "$40", "1d6", "Requires STR 10."),
        new("Flails", "War Flail", "$400", "1d12", "Requires STR 10."),
        new("Flails", "Bladed Flail", "$1000", "1d12", "Requires STR 10."),
        new("Flails", "Runed Flail", "$2000", "1d20, +2 AV, +1 AC", "Requires STR 14."),
        new("Flails", "Barbed Flail", "$2000", "1d30, +2 AV", "Requires STR 16."),
        new("Flails", "Spiked Flail", "$3000", "4d20, +2 AV, +1 AC", "Requires STR 16."),

        // --- Swords ---
        new("Swords", "Dagger", "$30", "1d4, +1 AV", "Requires STR 3."),
        new("Swords", "Ruby Dagger", "$40", "1d4, +3 AV", "Requires STR 3."),
        new("Swords", "Shortsword", "$50", "1d6, +1 AV", "Requires STR 8."),
        new("Swords", "Broadsword", "$60", "1d8, +1 AV", "Requires STR 12."),
        new("Swords", "Hook", "$60", "1d8, +1 AV", "Requires STR 10."),
        new("Swords", "Firesword", "$900", "1d12, +2 AV, +1 AC", "Requires STR 17."),
        new("Swords", "Lance Sword", "$60", "1d20 @20', +1 AV", "Requires STR 12."),
        new("Swords", "The Slicer", "—", "1d30, +4 AV, +2 AC", "Unbuyable. Requires STR 17."),
        new("Swords", "Dragon Tooth", "$6000", "2d20 @60', +8 AV, +2 AC", "Requires STR 12."),
        new("Swords", "Sword of Freedom", "—", "1d100, +15 AV, +5 AC", "Unbuyable. Casts Inferno when blessed."),

        // --- Two-Handers ---
        new("Two-Handers", "Pole Arm", "$90", "1d10 / 1d20, +1/+2 AV", "Requires STR 13/16."),
        new("Two-Handers", "Greatsword", "$80", "1d12, +1 AV", "Requires STR 17."),
        new("Two-Handers", "Grand Sword", "$5000", "2d12, +1 AV", "Requires STR 22."),
        new("Two-Handers", "Mountain Sword", "$2000", "1d30, +3 AV, +2 AC", "Casts Earth Summon. Mountain Lore 2."),
        new("Two-Handers", "Glow Sword", "$8000", "1d30, +1 AV", "Requires STR 24."),
        new("Two-Handers", "Holy Lance", "$9000", "3d20, +4 AV", "Requires STR 13."),
        new("Two-Handers", "Heavy Sword", "$8000", "8d8, -3 AV, -2 AC", "Requires STR 25."),
        new("Two-Handers", "Dragon Sword", "$5000", "4d20, +3 AV", "Requires STR 21."),

        // --- Maces ---
        new("Maces", "Mace", "$40", "1d8", "Requires STR 10."),
        new("Maces", "Old Peg Leg", "$200", "1d8, +1 AV", "Requires STR 10."),
        new("Maces", "Hammer", "$40", "1d10", "Requires STR 12."),
        new("Maces", "Long Mace", "$200", "1d12 / 1d20 @20', +1 AV", "Requires STR 15."),
        new("Maces", "Holy Mace", "$4000", "1d20, +2 AV, +1 AC", "Casts Exorcism. STR 12."),
        new("Maces", "Druids Mace", "$2000", "1d20, +2 AV, +2 AC", "Casts Cure All. STR 12."),
        new("Maces", "Throw Mace", "$4000", "2d12 / 1d12 @30', +1 AV", "Requires STR 18."),
        new("Maces", "Crush Mace", "$4000", "4d10", "Requires STR 15."),
        new("Maces", "Dwarf Hammer", "—", "1d30 @60'", "Unbuyable. Requires STR 20."),
        new("Maces", "Spell Staff", "$7000", "1d10, +5 AV, +8 AC", "Requires Low Magic 1."),
        new("Maces", "Mage Staff", "$20000", "1d20, +10 AV", ""),

        // --- Bows & Ammo ---
        new("Bows", "Bow", "$60", "20', rate 1", "Requires DEX 10."),
        new("Bows", "Long Bow", "$90", "+1 AV, 40'", "Requires DEX 14."),
        new("Bows", "Great Bow", "$310", "+2 AV, 50'", "Requires DEX 16."),
        new("Bows", "Archer's Bow", "$900", "+3 AV, 50'", "Requires DEX 18."),
        new("Bows", "Magic Bow", "$60", "+4 AV, 70'", "Requires DEX 10."),
        new("Bows", "Gatlin Bow", "$60", "20', auto rate", "Requires DEX 10."),
        new("Bows", "Crossbow", "$60", "30', rate 1", "Requires DEX 12."),
        new("Bows", "Tri-Cross", "$600", "+1 AV, 30', burst", "Requires DEX 15."),
        new("Ammo", "Arrows (White/Silver/Grey/Magic)", "—", "1d6..1d20", "Type 0 (bow)."),
        new("Ammo", "Magic Quiver", "$50", "1d4", "Type 0; self-refills each round."),
        new("Ammo", "Bolts (Long/Pierce/Mega/Dead)", "—", "1d4..1d20", "Type 1 (crossbow)."),

        // --- Thrown ---
        new("Thrown", "Javelin / Spear", "$40", "1d6 / 1d8 @30'-40'", "Requires DEX 12."),
        new("Thrown", "Holy Spear", "$40", "1d30 @10'", "Requires DEX 14."),
        new("Thrown", "Bomb", "$200", "1d30 / 2d30 @10'", "Underworld / Dwarves."),
        new("Thrown", "Fire Spear", "$400", "1d12 @50'", "Requires DEX 14."),
        new("Thrown", "Boomerang", "$700", "1d12 / 2d20 @50'-60', +2 AV, -1 AC", "Requires DEX 12/14."),
        new("Thrown", "Barbed Spear", "$4000", "1d20 @40'", "Requires DEX 16."),
        new("Thrown", "Trident", "$4000", "2d20 @40'", "Returns to inventory. DEX 15."),

        // --- Body Armor ---
        new("Body Armor", "Cloth Armor / Pilgrim Garb", "$25 / $20", "+1 AC", "Garb bypasses Old Dock guards."),
        new("Body Armor", "Royal Robe", "$2000", "+1 AC", ""),
        new("Body Armor", "Mage Cloth", "$2500", "+3 AC", "Casts Mage Light. Low Magic 1."),
        new("Body Armor", "Leather Armor", "$50", "+3 AC, -1 AV", ""),
        new("Body Armor", "Brigandine", "$80", "+4 AC, -1 AV", ""),
        new("Body Armor", "Scale Armor", "$250", "+6 AC, -2 AV", ""),
        new("Body Armor", "Chain Armor", "$310", "+7 AC, -3 AV", ""),
        new("Body Armor", "Magic Chain", "$6000", "+7 AC, 0 AV", ""),
        new("Body Armor", "Plate Mail", "$3100", "+10 AC, -5 AV", ""),
        new("Body Armor", "Magic Plate", "$50000", "+10 AC, -2 AV", ""),
        new("Body Armor", "Heavy Plate", "$4000", "+12 AC, -6 AV", ""),
        new("Body Armor", "Dragon Plate", "$3100", "+14 AC, -3 AV", ""),

        // --- Accessories ---
        new("Accessories", "Gauntlets / Silver Gloves", "$700 / $2000", "+2 / +3 AC", ""),
        new("Accessories", "Shield", "$1000", "+2 AC", "Requires STR 10."),
        new("Accessories", "Large Shield", "—", "+3 AC, -2 AV", ""),
        new("Accessories", "Magic Shield", "—", "+4 AC", ""),
        new("Accessories", "Fire Shield", "$5000", "+2 AC, -2 AV", ""),
        new("Accessories", "Dragon Shield", "$12000", "+5 AC", "Requires STR 10."),
        new("Accessories", "Helm / Gem / Black / Dragon Helm", "—", "+1..+4 AC", "Black casts Zak's Speed; Dragon -1 AV."),
        new("Accessories", "Lucky / Golden Boots", "—", "+1 / +2 AC", "Golden Boots hop environmental gaps."),
        new("Accessories", "The Ring", "$1250", "+2 AV, +2 AC", "Requires Cloak Arcane (High Magic)."),
        new("Accessories", "Mage Ring", "—", "+1 AV, +4 AC", "Casts Whirl Wind. Low Magic 3."),
        new("Accessories", "Magic Ring", "—", "+1 AV, +2 AC", ""),

        // --- Quest Items ---
        new("Quest", "Citizenship Papers", "—", "", "Cross the Forlorn Guard Bridge."),
        new("Quest", "Golden Boots", "—", "", "Jump gaps to Isle of Woe / Sword of Freedom."),
        new("Quest", "Governor's Pass", "—", "", "Cross the War Bridge."),
        new("Quest", "Kings / Lansk Ticket", "—", "", "Board the respective ferries."),
        new("Quest", "Stone Arms / Hand / Head / Trunk", "—", "", "Repair the Mud Toad statue."),
        new("Quest", "Dragon Gem", "—", "", "Given by the Lansk dragon; used in Nisir."),
        new("Quest", "Silver Key", "—", "", "From Nergal; frees Irkalla on the Isle of Woe."),
        new("Quest", "Water Potion", "—", "", "Breathe underwater in the Sunken Ruins."),
        new("Quest", "Dead Body", "—", "", "Throw into the Magan Pit to win the game."),
    };
}
