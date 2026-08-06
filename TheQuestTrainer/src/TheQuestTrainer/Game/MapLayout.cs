namespace TheQuestTrainer.Game;

/// <summary>
/// Where The Quest keeps the player's position, the map under their feet and the world that map
/// belongs to.
///
/// None of this is in the character record. The record is one member of the engine object; the
/// position lives on two <i>other</i> objects the engine object points at:
///
/// <code>
/// engine  = record - RecordInEngine
/// manager = [engine + EngineManager]     the SEngineManager — the live game
/// world   = [manager + World]            the SWorld the player is in ("Freymore")
/// map     = [manager + Map]              the SMap they are standing on ("Port of Mithria")
/// </code>
///
/// <b>The one thing to understand is the window.</b> The engine does not address tiles by their
/// place on the map. It keeps a square scratch grid — <see cref="WindowSize"/> tiles on a side,
/// which the game computes as <c>2 × drawDistance + 21</c> at startup — and loads the map (outdoors,
/// a three-by-three block of maps) into it. The player's position, <see cref="PlayerX"/> and
/// <see cref="PlayerY"/>, is an index into <i>that</i> grid, not into the map. Converting the two is
/// one subtraction, and which one depends on a flag: a map with <see cref="FlagOffsetByBorder"/> set
/// is laid into the window <see cref="WindowBorder"/> tiles in from the edge, and a map without it
/// starts at the window's origin. The game's own local-to-window helper is exactly that test.
///
/// Outdoors the world is a grid of 21×21-tile maps whose ids spell out the cell — <c>base_s0804</c>
/// is column 8, row 4 of the world named by the <c>base_s</c> prefix — and the game maintains a
/// world-absolute tile position at <see cref="WorldTileX"/>/<see cref="WorldTileY"/> from those two
/// numbers. Interiors are standalone 35×35 maps with names instead of cells, and the world-absolute
/// pair means nothing while the player is inside one.
///
/// Offsets were read out of <c>TheQuest.exe</c> v1.9.10 with Ghidra and then confirmed against a
/// live session; <c>docs/ReverseEngineering.md</c> §17 derives each one.
/// </summary>
public static class MapLayout
{
    // ---- from the engine object --------------------------------------------------------------

    /// <summary>Pointer to the <c>SEngineManager</c> — the object that owns the live game.</summary>
    public const uint EngineManager = 0x098;

    /// <summary>
    /// How far in from the window's edge a map with <see cref="FlagOffsetByBorder"/> is laid. The
    /// game sets it to the configured draw distance (<c>drawDistance</c> in <c>config.ini</c>, 14 in
    /// a default install), which is why it must be read rather than assumed.
    /// </summary>
    public const uint WindowBorder = 0x44E8;

    /// <summary>
    /// Side of the square tile window, which the game computes as
    /// <c>WindowBorder × 2 + <see cref="GridMapTiles"/></c> once at startup.
    /// </summary>
    public const uint WindowSize = WindowBorder + 4;

    // ---- from the engine manager ---------------------------------------------------------------

    /// <summary>
    /// Which way the player faces, in degrees, measured anticlockwise from north: 0 north, 90 west,
    /// 180 south, 270 east. Turning right walks it backwards through those four.
    /// </summary>
    public const uint Facing = 0x1570;

    /// <summary>
    /// The player's column in the tile window. <b>This is the field a teleport writes</b> — the
    /// engine reads it every frame, so a write moves the player, the camera and the automap at once.
    /// </summary>
    public const uint PlayerX = 0x158C;

    /// <summary>The player's row in the same window.</summary>
    public const uint PlayerY = PlayerX + 4;

    /// <summary>
    /// The tile window itself: <see cref="WindowSize"/> squared entries of
    /// <see cref="WindowTileBytes"/>. Read for nothing here — it is recorded so the next person
    /// knows what the pointer beside the flags is.
    /// </summary>
    public const uint TileWindow = 0x21BC;

    /// <summary>Bytes per entry in <see cref="TileWindow"/>.</summary>
    public const int WindowTileBytes = 0x42;

