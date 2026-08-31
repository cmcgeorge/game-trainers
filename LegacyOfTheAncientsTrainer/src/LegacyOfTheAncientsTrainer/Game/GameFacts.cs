namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>
/// Static facts about Legacy of the Ancients, confirmed from the game files and manual.
/// Used by the locator and the UI for process detection and display.
/// </summary>
public static class GameFacts
{
    /// <summary>The game was released by Electronic Arts in 1987 (IBM version 1989).</summary>
    public const string GameTitle = "Legacy of the Ancients";

    /// <summary>Publisher.</summary>
    public const string Publisher = "Electronic Arts";

    /// <summary>Developer.</summary>
    public const string Developer = "Quest Software, Inc.";

    /// <summary>Designers.</summary>
    public const string Designers = "John and Charles Dougherty";

    /// <summary>IBM version by.</summary>
    public const string IbmPort = "Al DeYoung";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1987;

    /// <summary>The main game library — a Microsoft BASIC Compiler Runtime v6.00.</summary>
    public const string MainLib = "LEGLIB.EXE";

    /// <summary>Key game modules.</summary>
    public static readonly string[] Modules =
        { "MENU.EXE", "OUT.EXE", "DUN.EXE", "TWNDR.EXE", "CASDR.EXE", "STDRV.EXE" };

    /// <summary>The character save file.</summary>
    public const string SaveFile = "CHAR.DAT";

    /// <summary>Process names to look for when attaching (the emulator, since it's a DOS game).</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    /// <summary>Starting characteristic value per the manual (all five start at 15).</summary>
    public const int StartingCharacteristic = 15;

    /// <summary>Starting hit points per the manual.</summary>
    public const int StartingHP = 200;

    /// <summary>Starting level per the manual.</summary>
    public const int StartingLevel = 1;

    /// <summary>Maximum level (the caretaker promotes through 10 levels).</summary>
    public const int MaxLevel = 10;

    /// <summary>Number of buyable spells per the manual.</summary>
    public const int SpellCount = 6;

    /// <summary>Maximum charges for most spells (99 per the walkthrough).</summary>
    public const int MaxSpellCharges = 99;

    /// <summary>Maximum charges for Kill Flash (20 per the walkthrough).</summary>
    public const int MaxKillFlashCharges = 20;

    /// <summary>Number of wilderness creatures per the manual.</summary>
    public const int WildernessMonsterCount = 32;

    /// <summary>Number of dungeon creatures per the manual.</summary>
    public const int DungeonMonsterCount = 12;

    /// <summary>Number of towns per the manual.</summary>
    public const int TownCount = 12;

    /// <summary>Number of dungeon levels per the manual.</summary>
    public const int DungeonLevelCount = 24;

    /// <summary>Maximum healing herbs the player can carry.</summary>
    public const int MaxHealingHerbs = 40;

    /// <summary>HP table by level (from the walkthrough, levels 1-10).</summary>
    public static readonly int[] HPByLevel = { 200, 300, 500, 800, 1200, 1600, 2200, 0, 0, 3000 };
}
