namespace MightAndMagic1Trainer.Game;

/// <summary>How an edge of a cell behaves, decoded from the maze's passability plane.</summary>
public enum EdgeKind : byte
{
    Open = 0,     // walk straight through
    Wall = 1,     // solid, blocks movement
    Door = 2,     // passable door
    Special = 3,  // passable but flagged (secret door / one-way / trigger)
}

/// <summary>
/// One 16×16 Might &amp; Magic 1 maze, decoded from a 512-byte record of <c>Mazedata.dta</c>.
/// Format (confirmed in <c>docs/maze-atlas.md</c>): two co-registered 16×16 planes —
/// plane 1 (bytes 0–255) = wall <em>graphic</em>, plane 2 (bytes 256–511) = wall
/// <em>passability</em>. Each cell byte packs four 2-bit direction fields:
/// W = bits 0–1, N = bits 2–3, E = bits 4–5, S = bits 6–7. Cell (x, y) lives at
/// byte <c>y*16 + x</c>; y = 0 is the south edge (rendered at the bottom, north up).
/// </summary>
public sealed class MazeMap
{
    public const int Size = 16;

    public int Index { get; }
    public string RawName { get; }
    public string DisplayName { get; }

    // [x, y, dir]; dir 0=W, 1=N, 2=E, 3=S
    private readonly EdgeKind[,,] _pass = new EdgeKind[Size, Size, 4];
    private readonly byte[,,] _graphic = new byte[Size, Size, 4];

    /// <summary>The raw 256-byte wall-graphic plane (plane 1), used to fingerprint this map
    /// against the live game's loaded maze buffer.</summary>
    public byte[] Plane1 { get; }

    internal MazeMap(int index, string rawName, string displayName, ReadOnlySpan<byte> record)
    {
        Index = index;
        RawName = rawName;
        DisplayName = displayName;
        Plane1 = record.Slice(0, 256).ToArray();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                byte g = record[y * Size + x];           // plane 1
                byte p = record[256 + y * Size + x];     // plane 2
                for (int dir = 0; dir < 4; dir++)
                {
                    _graphic[x, y, dir] = (byte)((g >> (dir * 2)) & 3);
                    _pass[x, y, dir] = (EdgeKind)((p >> (dir * 2)) & 3);
                }
            }
        }
    }

    /// <summary>Passability of the edge of cell (x, y) in direction <paramref name="dir"/> (0=W,1=N,2=E,3=S).</summary>
    public EdgeKind Edge(int x, int y, int dir) => _pass[x, y, dir];

    /// <summary>True when a wall is drawn (plane 1) but you can still walk through (plane 2 open) —
    /// MM1's illusory / secret passages.</summary>
    public bool IsIllusory(int x, int y, int dir) =>
        _graphic[x, y, dir] != 0 && _pass[x, y, dir] == EdgeKind.Open;

    public override string ToString() => DisplayName;
}

/// <summary>
/// Loads and holds all 55 mazes from a <c>Mazedata.dta</c> file (28,160 bytes = 55 × 512).
/// The record order matches the location-name table baked into <c>Mm.exe</c>.
/// </summary>
public sealed class MazeData
{
    public const int MapCount = 55;
    public const int RecordSize = 512;
    public const int FileSize = MapCount * RecordSize;   // 28160

    public IReadOnlyList<MazeMap> Maps { get; }

    // Built once at construction so MatchAt is read-only and safe to call from any thread
    // (the fingerprint scan runs on a background thread).
    private readonly Dictionary<ulong, List<int>> _prefixIndex;

    private MazeData(IReadOnlyList<MazeMap> maps)
    {
        Maps = maps;
        _prefixIndex = new Dictionary<ulong, List<int>>();
        for (int i = 0; i < maps.Count; i++)
        {
            ulong key = Prefix(maps[i].Plane1, 0);
            if (!_prefixIndex.TryGetValue(key, out var list)) _prefixIndex[key] = list = new List<int>();
            list.Add(i);
        }
    }

    /// <summary>Parses a Mazedata.dta byte buffer, or returns null if it isn't the expected size.</summary>
    public static MazeData? FromBytes(byte[] bytes)
    {
        if (bytes.Length < FileSize) return null;
        var maps = new List<MazeMap>(MapCount);
        for (int i = 0; i < MapCount; i++)
        {
            var rec = bytes.AsSpan(i * RecordSize, RecordSize);
            maps.Add(new MazeMap(i, RawNames[i], Display(i, RawNames[i]), rec));
        }
        return new MazeData(maps);
    }

