namespace JumpmanLivesTrainer.Game;

/// <summary>A keyboard control mapping.</summary>
/// <param name="Key">The key or keys.</param>
/// <param name="Action">What it does.</param>
public readonly record struct ControlInfo(string Key, string Action);

/// <summary>A level entry in the reference table.</summary>
/// <param name="Number">Level number (1–45).</param>
/// <param name="Name">The level's title as the game displays it.</param>
/// <param name="Bonus">Starting time bonus (0 for three special levels).</param>
/// <param name="Set">Which set the level belongs to.</param>
public readonly record struct LevelInfo(int Number, string Name, int Bonus, string Set);

/// <summary>
/// Facts about Jumpman Lives! that the trainer displays but never edits. Everything here was read
/// out of the game's source code and linker map, not taken from a platform manual.
/// </summary>
public static class GameFacts
{
    /// <summary>Title as the game prints it.</summary>
    public const string GameTitle = "Jumpman Lives!";

    /// <summary>Publisher and year.</summary>
    public const string Publisher = "Apogee Software, 1991";

    /// <summary>Author.</summary>
    public const string Author = "Dave Sharpless";

    /// <summary>The shipped executable name.</summary>
    public const string ExecutableName = "JMAN2.EXE";

    /// <summary>The executable's size in bytes.</summary>
    public const int ExecutableSize = 136_431;

    /// <summary>The compiler the game was built with.</summary>
    public const string Compiler = "Borland Turbo Pascal 6.0";

    /// <summary>Process-name fragments that mark a likely emulator.</summary>
    public static readonly IReadOnlyList<string> EmulatorHints = new[]
    {
        "dosbox", "dosbox-x", "dosbox-staging", "pcem", "86box", "qemu", "boxer",
    };

    /// <summary>
    /// The keyboard controls, from the game's own <c>Init_Once</c> (default scan codes) and the
    /// <c>Play</c> / <c>Main_Selector</c> / <c>RTime</c> procedures in the source code.
    /// </summary>
    public static readonly IReadOnlyList<ControlInfo> Controls = new[]
    {
        new ControlInfo("Left / Right arrows", "Move left and right"),
        new ControlInfo("Up / Down arrows", "Climb up and down ladders, ropes, and vines"),
        new ControlInfo("Space", "Jump (hold a direction for a directional jump; press alone for straight up)"),
        new ControlInfo("1 – 8", "Set game speed (1 = fastest, 8 = slowest)"),
        new ControlInfo("F1", "Pause the game"),
        new ControlInfo("Esc", "Pause / quit (confirm with Y)"),
        new ControlInfo("Tab × 4 (main menu)", "Enable trainer mode — 21 lives instead of 7"),
        new ControlInfo("Backspace (trainer mode)", "Skip current level; hold S to save first"),
        new ControlInfo("D (during pause)", "Save a screenshot"),
    };

