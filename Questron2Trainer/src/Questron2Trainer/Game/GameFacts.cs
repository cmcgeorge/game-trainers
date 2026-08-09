namespace Questron2Trainer.Game;

/// <summary>
/// Static facts about the Questron II game, confirmed from the game files and manual.
/// Used by the locator and the UI for process detection and display.
/// </summary>
public static class GameFacts
{
    /// <summary>The game was released by SSI in 1988 for DOS.</summary>
    public const string GameTitle = "Questron II";

    /// <summary>Publisher.</summary>
    public const string Publisher = "Strategic Simulations, Inc.";

    /// <summary>Developer.</summary>
    public const string Developer = "Westwood Associates";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1988;

    /// <summary>The main game engine executable (EXEPACK-compressed Microsoft C 1987).</summary>
    public const string MainExe = "START.EXE";

    /// <summary>Game version string from START.EXE.</summary>
    public const string GameVersion = "1.2";

    /// <summary>Copyright string from START.EXE, used as the locator anchor.</summary>
    public const string CopyrightString = "Questron II (C) 1988 S.S.I.";

    /// <summary>Process names to look for when attaching (the emulator, since it's a DOS game).</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    /// <summary>Starting values per the manual: HP, Food, and Gold all begin at 200.</summary>
    public const int StartingVital = 200;

    /// <summary>Attribute range for new characters.</summary>
    public const int AttributeMin = 1;
    public const int AttributeMax = 25;

    /// <summary>Number of buyable spells per the manual (plus Destruct found in strings).</summary>
    public const int BuyableSpellCount = 4;
}
