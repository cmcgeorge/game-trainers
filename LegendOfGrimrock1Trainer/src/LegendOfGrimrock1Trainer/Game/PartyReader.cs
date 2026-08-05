using LegendOfGrimrock1Trainer.Lua;

namespace LegendOfGrimrock1Trainer.Game;

/// <summary>One entry of a champion's <c>stats</c> table, with the slots its numbers live in.</summary>
public sealed record StatSnapshot(string Name, string UiName, double Value, double Max, uint ValueSlot, uint MaxSlot);

/// <summary>One entry of a champion's <c>conditions</c> table.</summary>
public sealed record ConditionSnapshot(string Name, string UiName, ConditionKind Kind, double Value, double Timer, uint ValueSlot, uint TimerSlot);

/// <summary>One entry of a champion's <c>skills</c> array.</summary>
public sealed record SkillSnapshot(string Name, string UiName, int Level, uint LevelSlot);

/// <summary>Everything the UI shows for one champion, read in a single pass.</summary>
public sealed class ChampionSnapshot
{
    /// <summary>1-based slot in <c>party.champions</c>.</summary>
    public int Index { get; init; }

    /// <summary>Address of the champion's Lua table.</summary>
    public uint Address { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Race name, e.g. "Minotaur".</summary>
    public string Race { get; init; } = "";

    /// <summary>Class name, e.g. "Fighter".</summary>
    public string ClassName { get; init; } = "";

    /// <summary>"male" or "female".</summary>
    public string Sex { get; init; } = "";

    /// <summary>Whether the slot holds a living, active champion.</summary>
    public bool Enabled { get; init; }

    /// <summary>Character level.</summary>
    public int Level { get; init; }

    /// <summary>Slot holding the level, on the champion's class instance.</summary>
    public uint LevelSlot { get; init; }

    /// <summary>Accumulated experience.</summary>
    public double Experience { get; init; }

    /// <summary>Slot holding the experience.</summary>
    public uint ExperienceSlot { get; init; }

    /// <summary>Experience needed for the next level.</summary>
    public double NextLevel { get; init; }

    /// <summary>Food, 0..1000.</summary>
    public double Food { get; init; }

    /// <summary>Slot holding the food value.</summary>
    public uint FoodSlot { get; init; }

    /// <summary>Unspent skill points.</summary>
    public int SkillPoints { get; init; }

    /// <summary>Slot holding the unspent skill points.</summary>
    public uint SkillPointsSlot { get; init; }

    /// <summary>Stats in <see cref="GameTables.Stats"/> order, then anything else the table held.</summary>
    public IReadOnlyList<StatSnapshot> Stats { get; init; } = Array.Empty<StatSnapshot>();

    /// <summary>Conditions in <see cref="GameTables.Conditions"/> order.</summary>
    public IReadOnlyList<ConditionSnapshot> Conditions { get; init; } = Array.Empty<ConditionSnapshot>();

    /// <summary>Trained skills, in the order the champion's own array holds them.</summary>
    public IReadOnlyList<SkillSnapshot> Skills { get; init; } = Array.Empty<SkillSnapshot>();

    /// <summary>Talent and trait keys the champion carries.</summary>
    public IReadOnlyList<string> Talents { get; init; } = Array.Empty<string>();

    /// <summary>Finds a stat by its game key, or null.</summary>
    public StatSnapshot? Stat(string name) => Stats.FirstOrDefault(s => s.Name == name);

    /// <summary>Finds a condition by its game key, or null.</summary>
    public ConditionSnapshot? Condition(string name) => Conditions.FirstOrDefault(c => c.Name == name);
}

/// <summary>
/// One dungeon level, as <c>dungeon.maps[i]</c> describes it.
///
/// <paramref name="CellsArray"/>/<paramref name="CellsCount"/> are the table's array part, which is
/// where a 32×32 level's 1025 tiles actually live and which makes a whole-level sweep one read per
/// tile instead of a linear node walk. <paramref name="CellsTable"/> is kept alongside so a map
/// whose tiles ended up in the hash part instead — which is a property of how a table was last
/// rehashed, not a guarantee — still reads and writes correctly rather than silently doing nothing.
/// </summary>
public sealed record MapSnapshot(
    int Level, string Name, int Width, int Height,
    uint CellsTable, uint CellsArray, uint CellsCount, bool Visited)
{
    /// <summary>Whether this map has a readable tile array at all.</summary>
    public bool HasCells => CellsTable != 0 && Width > 0 && Height > 0;

    /// <summary>
    /// Whether the dimensions describe a level a Lua table could plausibly hold. A torn read of
    /// <c>width</c>/<c>height</c> yields something absurd, and a whole-level sweep sized from it
    /// would be an unbounded run of syscalls; Grimrock's own levels are 32x32.
    /// </summary>
    public bool HasPlausibleSize => HasCells && (long)Width * Height <= Lua.LuaLayout.MaxTableEntries;
}

/// <summary>The party and its surroundings, read in a single pass.</summary>
public sealed class PartySnapshot
{
    /// <summary>Address of the <c>party</c> Lua table.</summary>
    public uint Address { get; init; }