    // --- live current-map fingerprinting ----------------------------------------
    // The game loads the current 16×16 maze into a RAM buffer byte-for-byte. Scanning
    // the attached process for the 256-byte plane-1 block that matches one of the 55
    // known records identifies the current map exactly — no map-id offset needed.

    private static ulong Prefix(byte[] b, int off)
    {
        ulong k = 0;
        for (int i = 0; i < 8; i++) k = (k << 8) | b[off + i];
        return k;
    }

    /// <summary>Index of the map whose plane-1 fingerprint exactly matches the 256 bytes of
    /// <paramref name="window"/> starting at <paramref name="off"/>, or -1.</summary>
    public int MatchAt(byte[] window, int off)
    {
        if (off < 0 || off + 256 > window.Length) return -1;
        if (!_prefixIndex.TryGetValue(Prefix(window, off), out var candidates)) return -1;
        foreach (int idx in candidates)
        {
            var p = Maps[idx].Plane1;
            int k = 0;
            while (k < 256 && window[off + k] == p[k]) k++;
            if (k == 256) return idx;
        }
        return -1;
    }

    /// <summary>Scans a memory buffer for the live maze; returns the matched map index and the
    /// byte offset it was found at, or (-1, -1).</summary>
    public (int Map, int Offset) FindInBuffer(byte[] buffer)
    {
        for (int off = 0; off + 256 <= buffer.Length; off++)
        {
            int idx = MatchAt(buffer, off);
            if (idx >= 0) return (idx, off);
        }
        return (-1, -1);
    }

    // The 55 location names in record order, extracted from Mm.exe's name table (offset 0x10BE7).
    private static readonly string[] RawNames =
    {
        "sorpigal", "portsmit", "algary", "dusk", "erliquin",
        "cave1", "cave2", "cave3", "cave4", "cave5", "cave6", "cave7", "cave8", "cave9",
        "areaa1", "areaa2", "areaa3", "areaa4", "areab1", "areab2", "areab3", "areab4",
        "areac1", "areac2", "areac3", "areac4", "aread1", "aread2", "aread3", "aread4",
        "areae1", "areae2", "areae3", "areae4",
        "doom", "blackrn", "blackrs", "qvl1", "qvl2", "rwl1", "rwl2", "enf1", "enf2",
        "whitew", "dragad", "udrag1", "udrag2", "udrag3", "demon", "alamar",
        "pp1", "pp2", "pp3", "pp4", "astral",
    };

    // Friendly labels. Confident ones (towns, overworld grid, named castles, astral) are named;
    // the rest keep a best-guess label with the raw token so nothing is silently mis-asserted.
    private static string Display(int i, string raw)
    {
        if (Friendly.TryGetValue(raw, out var name)) return $"{name}  ({raw})";
        if (raw.StartsWith("cave")) return $"Cave {raw[4..]}  ({raw})";
        if (raw.StartsWith("area") && raw.Length == 6)
            return $"Overworld {char.ToUpperInvariant(raw[4])}-{raw[5]}  ({raw})";
        return $"{raw}";
    }

    private static readonly Dictionary<string, string> Friendly = new()
    {
        ["sorpigal"] = "Sorpigal — town (start)",
        ["portsmit"] = "Portsmith — town",
        ["algary"]   = "Algary — town",
        ["dusk"]     = "Dusk — town",
        ["erliquin"] = "Erliquin — town",
        ["doom"]     = "Castle Doom",
        ["blackrn"]  = "Castle Blackridge (N)",
        ["blackrs"]  = "Castle Blackridge (S)",
        ["whitew"]   = "Castle White Wolf",
        ["dragad"]   = "Castle Dragadune (ruins)",
        ["alamar"]   = "Castle Alamar",
        ["demon"]    = "The Soul Maze",
        ["astral"]   = "The Astral Plane",
        ["qvl1"]     = "Quivering Forest lair 1",
        ["qvl2"]     = "Quivering Forest lair 2",
        ["rwl1"]     = "Raven's Wood lair 1",
        ["rwl2"]     = "Raven's Wood lair 2",
        ["enf1"]     = "Enchanted Forest 1",
        ["enf2"]     = "Enchanted Forest 2",
        ["udrag1"]   = "Dragadune underground 1",
        ["udrag2"]   = "Dragadune underground 2",
        ["udrag3"]   = "Dragadune underground 3",
        ["pp1"]      = "Old Order temple L1",
        ["pp2"]      = "Old Order temple L2",
        ["pp3"]      = "Old Order temple L3",
        ["pp4"]      = "Old Order temple L4",
    };
}
