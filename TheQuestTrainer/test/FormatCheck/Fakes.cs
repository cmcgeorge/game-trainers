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

    /// <summary>Points the carried-items vector at <paramref name="begin"/>..<paramref name="end"/>.</summary>
    public RecordBuilder Inventory(uint begin, uint end, uint capacity = 0)
    {
        Dword((int)ItemLayout.InventoryBegin, begin);
        Dword((int)ItemLayout.InventoryEnd, end);
        Dword((int)ItemLayout.InventoryCapacity, capacity == 0 ? end : capacity);
        return this;
    }

    /// <summary>Puts <paramref name="item"/> in body slot <paramref name="slot"/> of weapon set <paramref name="set"/>.</summary>
    public RecordBuilder Equip(int set, int slot, uint item)
    {
        uint at = ItemLayout.EquipmentSlot(0, set, slot);
        Dword((int)at, item);
        return this;
    }

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

/// <summary>
/// Lays item types, their strings and item objects into a fake heap, so the inventory reader, the
/// catalog sweep and every item edit can be exercised without a game.
///
/// The geometry mirrors the real thing where it matters: types stride by their own size plus a heap
/// header, exactly as the game's do, and the id and name are C strings in a separate block reached
/// by pointer rather than characters inside the type. That second part is what makes the sweep's
/// string checks real — a fixture with the names inline would let a broken reader pass.
/// </summary>
public sealed class ItemHeap
{
    /// <summary>Where item types are laid out.</summary>
    public const uint TypeBase = 0x0500_0000;

    /// <summary>Where the ids and names live.</summary>
    public const uint TextBase = 0x0508_0000;

    /// <summary>Where item objects are laid out.</summary>
    public const uint ItemBase = 0x0510_0000;

    /// <summary>Where enchantment vectors and their entries live.</summary>
    public const uint EnchantBase = 0x0518_0000;

    /// <summary>Where the carried-items pointer array lives.</summary>
    public const uint VectorBase = 0x0520_0000;

    /// <summary>Stride between types: the object plus the eight-byte heap header the real game has.</summary>
    public const int TypeStride = ItemLayout.TypeBytes + 8;

    /// <summary>Stride between items: sixteen bytes of allocation plus the same header.</summary>
    public const int ItemStride = 24;

    private readonly FakeMemory _mem;
    private readonly uint _engine;
    private readonly uint _vtable;
    private readonly byte[] _types = new byte[0x4000];
    private readonly byte[] _text = new byte[0x4000];
    private readonly byte[] _items = new byte[0x1000];
    private readonly byte[] _enchants = new byte[0x400];
    private readonly byte[] _vector = new byte[0x400];
    private int _typeCount, _itemCount, _textUsed, _enchantUsed;