    /// <summary>Dungeon level the party is on (1-based).</summary>
    public int Level { get; init; }

    /// <summary>Slot holding the level.</summary>
    public uint LevelSlot { get; init; }

    /// <summary>Party tile X, 0-based.</summary>
    public int X { get; init; }

    /// <summary>Slot holding X.</summary>
    public uint XSlot { get; init; }

    /// <summary>Party tile Y, 0-based.</summary>
    public int Y { get; init; }

    /// <summary>Slot holding Y.</summary>
    public uint YSlot { get; init; }

    /// <summary>Facing: 0 north, 1 east, 2 south, 3 west.</summary>
    public int Facing { get; init; }

    /// <summary>Slot holding the facing.</summary>
    public uint FacingSlot { get; init; }

    /// <summary>The four champion slots, whether or not they hold a living champion.</summary>
    public IReadOnlyList<ChampionSnapshot> Champions { get; init; } = Array.Empty<ChampionSnapshot>();

    /// <summary>Run statistics, keyed by the game's own stat name.</summary>
    public IReadOnlyList<(string Name, string UiName, double Value, uint Slot)> Statistics { get; init; } =
        Array.Empty<(string, string, double, uint)>();

    /// <summary>Levels of the loaded dungeon.</summary>
    public IReadOnlyList<MapSnapshot> Maps { get; init; } = Array.Empty<MapSnapshot>();

    /// <summary>The map the party is standing on, or null.</summary>
    public MapSnapshot? CurrentMap => Maps.FirstOrDefault(m => m.Level == Level);
}

/// <summary>
/// Turns the game's Lua object graph into typed snapshots, and writes edits back.
///
/// Every snapshot carries the address of each value it read, and those addresses are only used
/// during the tick that produced them. LuaJIT's collector never relocates an object, but adding a
/// key to a table rehashes its node array and moves every value in it, so caching a slot across
/// refreshes would eventually write into whatever moved in. Re-resolving costs one read per table.
/// </summary>
public sealed class PartyReader
{
    private readonly LuaHeap _heap;

    /// <summary>Wraps the heap reader the snapshots are built from.</summary>
    public PartyReader(LuaHeap heap)
    {
        ArgumentNullException.ThrowIfNull(heap);
        _heap = heap;
    }

    /// <summary>The heap this reader works through.</summary>
    public LuaHeap Heap => _heap;

    /// <summary>
    /// The address a numeric field was read from, or 0 when the field is not a number.
    ///
    /// This is what makes "a value is only written when the slot it came from was read back as a
    /// number this tick" true rather than aspirational: <see cref="LuaHeap.GetField"/> hands back a
    /// live slot for a nil, a string or a table just as readily as for a double, and every write path
    /// refuses a zero slot. Zeroing here is therefore the single choke point that stops an edit
    /// turning a string field into a double behind the VM's back.
    /// </summary>
    private static uint NumberSlot(LuaValue value) => value.IsNumber ? value.Slot : 0;

