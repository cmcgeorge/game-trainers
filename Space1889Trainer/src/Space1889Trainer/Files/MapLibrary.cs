using Space1889Trainer.Game;

namespace Space1889Trainer.Files;

/// <summary>A map the library knows about.</summary>
public sealed record MapEntry(int Id, string File)
{
    public int Planet => Id / 100;
    public int Area => Id / 10 % 10;
    public int MapNumber => Id % 10;

    /// <summary>"Earth › London › map 3", "Shared pub interior", "Space".</summary>
    public string Title => Id switch
    {
        992 => "Pub interior (shared by every city)",
        999 => "Inn interior (shared by every city)",
        _ when File.Equals("0.SYS", StringComparison.OrdinalIgnoreCase) => "Space (navigation chart)",
        _ => PlaceBook.Describe(Planet, Area, MapNumber),
    };

    public override string ToString() => $"{Id,3}  {Title}";
}

/// <summary>How a map is drawn.</summary>
public enum MapRenderMode
{
    /// <summary>With the game's own 16 × 16 tiles, read from the player's installation.</summary>
    Tiles,
    /// <summary>Flat colours per <see cref="TileClass"/>: small, and free of the game's artwork.</summary>
    Schematic,
}

/// <summary>A rendered map: BGRA32 pixels plus the cell size used.</summary>
public sealed record MapImage(int Width, int Height, int CellPixels, byte[] Bgra);

/// <summary>
/// Everything map-related the trainer reads from a Space 1889 folder: the three SYS files, the SET and NPC files,
/// and the tile sheets. Files are read on first use and cached; nothing is written.
/// </summary>
public sealed class MapLibrary
{
    private readonly Dictionary<string, byte[]?> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Picture?> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, GameMap?> _maps = new();
    private IReadOnlyList<MapEntry>? _entries;