    /// <summary>Maps the five heap blocks into <paramref name="mem"/>.</summary>
    public ItemHeap(FakeMemory mem, uint engine, uint vtable)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
        _engine = engine;
        _vtable = vtable;
        mem.Map(TypeBase, _types);
        mem.Map(TextBase, _text);
        mem.Map(ItemBase, _items);
        mem.Map(EnchantBase, _enchants);
        mem.Map(VectorBase, _vector);
    }

    /// <summary>Item types added so far, in the order they were added.</summary>
    public List<uint> Types { get; } = new();

    /// <summary>Item objects added so far.</summary>
    public List<uint> Items { get; } = new();

    /// <summary>
    /// Adds an object that would pass every cheap test for an item type — the engine back-pointer,
    /// a real category, readable ASCII strings — but whose vtable does not point into the game
    /// module. This is the false positive the sweep's vtable check exists for, and planting one is
    /// what stops "the sweep finds only real types" from passing for the wrong reason.
    /// </summary>
    public uint AddDecoy(string name, uint vtable = 0x7FFF_0000)
    {
        uint address = AddType($"decoy_{name}", name, category: 1, subtype: 2, vtable: vtable);
        Types.Remove(address);
        return address;
    }

    /// <summary>Adds an item type and returns its address.</summary>
    public uint AddType(string id, string name, int category, int subtype,
                        int weight = 100, int maxCondition = 0, int damageMin = 0, int damageMax = 0,
                        uint enchantments = 0, bool lightWeapon = false, int enchantStorage = 0,
                        uint vtable = 0)
    {
        int at = _typeCount++ * TypeStride;
        uint address = TypeBase + (uint)at;

        BitConverter.GetBytes(_engine).CopyTo(_types, at + (int)ItemLayout.TypeEngine);
        BitConverter.GetBytes(vtable == 0 ? _vtable : vtable).CopyTo(_types, at + (int)ItemLayout.TypeVTable);
        BitConverter.GetBytes(AddText(id)).CopyTo(_types, at + (int)ItemLayout.TypeId);
        BitConverter.GetBytes(AddText($"bres_{id}")).CopyTo(_types, at + (int)ItemLayout.TypeResourceId);
        BitConverter.GetBytes(AddText(name)).CopyTo(_types, at + (int)ItemLayout.TypeName);
        BitConverter.GetBytes(enchantments).CopyTo(_types, at + (int)ItemLayout.TypeEnchantments);
        BitConverter.GetBytes((ushort)weight).CopyTo(_types, at + (int)ItemLayout.TypeWeight);
        BitConverter.GetBytes((ushort)damageMin).CopyTo(_types, at + (int)ItemLayout.TypeDamageMin);
        BitConverter.GetBytes((ushort)damageMax).CopyTo(_types, at + (int)ItemLayout.TypeDamageMax);
        BitConverter.GetBytes((ushort)enchantStorage).CopyTo(_types, at + (int)ItemLayout.TypeEnchantStorage);
        BitConverter.GetBytes((ushort)maxCondition).CopyTo(_types, at + (int)ItemLayout.TypeMaxCondition);
        _types[at + (int)ItemLayout.TypeCategory] = (byte)category;
        _types[at + (int)ItemLayout.TypeSubtype] = (byte)subtype;
        _types[at + (int)ItemLayout.TypeFlags] = lightWeapon ? ItemLayout.FlagLightWeapon : (byte)0;

        Types.Add(address);
        return address;
    }

    /// <summary>Adds an item object pointing at <paramref name="type"/>, and returns its address.</summary>
    public uint AddItem(uint type, int meter = 0, uint enchantments = 0)
    {
        int at = _itemCount++ * ItemStride;
        uint address = ItemBase + (uint)at;
        BitConverter.GetBytes(type).CopyTo(_items, at + (int)ItemLayout.ItemType);
        BitConverter.GetBytes(enchantments).CopyTo(_items, at + (int)ItemLayout.ItemEnchantments);
        BitConverter.GetBytes((ushort)meter).CopyTo(_items, at + (int)ItemLayout.ItemCondition);
        Items.Add(address);
        return address;
    }

    /// <summary>
    /// Adds a one-entry enchantment vector whose entry carries <paramref name="maxCharges"/> at
    /// <c>+4</c>, which is where the game's own "recharge the wand" code reads a full charge count.
    /// Returns the address of the vector, which is what an item or a type points at.
    /// </summary>
    public uint AddChargeEnchantment(int maxCharges)
    {
        int entryAt = _enchantUsed; _enchantUsed += 16;
        int vectorAt = _enchantUsed; _enchantUsed += 16;
        int arrayAt = _enchantUsed; _enchantUsed += 16;

        BitConverter.GetBytes((ushort)maxCharges).CopyTo(_enchants, entryAt + 4);

        // The vector is the usual begin/end pair, and its single element points at the entry.
        BitConverter.GetBytes(EnchantBase + (uint)entryAt).CopyTo(_enchants, arrayAt);
        BitConverter.GetBytes(EnchantBase + (uint)arrayAt).CopyTo(_enchants, vectorAt);
        BitConverter.GetBytes(EnchantBase + (uint)arrayAt + 4).CopyTo(_enchants, vectorAt + 4);
        return EnchantBase + (uint)vectorAt;
    }

    /// <summary>Writes the carried-items pointer array and returns its begin and end.</summary>
    public (uint Begin, uint End) Vector(params uint[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Array.Clear(_vector);
        for (int i = 0; i < items.Length; i++)
            BitConverter.GetBytes(items[i]).CopyTo(_vector, i * 4);
        return (VectorBase, VectorBase + (uint)items.Length * 4);
    }

    /// <summary>Copies a NUL-terminated string into the text block and returns its address.</summary>
    private uint AddText(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        uint address = TextBase + (uint)_textUsed;
        bytes.CopyTo(_text, _textUsed);
        _textUsed += bytes.Length + 1;
        return address;
    }

    /// <summary>Rewrites an existing type's category, for a check that wants an invalid one.</summary>
    public void SetCategory(uint type, int category) =>
        _mem.Write(type + ItemLayout.TypeCategory, new[] { (byte)category });

    /// <summary>Rewrites an existing type's engine back-pointer, so it stops validating.</summary>
    public void SetEngine(uint type, uint engine) => _mem.PokeUInt32(type + ItemLayout.TypeEngine, engine);
}