    /// <summary>
    /// Reads the whole party from the globals table, or null when no game is loaded — at the main
    /// menu the global <c>party</c> is simply absent, which is the cleanest "not in a game" signal
    /// the engine offers.
    /// </summary>
    public PartySnapshot? ReadParty(uint globals)
    {
        var partyValue = _heap.GetField(globals, GrimrockLayout.PartyKey);
        if (!_heap.TryReadTable(partyValue, out var party)) return null;

        var level = _heap.GetField(party, GrimrockLayout.PartyLevelKey);
        var x = _heap.GetField(party, GrimrockLayout.PartyXKey);
        var y = _heap.GetField(party, GrimrockLayout.PartyYKey);
        var facing = _heap.GetField(party, GrimrockLayout.PartyFacingKey);
        if (!level.IsNumber || !x.IsNumber || !y.IsNumber) return null;

        var champions = new List<ChampionSnapshot>(GameFacts.PartySize);
        if (_heap.TryReadTable(_heap.GetField(party, GrimrockLayout.PartyChampionsKey), out var roster))
        {
            for (int i = 1; i <= GameFacts.PartySize; i++)
            {
                var slot = _heap.GetIndex(roster, i);
                if (!_heap.TryReadTable(slot, out var champion)) continue;
                champions.Add(ReadChampion(i, champion));
            }
        }

        return new PartySnapshot
        {
            Address = partyValue.Reference,
            Level = level.AsInt(),
            LevelSlot = NumberSlot(level),
            X = x.AsInt(),
            XSlot = NumberSlot(x),
            Y = y.AsInt(),
            YSlot = NumberSlot(y),
            Facing = facing.AsInt(),
            FacingSlot = NumberSlot(facing),
            Champions = champions,
            Statistics = ReadStatistics(party),
            Maps = ReadMaps(globals),
        };
    }

    /// <summary>Reads one champion table into a snapshot.</summary>
    private ChampionSnapshot ReadChampion(int index, LuaTable champion)
    {
        var food = _heap.GetField(champion, GrimrockLayout.ChampionFoodKey);
        var points = _heap.GetField(champion, GrimrockLayout.ChampionSkillPointsKey);

        double experience = 0, nextLevel = 0;
        int level = 0;
        uint levelSlot = 0, expSlot = 0;
        string className = "";
        if (_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionClassKey), out var cls))
        {
            var lv = _heap.GetField(cls, GrimrockLayout.LevelKey);
            var xp = _heap.GetField(cls, GrimrockLayout.ExpKey);
            level = lv.AsInt();
            levelSlot = NumberSlot(lv);
            experience = xp.AsNumber();
            expSlot = NumberSlot(xp);
            nextLevel = _heap.GetField(cls, GrimrockLayout.NextLevelKey).AsNumber();
            className = _heap.StringOf(_heap.GetField(cls, GrimrockLayout.NameKey)) ?? "";
        }

