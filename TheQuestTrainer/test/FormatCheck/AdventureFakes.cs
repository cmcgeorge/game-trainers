using System.Buffers.Binary;
using System.Text;
using TheQuestTrainer.Adventures;

namespace TheQuestTrainer.FormatCheck;

/// <summary>
/// Writes a record the way the game's own <c>SArchive</c> writes one: bytes and strings unaligned,
/// 16-bit words on even offsets, 32-bit words on multiples of four.
///
/// This is the <i>write</i> side the trainer deliberately does not ship — the trainer only reads
/// worlds — and it exists so the reader can be checked against bytes laid out by something other
/// than itself. A fixture that reused the reader's own arithmetic would agree with any alignment
/// bug it happened to contain.
/// </summary>
public sealed class ArchiveWriter
{
    private readonly List<byte> _bytes = [];

    /// <summary>The record so far, padded to a multiple of four the way the game's writer leaves it.</summary>
    public byte[] ToRecord(int trailingPadding = 4)
    {
        var record = new List<byte>(_bytes);
        for (int i = 0; i < trailingPadding; i++) record.Add(0);
        while (record.Count % 4 != 0) record.Add(0);
        return [.. record];
    }

    /// <summary>The record without the writer's trailing slack, for the checks that need an exact fit.</summary>
    public byte[] ToExactRecord() => [.. _bytes];

    public ArchiveWriter Byte(int value)
    {
        _bytes.Add((byte)value);
        return this;
    }

    public ArchiveWriter Bool(bool value) => Byte(value ? 1 : 0);

    public ArchiveWriter Word(int value)
    {
        Align(2);
        var word = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)value);
        _bytes.AddRange(word);
        return this;
    }

    public ArchiveWriter Dword(long value)
    {
        Align(4);
        var word = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(word, (uint)value);
        _bytes.AddRange(word);
        return this;
    }

    public ArchiveWriter Text(string value)
    {
        _bytes.AddRange(Encoding.Latin1.GetBytes(value));
        _bytes.Add(0);
        return this;
    }

    /// <summary>A length-prefixed opaque blob, as a map object's per-kind payload is stored.</summary>
    public ArchiveWriter Blob(byte[] payload)
    {
        Word(payload.Length);
        _bytes.AddRange(payload);
        return this;
    }

    /// <summary>Raw bytes, for the checks that need to plant something malformed.</summary>
    public ArchiveWriter Raw(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    private void Align(int to)
    {
        while (_bytes.Count % to != 0) _bytes.Add(0);
    }
}

/// <summary>
/// Builds a synthetic Palm database with the same header, record list and four-byte record padding
/// the shipped worlds use.
///
/// Needs no game and no copyrighted data: every string in the fixture is made up.
/// </summary>
public sealed class PalmDatabaseBuilder(string name, string type = PalmDatabase.WorldType,
                                        string creator = PalmDatabase.QuestCreator)
{
    private readonly List<(int Id, byte[] Bytes)> _records = [];

    /// <summary>Adds a record with a given unique id.</summary>
    public PalmDatabaseBuilder Add(int id, byte[] bytes)
    {
        var padded = new List<byte>(bytes);
        while (padded.Count % 4 != 0) padded.Add(0);
        _records.Add((id, [.. padded]));
        return this;
    }

    /// <summary>Adds a record built by <paramref name="writer"/>.</summary>
    public PalmDatabaseBuilder Add(int id, ArchiveWriter writer) => Add(id, writer.ToRecord());

    /// <summary>The file.</summary>
    public byte[] Build()
    {
        const int headerBytes = 78;
        int listBytes = _records.Count * 8;
        int at = headerBytes + listBytes;
        if (at % 4 != 0) at += 4 - at % 4;

        var file = new List<byte>(new byte[at]);

        var nameBytes = Encoding.Latin1.GetBytes(name);
        for (int i = 0; i < Math.Min(31, nameBytes.Length); i++) file[i] = nameBytes[i];
        Encoding.Latin1.GetBytes(type).CopyTo(CollectionsMarshalSpan(file, 60, 4));
        Encoding.Latin1.GetBytes(creator).CopyTo(CollectionsMarshalSpan(file, 64, 4));
        BinaryPrimitives.WriteUInt16BigEndian(CollectionsMarshalSpan(file, 76, 2), (ushort)_records.Count);

        for (int i = 0; i < _records.Count; i++)
        {
            int entry = headerBytes + i * 8;
            BinaryPrimitives.WriteUInt32BigEndian(CollectionsMarshalSpan(file, entry, 4), (uint)at);
            BinaryPrimitives.WriteUInt32BigEndian(CollectionsMarshalSpan(file, entry + 4, 4), (uint)_records[i].Id);
            at += _records[i].Bytes.Length;
        }

        foreach (var record in _records) file.AddRange(record.Bytes);
        return [.. file];
    }

    private static Span<byte> CollectionsMarshalSpan(List<byte> list, int at, int length) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(at, length);
}

