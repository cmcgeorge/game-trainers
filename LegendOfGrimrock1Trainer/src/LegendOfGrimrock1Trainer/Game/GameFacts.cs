namespace LegendOfGrimrock1Trainer.Game;

/// <summary>
/// Facts about the target that hold regardless of what is happening inside it: which process to
/// attach to, which build the notes were taken against, and the handful of game rules the UI clamps
/// against.
///
/// Note what is <i>not</i> here: no address of any game object. Legend of Grimrock keeps its whole
/// gameplay model in Lua tables, so the trainer reaches every value by name through the VM. The one
/// module-relative constant in the project is <see cref="GrimrockLayout.LuaStateSlotRva"/>, and even
/// that is only a shortcut — <see cref="GameLocator"/>'s scan finds the VM without it.
/// </summary>
public static class GameFacts
{
    /// <summary><see cref="System.Diagnostics.Process.ProcessName"/> of the game (no ".exe").</summary>
    public const string ProcessName = "grimrock";

    /// <summary>Substrings that sort a process to the top of the picker. Cosmetic only.</summary>
    public static readonly string[] TargetHints = { "grimrock" };

    /// <summary>Preferred image base from the optional header. The exe sets DYNAMICBASE, so it is relocated.</summary>
    public const uint PreferredImageBase = 0x00400000;

    // --- build fingerprint ------------------------------------------------------------------------
    // Reported, never used as a gate. What decides whether an address is trusted is the structural
    // validation in GameLocator, which is strictly stronger than a timestamp comparison: a rebuild
    // that left LuaJIT's layout alone still works, and one that moved it fails validation whatever
    // the timestamp says. A mismatch shows in the status bar so the numbers can be distrusted.

    /// <summary>Size on disk of the Steam build these notes were taken against.</summary>
    public const long KnownFileSize = 1_804_800;

    /// <summary>PE <c>TimeDateStamp</c> of that build (2013-02-08 15:04:43 UTC).</summary>
    public const uint KnownTimeDateStamp = 0x5115140B;

    /// <summary>Version string the game itself reports in the Lua global <c>config.gameVersion</c>.</summary>
    public const string KnownGameVersion = "1.3.7";

    /// <summary>Human-readable name of the build the notes came from.</summary>
    public const string KnownBuildName = "Steam / Legend of Grimrock 1.3.7 (2013-02-08)";

    /// <summary>Scripting VM the exe statically links, from its own exported version symbol.</summary>
    public const string LuaJitVersion = "LuaJIT 2.0.0-beta9";

    // --- game rules the UI clamps against ---------------------------------------------------------

    /// <summary>Champion slots in a Grimrock party. Always four; two front, two back.</summary>
    public const int PartySize = 4;

    /// <summary>
    /// Ceiling the trainer applies to a level edit. Grimrock defines no cap of its own: a champion's
    /// level is whatever <c>CharClass:levelUp</c> has counted up to, and the threshold comes from
    /// <c>expForLevel</c> (a <c>math.floor</c> over <c>math.pow</c> with the constants 850, 1.37
    /// and 2 — the level-2 threshold reads 850 XP in a fresh game). This is a UI guard only.
    /// </summary>
    public const int MaxChampionLevel = 99;

    /// <summary>
    /// Ceiling the trainer applies to any stat edit. Not a rule of the game — Grimrock stores stats
    /// as plain doubles and would happily take more — but a value this large already trivialises
    /// combat, and keeping edits inside a sane band avoids overflowing the character sheet's layout.
    /// </summary>
    public const int MaxStatValue = 9999;

    /// <summary>Food is a 0..1000 bar; 0 means starving.</summary>
    public const int MaxFood = 1000;

    /// <summary>
    /// Highest skill level the game's own upgrade tables reach. Grimrock 1 spends one skill point per
    /// level and runs each skill from 0 to 50, with milestone bonuses at listed levels — the top entry
    /// in every <c>Skill.skills[*].upgrades</c> table is level 50 (Iron Body, Armor Master, the four
    /// elemental Masteries, and so on). This is a real rule of the game, not a UI guard.
    /// </summary>
    public const int MaxSkillLevel = 50;

    /// <summary>Dungeon levels in the shipped campaign, "Into the Dark" through "The Cemetery".</summary>
    public const int CampaignLevels = 13;
}