    /// <summary>
    /// Non-zero while the player is on the outdoor grid, in which case
    /// <see cref="NeighbourMaps"/> holds the loaded three-by-three block; zero inside a standalone
    /// map, where only <see cref="Map"/> is loaded.
    /// </summary>
    public const uint Outdoors = 0x21C4;

    /// <summary>Pointer to the world the player is in.</summary>
    public const uint World = 0x21C8;

    /// <summary>Pointer to the map the player is standing on. Also mirrored at <see cref="WorldCurrentMap"/>.</summary>
    public const uint Map = World + 4;

    /// <summary>
    /// The three-by-three block of maps loaded around the player, row-major from the north-west, so
    /// index 4 is the map they are on. Empty slots are null at the edges of the world.
    /// </summary>
    public const uint NeighbourMaps = 0x21D0;

    /// <summary>Entries in <see cref="NeighbourMaps"/>.</summary>
    public const int NeighbourCount = 9;

    /// <summary>Index of the player's own map within <see cref="NeighbourMaps"/>.</summary>
    public const int NeighbourCentre = 4;

    // ---- the world -----------------------------------------------------------------------------

    /// <summary>Back-pointer to the engine object. The cheapest thing to validate a world against.</summary>
    public const uint WorldEngine = 0x00;

    /// <summary>MSVC <c>std::string</c>: the world's name as the game would say it, e.g. <c>Freymore</c>.</summary>
    public const uint WorldName = 0x08;

    /// <summary>
    /// <c>std::string</c>: the resource pack the world's art lives in, e.g. <c>base</c>. The world
    /// map picture is <c>worlds/&lt;pack&gt;/-WORLDMAP-.dds</c> inside the matching <c>.pak</c>.
    /// </summary>
    public const uint WorldPack = WorldName + StdString.Bytes;

    /// <summary><c>std::string</c>: the prefix every id in this world carries, e.g. <c>base_</c>.</summary>
    public const uint WorldIdPrefix = WorldPack + StdString.Bytes;

    /// <summary>
    /// <c>std::string</c>: the database the world was loaded from, e.g. <c>TheQuestBase</c>. Four
    /// bytes past where the previous string ends — an unidentified pointer sits between them.
    /// </summary>
    public const uint WorldDatabase = WorldIdPrefix + StdString.Bytes + 4;

    /// <summary>First pointer of the world's <c>std::vector&lt;SMap*&gt;</c> — every map it holds.</summary>
    public const uint WorldMapsBegin = 0x74;

    /// <summary>One past the last element.</summary>
    public const uint WorldMapsEnd = WorldMapsBegin + 4;

    /// <summary>
    /// The most maps the reader will walk. Freymore has 239 and the expansion fewer; a length beyond
    /// this means the two pointers are not a vector.
    /// </summary>
    public const int MaxMaps = 4096;

    /// <summary>The map the player is on, kept in step with <see cref="Map"/>.</summary>
    public const uint WorldCurrentMap = 0x8C;

    /// <summary>
    /// World-absolute tile column, i.e. <c>(cell column - 1) × <see cref="GridMapTiles"/> + local
    /// column</c>. The engine recomputes it from the current map's id whenever the player moves, and
    /// only while they are on the outdoor grid, so it is read for display and never written.
    /// </summary>
    public const uint WorldTileX = 0x90;

    /// <summary>World-absolute tile row.</summary>
    public const uint WorldTileY = WorldTileX + 4;

    /// <summary>
    /// <c>std::string</c>: the prefix the outdoor grid's map ids are built from, e.g. <c>base_s</c>.
    /// A map id that is this plus four digits is a grid map; anything else is an interior.
    /// </summary>
    public const uint WorldGridPrefix = 0xA0;

    /// <summary>
    /// <c>std::string</c>: the resource id of the world's map picture, e.g. <c>base_-WORLDMAP-</c>
    /// — the pack name, an underscore, and the file's stem inside <c>worlds/&lt;pack&gt;/</c>.
    /// </summary>
    public const uint WorldMapPicture = WorldGridPrefix + StdString.Bytes + 4;

    /// <summary>How much of the world object the reader snapshots in one go.</summary>
    public const int WorldBytes = 0x100;

