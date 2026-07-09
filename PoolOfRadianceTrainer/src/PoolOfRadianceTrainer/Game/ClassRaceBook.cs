namespace PoolOfRadianceTrainer.Game;

public sealed record ClassInfo(string Name, string HitDie, string PrimeStat, int GameCap, string Notes);
public sealed record RaceInfo(string Name, string ClassOptions, string Notes);
public sealed record XpRow(int Level, int Cleric, int Fighter, int Mage, int Thief);

/// <summary>
/// Reference tables for AD&amp;D-as-implemented in Pool of Radiance: classes, races,
/// the training-hall level caps, and the XP-to-reach-level tables. From the game Rule Book
/// and Stephen S. Lee's FAQ (cross-checked with c64-wiki). Reference only.
/// </summary>
public static class ClassRaceBook
{
    public static readonly IReadOnlyList<ClassInfo> Classes = new List<ClassInfo>
    {
        new("Fighter", "d10", "Strength", 8,
            "Any weapon/armor. Multiple attacks + 'sweep' vs weak foes. Only class with exceptional 18/xx Strength."),
        new("Cleric",  "d8",  "Wisdom",   6,
            "Crushing weapons + any armor; heals & turns undead. Only Humans & Half-Elves can be clerics."),
        new("Magic-User", "d4", "Intelligence", 6,
            "Dagger/staff only, no armor. Starts with Sleep — the game-defining spell. Reaches Fireball at level 5."),
        new("Thief",   "d6",  "Dexterity", 9,
            "Leather only, no shield. Backstab x2 (L1-4), x3 (L5-8), x4 (L9). No racial level limit."),
    };

    public static readonly IReadOnlyList<RaceInfo> Races = new List<RaceInfo>
    {
        new("Human", "Fighter, Cleric, Mage, Thief (single-class only)",
            "No level caps; the only race that reaches Cleric 6. Best for the full 4-game saga import."),
        new("Elf", "Fighter(7), Mage(11), Thief; F/M, F/T, M/T, F/M/T",
            "+1 DEX, infravision, 90% sleep/charm resist, finds secret doors. Cannot be a cleric."),
        new("Half-Elf", "Fighter(8), Mage(8), Cleric(5), Thief; widest multiclass menu incl. C/F/M",
            "30% sleep/charm resist. The go-to multiclass race (Cleric/Fighter/Mage)."),
        new("Dwarf", "Fighter(9), Thief; F/T only",
            "+1 CON, infravision, magic resistance, combat bonus vs goblins/orcs. No magic."),
        new("Gnome", "Fighter(6), Thief; F/T only", "Magic resistance; combat bonus vs kobolds/goblins."),
        new("Halfling", "Fighter(6), Thief; F/T only", "+1 DEX, magic resistance. No level limit as a thief."),
        new("Half-Orc", "(not selectable in PoR creation; exists in the engine)", "Race value 6 in the record format."),
    };

    /// <summary>In-game training-hall caps: Fighter 8, Thief 9, Cleric 6, Magic-User 6.</summary>
    public static readonly IReadOnlyList<XpRow> XpTable = new List<XpRow>
    {
        new(1, 0,      0,      0,      0),
        new(2, 1_500,  2_000,  2_500,  1_250),
        new(3, 3_000,  4_000,  5_000,  2_500),
        new(4, 6_000,  8_000,  10_000, 5_000),
        new(5, 13_000, 18_000, 22_500, 10_000),
        new(6, 27_500, 35_000, 40_000, 20_000),   // Cleric & Mage cap at 6
        new(7, 0,      70_000, 0,      42_500),
        new(8, 0,      125_000,0,      70_000),    // Fighter caps at 8
        new(9, 0,      0,      0,      110_000),   // Thief caps at 9
    };

    /// <summary>Exceptional-strength (fighters only) to-hit/damage bonuses, from AD&amp;D 1e.</summary>
    public static readonly IReadOnlyList<(string Range, string ToHit, string Damage)> ExceptionalStrength = new List<(string, string, string)>
    {
        ("18/01-50", "+1", "+3"),
        ("18/51-75", "+2", "+3"),
        ("18/76-90", "+2", "+4"),
        ("18/91-99", "+2", "+5"),
        ("18/00",    "+3", "+6"),
    };
}
