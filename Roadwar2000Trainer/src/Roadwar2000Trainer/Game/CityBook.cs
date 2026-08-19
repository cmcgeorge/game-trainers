// The 120 city definitions, transcribed out of START.EXE's initialised data (name table at
// DS:0x288F / slab 0x06D5, records at DS:0x2E72 / slab 0x0CB8, 120 records of 12 bytes).
// Only the immutable half is baked in here -- id, name, size, which overland map, and where on
// it. The mutable half (cache contents, who holds the town) is read from the live game.
//
// Size is deliberately taken from the EXE and not from the shipped CHICAGO.RWS: that save is a
// game in progress and 30 of its towns have already been looted below their starting level
// (Chicago 150 against a shipped 178, Ottawa 0 against 8). These are the pristine figures, which
// is what "restock to the shipped level" has to mean. FormatCheck pins the whole column against
// START.EXE whenever the game folder is present.
//
// All 120 (Map, X, Y) triples were verified to land on a city tile of the matching overland map
// using the engine's own index rule, Y * 48 + (X - 1).
namespace Roadwar2000Trainer.Game;

/// <summary>A city as shipped in the engine's table.</summary>
/// <param name="Size">The engine's starting supply/population figure; it falls as the town is looted.</param>
/// <param name="Map">1 = WEST.MAP, 2 = EAST.MAP.</param>
public sealed record CityInfo(int Id, string Name, int Size, int Map, int X, int Y)
{
    public string MapName => Map == 1 ? "West" : Map == 2 ? "East" : "?";

    public override string ToString() => Name;
}

