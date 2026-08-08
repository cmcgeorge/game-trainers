namespace FountainOfDreamsTrainer.Game;

/// <summary>
/// Static facts about the Fountain of Dreams game, confirmed from the game files and manual.
/// Used by the locator and the UI for process detection and display.
/// </summary>
public static class GameFacts
{
    /// <summary>The game was released by Electronic Arts in 1990 for DOS.</summary>
    public const string GameTitle = "Fountain of Dreams";

    /// <summary>Publisher.</summary>
    public const string Publisher = "Electronic Arts";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1990;

    /// <summary>The main game engine executable (EXEPACK-compressed Microsoft C 1988).</summary>
    public const string MainExe = "KEH.EXE";

    /// <summary>The character creation launcher (EXEPACK-compressed).</summary>
    public const string CreationExe = "FOD.EXE";

    /// <summary>Process names to look for when attaching (the emulator, since it's a DOS game).</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    /// <summary>Game data files used by the engine.</summary>
    public static readonly string[] DataFiles =
        { "ARCHTYPE", "GLOBALS", "WEAPONS", "SERVICES", "PACKETS", "BORDERS", "HDSPCT" };

    /// <summary>Save game files (DISK1-DISK4).</summary>
    public static readonly string[] SaveFiles = { "DISK1", "DISK2", "DISK3", "DISK4" };

    /// <summary>Starting cash for new characters ranges from 0 to 50.</summary>
    public const int StartingCashMin = 0;
    public const int StartingCashMax = 50;

    /// <summary>Attribute range for new characters: 3-20.</summary>
    public const int AttributeMin = 3;
    public const int AttributeMax = 20;
}
