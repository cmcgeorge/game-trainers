namespace EyeOfTheBeholder1Trainer.Game;

/// <summary>Confirmed game-level constants for Eye of the Beholder I.</summary>
public static class GameFacts
{
    /// <summary>Full title of the game.</summary>
    public const string GameTitle = "Eye of the Beholder";

    /// <summary>Developer/publisher.</summary>
    public const string Developer = "Westwood Studios / SSI";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1991;

    /// <summary>Process name when running under DOSBox (the emulator, not the game itself).</summary>
    public const string EmulatorProcess = "dosbox";

    /// <summary>Main game executable.</summary>
    public const string GameExe = "EOB.EXE";

    /// <summary>Save game file name.</summary>
    public const string SaveFileName = "EOBDATA.SAV";

    /// <summary>Number of dungeon levels.</summary>
    public const int DungeonLevels = 12;

    /// <summary>Each dungeon level is a grid of this size (32×32).</summary>
    public const int LevelGridSize = 32;

    /// <summary>Maximum party size.</summary>
    public const int MaxPartySize = 6;

    /// <summary>Character name maximum length.</summary>
    public const int MaxNameLength = 10;

    /// <summary>The final boss of the game.</summary>
    public const string FinalBoss = "Xanathar";
}
