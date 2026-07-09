namespace PoolOfRadianceTrainer.Game;

/// <summary>One bestiary entry. Blank/zero fields are unknown rather than guessed.</summary>
public sealed record MonsterInfo(
    string Name, int Xp, string Hp, string Ac, string Thac0, string Damage, string Notes);

/// <summary>
/// A reference bestiary for Pool of Radiance. XP-per-kill values are the game's own,
/// data-mined by the Gold Box Companion project (gbc.zorbus.net/mm/01_por.html); the
/// stat blocks are from that source and Stephen S. Lee's "Exhaustive Game Information"
/// FAQ. This is reference data only — the trainer edits monster records live via the
/// combat panel, it does not depend on this table.
/// </summary>
public static class MonsterBook
{
    public static readonly IReadOnlyList<MonsterInfo> All = new List<MonsterInfo>
    {
        // --- humanoids / early game ---
        new("Kobold",          8,  "1d4", "7", "20", "1d4",      "Trivial; fought in hordes. THAC0/dmg -2 in kobold-sized tunnels."),
        new("Kobold Leader",   16, "1d4+", "6", "19", "1d6",     "Commands kobold packs."),
        new("Goblin",          14, "1d6", "6", "20", "1d6",      "Slums/city filler."),
        new("Goblin Leader",   17, "1d8", "6", "19", "1d8",      ""),
        new("Orc",             15, "1d8", "6", "19", "1d8",      "The Slums 'orcs, 5 HP each' encounter."),
        new("Orc Leader",      44, "2d8", "5", "18", "1d8",      ""),
        new("Hobgoblin",       32, "1d8", "5", "18", "1d8",      "Cadorna Textile House garrison."),
        new("Gnoll",           0,  "2d8", "5", "18", "2d4",      "Eastern wilderness."),
        new("Bugbear",         199,"3d8", "5", "16", "2d4",      "Leads large Slums groups; Stojanow Gate front patrol."),

        // --- undead (Sokal Keep, Valhingen Graveyard) ---
        new("Skeleton",        19, "5",   "7", "19", "1d6",      "Half dmg from edged; immune sleep/charm/cold/poison; hurt by holy water."),
        new("Giant Skeleton",  270,"28",  "2", "15", "4d6",      "Hits hard, good AC."),
        new("Zombie",          0,  "2d8", "8", "18", "1d8",      "Always acts last; swarms."),
        new("Ghoul",           85, "10",  "6", "16", "1d3/1d3/1d6","Paralysis on hit (elves immune)."),
        new("Ghast",           0,  "4d8", "4", "15", "1d4/1d4/1d8","Paralysis affects elves too; stench."),
        new("Juju Zombie",     206,"24",  "6", "13", "3d4",      "Immune to non-magical weapons; half dmg blunt/fire."),
        new("Wight",           0,  "4d8", "5", "16", "1d4",      "Drains 1 level per hit; needs magic weapons."),
        new("Wraith",          0,  "5d8", "4", "13", "1d6",      "Drains 1 level; immune to non-magical weapons."),
        new("Mummy",           1414,"33", "3", "13", "1d12",     "Disease + fear; immune non-magical weapons; vulnerable to fire & holy water."),
        new("Spectre",         2030,"38", "2", "12", "1d8",      "Drains 2 levels per hit — the deadliest common foe."),
        new("Vampire",         18800,"8d8","1", "11", "1d10",    "Drains 2 levels; charm gaze; regenerates at its coffin (destroy it first)."),

        // --- beasts / wilderness ---
        new("Giant Lizard",    124,"3d8", "5", "15", "1d8",      "Multiple attacks/round; dangerous random encounter."),
        new("Lizardman",       98, "2d8", "5", "16", "1d8",      "Kuto's Well, Lizardman Keep."),
        new("Ogre",            195,"4d8", "5", "13", "1d10",     "Cadorna ogre carries Gauntlets of Ogre Power."),
        new("Troll",           0,  "6d8", "4", "13", "1d4/1d4/2d6","Regenerates; stand on the corpse to stop revival."),
        new("Wild Boar",       0,  "3d8", "7", "16", "3d4",      "Revives immediately; easy to re-drop."),
        new("Wyvern",          224,"7d8", "3", "13", "2d8/1d6",  "Deadly poison sting."),
        new("Giant Snake",     0,  "4d8", "5", "15", "1d3",      "Deadly poison bite."),
        new("Phase Spider",    0,  "5d8", "7", "13", "1d6",      "Blinks between phases; poison."),
        new("Basilisk",        1248,"31", "4", "13", "1d10",     "Petrifying gaze — equip mirrors! Mendor's Library."),
        new("Displacer Beast", 0,  "6d8", "4", "13", "2d4/2d4",  "Appears displaced (-2 to hit it)."),
        new("Minotaur",        0,  "6d8", "6", "13", "2d4",      ""),

        // --- bosses / special ---
        new("Fire Giant",      3644,"59", "3", "9",  "5d6",      "Valjevo Castle guards; huge damage."),
        new("Yarash",          959, "—",  "—", "—",  "spells",   "Sorcerer's Isle; Wand of Paralyzation. Carries Bracers AC 4."),
        new("Medusa",          0,  "6d8", "5", "13", "1d4",      "Petrifying gaze — equip mirrors! Inner Tower."),
        new("Tyranthraxus (Bronze Dragon)", 611, "—", "—", "—", "lightning breath ~80",
            "FINAL BOSS. Immune to all magic; fiery aura. Beat with melee + Dust of Disappearance; Javelin of Lightning helps."),
    };

    /// <summary>Filter the bestiary by a case-insensitive substring of the name/notes.</summary>
    public static IEnumerable<MonsterInfo> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return All;
        term = term.Trim();
        return All.Where(m =>
            m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            m.Notes.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
