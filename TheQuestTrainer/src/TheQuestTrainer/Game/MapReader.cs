using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>Which way the player is facing, as the game's four quarter-turns.</summary>
public enum Heading
{
    /// <summary>The facing word held something other than a quarter turn.</summary>
    Unknown = 0,

    /// <summary>0°.</summary>
    North,

    /// <summary>90°.</summary>
    West,

    /// <summary>180°.</summary>
    South,

    /// <summary>270°.</summary>
    East,
}

/// <summary>
/// One of the world's maps, as the world's own vector holds it.
///
/// A map is either a cell of the outdoor grid — 21×21 tiles, an id ending in its column and row —
/// or a standalone interior, 35×35 with a name for an id and no place in the world.
/// </summary>
public sealed record WorldMap
{
    /// <summary>Where the map object lives.</summary>
    public required uint Address { get; init; }

    /// <summary>The game's internal id, e.g. <c>base_s0804</c> or <c>base_house7</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name the game shows, e.g. <c>Port of Mithria</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Width in tiles.</summary>
    public required int Width { get; init; }

    /// <summary>Height in tiles.</summary>
    public required int Height { get; init; }

    /// <summary>The map's flag word — see the <c>Flag*</c> constants on <see cref="MapLayout"/>.</summary>
    public required ushort Flags { get; init; }

    /// <summary>One-based column of the outdoor grid, or null for an interior.</summary>
    public required int? Column { get; init; }

    /// <summary>One-based row of the outdoor grid, or null for an interior.</summary>
    public required int? Row { get; init; }

    /// <summary>Whether the map is a cell of the outdoor grid.</summary>
    public bool IsOutdoorCell => Column is not null && Row is not null;

    /// <summary>Where the map's north-west corner sits in world-absolute tiles, for a grid map.</summary>
    public int? OriginX => Column is { } c ? MapLayout.CellOriginTile(c) : null;

    /// <inheritdoc cref="OriginX"/>
    public int? OriginY => Row is { } r ? MapLayout.CellOriginTile(r) : null;

    /// <summary>
    /// Where the map is laid into the tile window: the draw border for an outdoor cell, the origin
    /// for an interior. The game's own local-to-window helper branches on the same flag.
    /// </summary>
    public int WindowOrigin(int border) => (Flags & MapLayout.FlagOffsetByBorder) != 0 ? border : 0;

    /// <summary>"8, 4" for an outdoor cell, a dash for an interior.</summary>
    public string CellLabel => IsOutdoorCell ? $"{Column}, {Row}" : "—";

    /// <summary>"21×21", for the atlas.</summary>
    public string SizeLabel => $"{Width}×{Height}";

