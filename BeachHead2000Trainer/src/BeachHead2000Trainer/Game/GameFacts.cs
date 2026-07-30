namespace BeachHead2000Trainer.Game;

/// <summary>
/// The three weapon types the player's bunker can fire. Each has its own ammo pool
/// defined per level by the <c>Ammo</c> line in the level file.
/// </summary>
public sealed record WeaponInfo(int Index, string Name, string Description)
{
    public static readonly IReadOnlyList<WeaponInfo> Weapons = new[]
    {
        new WeaponInfo(0, "Bullets",
            "Machine-gun fire (left mouse). High rate, low damage per shot. " +
            "Best against infantry and light vehicles."),
        new WeaponInfo(1, "Projectiles",
            "Cannon shells (right mouse). Slow rate, high damage. " +
            "Best against tanks and APCs."),
        new WeaponInfo(2, "Missiles",
            "Guided missiles (spacebar). Limited supply, high damage. " +
            "Best against helicopters and jets."),
    };
}

/// <summary>
/// The enemy unit types that attack the player's beach position. Each level file
/// declares how many of each type appear and whether they are initially visible.
/// </summary>
public sealed record EnemyInfo(int Index, string Name, string Description)
{
    public static readonly IReadOnlyList<EnemyInfo> Enemies = new[]
    {
        new EnemyInfo(0, "Infantry Barge",
            "Soldiers landing from barges. Easy to kill with bullets but " +
            "numerous — don't let them reach your bunker."),
        new EnemyInfo(1, "Vehicle Barge",
            "Landing craft delivering trucks and light vehicles. " +
            "Use projectiles for faster kills."),
        new EnemyInfo(2, "Tank",
            "M48 main battle tanks. Heavy armor — projectiles or missiles " +
            "are the only reliable way to destroy them."),
        new EnemyInfo(3, "APC",
            "Armored personnel carriers. Moderately armored; " +
            "projectiles work well."),
        new EnemyInfo(4, "Bomber",
            "High-altitude bombers that drop ordnance on your position. " +
            "Shoot them down with missiles before they release."),
        new EnemyInfo(5, "Jet",
            "Fast attack jets that strafe your bunker. Hard to hit — " +
            "lead your shots or use missiles."),
        new EnemyInfo(6, "Attack Helicopter",
            "Cobra gunships that hover and fire. Use missiles or " +
            "sustained cannon fire."),
        new EnemyInfo(7, "Transport Helicopter",
            "CH-53 helicopters that deliver troops. Shoot them down " +
            "before they drop their cargo."),
        new EnemyInfo(8, "C-130",
            "Cargo plane that flies overhead. A bonus target — " +
            "destroying it yields extra points."),
    };
}

/// <summary>Keyboard and mouse controls for BeachHead 2000.</summary>
public sealed record ControlInfo(string Key, string Action)
{
    public static readonly IReadOnlyList<ControlInfo> Controls = new[]
    {
        new ControlInfo("Left Mouse", "Fire bullets (machine gun)"),
        new ControlInfo("Right Mouse", "Fire projectiles (cannon)"),
        new ControlInfo("Spacebar", "Fire missiles"),
        new ControlInfo("Mouse Move", "Aim turret (left/right = traverse, up/down = elevation)"),
        new ControlInfo("1 / 2 / 3 / 4", "Change screen resolution (higher = slower but sharper)"),
        new ControlInfo("+ / -", "Increase / decrease mouse sensitivity (X-axis)"),
        new ControlInfo("I", "Invert mouse Y-axis"),
        new ControlInfo("F", "Toggle FPS / score display"),
        new ControlInfo("L", "Toggle frame limiter (caps at 30-35 fps)"),
        new ControlInfo(". / >", "Dial in processor clock rate for FPS calibration"),
    };
}

/// <summary>
/// Confirmed facts about BeachHead 2000 (Digital Fusion / WizardWorks, 2000).
/// All values are derived from the shipped level files, the game's readme, and
/// live observation of the running game.
/// </summary>
public static class GameFacts
{
    /// <summary>The process name of the actual game executable (not the launcher).</summary>
    public const string ProcessName = "Bh";

    /// <summary>Image base of the 32-bit x86 exe (no ASLR — a 2000-era build).</summary>
    public const uint ImageBase = 0x00400000;

    /// <summary>Total number of shipped level files (Level_00 through Level_60).</summary>
    public const int LevelCount = 61;

    /// <summary>First level index.</summary>
    public const int FirstLevel = 0;

    /// <summary>Last level index.</summary>
    public const int LastLevel = 60;

    /// <summary>Aggression range (1 = easiest, 9 = hardest), from level file comments.</summary>
    public const int AggressionMin = 1;
    public const int AggressionMax = 9;

    /// <summary>Default starting health (the player's bunker condition, shown as a bar).</summary>
    public const int DefaultHealth = 100;

    /// <summary>
    /// The level files live in the <c>beachhead\</c> subdirectory of the game install.
    /// The Steam Gold Edition installs to folder <c>509610</c>.
    /// </summary>
    public const string LevelFilePattern = "Level_{0:00}";

    /// <summary>The Steam "BeachHead Gold Edition" app folder under <c>steamapps\common</c>.</summary>
    public const string SteamAppFolder = "509610";

    /// <summary>
    /// The Steam Gold Edition install directory under <c>steamapps\common</c>; the game ships
    /// at <c>steamapps\common\BeachHead Gold Edition\509610</c> (the Gold Edition bundles several
    /// BeachHead titles, each in its own numbered subfolder).
    /// </summary>
    public const string SteamInstallFolder = "BeachHead Gold Edition";

    /// <summary>The subdirectory of the game install that holds the <c>Level_00</c>…<c>Level_60</c> files.</summary>
    public const string LevelSubdirectory = "beachhead";

    /// <summary>
    /// Enemy types that appear in the level file's Object sections, in the order
    /// the game expects them. <c>ObjectInc</c> entries after each <c>Object</c>
    /// declaration add more units of the same type.
    /// </summary>
    public static readonly IReadOnlyList<string> ObjectTypes = new[]
    {
        "Barge",       // infantry barges
        "Tank",
        "APC",
        "Bomber",
        "Jet",
        "Helicopter1", // attack helicopters
        "Helicopter2", // transport helicopters
        "C130",
    };

    /// <summary>The four aggression axes named in the level file comment.</summary>
    public static readonly IReadOnlyList<string> AggressionAxes = new[]
    {
        "Tank", "Jet", "Heli-Gun", "Heli-Rocket",
    };

    /// <summary>
    /// Suggested starting values for a new level — used by the level editor
    /// when the user clicks "Max Ammo" or similar convenience buttons.
    /// </summary>
    public const int MaxBullets = 999;
    public const int MaxProjectiles = 99;
    public const int MaxMissiles = 99;
}
