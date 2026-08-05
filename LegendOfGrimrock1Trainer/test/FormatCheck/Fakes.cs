using LegendOfGrimrock1Trainer.Game;
using LegendOfGrimrock1Trainer.Lua;
using LegendOfGrimrock1Trainer.Memory;
using LegendOfGrimrock1Trainer.ViewModels;

namespace FormatCheck;

/// <summary>
/// A synthetic 32-bit address space with a hand-assembled LuaJIT 2.0 heap in it.
///
/// This is what lets the locator and the reader be tested with no game running and no copyrighted
/// file on disk. It builds real object bytes — a <c>GG_State</c>, a globals table with the
/// self-reference and version string the validator insists on, a <c>party</c> with champions, stat,
/// condition and skill tables, a dungeon with maps — and it can just as easily build the cases a
/// live game cannot be asked to produce: a stale static pointer, a coroutine that is not the main
/// thread, a module relocated somewhere else, and an unreadable hole in the middle of the heap.
/// </summary>
public sealed class FakeHeap : IMemorySource
{
    private readonly Dictionary<uint, byte[]> _blocks = new();
    private readonly List<(uint Base, uint Size)> _regions = new();
    private readonly HashSet<uint> _holes = new();

    /// <inheritdoc/>
    public uint ModuleBase { get; private set; }

    /// <inheritdoc/>
    public int ModuleSize { get; private set; }

    /// <summary>Address of the assembled main <c>lua_State</c>.</summary>
    public uint LuaState { get; private set; }

    /// <summary>Address of the assembled globals table.</summary>
    public uint Globals { get; private set; }

    /// <summary>Address of the assembled <c>party</c> table, or 0 when the fixture has no game.</summary>
    public uint Party { get; private set; }

    /// <summary>Number of writes the fixture accepted; lets a check prove an edit actually landed.</summary>
    public int Writes { get; private set; }

    /// <summary>
    /// Makes every write fail, as a page that turned read-only under the trainer would. Lets a check
    /// watch what the UI does with an edit the game refused — which a live game cannot be asked to do.
    /// </summary>
    public bool RefuseWrites { get; set; }

    // --- IMemorySource -------------------------------------------------------------------------------

