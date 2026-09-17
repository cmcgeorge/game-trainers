namespace Space1889Trainer.Files;

/// <summary>One map square: which tile sheet (0 = the shared DEF sheet, 1 = the planet's SPR sheet) and which tile.</summary>
public readonly record struct MapCell(byte Sheet, byte Tile)
{
    /// <summary>Tile row in its 20-per-row sheet.</summary>
    public int SheetRow => Tile / 20;

    /// <summary>Tile column in its sheet.</summary>
    public int SheetColumn => Tile % 20;
}

/// <summary>A decoded map: <see cref="Rows"/> × <see cref="Columns"/> cells, row-major.</summary>
public sealed class GameMap
{
    public GameMap(int id, int screensDown, int screensAcross, string headerName, MapCell[] cells)
    {
        Id = id;
        Rows = screensDown * MapFile.RowsPerScreen;
        Columns = screensAcross * MapFile.ColumnsPerScreen;
        HeaderName = headerName;
        if (cells.Length != Rows * Columns) throw new ArgumentException("cell count does not match the size", nameof(cells));
        Cells = cells;
    }

    /// <summary>planet × 100 + area × 10 + map (992/999 = the shared pub/inn interiors).</summary>
    public int Id { get; }

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>The planet name written into the map header ("EARTH", "MARS", "SPACE" for ships).</summary>
    public string HeaderName { get; }

    public MapCell[] Cells { get; }

    public MapCell this[int row, int column] => Cells[row * Columns + column];

    public bool Contains(int row, int column) => row >= 0 && column >= 0 && row < Rows && column < Columns;

    public int Planet => Id / 100;

    public int Area => Id / 10 % 10;

    public int MapNumber => Id % 10;
}

/// <summary>
/// A.SYS (Earth, Luna, Mercury), B.SYS (Mars, Venus, ships) and 0.SYS (space): a table of u32 offsets keyed by map
/// id (−1 = none), then per map a 26-byte header — u8 screens down (× 12 rows), u8 screens across (× 20 columns),
/// char[24] planet name — a u16 packed length and a 9-bit LSB-first code stream (M.EXE 1000:24FB / 1000:91E7):
/// a code below 0xF0 is DEF tile <c>c</c>, 0xF0..0x1DF is SPR tile <c>c − 0xF0</c>, 0x1E0..0x1FE are base-31
/// repeat digits applied to the next literal (written <c>rep + 3</c> times), 0x1FF is a void cell.
/// </summary>
public static class MapFile
{
    public const int RowsPerScreen = 12;
    public const int ColumnsPerScreen = 20;

    /// <summary>Rows in the tallest ground map (the Earth and Mars planet maps are 100 × 108); no map square lies beyond it.</summary>
    public const int LargestGroundRows = 108;

    /// <summary>Columns in the widest ground map.</summary>
    public const int LargestGroundColumns = 100;

    /// <summary>Header bytes before the packed length.</summary>
    public const int HeaderSize = 26;

    /// <summary>Which SYS file holds a map id.</summary>
    public static string FileFor(int mapId) => (mapId / 100) switch
    {
        0 => "0.SYS",
        1 or 2 or 3 => "A.SYS",
        4 or 5 or 6 => "B.SYS",
        _ => "A.SYS",   // 992/999 exist in both; the A copy is used when the planet is unknown
    };

    /// <summary>Every map id present in a SYS file, with its offset.</summary>
    public static IReadOnlyDictionary<int, int> ReadIndex(byte[] sys)
    {
        ArgumentNullException.ThrowIfNull(sys);
        var index = new SortedDictionary<int, int>();
        if (sys.Length < 4) return index;
        // The table ends where the first map starts (4,000 bytes in A/B.SYS, 400 in 0.SYS).
        int first = sys.Length;
        for (int i = 0; i * 4 + 4 <= sys.Length && i * 4 < first; i++)
        {
            int o = BitConverter.ToInt32(sys, i * 4);
            if (o > 0) first = Math.Min(first, o);
        }
        for (int i = 0; i * 4 < first && i * 4 + 4 <= sys.Length; i++)
        {
            int o = BitConverter.ToInt32(sys, i * 4);
            if (o > 0 && o < sys.Length) index[i] = o;
        }
        return index;
    }

    /// <summary>Decodes the map at <paramref name="offset"/>, or returns null if it is malformed.</summary>
    public static GameMap? Decode(byte[] sys, int mapId, int offset)
    {
        ArgumentNullException.ThrowIfNull(sys);
        if (offset < 0 || offset > sys.Length - (HeaderSize + 2)) return null;
        int down = sys[offset], across = sys[offset + 1];
        if (down == 0 || across == 0) return null;
        int nameEnd = offset + 2;
        while (nameEnd < offset + HeaderSize && sys[nameEnd] != 0) nameEnd++;
        string name = System.Text.Encoding.ASCII.GetString(sys, offset + 2, nameEnd - offset - 2);
        int packed = sys[offset + HeaderSize] | (sys[offset + HeaderSize + 1] << 8);
        int start = offset + HeaderSize + 2;
        if (packed > sys.Length - start) return null;

        int rows = down * RowsPerScreen, cols = across * ColumnsPerScreen;
        var cells = new MapCell[rows * cols];
        int produced = 0;
        long bitPos = 0, bitEnd = (long)packed * 8;
        int repeat = 0;
        bool pendingRepeat = false;

        while (produced < cells.Length)
        {
            // The game reads 16 bits at a time, so the final code may run a byte past the packed length;
            // bits beyond the data read as zero. Anything further than that is a corrupt record.
            if (bitPos > bitEnd + 8) return null;
            int code = 0;
            for (int b = 0; b < 9; b++)
            {
                long p = bitPos + b;
                long at = start + (p >> 3);
                if (at < sys.Length && (sys[at] >> (int)(p & 7) & 1) != 0) code |= 1 << b;
            }
            bitPos += 9;

            MapCell cell;
            if (code < 0xF0) cell = new MapCell(0, (byte)code);
            else if (code < 0x1E0) cell = new MapCell(1, (byte)(code - 0xF0));
            else if (code != 0x1FF)
            {
                repeat = repeat * 31 + (code - 0x1E0);
                pendingRepeat = true;
                continue;
            }
            else cell = new MapCell(0x0F, 0xFF);

            int copies = pendingRepeat ? repeat + 3 : 1;
            for (int k = 0; k < copies && produced < cells.Length; k++) cells[produced++] = cell;
            repeat = 0;
            pendingRepeat = false;
        }
        return new GameMap(mapId, down, across, name, cells);
    }

    /// <summary>Reads map <paramref name="mapId"/> from its SYS file bytes, or null.</summary>
    public static GameMap? Load(byte[] sys, int mapId) =>
        ReadIndex(sys).TryGetValue(mapId, out int offset) ? Decode(sys, mapId, offset) : null;
}