        string race = "";
        if (_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionRaceKey), out var raceTable))
            race = _heap.StringOf(_heap.GetField(raceTable, GrimrockLayout.NameKey)) ?? "";

        return new ChampionSnapshot
        {
            Index = index,
            Address = champion.Address,
            Name = _heap.StringOf(_heap.GetField(champion, GrimrockLayout.ChampionNameKey)) ?? $"Champion {index}",
            Race = race,
            ClassName = className,
            Sex = _heap.StringOf(_heap.GetField(champion, GrimrockLayout.ChampionSexKey)) ?? "",
            Enabled = _heap.GetField(champion, GrimrockLayout.ChampionEnabledKey).AsBool(),
            Level = level,
            LevelSlot = levelSlot,
            Experience = experience,
            ExperienceSlot = expSlot,
            NextLevel = nextLevel,
            Food = food.AsNumber(),
            FoodSlot = NumberSlot(food),
            SkillPoints = points.AsInt(),
            SkillPointsSlot = NumberSlot(points),
            Stats = ReadStats(champion),
            Conditions = ReadConditions(champion),
            Skills = ReadSkills(champion),
            Talents = ReadTalents(champion),
        };
    }

    /// <summary>Reads <c>champion.stats</c>, ordering the known stats first and appending any extras.</summary>
    private List<StatSnapshot> ReadStats(LuaTable champion)
    {
        var result = new List<StatSnapshot>(GameTables.Stats.Length);
        if (!_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionStatsKey), out var stats))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in GameTables.Stats)
        {
            if (!_heap.TryReadTable(_heap.GetField(stats, info.Name), out var stat)) continue;
            seen.Add(info.Name);
            result.Add(BuildStat(info.Name, info.UiName, stat));
        }

        // A mod or a future patch can define stats this build never saw; show them rather than hide them.
        foreach (var (key, value) in _heap.Entries(stats))
        {
            var name = _heap.StringOf(key);
            if (name is null || !seen.Add(name)) continue;
            if (!_heap.TryReadTable(value, out var stat)) continue;
            result.Add(BuildStat(name, GameTables.Humanise(name), stat));
        }

        return result;
    }

    private StatSnapshot BuildStat(string name, string uiName, LuaTable stat)
    {
        var value = _heap.GetField(stat, GrimrockLayout.ValueKey);
        var max = _heap.GetField(stat, GrimrockLayout.MaxKey);
        return new StatSnapshot(name, uiName, value.AsNumber(), max.AsNumber(), NumberSlot(value), NumberSlot(max));
    }

    /// <summary>Reads <c>champion.conditions</c> in the game's own display order.</summary>
    private List<ConditionSnapshot> ReadConditions(LuaTable champion)
    {
        var result = new List<ConditionSnapshot>(GameTables.Conditions.Length);
        if (!_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionConditionsKey), out var conditions))
            return result;

        foreach (var info in GameTables.Conditions)
        {
            if (!_heap.TryReadTable(_heap.GetField(conditions, info.Name), out var condition)) continue;
            var value = _heap.GetField(condition, GrimrockLayout.ValueKey);
            var timer = _heap.GetField(condition, GrimrockLayout.TimerKey);
            var uiName = _heap.StringOf(_heap.GetField(condition, GrimrockLayout.UiNameKey)) ?? info.UiName;
            result.Add(new ConditionSnapshot(info.Name, uiName, info.Kind,
                value.AsNumber(), timer.AsNumber(), NumberSlot(value), NumberSlot(timer)));
        }

        return result;
    }

    /// <summary>Reads <c>champion.skills</c>, an array of <c>{ name, level }</c> tables.</summary>
    private List<SkillSnapshot> ReadSkills(LuaTable champion)
    {
        var result = new List<SkillSnapshot>();
        if (!_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionSkillsKey), out var skills))
            return result;

        int count = _heap.SequenceLength(skills, GameTables.Skills.Length + 8);
        for (int i = 1; i <= count; i++)
        {
            if (!_heap.TryReadTable(_heap.GetIndex(skills, i), out var skill)) continue;
            var name = _heap.StringOf(_heap.GetField(skill, GrimrockLayout.NameKey));
            if (name is null) continue;
            var level = _heap.GetField(skill, GrimrockLayout.LevelKey);
            var uiName = GameTables.SkillUiNames.TryGetValue(name, out var pretty) ? pretty : GameTables.Humanise(name);
            result.Add(new SkillSnapshot(name, uiName, level.AsInt(), NumberSlot(level)));
        }

        return result;
    }

    /// <summary>Reads the keys of <c>champion.talents</c>.</summary>
    private List<string> ReadTalents(LuaTable champion)
    {
        var result = new List<string>();
        if (!_heap.TryReadTable(_heap.GetField(champion, GrimrockLayout.ChampionTalentsKey), out var talents))
            return result;

        foreach (var (key, _) in _heap.Entries(talents))
        {
            var name = _heap.StringOf(key);
            if (name is not null) result.Add(name);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Reads <c>party.statistics.stats</c>, an array of <c>{ name, uiName, value }</c> tables.</summary>
    private List<(string, string, double, uint)> ReadStatistics(LuaTable party)
    {
        var result = new List<(string, string, double, uint)>();
        if (!_heap.TryReadTable(_heap.GetField(party, GrimrockLayout.PartyStatisticsKey), out var statistics))
            return result;
        if (!_heap.TryReadTable(_heap.GetField(statistics, GrimrockLayout.ChampionStatsKey), out var stats))
            return result;

        int count = _heap.SequenceLength(stats, 64);
        for (int i = 1; i <= count; i++)
        {
            if (!_heap.TryReadTable(_heap.GetIndex(stats, i), out var entry)) continue;
            var name = _heap.StringOf(_heap.GetField(entry, GrimrockLayout.NameKey)) ?? "";
            var uiName = _heap.StringOf(_heap.GetField(entry, GrimrockLayout.UiNameKey)) ?? GameTables.Humanise(name);
            var value = _heap.GetField(entry, GrimrockLayout.ValueKey);
            result.Add((name, uiName, value.AsNumber(), NumberSlot(value)));
        }

        return result;
    }

    /// <summary>Reads <c>dungeon.maps</c>: one entry per dungeon level.</summary>
    public List<MapSnapshot> ReadMaps(uint globals)
    {
        var result = new List<MapSnapshot>();
        if (!_heap.TryReadTable(_heap.GetField(globals, GrimrockLayout.DungeonKey), out var dungeon))
            return result;
        if (!_heap.TryReadTable(_heap.GetField(dungeon, GrimrockLayout.DungeonMapsKey), out var maps))
            return result;

        int count = _heap.SequenceLength(maps, 64);
        for (int i = 1; i <= count; i++)
        {
            if (!_heap.TryReadTable(_heap.GetIndex(maps, i), out var map)) continue;
            var name = _heap.StringOf(_heap.GetField(map, GrimrockLayout.MapNameKey))
                       ?? (i <= GameTables.CampaignLevelNames.Length ? GameTables.CampaignLevelNames[i - 1] : $"Level {i}");
            int width = _heap.GetField(map, GrimrockLayout.MapWidthKey).AsInt();
            int height = _heap.GetField(map, GrimrockLayout.MapHeightKey).AsInt();
            bool visited = _heap.GetField(map, GrimrockLayout.MapVisitedKey).AsBool();

            uint cellsTable = 0, cellsArray = 0, cellsCount = 0;
            if (_heap.TryReadTable(_heap.GetField(map, GrimrockLayout.MapCellsKey), out var cells))
            {
                cellsTable = cells.Address;
                cellsArray = cells.Array;
                cellsCount = cells.ArraySize;
            }

            result.Add(new MapSnapshot(i, name, width, height, cellsTable, cellsArray, cellsCount, visited));
        }

        return result;
    }

    // --- edits ---------------------------------------------------------------------------------------

    /// <summary>Writes a number into a slot the caller read this tick. No-op for a zero slot.</summary>
    public bool Write(uint slot, double value) => slot != 0 && _heap.WriteNumber(slot, value);

    /// <summary>
    /// Reads one tile's <c>TValue</c>. The value carries the address it came from, so a caller that
    /// wants to write does not have to read the tile a second time.
    ///
    /// The array part is the fast path — one read, no node walk — and the hash part is the fallback
    /// for a table whose integer keys did not land there. That fallback reads and walks the whole node
    /// array per tile; correct either way, but only the array path is cheap enough for a whole-level
    /// sweep, which is why <see cref="MapSnapshot.CellsArray"/> is kept alongside the table.
    /// </summary>
    private LuaValue CellValue(MapSnapshot map, int x, int y)
    {
        if (!map.HasCells) return LuaValue.Unreadable(0);
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return LuaValue.Unreadable(0);

        int index = GrimrockLayout.CellIndex(x, y, map.Width);
        if (index < 0) return LuaValue.Unreadable(0);

        if (map.CellsArray != 0 && index < map.CellsCount)
            return _heap.ReadValue(map.CellsArray + (uint)(index * LuaLayout.TValueSize));

        return _heap.GetIndex(map.CellsTable, index);
    }

    /// <summary>Reads one tile's bitmask, or null when the tile is out of range or not a number.</summary>
    public double? ReadCell(MapSnapshot map, int x, int y)
    {
        var value = CellValue(map, x, y);
        return value.IsNumber ? value.Number : null;
    }

    /// <summary>Writes one tile's bitmask. Refuses a tile outside the map or one that is not a number.</summary>
    public bool WriteCell(MapSnapshot map, int x, int y, double bits)
    {
        uint slot = NumberSlot(CellValue(map, x, y));
        return slot != 0 && _heap.WriteNumber(slot, bits);
    }
}