/// <summary>
/// A whole synthetic adventure: a header, one of each object the reader understands, a map list and
/// per-map placement records.
///
/// The fixture deliberately covers the cases that broke a naive reader:
/// <list type="bullet">
/// <item>a dialog topic that is a <i>reference</i> into the shared pool, which stores no text at all,
/// beside one that carries its own;</item>
/// <item>a map whose id ends in four digits but does not start with the grid prefix, so it is an
/// interior rather than cell 1, 2;</item>
/// <item>a map with a placement record and a map without one, so "bare" and "nothing named" stay
/// different things;</item>
/// <item>a record after the map list whose first byte is an item's tag, which is what a tag-driven
/// reader picks up and decodes into rubbish.</item>
/// </list>
/// </summary>
public static class FakeAdventure
{
    /// <summary>The version the fixture writes, matching the shipped worlds.</summary>
    public const int Version = AdventureLayout.KnownVersion;

    /// <summary>The grid prefix the fixture's outdoor maps are named from.</summary>
    public const string GridPrefix = "test_s";

    /// <summary>Record id of the first outdoor map; the second is <see cref="MapStride"/> later.</summary>
    public const int FirstMapRecord = 6000;

    /// <summary>
    /// Record ids a map owns, and which of them holds its placement list.
    ///
    /// These are written out as the numbers observed in both shipped worlds rather than taken from
    /// <see cref="AdventureReader"/>, so a check against them pins the layout instead of agreeing
    /// with whatever the reader currently believes.
    /// </summary>
    public const int MapStride = 5;

    /// <inheritdoc cref="MapStride"/>
    public const int PlacementOffset = 3;

    /// <summary>Builds the database file.</summary>
    public static byte[] Build()
    {
        var db = new PalmDatabaseBuilder("TheQuestTest");
        int id = AdventureLayout.FirstContentRecordId;

        db.Add(AdventureLayout.HeaderRecordId, Header("Testland", "test", "TheQuestTest", GridPrefix, 3, 2));

        db.Add(id++, Quests());
        db.Add(id++, Item("test_sword", "Test Sword", "A blade for checking things.", "", 250, 900, 3, 9, 0, 1, 1));
        db.Add(id++, Item("test_shield", "Test Shield", "", "test_spellward", 400, 1600, 0, 0, 7, 2, 1));
        db.Add(id++, Spell());
        db.Add(id++, Monsters());
        db.Add(id++, NpcTypes());
        db.Add(id++, DialogPool());
        db.Add(id++, Npc());
        db.Add(id++, MapObject("test_sign", "The sign reads: beware the test."));
        db.Add(id++, Races());
        db.Add(id++, Skills());
        db.Add(id++, Attributes());
        db.Add(id, MapList());

        // The per-map records. Each map owns five ids; the placement list is the fourth. The second
        // outdoor map has no placement record at all, and the interior's tile record deliberately
        // starts with an item's tag so a tag-driven reader would try to decode it.
        db.Add(FirstMapRecord, new ArchiveWriter().Byte(AdventureLayout.TagMapTiles).Raw(1, 2, 3, 4));
        db.Add(FirstMapRecord + PlacementOffset, Placement("test_sign", "test_villager"));

        db.Add(FirstMapRecord + MapStride,
               new ArchiveWriter().Byte(AdventureLayout.TagMapTiles).Raw(5, 6, 7, 8));

        db.Add(FirstMapRecord + MapStride * 2,
               new ArchiveWriter().Byte(AdventureLayout.TagItem).Raw(0xFF, 0xFF, 0xFF, 0xFF));
        db.Add(FirstMapRecord + MapStride * 2 + PlacementOffset, Placement("test_sign"));

        return db.Build();
    }

    /// <summary>Reads what <see cref="Build"/> produced.</summary>
    public static Adventure Read(out string why)
    {
        var database = PalmDatabase.Parse(Build(), out why)
            ?? throw new InvalidOperationException("the fixture is not a Palm database: " + why);
        return AdventureReader.Read(database, "fixture", out why)
            ?? throw new InvalidOperationException("the fixture did not decode: " + why);
    }

    // ---- records ------------------------------------------------------------------------------

