namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// One area of the trilogy as the remaster ships it: the chapter it belongs to, whether
/// it is a dungeon or a city/wilderness map, its index inside that chapter's map array,
/// its grid size, and the entry/parent links the game uses for stairs.
///
/// <para><see cref="Index"/> together with <see cref="IsDungeon"/> is exactly what
/// <c>Player.LoadMap</c> takes, so it is also what a teleport writes into
/// <c>TeleportTarget.m_map</c> / <c>m_isDungeon</c>.</para>
/// </summary>
public sealed record GameMapInfo(
    GameChapter Chapter,
    bool IsDungeon,
    int Index,
    string Name,
    int Width,
    int Height,
    int Level,
    bool IsTower,
    bool IsWilderness,
    bool IsOutside,
    bool WrapsAround,
    int EntryX,
    int EntryZ,
    int ParentMap,
    int ParentX,
    int ParentZ,
    string Asset)
{
    /// <summary>"Tales of the Unknown" / "The Destiny Knight" / "Thief of Fate".</summary>
    public string ChapterName => MapBook.ChapterName(Chapter);

    /// <summary>Short chapter tag used in list headers ("BT1", "BT2", "BT3").</summary>
    public string ChapterTag => MapBook.ChapterTag(Chapter);

    /// <summary>
    /// Multi-level areas repeat a name with a " Lv&lt;n&gt;" suffix; this is the name without
    /// it, so the picker can group all five floors of Mangar's Tower under one heading.
    /// </summary>
    public string Group
    {
        get
        {
            int i = Name.LastIndexOf(" Lv", StringComparison.Ordinal);
            return i > 0 ? Name[..i] : Name;
        }
    }

    /// <summary>"City", "Wilderness" or "Dungeon" — how the map behaves, for display.</summary>
    public string Kind => IsWilderness ? "Wilderness" : IsDungeon ? "Dungeon" : "City";

    public string Display => $"{Name}  ({Width}\u00D7{Height})";

    /// <summary>Category header for the grouped map picker.</summary>
    public string Category => IsDungeon
        ? $"{ChapterTag} \u2014 Dungeons"
        : $"{ChapterTag} \u2014 Cities \u0026 wilderness";
}

/// <summary>
/// One entry of the BT2 dream spell (ZZGO) destination table. <see cref="Map"/> is a
/// <em>city</em> map index, not a dungeon one: the spell sets the party down at the dungeon's
/// entrance out in the world, which is what <c>OnGotGetDreamSpellDestination</c> feeds to
/// <c>Player.LoadMap</c> with its dungeon flag clear.
/// </summary>
public sealed record DreamSpellTarget(string Name, int Map, int X, int Z, Facing Facing);

/// <summary>
/// Every map in The Bard's Tale Trilogy remaster, extracted from the game's own data: the
/// three <c>BardsTale.GlobalMaps</c> objects (one per chapter, serialised into <c>level3</c>,
/// <c>level4</c> and <c>level5</c>) supply the names, entry points and stair links, and each
/// map's <c>map_*_asc</c> TextAsset supplies the grid size and behaviour flags.
///
/// <para>This is metadata only — names, sizes and indices. The cell-by-cell terrain stays in
/// the player's own installation and is read from it on demand by <see cref="MapArchive"/>,
/// so no game content is redistributed here.</para>
///
/// <para>121 maps in total: 17 in BT1, 33 in BT2, 71 in BT3.</para>
/// </summary>
public static class MapBook
{
    private static GameMapInfo M(GameChapter c, bool dungeon, int idx, string name,
        int w, int h, int level, bool tower, bool wild, bool outside, bool wrap,
        int ex, int ez, int pm, int px, int pz, string asset) =>
        new(c, dungeon, idx, name, w, h, level, tower, wild, outside, wrap, ex, ez, pm, px, pz, asset);