    // ---- a map ---------------------------------------------------------------------------------

    /// <summary>Back-pointer to the engine object.</summary>
    public const uint MapEngine = 0x00;

    /// <summary>Back-pointer to the world. Together with <see cref="MapEngine"/> this identifies a map.</summary>
    public const uint MapWorld = MapEngine + 4;

    /// <summary>Pointer to the map's internal id, e.g. <c>base_s0804</c>. A plain C string.</summary>
    public const uint MapId = 0x0C;

    /// <summary>Pointer to the name the game shows, e.g. <c>Port of Mithria</c>.</summary>
    public const uint MapName = MapId + 4;

    /// <summary>Width in tiles: 21 for a grid map, 35 for an interior.</summary>
    public const uint MapWidth = 0x2C;

    /// <summary>Height in tiles.</summary>
    public const uint MapHeight = MapWidth + 4;

    /// <summary>Map flags, a word. See the <c>Flag*</c> constants.</summary>
    public const uint MapFlags = 0x40;

    /// <summary>How much of a map object the reader snapshots in one go.</summary>
    public const int MapBytes = 0x44;

    /// <summary>The game refuses to Mark a position on a map with this set.</summary>
    public const ushort FlagMarkDenied = 0x0008;

    /// <summary>
    /// The map is laid into the tile window <see cref="WindowBorder"/> tiles in from the edge rather
    /// than at its origin. Set on every outdoor grid map and clear on every interior — the game's
    /// own local-to-window helper branches on exactly this bit.
    /// </summary>
    public const ushort FlagOffsetByBorder = 0x0080;

    /// <summary>The map is somewhere Recall can bring the player back to.</summary>
    public const ushort FlagRecallTarget = 0x0200;

    /// <summary>The game refuses to cast Teleport or Recall on a map with this set.</summary>
    public const ushort FlagTeleportDenied = 0x0400;

    // ---- the outdoor grid ------------------------------------------------------------------------

    /// <summary>Tiles along one side of an outdoor grid map. The whole outdoor world is a grid of these.</summary>
    public const int GridMapTiles = 21;

    /// <summary>Digits of cell column and cell row an outdoor map id ends with.</summary>
    public const int GridIdDigits = 4;

    /// <summary>Largest map side the reader will accept, so garbage cannot ask for a huge grid.</summary>
    public const int MaxMapTiles = 255;

    /// <summary>Address of the map at <paramref name="index"/> in the world's vector.</summary>
    public static uint MapSlot(uint begin, int index) => begin + (uint)index * 4;

    /// <summary>Address of neighbour slot <paramref name="index"/> on the engine manager.</summary>
    public static uint NeighbourSlot(uint manager, int index) => manager + NeighbourMaps + (uint)index * 4;

    /// <summary>
    /// Splits an outdoor map id into its one-based cell, or null when it is not one — an interior's
    /// id has no digits on the end and does not carry a place in the world.
    /// </summary>
    /// <param name="id">The map's internal id, e.g. <c>base_s0804</c>.</param>
    /// <param name="gridPrefix">The world's grid prefix, e.g. <c>base_s</c>.</param>
    public static (int Column, int Row)? CellFromId(string? id, string? gridPrefix)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(gridPrefix)) return null;
        if (id.Length != gridPrefix.Length + GridIdDigits) return null;
        if (!id.StartsWith(gridPrefix, StringComparison.Ordinal)) return null;

        int at = gridPrefix.Length;
        for (int i = 0; i < GridIdDigits; i++)
            if (id[at + i] is < '0' or > '9') return null;

        int column = (id[at] - '0') * 10 + (id[at + 1] - '0');
        int row = (id[at + 2] - '0') * 10 + (id[at + 3] - '0');

        // The game's own arithmetic is "the two digits, minus one" — cell 0000 does not exist and
        // would put the map's origin at a negative tile.
        return column == 0 || row == 0 ? null : (column, row);
    }

    /// <summary>The world-absolute tile the north-west corner of one-based cell <paramref name="column"/> sits at.</summary>
    public static int CellOriginTile(int column) => (column - 1) * GridMapTiles;
}
