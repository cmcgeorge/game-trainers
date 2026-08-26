namespace KnightsOfLegendTrainer.Game;

/// <summary>One quest in Knights of Legend. [Manual]</summary>
public sealed record QuestEntry(
    int Id,
    string Name,
    string QuestGiver,
    string Location,
    string Keyword,
    string TargetLocation,
    string Monster,
    string Reward,
    string Notes);

/// <summary>
/// The 24 quests of Knights of Legend, in the order they must be attempted.
/// All 24 must be completed before the final rescue of Seggallion. Each quest
/// has a giver (NPC), a keyword for conversation, a target location, and a
/// monster to defeat. [Manual]
/// </summary>
internal static class QuestBook
{
    public static IReadOnlyList<QuestEntry> Quests { get; } = new[]
    {
        new QuestEntry(0, "The Stolen Gavel", "Stephanie", "Brettle", "gavel",
            "Tantowyn", "Ruffians", "Access to further quests",
            "First quest. Talk to Stephanie in Brettle and say 'gavel'. Fight ruffians in Tantowyn."),
        new QuestEntry(1, "The Stolen Standard", "Stephen", "Brettle", "standard",
            "North of Brettle", "Bandits", "Reputation",
            "Talk to Stephen, say 'standard'. Fight bandits north of Brettle."),
        new QuestEntry(2, "The Stolen Quill", "Hegissa", "Brettle", "knight",
            "Klvar Wood", "Ghouls", "Access to Klvar Wood",
            "Talk to Hegissa, say 'knight'. Fight ghouls in Klvar Wood."),
        new QuestEntry(3, "The Truth Sword", "Mayor Benjamin", "Brettle", "",
            "South of Brettle", "Goblins", "Truth Sword (4-32 damage, very light)",
            "Combine letters K+A+M from goblins south of Brettle. Truth Sword is one of the best weapons."),
        new QuestEntry(4, "The Crown", "Biblik the Sage", "Htron", "",
            "Tegal River", "", "Crown",
            "Talk to Biblik in Htron. Search the Tegal River area."),
        new QuestEntry(5, "The Parth Oil", "Sam", "Htron", "stod",
            "Berthand's Bay", "", "Parth Oil",
            "Talk to Sam in Htron, say 'stod'. Search Berthand's Bay."),
        new QuestEntry(6, "The Shipwheel", "Pegleg", "West of Stone Island", "Nobjor",
            "Erwenwald", "", "Shipwheel",
            "Find Pegleg (pirates west of Stone Island), say 'Nobjor'. Search Erwenald."),
        new QuestEntry(7, "The Pirate Hat", "Scotty", "", "map",
            "Prazen Point", "Sylphs", "Pirate Hat",
            "Talk to Scotty, say 'map'. Fight sylphs at Prazen Point."),
        new QuestEntry(8, "The Iron Chest", "Tulliana Daverland", "Htron", "map",
            "Ebbwater", "Minotaurs", "Iron Chest",
            "Talk to Tulliana in Htron, say 'map'. Fight minotaurs in Ebbwater."),
        new QuestEntry(9, "The Golden Necklace", "Belinda", "Olanthen", "",
            "Mountain of Lorr", "Orcs", "Golden Necklace",
            "Talk to Belinda in Olanthen. Fight orcs in the Mountain of Lorr."),
        new QuestEntry(10, "The Wand", "Orofin", "Poitle Lock", "",
            "Southern river", "Skeletons", "Wand",
            "Talk to Orofin in Poitle Lock. Fight skeletons at the southern river."),
        new QuestEntry(11, "The Coat of Arms", "Sedfrey", "Poitle Lock", "gold",
            "Tegal Forest", "Thugs", "Coat of Arms",
            "Talk to Sedfrey, say 'gold'. Fight thugs in Tegal Forest."),
        new QuestEntry(12, "The Oil of Changeling", "Milinya", "Thimberwald", "",
            "Downing Swamp", "Muck Creatures", "Oil of Changeling",
            "Talk to Milinya in Thimberwald. Fight muck creatures in Downing Swamp."),
        new QuestEntry(13, "The Cloak", "Trimrose", "Thimberwald", "Delmor",
            "Karg Hill / Northwald", "", "Flying Cloak",
            "Talk to Trimrose, say 'Delmor'. The Flying Cloak allows flying in combat for any character."),
        new QuestEntry(14, "The Vial", "Keldinarr", "Thimblewald", "vial",
            "Windy Run", "", "Vial",
            "Talk to Keldinarr, say 'vial'. Search Windy Run."),
        new QuestEntry(15, "The Millet", "Ballaster", "Krag Keep", "scalfeth",
            "Wesswald", "Mist Giants", "Millet",
            "Talk to Ballaster at Krag Keep, say 'scalfeth'. Fight mist giants in Wesswald. Wear Courage Coat."),
        new QuestEntry(16, "The Golden Chalice", "Dunnigen", "Tegal Forest", "rhording",
            "The Darkwood", "Ogres", "Golden Chalice",
            "Talk to Dunnigen in Tegal Forest, say 'rhording'. Fight ogres in the Darkwood."),
        new QuestEntry(17, "The Hidden Staff", "Lord Stiveron", "Hobean Keep", "inthos",
            "Downing Mountains", "Stone Ogres", "Hidden Staff",
            "Talk to Lord Stiveron at Hobean Keep, say 'inthos'. Fight stone ogres in Downing Mountains."),
        new QuestEntry(18, "The Wristband", "Rodrigard", "Sheller Bridge", "bryor",
            "Sheller Ridge", "Ettins", "Wristband",
            "Talk to Rodrigard at Sheller Bridge, say 'bryor'. Fight ettins at Sheller Ridge."),
        new QuestEntry(19, "The Djinn Item", "Aurin", "Sheller Bridge", "grey",
            "Thanakesh Hills", "Djinn", "Djinn Item",
            "Talk to Aurin at Sheller Bridge, say 'grey'. Fight djinn in Thanakesh Hills."),
        new QuestEntry(20, "The Shade Ring", "Sheller Elite Guard", "Sheller Bridge", "",
            "Westwash", "Cliff Trolls", "Shade Ring",
            "Aurin sends you to the Elite Guard. Fight cliff trolls in Westwash. Concentrate fire; they regenerate."),
        new QuestEntry(21, "The Ward", "Lord Norgan", "Shellernoon", "silver knot",
            "Sodden Hills", "Sledge Creatures", "Ward",
            "Talk to Lord Norgan in Shellernoon, say 'silver knot'. Party must split! Fight sledge creatures."),
        new QuestEntry(22, "The Statuette", "Denswurth", "Olanthen", "",
            "Missip Valley", "Trolls", "Statuette",
            "Talk to Denswurth in Olanthen. Fight trolls in Missip Valley. They regenerate each round."),
        new QuestEntry(23, "Rescue Seggallion", "Dundle", "Assembly Building, Olanthen Barrier", "",
            "Ghor Hills", "Cyclops", "Win the game",
            "Final quest. Talk to Dundle at the Assembly Building in Olanthen Barrier. Fight cyclops in Ghor Hills. Wear Courage Coat."),
    };

    public static QuestEntry? ById(int id) =>
        id >= 0 && id < Quests.Count ? Quests[id] : null;
}
