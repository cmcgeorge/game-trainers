namespace AlternateRealityTrainer.Game;

/// <summary>What occupies one square of The City.</summary>
public enum TerrainKind
{
    /// <summary>Open street — you can walk here.</summary>
    Street,

    /// <summary>A doorway into an inn, tavern, bank, shop, smithy, healer or guild.</summary>
    Doorway,

    /// <summary>A building block. Solid; you walk around it.</summary>
    Building,

    /// <summary>The city boundary and interior dividing walls. Solid.</summary>
    Wall,

    /// <summary>Open scenery beyond the streets — the mountains you can see but not enter. [Inferred]</summary>
    Scenery,
}

/// <summary>
/// The City's street map: 64 × 64 squares, one byte each, exactly as the game holds it.
///
/// The game keeps this at <see cref="CharacterFormat.DgroupTerrainOffset"/> relative to its data
/// segment, loaded verbatim out of <c>CITY.EXE</c> and never modified during play. The trainer reads
/// it from the attached game (or from the player's own copy of <c>CITY.EXE</c>) rather than shipping
/// it, because it is the game's copyrighted data.
///
/// <para>Byte layout, recovered by scoring every 4,096-byte window in <c>CITY.EXE</c> against the 60
/// building squares whose coordinates the shipped hint file lists. The search used a richer score
/// than <see cref="MatchingKnownPlaces"/> — it also rewarded each building type getting a
/// <i>distinct</i> code — and out of a possible 95 the correct window scored 92 against a runner-up
/// of 59. That search score is history; the run-time check below is the simple 60-square count:</para>
/// <list type="bullet">
/// <item>the low nibble is the <b>location type</b>: 0 none, 1 Inn, 2 Tavern, 3 Bank, 4 Shop,
///   5 Smithy, 6 scenery, 7 Healer, 8 Guild — matching every known coordinate;</item>
/// <item><c>0x40</c> marks a <b>building block</b> and <c>0x20</c> a <b>wall</b>; a square with
///   either is solid. <c>0x60</c> is both.</item>
/// </list>
///
/// <para>Row 0 of the array is north 64 and column 0 is east 1, so the array reads exactly like the
/// drawn map: north at the top, east to the right.</para>
/// </summary>
public sealed class CityTerrain
{
    /// <summary>Squares per side.</summary>
    public const int Size = GameFacts.CitySize;

    /// <summary>Bytes in the map — one per square.</summary>
    public const int ByteCount = Size * Size;

    private readonly byte[] _cells;

    private CityTerrain(byte[] cells) => _cells = cells;

    /// <summary>The raw byte for the square at <paramref name="north"/>, <paramref name="east"/> (1-based).</summary>
    public byte Raw(int north, int east)
    {
        if (north < 1 || north > Size)
            throw new ArgumentOutOfRangeException(nameof(north), north, $"North is 1..{Size}.");
        if (east < 1 || east > Size)
            throw new ArgumentOutOfRangeException(nameof(east), east, $"East is 1..{Size}.");
        return _cells[(Size - north) * Size + (east - 1)];
    }

    /// <summary>The location type stored in the low nibble, or <see cref="PlaceKind"/>-less 0.</summary>
    public int LocationCode(int north, int east) => Raw(north, east) & 0x0F;

    /// <summary>What occupies the square.</summary>
    public TerrainKind KindAt(int north, int east)
    {
        byte v = Raw(north, east);
        if ((v & 0x40) != 0) return TerrainKind.Building;
        if ((v & 0x20) != 0) return TerrainKind.Wall;
        int code = v & 0x0F;
        if (code == SceneryCode) return TerrainKind.Scenery;
        return code == 0 ? TerrainKind.Street : TerrainKind.Doorway;
    }

    /// <summary>True when a character could stand on the square.</summary>
    public bool IsWalkable(int north, int east)
    {
        var kind = KindAt(north, east);
        return kind is TerrainKind.Street or TerrainKind.Doorway;
    }

    /// <summary>The low-nibble code for open scenery. [Inferred]</summary>
    public const int SceneryCode = 6;

