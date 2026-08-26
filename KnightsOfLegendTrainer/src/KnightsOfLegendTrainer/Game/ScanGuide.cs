namespace KnightsOfLegendTrainer.Game;

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
/// Scan recipes for Knights of Legend's live character state. No game binary was available
/// for static analysis, so there is no GameLocator — the value scanner is the only way to
/// find these fields in live memory. Pause the game (it is turn-based, so it pauses between
/// action rounds) before reading a value.
///
/// All recipes are [Inferred] — the stat ranges and types are from the manual, but the
/// in-memory representation (byte width, signedness) is not confirmed.
/// </summary>
internal static class ScanGuide
{
    public static IReadOnlyList<ScanRecipe> Recipes { get; } = new[]
    {
        new ScanRecipe(
            "gold", "Gold Crowns", ScanWidth.Int32, 1000, 0, 999999,
            "0..999999",
            "Open the character sheet (press the Character icon or the matching key) and " +
            "read the Gold Crowns value. Type it and First Scan; buy something or earn gold " +
            "so the value changes, then type the new value and scan Exact. Repeat until one " +
            "row remains, then Pin it. Gold is likely a 32-bit signed integer; try Int16 if " +
            "nothing narrows. [Inferred]"),

        new ScanRecipe(
            "adventure_points", "Adventure Points", ScanWidth.Int32, 0, 0, 999999,
            "0..999999",
            "Open the character sheet and read your Adventure Points (experience). Type it " +
            "and First Scan; win a battle or train so the value changes, then scan Exact. " +
            "Repeat until one row remains, then Pin it. [Inferred]"),

        new ScanRecipe(
            "body_points", "Body Points (current)", ScanWidth.Int16, 30, 1, 999,
            "1..999",
            "Read current Body Points from the character sheet or combat screen. Type it " +
            "and First Scan; take damage in combat so the value drops, type the new value " +
            "and scan Exact. Repeat until one row remains, then Pin it. [Inferred]"),

        new ScanRecipe(
            "max_body_points", "Max Body Points", ScanWidth.Int16, 30, 1, 999,
            "1..999",
            "Read maximum Body Points from the character sheet. Type it and First Scan; " +
            "if the value does not change between scans, use Unchanged to narrow. This may " +
            "share an address region with current BP — look for a nearby Int16 that does " +
            "not move when current BP does. [Inferred]"),

        new ScanRecipe(
            "strength", "Strength", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read Strength from the character sheet. Type it and First Scan; it rarely " +
            "changes, so use Unchanged between scans while other stats move. If it does " +
            "change (drain, restoration), scan Exact with the new value. [Inferred]"),

        new ScanRecipe(
            "quickness", "Quickness", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read Quickness from the character sheet. Same procedure as Strength — it is " +
            "stable, so Unchanged narrowing is most effective. [Inferred]"),

        new ScanRecipe(
            "health", "Health (stat)", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read the Health primary statistic from the character sheet (not Body Points). " +
            "Same procedure as Strength. [Inferred]"),

        new ScanRecipe(
            "foresight", "Foresight", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read Foresight from the character sheet. This determines whether you see " +
            "enemy actions before executing your own in combat. Same scan procedure. " +
            "[Inferred]"),

        new ScanRecipe(
            "charisma", "Charisma", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read Charisma from the character sheet. Needs to be 80+ to receive most " +
            "quests. Same scan procedure. [Inferred]"),

        new ScanRecipe(
            "intellect", "Intellect", ScanWidth.Byte, 50, 0, 100,
            "0..100",
            "Read Intellect from the character sheet. Same scan procedure. [Inferred]"),

        new ScanRecipe(
            "level", "Level", ScanWidth.Byte, 1, 1, 25,
            "1..25",
            "Read your current class level from the character sheet. Type it and First " +
            "Scan; promote in the arena so the level rises, type the new value and scan " +
            "Exact. Range is 1 (Peasant) to 25 (Knight). [Inferred]"),

        new ScanRecipe(
            "fatigue", "Fatigue / Endurance", ScanWidth.Byte, 0, 0, 100,
            "0..100",
            "Read the endurance/fatigue value from the combat screen. Type it and First " +
            "Scan; take an action that costs energy, type the new value and scan Exact. " +
            "Every combat action (attack, defend, move) costs energy; resting recovers it. " +
            "[Inferred]"),

        new ScanRecipe(
            "rations", "Rations / Food", ScanWidth.Int16, 10, 0, 999,
            "0..999",
            "Read rations from the character sheet or inventory. Type it and First Scan; " +
            "eat or buy food so the value changes, then scan Exact. [Inferred]"),
    };
}
