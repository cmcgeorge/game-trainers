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
/// One 16×16 Might &amp; Magic 1 maze. Two co-registered 16×16 planes describe it (confirmed in
/// <c>docs/maze-atlas.md</c>): plane 1 = wall <em>graphic</em>, plane 2 = wall
/// <em>passability</em>. Each cell byte packs four 2-bit direction fields: W = bits 0–1,
/// N = bits 2–3, E = bits 4–5, S = bits 6–7. Cell (x, y) lives at byte <c>y*16 + x</c>; y = 0
/// is the south edge (rendered at the bottom, north up).
///
/// <para>A maze comes either from a 512-byte record of the player's own <c>Mazedata.dta</c> —
/// exact, including the wall graphic the live fingerprint matches on — or from the bundled
/// <see cref="BuiltInMazes"/> grids, which carry every edge's behaviour but only whether a
/// wall is drawn, not which graphic. Everything the renderer needs is present either way.</para>
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

    /// <summary>
    /// The raw 256-byte wall-graphic plane (plane 1), used to fingerprint this map against the
    /// live game's loaded maze buffer — null for a built-in map, whose graphic plane is only
    /// known as drawn-or-not.
    /// </summary>
    public byte[]? Plane1 { get; }

    /// <summary>The 256-byte passability plane (plane 2), packed the way the game stores it.</summary>
    public byte[] Plane2 { get; }

    /// <summary>Builds a maze from one 512-byte record of <c>Mazedata.dta</c>.</summary>
    internal MazeMap(int index, string rawName, string displayName, ReadOnlySpan<byte> record)
    {
        Index = index;
        RawName = rawName;
        DisplayName = displayName;
        Plane1 = record.Slice(0, 256).ToArray();
        Plane2 = record.Slice(256, 256).ToArray();

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

    /// <summary>
    /// Builds a maze from one bundled <see cref="BuiltInMazes"/> grid: 33 lines of 33
    /// characters, row 0 being y = 15. Line <c>2r</c> holds the horizontal edges above square
    /// row r, line <c>2r + 1</c> the vertical edges of that row. A shared edge appears once and
    /// is given to both cells, so plane 2 is rebuilt from it.
    /// </summary>
    internal MazeMap(int index, string rawName, string displayName, string[] rows)
    {
        Index = index;
        RawName = rawName;
        DisplayName = displayName;
        Plane1 = null;
        Plane2 = new byte[256];

        for (int y = 0; y < Size; y++)
        {
            int r = Size - 1 - y;                  // row 0 of the grid is the northernmost, y = 15
            string above = Row(rows, r * 2);       // north edge of this row
            string below = Row(rows, r * 2 + 2);   // south edge of this row
            string sides = Row(rows, r * 2 + 1);

            for (int x = 0; x < Size; x++)
            {
                Set(x, y, 0, At(sides, x * 2));          // W
                Set(x, y, 1, At(above, x * 2 + 1));      // N
                Set(x, y, 2, At(sides, x * 2 + 2));      // E
                Set(x, y, 3, At(below, x * 2 + 1));      // S

                int packed = 0;
                for (int dir = 0; dir < 4; dir++) packed |= (int)_pass[x, y, dir] << (dir * 2);
                Plane2[y * Size + x] = (byte)packed;
            }
        }

        static string Row(string[] rows, int index) => index >= 0 && index < rows.Length ? rows[index] : "";
        static char At(string row, int index) => index >= 0 && index < row.Length ? row[index] : ' ';

        void Set(int x, int y, int dir, char glyph)
        {
            _pass[x, y, dir] = glyph switch
            {
                '#' => EdgeKind.Wall,
                'D' => EdgeKind.Door,
                'S' => EdgeKind.Special,
                _ => EdgeKind.Open,
            };
            // '#', 'D', 'S' and 'o' all mean "a wall is drawn here"; only a space means nothing
            // is. Which of plane 1's three graphics it was does not survive the transcription,
            // and the renderer never asks.
            _graphic[x, y, dir] = glyph == ' ' ? (byte)0 : (byte)1;
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
/// Holds all 55 mazes of the game, either decoded from a <c>Mazedata.dta</c> file
/// (28,160 bytes = 55 × 512) or built from the bundled <see cref="BuiltInMazes"/> grids. The
/// record order matches the location-name table baked into <c>Mm.exe</c>.
/// </summary>
public sealed class MazeData
{
    public const int MapCount = 55;
    public const int RecordSize = 512;
    public const int FileSize = MapCount * RecordSize;   // 28160

    /// <summary>Bytes of the live maze buffer one fingerprint comparison reads.</summary>
    public const int FingerprintLength = 256;

    public IReadOnlyList<MazeMap> Maps { get; }

    /// <summary>
    /// True when these mazes came from a real <c>Mazedata.dta</c>. Exact data fingerprints the
    /// live maze on the wall-graphic plane byte for byte; the bundled data has to fall back to
    /// a near-match on the passability plane (see <see cref="MatchAt"/>).
    /// </summary>
    public bool IsExact { get; }

    // Built once at construction so MatchAt is read-only and safe to call from any thread
    // (the fingerprint scan runs on a background thread).
    private readonly Dictionary<ulong, List<int>> _prefixIndex;
    private readonly int[] _blockedFields;

    private MazeData(IReadOnlyList<MazeMap> maps, bool isExact)
    {
        Maps = maps;
        IsExact = isExact;
        _prefixIndex = new Dictionary<ulong, List<int>>();
        _blockedFields = new int[maps.Count];
        for (int i = 0; i < maps.Count; i++) _blockedFields[i] = CountNonZeroFields(maps[i].Plane2);
        if (!isExact) return;
        for (int i = 0; i < maps.Count; i++)
        {
            ulong key = Prefix(maps[i].Plane1!, 0);
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
        return new MazeData(maps, isExact: true);
    }

    /// <summary>
    /// The bundled set: every area of the game, drawn from <see cref="BuiltInMazes"/> so the
    /// map tab works before — or without — the player pointing at their own Mazedata.dta.
    /// </summary>
    public static MazeData BuiltIn()
    {
        var maps = new List<MazeMap>(MapCount);
        for (int i = 0; i < MapCount && i < BuiltInMazes.Records.Count; i++)
            maps.Add(new MazeMap(i, RawNames[i], Display(i, RawNames[i]), BuiltInMazes.Records[i]));
        return new MazeData(maps, isExact: false);
    }

    // --- live current-map fingerprinting ----------------------------------------
    // The game loads the current 16×16 maze into a RAM buffer byte-for-byte. Scanning the
    // attached process for the 256-byte plane that matches one of the 55 known records
    // identifies the current map exactly — no map-id offset needed.

    private static ulong Prefix(byte[] b, int off)
    {
        ulong k = 0;
        for (int i = 0; i < 8; i++) k = (k << 8) | b[off + i];
        return k;
    }

    /// <summary>
    /// How many of a plane's 1024 two-bit fields may differ and the plane still count as this
    /// map. Only used for the bundled data, where a one-sided door reaches this build recorded
    /// from one side only — around eight fields per map, so this leaves ample headroom while
    /// staying far below the hundreds of fields that separate any two different mazes.
    /// </summary>
    private const int MaxFieldMismatch = 48;

    /// <summary>
    /// Index of the map whose fingerprint matches the 256 bytes of <paramref name="window"/>
    /// starting at <paramref name="off"/>, or -1.
    ///
    /// <para>With exact data that is the wall-graphic plane, matched byte for byte, which
    /// cannot false-positive. With the bundled data it is the passability plane, matched
    /// within <see cref="MaxFieldMismatch"/> and only when no second map comes close — a
    /// near-match across 1024 fields is still far beyond coincidence.</para>
    /// </summary>
    public int MatchAt(byte[] window, int off)
    {
        if (off < 0 || off + FingerprintLength > window.Length) return -1;

        if (IsExact)
        {
            if (!_prefixIndex.TryGetValue(Prefix(window, off), out var candidates)) return -1;
            foreach (int idx in candidates)
            {
                var p = Maps[idx].Plane1!;
                int k = 0;
                while (k < 256 && window[off + k] == p[k]) k++;
                if (k == 256) return idx;
            }
            return -1;
        }

        // Counting the window's non-blank fields once costs 256 lookups and then rejects almost
        // every candidate in a single comparison: a differing field moves that count by at most
        // one, so a count more than MaxFieldMismatch apart guarantees the distance is too. That
        // is what keeps the whole-segment scan affordable — without it every offset would walk
        // all 55 planes byte by byte.
        int windowFields = CountNonZeroFields(window, off);

        int best = -1, bestScore = int.MaxValue, runnerUp = int.MaxValue;
        for (int i = 0; i < Maps.Count; i++)
        {
            if (Math.Abs(windowFields - _blockedFields[i]) > MaxFieldMismatch) continue;
            int d = FieldDistance(window, off, Maps[i].Plane2);
            if (d < bestScore) { runnerUp = bestScore; bestScore = d; best = i; }
            else if (d < runnerUp) runnerUp = d;
        }
        return bestScore <= MaxFieldMismatch && runnerUp > MaxFieldMismatch ? best : -1;
    }

    /// <summary>
    /// How many of a byte's four two-bit fields are non-zero. Serves twice over: on a plane's
    /// own bytes it counts the edges that are not plain open ground, and on the XOR of two
    /// planes it counts the edges they disagree about.
    /// </summary>
    private static readonly byte[] NonZeroFields = BuildNonZeroFields();

    private static byte[] BuildNonZeroFields()
    {
        var table = new byte[256];
        for (int v = 0; v < 256; v++)
        {
            int n = 0;
            for (int dir = 0; dir < 4; dir++) if (((v >> (dir * 2)) & 3) != 0) n++;
            table[v] = (byte)n;
        }
        return table;
    }

    private static int CountNonZeroFields(byte[] bytes, int off = 0)
    {
        int n = 0;
        for (int k = 0; k < 256; k++) n += NonZeroFields[bytes[off + k]];
        return n;
    }

    /// <summary>
    /// Counts differing two-bit fields between a 256-byte window and a plane, giving up as
    /// soon as the count passes <see cref="MaxFieldMismatch"/>, so a candidate that survived
    /// the field-count filter still costs only the few bytes it takes to rule it out.
    /// </summary>
    private static int FieldDistance(byte[] window, int off, byte[] plane)
    {
        int differing = 0;
        for (int k = 0; k < 256; k++)
        {
            differing += NonZeroFields[window[off + k] ^ plane[k]];
            if (differing > MaxFieldMismatch) return differing;
        }
        return differing;
    }

    /// <summary>Scans a memory buffer for the live maze; returns the matched map index and the
    /// byte offset it was found at, or (-1, -1).</summary>
    public (int Map, int Offset) FindInBuffer(byte[] buffer)
    {
        for (int off = 0; off + FingerprintLength <= buffer.Length; off++)
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
