namespace WastelandTrainer.Game;

/// <summary>
/// One of the seven character attributes: its record index (0..6, matching
/// <see cref="CharacterFormat.AttributeNames"/> and <see cref="CharacterRecord.GetAttribute"/>), the
/// abbreviation shown in the UI, the full name, what the game uses it for (<see cref="Role"/>), and a
/// practical build note (<see cref="InPlay"/>).
/// </summary>
public sealed record AttributeInfo(int Index, string Abbr, string Name, string Role, string InPlay)
{
    /// <summary>A one-paragraph blurb (full name + role + practical note) for a tooltip.</summary>
    public string Description => $"{Name} ({Abbr}) — {Role} {InPlay}";
}

/// <summary>
/// Reference descriptions of Wasteland's seven attributes: what each one does in the game and how it
/// matters when building a character. The <see cref="AttributeInfo.Role"/> text is grounded in the
/// game manual's "Attributes and Personal Statistics" section (<c>.game\manual.txt</c>); the
/// <see cref="AttributeInfo.InPlay"/> notes follow the bundled strategy guide's attribute table
/// (<c>.docs\Wasteland-Strategy-Guide.md</c>). Reference only — nothing here is written to the game.
///
/// The list is ordered to match <see cref="CharacterFormat.AttributeNames"/> exactly (STR, IQ, LCK,
/// SPD, AGL, DEX, CHR), so <see cref="ByIndex"/> lines up with <see cref="CharacterRecord.GetAttribute"/>.
/// </summary>
public static class AttributeBook
{
    public static readonly IReadOnlyList<AttributeInfo> Attributes = new AttributeInfo[]
    {
        new(0, "STR", "Strength",
            "raw power — overpowering enemies and lifting, moving, or breaking things; it weighs on "
            + "hand-to-hand combat and physical tasks such as breaking down doors.",
            "Best on a brawler or the party's physical problem-solver."),
        new(1, "IQ", "Intelligence",
            "how well the character learns: it sets which skills can be mastered — every skill has a "
            + "minimum IQ — and a fresh character starts with skill points equal to IQ.",
            "The most important creation stat: a high IQ both unlocks the advanced skills and pays for "
            + "them, and it's well worth raising through the game."),
        new(2, "LCK", "Luck",
            "a catch-all edge: lucky characters find more things, avoid more damage, and get better "
            + "odds in hand-to-hand combat and on the game's many random rolls.",
            "General insurance — useful on anyone, decisive for no one."),
        new(3, "SPD", "Speed",
            "how quickly the character moves, which helps you escape tight situations.",
            "Helps mobile, scouting characters slip away from fights they'd rather not take."),
        new(4, "AGL", "Agility",
            "how deftly the character moves — acrobatics, dodging blows, and jumping; higher agility "
            + "also improves hand-to-hand combat.",
            "Strong for front-line melee fighters."),
        new(5, "DEX", "Dexterity",
            "fine motor control — aiming weapons and picking locks. The manual calls it very important "
            + "in combat and central to the 'thiefly' skills.",
            "The key accuracy stat for shooters and for technical (lockpick / disarm) characters."),
        new(6, "CHR", "Charisma",
            "likeability and persuasion: the game uses it for how NPCs react to you when you try to "
            + "recruit or trade with them, and for convincing someone you're trustworthy.",
            "Its effect is almost entirely social — it barely touches combat — so it's usually the "
            + "party spokesperson's stat and the first one other characters can spare."),
    };

    /// <summary>The attribute at record index <paramref name="index"/> (0..6), or null if out of range.</summary>
    public static AttributeInfo? ByIndex(int index) =>
        index >= 0 && index < Attributes.Count ? Attributes[index] : null;

    /// <summary>The tooltip blurb for the attribute at <paramref name="index"/>, or "" if out of range.</summary>
    public static string DescriptionOf(int index) => ByIndex(index)?.Description ?? "";
}
