namespace Wizardry1Trainer.Game;

/// <summary>
/// Static game facts about Wizardry 1: Proving Grounds of the Mad Overlord (Sir-Tech, 1981),
/// used by the trainer's UI and reference tabs.
/// </summary>
public static class GameFacts
{
    public const string GameTitle = "Wizardry: Proving Grounds of the Mad Overlord";
    public const string GameYear = "1981";
    public const string GameDeveloper = "Sir-Tech Software, Inc.";
    public const string GameAuthors = "Andrew C. Greenberg & Robert Woodhead";

    /// <summary>Process names the trainer looks for as emulator hints.</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "boxer" };

    /// <summary>The launcher batch file name.</summary>
    public const string LauncherBatch = "WIZ1.BAT";

    /// <summary>The disk image name.</summary>
    public const string DiskImage = "WIZ1.DSK";

    /// <summary>The p-system emulator that runs the disk image.</summary>
    public const string EmulatorExe = "WIZDOS.COM";

    /// <summary>Character record size in bytes (TCHAR).</summary>
    public const int RecordSize = CharacterFormat.RecordSize;

    /// <summary>Maximum party size.</summary>
    public const int MaxPartySize = 6;

    /// <summary>Dungeon dimensions (20 x 20 per level).</summary>
    public const int MazeSize = 20;

    /// <summary>Number of dungeon levels.</summary>
    public const int DungeonLevels = 10;

    /// <summary>XP required to reach each level (cumulative, from the game manual).</summary>
    public static readonly long[] XpForLevel =
    {
        0,        // level 1 (start)
        0,        // level 2
        1000,     // level 3
        3000,     // level 4
        6000,     // level 5
        12000,    // level 6
        20000,    // level 7
        35000,    // level 8
        60000,    // level 9
        100000,   // level 10
        160000,   // level 11
        240000,   // level 12
        360000,   // level 13
        500000,   // level 14
        700000,   // level 15
        1000000,  // level 16
        1400000,  // level 17
        1900000,  // level 18
        2500000,  // level 19
        3200000,  // level 20
    };
}
