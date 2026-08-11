namespace LegendOfFaerghailTrainer.Game;

/// <summary>
/// The six playable races, in the game's own index order. Read verbatim from the twelve-byte
/// padded table at <c>LOF.EXE</c> file offset 0x41E4A (DGROUP:0x617A). Index 5 was confirmed on
/// screen: the shipped roster's Connar has race 5 and the sheet calls him a Half-Orc.
/// </summary>
public static class RaceBook
{
    public static readonly string[] Names =
    {
        "Human", "Half-Elf", "Elf", "Halfling", "Dwarf", "Half-Orc"
    };

    public static int Count => Names.Length;

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"Race {index}";
}

/// <summary>
/// The twelve trades plus the "??" slot the game uses for non-player characters, from the padded
/// table at file offset 0x41E98 (DGROUP:0x61C8). The manual calls four of them by different names
/// than the program does — Thief/Blacksmith/Ranger/Cleric on paper, Rogue/Smith/Scout/Priest on
/// screen — so both are carried here. Verified against the tavern's Recruit list for all eleven
/// shipped roster entries.
/// </summary>
public static class ClassBook
{
    public static readonly string[] Names =
    {
        "Warrior", "Barbarian", "Rogue", "Smith", "Scout", "Priest",
        "Druid", "Magician", "Illusionist", "Paladin", "Healer", "Monk", "??"
    };

    /// <summary>The manual's name for each trade, where it differs from the program's.</summary>
    public static readonly string[] ManualNames =
    {
        "Warrior", "Barbarian", "Thief", "Blacksmith", "Ranger", "Cleric",
        "Druid", "Magician", "Illusionist", "Paladin", "Healer", "Monk", "(non-player)"
    };

    public static int Count => Names.Length;

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"Class {index}";

    public static string DescriptionOf(int index) =>
        index >= 0 && index < ManualNames.Length && ManualNames[index] != Names[index]
            ? $"the manual calls this trade \"{ManualNames[index]}\""
            : "";
}

/// <summary>
/// Health states, from the six-byte padded table at file offset 0x42190 (DGROUP:0x64C0). Index 7
/// (Dead) was confirmed live on a character at 0 hit points.
/// </summary>
public static class StatusBook
{
    public static readonly string[] Names =
    {
        "Good", "Mad", "Ill", "Poisoned", "Blind", "Stunned", "Stoned", "Dead"
    };

    public static int Count => Names.Length;

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"State {index}";
}

/// <summary>
/// The eight languages in record order, from the table at file offset 0x41DBE (DGROUP:0x60EE).
/// Order confirmed live: writing 1 across the eight bytes at +0x7A printed all eight lines, and in
/// the shipped records only the Half-Orc has +0x7C set and only the Dwarf has +0x7E.
/// </summary>
public static class LanguageBook
{
    public static readonly string[] Names =
    {
        "Common tongue", "Animal tongue", "Orc tongue", "Lizard tongue",
        "Dwarven tongue", "Elven tongue", "Dark tongue", "Magic tongue"
    };

    public static int Count => Names.Length;

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"Language {index}";
}

/// <summary>
/// The nine trained abilities in the order the character sheet prints them, from the table at file
/// offset 0x41D52 (DGROUP:0x6082). Their record offsets are in
/// <see cref="CharacterFormat.AbilityOffsets"/>.
/// </summary>
public static class AbilityBook
{
    public static readonly string[] Names =
    {
        "Negotiating", "Attack", "Defence", "Concentration", "Pick-pocketing",
        "Stalking", "Trap detecting", "Trap disarming", "Lock picking"
    };

    /// <summary>What the manual says each ability is for.</summary>
    public static readonly string[] Descriptions =
    {
        "buy cheap, sell dear, and parley with anything that will talk",
        "chance of landing a heavy blow",
        "parries, feints, and dodging spells",
        "spell effect, and the rate at which new spells and languages are learned",
        "lifting purses in taverns; getting caught has consequences",
        "creeping up for an attack from behind",
        "spotting a trap instead of walking into it",
        "removing a spotted trap without setting it off",
        "opening a locked door without breaking it down"
    };

    public static int Count => Names.Length;

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"Ability {index}";

    public static string DescriptionOf(int index) =>
        index >= 0 && index < Descriptions.Length ? Descriptions[index] : "";
}

/// <summary>
/// The eight regions the status panel names, from the 22-byte padded table at file offset 0x42240
/// (DGROUP:0x6570). Reference only — the trainer does not edit map position.
/// </summary>
public static class LocationBook
{
    public static readonly string[] Names =
    {
        "Valley of Faerghail", "Monastery of Sagacita", "Sagacita catacombs", "The Mines",
        "The Pyramid", "The Temple", "The Castle", "The Mountain"
    };
}
