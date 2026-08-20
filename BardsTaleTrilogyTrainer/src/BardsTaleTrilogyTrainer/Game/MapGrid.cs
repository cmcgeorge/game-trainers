using System.Globalization;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>What stands on one side of a dungeon cell (<c>DungeonMapCell.WallIndex</c>).</summary>
public enum WallKind
{
    None,
    Solid,
    Door,
    SecretDoor,
    LockedDoor,
    CrumblingWall,
    InvisibleWall,
    Railing,
    Unknown,
}

/// <summary>
/// Per-cell behaviour flags of a dungeon square (<c>DungeonMapCell.Flags</c>). The names are
/// the game's own; several are the classic Bard's Tale hazards — spinners, darkness, anti-magic.
/// </summary>
[Flags]
public enum CellFlags
{
    None = 0,
    StairsIn = 1 << 0,
    StairsOut = 1 << 1,
    PortalUp = 1 << 2,
    PortalDown = 1 << 3,
    Spinner = 1 << 4,
    Darkness = 1 << 5,
    AntiMagic = 1 << 6,
    AntiApar = 1 << 7,
    AntiMap = 1 << 8,
    DrainMagic = 1 << 9,
    RegenMagic = 1 << 10,
    RegenHealth = 1 << 11,
    HarmParty = 1 << 12,
    PoisonGas = 1 << 13,
    Smoke = 1 << 14,
    SilenceBard = 1 << 15,
    Flypaper = 1 << 16,
    Turncoat = 1 << 17,
    RandomCombat = 1 << 18,
    PresetCombat = 1 << 19,
    RandomTrap = 1 << 20,
    SpecialAhead = 1 << 21,
    Secret = 1 << 22,
    Odd = 1 << 23,
    Runes = 1 << 24,
    /// <summary>City cells only: the square can be walked onto.</summary>
    Passable = 1 << 25,
    /// <summary>City cells only: a building or other obstruction.</summary>
    Blocked = 1 << 26,
    GateOpen = 1 << 27,
    GateLocked = 1 << 28,
    Kickable = 1 << 29,
    ThievesTemple = 1 << 30,
}

/// <summary>A building or service occupying a city square (<c>CityMapCell.Modules</c>).</summary>
public enum CityModule
{
    None,
    Generic,
    Tavern,
    Temple,
    Casino,
    Guild,
    Garths,
    Review,
    Roscoes,
    Bank,
    StorageRoom,
    WizardsGuild,
    BardsHall,
    Unknown,
}

/// <summary>
/// One square of a decoded map. Dungeon squares carry four wall sides; city squares carry a
/// module and a passability flag instead. <see cref="Flags"/> is used by both.
/// </summary>
public readonly record struct MapCell(
    WallKind North,
    WallKind East,
    WallKind South,
    WallKind West,
    CellFlags Flags,
    CityModule Module)
{
    public static readonly MapCell Empty =
        new(WallKind.None, WallKind.None, WallKind.None, WallKind.None, CellFlags.None, CityModule.None);

    public WallKind Side(Facing facing) => facing switch
    {
        Facing.North => North,
        Facing.East => East,
        Facing.South => South,
        _ => West,
    };

    /// <summary>True when the square blocks movement — a city building rather than a street.</summary>
    public bool IsBlocked => Flags.HasFlag(CellFlags.Blocked);

    public bool HasStairs => (Flags & (CellFlags.StairsIn | CellFlags.StairsOut |
                                       CellFlags.PortalUp | CellFlags.PortalDown)) != 0;
}

/// <summary>
/// A decoded map: the grid itself plus the header the game stores with it. Cell (0,0) is the
/// south-west corner — X grows east and Z grows north, matching <c>Player.m_gridX/m_gridZ</c>.
/// </summary>
public sealed class MapGrid
{
    private readonly MapCell[] _cells;

    public int Width { get; }
    public int Height { get; }
    public bool IsDungeon { get; }
    public bool IsTower { get; }
    public bool IsOutside { get; }
    public bool IsWilderness { get; }
    public bool WrapsAround { get; }
    public int Level { get; }

    /// <summary>Location scripts by cell, keyed by <c>(x, z)</c> — stairs, messages, teleports.</summary>
    public IReadOnlyDictionary<(int X, int Z), string> LocationScripts { get; }

