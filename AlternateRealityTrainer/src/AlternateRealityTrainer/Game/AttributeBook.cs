namespace AlternateRealityTrainer.Game;

/// <summary>One of the seven character attributes.</summary>
/// <param name="Index">Position in the record's attribute array (see <see cref="CharacterFormat.AttributeOffset"/>).</param>
/// <param name="Name">Full name as the game's guild screens spell it.</param>
/// <param name="Abbreviation">The three-letter form the status bar uses, or <c>SPD</c> for the hidden one.</param>
/// <param name="Hidden">True for Physical Speed, which has no column on the status bar.</param>
public readonly record struct AttributeInfo(int Index, string Name, string Abbreviation, bool Hidden);

/// <summary>
/// The seven attributes, in the order they are <b>stored</b> in the character record — which is not
/// the order the status bar prints them in. Storage order was confirmed live: <c>Neuro</c>'s record
/// holds 9, 12, 16, 11, 22, 17, 14 while the screen read STA 22, CHR 17, STR 9, INT 12, WIS 16,
/// SKL 11.
/// </summary>
public static class AttributeBook
{
    public static readonly IReadOnlyList<AttributeInfo> All = new[]
    {
        new AttributeInfo(0, "Strength",      "STR", false),
        new AttributeInfo(1, "Intelligence",  "INT", false),
        new AttributeInfo(2, "Wisdom",        "WIS", false),
        new AttributeInfo(3, "Skill",         "SKL", false),
        new AttributeInfo(4, "Stamina",       "STA", false),
        new AttributeInfo(5, "Charm",         "CHR", false),
        new AttributeInfo(6, "Physical Speed","SPD", true),
    };

    /// <summary>The order the game's own status bar prints the six visible attributes in.</summary>
    public static readonly IReadOnlyList<int> DisplayOrder = new[] { 4, 5, 0, 1, 2, 3 };

    public static AttributeInfo At(int index) => All[index];
}
