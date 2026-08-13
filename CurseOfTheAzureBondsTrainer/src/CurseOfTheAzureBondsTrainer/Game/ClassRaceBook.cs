namespace CurseOfTheAzureBondsTrainer.Game;

public sealed record ClassInfo(string Name, string HitDie, string PrimeStat, int GameCap, string Notes);
public sealed record RaceInfo(string Name, string ClassOptions, string Notes);

/// <summary>One row of the experience-to-level tables, in the game's own class order.
/// 0 means the class cannot reach that level in Curse of the Azure Bonds.</summary>
public sealed record XpRow(int Level, int Cleric, int Fighter, int Paladin, int Ranger, int Mage, int Thief);

/// <summary>Spells memorizable per day at a given level, by spell level 1–5. From the Rule Book's
/// class tables; clerics add the Wisdom bonus spells on top (see <see cref="ClericWisdomBonus"/>).</summary>
public sealed record SpellSlotRow(int Level, string Cleric, string Mage);

/// <summary>
/// Reference tables for AD&amp;D as Curse of the Azure Bonds implements it: classes, races, the
/// racial level limits, and the experience-to-level tables. Every number here is transcribed from
/// the game's own Rule Book (<c>curseazure.pdf</c>, "APPENDICES"), which ships with this install —
/// so these are the game's rules, not the tabletop ones it is adapted from. Reference only.
///
/// <para>Two of these tables are also what the character record is checked against: a cleric's
/// spells-per-day and a character's experience share both fall out of them exactly for the sample
/// party (<c>test/FormatCheck</c>), which is part of why the offsets are trusted.</para>
/// </summary>
public static class ClassRaceBook
{
    public static readonly IReadOnlyList<ClassInfo> Classes = new List<ClassInfo>
    {
        new("Fighter", "d10", "Strength", 12,
            "Any armor, any shield, any weapon. The only class that rolls exceptional 18/xx Strength, " +
            "and the one that reaches the highest level in the game."),
        new("Cleric", "d8", "Wisdom", 10,
            "Any armor and shield; club, flail, hammer, mace, staff and staff sling only. Heals, turns " +
            "undead, and reaches 5th-level spells — Raise Dead and Flame Strike. Humans and half-elves only."),
        new("Paladin", "d10", "Strength", 11,
            "A fighter who must be Lawful Good, saves 2 better than anyone, radiates protection from " +
            "evil permanently (the record carries it as effect 0x08), and gains clerical spells at 9th. Human only."),
        new("Ranger", "d8 (2d8 at 1st)", "Strength", 11,
            "A fighter with extra hit dice who picks up first-level druid and magic-user spells at " +
            "8th and 9th. Human or half-elf."),
        new("Magic-User", "d4", "Intelligence", 11,
            "No armor, no shield; dagger, dart and staff only. Sleep decides the early fights and " +
            "Fireball, Haste and Lightning Bolt decide the rest. Reaches 5th-level spells."),
        new("Thief", "d6", "Dexterity", 12,
            "Leather only, no shield; club, dagger, dart, sling, short bow and one-handed swords. The " +
            "one class with no racial level limit — every race reaches 12."),
    };

    public static readonly IReadOnlyList<RaceInfo> Races = new List<RaceInfo>
    {
        new("Human", "Cleric 10, Fighter 12, Paladin 11, Ranger 11, Magic-User 11, Thief 12",
            "No racial limits — the only race that can be a paladin, and the only one that reaches the " +
            "class caps. Strength 3–18(00) male, 3–18(50) female."),
        new("Elf", "Fighter 5–7, Magic-User 9–11, Thief 12; multi-class",
            "Dexterity to 19 and 90% resistance to sleep and charm (effect 0x6B in the record). Cannot " +
            "be a cleric, and cannot be raised from the dead."),
        new("Half-Elf", "Cleric 5, Fighter 6–8, Ranger 6–8, Magic-User 6–8, Thief 12; widest multi-class menu",
            "The only non-human cleric, and the only race that can combine cleric, fighter and mage."),
        new("Dwarf", "Fighter 7–9, Thief 12; Fighter/Thief",
            "Constitution to 19, a THAC0 bonus, a bonus against giants and a saving-throw bonus " +
            "(effects 0x1A, 0x2F and 0x61 — every dwarf in a real save carries all three). No magic."),
        new("Gnome", "Fighter 5–6, Thief 12; Fighter/Thief", "Sturdy and magic-resistant; no spellcasting."),
        new("Halfling", "Fighter 4–5, Thief 12; Fighter/Thief",
            "Dexterity to 18 and magic resistance, but the lowest fighter cap in the game. Strength tops out at 17."),
        new("Half-Orc", "(not offered at character creation)",
            "Race value 6 in the record format — the engine keeps the slot, but Curse's own race table " +
            "does not list it."),
    };