    public MapGrid(int width, int height, bool isDungeon, bool isTower, bool isOutside,
        bool isWilderness, bool wrapsAround, int level, MapCell[] cells,
        IReadOnlyDictionary<(int X, int Z), string> locationScripts)
    {
        Width = width;
        Height = height;
        IsDungeon = isDungeon;
        IsTower = isTower;
        IsOutside = isOutside;
        IsWilderness = isWilderness;
        WrapsAround = wrapsAround;
        Level = level;
        _cells = cells;
        LocationScripts = locationScripts;
    }

    public bool Contains(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

    public MapCell this[int x, int z] => Contains(x, z) ? _cells[z * Width + x] : MapCell.Empty;
}

/// <summary>
/// Parser for the remaster's map files. Each map ships as a plain-text TextAsset
/// (<c>map_bt1_dung00_cellars_asc</c> and friends): a <c>key=value</c> header, a <c>map</c>
/// section of one line per cell, then location scripts. A dungeon cell line reads
/// <c>x,z:North,East,South,West[, Flag…]</c>; a city cell line reads
/// <c>x,z:extra,motion,picture,Module[, Flag…]</c>.
///
/// <para>Unknown wall or flag names are tolerated rather than fatal, so a map from a future
/// build still renders — it just loses the tokens this build has never seen.</para>
/// </summary>
public static class MapFileParser
{
    public static MapGrid Parse(string text)
    {
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scripts = new Dictionary<(int X, int Z), string>();
        MapCell[]? cells = null;
        int width = 0, height = 0;
        bool isDungeon = false, inMap = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim('\r', ' ', '\t');
            if (line.Length == 0) continue;

            if (inMap)
            {
                if (TryParseCell(line, isDungeon, out int x, out int z, out var cell))
                {
                    if (cells != null && x >= 0 && x < width && z >= 0 && z < height)
                        cells[z * width + x] = cell;
                    continue;
                }
                inMap = false;      // the map section ended; fall through and treat as header
            }

            if (line == "map")
            {
                width = GetInt(header, "width");
                height = GetInt(header, "height");
                isDungeon = GetInt(header, "isDungeon") != 0;
                if (width <= 0 || height <= 0 || (long)width * height > 1 << 20)
                    throw new FormatException($"Map declares an unusable size ({width}x{height}).");
                cells = new MapCell[width * height];
                Array.Fill(cells, MapCell.Empty);
                inMap = true;
                continue;
            }

            if (line == "scripts") break;    // the script bodies are not needed for drawing

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq];
            string value = line[(eq + 1)..];

