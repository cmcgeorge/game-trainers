namespace KnightsOfLegendTrainer.Game;

/// <summary>
/// Confirmed constants for Knights of Legend (Origin Systems, 1989, by Todd Porter).
/// Sources, in order of authority: the game manual (FreeGameEmpire), the RPG Gamers walkthrough,
/// the CRPG Addict blog, and Wikipedia. No game binary or memory dumps were available, so every
/// value is marked [Manual] (confirmed from the manual or walkthrough) or [Inferred] (plausible
/// from the sources but unconfirmed against the running game).
/// </summary>
internal static class GameFacts
{
    /// <summary>Game title.</summary>
    public const string GameTitle = "Knights of Legend";

    /// <summary>Developer.</summary>
    public const string Developer = "Origin Systems";

    /// <summary>Designer.</summary>
    public const string Designer = "Todd Porter";

    /// <summary>Release year (Apple II/C64). [Manual]</summary>
    public const int ReleaseYear = 1989;

    /// <summary>DOS release year. [Manual]</summary>
    public const int DosReleaseYear = 1990;

    /// <summary>Maximum primary statistic value. [Manual]</summary>
    public const int MaxStatistic = 100;

    /// <summary>Minimum primary statistic value (rolled at creation). [Manual]</summary>
    public const int MinStatistic = 0;

    /// <summary>Number of primary statistics. [Manual]</summary>
    public const int PrimaryStatCount = 7;

    /// <summary>Maximum party size. [Manual]</summary>
    public const int MaxPartySize = 6;

    /// <summary>Maximum saved characters per disk. [Manual]</summary>
    public const int MaxSavedCharacters = 16;

    /// <summary>Number of races. [Manual]</summary>
    public const int RaceCount = 4;

    /// <summary>Total number of character classes. [Manual]</summary>
    public const int ClassCount = 33;

    /// <summary>Number of magic orders. [Manual]</summary>
    public const int MagicOrderCount = 6;

    /// <summary>Total number of quests. [Manual]</summary>
    public const int QuestCount = 24;

    /// <summary>Starting town. [Manual]</summary>
    public const string StartingTown = "Brettle";

    /// <summary>Setting: the duchy of Ashtalarea. [Manual]</summary>
    public const string Setting = "Ashtalarea";

    /// <summary>Parent kingdom. [Manual]</summary>
    public const string Kingdom = "Sondar";

    /// <summary>Currency unit. [Manual]</summary>
    public const string Currency = "Gold Crowns";

    /// <summary>Experience unit. [Manual]</summary>
    public const string Experience = "Adventure Points";

    /// <summary>Training cost per session in Gold Crowns. [Manual]</summary>
    public const int TrainingCost = 200;

    /// <summary>Adventure points per skill level for training. [Manual]</summary>
    public const int TrainingApCost = 100;

    /// <summary>Skill points formula: 20 x level. [Manual]</summary>
    public const int SkillPointsPerLevel = 20;

    /// <summary>Lowest class level (Peasant). [Manual]</summary>
    public const int MinLevel = 1;

    /// <summary>Highest class level (Knight). [Manual]</summary>
    public const int MaxLevel = 25;

    /// <summary>Inn cost at Trollsbane Inn per character per night. [Manual]</summary>
    public const int SafeInnCost = 60;

    /// <summary>Free inn (Broken Keg) cost. [Manual]</summary>
    public const int FreeInnCost = 0;

    /// <summary>Arrows per archer per battle. [Manual]</summary>
    public const int ArrowsPerBattle = 20;

    /// <summary>Emulator process name hints for auto-selection.</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "boxer" };
}
