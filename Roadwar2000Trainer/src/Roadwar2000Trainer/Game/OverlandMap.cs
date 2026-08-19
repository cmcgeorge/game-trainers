using System.IO;

namespace Roadwar2000Trainer.Game;

/// <summary>
/// One of the two overland maps.
/// <para>
/// A <c>.MAP</c> file is an eight-byte header followed by 2,016 terrain bytes, which the engine
/// reads verbatim into <c>DS:0x03C7</c>. The grid is 48 columns by 42 rows, and the square a
/// gang stands on is <c>Y * 48 + (X - 1)</c> -- X is 1-based, Y is 0-based. That rule is not a
/// guess: all 120 shipped city records land on a city tile of their own map under it, and
/// nothing else, and the party marker in live memory moved by exactly -1 on a westward step and
/// -48 on a northward one.
/// </para>
/// <para>
/// While the gang is on a square, the engine ORs <c>0x80</c> into that byte to mark it, so any
/// reader has to mask the top bit off before looking the terrain up.
/// </para>
/// </summary>
public sealed class OverlandMap
{
    public const int Width = 48;
    public const int Height = 42;
    public const int CellCount = Width * Height;      // 2,016
    public const int FileHeaderLength = 8;
    public const int FileLength = FileHeaderLength + CellCount;

    /// <summary>The engine sets this bit on the square the gang occupies.</summary>
    public const byte PartyMarker = 0x80;

    private readonly byte[] _cells;

    private OverlandMap(int mapId, string name, byte[] cells)
    {
        MapId = mapId;
        Name = name;
        _cells = cells;
    }

    /// <summary>1 = WEST.MAP, 2 = EAST.MAP -- the same numbering the city records use.</summary>
    public int MapId { get; }

    public string Name { get; }

    /// <summary>Terrain code at a square, with the party marker masked off.</summary>
    public int this[int x, int y]
    {
        get
        {
            int i = Index(x, y);
            return i >= 0 && i < CellCount ? _cells[i] & 0x7F : TerrainBook.Water;
        }
    }

    /// <summary>
    /// True when a square is somewhere a gang can be placed: column 1..48, row 0..41.
    /// <para>
    /// Column 0 is deliberately excluded even though the engine's flat index accepts it. The
    /// shipped city table contains exactly one record with X = 0 -- HOUSTON, at map 2, (0, 32) --
    /// and under the engine's own index that wraps onto row 31, column 47. The overland map does
    /// carry a large-metropolis tile at precisely that wrapped square, so the map data was
    /// authored to match; but standing there in the running game prints a blank location line,
    /// because the terrain name for a city code is empty and the engine's city lookup does not
    /// name it either. So the square is real, readable and reachable, and is still not somewhere
    /// to send a gang. See docs/reverse-engineering.md.
    /// </para>
    /// </summary>
    public static bool IsInside(int x, int y) => x >= 1 && x <= Width && y >= 0 && y < Height;

    /// <summary>
    /// The engine's own index rule: a flat array indexed <c>Y * 48 + (X - 1)</c>. Confirmed
    /// against live memory -- a westward step moved the party marker by -1 and a northward step
    /// by -48 -- and against all 120 shipped city records.
    /// </summary>
    public static int Index(int x, int y) => y * Width + (x - 1);

    /// <summary>Reads a WEST/EAST.MAP file. Returns null if it is not the right shape.</summary>
    public static OverlandMap? FromFile(string path, int mapId)
    {
        byte[] raw;
        try { raw = File.ReadAllBytes(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        return FromBytes(raw, mapId, Path.GetFileName(path));
    }

    /// <summary>
    /// Accepts either the raw file (header included) or the 2,016 loaded bytes, so the same
    /// parser serves the on-disk maps and a map lifted straight out of guest RAM.
    /// </summary>
    public static OverlandMap? FromBytes(byte[] raw, int mapId, string name)
    {
        byte[] cells;
        if (raw.Length == FileLength) cells = raw[FileHeaderLength..];
        else if (raw.Length == CellCount) cells = (byte[])raw.Clone();
        else return null;
        return new OverlandMap(mapId, name, cells);
    }

    /// <summary>Loads both overland maps out of a Roadwar game folder. Missing files come back null.</summary>
    public static (OverlandMap? West, OverlandMap? East) LoadPair(string gameFolder) =>
        (FromFile(Path.Combine(gameFolder, "WEST.MAP"), 1),
         FromFile(Path.Combine(gameFolder, "EAST.MAP"), 2));

    /// <summary>True when a gang can be placed here -- see <see cref="TerrainBook.IsPassable"/>.</summary>
    public bool IsPassable(int x, int y) => IsInside(x, y) && TerrainBook.IsPassable(this[x, y]);

    /// <summary>The city on a square, if any.</summary>
    public CityInfo? CityAt(int x, int y) => CityBook.At(MapId, x, y);

    /// <summary>Human-readable name of a square: the city if there is one, else the terrain.</summary>
    public string DescribeSquare(int x, int y)
    {
        if (!IsInside(x, y)) return "off the map";
        var city = CityAt(x, y);
        return city is not null ? city.Name : TerrainBook.NameOf(this[x, y]);
    }

    /// <summary>One ASCII glyph per terrain class, for the schematic and the strategy guide.</summary>
    public static char Glyph(int code) => code switch
    {
        TerrainBook.Plains => '.',
        TerrainBook.Farmland => ',',
        TerrainBook.Desert => '~',
        TerrainBook.Forest => '"',
        TerrainBook.Ruins => 'x',
        TerrainBook.Oilfield => 'O',
        TerrainBook.CitySmall => '1',
        TerrainBook.CityLarge => '2',
        TerrainBook.CityMetroplex => '3',
        _ when TerrainBook.IsRoad(code) => '=',
        _ => ' ',
    };

    /// <summary>Renders the whole map as ASCII, one line per row, without the ruler.</summary>
    public string[] ToAscii()
    {
        var rows = new string[Height];
        var buf = new char[Width];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) buf[x] = Glyph(this[x + 1, y]);
            rows[y] = new string(buf);
        }
        return rows;
    }
}