    /// <summary>Maps a low-nibble location code to the building type, or null when it is not a building.</summary>
    public static PlaceKind? PlaceKindForCode(int code) => code switch
    {
        1 => PlaceKind.Inn,
        2 => PlaceKind.Tavern,
        3 => PlaceKind.Bank,
        4 => PlaceKind.Shop,
        5 => PlaceKind.Smithy,
        7 => PlaceKind.Healer,
        8 => PlaceKind.Guild,
        _ => null,
    };

    /// <summary>How many squares carry each terrain kind — a quick sanity read-out for the UI.</summary>
    public IReadOnlyDictionary<TerrainKind, int> Census()
    {
        var counts = new Dictionary<TerrainKind, int>();
        foreach (var kind in Enum.GetValues<TerrainKind>()) counts[kind] = 0;
        for (int n = 1; n <= Size; n++)
            for (int e = 1; e <= Size; e++)
                counts[KindAt(n, e)]++;
        return counts;
    }

    /// <summary>
    /// How many of the <see cref="CityBook.Places"/> squares carry the location code they should.
    /// This is the check that identified the map in the first place, so it is also the right way to
    /// decide whether a candidate block really is the map.
    /// </summary>
    public int MatchingKnownPlaces()
    {
        int matched = 0;
        foreach (var p in CityBook.Places)
            if (PlaceKindForCode(LocationCode(p.North, p.East)) == p.Kind)
                matched++;
        return matched;
    }

    /// <summary>
    /// The share of <see cref="CityBook.Places"/> that a block must explain before it is accepted as
    /// the map. The real map scores <b>57 of 60</b> (95 %) — one square each of the shop, smithy and
    /// guild lists reads as plain street, most likely because the hint file records the square you
    /// approach from rather than the doorway itself. Hence a threshold well below 100 %, but far
    /// above what unrelated data reaches.
    /// </summary>
    public const double MinimumKnownPlaceMatch = 0.80;

    /// <summary>
    /// Parses <paramref name="raw"/> as the city map, returning null unless it explains enough of the
    /// known building squares to be the real thing.
    /// </summary>
    public static CityTerrain? TryParse(byte[]? raw, int offset = 0)
    {
        if (!Fits(raw, offset)) return null;
        if (ScoreAt(raw!, offset) < CityBook.Places.Count * MinimumKnownPlaceMatch) return null;
        var cells = new byte[ByteCount];
        Array.Copy(raw!, offset, cells, 0, ByteCount);
        return new CityTerrain(cells);
    }

    private static bool Fits(byte[]? raw, int offset) =>
        raw != null && offset >= 0 && offset <= raw.Length - ByteCount;

    /// <summary>
    /// Scores a candidate window <b>in place</b>: how many of <see cref="CityBook.Places"/> carry the
    /// location code they should. Copying 4 KB per offset before scoring made the whole-file sweep
    /// below allocate hundreds of megabytes for nothing.
    /// </summary>
    private static int ScoreAt(byte[] raw, int offset)
    {
        int matched = 0;
        foreach (var p in CityBook.Places)
        {
            int code = raw[offset + (Size - p.North) * Size + (p.East - 1)] & 0x0F;
            if (PlaceKindForCode(code) == p.Kind) matched++;
        }
        return matched;
    }

    /// <summary>
    /// Finds the map inside a copy of <c>CITY.EXE</c>. The shipped build keeps it at
    /// <see cref="CityExeOffset"/>, but the file is swept anyway so a different build still works.
    /// </summary>
    public static CityTerrain? FromCityExe(byte[]? image)
    {
        if (image == null || image.Length < ByteCount) return null;

        var atKnownOffset = TryParse(image, CityExeOffset);
        if (atKnownOffset != null) return atKnownOffset;

        int bestOffset = -1, bestScore = (int)(CityBook.Places.Count * MinimumKnownPlaceMatch) - 1;
        for (int i = 0; i <= image.Length - ByteCount; i++)
        {
            int score = ScoreAt(image, i);
            if (score > bestScore) { bestScore = score; bestOffset = i; }
        }
        return bestOffset >= 0 ? TryParse(image, bestOffset) : null;
    }

    /// <summary>Where the map sits in the shipped <c>CITY.EXE</c> (332,160 bytes).</summary>
    public const int CityExeOffset = 0x279F0;
}