    /// <inheritdoc/>
    public int Read(uint address, byte[] buffer, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryByte(address + (uint)i, out byte b)) return 0;   // all-or-nothing, like ProcessMemory
            buffer[i] = b;
        }
        return count;
    }

    /// <inheritdoc/>
    public bool Write(uint address, byte[] data)
    {
        if (RefuseWrites) return false;
        for (int i = 0; i < data.Length; i++)
            if (!Locate(address + (uint)i, out _, out _)) return false;

        for (int i = 0; i < data.Length; i++)
        {
            Locate(address + (uint)i, out var block, out int offset);
            block![offset] = data[i];
        }
        Writes++;
        return true;
    }

    /// <inheritdoc/>
    public IEnumerable<SourceRegion> Regions() =>
        _regions.OrderBy(r => r.Base).Select(r => new SourceRegion(r.Base, r.Size));

    private bool TryByte(uint address, out byte value)
    {
        value = 0;
        if (!Locate(address, out var block, out int offset)) return false;
        value = block![offset];
        return true;
    }

    private bool Locate(uint address, out byte[]? block, out int offset)
    {
        block = null;
        offset = 0;
        foreach (var (b, data) in _blocks)
        {
            if (address < b || address >= (long)b + data.Length) continue;
            if (_holes.Contains(b)) return false;
            block = data;
            offset = (int)(address - b);
            return true;
        }
        return false;
    }

    // --- construction --------------------------------------------------------------------------------

    private uint _next = 0x0200_0000;

    /// <summary>Reserves a block and registers it as a scannable region.</summary>
    public uint Alloc(int size, bool scannable = true)
    {
        uint at = _next;
        _next = (uint)(((long)_next + size + 0xFFF) & ~0xFFFL);
        _blocks[at] = new byte[size];
        if (scannable) _regions.Add((at, (uint)size));
        return at;
    }

    /// <summary>Makes the block containing <paramref name="address"/> unreadable, as a guard page would be.</summary>
    public void Poison(uint address) => _holes.Add(BlockOf(address));

    /// <summary>Makes a poisoned block readable again.</summary>
    public void Revive(uint address) => _holes.Remove(BlockOf(address));

    private uint BlockOf(uint address)
    {
        foreach (var (b, data) in _blocks)
            if (address >= b && address < (long)b + data.Length) return b;
        return address;
    }

    /// <summary>
    /// Punches an unreadable hole through one champion's stat table — the hazard the read-validate-
    /// write discipline exists for, and one a live game cannot be asked to produce on demand.
    /// </summary>
    public void PoisonChampionStats(int championIndex)
    {
        var lua = new LuaHeap(this);
        var champion = lua.GetIndex(lua.GetPath(Globals, GrimrockLayout.PartyKey,
            GrimrockLayout.PartyChampionsKey).Reference, championIndex);
        var stats = lua.GetField(champion.Reference, GrimrockLayout.ChampionStatsKey);
        if (!lua.TryReadTable(stats, out var table)) return;
        Poison(table.Node);
    }

    /// <summary>Overwrites the module's PE signature so the header will not parse.</summary>
    public void CorruptModuleHeader() => PutUInt32(ModuleBase, 0);

    /// <summary>Rewrites the module's COFF machine type (0x8664 = x64).</summary>
    public void SetModuleMachine(ushort machine)
    {
        var buf = new byte[2];
        BitConverter.GetBytes(machine).CopyTo(buf, 0);
        Write(ModuleBase + 0x100 + 4, buf);
    }

    /// <summary>Writes a 32-bit word.</summary>
    public void PutUInt32(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    /// <summary>Writes a double.</summary>
    public void PutDouble(uint address, double value) => Write(address, BitConverter.GetBytes(value));

    /// <summary>Reads a double back.</summary>
    public double GetDouble(uint address)
    {
        var buf = new byte[8];
        return Read(address, buf, 8) == 8 ? BitConverter.ToDouble(buf) : double.NaN;
    }

    /// <summary>Interns a string as a <c>GCstr</c> and returns its address.</summary>
    public uint NewString(string text)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes(text);
        uint at = Alloc(LuaLayout.StringHeaderSize + bytes.Length + 1);
        var block = _blocks[at];
        block[LuaLayout.GcType] = LuaLayout.GcTypeString;
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(block, LuaLayout.StringLength);
        bytes.CopyTo(block, LuaLayout.StringHeaderSize);
        return at;
    }

    /// <summary>
    /// Builds a <c>GCtab</c> with an array part of <paramref name="arraySize"/> slots and a hash part
    /// of <paramref name="hashSlots"/> nodes (rounded up to a power of two, as LuaJIT requires).
    /// </summary>
    public uint NewTable(int arraySize, int hashSlots)
    {
        int nodes = 1;
        while (nodes < Math.Max(hashSlots, 1)) nodes <<= 1;

        uint tab = Alloc(LuaLayout.TableSize);
        uint array = arraySize > 0 ? Alloc(arraySize * LuaLayout.TValueSize) : 0;
        uint node = Alloc(nodes * LuaLayout.NodeSize);

        var block = _blocks[tab];
        block[LuaLayout.GcType] = LuaLayout.GcTypeTable;
        BitConverter.GetBytes(array).CopyTo(block, LuaLayout.TableArray);
        BitConverter.GetBytes(node).CopyTo(block, LuaLayout.TableNode);
        BitConverter.GetBytes((uint)arraySize).CopyTo(block, LuaLayout.TableArraySize);
        BitConverter.GetBytes((uint)(nodes - 1)).CopyTo(block, LuaLayout.TableHashMask);

        // Every slot starts nil, both in the array and in the hash.
        for (int i = 0; i < arraySize; i++)
            PutUInt32(array + (uint)(i * LuaLayout.TValueSize) + LuaLayout.TValueIt, LuaLayout.ItNil);
        for (int i = 0; i < nodes; i++)
        {
            uint n = node + (uint)(i * LuaLayout.NodeSize);
            PutUInt32(n + LuaLayout.NodeKey + LuaLayout.TValueIt, LuaLayout.ItNil);
            PutUInt32(n + LuaLayout.NodeValue + LuaLayout.TValueIt, LuaLayout.ItNil);
        }
        return tab;
    }

    private uint NodeArray(uint table, out int count)
    {
        var buf = new byte[LuaLayout.TableSize];
        Read(table, buf, buf.Length);
        count = (int)BitConverter.ToUInt32(buf, LuaLayout.TableHashMask) + 1;
        return BitConverter.ToUInt32(buf, LuaLayout.TableNode);
    }

    /// <summary>Finds the first free hash node and returns its address, or 0 when the table is full.</summary>
    private uint FreeNode(uint table)
    {
        uint node = NodeArray(table, out int count);
        for (int i = 0; i < count; i++)
        {
            uint n = node + (uint)(i * LuaLayout.NodeSize);
            var buf = new byte[4];
            Read(n + LuaLayout.NodeKey + LuaLayout.TValueIt, buf, 4);
            if (BitConverter.ToUInt32(buf) == LuaLayout.ItNil) return n;
        }
        return 0;
    }

    /// <summary>Sets a string-keyed entry to a tagged GC value, returning the value's slot.</summary>
    public uint SetField(uint table, string key, uint tag, uint reference)
    {
        uint n = FreeNode(table);
        if (n == 0) throw new InvalidOperationException($"fixture table is full; cannot add '{key}'.");
        PutUInt32(n + LuaLayout.NodeKey + LuaLayout.TValueLo, NewString(key));
        PutUInt32(n + LuaLayout.NodeKey + LuaLayout.TValueIt, LuaLayout.ItString);
        PutUInt32(n + LuaLayout.NodeValue + LuaLayout.TValueLo, reference);
        PutUInt32(n + LuaLayout.NodeValue + LuaLayout.TValueIt, tag);
        return n + LuaLayout.NodeValue;
    }

    /// <summary>Sets a string-keyed numeric entry, returning the value's slot.</summary>
    public uint SetNumber(uint table, string key, double value)
    {
        uint n = FreeNode(table);
        if (n == 0) throw new InvalidOperationException($"fixture table is full; cannot add '{key}'.");
        PutUInt32(n + LuaLayout.NodeKey + LuaLayout.TValueLo, NewString(key));
        PutUInt32(n + LuaLayout.NodeKey + LuaLayout.TValueIt, LuaLayout.ItString);
        PutDouble(n + LuaLayout.NodeValue, value);
        return n + LuaLayout.NodeValue;
    }

    /// <summary>Sets a string-keyed boolean entry.</summary>
    public void SetBool(uint table, string key, bool value) =>
        SetField(table, key, value ? LuaLayout.ItTrue : LuaLayout.ItFalse, 0);

    /// <summary>Sets a string-keyed string entry.</summary>
    public void SetString(uint table, string key, string value) =>
        SetField(table, key, LuaLayout.ItString, NewString(value));

    /// <summary>Sets a string-keyed table entry.</summary>
    public void SetTable(uint table, string key, uint value) =>
        SetField(table, key, LuaLayout.ItTable, value);

    /// <summary>Sets a 1-based array slot to a table reference.</summary>
    public void SetArrayTable(uint table, int index, uint value)
    {
        var buf = new byte[LuaLayout.TableSize];
        Read(table, buf, buf.Length);
        uint array = BitConverter.ToUInt32(buf, LuaLayout.TableArray);
        uint size = BitConverter.ToUInt32(buf, LuaLayout.TableArraySize);
        if (index >= size) throw new ArgumentOutOfRangeException(nameof(index));
        uint slot = array + (uint)(index * LuaLayout.TValueSize);
        PutUInt32(slot + LuaLayout.TValueLo, value);
        PutUInt32(slot + LuaLayout.TValueIt, LuaLayout.ItTable);
    }

    /// <summary>Sets a 1-based array slot to a number.</summary>
    public void SetArrayNumber(uint table, int index, double value)
    {
        var buf = new byte[LuaLayout.TableSize];
        Read(table, buf, buf.Length);
        uint array = BitConverter.ToUInt32(buf, LuaLayout.TableArray);
        PutDouble(array + (uint)(index * LuaLayout.TValueSize), value);
    }

    /// <summary>Address of an array slot, for a check that wants to poke it directly.</summary>
    public uint ArraySlot(uint table, int index)
    {
        var buf = new byte[LuaLayout.TableSize];
        Read(table, buf, buf.Length);
        return BitConverter.ToUInt32(buf, LuaLayout.TableArray) + (uint)(index * LuaLayout.TValueSize);
    }

    /// <summary>
    /// Builds a thread object. Only <paramref name="mainThread"/> gets the
    /// <c>glref == L + sizeof(lua_State)</c> relationship that identifies LuaJIT's main thread.
    /// </summary>
    public uint NewThread(uint env, bool mainThread)
    {
        // Allocate the thread and the global state together so the main-thread relationship can hold.
        uint at = Alloc(LuaLayout.StateSize + 256);
        uint stack = Alloc(64 * LuaLayout.TValueSize);

        var block = _blocks[at];
        block[LuaLayout.GcType] = LuaLayout.GcTypeThread;
        block[LuaLayout.StateDummyFfid] = LuaLayout.FastFunctionC;
        block[LuaLayout.StateStatus] = 0;
        BitConverter.GetBytes(mainThread ? at + LuaLayout.MainThreadGlobalStateDelta : at + 0x100)
            .CopyTo(block, LuaLayout.StateGlobalRef);
        BitConverter.GetBytes(stack).CopyTo(block, LuaLayout.StateStack);
        BitConverter.GetBytes(stack + 8).CopyTo(block, LuaLayout.StateBase);
        BitConverter.GetBytes(stack + 16).CopyTo(block, LuaLayout.StateTop);
        BitConverter.GetBytes(stack + 64 * LuaLayout.TValueSize).CopyTo(block, LuaLayout.StateMaxStack);
        BitConverter.GetBytes(env).CopyTo(block, LuaLayout.StateEnv);
        BitConverter.GetBytes(64u).CopyTo(block, LuaLayout.StateStackSize);
        return at;
    }

    /// <summary>
    /// Lays down a PE image with one executable and one writable-data section, at
    /// <paramref name="moduleBase"/>. The data section covers the static <c>lua_State</c> slot.
    /// </summary>
    public void BuildModule(uint moduleBase, uint timeDateStamp = GameFacts.KnownTimeDateStamp,
                            bool includeDataSection = true)
    {
        const int size = 0x2000;
        ModuleBase = moduleBase;
        ModuleSize = 0x1C0000;
        _blocks[moduleBase] = new byte[Math.Max(size, (int)GrimrockLayout.LuaStateSlotRva + 0x10)];

        var img = _blocks[moduleBase];
        img[0] = (byte)'M'; img[1] = (byte)'Z';
        const int peOffset = 0x100;
        BitConverter.GetBytes(peOffset).CopyTo(img, 0x3C);
        BitConverter.GetBytes(0x00004550u).CopyTo(img, peOffset);

        int coff = peOffset + 4;
        BitConverter.GetBytes((ushort)0x014C).CopyTo(img, coff);                 // i386
        BitConverter.GetBytes((ushort)(includeDataSection ? 2 : 1)).CopyTo(img, coff + 2);
        BitConverter.GetBytes(timeDateStamp).CopyTo(img, coff + 4);
        BitConverter.GetBytes((ushort)224).CopyTo(img, coff + 16);               // optional header size

        int opt = coff + 20;
        BitConverter.GetBytes((ushort)0x010B).CopyTo(img, opt);                  // PE32
        BitConverter.GetBytes(GameFacts.PreferredImageBase).CopyTo(img, opt + 28);
        BitConverter.GetBytes(0x001C0000u).CopyTo(img, opt + 56);
        BitConverter.GetBytes((ushort)0x8140).CopyTo(img, opt + 70);             // DYNAMICBASE | NX | TS-aware

        int table = opt + 224;
        WriteSection(img, table, ".text", 0x1000, 0x128301, 0x60000020);
        if (includeDataSection)
            WriteSection(img, table + 40, ".data", 0x183000, 0x64C0, 0xC0000040);
    }

    private static void WriteSection(byte[] img, int at, string name, uint rva, uint vsize, uint chars)
    {
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(img, at);
        BitConverter.GetBytes(vsize).CopyTo(img, at + 8);
        BitConverter.GetBytes(rva).CopyTo(img, at + 12);
        BitConverter.GetBytes(chars).CopyTo(img, at + 36);
    }

    /// <summary>Points the module's static <c>lua_State</c> slot at <paramref name="value"/>.</summary>
    public void SetStaticStatePointer(uint value) =>
        PutUInt32(ModuleBase + GrimrockLayout.LuaStateSlotRva, value);

    /// <summary>Builds a globals table that passes validation, without any game loaded.</summary>
    public uint BuildGlobals()
    {
        uint g = NewTable(0, 32);
        SetTable(g, GrimrockLayout.GlobalsSelfKey, g);
        SetString(g, GrimrockLayout.VersionKey, GrimrockLayout.ExpectedLuaVersion);
        foreach (var cls in GrimrockLayout.EngineClassKeys)
            SetTable(g, cls, NewTable(0, 2));
        Globals = g;
        return g;
    }

    /// <summary>
    /// Assembles a complete fixture: module, globals, main thread, and — when
    /// <paramref name="withGame"/> — a party of <paramref name="champions"/> champions on a dungeon
    /// level with a small map.
    /// </summary>
    public static FakeHeap BuildGame(uint moduleBase = 0x00990000, int champions = GameFacts.PartySize,
                                     bool withGame = true, bool staticPointerValid = true,
                                     bool addDecoyThread = true)
    {
        var heap = new FakeHeap();
        heap.BuildModule(moduleBase);
        uint globals = heap.BuildGlobals();

        if (addDecoyThread)
        {
            // A coroutine: right gct, right dummy_ffid, wrong glref. The scan must walk past it.
            heap.NewThread(globals, mainThread: false);
        }

        uint state = heap.NewThread(globals, mainThread: true);
        heap.LuaState = state;
        heap.SetStaticStatePointer(staticPointerValid ? state : 0xDEADBEEF);

        if (!withGame) return heap;

        uint party = heap.NewTable(0, 16);
        heap.SetNumber(party, GrimrockLayout.PartyLevelKey, 1);
        heap.SetNumber(party, GrimrockLayout.PartyXKey, 2);
        heap.SetNumber(party, GrimrockLayout.PartyYKey, 8);
        heap.SetNumber(party, GrimrockLayout.PartyFacingKey, 0);

        uint roster = heap.NewTable(champions + 1, 2);
        for (int i = 1; i <= champions; i++) heap.SetArrayTable(roster, i, heap.BuildChampion(i));
        heap.SetTable(party, GrimrockLayout.PartyChampionsKey, roster);

        // party.statistics.stats[1..2]
        uint statistics = heap.NewTable(0, 2);
        uint statList = heap.NewTable(3, 2);
        for (int i = 1; i <= 2; i++)
        {
            uint entry = heap.NewTable(0, 4);
            heap.SetString(entry, GrimrockLayout.NameKey, i == 1 ? "play_time" : "monsters_killed");
            heap.SetString(entry, GrimrockLayout.UiNameKey, i == 1 ? "Play Time" : "Monsters Killed");
            heap.SetNumber(entry, GrimrockLayout.ValueKey, i == 1 ? 12.5 : 3);
            heap.SetArrayTable(statList, i, entry);
        }
        heap.SetTable(statistics, GrimrockLayout.ChampionStatsKey, statList);
        heap.SetTable(party, GrimrockLayout.PartyStatisticsKey, statistics);

        heap.SetTable(globals, GrimrockLayout.PartyKey, party);
        heap.Party = party;

        // dungeon.maps[1] — a 4x4 level whose middle is open floor and whose border is wall.
        uint dungeon = heap.NewTable(0, 4);
        uint maps = heap.NewTable(2, 2);
        uint map = heap.NewTable(0, 8);
        const int w = 4, h = 4;
        uint cells = heap.NewTable(w * h + 1, 1);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                // Interior tiles carry a pit and a pressure plate. Without a non-automap bit on at
                // least one open tile, "reveal preserves everything else" would be asserted against
                // zero and could not fail.
                long bits = border
                    ? GrimrockLayout.CellBits.Wall
                    : GrimrockLayout.CellBits.Pit | GrimrockLayout.CellBits.Pad;
                heap.SetArrayNumber(cells, GrimrockLayout.CellIndex(x, y, w), bits);
            }
        }
        heap.SetString(map, GrimrockLayout.MapNameKey, "Into the Dark");
        heap.SetNumber(map, GrimrockLayout.MapWidthKey, w);
        heap.SetNumber(map, GrimrockLayout.MapHeightKey, h);
        heap.SetBool(map, GrimrockLayout.MapVisitedKey, true);
        heap.SetTable(map, GrimrockLayout.MapCellsKey, cells);
        heap.SetArrayTable(maps, 1, map);
        heap.SetTable(dungeon, GrimrockLayout.DungeonMapsKey, maps);
        heap.SetTable(globals, GrimrockLayout.DungeonKey, dungeon);

        return heap;
    }

    /// <summary>Builds one champion with the full stat, condition and skill tables.</summary>
    public uint BuildChampion(int index)
    {
        uint champion = NewTable(0, 16);
        SetString(champion, GrimrockLayout.ChampionNameKey, $"Champion {index}");
        SetString(champion, GrimrockLayout.ChampionSexKey, "male");
        SetBool(champion, GrimrockLayout.ChampionEnabledKey, true);
        SetNumber(champion, GrimrockLayout.ChampionOrdinalKey, index);
        SetNumber(champion, GrimrockLayout.ChampionFoodKey, 750);
        SetNumber(champion, GrimrockLayout.ChampionSkillPointsKey, 0);

        uint stats = NewTable(0, GameTables.Stats.Length);
        foreach (var info in GameTables.Stats)
        {
            uint stat = NewTable(0, 4);
            SetString(stat, GrimrockLayout.NameKey, info.Name);
            double value = info.Name switch
            {
                "health" => 60 + index,
                "energy" => 40 + index,
                "strength" => 12,
                "dexterity" => 11,
                "vitality" => 10,
                "willpower" => 9,
                _ => 0,
            };
            SetNumber(stat, GrimrockLayout.ValueKey, value);
            SetNumber(stat, GrimrockLayout.MaxKey, value);
            SetTable(stats, info.Name, stat);
        }
        SetTable(champion, GrimrockLayout.ChampionStatsKey, stats);

        uint conditions = NewTable(0, GameTables.Conditions.Length);
        foreach (var info in GameTables.Conditions)
        {
            uint condition = NewTable(0, 4);
            SetString(condition, GrimrockLayout.NameKey, info.Name);
            SetString(condition, GrimrockLayout.UiNameKey, info.UiName);
            SetNumber(condition, GrimrockLayout.ValueKey, info.Name == "poison" ? 1 : 0);
            SetNumber(condition, GrimrockLayout.TimerKey, info.Name == "poison" ? 30 : 0);
            SetTable(conditions, info.Name, condition);
        }
        SetTable(champion, GrimrockLayout.ChampionConditionsKey, conditions);

        uint skills = NewTable(4, 2);
        string[] trained = { "athletics", "armors", "swords" };
        for (int i = 0; i < trained.Length; i++)
        {
            uint skill = NewTable(0, 2);
            SetString(skill, GrimrockLayout.NameKey, trained[i]);
            SetNumber(skill, GrimrockLayout.LevelKey, i + 1);
            SetArrayTable(skills, i + 1, skill);
        }
        SetTable(champion, GrimrockLayout.ChampionSkillsKey, skills);

        uint talents = NewTable(0, 2);
        SetTable(talents, "athletic", NewTable(0, 1));
        SetTable(champion, GrimrockLayout.ChampionTalentsKey, talents);

        uint cls = NewTable(0, 8);
        SetString(cls, GrimrockLayout.NameKey, "Fighter");
        SetNumber(cls, GrimrockLayout.LevelKey, 1);
        SetNumber(cls, GrimrockLayout.ExpKey, 0);
        SetNumber(cls, GrimrockLayout.NextLevelKey, 850);
        SetTable(champion, GrimrockLayout.ChampionClassKey, cls);

        uint race = NewTable(0, 8);
        SetString(race, GrimrockLayout.NameKey, "Human");
        SetTable(champion, GrimrockLayout.ChampionRaceKey, race);

        return champion;
    }
}