            if (key.Equals("locationScript", StringComparison.OrdinalIgnoreCase))
            {
                // locationScript=<x>,<z>,<label>
                var parts = value.Split(',');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sx) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sz))
                    scripts[(sx, sz)] = parts[2].Trim();
                continue;
            }

            header[key] = value;
        }

        if (cells == null)
            throw new FormatException("Map file has no 'map' section.");

        return new MapGrid(width, height, isDungeon,
            GetInt(header, "isTower") != 0,
            GetInt(header, "isOutside") != 0,
            GetInt(header, "isWilderness") != 0,
            GetInt(header, "wrapAroundEnable") != 0,
            GetInt(header, "level"),
            cells, scripts);
    }

    private static int GetInt(Dictionary<string, string> header, string key) =>
        header.TryGetValue(key, out var v) &&
        int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    private static bool TryParseCell(string line, bool isDungeon, out int x, out int z, out MapCell cell)
    {
        x = z = 0;
        cell = MapCell.Empty;

        int colon = line.IndexOf(':');
        if (colon <= 0) return false;
        var coords = line[..colon].Split(',');
        if (coords.Length != 2) return false;
        if (!int.TryParse(coords[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)) return false;
        if (!int.TryParse(coords[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) return false;

        var parts = line[(colon + 1)..].Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

        var flags = CellFlags.None;
        if (isDungeon)
        {
            if (parts.Length < 4) return false;
            // Sides first, then any number of behaviour flags.
            var n = ParseWall(parts[0]);
            var e = ParseWall(parts[1]);
            var s = ParseWall(parts[2]);
            var w = ParseWall(parts[3]);
            for (int i = 4; i < parts.Length; i++) flags |= ParseFlag(parts[i]);
            cell = new MapCell(n, e, s, w, flags, CityModule.None);
            return true;
        }

        // City: three numbers (extra, motion, picture) then the module, then flags.
        if (parts.Length < 4) return false;
        var module = ParseModule(parts[3]);
        for (int i = 4; i < parts.Length; i++) flags |= ParseFlag(parts[i]);
        cell = new MapCell(WallKind.None, WallKind.None, WallKind.None, WallKind.None, flags, module);
        return true;
    }

    /// <summary>
    /// Wall names carry a "NoPHDO" suffix when Phase Door cannot pass them. That only affects
    /// one spell, not what is drawn, so it is folded into the base kind.
    /// </summary>
    private static WallKind ParseWall(string token)
    {
        if (token.EndsWith("NoPHDO", StringComparison.Ordinal))
            token = token[..^"NoPHDO".Length];

        return token switch
        {
            "None" or "" => WallKind.None,
            "Solid" => WallKind.Solid,
            "Door" => WallKind.Door,
            "SecretDoor" => WallKind.SecretDoor,
            "LockedDoor" => WallKind.LockedDoor,
            "CrumblingWall" => WallKind.CrumblingWall,
            "InvisibleWall" => WallKind.InvisibleWall,
            "Railing" or "SolidRailing" => WallKind.Railing,
            _ => WallKind.Unknown,
        };
    }

    private static CellFlags ParseFlag(string token)
    {
        // "Face=North", "KAP=1", "Hint=3" and "RuneSpell=…" are cosmetic parameters, not flags.
        int eq = token.IndexOf('=');
        if (eq >= 0) return CellFlags.None;

        return token switch
        {
            "StairsIn" => CellFlags.StairsIn,
            "StairsOut" => CellFlags.StairsOut,
            "PortalUp" => CellFlags.PortalUp,
            "PortalDown" => CellFlags.PortalDown,
            "Spinner" => CellFlags.Spinner,
            "Darkness" => CellFlags.Darkness,
            "AntiMagic" => CellFlags.AntiMagic,
            "AntiApar" => CellFlags.AntiApar,
            "AntiMap" => CellFlags.AntiMap,
            "DrainMagic" => CellFlags.DrainMagic,
            "RegenMagic" => CellFlags.RegenMagic,
            "RegenHealth" => CellFlags.RegenHealth,
            "HarmParty" => CellFlags.HarmParty,
            "PoisonGas" => CellFlags.PoisonGas,
            "Smoke" => CellFlags.Smoke,
            "SilenceBard" => CellFlags.SilenceBard,
            "Flypaper" => CellFlags.Flypaper,
            "Turncoat" => CellFlags.Turncoat,
            "RandomCombat" => CellFlags.RandomCombat,
            "PresetCombat" => CellFlags.PresetCombat,
            "RandomTrap" => CellFlags.RandomTrap,
            "SpecialAhead" => CellFlags.SpecialAhead,
            "Secret" => CellFlags.Secret,
            "Odd" => CellFlags.Odd,
            "Runes" => CellFlags.Runes,
            "Passable" => CellFlags.Passable,
            "Blocked" => CellFlags.Blocked,
            "GateOpen" => CellFlags.GateOpen,
            "GateLocked" => CellFlags.GateLocked,
            "Kickable" => CellFlags.Kickable,
            "ThievesTemple" => CellFlags.ThievesTemple,
            _ => CellFlags.None,
        };
    }

    private static CityModule ParseModule(string token) => token switch
    {
        "None" or "" => CityModule.None,
        "Generic" => CityModule.Generic,
        "Tavern" => CityModule.Tavern,
        "Temple" => CityModule.Temple,
        "Casino" => CityModule.Casino,
        "Guild" => CityModule.Guild,
        "Garths" => CityModule.Garths,
        "Review" => CityModule.Review,
        "Roscoes" => CityModule.Roscoes,
        "Bank" => CityModule.Bank,
        "StorageRoom" => CityModule.StorageRoom,
        "WizardsGuild" => CityModule.WizardsGuild,
        "BardsHall" => CityModule.BardsHall,
        _ => CityModule.Unknown,
    };

    /// <summary>Short label drawn on a city square, or null for plain street.</summary>
    public static string? ModuleLabel(CityModule module) => module switch
    {
        CityModule.Tavern => "TAV",
        CityModule.Temple => "TMP",
        CityModule.Casino => "CAS",
        CityModule.Guild => "GLD",
        CityModule.Garths => "GAR",
        CityModule.Review => "REV",
        CityModule.Roscoes => "ROS",
        CityModule.Bank => "BNK",
        CityModule.StorageRoom => "STO",
        CityModule.WizardsGuild => "WIZ",
        CityModule.BardsHall => "BRD",
        _ => null,
    };
}
