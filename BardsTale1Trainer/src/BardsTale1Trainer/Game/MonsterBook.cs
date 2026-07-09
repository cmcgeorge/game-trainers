namespace BardsTale1Trainer.Game;

/// <summary>
/// One monster from the game's bestiary. <see cref="Raw"/> is the name exactly as stored
/// in the running game's data segment, including its inflection markup
/// (<c>base^singular-tail^plural-tail^</c>, e.g. <c>"Dwar^f^ves^"</c>); <see cref="Name"/>
/// and <see cref="Plural"/> are the two forms that markup expands to. <see cref="Id"/> is
/// the game's own 0-based monster index (the order of the name pointer table).
/// </summary>
public sealed record GameMonster(int Id, string Raw)
{
    public string Name { get; } = MonsterBook.DecodeName(Raw).Singular;
    public string Plural { get; } = MonsterBook.DecodeName(Raw).Plural;

    /// <summary>Difficulty tier, derived from the table position (see <see cref="MonsterBook.GroupOf"/>).</summary>
    public string Group => MonsterBook.GroupOf(Id);

    /// <summary>Detail line for the reference list.</summary>
    public string DetailText => Name == Plural ? $"#{Id}" : $"#{Id} · plural: {Plural}";

    /// <summary>Right-aligned accent tag: the game's internal monster id.</summary>
    public string IdTag => $"#{Id}";
}

/// <summary>
/// The game's complete 127-entry monster name table, extracted verbatim from the running
/// BARD.EXE's data segment (names at DS:0x2874, id order per the pointer table at
/// DS:0x2F3E — see <see cref="PartyFormat.DsMonsterNames"/>). BARD.EXE is packed on disk,
/// so the live segment is the only readable source.
///
/// The list is ordered in eight 16-id difficulty bands (the last band has 15): each band
/// carries the four enemy spell-caster classes of its tier (at the band's end from tier 2
/// on; tier 1's pair sits at ids 6-7), and the dungeon encounter tables draw from the
/// bands by depth. Per-monster combat stats exist as parallel byte
/// arrays in the segment (DS:0x19C3/0x1A43/0x1AC3/0x1B43) but their encoding has not been
/// decoded yet, so — rather than guess — only the names and table order are included here.
/// </summary>
public static class MonsterBook
{
    public const int Count = PartyFormat.MonsterCount;

    /// <summary>
    /// Expands the game's inflection markup: <c>base^s-tail^p-tail^</c> means
    /// singular = base + s-tail and plural = base + p-tail (<c>"Old M^an^en^"</c> →
    /// "Old Man" / "Old Men"). A name without markup (e.g. <c>"Samurai"</c>, <c>"Mangar"</c>)
    /// is its own plural.
    /// </summary>
    public static (string Singular, string Plural) DecodeName(string raw)
    {
        int a = raw.IndexOf('^');
        if (a < 0) return (raw, raw);
        int b = raw.IndexOf('^', a + 1);
        int c = b < 0 ? -1 : raw.IndexOf('^', b + 1);
        if (b < 0 || c < 0) return (raw, raw);   // malformed markup: show verbatim
        string stem = raw[..a];
        return (stem + raw[(a + 1)..b], stem + raw[(b + 1)..c]);
    }

    /// <summary>Difficulty band of a 0-based monster id, from its table position.</summary>
    public static string GroupOf(int id) => (id / 16) switch
    {
        0 => "Tier 1 — ids 0-15 (easiest)",
        1 => "Tier 2 — ids 16-31",
        2 => "Tier 3 — ids 32-47",
        3 => "Tier 4 — ids 48-63",
        4 => "Tier 5 — ids 64-79",
        5 => "Tier 6 — ids 80-95",
        6 => "Tier 7 — ids 96-111",
        _ => "Tier 8 — ids 112-126 (Mangar's own)",
    };

    private static GameMonster M(int id, string raw) => new(id, raw);

