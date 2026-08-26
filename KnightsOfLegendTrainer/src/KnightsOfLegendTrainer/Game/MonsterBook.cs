namespace KnightsOfLegendTrainer.Game;

/// <summary>One monster type in Knights of Legend. [Manual]</summary>
public sealed record MonsterEntry(
    int Id,
    string Name,
    string Category,
    string Location,
    string Notes);

/// <summary>
/// Monsters encountered in Knights of Legend, organized by the category the game
/// assigns them. Each quest typically involves fighting a specific monster type in
/// a specific location. [Manual]
/// </summary>
internal static class MonsterBook
{
    public static IReadOnlyList<MonsterEntry> Monsters { get; } = new[]
    {
        new MonsterEntry(0, "Ruffian", "Human", "Tantowyn",
            "Quest 1 target. Weak but numerous; guard the stolen gavel."),
        new MonsterEntry(1, "Bandit", "Human", "North of Brettle",
            "Quest 2 target. Guards the stolen standard."),
        new MonsterEntry(2, "Ghoul", "Undead", "Klvar Wood",
            "Quest 3 target. Guards the stolen quill. Paralyzing touch."),
        new MonsterEntry(3, "Goblin", "Humanoid", "South of Brettle",
            "Quest 4 target. Guards the letters for the Truth Sword quest."),
        new MonsterEntry(4, "Sylph", "Elemental", "Prazen Point",
            "Quest 8 target. Air elemental; guards the pirate hat."),
        new MonsterEntry(5, "Minotaur", "Giant", "Ebbwater",
            "Quest 9 target. Strong melee fighter; guards the iron chest."),
        new MonsterEntry(6, "Orc", "Humanoid", "Mountain of Lorr",
            "Quest 10 target. Guards the golden necklace."),
        new MonsterEntry(7, "Skeleton", "Undead", "Southern river",
            "Quest 11 target. Guards the wand. Weak to crushing weapons."),
        new MonsterEntry(8, "Thug", "Human", "Tegal Forest",
            "Quest 12 target. Guards the coat of arms."),
        new MonsterEntry(9, "Muck Creature", "Elemental", "Downing Swamp",
            "Quest 13 target. Poisonous; guards the oil of changeling."),
        new MonsterEntry(10, "Mist Giant", "Giant", "Wesswald",
            "Quest 16 target. Guards the millet. Very tough; wear Courage Coat."),
        new MonsterEntry(11, "Ogre", "Giant", "The Darkwood",
            "Quest 17 target. Guards the golden chalice. Strong but slow."),
        new MonsterEntry(12, "Stone Ogre", "Giant", "Downing Mountains",
            "Quest 18 target. Very high armor; guards the hidden staff."),
        new MonsterEntry(13, "Ettin", "Giant", "Sheller Ridge",
            "Quest 19 target. Two-headed giant; guards the wristband."),
        new MonsterEntry(14, "Djinn", "Elemental", "Thanakesh Hills",
            "Quest 20 target. Air elemental; guards the djinn item. Ranged attacks."),
        new MonsterEntry(15, "Cliff Troll", "Humanoid", "Westwash",
            "Quest 21 target. Guards the shade ring. Regenerates; concentrate fire."),
        new MonsterEntry(16, "Sledge Creature", "Elemental", "Sodden Hills",
            "Quest 22 target. Party must split for this quest. Crushing attacks."),
        new MonsterEntry(17, "Troll", "Humanoid", "Missip Valley",
            "Quest 23 target. Guards the statuette. Regenerates each round."),
        new MonsterEntry(18, "Cyclops", "Giant", "Ghor Hills",
            "Quest 24 target. Final quest. Enormous Strength; wear Courage Coat."),
        new MonsterEntry(19, "Pirate", "Human", "West of Stone Island",
            "Encounter during quest 7. Pegleg gives the shipwheel quest."),
    };

    public static IReadOnlyList<MonsterEntry> ByCategory(string category) =>
        Monsters.Where(m => m.Category == category).ToList();

    public static MonsterEntry? ById(int id) =>
        id >= 0 && id < Monsters.Count ? Monsters[id] : null;
}
