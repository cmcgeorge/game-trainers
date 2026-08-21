namespace DarksypreTrainer.Game;

/// <summary>
/// Confirmed constants for DarkSpyre (Event Horizon Software, 1990).
/// Sources: game manual (Amiga supplement, Lemon Amiga), Cheatbook walkthrough,
/// Wikipedia, and MobyGames. All values are [Confirmed] from published sources
/// unless marked [Inferred].
/// </summary>
internal static class GameFacts
{
    /// <summary>Game title.</summary>
    public const string GameTitle = "DarkSpyre";

    /// <summary>Developer.</summary>
    public const string Developer = "Event Horizon Software";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1990;

    /// <summary>Maximum attribute value (manual states "can never exceed 20"). [Confirmed]</summary>
    public const int MaxAttribute = 20;

    /// <summary>Maximum spell points (manual states "spell points can never exceed 100"). [Confirmed]</summary>
    public const int MaxSpellPoints = 100;

    /// <summary>Total levels in the game. [Confirmed]</summary>
    public const int TotalLevels = 50;

    /// <summary>Required levels to complete the game. [Confirmed]</summary>
    public const int RequiredLevels = 39;

    /// <summary>Number of character attributes. [Confirmed]</summary>
    public const int AttributeCount = 6;

    /// <summary>Number of weapon proficiency types. [Confirmed]</summary>
    public const int WeaponTypeCount = 7;

    /// <summary>Number of weapon proficiency levels. [Confirmed]</summary>
    public const int WeaponProficiencyLevels = 10;

    /// <summary>Number of magic skill classes. [Confirmed]</summary>
    public const int MagicClassCount = 6;

    /// <summary>Number of magic proficiency levels. [Confirmed]</summary>
    public const int MagicProficiencyLevels = 7;

    /// <summary>Number of armor protection levels. [Confirmed]</summary>
    public const int ArmorProtectionLevels = 15;

    /// <summary>Number of armor condition levels. [Confirmed]</summary>
    public const int ArmorConditionLevels = 7;

    /// <summary>Number of runes of power (the 5 special ones needed to complete the game). [Confirmed]</summary>
    public const int PowerRuneCount = 5;

    /// <summary>Total number of runes in the game. [Confirmed]</summary>
    public const int TotalRunes = 25;

    /// <summary>HP formula: Strength + Endurance + random. [Confirmed]</summary>
    public const string HpFormula = "Strength + Endurance + Random";

    /// <summary>SP formula: Talent + Power + random. [Confirmed]</summary>
    public const string SpFormula = "Talent + Power + Random";

    /// <summary>Emulator process name hints for auto-selection.</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "boxer" };
}
