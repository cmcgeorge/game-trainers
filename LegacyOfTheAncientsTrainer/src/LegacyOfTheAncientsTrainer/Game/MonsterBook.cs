namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients monster.</summary>
public sealed record MonsterInfo(int Id, string Name, string Category, string? Note = null);

/// <summary>
/// The 44 monsters of Legacy of the Ancients — 32 wilderness and 12 dungeon —
/// from the game manual and walkthrough. Wilderness monsters are grouped by terrain
/// type; dungeon monsters are split across depth ranges.
/// </summary>
public static class MonsterBook
{
    public static readonly MonsterInfo[] Monsters =
    {
        // Wilderness — travelling creatures
        new(0,  "Pixie",            "Wilderness", "May talk or attack"),
        new(1,  "Strider",          "Wilderness", "May talk or attack"),
        new(2,  "Farmer",           "Wilderness", "May talk or attack"),
        new(3,  "Eaton Warrior",    "Wilderness", "May talk or attack"),
        new(4,  "Bandit",           "Wilderness", "May talk or attack"),
        new(5,  "Shadow Wisp",      "Wilderness", "May talk or attack"),
        new(6,  "Huggyn",           "Wilderness", "May talk or attack"),
        // Wilderness — ocean
        new(7,  "Sprayfish",        "Wilderness", "Ocean; very dangerous"),
        new(8,  "Wave Skimmer",     "Wilderness", "Ocean"),
        new(9,  "Sea Swallow",      "Wilderness", "Ocean; very dangerous"),
        new(10, "Giant Mantaray",   "Wilderness", "Ocean"),
        // Wilderness — desert
        new(11, "Wind Stalker",     "Wilderness", "Desert"),
        new(12, "Scorpod",          "Wilderness", "Desert"),
        // Wilderness — forest/plains
        new(13, "Bone Dweller",     "Wilderness", "Forest"),
        new(14, "Practon Piercer",  "Wilderness", "Forest"),
        new(15, "Carrion Mangler",  "Wilderness", "Plains"),
        new(16, "Ventro Flailer",   "Wilderness", "Plains"),
        new(17, "Stinging Rakish",  "Wilderness", "Forest"),
        new(18, "Blistopod",        "Wilderness", "Plains"),
        new(19, "Pit Striker",      "Wilderness", "Forest"),
        // Wilderness — swamp
        new(20, "Slash Nettle",     "Wilderness", "Swamp"),
        new(21, "Venom Floater",    "Wilderness", "Swamp"),
        // Wilderness — mountains
        new(22, "Pulp Crawler",     "Wilderness", "Mountain"),
        new(23, "Thrust Creeper",   "Wilderness", "Mountain"),
        new(24, "Slime Wierd",      "Wilderness", "Mountain"),
        new(25, "Scrabbler",        "Wilderness", "Desert; very dangerous"),
        // Wilderness — tundra/high
        new(26, "Neural Cloud",     "Wilderness", "Tundra"),
        new(27, "Churler",          "Wilderness", "Tundra"),
        new(28, "Rock Beetle",      "Wilderness", "Mountain; very dangerous"),
        new(29, "Mammoth Screecher", "Wilderness", "Tundra"),
        new(30, "Mime Ghoul",       "Wilderness", "Tundra"),
        new(31, "Maston Leaper",    "Wilderness", "Mountain; fears only bladed staff"),

        // Dungeon — levels 1-4
        new(32, "Nerve Streaker",   "Dungeon 1-4"),
        new(33, "Gnasher Turtle",   "Dungeon 1-4"),
        new(34, "Tendro Snapper",   "Dungeon 1-4"),
        new(35, "Night Stalker",    "Dungeon 1-4"),
        new(36, "Grappler",         "Dungeon 1-4"),
        new(37, "Knuckles",         "Dungeon 1-4", "Destroys weapon!"),

        // Dungeon — levels 5-8
        new(38, "Dangler",          "Dungeon 5-8", "Drains endurance!"),
        new(39, "Mr Potato",        "Dungeon 5-8"),
        new(40, "Raker Brute",      "Dungeon 5-8"),
        new(41, "Blue Lion",        "Dungeon 5-8"),
        new(42, "Giant Slug",       "Dungeon 5-8"),
        new(43, "Slime Wart",       "Dungeon 5-8"),
    };

    public static int Count => Monsters.Length;
    public static int WildernessCount => 32;
    public static int DungeonCount => 12;
}
