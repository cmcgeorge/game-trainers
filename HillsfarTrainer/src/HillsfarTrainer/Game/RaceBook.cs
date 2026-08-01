namespace HillsfarTrainer.Game;

/// <summary>
/// The six player races, in the game's own internal order — read out of the name table at
/// <c>DGROUP:0x8AD9</c> and confirmed live (writing 1 into the race byte turned a human into an elf
/// on the character sheet).
/// </summary>
public static class RaceBook
{
    /// <summary>Race names indexed by the byte at <see cref="CharacterFormat.OffRace"/>.</summary>
    public static readonly IReadOnlyList<string> Races = new[]
    {
        "Dwarf", "Elf", "Gnome", "Half-elf", "Halfling", "Human",
    };

    /// <summary>Gender names indexed by the byte at <see cref="CharacterFormat.OffGender"/>.</summary>
    public static readonly IReadOnlyList<string> Genders = new[] { "Male", "Female" };

    /// <summary>Name for a race index, or <c>"(unknown)"</c> when it is out of range.</summary>
    public static string NameForRace(int index) =>
        index >= 0 && index < Races.Count ? Races[index] : "(unknown)";

    /// <summary>Name for a gender index, or <c>"(unknown)"</c> when it is out of range.</summary>
    public static string NameForGender(int index) =>
        index >= 0 && index < Genders.Count ? Genders[index] : "(unknown)";
}

/// <summary>
/// The nine alignments. The game composes the name from two three-entry tables — Lawful/Neutral/
/// Chaotic at <c>DGROUP:0x8B1D</c> and Good/True/Evil at <c>DGROUP:0x8B2E</c> — as
/// <c>law * 3 + moral</c>, and prints index 4 as <b>"True Neutral"</b> rather than "Neutral True".
/// Verified on screen at indices 0, 3, 4 and 8.
/// </summary>
public static class AlignmentBook
{
    /// <summary>Alignment names indexed by the byte at <see cref="CharacterFormat.OffAlignment"/>.</summary>
    public static readonly IReadOnlyList<string> Alignments = new[]
    {
        "Lawful Good", "Lawful Neutral", "Lawful Evil",
        "Neutral Good", "True Neutral", "Neutral Evil",
        "Chaotic Good", "Chaotic Neutral", "Chaotic Evil",
    };

    /// <summary>Name for an alignment index, or <c>"(unknown)"</c> when it is out of range.</summary>
    public static string NameFor(int index) =>
        index >= 0 && index < Alignments.Count ? Alignments[index] : "(unknown)";
}
