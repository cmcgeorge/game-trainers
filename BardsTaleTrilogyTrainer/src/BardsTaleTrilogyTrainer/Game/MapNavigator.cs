using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>Where the party is right now, as read from the live <c>Player</c> and <c>GameMap</c>.</summary>
public sealed record PartyLocation(
    GameChapter Chapter,
    bool IsDungeon,
    int MapIndex,
    string MapName,
    int Width,
    int Height,
    int Level,
    int X,
    int Z,
    Facing Facing,
    bool IsWilderness,
    bool IsOutside,
    bool IsTower)
{
    /// <summary>The catalogue entry for this map, when the build matches the shipped one.</summary>
    public GameMapInfo? Info => MapBook.Find(Chapter, IsDungeon, MapIndex);

    public string Kind => IsWilderness ? "wilderness" : IsDungeon ? "dungeon" : "city";

    public string Summary =>
        $"{MapBook.ChapterTag(Chapter)} · {MapName} ({Kind} {MapIndex}) · X {X} · Z {Z} · facing {Facing}";
}

/// <summary>One entry of the live map list read out of <c>GlobalMaps</c>.</summary>
public sealed record LiveMapEntry(bool IsDungeon, int Index, string Name, int Width, int Height, int Level);

/// <summary>
/// Reads the party's position and moves it, using the same route the game itself uses.
///
/// <para>Reading is a short pointer walk: <c>Player</c>'s class → its static block →
/// <c>Instance</c> → <c>m_gridX</c>/<c>m_gridZ</c>/<c>m_facing</c>, plus <c>m_map</c> for the
/// map's name, size and index.</para>
///
/// <para>Moving goes through <c>Player.m_queueTeleport</c>. Every state tick the game checks
/// that field, and when it holds a <c>TeleportTarget</c> with <c>m_isValid</c> set and
/// <c>m_teleportDone</c> clear it fades out, calls <c>LoadMap</c> for the destination map and
/// then <c>TeleportTo</c> for the square — loading a different map, running its startup
/// scripts and updating the automap on the way. Filling that field is therefore a real
/// teleport rather than a position poke, and it works across maps, not just within one.</para>
/// </summary>
public sealed class MapNavigator
{
    private readonly IMemorySource _mem;
    private readonly GameClasses _classes;

    /// <summary>Our own <c>TeleportTarget</c>, committed in the game on first use and reused after.</summary>
    private nuint _scratchTarget;

    public MapNavigator(IMemorySource mem, GameClasses classes)
    {
        _mem = mem;
        _classes = classes;
    }

    public GameClasses Classes => _classes;

    /// <summary>The live <c>Player</c> singleton, or 0 when the game has not created it yet.</summary>
    public nuint PlayerInstance => _mem.ReadStaticRef(_classes.Player);

    /// <summary>The live <c>GlobalMaps</c> singleton for the loaded chapter.</summary>
    public nuint GlobalMapsInstance =>
        _mem.ReadStaticRef(_classes.GlobalMaps, MapFormat.GlobalMapsInstanceStatic);

    /// <summary>Which of the three games is loaded (a static on <c>GlobalMaps</c>).</summary>
    public GameChapter Chapter
    {
        get
        {
            if (_classes.GlobalMaps == 0) return GameChapter.None;
            int raw = _mem.ReadStaticI32(_classes.GlobalMaps, MapFormat.GlobalMapsChapterStatic);
            return raw is >= 0 and <= 2 ? (GameChapter)raw : GameChapter.None;
        }
    }