    /// <summary>What the flag word says, in the game's own terms.</summary>
    public string Notes
    {
        get
        {
            var parts = new List<string>();
            if ((Flags & MapLayout.FlagTeleportDenied) != 0) parts.Add("Teleport magic denied");
            if ((Flags & MapLayout.FlagMarkDenied) != 0) parts.Add("Mark denied");
            if ((Flags & MapLayout.FlagRecallTarget) != 0) parts.Add("Recall target");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>Where the player is, read in one pass off the engine manager, the world and the map.</summary>
public sealed record MapSnapshot
{
    /// <summary>The engine object the character record is embedded in.</summary>
    public required uint Engine { get; init; }

    /// <summary>The <c>SEngineManager</c> — the object holding the live position.</summary>
    public required uint Manager { get; init; }

    /// <summary>The world the player is in.</summary>
    public required uint World { get; init; }

    /// <summary>The map the player is standing on.</summary>
    public required uint Map { get; init; }

    /// <summary>The world's name, e.g. <c>Freymore</c>.</summary>
    public required string WorldName { get; init; }

    /// <summary>The resource pack the world's art is in, e.g. <c>base</c>.</summary>
    public required string WorldPack { get; init; }

    /// <summary>The prefix the outdoor grid's ids are built from, e.g. <c>base_s</c>.</summary>
    public required string GridPrefix { get; init; }

    /// <summary>The resource id of the world's map picture, e.g. <c>base_-WORLDMAP-</c>.</summary>
    public required string PictureId { get; init; }

    /// <summary>The current map, decoded the same way every entry in the atlas is.</summary>
    public required WorldMap Here { get; init; }

    /// <summary>Side of the engine's square tile window.</summary>
    public required int WindowSize { get; init; }

    /// <summary>How far in from the window's edge an outdoor map is laid — the draw distance.</summary>
    public required int WindowBorder { get; init; }

    /// <summary>Whether the outdoor three-by-three block is loaded rather than a single interior.</summary>
    public required bool Outdoors { get; init; }

    /// <summary>The player's column in the tile window. This is the field a teleport writes.</summary>
    public required int WindowX { get; init; }

    /// <summary>The player's row in the tile window.</summary>
    public required int WindowY { get; init; }

    /// <summary>The facing word, in degrees anticlockwise from north.</summary>
    public required int FacingDegrees { get; init; }

    /// <summary>
    /// The world-absolute tile the engine has cached. Only maintained outdoors, and it lags a
    /// teleport by one of the player's own steps, so the trainer shows
    /// <see cref="GlobalX"/> — derived from the live window position — and keeps this for the atlas.
    /// </summary>
    public required int CachedWorldTileX { get; init; }

    /// <inheritdoc cref="CachedWorldTileX"/>
    public required int CachedWorldTileY { get; init; }

    /// <summary>Where the current map starts inside the tile window.</summary>
    public int Origin => Here.WindowOrigin(WindowBorder);

    /// <summary>The player's column within the current map, counting from its north-west corner.</summary>
    public int LocalX => WindowX - Origin;

    /// <inheritdoc cref="LocalX"/>
    public int LocalY => WindowY - Origin;

    /// <summary>Whether the player is standing on a tile of the map the engine says they are on.</summary>
    public bool IsOnMap =>
        LocalX >= 0 && LocalY >= 0 && LocalX < Here.Width && LocalY < Here.Height;

    /// <summary>World-absolute tile column, for an outdoor cell; null inside an interior.</summary>
    public int? GlobalX => Here.OriginX is { } o ? o + LocalX : null;

    /// <inheritdoc cref="GlobalX"/>
    public int? GlobalY => Here.OriginY is { } o ? o + LocalY : null;

    /// <summary>The facing word as one of the four compass points.</summary>
    public Heading Heading => FacingDegrees switch
    {
        0 => Heading.North,
        90 => Heading.West,
        180 => Heading.South,
        270 => Heading.East,
        _ => Heading.Unknown,
    };

    /// <summary>"North", or the raw angle when the game is mid-turn.</summary>
    public string HeadingLabel =>
        Heading == Heading.Unknown ? $"{FacingDegrees}°" : Heading.ToString();
}

/// <summary>
/// Reads where the player is, and what maps the world they are in contains.
///
/// Two entry points with very different costs. <see cref="Read"/> is eight reads and runs on the
/// refresh; <see cref="ReadAtlas"/> walks the world's whole map vector — 239 maps in Freymore, four
/// reads each — and runs on attach and on the explicit rescan, the same way the item catalog does.
///
/// <b>Everything is validated against the engine object.</b> The world and every map carry a
/// back-pointer to it, exactly as an item type does, so "is this pointer really a world" is one
/// comparison rather than a guess. That matters because the manager pointer chain is three
/// dereferences deep and the game nulls the middle of it between a save being loaded and the new
/// game being built.
/// </summary>
public static class MapReader
{
    /// <summary>
    /// Snapshots the player's position, or null when the chain from the character record to a
    /// validated world and map cannot be followed — which is the normal state on the title screen.
    /// </summary>
    public static MapSnapshot? Read(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        uint engine = record - QuestLayout.RecordInEngine;

        if (!TryReadUInt32(source, engine + MapLayout.EngineManager, out uint manager) || manager == 0) return null;
        if (!TryReadUInt32(source, manager + MapLayout.World, out uint world) || world == 0) return null;
        if (!TryReadUInt32(source, manager + MapLayout.Map, out uint map) || map == 0) return null;

        var worldBytes = new byte[MapLayout.WorldBytes];
        if (source.Read(world, worldBytes, worldBytes.Length) != worldBytes.Length) return null;
        if (BitConverter.ToUInt32(worldBytes, (int)MapLayout.WorldEngine) != engine) return null;

        string? worldName = StdString.Read(source, worldBytes, (int)MapLayout.WorldName);
        string? pack = StdString.Read(source, worldBytes, (int)MapLayout.WorldPack);
        string? gridPrefix = StdString.Read(source, worldBytes, (int)MapLayout.WorldGridPrefix);
        string? picture = StdString.Read(source, worldBytes, (int)MapLayout.WorldMapPicture);
        if (worldName is null || pack is null || gridPrefix is null || picture is null) return null;

        var here = ReadMap(source, map, engine, world, gridPrefix);
        if (here is null) return null;

        // The window's geometry is on the engine object, not the manager: the game sizes it once at
        // startup from the configured draw distance and never moves it.
        var window = new byte[8];
        if (source.Read(engine + MapLayout.WindowBorder, window, window.Length) != window.Length) return null;
        int border = BitConverter.ToInt32(window, 0);
        int size = BitConverter.ToInt32(window, 4);
        if (border < 0 || size <= 0 || size > MapLayout.MaxMapTiles || border >= size) return null;

        // Facing, then the position, in one read of the span that holds both.
        var live = new byte[MapLayout.PlayerY + 4 - MapLayout.Facing];
        if (source.Read(manager + MapLayout.Facing, live, live.Length) != live.Length) return null;

        var outdoors = new byte[1];
        if (source.Read(manager + MapLayout.Outdoors, outdoors, 1) != 1) return null;

        return new MapSnapshot
        {
            Engine = engine,
            Manager = manager,
            World = world,
            Map = map,
            WorldName = worldName,
            WorldPack = pack,
            GridPrefix = gridPrefix,
            PictureId = picture,
            Here = here,
            WindowSize = size,
            WindowBorder = border,
            Outdoors = outdoors[0] != 0,
            FacingDegrees = BitConverter.ToInt32(live, 0),
            WindowX = BitConverter.ToInt32(live, (int)(MapLayout.PlayerX - MapLayout.Facing)),
            WindowY = BitConverter.ToInt32(live, (int)(MapLayout.PlayerY - MapLayout.Facing)),
            CachedWorldTileX = BitConverter.ToInt32(worldBytes, (int)MapLayout.WorldTileX),
            CachedWorldTileY = BitConverter.ToInt32(worldBytes, (int)MapLayout.WorldTileY),
        };
    }

    /// <summary>
    /// Every map in the world the player is in, in the world's own order.
    ///
    /// This is the trainer's map reference, and it is read out of the running game rather than baked
    /// in, so it is right for the expansion and for any build: the world says how many maps it has
    /// and each one carries its own id, name, size and flags.
    /// </summary>
    public static IReadOnlyList<WorldMap> ReadAtlas(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        var snapshot = Read(source, record);
        if (snapshot is null) return Array.Empty<WorldMap>();

        if (!TryReadUInt32(source, snapshot.World + MapLayout.WorldMapsBegin, out uint begin)) return Array.Empty<WorldMap>();
        if (!TryReadUInt32(source, snapshot.World + MapLayout.WorldMapsEnd, out uint end)) return Array.Empty<WorldMap>();

        int count = VectorLength(begin, end);
        if (count <= 0) return Array.Empty<WorldMap>();

        var pointers = new byte[count * 4];
        if (source.Read(begin, pointers, pointers.Length) != pointers.Length) return Array.Empty<WorldMap>();

        var maps = new List<WorldMap>(count);
        for (int i = 0; i < count; i++)
        {
            uint address = BitConverter.ToUInt32(pointers, i * 4);
            var map = ReadMap(source, address, snapshot.Engine, snapshot.World, snapshot.GridPrefix);
            if (map is not null) maps.Add(map);
        }
        return maps;
    }

    /// <summary>
    /// Reads one map object, or null when it does not validate.
    ///
    /// Three checks: the back-pointers to the engine and the world both match, the size is a
    /// plausible one, and the id and name are readable C strings. Together they are what let the
    /// atlas walk a vector of pointers without trusting any of them.
    /// </summary>
    public static WorldMap? ReadMap(IMemorySource source, uint address, uint engine, uint world, string gridPrefix)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (address == 0 || (address & 3) != 0) return null;

        var buffer = new byte[MapLayout.MapBytes];
        if (source.Read(address, buffer, buffer.Length) != buffer.Length) return null;

        if (BitConverter.ToUInt32(buffer, (int)MapLayout.MapEngine) != engine) return null;
        if (BitConverter.ToUInt32(buffer, (int)MapLayout.MapWorld) != world) return null;

        int width = BitConverter.ToInt32(buffer, (int)MapLayout.MapWidth);
        int height = BitConverter.ToInt32(buffer, (int)MapLayout.MapHeight);
        if (width <= 0 || height <= 0 || width > MapLayout.MaxMapTiles || height > MapLayout.MaxMapTiles) return null;

        string? id = ItemTypeReader.ReadText(source, BitConverter.ToUInt32(buffer, (int)MapLayout.MapId));
        string? name = ItemTypeReader.ReadText(source, BitConverter.ToUInt32(buffer, (int)MapLayout.MapName));
        if (id is null || name is null) return null;

        var cell = MapLayout.CellFromId(id, gridPrefix);
        return new WorldMap
        {
            Address = address,
            Id = id,
            Name = name,
            Width = width,
            Height = height,
            Flags = BitConverter.ToUInt16(buffer, (int)MapLayout.MapFlags),
            Column = cell?.Column,
            Row = cell?.Row,
        };
    }

    /// <summary>
    /// Elements in a <c>std::vector</c> of dwords, or -1 when the two pointers are not a plausible
    /// one.
    /// </summary>
    private static int VectorLength(uint begin, uint end)
    {
        if (begin == 0 && end == 0) return 0;
        if (begin == 0 || end < begin) return -1;
        uint bytes = end - begin;
        if (bytes % 4 != 0) return -1;
        uint count = bytes / 4;
        return count > MapLayout.MaxMaps ? -1 : (int)count;
    }

    private static bool TryReadUInt32(IMemorySource source, uint address, out uint value)
    {
        var word = new byte[4];
        if (source.Read(address, word, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToUInt32(word, 0);
        return true;
    }
}
