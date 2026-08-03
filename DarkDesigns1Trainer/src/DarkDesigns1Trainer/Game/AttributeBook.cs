namespace DarkDesigns1Trainer.Game;

/// <summary>
/// What each of the five attributes actually does, so the roller can explain a target rather than
/// just name it. Index-aligned with <see cref="CharacterFormat.AttributeNames"/> (STR, DEX, CON,
/// INT, PIE); the text matches the attribute table in <c>docs/StrategyGuide.md</c>.
/// </summary>
public static class AttributeBook
{
    /// <summary>One-line description of each attribute, in record order.</summary>
    public static readonly string[] Descriptions =
    {
        "Strength — damage dealt by melee attacks. The attribute Fighters (and, to a lesser extent, "
        + "Priests) live on.",

        "Dexterity — combat initiative (who strikes first) and the ability to dodge. Useful to every "
        + "class.",

        "Constitution — Body (hit points) per level, and resistance to some spells. The difference "
        + "between surviving a Dungeon Level 2 ambush and not.",

        "Intelligence — a Wizard's magic points, and resistance to some spells. Dead weight on a "
        + "pure Fighter.",

        "Piety — a Priest's magic points, and how much their healing spells restore.",
    };

    /// <summary>Description for attribute <paramref name="index"/>, or an empty string if out of range.</summary>
    public static string DescriptionOf(int index) =>
        index >= 0 && index < Descriptions.Length ? Descriptions[index] : "";
}
