namespace LegendOfGrimrock1Trainer.Game;

/// <summary>
/// Everything the trainer knows about where Legend of Grimrock keeps its state.
///
/// The list is short on purpose. Because the game is Lua all the way down, "the layout" is almost
/// entirely a set of <i>key names</i> rather than offsets: <c>_G.party.champions[i].stats.health.value</c>
/// is stable across builds in a way a byte offset never is, and the VM's own hash tables do the
/// address arithmetic. The single module-relative number below is a shortcut, not a dependency —
/// <see cref="GameLocator"/> finds the same VM by structural scan when it is wrong or missing.
/// </summary>
public static class GrimrockLayout
{
    // --- the one module-relative constant ---------------------------------------------------------

    /// <summary>
    /// RVA of the <c>.data</c> word that holds the process-wide <c>lua_State *</c> (VA 0x00588AB8 at
    /// the preferred image base; Grimrock sets DYNAMICBASE, so the trainer adds the module base the
    /// OS reports rather than assuming 0x00400000).
    ///
    /// Recovered in Ghidra: the slot has exactly one cross-reference, a WRITE at 0x0040BB75 inside
    /// the function that registers the engine's whole C API with Lua (the caller of that function
    /// also calls the exported <c>luaL_newstate</c>). So it is written once at start-up and never
    /// reassigned, which makes it a sound fast path — but it is still only ever <i>believed</i> after
    /// the same structural validation the scan result goes through.
    /// </summary>
    public const uint LuaStateSlotRva = 0x00188AB8;

    // --- Lua globals the trainer navigates from ---------------------------------------------------

    /// <summary>Self-reference every Lua globals table carries; used to prove a table really is <c>_G</c>.</summary>
    public const string GlobalsSelfKey = "_G";

    /// <summary>Lua's version global. LuaJIT 2.0 reports the language version, not its own.</summary>
    public const string VersionKey = "_VERSION";

    /// <summary>Value <see cref="VersionKey"/> must hold.</summary>
    public const string ExpectedLuaVersion = "Lua 5.1";

    /// <summary>The live party instance. Absent (or not a table) whenever no game is loaded.</summary>
    public const string PartyKey = "party";

    /// <summary>The loaded dungeon: level maps, item archetypes, spell definitions.</summary>
    public const string DungeonKey = "dungeon";

    /// <summary>The in-game mode object; its <c>paused</c> flag distinguishes play from a menu.</summary>
    public const string GameModeKey = "gameMode";

    /// <summary>The engine's settings table, including <c>gameVersion</c> and <c>difficulty</c>.</summary>
    public const string ConfigKey = "config";

    /// <summary>Class tables that must exist in any Grimrock Lua state, loaded game or not.</summary>
    public static readonly string[] EngineClassKeys = { "Champion", "Party", "Dungeon", "Map", "Condition", "Skill" };

    // --- party fields -----------------------------------------------------------------------------

    /// <summary>Array of the four champions, 1-based.</summary>
    public const string PartyChampionsKey = "champions";

    /// <summary>Dungeon level the party is standing on (1-based).</summary>
    public const string PartyLevelKey = "level";

    /// <summary>Party tile X, 0-based.</summary>
    public const string PartyXKey = "x";

    /// <summary>Party tile Y, 0-based.</summary>
    public const string PartyYKey = "y";

    /// <summary>Facing: 0 north, 1 east, 2 south, 3 west.</summary>
    public const string PartyFacingKey = "facing";

    /// <summary>The <c>Map</c> the party is currently on.</summary>
    public const string PartyMapKey = "map";

    /// <summary>Run statistics (play time, monsters killed, secrets found, …).</summary>
    public const string PartyStatisticsKey = "statistics";

    // --- champion fields --------------------------------------------------------------------------

    /// <summary>Champion display name.</summary>
    public const string ChampionNameKey = "name";

    /// <summary>Whether the slot holds a living, active champion.</summary>
    public const string ChampionEnabledKey = "enabled";

    /// <summary>Position in the party, 1..4 (1 and 2 are the front row).</summary>
    public const string ChampionOrdinalKey = "ordinal";

    /// <summary>Food, 0..1000.</summary>
    public const string ChampionFoodKey = "food";

    /// <summary>Unspent skill points.</summary>
    public const string ChampionSkillPointsKey = "skillPoints";

    /// <summary>Map of stat name to a <c>{ name, value, max }</c> table.</summary>
    public const string ChampionStatsKey = "stats";

    /// <summary>Map of condition name to a <c>{ name, value, … }</c> table.</summary>
    public const string ChampionConditionsKey = "conditions";

