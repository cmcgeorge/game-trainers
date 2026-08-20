namespace BardsTaleTrilogyTrainer.Game;

/// <summary>Which of the three games is loaded (<c>BardsTale.GameChapter</c>).</summary>
public enum GameChapter
{
    None = -1,
    TalesOfTheUnknown = 0,
    DestinyKnight = 1,
    ThiefOfFate = 2,
}

/// <summary>Compass heading of the party (<c>BardsTale.Facing</c>).</summary>
public enum Facing
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

/// <summary>
/// How a teleport presents itself (<c>BardsTale.TeleportType</c>): no transition, the
/// dimensional-travel effect, or a fade out/in.
/// </summary>
public enum TeleportType
{
    Quiet = 0,
    Dimensional = 1,
    Fade = 2,
}

/// <summary>
/// Field offsets for the position, map and teleport objects of the remaster, taken from the
/// game's own IL2CPP metadata (<c>global-metadata.dat</c> field tables cross-referenced with
/// the field-offset table in <c>GameAssembly.dll</c>) and confirmed against the compiled code.
///
/// <para>All offsets are from the object base, i.e. they include the 16-byte IL2CPP header.
/// See <c>docs/ReverseEngineering.md</c> for how each was recovered.</para>
/// </summary>
public static class MapFormat
{
    // --- BardsTale.Player (instance size 0x168) ---------------------------------
    /// <summary>[Confirmed] <c>m_map</c> — the loaded <c>GameMap</c>.</summary>
    public const int PlayerMap = 0x18;

    /// <summary>[Confirmed] <c>m_queueTeleport</c> — the pending <c>TeleportTarget</c>, polled every state tick.</summary>
    public const int PlayerQueueTeleport = 0x68;

    /// <summary>[Confirmed] <c>m_facing</c> — a <see cref="Facing"/>.</summary>
    public const int PlayerFacing = 0xE8;

    /// <summary>[Confirmed] <c>m_gridX</c> — party column on the current map.</summary>
    public const int PlayerGridX = 0xEC;

    /// <summary>[Confirmed] <c>m_gridZ</c> — party row on the current map (north is +Z).</summary>
    public const int PlayerGridZ = 0xF0;

    /// <summary>[Confirmed] <c>m_roomX</c> — column inside the current city room, when in one.</summary>
    public const int PlayerRoomX = 0x100;

    /// <summary>[Confirmed] <c>m_roomZ</c>.</summary>
    public const int PlayerRoomZ = 0x104;

    /// <summary>[Confirmed] <c>m_prevX</c> — the cell stepped out of; used to bounce the party back.</summary>
    public const int PlayerPrevX = 0x108;

    /// <summary>[Confirmed] <c>m_prevZ</c>.</summary>
    public const int PlayerPrevZ = 0x10C;

    /// <summary>[Confirmed] <c>m_moving</c>.</summary>
    public const int PlayerMoving = 0x130;

    /// <summary>[Confirmed] <c>m_turning</c>.</summary>
    public const int PlayerTurning = 0x131;

    // --- BardsTale.GameMap (instance size 0x1F0) --------------------------------
    /// <summary>[Confirmed] <c>m_name</c> — the map's internal name as a managed string.</summary>
    public const int GameMapName = 0xB8;

    /// <summary>[Confirmed] <c>m_wrapAroundEnabled</c>.</summary>
    public const int GameMapWrapAround = 0xC1;

    /// <summary>[Confirmed] <c>m_isTower</c>.</summary>
    public const int GameMapIsTower = 0xC3;

    /// <summary>[Confirmed] <c>m_isOutside</c>.</summary>
    public const int GameMapIsOutside = 0xC4;

    /// <summary>[Confirmed] <c>m_isWilderness</c>.</summary>
    public const int GameMapIsWilderness = 0xC5;

    /// <summary>[Confirmed] <c>m_width</c>.</summary>
    public const int GameMapWidth = 0xC8;

    /// <summary>[Confirmed] <c>m_height</c>.</summary>
    public const int GameMapHeight = 0xCC;

    /// <summary>[Confirmed] <c>m_level</c> — the floor number within a multi-level area.</summary>
    public const int GameMapLevel = 0x118;

    /// <summary>[Confirmed] <c>m_isDungeonMap</c> — selects the dungeon vs city map array.</summary>
    public const int GameMapIsDungeon = 0x198;

    /// <summary>[Confirmed] <c>m_mapIdx</c> — index into that array; compared by <c>Player.LoadMap</c>.</summary>
    public const int GameMapIndex = 0x19C;

    /// <summary>[Confirmed] <c>m_desc</c> — the <c>MapDescription</c> this map was built from.</summary>
    public const int GameMapDescription = 0x1A0;

    // --- BardsTale.MapDescription (instance size 0x108) -------------------------
    /// <summary>[Confirmed] <c>m_name</c>.</summary>
    public const int MapDescName = 0x10;