    // --- reading ----------------------------------------------------------------
    /// <summary>
    /// Reads the party's current map and square, or null when no map is loaded (main menu,
    /// character creation, or mid-load).
    /// </summary>
    public PartyLocation? ReadLocation()
    {
        nuint player = PlayerInstance;
        if (player == 0) return null;

        nuint map = _mem.ReadPtr(player + MapFormat.PlayerMap);
        if (map == 0) return null;

        int width = _mem.ReadI32(map + MapFormat.GameMapWidth);
        int height = _mem.ReadI32(map + MapFormat.GameMapHeight);
        if (width <= 0 || height <= 0 || width > 512 || height > 512) return null;

        int x = _mem.ReadI32(player + MapFormat.PlayerGridX);
        int z = _mem.ReadI32(player + MapFormat.PlayerGridZ);
        int facing = _mem.ReadI32(player + MapFormat.PlayerFacing);
        int index = _mem.ReadI32(map + MapFormat.GameMapIndex);
        bool isDungeon = _mem.ReadBool(map + MapFormat.GameMapIsDungeon);

        string name = _mem.ReadManagedString(_mem.ReadPtr(map + MapFormat.GameMapName));
        if (name.Length == 0)
            name = MapBook.Find(Chapter, isDungeon, index)?.Name ?? $"map {index}";

        return new PartyLocation(
            Chapter, isDungeon, index, name, width, height,
            _mem.ReadI32(map + MapFormat.GameMapLevel),
            x, z, facing is >= 0 and <= 3 ? (Facing)facing : Facing.North,
            _mem.ReadBool(map + MapFormat.GameMapIsWilderness),
            _mem.ReadBool(map + MapFormat.GameMapIsOutside),
            _mem.ReadBool(map + MapFormat.GameMapIsTower));
    }

    /// <summary>
    /// Enumerates the loaded chapter's map arrays from <c>GlobalMaps</c>. This is what the
    /// running game will actually accept as a teleport destination, so it is the authority
    /// when an installation differs from the catalogue baked into <see cref="MapBook"/>.
    /// </summary>
    public List<LiveMapEntry> ReadLiveMaps()
    {
        var result = new List<LiveMapEntry>();
        nuint globals = GlobalMapsInstance;
        if (globals == 0) return result;

        Collect(globals + MapFormat.GlobalMapsCityMaps, isDungeon: false);
        Collect(globals + MapFormat.GlobalMapsDungeonMaps, isDungeon: true);
        return result;

        void Collect(nuint arrayField, bool isDungeon)
        {
            nuint array = _mem.ReadPtr(arrayField);
            int count = _mem.ReadArrayLength(array);
            if (count <= 0 || count > 512) return;
            for (int i = 0; i < count; i++)
            {
                nuint desc = _mem.ReadArrayRef(array, i);
                if (desc == 0) continue;
                result.Add(new LiveMapEntry(
                    isDungeon, i,
                    _mem.ReadManagedString(_mem.ReadPtr(desc + MapFormat.MapDescName)),
                    _mem.ReadI32(desc + MapFormat.MapDescWidth),
                    _mem.ReadI32(desc + MapFormat.MapDescHeight),
                    _mem.ReadI32(desc + MapFormat.MapDescLevel)));
            }
        }
    }