    /// <summary>Array of <c>{ name, level }</c> tables, one per trained skill.</summary>
    public const string ChampionSkillsKey = "skills";

    /// <summary>Map of talent/trait name to its definition table.</summary>
    public const string ChampionTalentsKey = "talents";

    /// <summary>The champion's class instance: name, level, exp, nextLevel.</summary>
    public const string ChampionClassKey = "class";

    /// <summary>The champion's race instance: name, base attributes, skill points, food rate.</summary>
    public const string ChampionRaceKey = "race";

    /// <summary>Male or female; only affects portraits and text.</summary>
    public const string ChampionSexKey = "sex";

    // --- stat / condition / skill / class sub-fields -----------------------------------------------

    /// <summary>Current value of a stat or condition.</summary>
    public const string ValueKey = "value";

    /// <summary>Cap of a stat. For health and energy this is the bar's maximum.</summary>
    public const string MaxKey = "max";

    /// <summary>Internal name of a stat, condition, skill or spell.</summary>
    public const string NameKey = "name";

    /// <summary>Human-readable name the game shows for a condition, skill or statistic.</summary>
    public const string UiNameKey = "uiName";

    /// <summary>Remaining duration of a condition, in seconds.</summary>
    public const string TimerKey = "timer";

    /// <summary>Trained level of a skill.</summary>
    public const string LevelKey = "level";

    /// <summary>Accumulated experience on a class instance.</summary>
    public const string ExpKey = "exp";

    /// <summary>Experience the class instance needs for its next level.</summary>
    public const string NextLevelKey = "nextLevel";

    // --- map fields -------------------------------------------------------------------------------

    /// <summary>Level maps of the loaded dungeon, 1-based.</summary>
    public const string DungeonMapsKey = "maps";

    /// <summary>Per-tile bitmask array; see <see cref="CellBits"/>.</summary>
    public const string MapCellsKey = "cells";

    /// <summary>Map width in tiles.</summary>
    public const string MapWidthKey = "width";

    /// <summary>Map height in tiles.</summary>
    public const string MapHeightKey = "height";

    /// <summary>Display name of a level, e.g. "Into the Dark".</summary>
    public const string MapNameKey = "name";

    /// <summary>Whether the party has ever set foot on this level.</summary>
    public const string MapVisitedKey = "visited";

    /// <summary>
    /// Lua index of tile <paramref name="x"/>,<paramref name="y"/> in a map's <c>cells</c> array, or
    /// -1 when the arithmetic would not fit an <see cref="int"/>.
    ///
    /// Confirmed live: the party standing at (2, 8) on a 32-wide map carried the
    /// <see cref="CellBits.DynamicObstacle"/> bit at <c>cells[8 * 32 + 2 + 1]</c>. Computed in
    /// <see cref="long"/> because <c>width</c> comes from the game, and a torn read of it would
    /// otherwise wrap the product into a plausible-looking index for a completely different tile.
    /// </summary>
    public static int CellIndex(int x, int y, int width)
    {
        long index = (long)y * width + x + 1;
        return index is < 0 or > int.MaxValue ? -1 : (int)index;
    }

    /// <summary>
    /// Bits packed into each entry of a map's <c>cells</c> array, from the game's own
    /// <c>CellBits</c> global. Only the ones this trainer reads or writes are listed.
    /// </summary>
    public static class CellBits
    {
        /// <summary>Tile is solid rock or a wall.</summary>
        public const long Wall = 1;

        /// <summary>Tile holds a static obstacle.</summary>
        public const long Obstacle = 2;

        /// <summary>Tile is occupied by a moving body — the party sets this on the tile it stands on.</summary>
        public const long DynamicObstacle = 4;

        /// <summary>Tile is a pit.</summary>
        public const long Pit = 8;

        /// <summary>Tile carries a pressure plate.</summary>
        public const long Pad = 32;

        /// <summary>Automap: floor of this tile has been seen.</summary>
        public const long MapFloor = 2097152;

        /// <summary>Automap: north wall has been seen.</summary>
        public const long MapWallNorth = 4194304;

        /// <summary>Automap: east wall has been seen.</summary>
        public const long MapWallEast = 8388608;

        /// <summary>Automap: south wall has been seen.</summary>
        public const long MapWallSouth = 16777216;

        /// <summary>Automap: west wall has been seen.</summary>
        public const long MapWallWest = 33554432;

        /// <summary>Every automap bit at once — what "reveal this level" sets on a walkable tile.</summary>
        public const long MapAll = MapFloor | MapWallNorth | MapWallEast | MapWallSouth | MapWallWest;

        /// <summary>Bits that make a tile impassable to the party.</summary>
        public const long Blocking = Wall | Obstacle;
    }
}
