using System.Text;
using TheQuestTrainer.Game;
using TheQuestTrainer.Memory;

namespace TheQuestTrainer.FormatCheck;

/// <summary>
/// A synthetic 32-bit address space: a mapped PE32 image with the same section geometry as
/// <c>TheQuest.exe</c>, plus whatever heap blocks a check wants to put in it.
///
/// Reads are all-or-nothing within a single block, exactly like <c>ReadProcessMemory</c> across a
/// region boundary, so "the record straddles an unreadable page" is expressible: map two blocks with
/// a gap between them.
/// </summary>
public sealed class FakeMemory : IMemorySource
{
    private readonly List<(uint Base, byte[] Data)> _blocks = new();

    /// <inheritdoc/>
    public uint ModuleBase { get; set; }

    /// <inheritdoc/>
    public int ModuleSize { get; set; }

    /// <summary>Maps <paramref name="data"/> at <paramref name="at"/>, replacing any block with that base.</summary>
    public void Map(uint at, byte[] data)
    {
        _blocks.RemoveAll(b => b.Base == at);
        _blocks.Add((at, data));
        _blocks.Sort((a, b) => a.Base.CompareTo(b.Base));
    }

    /// <summary>Maps <paramref name="length"/> zero bytes at <paramref name="at"/>.</summary>
    public void MapZeros(uint at, int length) => Map(at, new byte[length]);

    /// <summary>Removes the block based at <paramref name="at"/>, leaving a hole.</summary>
    public void Unmap(uint at) => _blocks.RemoveAll(b => b.Base == at);

    /// <summary>Writes a dword into whichever block holds <paramref name="at"/>.</summary>
    public void PokeUInt32(uint at, uint value) => Write(at, BitConverter.GetBytes(value));

    /// <summary>Writes a word into whichever block holds <paramref name="at"/>.</summary>
    public void PokeUInt16(uint at, ushort value) => Write(at, BitConverter.GetBytes(value));

    /// <inheritdoc/>
    public int Read(uint address, byte[] buffer, int count)
    {
        foreach (var (b, data) in _blocks)
        {
            if (address < b) continue;
            long offset = (long)address - b;
            if (offset + count > data.Length) continue;
            Array.Copy(data, offset, buffer, 0, count);
            return count;
        }
        return 0;
    }