    public static readonly IReadOnlyList<GameMonster> Bestiary = new[]
    {
        M(  0, "Kobold^^s^"),
        M(  1, "Hobbit^^s^"),
        M(  2, "Gnome^^s^"),
        M(  3, "Dwar^f^ves^"),
        M(  4, "Thie^f^ves^"),
        M(  5, "Hobgoblin^^s^"),
        M(  6, "Conjurer^^s^"),
        M(  7, "Magician^^s^"),
        M(  8, "Orc^^s^"),
        M(  9, "Skeleton^^s^"),
        M( 10, "Nomad^^s^"),
        M( 11, "Spider^^s^"),
        M( 12, "Mad Dog^^s^"),
        M( 13, "Barbarian^^s^"),
        M( 14, "Mercenar^y^ies^"),
        M( 15, "Wol^f^ves^"),
        M( 16, "Jade Monk^^s^"),
        M( 17, "Half Orc^^s^"),
        M( 18, "Swordsm^an^en^"),
        M( 19, "Zombie^^s^"),
        M( 20, "Conjurer^^s^"),
        M( 21, "Magician^^s^"),
        M( 22, "Sorcerer^^s^"),
        M( 23, "Wizard^^s^"),
        M( 24, "Samurai"),
        M( 25, "Black Widow^^s^"),
        M( 26, "Assassin^^s^"),
        M( 27, "Werewol^f^ves^"),
        M( 28, "Ogre^^s^"),
        M( 29, "Wight^^s^"),
        M( 30, "Statue^^s^"),
        M( 31, "Bladesm^an^en^"),
        M( 32, "Goblin Lord^^s^"),
        M( 33, "Master Thie^f^ves^"),
        M( 34, "Conjurer^^s^"),
        M( 35, "Magician^^s^"),
        M( 36, "Sorcerer^^s^"),
        M( 37, "Wizard^^s^"),
        M( 38, "Ninja^^s^"),
        M( 39, "Spinner^^s^"),
        M( 40, "Scarlet Monk^^s^"),
        M( 41, "Doppleganger^^s^"),
        M( 42, "Stone Giant^^s^"),
        M( 43, "Ogre Magician^^s^"),
        M( 44, "Jackalwere^^s^"),
        M( 45, "Stone Elemental^^s^"),
        M( 46, "Blue Dragon^^s^"),
        M( 47, "Seeker^^s^"),
        M( 48, "Dwarf King^^s^"),
        M( 49, "Samurai Lord^^s^"),
        M( 50, "Ghoul^^s^"),
        M( 51, "Conjurer^^s^"),
        M( 52, "Magician^^s^"),
        M( 53, "Sorcerer^^s^"),
        M( 54, "Wizard^^s^"),
        M( 55, "Azure Monk^^s^"),
        M( 56, "Weretiger^^s^"),
        M( 57, "Hydra^^s^"),
        M( 58, "Green Dragon^^s^"),
        M( 59, "Wraith^^s^"),
        M( 60, "Lurker^^s^"),
        M( 61, "Fire Giant^^s^"),
        M( 62, "Copper Dragon^^s^"),
        M( 63, "Ivory Monk^^s^"),
        M( 64, "Shadow^^s^"),
        M( 65, "Berserker^^s^"),
        M( 66, "Conjurer^^s^"),
        M( 67, "Magician^^s^"),
        M( 68, "Sorcerer^^s^"),
        M( 69, "Wizard^^s^"),
        M( 70, "White Dragon^^s^"),
        M( 71, "Ice Giant^^s^"),
        M( 72, "Eye Sp^y^ies^"),
        M( 73, "Ogre Lord^^s^"),
        M( 74, "Body Snatcher^^s^"),
        M( 75, "Xorn^^s^"),
        M( 76, "Phantom^^s^"),
        M( 77, "Lesser Demon^^s^"),
        M( 78, "Fred^^s^"),
        M( 79, "Conjurer^^s^"),
        M( 80, "Magician^^s^"),
        M( 81, "Sorcerer^^s^"),
        M( 82, "Wizard^^s^"),
        M( 83, "Master Ninja^^s^"),
        M( 84, "War Giant^^s^"),
        M( 85, "Warrior Elite^^s^"),
        M( 86, "Bone Crusher^^s^"),
        M( 87, "Ghost^^s^"),
        M( 88, "Grey Dragon^^s^"),
        M( 89, "Basilisk^^s^"),
        M( 90, "Evil Eye^^s^"),
        M( 91, "Mimic^^s^"),
        M( 92, "Golem^^s^"),
        M( 93, "Vampire^^s^"),
        M( 94, "Demon^^s^"),
        M( 95, "Bandersnatch^^es^"),
        M( 96, "Maze Dweller^^s^"),
        M( 97, "Mongo^^s^"),
        M( 98, "Mangar Guard^^s^"),
        M( 99, "Gimp^^s^"),
        M(100, "Red Dragon^^s^"),
        M(101, "Titan^^s^"),
        M(102, "Master Conjurer^^s^"),
        M(103, "Master Magician^^s^"),
        M(104, "Master Sorcerer^^s^"),
        M(105, "Mind Shadow^^s^"),
        M(106, "Spectre^^s^"),
        M(107, "Cloud Giant^^s^"),
        M(108, "Beholder^^s^"),
        M(109, "Vampire Lord^^s^"),
        M(110, "Greater Demon^^s^"),
        M(111, "Master Wizard^^s^"),
        M(112, "Mad God^^s^"),
        M(113, "Maze Master^^s^"),
        M(114, "Death Denizen^^s^"),
        M(115, "Jabberwock^^s^"),
        M(116, "Black Dragon^^s^"),
        M(117, "Mangar"),
        M(118, "Crystal Golem^^s^"),
        M(119, "Soul Sucker^^s^"),
        M(120, "Storm Giant^^s^"),
        M(121, "Ancient Enem^y^ies^"),
        M(122, "Balrog^^s^"),
        M(123, "Lich^^es^"),
        M(124, "Archmage^^s^"),
        M(125, "Demon Lord^^s^"),
        M(126, "Old M^an^en^"),
    };
}