    /// <summary>Record 3999. The first word is uninitialised in the shipped worlds; so is this one.</summary>
    public static byte[] Header(string world, string pack, string database, string gridPrefix,
                                int width, int height, int version = Version,
                                byte magic0 = AdventureLayout.HeaderMagic0)
    {
        var a = new ArchiveWriter()
            .Raw(0xCD, 0xCD, 0xCD, 0xCD)
            .Byte(magic0).Byte(AdventureLayout.HeaderMagic1).Byte(AdventureLayout.HeaderMagic2)
            .Byte(version).Byte(0xCD).Byte(0xCD)
            .Text(world).Text(pack).Text(database);

        if (version >= 0x77) a.Byte(1);
        if (version >= 0x7A) a.Byte(0);

        return a.Word(width).Word(height).Text(gridPrefix).ToRecord();
    }

    private static ArchiveWriter Quests()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagQuestList).Word(2);
        a.Byte(AdventureLayout.TagQuest).Text("test_errand").Text("An errand").Text("Fetch the thing.");
        a.Byte(AdventureLayout.TagQuest).Text("test_rescue").Text("A rescue").Text("Free the other thing.");
        return a;
    }

    /// <summary>One item type, written in the order <c>FUN_00509880</c> reads.</summary>
    public static ArchiveWriter Item(string itemId, string name, string description, string spellId,
                                     int value, int weight, int damageMin, int damageMax, int armour,
                                     int category, int subtype)
    {
        var a = new ArchiveWriter()
            .Byte(AdventureLayout.TagItem)
            .Text(itemId).Text("").Text("pic_" + itemId).Text(name)
            .Text(description).Text("").Text("")
            .Text(spellId);

        // One carried effect, so the holder and the effect list are exercised.
        a.Bool(true)
         .Byte(AdventureLayout.TagEffectHolder).Dword(0).Word(1)
         .Byte(AdventureLayout.TagEffect).Text("test_effect").Word(1).Word(2).Word(3).Word(4)
         .Byte(5).Byte(6).Byte(7).Byte(8);

        return a.Dword(value)
                .Word(weight).Word(damageMin).Word(damageMax).Word(armour).Word(0).Word(100)
                .Byte(0).Byte(category).Byte(subtype).Byte(0).Byte(0)
                .Byte(0)
                .Byte(0);
    }

    private static ArchiveWriter Spell() =>
        new ArchiveWriter()
            .Byte(AdventureLayout.TagSpell)
            .Text("test_spellward").Text("Ward").Text("Keeps the tests away.").Text("pic_ward")
            .Text("").Text("").Text("").Text("").Text("")
            .Word(12).Word(34).Word(56)
            .Word(0)
            .Byte(0).Byte(0).Byte(0).Byte(0)
            .Text("");

    private static ArchiveWriter Monsters()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagMonsterList).Word(1);
        a.Byte(AdventureLayout.TagMonster)
         .Text("test_beast").Text("Test Beast").Text("Test Beasts")
         .Word(2).Text("frame1").Text("frame2")
         .Text("").Text("").Text("").Text("").Text("");
        for (int i = 0; i < 10; i++) a.Word(i * 10);
        a.Byte(0).Byte(42).Byte(0).Byte(0).Byte(0);
        a.Word(0);
        a.Byte(0).Text("");
        return a;
    }

    private static ArchiveWriter NpcTypes()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagNpcTypeList).Word(1);
        a.Byte(AdventureLayout.TagNpcType)
         .Text("test_townsfolk").Text("pic").Text("Townsfolk").Text("walk")
         .Text("")
         .Word(0)
         .Word(1).Byte(AdventureLayout.TagAbility).Word(3).Byte(1).Byte(2).Byte(3).Byte(4);
        for (int i = 0; i < 10; i++) a.Word(i + 1);
        a.Byte(0).Byte(0).Byte(0).Byte(0).Text("").Byte(0).Byte(0).Byte(0);
        return a;
    }

    /// <summary>The shared pool, which always stores its wording.</summary>
    private static ArchiveWriter DialogPool()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagDialog);
        a.Word(1);
        Topic(a, "test_shared", reference: true, "About the town", "What is this place?",
              "It is a fixture.", "test_errand", "Tell me more.");
        a.Word(0);
        return a;
    }

    private static ArchiveWriter Npc()
    {
        var a = new ArchiveWriter()
            .Byte(AdventureLayout.TagNpc)
            .Text("test_villager").Text("Villager").Text("pic").Text("test_townsfolk")
            .Text("").Text("").Text("")
            .Dword(120)
            .Byte(0)
            .Word(0)
            .Byte(0).Byte(0).Byte(0).Byte(0).Byte(0).Byte(0)
            .Word(0);

        // A shop with one entry.
        a.Bool(true).Word(1).Byte(AdventureLayout.TagStock).Text("test_sword").Text("");

        // A conversation with two topics: one referencing the pool, one carrying its own words.
        a.Bool(true).Byte(AdventureLayout.TagDialog);
        a.Word(2);
        Topic(a, "test_shared", reference: true, "", "", "", "", "");
        Topic(a, "test_own", reference: false, "About the rescue", "Who needs rescuing?",
              "My cousin does.", "test_rescue", "I will help.");
        a.Word(0);

        return a.Byte(0).Byte(0);
    }

    /// <summary>
    /// One dialog topic. A reference stores its id and stops; anything else stores the wording, and
    /// that branch is the reason a reader that ignores the flag falls apart two people in.
    /// </summary>
    private static void Topic(ArchiveWriter a, string topicId, bool reference, string label,
                              string question, string reply, string symbol, string choice)
    {
        a.Byte(AdventureLayout.TagDialogTopic).Dword(reference ? 1 : 0).Text(topicId);

        bool writeText = !reference || label.Length > 0;
        if (!writeText) return;

        a.Text(label).Bool(false).Text(question);
        a.Word(1);
        a.Byte(AdventureLayout.TagReply);

        // Five id slots, of which the first is filled.
        a.Bool(true).Byte(AdventureLayout.TagScript).Text(symbol).Word(0).Byte(0).Byte(0).Byte(0);
        for (int i = 1; i < AdventureReader.ReplySymbolSlots; i++) a.Bool(false);

        a.Text(reply);

        // Four follow-up slots, of which the first is filled.
        a.Bool(true).Byte(AdventureLayout.TagChoice).Bool(false).Text(choice);
        for (int i = 1; i < AdventureReader.ReplyChoiceSlots; i++) a.Bool(false);

        a.Blob([]);
    }

    private static ArchiveWriter MapObject(string objectId, string text) =>
        new ArchiveWriter()
            .Byte(AdventureLayout.TagMapObject)
            .Text(objectId)
            .Byte(3)
            .Blob([.. Encoding.Latin1.GetBytes(text), 0]);

    private static ArchiveWriter Races()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagRaceList).Word(1);
        a.Byte(AdventureLayout.TagRace).Text("test_human").Text("Human").Text("The usual sort.")
         .Text("").Text("");
        return a;
    }

    private static ArchiveWriter Skills()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagSkillList).Word(1);
        a.Byte(AdventureLayout.TagSkill).Text("test_blade").Text("Blade").Text("Hitting things.")
         .Text("").Byte(1).Byte(0).Text("");
        return a;
    }

    private static ArchiveWriter Attributes()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagAttributeList).Word(2);
        a.Byte(AdventureLayout.TagAttribute).Text("test_str").Text("Strength").Text("Str");
        a.Byte(AdventureLayout.TagAttribute).Text("test_dex").Text("Dexterity").Text("Dex");
        return a;
    }

    /// <summary>
    /// The map list: two resource words, a one-byte count, then one entry per map.
    ///
    /// Three maps: cell 1,1 with the outdoor flag, cell 2,1 without a placement record, and an
    /// interior whose id ends in four digits but does not carry the grid prefix.
    /// </summary>
    private static ArchiveWriter MapList()
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagMapList).Word(0).Word(0).Byte(3);
        Map(a, FirstMapRecord, "Testfield", GridPrefix + "0101", 0x0090);
        Map(a, FirstMapRecord + MapStride, "Testmoor", GridPrefix + "0201", 0x0480);
        Map(a, FirstMapRecord + MapStride * 2, "Test House", "test_house0102", 0x0000);
        return a;
    }

    private static void Map(ArchiveWriter a, int recordId, string name, string mapId, int flags)
    {
        a.Word(recordId).Byte(AdventureLayout.TagMap).Text(name).Text(mapId).Word(flags)
         .Word(0).Byte(100)
         .Word(0).Byte(100)
         .Word(0).Word(0)
         .Byte(0xFF)
         .Byte(0).Byte(0).Byte(21).Byte(21)
         .Word(0).Word(0).Word(0).Word(0);
    }

    /// <summary>
    /// A map's placement record. Only the ids are read — the entry layout was never worked out — so
    /// the fixture writes ids separated by the same kind of binary the real records carry.
    /// </summary>
    private static ArchiveWriter Placement(params string[] ids)
    {
        var a = new ArchiveWriter().Byte(AdventureLayout.TagMapObjectPlacement)
                                   .Byte(ids.Length).Word(0).Dword(0);
        foreach (string id in ids) a.Text(id).Raw(2, 0, 6, 3, 7, 0, 0, 0);
        return a;
    }
}