/// <summary>A host stub that records what the row view-models reported.</summary>
public sealed class FakeHost : IGameHost
{
    private readonly PartyReader _reader;
    private readonly uint _globals;

    /// <summary>Creates a host over a reader and a globals table.</summary>
    public FakeHost(PartyReader reader, uint globals)
    {
        _reader = reader;
        _globals = globals;
        Actions = new TrainerActions(reader);
    }

    /// <inheritdoc/>
    public bool WritesAllowed { get; set; } = true;

    /// <inheritdoc/>
    public bool EditorHasFocus { get; set; }

    /// <inheritdoc/>
    public TrainerActions? Actions { get; }

    /// <summary>The last message reported.</summary>
    public string LastMessage { get; private set; } = "";

    /// <summary>How many refreshes were requested.</summary>
    public int RefreshRequests { get; private set; }

    /// <inheritdoc/>
    public void Report(string message) => LastMessage = message;

    /// <inheritdoc/>
    public void RequestRefresh() => RefreshRequests++;

    /// <inheritdoc/>
    public PartySnapshot? ResolveParty() => _reader.ReadParty(_globals);

    /// <inheritdoc/>
    public ChampionSnapshot? ResolveChampion(int index) =>
        ResolveParty()?.Champions.FirstOrDefault(c => c.Index == index);
}