    /// <inheritdoc/>
    public bool Write(uint address, byte[] data)
    {
        foreach (var (b, block) in _blocks)
        {
            if (address < b) continue;
            long offset = (long)address - b;
            if (offset + data.Length > block.Length) continue;
            Array.Copy(data, 0, block, offset, data.Length);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public IEnumerable<SourceRegion> Regions()
    {
        foreach (var (b, data) in _blocks)
            yield return new SourceRegion(b, (uint)data.Length);
    }
}

/// <summary>Builds a character record byte-for-byte, so every check works from real field bytes.</summary>
public sealed class RecordBuilder
{
    private readonly byte[] _bytes = new byte[QuestLayout.RecordBytes];

    /// <summary>Starts from the layout of a plausible live character.</summary>
    public RecordBuilder(uint vtable)
    {
        BitConverter.GetBytes(vtable).CopyTo(_bytes, (int)QuestLayout.VTable);
        WriteExperienceTable();
        Name("Gerth the Derth");
        Portrait("bres_head34");
        Level(5);
        Experience(2915);
        NextLevel(4000);
        Health(72);
        Mana(125);
        Gold(2561);
        Fame(0);
        Crime(0);
        Race(4);
        AttributePoints(20);
        SkillPoints(40);
        for (int id = 1; id <= 5; id++) Attribute(id, 23);
        for (int id = 1; id <= 20; id++) { Skill(id, 10); StartingSkill(id, 8); }
    }

    /// <summary>The bytes as they would sit in the target.</summary>
    public byte[] Bytes => _bytes;

    /// <summary>Writes the canonical experience table used as the locator's signature.</summary>
    public RecordBuilder WriteExperienceTable()
    {
        for (int i = 0; i < GameFacts.ExperienceTableEntries; i++)
            BitConverter.GetBytes(Thresholds[i]).CopyTo(_bytes, (int)QuestLayout.ExperienceTable + i * 4);
        return this;
    }

    /// <summary>Corrupts the table so the locator's signature check fails.</summary>
    public RecordBuilder BreakExperienceTable()
    {
        BitConverter.GetBytes(12345u).CopyTo(_bytes, (int)QuestLayout.ExperienceTable);
        return this;
    }

    /// <summary>Sets the name as an inline (short) <c>std::string</c>.</summary>
    public RecordBuilder Name(string value) => InlineString((int)QuestLayout.Name, value);

    /// <summary>Sets the portrait id as an inline <c>std::string</c>.</summary>
    public RecordBuilder Portrait(string value) => InlineString((int)QuestLayout.PortraitId, value);

    /// <summary>
    /// Points the portrait string at a heap buffer, as MSVC does once the value exceeds 15
    /// characters. The caller maps <paramref name="pointer"/> with <see cref="SpilledBytes"/>.
    /// </summary>
    public RecordBuilder SpilledPortrait(uint pointer, string value)
    {
        int at = (int)QuestLayout.PortraitId;
        Array.Clear(_bytes, at, StdString.Bytes);
        BitConverter.GetBytes(pointer).CopyTo(_bytes, at);
        BitConverter.GetBytes((uint)value.Length).CopyTo(_bytes, at + 16);
        BitConverter.GetBytes((uint)Math.Max(31, value.Length)).CopyTo(_bytes, at + 20);
        return this;
    }

    /// <summary>The heap bytes a spilled string points at.</summary>
    public static byte[] SpilledBytes(string value)
    {
        var bytes = new byte[value.Length + 1];
        Encoding.ASCII.GetBytes(value).CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>Sets the level word.</summary>
    public RecordBuilder Level(int v) => Word((int)QuestLayout.Level, v);

    /// <summary>Sets total experience.</summary>
    public RecordBuilder Experience(long v) => Dword((int)QuestLayout.Experience, v);

    /// <summary>Sets the cached next-level threshold.</summary>
    public RecordBuilder NextLevel(long v) => Dword((int)QuestLayout.ExperienceForNextLevel, v);

    /// <summary>Sets current health.</summary>
    public RecordBuilder Health(int v) => Word((int)QuestLayout.Health, v);

    /// <summary>Sets current mana.</summary>
    public RecordBuilder Mana(int v) => Word((int)QuestLayout.Mana, v);

    /// <summary>Sets gold.</summary>
    public RecordBuilder Gold(long v) => Dword((int)QuestLayout.Gold, v);

    /// <summary>Sets fame.</summary>
    public RecordBuilder Fame(int v)
    {
        BitConverter.GetBytes((short)v).CopyTo(_bytes, (int)QuestLayout.Fame);
        return this;
    }

    /// <summary>Sets crime.</summary>
    public RecordBuilder Crime(long v) => Dword((int)QuestLayout.Crime, v);

    /// <summary>Sets the race id.</summary>
    public RecordBuilder Race(uint v) => Dword((int)QuestLayout.Race, v);

    /// <summary>Sets unspent attribute points.</summary>
    public RecordBuilder AttributePoints(int v) => Word((int)QuestLayout.AttributePoints, v);

    /// <summary>Sets unspent skill points.</summary>
    public RecordBuilder SkillPoints(int v) => Word((int)QuestLayout.SkillPoints, v);

    /// <summary>Sets base attribute <paramref name="id"/> (1..5).</summary>
    public RecordBuilder Attribute(int id, int v) => Word((int)QuestLayout.BaseAttributes + id * 2, v);

    /// <summary>Sets base skill <paramref name="id"/> (1..20).</summary>
    public RecordBuilder Skill(int id, int v) => Word((int)QuestLayout.BaseSkills + id * 2, v);

    /// <summary>Sets the creation-time value of skill <paramref name="id"/>.</summary>
    public RecordBuilder StartingSkill(int id, int v) => Word((int)QuestLayout.StartingSkills + id * 2, v);

    /// <summary>Turns the record into the game's pristine new-character prototype.</summary>
    public RecordBuilder AsPrototype()
    {
        Name("");
        NextLevel(0);
        Level(1);
        Experience(0);
        Gold(0);
        Health(40);
        Mana(40);
        return this;
    }

    // Latin-1, not ASCII: the game stores single-byte characters and a player may well type an
    // accented one, so the fixture has to be able to express that.
    private RecordBuilder InlineString(int at, string value)
    {
        Array.Clear(_bytes, at, StdString.Bytes);
        var ascii = Encoding.Latin1.GetBytes(value);
        if (ascii.Length > StdString.InlineCapacity)
            throw new ArgumentException($"'{value}' does not fit in the inline buffer.", nameof(value));
        ascii.CopyTo(_bytes, at);
        BitConverter.GetBytes((uint)ascii.Length).CopyTo(_bytes, at + 16);
        BitConverter.GetBytes((uint)StdString.InlineCapacity).CopyTo(_bytes, at + 20);
        return this;
    }

    private RecordBuilder Word(int at, int v)
    {
        BitConverter.GetBytes((ushort)v).CopyTo(_bytes, at);
        return this;
    }

    private RecordBuilder Dword(int at, long v)
    {
        BitConverter.GetBytes((uint)v).CopyTo(_bytes, at);
        return this;
    }

    /// <summary>The real game's per-level thresholds, so the fixture and the game agree.</summary>
    public static readonly uint[] Thresholds = BuildThresholds();

    private static uint[] BuildThresholds()
    {
        // The first eight are the locator's signature; the rest only have to be strictly increasing
        // for the level maths to be meaningful, so they follow the game's own shape.
        var table = new uint[GameFacts.ExperienceTableEntries];
        uint[] head = { 400, 900, 1500, 2500, 4000, 7000, 11000, 17000, 25000, 40000 };
        head.CopyTo(table, 0);
        uint step = 20000;
        for (int i = head.Length; i < table.Length; i++)
        {
            table[i] = table[i - 1] + step;
            step += 2000;
        }
        return table;
    }
}

/// <summary>Assembles a whole fake process: a mapped image, an engine object and its records.</summary>
public static class FakeGame
{
    /// <summary>Where the fake image is mapped — deliberately not the PE's preferred base.</summary>
    public const uint ModuleBase = 0x0026_0000;

    /// <summary>Preferred base in the fake PE header, which nothing may rely on.</summary>
    public const uint PreferredImageBase = 0x0040_0000;

    /// <summary>Mapped size of the fake image.</summary>
    public const uint ImageSize = 0x0038_F000;

    /// <summary>RVA of the vtable the character records point at — inside the fake <c>.rdata</c>.</summary>
    public const uint VTableRva = 0x0030_AA24;

    /// <summary>Where the fake engine object lives.</summary>
    public const uint EngineAddress = 0x041F_92D0;

    /// <summary>Offset of the prototype record inside the engine object, as the real game has it.</summary>
    public const uint PrototypeInEngine = 0x06F0;

    /// <summary>Size of the fake engine block; large enough to hold both records.</summary>
    public const int EngineBytes = 0x5000;

    /// <summary>Heap block the live record's portrait string spills into, as the real game's does.</summary>
    public const uint PortraitHeap = 0x0450_0000;

    /// <summary>The 21-character portrait id the live session was observed to hold.</summary>
    public const string PortraitValue = "bres_head00_racederth";

    /// <summary>Absolute address of the live record.</summary>
    public const uint LiveRecord = EngineAddress + QuestLayout.RecordInEngine;

    /// <summary>Absolute address of the prototype record.</summary>
    public const uint PrototypeRecord = EngineAddress + PrototypeInEngine;

    /// <summary>
    /// Builds a process containing a mapped image, a static slot pointing at the engine object, a
    /// live character record and the game's new-character prototype beside it.
    /// </summary>
    public static FakeMemory BuildGame(Action<RecordBuilder>? customise = null)
    {
        var mem = new FakeMemory { ModuleBase = ModuleBase, ModuleSize = (int)ImageSize };
        mem.Map(ModuleBase, BuildHeader());

        // The .data page holding the engine pointer.
        uint slotPage = ModuleBase + (QuestLayout.EngineSlotRva & ~0xFFFu);
        mem.MapZeros(slotPage, 0x1000);
        mem.PokeUInt32(ModuleBase + QuestLayout.EngineSlotRva, EngineAddress);

        var engine = new byte[EngineBytes];
        mem.Map(EngineAddress, engine);

        // The live record's portrait id is 21 characters, so — as in the real game — the string
        // spills to the heap and the inline union holds a pointer instead of characters.
        mem.Map(PortraitHeap, RecordBuilder.SpilledBytes(PortraitValue));

        var live = new RecordBuilder(ModuleBase + VTableRva).SpilledPortrait(PortraitHeap, PortraitValue);
        customise?.Invoke(live);
        live.Bytes.CopyTo(engine, (int)QuestLayout.RecordInEngine);

        var prototype = new RecordBuilder(ModuleBase + VTableRva).AsPrototype();
        prototype.Bytes.CopyTo(engine, (int)PrototypeInEngine);

        return mem;
    }

    /// <summary>Parses the fake image's header the same way the trainer does at run time.</summary>
    public static PeImage Image(FakeMemory mem)
    {
        var header = new byte[PeImage.HeaderBytes];
        mem.Read(mem.ModuleBase, header, header.Length);
        return PeImage.Parse(header) ?? throw new InvalidOperationException("fake header did not parse");
    }

    /// <summary>
    /// A PE32 header with the same three sections the real image has, so the locator's "is this RVA
    /// writable data / read-only data" questions have real answers.
    /// </summary>
    public static byte[] BuildHeader()
    {
        var h = new byte[PeImage.HeaderBytes];
        h[0] = (byte)'M'; h[1] = (byte)'Z';
        const int peOffset = 0x80;
        BitConverter.GetBytes(peOffset).CopyTo(h, 0x3C);

        BitConverter.GetBytes(0x00004550u).CopyTo(h, peOffset);       // "PE\0\0"
        int coff = peOffset + 4;
        BitConverter.GetBytes((ushort)0x014C).CopyTo(h, coff);        // i386
        BitConverter.GetBytes((ushort)3).CopyTo(h, coff + 2);         // three sections
        BitConverter.GetBytes(GameFacts.KnownTimeDateStamp).CopyTo(h, coff + 4);
        BitConverter.GetBytes((ushort)0xE0).CopyTo(h, coff + 16);     // optional header size
        BitConverter.GetBytes((ushort)0x0102).CopyTo(h, coff + 18);   // EXECUTABLE_IMAGE | 32BIT_MACHINE

        int opt = coff + 20;
        BitConverter.GetBytes((ushort)0x010B).CopyTo(h, opt);         // PE32
        BitConverter.GetBytes(PreferredImageBase).CopyTo(h, opt + 28);
        BitConverter.GetBytes(ImageSize).CopyTo(h, opt + 56);
        BitConverter.GetBytes((ushort)0x8140).CopyTo(h, opt + 70);    // DYNAMICBASE | NX | TS-aware

        int table = opt + 0xE0;
        WriteSection(h, table + 0, ".text", 0x0000_1000, 0x002B_2506, 0x6000_0020);
        WriteSection(h, table + 40, ".rdata", 0x002B_4000, 0x0007_A3F2, 0x4000_0040);
        WriteSection(h, table + 80, ".data", 0x0032_F000, 0x0000_6C8C, 0xC000_0040);
        return h;
    }

    private static void WriteSection(byte[] h, int at, string name, uint rva, uint vsize, uint chars)
    {
        Encoding.ASCII.GetBytes(name).CopyTo(h, at);
        BitConverter.GetBytes(vsize).CopyTo(h, at + 8);
        BitConverter.GetBytes(rva).CopyTo(h, at + 12);
        BitConverter.GetBytes(chars).CopyTo(h, at + 36);
    }
}
