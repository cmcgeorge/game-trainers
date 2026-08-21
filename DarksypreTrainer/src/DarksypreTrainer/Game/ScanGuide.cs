namespace DarksypreTrainer.Game;

/// <summary>
/// One guided-scan recipe: tells the user what on-screen value to scan for, what width
/// to use, and gives step-by-step instructions for narrowing it to a single address.
/// </summary>
public sealed record ScanRecipe(
    string Field,
    string Label,
    ScanWidth Width,
    long SuggestedDefault,
    long TypicalMin,
    long TypicalMax,
    string Range,
    string Instructions);

/// <summary>
/// Pre-built scan recipes for DarkSpyre's character stats. Each recipe picks the right
/// scan width and gives the user a concrete narrowing strategy that works in the game's
/// real-time environment (pause with P to read values safely).
/// </summary>
internal static class ScanGuide
{
    public static IReadOnlyList<ScanRecipe> Recipes { get; } = new[]
    {
        new ScanRecipe(
            "hp", "Hit Points", ScanWidth.Int16, 20, 1, 999,
            "1..999",
            "Read your current HP from the HP bar at the top of the character screen. " +
            "Type it and First Scan; take a hit from a monster so HP drops; type the new " +
            "value and scan Exact. Repeat until one row remains, then Pin it."),

        new ScanRecipe(
            "sp", "Spell Points", ScanWidth.Byte, 10, 0, GameFacts.MaxSpellPoints,
            "0..100",
            "Read your current SP from the SP bar. Type it and First Scan; cast a spell " +
            "so SP drops; type the new value and scan Exact. Repeat until one row remains, " +
            "then Pin it. SP maxes at 100."),

        new ScanRecipe(
            "str", "Strength", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Strength. Type it and First Scan. " +
            "Strength does not change normally — if you know the exact value, one Exact " +
            "scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "agi", "Agility", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Agility. Type it and First Scan. " +
            "Agility does not change normally — one Exact scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "end", "Endurance", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Endurance. Type it and First Scan. " +
            "Endurance does not change normally — one Exact scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "acc", "Accuracy", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Accuracy. Type it and First Scan. " +
            "Accuracy does not change normally — one Exact scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "tal", "Talent", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Talent. Type it and First Scan. " +
            "Talent does not change normally — one Exact scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "pwr", "Power", ScanWidth.Byte, 10, 1, GameFacts.MaxAttribute,
            "1..20",
            "Press A to show attributes; read Power. Type it and First Scan. " +
            "Power does not change normally — one Exact scan should narrow it. Pin the survivor."),

        new ScanRecipe(
            "enc", "Encumbrance", ScanWidth.Byte, 0, 0, 255,
            "0..255",
            "Read the ENC value from the status bars. Type it and First Scan; pick up or " +
            "drop an item so ENC changes; type the new value and scan Exact. Pin the survivor."),

        new ScanRecipe(
            "level", "Level Number", ScanWidth.Int16, 1, 1, GameFacts.TotalLevels,
            "1..50",
            "Press F8 to display your current level. Type it and First Scan; find a gateway " +
            "and step through to the next level; press F8, type the new level, scan Exact. " +
            "Pin the survivor. (Thurisaz rune also takes you to the next level.)"),

        new ScanRecipe(
            "score", "Score", ScanWidth.Int32, 0, 0, 999999,
            "0..999999",
            "Press F8 to display your score. Type it and First Scan; kill a monster or " +
            "pick up an item so the score increases; press F8, type the new score, scan " +
            "Exact. Pin the survivor."),
    };
}