    /// <summary>Every map of all three chapters, in chapter then array order.</summary>
    public static readonly IReadOnlyList<GameMapInfo> Maps = new[]
    {

        // --- BT1: cities & wilderness ---
        M(GameChapter.TalesOfTheUnknown, false,  0, "Skara Brae",               30,  30, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt1_city00_skarabrae_asc"),

        // --- BT1: dungeons ---
        M(GameChapter.TalesOfTheUnknown, true,  0, "Cellars",                  22,  22, 0, false, false, false, true,   0,  0,  0, 28,  5, "map_bt1_dung00_cellars_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  1, "Sewers Lv1",               22,  22, 1, false, false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung01_sewers_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  2, "Sewers Lv2",               22,  22, 2, false, false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung02_sewers_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  3, "Sewers Lv3",               22,  22, 3, false, false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung03_sewers_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  4, "Catacombs Lv1",            22,  22, 0, false, false, false, true,   0,  0,  0, 17, 15, "map_bt1_dung04_catacombs_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  5, "Catacombs Lv2",            22,  22, 1, false, false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung05_catacombs_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  6, "Catacombs Lv3",            22,  22, 2, false, false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung06_catacombs_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  7, "Harkyns Lv1",              22,  22, 0, true,  false, false, true,   0,  0,  0,  5, 24, "map_bt1_dung07_castle_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  8, "Harkyns Lv2",              22,  22, 1, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung08_castle_asc"),
        M(GameChapter.TalesOfTheUnknown, true,  9, "Harkyns Lv3",              22,  22, 2, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung09_castle_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 10, "Kylearans Tower",          22,  22, 0, true,  false, false, true,   0,  0,  0, 27, 27, "map_bt1_dung10_tower_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 11, "Mangars Tower Lv1",        22,  22, 0, true,  false, false, true,   0,  0,  0,  2,  3, "map_bt1_dung11_thetower_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 12, "Mangars Tower Lv2",        22,  22, 1, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung12_thetower_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 13, "Mangars Tower Lv3",        22,  22, 2, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung13_thetower_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 14, "Mangars Tower Lv4",        22,  22, 3, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt1_dung14_thetower_asc"),
        M(GameChapter.TalesOfTheUnknown, true, 15, "Mangars Tower Lv5",        22,  22, 4, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt1_dung15_thetower_asc"),

        // --- BT2: cities & wilderness ---
        M(GameChapter.DestinyKnight, false,  0, "The Forest",               32,  48, 0, false, true,  true,  false,  0,  0,  0,  0,  0, "map_bt2_city00_theforest_asc"),
        M(GameChapter.DestinyKnight, false,  1, "Tangramayne",              16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city01_tangramayne_asc"),
        M(GameChapter.DestinyKnight, false,  2, "Epheseus",                 16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city02_ephesus_asc"),
        M(GameChapter.DestinyKnight, false,  3, "Philippi",                 16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city03_philippi_asc"),
        M(GameChapter.DestinyKnight, false,  4, "Colosse",                  16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city04_colosse_asc"),
        M(GameChapter.DestinyKnight, false,  5, "Corinth",                  16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city05_corinth_asc"),
        M(GameChapter.DestinyKnight, false,  6, "Thessalonica",             16,  16, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt2_city06_thessalonica_asc"),

        // --- BT2: dungeons ---
        M(GameChapter.DestinyKnight, true,  0, "Dark Domain Lv1",          22,  22, 0, false, false, true,  true,   0,  0,  1, 15,  8, "map_bt2_dung00_darkdomain_asc"),
        M(GameChapter.DestinyKnight, true,  1, "Dark Domain Lv2",          22,  22, 1, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung01_darkdomain_asc"),
        M(GameChapter.DestinyKnight, true,  2, "Dark Domain Lv3",          22,  22, 2, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung02_darkdomain_asc"),
        M(GameChapter.DestinyKnight, true,  3, "Dark Domain Lv4",          22,  22, 3, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung03_darkdomain_asc"),
        M(GameChapter.DestinyKnight, true,  4, "The Tombs Lv1",            22,  22, 0, false, false, true,  true,   0,  0,  2,  8,  7, "map_bt2_dung04_thetombs_asc"),
        M(GameChapter.DestinyKnight, true,  5, "The Tombs Lv2",            22,  22, 1, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung05_thetombs_asc"),
        M(GameChapter.DestinyKnight, true,  6, "The Tombs Lv3",            22,  22, 2, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung06_thetombs_asc"),
        M(GameChapter.DestinyKnight, true,  7, "The Castle",               22,  22, 0, true,  false, true,  true,   0,  0,  0, 17, 26, "map_bt2_dung07_thecastle_asc"),
        M(GameChapter.DestinyKnight, true,  8, "The Tower Lv1",            22,  22, 0, true,  false, true,  true,   0,  0,  3, 13,  2, "map_bt2_dung08_thetower_asc"),
        M(GameChapter.DestinyKnight, true,  9, "The Tower Lv2",            22,  22, 1, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung09_thetower_asc"),
        M(GameChapter.DestinyKnight, true, 10, "The Tower Lv3",            22,  22, 2, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung10_thetower_asc"),
        M(GameChapter.DestinyKnight, true, 11, "The Tower Lv4",            22,  22, 3, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung11_thetower_asc"),
        M(GameChapter.DestinyKnight, true, 12, "The Tower Lv5",            22,  22, 4, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung12_thetower_asc"),
        M(GameChapter.DestinyKnight, true, 13, "Maze of Dread Lv1",        22,  22, 0, false, false, true,  true,   0,  0,  6, 11, 14, "map_bt2_dung13_mazeofdread_asc"),
        M(GameChapter.DestinyKnight, true, 14, "Maze of Dread Lv2",        22,  22, 1, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung14_mazeofdread_asc"),
        M(GameChapter.DestinyKnight, true, 15, "Maze of Dread Lv3",        22,  22, 2, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung15_mazeofdread_asc"),
        M(GameChapter.DestinyKnight, true, 16, "Oscon's Fort Lv1",         22,  22, 0, true,  false, true,  true,   0,  0,  5,  8, 13, "map_bt2_dung16_osconsfort_asc"),
        M(GameChapter.DestinyKnight, true, 17, "Oscon's Fort Lv2",         22,  22, 1, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung17_osconsfort_asc"),
        M(GameChapter.DestinyKnight, true, 18, "Oscon's Fort Lv3",         22,  22, 2, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung18_osconsfort_asc"),
        M(GameChapter.DestinyKnight, true, 19, "Oscon's Fort Lv4",         22,  22, 3, true,  false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung19_osconsfort_asc"),
        M(GameChapter.DestinyKnight, true, 20, "Grey Crypt Lv1",           22,  22, 0, false, false, true,  true,   0,  0,  0,  8, 31, "map_bt2_dung20_greycrypt_asc"),
        M(GameChapter.DestinyKnight, true, 21, "Grey Crypt Lv2",           22,  22, 1, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung21_greycrypt_asc"),
        M(GameChapter.DestinyKnight, true, 22, "Destiny Stone Lv1",        22,  22, 0, false, false, true,  true,   0,  0,  4,  2, 13, "map_bt2_dung22_destinystone_asc"),
        M(GameChapter.DestinyKnight, true, 23, "Destiny Stone Lv2",        22,  22, 1, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung23_destinystone_asc"),
        M(GameChapter.DestinyKnight, true, 24, "Destiny Stone Lv3",        22,  22, 2, false, false, true,  true,   0,  0,  0,  0,  0, "map_bt2_dung24_destinystone_asc"),
        M(GameChapter.DestinyKnight, true, 25, "Saradon's Workshop",        8,   8, 0, true,  false, true,  true,   0,  0,  3, 13, 15, "map_bt2_dung25_saradons_asc"),

        // --- BT3: cities & wilderness ---
        M(GameChapter.ThiefOfFate, false,  0, "Wilderness",               32,  32, 0, false, true,  true,  false,  0,  0,  0,  0,  0, "map_bt3_city00_wilderness_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  1, "Skara Brae",               30,  30, 0, false, false, true,  false,  0,  0,  0,  0,  0, "map_bt3_city01_skarabrae_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  2, "Arboria",                  22,  22, 0, false, true,  true,  false,  0,  0,  0,  0,  0, "map_bt3_city02_arboria_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  3, "Cierra Brannia",           16,  16, 0, false, false, false, false,  0,  0,  0,  0,  0, "map_bt3_city03_cierabrannia_asc"),
        M(GameChapter.ThiefOfFate, false,  4, "Gelidia",                  16,  16, 0, false, true,  false, false,  0,  0,  0,  0,  0, "map_bt3_city04_gelidia_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  5, "Lucencia",                 15,  15, 0, false, true,  true,  false,  0,  0,  0,  0,  0, "map_bt3_city05_lucencia_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  6, "Celaria Bree",             16,  16, 0, false, false, false, false,  0,  0,  0,  0,  0, "map_bt3_city06_celariabree_asc"),
        M(GameChapter.ThiefOfFate, false,  7, "Nowhere",                  16,  16, 0, false, true,  true,  false,  0,  0,  0,  0,  0, "map_bt3_city07_nowhere_ex_asc"),
        M(GameChapter.ThiefOfFate, false,  8, "Dark Copse",               11,  11, 0, false, true,  false, false,  0,  0,  0,  0,  0, "map_bt3_city08_darkcopse_asc"),
        M(GameChapter.ThiefOfFate, false,  9, "Black Scar",               16,  16, 0, false, false, false, false,  0,  0,  0,  0,  0, "map_bt3_city09_blackscar_asc"),

        // --- BT3: dungeons ---
        M(GameChapter.ThiefOfFate, true,  0, "Festering Pit Lv1",        15,  15, 0, false, false, false, true,   0,  0,  2, 17,  4, "map_bt3_dung00_festeringpit_asc"),
        M(GameChapter.ThiefOfFate, true,  1, "Festering Pit Lv2",        12,  12, 1, false, false, false, true,   0,  0,  2,  9,  3, "map_bt3_dung01_festeringpit_asc"),
        M(GameChapter.ThiefOfFate, true,  2, "Palace",                   15,  10, 0, false, false, false, true,  14,  4,  2,  5, 17, "map_bt3_dung02_palace_asc"),
        M(GameChapter.ThiefOfFate, true,  3, "Tower Lv1",                 5,   5, 0, true,  false, false, true,   0,  2,  2,  3,  3, "map_bt3_dung03_tower_asc"),
        M(GameChapter.ThiefOfFate, true,  4, "Tower Lv2",                 5,   5, 1, true,  false, false, true,   0,  2,  2,  1,  1, "map_bt3_dung04_tower_asc"),
        M(GameChapter.ThiefOfFate, true,  5, "Tower Lv3",                 5,   5, 2, true,  false, false, true,   0,  2,  2,  1,  1, "map_bt3_dung05_tower_asc"),
        M(GameChapter.ThiefOfFate, true,  6, "Tower Lv4",                 5,   5, 3, true,  false, false, true,   0,  2,  2,  1,  1, "map_bt3_dung06_tower_asc"),
        M(GameChapter.ThiefOfFate, true,  7, "Sacred Grove",             10,  10, 0, false, false, true,  true,   0,  9,  3,  7,  9, "map_bt3_dung07_sacredgrove_asc"),
        M(GameChapter.ThiefOfFate, true,  8, "White Tower Lv1",           5,   5, 0, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung08_whitetower_asc"),
        M(GameChapter.ThiefOfFate, true,  9, "White Tower Lv2",           5,   5, 1, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung09_whitetower_asc"),
        M(GameChapter.ThiefOfFate, true, 10, "White Tower Lv3",           5,   5, 2, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung10_whitetower_asc"),
        M(GameChapter.ThiefOfFate, true, 11, "White Tower Lv4",           5,   5, 3, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung11_whitetower_asc"),
        M(GameChapter.ThiefOfFate, true, 12, "Grey Tower Lv1",            5,   5, 0, true,  false, false, true,   4,  0,  0,  0,  0, "map_bt3_dung12_greytower_asc"),
        M(GameChapter.ThiefOfFate, true, 13, "Grey Tower Lv2",            5,   5, 1, true,  false, false, true,   4,  0,  0,  0,  0, "map_bt3_dung13_greytower_asc"),
        M(GameChapter.ThiefOfFate, true, 14, "Grey Tower Lv3",            5,   5, 2, true,  false, false, true,   4,  0,  0,  0,  0, "map_bt3_dung14_greytower_asc"),
        M(GameChapter.ThiefOfFate, true, 15, "Grey Tower Lv4",            5,   5, 3, true,  false, false, true,   4,  0,  0,  0,  0, "map_bt3_dung15_greytower_asc"),
        M(GameChapter.ThiefOfFate, true, 16, "Black Tower Lv1",           5,   5, 0, true,  false, false, true,   0,  4,  0,  0,  0, "map_bt3_dung16_blacktower_asc"),
        M(GameChapter.ThiefOfFate, true, 17, "Black Tower Lv2",           5,   5, 1, true,  false, false, true,   0,  4,  0,  0,  0, "map_bt3_dung17_blacktower_asc"),
        M(GameChapter.ThiefOfFate, true, 18, "Black Tower Lv3",           5,   5, 2, true,  false, false, true,   0,  4,  0,  0,  0, "map_bt3_dung18_blacktower_asc"),
        M(GameChapter.ThiefOfFate, true, 19, "Black Tower Lv4",           5,   5, 3, true,  false, false, true,   0,  4,  0,  0,  0, "map_bt3_dung19_blacktower_asc"),
        M(GameChapter.ThiefOfFate, true, 20, "Ice Dungeon Lv1",           9,   9, 0, false, false, false, true,   2,  8,  0,  0,  0, "map_bt3_dung20_icedungeon_asc"),
        M(GameChapter.ThiefOfFate, true, 21, "Ice Dungeon Lv2",           5,   5, 1, false, false, false, true,   2,  8,  0,  0,  0, "map_bt3_dung21_icedungeon_asc"),
        M(GameChapter.ThiefOfFate, true, 22, "Ice Keep Lv1",             12,  10, 0, true,  false, false, true,   1,  0,  4, 10,  5, "map_bt3_dung22_icekeep_asc"),
        M(GameChapter.ThiefOfFate, true, 23, "Ice Keep Lv2",             12,  10, 1, true,  false, false, true,   1,  0,  4, 10,  6, "map_bt3_dung23_icekeep_asc"),
        M(GameChapter.ThiefOfFate, true, 24, "Mountain Lv1",             18,  18, 0, true,  false, false, true,   9,  0,  5,  1, 13, "map_bt3_dung24_mountain_asc"),
        M(GameChapter.ThiefOfFate, true, 25, "Mountain Lv2",             11,  11, 1, true,  false, false, true,   9,  0,  5,  1, 10, "map_bt3_dung25_mountain_asc"),
        M(GameChapter.ThiefOfFate, true, 26, "Cyanis Tower Lv1",          7,   7, 0, true,  false, false, true,   0,  0,  5,  3,  3, "map_bt3_dung26_cyanistower_asc"),
        M(GameChapter.ThiefOfFate, true, 27, "Cyanis Tower Lv2",          7,   7, 1, true,  false, false, true,   0,  0,  5,  4,  1, "map_bt3_dung27_cyanistower_asc"),
        M(GameChapter.ThiefOfFate, true, 28, "Cyanis Tower Lv3",          7,   7, 2, true,  false, false, true,   0,  0,  5,  4,  1, "map_bt3_dung28_cyanistower_asc"),
        M(GameChapter.ThiefOfFate, true, 29, "Allirias Tomb Lv1",        13,  17, 0, true,  false, false, true,   0,  6,  5,  1,  7, "map_bt3_dung29_alliriastomb_asc"),
        M(GameChapter.ThiefOfFate, true, 30, "Allirias Tomb Lv2",        13,   9, 1, true,  false, false, true,   0,  6,  0,  0,  0, "map_bt3_dung30_alliriastomb_asc"),
        M(GameChapter.ThiefOfFate, true, 31, "Wasteland",                11,  17, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung31_wasteland_asc"),
        M(GameChapter.ThiefOfFate, true, 32, "Tarmitia",                 12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung32_tarmitia_asc"),
        M(GameChapter.ThiefOfFate, true, 33, "Berlin",                   12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung33_berlin_asc"),
        M(GameChapter.ThiefOfFate, true, 34, "Stalingrad",               12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung34_stalingrad_asc"),
        M(GameChapter.ThiefOfFate, true, 35, "Hiroshima",                12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung35_hiroshima_asc"),
        M(GameChapter.ThiefOfFate, true, 36, "Troy",                     12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung36_troy_asc"),
        M(GameChapter.ThiefOfFate, true, 37, "Rome",                     12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung37_rome_asc"),
        M(GameChapter.ThiefOfFate, true, 38, "Nottingham",               12,  12, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung38_nottingham_asc"),
        M(GameChapter.ThiefOfFate, true, 39, "Kunwang",                  12,  16, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung39_kunwang_asc"),
        M(GameChapter.ThiefOfFate, true, 40, "Catacombs",                13,  13, 0, false, false, false, true,   0,  0,  1, 18, 15, "map_bt3_dung40_catacombs_asc"),
        M(GameChapter.ThiefOfFate, true, 41, "Tunnels",                  22,  10, 1, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung41_tunnels_asc"),
        M(GameChapter.ThiefOfFate, true, 42, "Malefia Lv1",              22,  22, 0, false, false, false, true,  10,  0,  0,  0,  0, "map_bt3_dung42_malefia_asc"),
        M(GameChapter.ThiefOfFate, true, 43, "Malefia Lv2",              22,  22, 1, false, false, false, true,  10,  0,  0,  0,  0, "map_bt3_dung43_malefia_asc"),
        M(GameChapter.ThiefOfFate, true, 44, "Malefia Lv3",              22,  22, 2, false, false, false, true,  10,  0,  0,  0,  0, "map_bt3_dung44_malefia_asc"),
        M(GameChapter.ThiefOfFate, true, 45, "Barracks",                 12,  15, 0, false, false, false, true,  11, 14,  0,  0,  0, "map_bt3_dung45_barracks_asc"),
        M(GameChapter.ThiefOfFate, true, 46, "Ferofists",                18,  18, 0, true,  false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung46_ferofists_asc"),
        M(GameChapter.ThiefOfFate, true, 47, "Private Quarter",           9,  17, 0, false, false, false, true,   0, 10,  0,  0,  0, "map_bt3_dung47_privatequarter_asc"),
        M(GameChapter.ThiefOfFate, true, 48, "Workshop",                  9,   9, 0, false, false, false, true,   6,  0,  0,  0,  0, "map_bt3_dung48_workshop_asc"),
        M(GameChapter.ThiefOfFate, true, 49, "Urmechs Paradise",         15,  15, 1, false, false, false, true,   6,  0,  0,  0,  0, "map_bt3_dung49_urmechsparadise_asc"),
        M(GameChapter.ThiefOfFate, true, 50, "Viscous Plane",            15,   9, 2, false, false, false, true,   6,  0,  0,  0,  0, "map_bt3_dung50_viscousplane_asc"),
        M(GameChapter.ThiefOfFate, true, 51, "Sanctum",                  13,  13, 0, false, false, false, true,  12,  3,  0,  0,  0, "map_bt3_dung51_sanctum_asc"),
        M(GameChapter.ThiefOfFate, true, 52, "Unterbrae Lv1",            15,  15, 0, false, false, false, true,  14,  0,  1, 18, 15, "map_bt3_dung52_unterbrae_asc"),
        M(GameChapter.ThiefOfFate, true, 53, "Unterbrae Lv2",            15,  15, 1, false, false, false, true,  14,  0,  1, 18, 15, "map_bt3_dung53_unterbrae_asc"),
        M(GameChapter.ThiefOfFate, true, 54, "Unterbrae Lv3",            15,  15, 2, false, false, false, true,  14,  0,  1, 18, 15, "map_bt3_dung54_unterbrae_asc"),
        M(GameChapter.ThiefOfFate, true, 55, "Unterbrae Lv4",            10,  22, 3, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung55_unterbrae_asc"),
        M(GameChapter.ThiefOfFate, true, 56, "Tarquarry",                11,  17, 0, false, false, true,  true,   0, 16,  0,  0,  0, "map_bt3_dung56_tarquarry_asc"),
        M(GameChapter.ThiefOfFate, true, 57, "Shadow Canyon",            13,  22, 0, false, false, true,  true,   3, 21,  0,  0,  0, "map_bt3_dung57_shadowcanyon_asc"),
        M(GameChapter.ThiefOfFate, true, 58, "Sceadu's Demens Lv1",      15,  15, 0, false, false, false, true,   1,  1,  7,  7,  7, "map_bt3_dung58_sceadu_asc"),
        M(GameChapter.ThiefOfFate, true, 59, "Sceadu's Demens Lv2",      15,  15, 1, false, false, false, true,   1,  1,  7,  5,  5, "map_bt3_dung59_sceadu_asc"),
        M(GameChapter.ThiefOfFate, true, 60, "Tarjan",                    6,   6, 0, false, false, false, true,   0,  0,  0,  0,  0, "map_bt3_dung60_tarjan_asc"),
    };

    /// <summary>
    /// The BT2 dream spell (ZZGO) destination table, straight from
    /// <c>GlobalMaps.m_dreamSpellTargets</c>. Each entry names the square in a city or the
    /// wilderness where that dungeon's entrance stands.
    /// </summary>
    public static readonly IReadOnlyList<DreamSpellTarget> DreamSpellTargets = new[]
    {
        new DreamSpellTarget("The Tombs",           2,  8,  7, Facing.North),
        new DreamSpellTarget("Fanskar's Castle",    0, 17, 27, Facing.North),
        new DreamSpellTarget("Dargoth's Tower",     3, 13,  2, Facing.North),
        new DreamSpellTarget("Maze of Dread",       6, 11, 14, Facing.North),
        new DreamSpellTarget("Oscon's Fortress",    5,  8, 13, Facing.North),
        new DreamSpellTarget("Grey Crypt",          0,  8, 31, Facing.North),
        new DreamSpellTarget("Destiny Stone",       4,  2, 13, Facing.North),
    };

    /// <summary>Where a new party starts, per chapter (from <c>GlobalMaps.m_newGameLocation</c>).</summary>
    public static readonly IReadOnlyDictionary<GameChapter, (bool IsDungeon, int Map, int X, int Z, Facing Facing)> NewGameLocations =
        new Dictionary<GameChapter, (bool IsDungeon, int Map, int X, int Z, Facing Facing)>
        {
            [GameChapter.TalesOfTheUnknown] = (false, 0, 24, 15, Facing.West),
            [GameChapter.DestinyKnight]     = (false, 1,  2,  8, Facing.East),
            [GameChapter.ThiefOfFate]       = (false, 0, 25, 16, Facing.North),
        };

    public static string ChapterName(GameChapter c) => c switch
    {
        GameChapter.TalesOfTheUnknown => "Tales of the Unknown",
        GameChapter.DestinyKnight => "The Destiny Knight",
        GameChapter.ThiefOfFate => "Thief of Fate",
        _ => "(no chapter loaded)",
    };

    public static string ChapterTag(GameChapter c) => c switch
    {
        GameChapter.TalesOfTheUnknown => "BT1",
        GameChapter.DestinyKnight => "BT2",
        GameChapter.ThiefOfFate => "BT3",
        _ => "?",
    };

    /// <summary>The maps of one chapter, in the order the game stores them.</summary>
    public static IEnumerable<GameMapInfo> ForChapter(GameChapter chapter) =>
        Maps.Where(m => m.Chapter == chapter);

    /// <summary>
    /// Looks up the catalogue entry for a live map: <paramref name="index"/> is
    /// <c>GameMap.m_mapIdx</c> and <paramref name="isDungeon"/> is <c>GameMap.m_isDungeonMap</c>.
    /// </summary>
    public static GameMapInfo? Find(GameChapter chapter, bool isDungeon, int index) =>
        Maps.FirstOrDefault(m => m.Chapter == chapter && m.IsDungeon == isDungeon && m.Index == index);
}
