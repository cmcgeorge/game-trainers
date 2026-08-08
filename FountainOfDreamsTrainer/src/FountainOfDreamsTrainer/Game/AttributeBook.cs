namespace FountainOfDreamsTrainer.Game;

/// <summary>One attribute's name, abbreviation, and in-game description.</summary>
public sealed record AttributeInfo(int Index, string Name, string Abbr, string Description);

/// <summary>
/// The seven Fountain of Dreams attributes, in record order (ST, IQ, DX, WP, AP, CH, LK).
/// The order was confirmed from FOD.EXE's character-creation display strings
/// ("ST:|IQ:|DX:|WP:|AP:|CH:|LK:") and cross-checked against the ARCHTYPE file's starting
/// attribute values for each profession. Descriptions paraphrase the game manual.
/// </summary>
public static class AttributeBook
{
    public static readonly IReadOnlyList<AttributeInfo> Attributes = new AttributeInfo[]
    {
        new(0, "Strength",     "ST", "Raw physical power; determines melee damage and carry capacity."),
        new(1, "Intelligence", "IQ", "Mental acuity; gates which skills a character can learn and how quickly they advance."),
        new(2, "Dexterity",    "DX", "Hand-eye coordination; improves aimed shots and quick actions."),
        new(3, "Willpower",    "WP", "Mental resolve; resists mental mutations and affects initiative."),
        new(4, "Appeal",       "AP", "Physical attractiveness; influences NPC interactions and trade."),
        new(5, "Charisma",     "CH", "Force of personality; affects recruitment and party morale."),
        new(6, "Luck",         "LK", "Fortune's favour; influences random outcomes, loot, and critical events."),
    };

    public static AttributeInfo? Find(int index) =>
        index >= 0 && index < Attributes.Count ? Attributes[index] : null;

    public static string DescriptionOf(int index) => Find(index)?.Description ?? "";
}