    /// <summary>
    /// Experience needed to reach each level, per class, from the Rule Book's "TABLE OF EXPERIENCE
    /// PER LEVEL". A non-human multi-class character divides everything it earns by the number of
    /// its classes — which is why a freshly created multi-class character shows 12,500 against a
    /// single-class character's 25,000.
    /// </summary>
    public static readonly IReadOnlyList<XpRow> XpTable = new List<XpRow>
    {
        //       lvl  cleric    fighter    paladin    ranger     mage       thief
        new( 1,        0,         0,         0,         0,         0,         0),
        new( 2,    1_501,     2_001,     2_751,     2_251,     2_501,     1_251),
        new( 3,    3_001,     4_001,     5_501,     4_501,     5_001,     2_501),
        new( 4,    6_001,     8_001,    12_001,    10_001,    10_001,     5_001),
        new( 5,   13_001,    18_001,    24_001,    20_001,    22_501,    10_001),
        new( 6,   27_501,    35_001,    45_001,    40_001,    40_001,    20_001),
        new( 7,   55_001,    70_001,    95_001,    90_001,    60_001,    42_501),
        new( 8,  110_001,   125_001,   175_001,   150_001,    90_001,    70_001),
        new( 9,  225_001,   250_001,   350_001,   225_001,   135_001,   110_001),
        new(10,  450_001,   500_001,   700_001,   325_001,   250_001,   160_001),
        new(11,        0,   750_001, 1_050_001,   650_001,   375_001,   220_001),
        new(12,        0, 1_000_001,         0,         0,         0,   440_001),
    };

    /// <summary>Spells memorizable per day by class level, before the cleric's Wisdom bonus.</summary>
    public static readonly IReadOnlyList<SpellSlotRow> SpellSlots = new List<SpellSlotRow>
    {
        new( 1, "1 / – / – / – / –", "1 / – / – / – / –"),
        new( 2, "2 / – / – / – / –", "2 / – / – / – / –"),
        new( 3, "2 / 1 / – / – / –", "2 / 1 / – / – / –"),
        new( 4, "3 / 2 / – / – / –", "3 / 2 / – / – / –"),
        new( 5, "3 / 3 / 1 / – / –", "4 / 2 / 1 / – / –"),
        new( 6, "3 / 3 / 2 / – / –", "4 / 2 / 2 / – / –"),
        new( 7, "3 / 3 / 2 / 1 / –", "4 / 3 / 2 / 1 / –"),
        new( 8, "3 / 3 / 3 / 2 / –", "4 / 3 / 3 / 2 / –"),
        new( 9, "4 / 4 / 3 / 2 / 1", "4 / 4 / 3 / 2 / 1"),
        new(10, "4 / 4 / 3 / 3 / 2", "4 / 4 / 4 / 2 / 2"),
        new(11, "–",                 "4 / 4 / 4 / 3 / 3"),
    };

    /// <summary>Bonus clerical spells for a high Wisdom, by spell level 1–5. Only granted once the
    /// cleric is entitled to spells of that level at all.</summary>
    public static readonly IReadOnlyList<(string Wisdom, string Bonus)> ClericWisdomBonus =
        new List<(string, string)>
    {
        ("9–12", "– / – / – / – / –"),
        ("13",   "+1 / – / – / – / –"),
        ("14",   "+2 / – / – / – / –"),
        ("15",   "+2 / +1 / – / – / –"),
        ("16",   "+2 / +2 / – / – / –"),
        ("17",   "+2 / +2 / +1 / – / –"),
        ("18",   "+2 / +2 / +1 / +1 / –"),
    };

    /// <summary>Exceptional-strength (fighters, paladins and rangers only) to-hit and damage
    /// bonuses. The percentile lives at <see cref="CoabFormat.OffStrPercent"/>.</summary>
    public static readonly IReadOnlyList<(string Range, string ToHit, string Damage)> ExceptionalStrength =
        new List<(string, string, string)>
    {
        ("18/01-50", "+1", "+3"),
        ("18/51-75", "+2", "+3"),
        ("18/76-90", "+2", "+4"),
        ("18/91-99", "+2", "+5"),
        ("18/00",    "+3", "+6"),
    };

    /// <summary>Armor as the Rule Book lists it: cost in gold, the AC it gives, and the movement it
    /// allows. A shield subtracts a further 1 from AC.</summary>
    public static readonly IReadOnlyList<(string Name, string Cost, string ArmorClass, string Movement)> Armor =
        new List<(string, string, string, string)>
    {
        ("None",            "—",    "10", "12 squares"),
        ("Shield, small",   "50",   "-1", "—"),
        ("Leather",         "50",   "8",  "12 squares"),
        ("Padded",          "100",  "8",  "9 squares"),
        ("Studded",         "200",  "7",  "9 squares"),
        ("Ring",            "250",  "7",  "9 squares"),
        ("Chain",           "300",  "5",  "9 squares"),
        ("Banded",          "350",  "4",  "9 squares"),
        ("Scale",           "400",  "6",  "6 squares"),
        ("Splint",          "400",  "4",  "6 squares"),
        ("Plate",           "450",  "3",  "6 squares"),
    };
}