    /// <summary>[Confirmed] <c>m_width</c> (0 for city maps — their size comes from the map file).</summary>
    public const int MapDescWidth = 0xD0;

    /// <summary>[Confirmed] <c>m_height</c>.</summary>
    public const int MapDescHeight = 0xD4;

    /// <summary>[Confirmed] <c>level</c>.</summary>
    public const int MapDescLevel = 0xE8;

    /// <summary>[Confirmed] <c>m_entryX</c>.</summary>
    public const int MapDescEntryX = 0xFC;

    /// <summary>[Confirmed] <c>m_entryZ</c>.</summary>
    public const int MapDescEntryZ = 0x100;

    // --- BardsTale.GlobalMaps ---------------------------------------------------
    /// <summary>[Confirmed] static <c>Instance</c>, first field of the static block.</summary>
    public const int GlobalMapsInstanceStatic = 0x00;

    /// <summary>[Confirmed] static <c>m_gameChapter</c> — a <see cref="GameChapter"/>.</summary>
    public const int GlobalMapsChapterStatic = 0x08;

    /// <summary>[Confirmed] <c>m_cityMaps</c> — <c>MapDescription[]</c>.</summary>
    public const int GlobalMapsCityMaps = 0x18;

    /// <summary>[Confirmed] <c>m_dungeonMaps</c> — <c>MapDescription[]</c>.</summary>
    public const int GlobalMapsDungeonMaps = 0x20;

    /// <summary>[Confirmed] <c>m_newGameLocation</c> — a live <c>TeleportTarget</c> instance.</summary>
    public const int GlobalMapsNewGameLocation = 0x68;

    /// <summary>[Confirmed] <c>m_dreamSpellTargets</c> — <c>DreamSpellTarget[]</c> (BT2 only).</summary>
    public const int GlobalMapsDreamSpellTargets = 0x70;

    // --- BardsTale.TeleportTarget (instance size 0x40) --------------------------
    /// <summary>[Confirmed] <c>m_isValid</c> — the game only acts on the queue when this is set.</summary>
    public const int TeleportIsValid = 0x10;

    /// <summary>[Confirmed] <c>m_isDungeon</c> — chooses which map array <c>m_map</c> indexes.</summary>
    public const int TeleportIsDungeon = 0x11;

    /// <summary>[Confirmed] <c>m_doJournal</c> — whether to record the jump in the journal.</summary>
    public const int TeleportDoJournal = 0x12;

    /// <summary>[Confirmed] <c>m_map</c> — destination map index.</summary>
    public const int TeleportMap = 0x14;

    /// <summary>[Confirmed] <c>m_x</c>.</summary>
    public const int TeleportX = 0x18;

    /// <summary>[Confirmed] <c>m_z</c>.</summary>
    public const int TeleportZ = 0x1C;

    /// <summary>[Confirmed] <c>m_facing</c>.</summary>
    public const int TeleportFacing = 0x20;

    /// <summary>[Confirmed] <c>m_mapWidth</c>.</summary>
    public const int TeleportMapWidth = 0x24;

    /// <summary>[Confirmed] <c>m_mapHeight</c>.</summary>
    public const int TeleportMapHeight = 0x28;

    /// <summary>[Confirmed] <c>m_teleportType</c> — a <see cref="BardsTaleTrilogyTrainer.Game.TeleportType"/>.</summary>
    public const int TeleportKind = 0x2C;

    /// <summary>[Confirmed] <c>m_preDelay</c> — seconds to wait before the jump.</summary>
    public const int TeleportPreDelay = 0x30;

    /// <summary>[Confirmed] <c>m_teleportDone</c> — set by the game once it has consumed the queue.</summary>
    public const int TeleportDone = 0x34;

    /// <summary>[Confirmed] <c>m_postJournal</c> — managed string shown afterwards; null is fine.</summary>
    public const int TeleportPostJournal = 0x38;

    /// <summary>Bytes to allocate for a fabricated <c>TeleportTarget</c>.</summary>
    public const int TeleportTargetSize = 0x40;

    // --- BardsTale.DreamSpellTarget (instance size 0x28) ------------------------
    public const int DreamTargetName = 0x10;
    public const int DreamTargetMap = 0x18;
    public const int DreamTargetX = 0x1C;
    public const int DreamTargetZ = 0x20;
    public const int DreamTargetFacing = 0x24;

    // --- helpers ----------------------------------------------------------------
    public static string FacingName(int facing) => facing switch
    {
        0 => "North",
        1 => "East",
        2 => "South",
        3 => "West",
        _ => $"?({facing})",
    };

    /// <summary>Unit step for one pace in the given heading, in map coordinates.</summary>
    public static (int Dx, int Dz) Step(Facing facing) => facing switch
    {
        Facing.North => (0, 1),
        Facing.East => (1, 0),
        Facing.South => (0, -1),
        _ => (-1, 0),
    };
}