    public MapLibrary(string folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    public string Folder { get; }

    public byte[]? File(string name)
    {
        if (!_files.TryGetValue(name, out var bytes))
            _files[name] = bytes = GameFolder.ReadFile(Folder, name);
        return bytes;
    }

    /// <summary>Every map in A.SYS, B.SYS and 0.SYS (the shared 992/999 interiors listed once, from A.SYS).</summary>
    public IReadOnlyList<MapEntry> Maps => _entries ??= BuildEntries();

    private List<MapEntry> BuildEntries()
    {
        var list = new List<MapEntry>();
        var seen = new HashSet<int>();
        foreach (var name in new[] { "A.SYS", "B.SYS" })
            if (File(name) is { } sys)
                foreach (var id in MapFile.ReadIndex(sys).Keys)
                    if (seen.Add(id)) list.Add(new MapEntry(id, name));
        if (File("0.SYS") is { } space && MapFile.ReadIndex(space).ContainsKey(0))
            list.Add(new MapEntry(0, "0.SYS"));
        return list.OrderBy(e => e.File == "0.SYS" ? -1 : e.Id).ToList();
    }

    /// <summary>
    /// Loads a map. <paramref name="currentPlanet"/> picks the file for the shared 992/999 interiors (B.SYS from
    /// Mars onwards); for other ids the id decides.
    /// </summary>
    public GameMap? Load(int mapId, int currentPlanet = 1, bool space = false)
    {
        string file = space ? "0.SYS" : mapId is 992 or 999 ? (currentPlanet >= 4 ? "B.SYS" : "A.SYS") : MapFile.FileFor(mapId);
        int key = space ? -1 : mapId * 10 + (file == "B.SYS" ? 1 : 0);
        if (_maps.TryGetValue(key, out var cached)) return cached;
        var map = File(file) is { } sys ? MapFile.Load(sys, space ? 0 : mapId) : null;
        _maps[key] = map;
        return map;
    }

    /// <summary>Area record (dark maps, entry positions) for a city, or null.</summary>
    public AreaInfo? Area(int planet, int area) =>
        File(AreaFiles.SetFileFor(planet)) is { } set ? AreaFiles.ReadArea(set, planet, area) : null;

    /// <summary>
    /// NPCs placed on a map. For the shared pub/inn (992/999) the list comes from the city the party is in,
    /// keyed <c>planet·100 + area·10 + 2</c> or <c>+ 9</c>.
    /// </summary>
    public IReadOnlyList<NpcInfo> Npcs(int mapId, int planet, int area)
    {
        int key = mapId is 992 or 999 ? planet * 100 + area * 10 + mapId % 10 : mapId;
        int p = mapId is 992 or 999 ? planet : mapId / 100;
        if (p is < 1 or > 6) return Array.Empty<NpcInfo>();
        return File(AreaFiles.NpcFileFor(p)) is { } npc ? AreaFiles.ReadNpcs(npc, key) : Array.Empty<NpcInfo>();
    }

    /// <summary>The DEF (sheet 0) or SPR (sheet 1) tile sheet a planet's maps draw from.</summary>
    public Picture? Sheet(int planet, int sheet)
    {
        string name = sheet == 0
            ? (planet == 6 ? "6.DEF" : planet == 0 ? "0.DEF" : "1.DEF")
            : $"{Math.Clamp(planet, 1, 6)}.SPR";
        if (!_sheets.TryGetValue(name, out var pic))
            _sheets[name] = pic = File(name) is { } bytes ? PicFile.Decode(bytes) : null;
        return pic;
    }

    // --- rendering ------------------------------------------------------------------------

    /// <summary>Schematic colours (0xRRGGBB) per <see cref="TileClass"/>.</summary>
    public static readonly IReadOnlyDictionary<TileClass, int> SchematicColors = new Dictionary<TileClass, int>
    {
        [TileClass.Floor] = 0xC9C2A8,
        [TileClass.Blocked] = 0x3A3F4B,
        [TileClass.Water] = 0x3D6FB8,
        [TileClass.Door] = 0xB07A3C,
        [TileClass.Exit] = 0xE8C33A,
        [TileClass.Service] = 0x2FB7B0,
        [TileClass.Entrance] = 0xE0762C,
        [TileClass.Void] = 0x000000,
    };

    /// <summary>Renders a schematic with no tile sheets at all (usable without a game folder's graphics).</summary>
    public static MapImage RenderSchematic(GameMap map, int cellPixels)
    {
        ArgumentNullException.ThrowIfNull(map);
        cellPixels = Math.Clamp(cellPixels, 1, 32);
        int w = map.Columns * cellPixels, h = map.Rows * cellPixels;
        var bgra = new byte[w * h * 4];
        for (int r = 0; r < map.Rows; r++)
            for (int c = 0; c < map.Columns; c++)
            {
                int rgb = SchematicColors[TileRules.Classify(map[r, c])];
                for (int py = 0; py < cellPixels; py++)
                {
                    int dst = ((r * cellPixels + py) * w + c * cellPixels) * 4;
                    for (int px = 0; px < cellPixels; px++, dst += 4)
                    {
                        bgra[dst] = (byte)rgb;
                        bgra[dst + 1] = (byte)(rgb >> 8);
                        bgra[dst + 2] = (byte)(rgb >> 16);
                        bgra[dst + 3] = 0xFF;
                    }
                }
            }
        return new MapImage(w, h, cellPixels, bgra);
    }

    /// <summary>
    /// Renders <paramref name="map"/>. In <see cref="MapRenderMode.Tiles"/> mode <paramref name="cellPixels"/> is a
    /// divisor-friendly 16, 8 or 4 (tiles are point-sampled down); if a sheet is missing that cell falls back to its
    /// schematic colour. <paramref name="tilePlanet"/> picks the sheets (the pub/inn use the current planet's).
    /// </summary>
    public MapImage Render(GameMap map, MapRenderMode mode, int cellPixels, int tilePlanet)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (mode == MapRenderMode.Schematic) return RenderSchematic(map, cellPixels);
        cellPixels = Math.Clamp(cellPixels, 1, 16);
        int w = map.Columns * cellPixels, h = map.Rows * cellPixels;
        var bgra = new byte[w * h * 4];
        bool space = map.Id == 0 && tilePlanet == 0;
        Picture? def = Sheet(tilePlanet, 0);
        Picture? spr = space ? null : Sheet(tilePlanet, 1);
        int step = 16 / Math.Max(1, Math.Min(16, cellPixels));

        for (int r = 0; r < map.Rows; r++)
        {
            for (int c = 0; c < map.Columns; c++)
            {
                var cell = map[r, c];
                Picture? sheet = cell.Sheet == 0 ? def : cell.Sheet == 1 ? spr : null;
                int tile = cell.Tile;
                if (space && cell.Sheet == 0) tile -= 60;   // 0.DEF sits three tile rows down its page [Inferred]
                int sx = tile % 20 * 16, sy = tile / 20 * 16;
                bool haveTile = sheet is not null && tile >= 0 && sy + 16 <= sheet.Height && sx + 16 <= sheet.Width;
                int flat = SchematicColors[TileRules.Classify(cell)];

                for (int py = 0; py < cellPixels; py++)
                {
                    int dst = ((r * cellPixels + py) * w + c * cellPixels) * 4;
                    for (int px = 0; px < cellPixels; px++, dst += 4)
                    {
                        int rgb = haveTile
                            ? PicFile.EgaPalette[sheet!.Pixels[(sy + py * step) * sheet.Width + sx + px * step]]
                            : flat;
                        bgra[dst] = (byte)rgb;
                        bgra[dst + 1] = (byte)(rgb >> 8);
                        bgra[dst + 2] = (byte)(rgb >> 16);
                        bgra[dst + 3] = 0xFF;
                    }
                }
            }
        }
        return new MapImage(w, h, cellPixels, bgra);
    }
}