/// <summary>
/// Lays effect objects, their group vectors and disease types into a fake heap, and writes the
/// effect-kind table into the record, so every condition the trainer reads or cures can be built
/// without a game.
///
/// The geometry mirrors the real thing where it matters: effects stride by their own size plus a
/// heap header, the group vectors are the same begin/end/capacity triples the record holds, and the
/// kind table really is consulted — a check can move poison to another group and the reader has to
/// follow, which is what stops a hard-coded group number from passing.
/// </summary>
public sealed class ConditionHeap
{
    /// <summary>Where effect objects are laid out.</summary>
    public const uint EffectBase = 0x0530_0000;

    /// <summary>Where the groups' pointer arrays are laid out, one slice per group.</summary>
    public const uint VectorBase = 0x0538_0000;

    /// <summary>Where disease type objects live.</summary>
    public const uint DiseaseBase = 0x0540_0000;

    /// <summary>Where the disease vector's pointer array lives.</summary>
    public const uint DiseaseVectorBase = 0x0544_0000;

    /// <summary>Where disease ids and names live.</summary>
    public const uint TextBase = 0x0548_0000;

    /// <summary>Stride between effects: the 20-byte object plus the eight-byte heap header.</summary>
    public const int EffectStride = ConditionLayout.EffectBytes + 8;

    /// <summary>Bytes reserved for each group's pointer array.</summary>
    public const int GroupArrayBytes = 64;

    /// <summary>Stride between disease types, which are opaque to the trainer beyond two pointers.</summary>
    public const int DiseaseStride = 0x20;

    /// <summary>The group each of the three effect kinds is filed under in the shipped build.</summary>
    public const int PoisonGroup = 23;

    /// <inheritdoc cref="PoisonGroup"/>
    public const int CurseGroup = 22;

    /// <inheritdoc cref="PoisonGroup"/>
    public const int ParalysisGroup = 21;

    private readonly FakeMemory _mem;
    private readonly uint _record;
    private readonly byte[] _effects = new byte[0x1000];
    private readonly byte[] _vectors = new byte[GroupArrayBytes * ConditionLayout.EffectGroupSlots];
    private readonly byte[] _diseases = new byte[0x400];
    private readonly byte[] _diseaseVector = new byte[0x100];
    private readonly byte[] _text = new byte[0x400];
    private int _effectCount, _diseaseCount, _textUsed;

