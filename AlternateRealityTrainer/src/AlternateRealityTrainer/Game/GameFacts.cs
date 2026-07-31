namespace AlternateRealityTrainer.Game;

/// <summary>A keyboard command, exactly as the game's own help panel lists it.</summary>
public readonly record struct ControlInfo(string Key, string Action);

/// <summary>
/// Facts about <c>Alternate Reality: The City</c> that the trainer displays but never edits.
/// Every string here was read out of <c>CITY.EXE</c>'s data segment, not from a platform manual.
/// </summary>
public static class GameFacts
{
    public const string GameTitle = "Alternate Reality: The City";
    public const string Publisher = "Datasoft / Intellicreations, 1987–88";
    public const string CityName = "Xebec's Demise";

    /// <summary>The City is a 64 × 64 grid; square 1N, 1E is the south-west corner.</summary>
    public const int CitySize = 64;

    /// <summary>Roughly one game hour per four real minutes.</summary>
    public const int RealMinutesPerGameHour = 4;

    /// <summary>Process-name fragments that mark a likely emulator, floated to the top of the list.</summary>
    public static readonly IReadOnlyList<string> EmulatorHints = new[]
    {
        "dosbox", "dosbox-x", "dosbox-staging", "pcem", "86box", "qemu", "boxer",
    };

    /// <summary>The eleven months of the City calendar, in order.</summary>
    public static readonly IReadOnlyList<string> Months = new[]
    {
        "Rebirth", "Awakening", "Winds", "Rains", "Sowings", "First Fruits",
        "Harvest", "Final Reaping", "Darkness", "Cold Winds", "Lights",
    };

    /// <summary>
    /// The keyboard commands, verbatim from the game's help panel
    /// (<c>The commands are: &lt;G&gt;et, &lt;U&gt;se, &lt;D&gt;rop, …</c>).
    /// </summary>
    public static readonly IReadOnlyList<ControlInfo> Controls = new[]
    {
        new ControlInfo("↑",  "Walk forward one square"),
        new ControlInfo("↓",  "Walk backward one square"),
        new ControlInfo("← →", "Turn left / right"),
        new ControlInfo("G", "Get an item"),
        new ControlInfo("U", "Use an item — equip a weapon or armour, drink a saved potion"),
        new ControlInfo("D", "Drop an item"),
        new ControlInfo("C", "Cast a spell (nothing to cast in The City)"),
        new ControlInfo("S", "Save the game"),
        new ControlInfo("P", "Pause — stops the clock, safe for mapping"),
        new ControlInfo("W", "Switch primary and secondary weapons"),
        new ControlInfo("Esc", "Cancel / back out of a prompt"),
    };

    /// <summary>The encounter menu, verbatim from the game.</summary>
    public static readonly IReadOnlyList<string> EncounterOptions = new[]
    {
        "1) Attack", "2) Trick", "3) Charm", "4) Offer", "5) Leave", "6) Lunge",
    };

    /// <summary>The condition banners the game prints along the bottom of the screen.</summary>
    public static readonly IReadOnlyList<string> Conditions = new[]
    {
        "Famished", "Starving", "Thirsty", "Very Thirsty", "Parched", "Weary", "Tired",
        "Very Tired", "Drunk", "Very Drunk", "Poisoned!", "Diseased!", "Burdened",
        "Encumbered", "Immobilized!", "Bloated", "Cursed!",
    };

    /// <summary>
    /// The eighteen creatures the cluebook lists as evil — the only ones you may attack, trick or
    /// charm without losing moral alignment.
    /// </summary>
    public static readonly IReadOnlyList<string> EvilCreatures = new[]
    {
        "Assassin", "Black Slime", "Brown Mold", "Ghost", "Ghoul", "Giant Rat", "Gnoll",
        "Goblin", "Gremlin", "Imp", "Nightstalker", "Orc", "Skeleton", "Spectre", "Troll",
        "Wolf", "Wraith", "Zombie",
    };

    /// <summary>Weapons, weakest first, as named in the game's item table.</summary>
    public static readonly IReadOnlyList<string> Weapons = new[]
    {
        "Dagger", "Stiletto", "Shortsword", "Flail", "Battle Axe", "Sword",
        "Battle Hammer", "Longsword", "Magical Flamesword",
    };

    /// <summary>Armour materials, weakest first. Each exists as helmet, coat, gauntlets and greaves.</summary>
    public static readonly IReadOnlyList<string> ArmourMaterials = new[]
    {
        "Padded", "Leather", "Studded", "Ringmail", "Scalemail", "Splintmail",
        "Elfinmail", "Chainmail", "Banded", "Crystal", "Plated",
    };

    /// <summary>Shields add parry chance, not armour.</summary>
    public static readonly IReadOnlyList<string> Shields = new[]
    {
        "Small Shield", "Shield", "Spiked Shield", "Tower Shield",
    };

    /// <summary>Short survival notes drawn from the strategy guide.</summary>
    public static readonly IReadOnlyList<string> Tips = new[]
    {
        "Never still be inside a Tavern or a Bank at closing time — get locked in and that kind of building is barred to you forever.",
        "Never Leave an encounter with a Thief or a Mugger; disengaging is exactly when they rob you.",
        "Equip a newly found weapon as your SECONDARY first, in case it is cursed.",
        "Giant Rats, Brown Mold and Black Slime carry disease that surfaces two or three days later.",
        "A Ghost's touch permanently drains Strength.",
        "Your first visit to each of the twelve guilds raises a stat for free.",
        "Buy a compass for 5 silver at any shop, then map with P (pause) held.",
        "Night and rain both multiply your encounter rate.",
    };
}