    /// <summary>
    /// The 45 levels in play order, from the source code's <c>scrtitle</c> and
    /// <c>current_level</c> assignments. Three levels have no time bonus (bonus = 0).
    /// </summary>
    public static readonly IReadOnlyList<LevelInfo> Levels = new[]
    {
        new LevelInfo( 1, "NOTHING TO IT",       1500, "Jumpman"),
        new LevelInfo( 2, "ROBOTS",              1500, "Jumpman"),
        new LevelInfo( 3, "INVASION",               0, "Jumpman"),
        new LevelInfo( 4, "JUMPING BLOCKS",      1500, "Jumpman"),
        new LevelInfo( 5, "GRAND PUZZLE I",      1500, "Jumpman"),
        new LevelInfo( 6, "BOMBS AWAY",          1500, "Jumpman"),
        new LevelInfo( 7, "DRAGON SLAYER",         0, "Jumpman"),
        new LevelInfo( 8, "VAMPIRE",              1500, "Jumpman"),
        new LevelInfo( 9, "HAILSTONES",           1500, "Jumpman"),
        new LevelInfo(10, "FIGURIT",              1500, "Jumpman"),
        new LevelInfo(11, "GUNFIGHTER",             0, "Jumpman"),
        new LevelInfo(12, "FOLLOW THE LEADER",    1500, "Jumpman"),
        new LevelInfo(13, "EASY DOES IT",         1500, "Jumpman Jr"),
        new LevelInfo(14, "VINE MADNESS",         1500, "Jumpman Jr"),
        new LevelInfo(15, "HOPPING HEIGHTS",      1500, "Jumpman Jr"),
        new LevelInfo(16, "FIRE! FIRE!",          1500, "Jumpman Jr"),
        new LevelInfo(17, "HOTFOOT",              1500, "Jumpman Jr"),
        new LevelInfo(18, "BUILDER",              1500, "Jumpman Jr"),
        new LevelInfo(19, "LADDER CHALLENGE",     1500, "Jumpman Jr"),
        new LevelInfo(20, "HERETHEREEVERYWHERE",  1500, "Jumpman Jr"),
        new LevelInfo(21, "A LADDER PLEASE",      1500, "Jumpman Jr"),
        new LevelInfo(22, "PYRIMID",              1500, "Jumpman Jr"),
        new LevelInfo(23, "ROUND ABOUT",          1500, "Jumpman Jr"),
        new LevelInfo(24, "IN BIG TROUBLE",       1500, "Jumpman Jr"),
        new LevelInfo(25, "JUNGLE",               1500, "Jumpman Jr"),
        new LevelInfo(26, "THE ROOST",            1500, "Jumpman Jr"),
        new LevelInfo(27, "GRAND PUZZLE II",      1500, "Jumpman Jr"),
        new LevelInfo(28, "LOOK OUT BELOW",       1500, "Original"),
        new LevelInfo(29, "JUMP AND RUN",         1500, "Original"),
        new LevelInfo(30, "NOW YOU SEE IT...",    1500, "Original"),
        new LevelInfo(31, "SREDDAL",              1500, "Original"),
        new LevelInfo(32, "A BIT OF ROPING..",    1500, "Original"),
        new LevelInfo(33, "FIGURITS REVENGE",     1500, "Original"),
        new LevelInfo(34, "WALLS",                1500, "Original"),
        new LevelInfo(35, "THE PIT",              1500, "Original"),
        new LevelInfo(36, "HELLSTONES",           1500, "Original"),
        new LevelInfo(37, "ZIG ZAG",              1500, "Original"),
        new LevelInfo(38, "THE TREE",             1500, "Original"),
        new LevelInfo(39, "FREEZE",               1500, "Original"),
        new LevelInfo(40, "TARZAN",               1500, "Original"),
        new LevelInfo(41, "HURRICANE",            1500, "Original"),
        new LevelInfo(42, "GOING DOWN?",          1500, "Original"),
        new LevelInfo(43, "HATCHLINGS",           1500, "Original"),
        new LevelInfo(44, "ROLL ME OVER",         1500, "Original"),
        new LevelInfo(45, "GRAND PUZZLE III",     1500, "Original"),
    };

    /// <summary>Short survival tips drawn from the strategy guide.</summary>
    public static readonly IReadOnlyList<string> Tips = new[]
    {
        "Press TAB four times at the main menu for 21 lives — it is built into the game, not a cheat.",
        "Speed 1 is fastest and earns the most time bonus, but speed 4–5 is safer on tricky levels.",
        "The time bonus starts at 1500 and drops by 100 every few seconds — finish quickly for more points.",
        "Three levels have no time bonus (INVASION, DRAGON SLAYER, GUNFIGHTER) — take your time on those.",
        "Every 10,000 points awards an extra life.",
        "Save every 5 levels when the game prompts you — restarting from a save beats restarting from level 1.",
        "Press Space without a direction for a straight-up jump — essential for pellets directly above you.",
        "Use trainer mode (TAB ×4) and Backspace to skip levels you are stuck on and practice later ones.",
        "Falling too far costs a life. Use directional jumps to cross gaps instead of walking off edges.",
        "In multiplayer, each player has independent lives and score. Players alternate turns on each life.",
    };
}