    // --- moving -----------------------------------------------------------------
    /// <summary>
    /// Answers whether the running game would survive being sent to <paramref name="map"/>.
    ///
    /// <para>This is the one check a teleport cannot do without. <c>TeleportTarget.m_map</c> is
    /// a bare index into the loaded chapter's own <c>m_cityMaps</c>/<c>m_dungeonMaps</c>, and
    /// <c>Player.LoadMap</c> indexes it without a bounds test — so a destination picked from
    /// another chapter is not "the wrong map", it is an <c>IndexOutOfRangeException</c> inside
    /// the game's state machine when the index is past the end, and a silent load of an
    /// unrelated map with mismatched dimensions when it is not. The picker lists all 121 maps
    /// of the trilogy at all times, so this is a click away rather than a corner case.</para>
    ///
    /// <para>The chapter is the first test; the live arrays are the second, because they are
    /// what the game will actually index and so remain authoritative if an installation ever
    /// differs from the catalogue in <see cref="MapBook"/>. A chapter that cannot be read at
    /// all is refused rather than assumed — there is no safe default here.</para>
    /// </summary>
    public bool AcceptsDestination(GameMapInfo map, out string message)
    {
        message = "";
        var chapter = Chapter;
        if (chapter == GameChapter.None)
        {
            message = "Which game is loaded could not be read, so a teleport cannot be aimed safely. " +
                      "Load a save and step into a map first.";
            return false;
        }

        if (map.Chapter != chapter)
        {
            message = $"{map.Name} belongs to {MapBook.ChapterName(map.Chapter)}, but " +
                      $"{MapBook.ChapterName(chapter)} is loaded. The game indexes each chapter's " +
                      "own map array, so this would load the wrong map or crash it.";
            return false;
        }

        // Bound against the array's declared length rather than against the descriptors that
        // read back, because the length is exactly what LoadMap indexes: a null element is the
        // game's own business, a past-the-end index is the crash. A length of 0 means the
        // arrays are not up yet and there is nothing to check against.
        var (cities, dungeons) = ReadMapArrayCounts();
        int available = map.IsDungeon ? dungeons : cities;
        if (available > 0 && (map.Index < 0 || map.Index >= available))
        {
            string kind = map.IsDungeon ? "dungeon" : "city";
            message = $"{map.Name} is {kind} map {map.Index}, but the running game only has " +
                      $"{available} {kind} map{(available == 1 ? "" : "s")}. This build's map table " +
                      "differs from the catalogue.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// The lengths of the loaded chapter's two map arrays, or (0, 0) when <c>GlobalMaps</c> has
    /// not been created yet. These are the bounds <c>Player.LoadMap</c> indexes without checking.
    /// </summary>
    public (int Cities, int Dungeons) ReadMapArrayCounts()
    {
        nuint globals = GlobalMapsInstance;
        if (globals == 0) return (0, 0);
        return (Length(MapFormat.GlobalMapsCityMaps), Length(MapFormat.GlobalMapsDungeonMaps));

        int Length(int field)
        {
            int n = _mem.ReadArrayLength(_mem.ReadPtr(globals + (nuint)field));
            return n is > 0 and <= 512 ? n : 0;
        }
    }

    /// <summary>
    /// Queues a teleport to <paramref name="map"/> square (<paramref name="x"/>,
    /// <paramref name="z"/>). The game performs it on its next state tick, so the call
    /// returns as soon as the request is in place.
    /// </summary>
    public bool TryTeleport(GameMapInfo map, int x, int z, Facing facing,
        TeleportType style, bool journal, out string message)
    {
        if (x < 0 || x >= map.Width || z < 0 || z >= map.Height)
        {
            message = $"({x}, {z}) is outside {map.Name}, which is {map.Width}×{map.Height}.";
            return false;
        }

        nuint player = PlayerInstance;
        if (player == 0)
        {
            message = "No party is in the world yet — load a game and step into a map first.";
            return false;
        }

        if (!AcceptsDestination(map, out message)) return false;

        nuint target = GetTeleportTarget(out string how);
        if (target == 0)
        {
            message = "Could not obtain a TeleportTarget to fill in. " +
                      "Run the trainer as administrator so it can allocate in the game.";
            return false;
        }

        // Disarm before refilling. The game polls this object every tick, so a target that is
        // still armed from a previous jump must not be seen half-rewritten.
        _mem.WriteBool(target + MapFormat.TeleportIsValid, false);

        bool ok = _mem.WriteBool(target + MapFormat.TeleportIsDungeon, map.IsDungeon);
        ok &= _mem.WriteBool(target + MapFormat.TeleportDoJournal, journal);
        ok &= _mem.WriteI32(target + MapFormat.TeleportMap, map.Index);
        ok &= _mem.WriteI32(target + MapFormat.TeleportX, x);
        ok &= _mem.WriteI32(target + MapFormat.TeleportZ, z);
        ok &= _mem.WriteI32(target + MapFormat.TeleportFacing, (int)facing);
        ok &= _mem.WriteI32(target + MapFormat.TeleportMapWidth, map.Width);
        ok &= _mem.WriteI32(target + MapFormat.TeleportMapHeight, map.Height);
        ok &= _mem.WriteI32(target + MapFormat.TeleportKind, (int)style);
        ok &= _mem.WriteI32(target + MapFormat.TeleportPreDelay, 0);          // float 0.0f
        ok &= _mem.WritePtr(target + MapFormat.TeleportPostJournal, 0);       // no follow-up message
        ok &= _mem.WriteBool(target + MapFormat.TeleportDone, false);

        // m_isValid last: the game polls this object, so it must be complete before it is armed.
        ok &= _mem.WriteBool(target + MapFormat.TeleportIsValid, true);
        ok &= _mem.WritePtr(player + MapFormat.PlayerQueueTeleport, target);

        message = ok
            ? $"Teleporting to {map.Name} at X {x} · Z {z}, facing {facing} ({how})."
            : "Some teleport fields could not be written — the game may have moved on.";
        return ok;
    }

    /// <summary>
    /// Writes the party's square directly, without the fade or a map load. Only valid within
    /// the current map; the view catches up on the next step or turn. Kept as a fallback for
    /// when a <c>TeleportTarget</c> cannot be obtained.
    /// </summary>
    public bool TrySetGridPosition(int x, int z, Facing facing, out string message)
    {
        nuint player = PlayerInstance;
        if (player == 0)
        {
            message = "No party is in the world yet.";
            return false;
        }

        bool ok = _mem.WriteI32(player + MapFormat.PlayerGridX, x);
        ok &= _mem.WriteI32(player + MapFormat.PlayerGridZ, z);
        ok &= _mem.WriteI32(player + MapFormat.PlayerFacing, (int)facing);
        // Keep "previous" in step so a blocked move cannot bounce the party back to the old cell.
        ok &= _mem.WriteI32(player + MapFormat.PlayerPrevX, x);
        ok &= _mem.WriteI32(player + MapFormat.PlayerPrevZ, z);

        message = ok
            ? $"Party position set to X {x} · Z {z} (take a step for the view to catch up)."
            : "Could not write the party position.";
        return ok;
    }

    /// <summary>Turns the party on the spot.</summary>
    public bool TrySetFacing(Facing facing)
    {
        nuint player = PlayerInstance;
        return player != 0 && _mem.WriteI32(player + MapFormat.PlayerFacing, (int)facing);
    }

    /// <summary>
    /// Produces a <c>TeleportTarget</c> to fill in. Preferred: our own block, committed in the
    /// game once and reused, so no object the game owns is disturbed. If it cannot be
    /// allocated, an existing target is borrowed instead — the pending one if there is one,
    /// otherwise the chapter's new-game location, which is only read when a new game starts.
    /// </summary>
    private nuint GetTeleportTarget(out string how)
    {
        if (_scratchTarget != 0)
        {
            how = "trainer-owned target";
            return _scratchTarget;
        }

        if (_classes.TeleportTarget != 0)
        {
            nuint block = _mem.Allocate(MapFormat.TeleportTargetSize);
            if (block != 0 && _mem.WritePtr(block + Il2Cpp.ObjectClassOffset, _classes.TeleportTarget))
            {
                _scratchTarget = block;
                how = "trainer-owned target";
                return block;
            }
        }

        nuint player = PlayerInstance;
        nuint pending = player == 0 ? 0 : _mem.ReadPtr(player + MapFormat.PlayerQueueTeleport);
        if (pending != 0)
        {
            how = "reusing the game's last teleport target";
            return pending;
        }

        nuint globals = GlobalMapsInstance;
        nuint newGame = globals == 0 ? 0 : _mem.ReadPtr(globals + MapFormat.GlobalMapsNewGameLocation);
        if (newGame != 0)
        {
            how = "borrowing the new-game location object";
            return newGame;
        }

        how = "";
        return 0;
    }
}
