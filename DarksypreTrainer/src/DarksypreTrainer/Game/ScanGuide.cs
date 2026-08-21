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
/// Scan recipes for the state the Character tab does not locate automatically, plus fallbacks
/// for hit points and spell points in case the locator comes up empty on a different release of
/// the game. Attributes, encumbrance and the HP/SP maxima are always found automatically, so
/// they have no recipe here.
///
/// Pause the game with <b>P</b> before reading a value: DarkSpyre runs in real time and the
/// number under your eyes can move between reading it and typing it.
/// </summary>
internal static class ScanGuide
{
    public static IReadOnlyList<ScanRecipe> Recipes { get; } = new[]
    {
        new ScanRecipe(
            "level", "Level Number", ScanWidth.Byte, 1, 1, GameFacts.TotalLevels,
            "1..50",
            "Press F8 for the information display and read the level you are on. Type it and " +
            "First Scan; find a gateway and step through, then press F8, type the new level and " +
            "scan Exact. Repeat until one row remains, then Pin it. The high-score file stores " +
            "the level as a single byte, so start with Byte and retry as Int16 if nothing " +
            "narrows. (The Thurisaz rune also moves you a level on.)"),

        new ScanRecipe(
            "score", "Score", ScanWidth.Int16, 0, 0, 65535,
            "0..65535",
            "Press F8 and read your score. Type it and First Scan; kill a monster or pick up an " +
            "item so the score rises, press F8 again, type the new score and scan Exact. The " +
            "high-score file keeps scores as 16-bit values, so start with Int16 and retry as " +
            "Int32 if nothing narrows."),

        new ScanRecipe(
            "hp", "Hit Points (fallback)", ScanWidth.Int16, 20, 1, 999,
            "1..999",
            "Only needed if the Character tab cannot find your character. Read current HP from " +
            "the status bars, type it and First Scan; take a hit so HP drops, type the new value " +
            "and scan Exact. Repeat until one row remains, then Pin it."),

        new ScanRecipe(
            "sp", "Spell Points (fallback)", ScanWidth.Int16, 10, 0, 999,
            "0..999",
            "Only needed if the Character tab cannot find your character. Read current SP from " +
            "the status bars, type it and First Scan; cast a spell so SP drops, type the new " +
            "value and scan Exact. Repeat until one row remains, then Pin it."),
    };
}