    /// <summary>Maps the heap blocks into <paramref name="mem"/> and writes the shipped kind table.</summary>
    public ConditionHeap(FakeMemory mem, uint record)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
        _record = record;
        mem.Map(EffectBase, _effects);
        mem.Map(VectorBase, _vectors);
        mem.Map(DiseaseBase, _diseases);
        mem.Map(DiseaseVectorBase, _diseaseVector);
        mem.Map(TextBase, _text);
        WriteKindTable(mem, record);
    }

    /// <summary>
    /// Writes the effect-kind table the shipped build holds. Called for every fake game, not only
    /// the ones a condition check builds: a record without it is not a record the game would have.
    /// </summary>
    public static void WriteKindTable(FakeMemory mem, uint record)
    {
        ArgumentNullException.ThrowIfNull(mem);
        SetKind(mem, record, ConditionLayout.KindPoison, PoisonGroup);
        SetKind(mem, record, ConditionLayout.KindCurse, CurseGroup);
        SetKind(mem, record, ConditionLayout.KindParalysis, ParalysisGroup);
    }

    /// <summary>Files effect kind <paramref name="kind"/> under <paramref name="group"/>.</summary>
    public static void SetKind(FakeMemory mem, uint record, int kind, int group)
    {
        ArgumentNullException.ThrowIfNull(mem);
        mem.PokeUInt32(ConditionLayout.EffectGroupSlot(record, kind), (uint)group);
    }

    /// <summary>Files a kind under a different group, so a check can prove the table is followed.</summary>
    public void SetKind(int kind, int group) => SetKind(_mem, _record, kind, group);

    /// <summary>Adds an effect object and returns its address.</summary>
    public uint AddEffect(int magnitude, int duration, byte source, int group)
    {
        int at = _effectCount++ * EffectStride;
        uint address = EffectBase + (uint)at;

        BitConverter.GetBytes((short)magnitude).CopyTo(_effects, at + (int)ConditionLayout.EffectMagnitude);
        BitConverter.GetBytes(duration).CopyTo(_effects, at + (int)ConditionLayout.EffectDuration);
        _effects[at + (int)ConditionLayout.EffectGroup] = (byte)group;
        _effects[at + (int)ConditionLayout.EffectSource] = source;
        return address;
    }

    /// <summary>Puts <paramref name="effects"/> in group <paramref name="group"/>, in that order.</summary>
    public void SetGroup(int group, params uint[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.Length * 4 > GroupArrayBytes)
            throw new ArgumentException("more effects than the fixture reserves room for", nameof(effects));

        int at = group * GroupArrayBytes;
        Array.Clear(_vectors, at, GroupArrayBytes);
        for (int i = 0; i < effects.Length; i++)
            BitConverter.GetBytes(effects[i]).CopyTo(_vectors, at + i * 4);

        uint begin = VectorBase + (uint)at;
        Vector(ConditionLayout.EffectGroupBegin(_record, group), begin, begin + (uint)effects.Length * 4);
    }

    /// <summary>Points group <paramref name="group"/> at an arbitrary pair, for the malformed cases.</summary>
    public void SetGroupRaw(int group, uint begin, uint end) =>
        Vector(ConditionLayout.EffectGroupBegin(_record, group), begin, end);

    /// <summary>The address of group <paramref name="group"/>'s pointer array.</summary>
    public static uint GroupArray(int group) => VectorBase + (uint)(group * GroupArrayBytes);

    /// <summary>Adds a disease type and returns its address.</summary>
    public uint AddDiseaseType(string id, string name)
    {
        int at = _diseaseCount++ * DiseaseStride;
        uint address = DiseaseBase + (uint)at;
        BitConverter.GetBytes(AddText(id)).CopyTo(_diseases, at + (int)ConditionLayout.DiseaseTypeId);
        BitConverter.GetBytes(AddText(name)).CopyTo(_diseases, at + (int)ConditionLayout.DiseaseTypeName);
        return address;
    }

    /// <summary>Gives the character the diseases named by <paramref name="types"/>.</summary>
    public void SetDiseases(params uint[] types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Array.Clear(_diseaseVector);
        for (int i = 0; i < types.Length; i++)
            BitConverter.GetBytes(types[i]).CopyTo(_diseaseVector, i * 4);

        Vector(_record + ConditionLayout.DiseasesBegin, DiseaseVectorBase,
               DiseaseVectorBase + (uint)types.Length * 4);
    }

    /// <summary>Points the disease vector at an arbitrary pair, for the malformed cases.</summary>
    public void SetDiseasesRaw(uint begin, uint end) =>
        Vector(_record + ConditionLayout.DiseasesBegin, begin, end);

    /// <summary>Rewrites an existing type's name pointer, so it stops reading back as one.</summary>
    public void SetDiseaseName(uint type, uint pointer) =>
        _mem.PokeUInt32(type + ConditionLayout.DiseaseTypeName, pointer);

    /// <summary>Writes a begin/end/capacity triple at <paramref name="slot"/>.</summary>
    private void Vector(uint slot, uint begin, uint end)
    {
        _mem.PokeUInt32(slot, begin);
        _mem.PokeUInt32(slot + 4, end);
        _mem.PokeUInt32(slot + 8, end);
    }

    private uint AddText(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        uint address = TextBase + (uint)_textUsed;
        bytes.CopyTo(_text, _textUsed);
        _textUsed += bytes.Length + 1;
        return address;
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

        // The effect-kind table lives past the part of the record RecordBuilder covers, and every
        // real record has one, so it is written here rather than only by the condition checks: a
        // fixture without it would make "the trainer cannot find where poison lives" the normal case.
        ConditionHeap.WriteKindTable(mem, LiveRecord);

        return mem;
    }

    /// <summary>
    /// A fake game whose character is poisoned, cursed, paralysed and carrying two diseases, with
    /// one effect in each group that a cure must <i>not</i> take — the racial modifiers a real
    /// character has — and one penalty granted by a disease, which a cure must take only because the
    /// disease goes with it.
    /// </summary>
    public static (FakeMemory Memory, ConditionHeap Heap) BuildAfflictedGame()
    {
        var mem = BuildGame();
        var heap = new ConditionHeap(mem, LiveRecord);

        heap.SetGroup(ConditionHeap.PoisonGroup,
            heap.AddEffect(2, 0, source: 6, group: ConditionHeap.PoisonGroup));

        heap.SetGroup(ConditionHeap.CurseGroup,
            heap.AddEffect(0, 3, source: 2, group: ConditionHeap.CurseGroup),
            heap.AddEffect(0, 14, source: 3, group: ConditionHeap.CurseGroup));

        heap.SetGroup(ConditionHeap.ParalysisGroup,
            heap.AddEffect(0, 5, source: 6, group: ConditionHeap.ParalysisGroup));

        // Group 2 is where the game keeps attribute modifiers. A Derth's −5 Strength is source 5,
        // its race, and no cure removes it; the −3 beside it came from a disease and goes when the
        // disease does.
        heap.SetGroup(2,
            heap.AddEffect(-5, 0, ConditionLayout.SourceRace, group: 2),
            heap.AddEffect(-3, 0, ConditionLayout.SourceDisease, group: 2));

        heap.SetDiseases(heap.AddDiseaseType("base_dis_greyfever", "Grey Fever"),
                         heap.AddDiseaseType("base_dis_rot", "Bone Rot"));

        return (mem, heap);
    }

    /// <summary>
    /// A fake game whose character carries a pack covering every shape the reader has to cope with:
    /// something that wears out, something already at full condition, something with no meter at
    /// all, a wand whose charges come from an enchantment, and a stack of ammunition. One item is
    /// equipped in each weapon set, because an item in the inactive set is still equipped.
    /// </summary>
    public static (FakeMemory Memory, ItemHeap Heap) BuildGameWithItems()
    {
        var mem = BuildGame();
        var heap = new ItemHeap(mem, EngineAddress, ModuleBase + VTableRva);

        uint charges = heap.AddChargeEnchantment(WandCharges);

        uint sword = heap.AddType("base_weap_longsword", "Longsword", 1, 2, weight: 1000, maxCondition: 10000, damageMin: 6, damageMax: 17);
        uint helm = heap.AddType("base_helm_helm", "Helm", 2, 4, weight: 200, maxCondition: 2500);
        uint bread = heap.AddType("base_com_bread", "Bread", 14, 1, weight: 30);
        uint wand = heap.AddType("base_wndfire", "Fire", 9, 4, weight: 20, enchantments: charges);
        uint quiver = heap.AddType("base_weap_quiver", "Arrows", 1, 11, weight: 50);

        uint swordItem = heap.AddItem(sword, meter: 4000);
        uint helmItem = heap.AddItem(helm, meter: 2500);
        heap.AddItem(bread);
        heap.AddItem(wand, meter: 3);
        heap.AddItem(quiver, meter: 7);

        var (begin, end) = heap.Vector(heap.Items.ToArray());
        mem.PokeUInt32(LiveRecord + ItemLayout.InventoryBegin, begin);
        mem.PokeUInt32(LiveRecord + ItemLayout.InventoryEnd, end);
        mem.PokeUInt32(LiveRecord + ItemLayout.InventoryCapacity, end);

        mem.PokeUInt32(ItemLayout.EquipmentSlot(LiveRecord, 0, 1), helmItem);
        mem.PokeUInt32(ItemLayout.EquipmentSlot(LiveRecord, 1, 4), swordItem);

        return (mem, heap);
    }

    /// <summary>Full charge count of the fixture's wand.</summary>
    public const int WandCharges = 12;

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