/// <summary>The engine's city table, in engine order.</summary>
public static class CityBook
{
    /// <summary>The 120 cities; the index is the record index in the save.</summary>
    public static readonly IReadOnlyList<CityInfo> All = new CityInfo[]
    {
        new(  0, "LOUISVILLE", 23, 2, 15, 19),
        new(  1, "VANCOUVER", 40, 1, 3, 1),
        new(  2, "SEATTLE", 41, 1, 4, 3),
        new(  3, "TACOMA", 13, 1, 3, 4),
        new(  4, "PORTLAND", 32, 1, 3, 7),
        new(  5, "SPOKANE", 9, 1, 11, 3),
        new(  6, "SACRAMENTO", 26, 1, 5, 17),
        new(  7, "SANTA ROSA", 8, 1, 4, 18),
        new(  8, "SN FRAN/OAKLND", 82, 1, 4, 19),
        new(  9, "SN JOSE/MTN VW", 33, 1, 5, 20),
        new( 10, "STOCKTON", 9, 1, 6, 19),
        new( 11, "FRESNO", 13, 1, 8, 21),
        new( 12, "BAKERSFIELD", 11, 1, 9, 23),
        new( 13, "OXN/SIMI V/VNT", 14, 1, 9, 24),
        new( 14, "LOS ANGELES", 187, 1, 11, 25),
        new( 15, "ANA/S ANA/G GR", 49, 1, 12, 25),
        new( 16, "RVRSD/SN B/ONT", 39, 1, 13, 25),
        new( 17, "SAN DIEGO", 47, 1, 13, 27),
        new( 18, "TIJUANA", 14, 1, 13, 28),
        new( 19, "MEXICALI", 9, 1, 15, 28),
        new( 20, "LAS VEGAS", 12, 1, 16, 22),
        new( 21, "SLT LK CTY/OGD", 24, 1, 21, 14),
        new( 22, "PHOENIX", 38, 1, 20, 26),
        new( 23, "TUCSON", 14, 1, 22, 28),
        new( 24, "HERMOSILLO", 5, 1, 21, 31),
        new( 25, "DURANGO", 7, 1, 32, 39),
        new( 26, "TORREON", 10, 1, 34, 38),
        new( 27, "MONTERREY", 44, 1, 39, 38),
        new( 28, "CHIHUAHUA", 10, 1, 30, 33),
        new( 29, "CIUDAD JUAREZ", 14, 1, 29, 29),
        new( 30, "EL PASO", 12, 1, 29, 28),
        new( 31, "ALBUQUERQUE", 12, 1, 29, 23),
        new( 32, "COLRADO SPRNGS", 8, 1, 32, 17),
        new( 33, "DENVER", 41, 1, 32, 16),
        new( 34, "WINNEPEG", 15, 1, 44, 0),
        new( 35, "FARGO", 4, 1, 44, 4),
        new( 36, "OMAHA", 15, 1, 46, 13),
        new( 37, "WICHITA", 11, 1, 44, 19),
        new( 38, "TULSA", 18, 1, 46, 22),
        new( 39, "OKLAHOMA CITY", 21, 1, 44, 23),
        new( 40, "DALLAS/FT WRTH", 75, 1, 45, 27),
        new( 41, "AUSTIN", 14, 1, 43, 31),
        new( 42, "SAN ANTONIO", 27, 1, 42, 32),
        new( 43, "CORPUS CHRISTI", 9, 1, 44, 35),
        new( 44, "MNPLS/ST PAUL", 53, 2, 4, 7),
        new( 45, "KANSAS CITY", 34, 2, 1, 17),
        new( 46, "HOUSTON", 76, 2, 0, 32),
        new( 47, "BEAUMONT", 10, 2, 2, 31),
        new( 48, "NEW ORLEANS", 30, 2, 8, 32),
        new( 49, "MEMPHIS", 23, 2, 8, 23),
        new( 50, "ST LOUIS", 59, 2, 8, 18),
        new( 51, "MILWAUKEE", 35, 2, 12, 11),
        new( 52, "CHICAGO", 178, 2, 12, 13),
        new( 53, "GRY/HMND/E CHI", 17, 2, 13, 13),
        new( 54, "INDIANAPOLIS", 18, 2, 15, 16),
        new( 55, "NASHVILLE", 22, 2, 13, 22),
        new( 56, "BIRMINGHAM", 22, 2, 13, 26),
        new( 57, "GRAND RAPIDS", 16, 2, 15, 11),
        new( 58, "FLINT", 14, 2, 18, 11),
        new( 59, "DETROIT", 109, 2, 20, 12),
        new( 60, "WINDSOR", 5, 2, 21, 12),
        new( 61, "TOLEDO", 20, 2, 19, 13),
        new( 62, "DAYTON", 21, 2, 17, 16),
        new( 63, "CINCINNATI", 36, 2, 17, 17),
        new( 64, "COLUMBUS", 28, 2, 20, 16),
        new( 65, "AKRON", 17, 2, 22, 15),
        new( 66, "CLEVELAND", 48, 2, 22, 14),
        new( 67, "YNGSTWN/WARREN", 14, 2, 23, 14),
        new( 68, "PITTSBURGH", 57, 2, 24, 15),
        new( 69, "TORONTO", 16, 2, 25, 9),
        new( 70, "HAMILTON", 8, 2, 25, 10),
        new( 71, "BUFFALO", 32, 2, 26, 11),
        new( 72, "ROCHESTER", 25, 2, 28, 11),
        new( 73, "SYRACUSE", 17, 2, 31, 11),
        new( 74, "OTTAWA", 8, 2, 31, 7),
        new( 75, "MONTREAL", 71, 2, 35, 7),
        new( 76, "QUEBEC", 5, 2, 38, 4),
        new( 77, "ALB/SCHEN/TROY", 20, 2, 34, 11),
        new( 78, "SPFD/CHCP/HLYK", 14, 2, 36, 12),
        new( 79, "BOSTON", 70, 2, 39, 12),
        new( 80, "PROV/WRWK/PAWT", 23, 2, 38, 13),
        new( 81, "HARTFORD", 19, 2, 36, 13),
        new( 82, "NEW YORK CITY", 228, 2, 34, 14),
        new( 83, "NWRK/JERSY CTY", 63, 2, 33, 15),
        new( 84, "SCRANTON", 17, 2, 31, 13),
        new( 85, "ALNTN/BTH/EAST", 16, 2, 31, 14),
        new( 86, "PHILADELPHIA", 118, 2, 32, 15),
        new( 87, "WILMINGTON", 40, 2, 31, 16),
        new( 88, "BALTIMORE", 55, 2, 30, 18),
        new( 89, "WASHINGTON,DC", 77, 2, 29, 19),
        new( 90, "RICHMOND", 16, 2, 29, 20),
        new( 91, "NRFK/VA B/PTSM", 21, 2, 30, 21),
        new( 92, "RALEIGH/DURHAM", 16, 2, 27, 22),
        new( 93, "GBRO/W-S/HI PT", 21, 2, 25, 22),
        new( 94, "CHARLOTTE", 16, 2, 23, 23),
        new( 95, "GRNVL/SPRTNBRG", 15, 2, 21, 24),
        new( 96, "ATLANTA", 51, 2, 17, 26),
        new( 97, "JACKSONVILLE", 19, 2, 22, 31),
        new( 98, "TMPA/ST PTRBRG", 40, 2, 20, 35),
        new( 99, "ORLANDO", 18, 2, 22, 34),
        new(100, "W PM BH/B RATN", 15, 2, 24, 36),
        new(101, "FT LAUD/HLLYWD", 26, 2, 24, 37),
        new(102, "MIAMI", 41, 2, 24, 38),
        new(103, "EUGN/SPRINGFLD", 7, 1, 3, 9),
        new(104, "SALEM", 7, 1, 3, 8),
        new(105, "MODESTO", 7, 1, 7, 20),
        new(106, "SLNS/MONT/SEAS", 8, 1, 5, 21),
        new(107, "S BRB/S MR/LOM", 8, 1, 8, 24),
        new(108, "NAPA/VLJ/FRFLD", 9, 1, 5, 18),
        new(109, "VISALIA", 7, 1, 9, 22),
        new(110, "BOISE", 5, 1, 13, 10),
        new(111, "RENO", 5, 1, 8, 16),
        new(112, "AMARILLO", 5, 1, 37, 24),
        new(113, "BROWNSVILLE", 6, 1, 44, 38),
        new(114, "LUBBOCK", 6, 1, 37, 26),
        new(115, "MCALN/PHR/EDNB", 8, 1, 43, 38),
        new(116, "WACO", 5, 1, 43, 29),
        new(117, "TEMPLE/KILLEEN", 6, 1, 43, 30),
        new(118, "PROVO", 6, 1, 21, 15),
        new(119, "LINCOLN", 5, 1, 45, 14),
    };

    public static CityInfo? ById(int id) => id >= 0 && id < All.Count ? All[id] : null;

    /// <summary>The city standing on an overland square, or null when that square is open country.</summary>
    public static CityInfo? At(int map, int x, int y)
    {
        foreach (var c in All)
            if (c.Map == map && c.X == x && c.Y == y) return c;
        return null;
    }
}
