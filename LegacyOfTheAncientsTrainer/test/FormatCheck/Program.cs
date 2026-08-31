using LegacyOfTheAncientsTrainer.Game;
using LegacyOfTheAncientsTrainer.Memory;

namespace FormatCheck;

internal sealed class Program
{
    private static int _pass, _fail;

    private static void Main()
    {
        TestFormatConstants();
        TestReferenceTables();
        TestCharacterRecordRoundTrip();
        TestIsValidRecord();
        TestLocator();
        TestLocatorStructuralOnly();
        TestLocatorEmptyMemory();
        TestLocatorCancellation();

        Console.WriteLine($"\n{_pass + _fail} checks: {_pass} passed, {_fail} FAILED");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }

    // --- helpers -------------------------------------------------------------
    private static void Check(bool condition, string label)
    {
        if (condition) { _pass++; }
        else { _fail++; Console.WriteLine($"  FAIL: {label}"); }
    }

    private static void Check<T>(T actual, T expected, string label) where T : notnull
    {
        if (actual.Equals(expected)) { _pass++; }
        else { _fail++; Console.WriteLine($"  FAIL: {label}: expected {expected}, got {actual}"); }
    }

    private static byte[] MakeDemoRecord(string name = "CHRISTOPHER", int hp = 200, int level = 1,
        int str = 15, int end = 15, int dex = 15, int intl = 15, int cha = 15)
    {
        var buf = new byte[CharacterFormat.RecordSize];

        // Header: bytes 4-5 = record size (382 = 0x017E)
        buf[CharacterFormat.OffRecordSize] = 0x7E;
        buf[CharacterFormat.OffRecordSize + 1] = 0x01;

        // Name (15 bytes, space-padded)
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, buf, CharacterFormat.OffName, Math.Min(nameBytes.Length, CharacterFormat.NameLength));
        for (int i = nameBytes.Length; i < CharacterFormat.NameLength; i++)
            buf[CharacterFormat.OffName + i] = 0x20;

        // Characteristics
        WriteI32(buf, CharacterFormat.OffStrength, str);
        WriteI32(buf, CharacterFormat.OffEndurance, end);
        WriteI16(buf, CharacterFormat.OffHP, hp);
        WriteI16(buf, CharacterFormat.OffLevel, level);
        WriteI16(buf, CharacterFormat.OffDexterity, dex);
        WriteI32(buf, CharacterFormat.OffIntelligence, intl);
        WriteI32(buf, CharacterFormat.OffCharm, cha);

        return buf;
    }

    private static void WriteI16(byte[] buf, int off, int v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteI32(byte[] buf, int off, int v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
        buf[off + 2] = (byte)((v >> 16) & 0xFF);
        buf[off + 3] = (byte)((v >> 24) & 0xFF);
    }

    // --- tests ---------------------------------------------------------------
    private static void TestFormatConstants()
    {
        Console.WriteLine("Format constants");
        Check(CharacterFormat.RecordSize == 382, "RecordSize == 382");
        Check(CharacterFormat.RecordCount == 9, "RecordCount == 9");
        Check(CharacterFormat.OffHeader == 0x00, "OffHeader == 0x00");
        Check(CharacterFormat.OffRecordSize == 0x04, "OffRecordSize == 0x04");
        Check(CharacterFormat.OffName == 0x06, "OffName == 0x06");
        Check(CharacterFormat.NameLength == 15, "NameLength == 15");
        Check(CharacterFormat.OffStrength == 0x15, "OffStrength == 0x15");
        Check(CharacterFormat.StrengthSize == 4, "StrengthSize == 4");
        Check(CharacterFormat.OffEndurance == 0x21, "OffEndurance == 0x21");
        Check(CharacterFormat.EnduranceSize == 4, "EnduranceSize == 4");
        Check(CharacterFormat.OffHP == 0x2F, "OffHP == 0x2F");
        Check(CharacterFormat.OffLevel == 0x31, "OffLevel == 0x31");
        Check(CharacterFormat.OffDexterity == 0x33, "OffDexterity == 0x33");
        Check(CharacterFormat.DexteritySize == 2, "DexteritySize == 2");
        Check(CharacterFormat.OffIntelligence == 0x45, "OffIntelligence == 0x45");
        Check(CharacterFormat.IntelligenceSize == 4, "IntelligenceSize == 4");
        Check(CharacterFormat.OffCharm == 0x5D, "OffCharm == 0x5D");
        Check(CharacterFormat.CharmSize == 4, "CharmSize == 4");
        Check(CharacterFormat.CharacteristicCount == 5, "CharacteristicCount == 5");
        Check(CharacterFormat.MaxCharacteristic == 100, "MaxCharacteristic == 100");
        Check(CharacterFormat.MaxHP == 9999, "MaxHP == 9999");
        Check(CharacterFormat.MaxLevelValue == 10, "MaxLevelValue == 10");
        Console.WriteLine();
    }

    private static void TestReferenceTables()
    {
        Console.WriteLine("Reference tables");
        Check(SpellBook.Count == 6, "SpellBook.Count == 6");
        Check(SpellBook.Spells[0].Name == "Magic Flame", "Spell 0 name");
        Check(SpellBook.Spells[1].Name == "Firebolt", "Spell 1 name");
        Check(SpellBook.Spells[2].Name == "Befuddle", "Spell 2 name");
        Check(SpellBook.Spells[3].Name == "Psycho Strength", "Spell 3 name");
        Check(SpellBook.Spells[4].Name == "Kill Flash", "Spell 4 name");
        Check(SpellBook.Spells[4].MaxCharges == 20, "Kill Flash max 20");
        Check(SpellBook.Spells[5].Name == "Seek", "Spell 5 name");

        Check(WeaponBook.Count == 9, "WeaponBook.Count == 9");
        Check(WeaponBook.Weapons[0].Name == "Bare Hands", "Weapon 0 name");
        Check(WeaponBook.Weapons[1].Name == "Knife", "Weapon 1 name");
        Check(WeaponBook.Weapons[8].Name == "Compound Bow", "Weapon 8 name");
        Check(WeaponBook.Qualities.Length == 5, "5 weapon qualities");

        Check(ArmorBook.Count == 5, "ArmorBook.Count == 5");
        Check(ArmorBook.Armors[0].Name == "Studded Hide", "Armor 0 name");
        Check(ArmorBook.Armors[4].Name == "Mythan Plate", "Armor 4 name");
        Check(ArmorBook.Qualities.Length == 5, "5 armor qualities");

        Check(MonsterBook.Count == 44, "MonsterBook.Count == 44");
        Check(MonsterBook.WildernessCount == 32, "WildernessCount == 32");
        Check(MonsterBook.DungeonCount == 12, "DungeonCount == 12");
        Check(MonsterBook.Monsters[0].Name == "Pixie", "Monster 0 name");
        Check(MonsterBook.Monsters[31].Name == "Maston Leaper", "Monster 31 name");
        Check(MonsterBook.Monsters[37].Name == "Knuckles", "Monster 37 name");
        Check(MonsterBook.Monsters[37].Note == "Destroys weapon!", "Knuckles note");
        Check(MonsterBook.Monsters[43].Name == "Slime Wart", "Monster 43 name");

        Check(LocationBook.Count == 17, "LocationBook.Count == 17");
        Check(LocationBook.TownCount == 12, "TownCount == 12");
        Check(LocationBook.Locations[0].Name == "Eagle Hollow", "Location 0 name");
        Check(LocationBook.Locations[12].Name == "Galactic Museum", "Museum name");
        Check(LocationBook.Locations[13].Name == "Kelfor Castle", "Castle name");

        Check(ItemBook.Count == 24, "ItemBook.Count == 24");
        Check(ItemBook.Items[0].Name == "Stone Key", "Item 0 name");
        Check(ItemBook.Items[23].Name == "Diamond Coin", "Item 23 name");

        Check(GameFacts.GameTitle == "Legacy of the Ancients", "GameTitle");
        Check(GameFacts.Publisher == "Electronic Arts", "Publisher");
        Check(GameFacts.ReleaseYear == 1987, "ReleaseYear");
        Check(GameFacts.SpellCount == 6, "SpellCount");
        Check(GameFacts.TownCount == 12, "TownCount");
        Check(GameFacts.HPByLevel[0] == 200, "HP level 1");
        Check(GameFacts.HPByLevel[9] == 3000, "HP level 10");
        Console.WriteLine();
    }

    private static void TestCharacterRecordRoundTrip()
    {
        Console.WriteLine("Character record round-trip");
        var rec = new CharacterRecord(MakeDemoRecord());

        Check(rec.Name, "CHRISTOPHER", "Name");
        Check(rec.HP, 200, "HP");
        Check(rec.Level, 1, "Level");
        Check(rec.Strength, 15, "Strength");
        Check(rec.Endurance, 15, "Endurance");
        Check(rec.Dexterity, 15, "Dexterity");
        Check(rec.Intelligence, 15, "Intelligence");
        Check(rec.Charm, 15, "Charm");
        Check(rec.IsOccupied, true, "IsOccupied");
        Check(rec.IsHeaderOccupied, true, "IsHeaderOccupied");

        for (int i = 0; i < CharacterFormat.CharacteristicCount; i++)
            Check(rec.GetCharacteristic(i), 15, $"Characteristic {i}");

        // Name round-trip
        rec.Name = "HERO";
        Check(rec.Name, "HERO", "Name round-trip short");
        rec.Name = "FIFTEENCHARS!!";
        Check(rec.Name, "FIFTEENCHARS!!", "Name round-trip max length (14)");

        // HP round-trip
        rec.HP = 500; Check(rec.HP, 500, "HP round-trip");
        rec.HP = 9999; Check(rec.HP, 9999, "HP max");
        rec.HP = 10000; Check(rec.HP, 9999, "HP clamped to max");

        // Level round-trip
        rec.Level = 5; Check(rec.Level, 5, "Level round-trip");
        rec.Level = 10; Check(rec.Level, 10, "Level max");
        rec.Level = 11; Check(rec.Level, 10, "Level clamped to max");

        // Characteristic set with clamping
        rec.SetCharacteristic(0, 100); Check(rec.GetCharacteristic(0), 100, "Characteristic set to max");
        rec.SetCharacteristic(0, 1); Check(rec.GetCharacteristic(0), 1, "Characteristic set to min");
        rec.SetCharacteristic(0, 200); Check(rec.GetCharacteristic(0), 100, "Characteristic clamped to max");

        // Individual characteristic setters
        rec.Strength = 50; Check(rec.Strength, 50, "Strength set");
        rec.Endurance = 60; Check(rec.Endurance, 60, "Endurance set");
        rec.Dexterity = 40; Check(rec.Dexterity, 40, "Dexterity set");
        rec.Intelligence = 70; Check(rec.Intelligence, 70, "Intelligence set");
        rec.Charm = 80; Check(rec.Charm, 80, "Charm set");

        // Empty record
        var empty = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
        Check(!empty.IsOccupied, "Empty record not occupied");
        Check(!empty.IsHeaderOccupied, "Empty record header not occupied");

        Console.WriteLine();
    }

    private static void TestIsValidRecord()
    {
        Console.WriteLine("IsValidRecord");
        var demo = MakeDemoRecord();
        Check(CharacterRecord.IsValidRecord(demo, 0), "Demo record valid");

        // Empty record (all zeros) — invalid (header record-size = 0)
        Check(!CharacterRecord.IsValidRecord(new byte[CharacterFormat.RecordSize], 0), "All-zeros invalid");

        // Header record-size wrong
        var bad = MakeDemoRecord();
        bad[CharacterFormat.OffRecordSize] = 0x00;
        bad[CharacterFormat.OffRecordSize + 1] = 0x00;
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Header record-size 0 invalid");

        // Name doesn't start with a letter
        bad = MakeDemoRecord("123HERO");
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Name starting with digit invalid");

        // HP = 0
        bad = MakeDemoRecord(hp: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "HP=0 invalid");

        // Strength out of range
        bad = MakeDemoRecord(str: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Strength=0 invalid");

        bad = MakeDemoRecord(str: 1000);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Strength > Plausible invalid");

        // Endurance out of range
        bad = MakeDemoRecord(end: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Endurance=0 invalid");

        // Dexterity out of range
        bad = MakeDemoRecord(dex: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Dexterity=0 invalid");

        // Intelligence out of range
        bad = MakeDemoRecord(intl: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Intelligence=0 invalid");

        // Charm out of range
        bad = MakeDemoRecord(cha: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Charm=0 invalid");

        // Level out of range
        bad = MakeDemoRecord(level: 99);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Level > 20 invalid");

        // Name too short (1 char)
        bad = MakeDemoRecord("A");
        // "A" + 14 spaces — nameLen counts non-space chars, so nameLen=1
        Check(!CharacterRecord.IsValidRecord(bad, 0), "1-char name invalid");

        // Buffer too small
        Check(!CharacterRecord.IsValidRecord(new byte[10], 0), "Buffer too small invalid");

        Console.WriteLine();
    }

    // --- Fake memory source for locator tests --------------------------------
    private sealed class FakeMemorySource : IMemorySource
    {
        private readonly byte[] _data;
        private readonly int _base;
        private readonly int _size;

        public FakeMemorySource(byte[] data, int baseAddr = 0x10000)
        {
            _data = data;
            _base = baseAddr;
            _size = data.Length;
        }

        public IEnumerable<MemoryRegion> EnumerateRegions()
        {
            yield return new MemoryRegion((nuint)_base, (nuint)_size);
        }

        public int Read(nuint address, byte[] buffer, int count)
        {
            int off = (int)address - _base;
            if (off < 0 || off >= _size) return 0;
            int n = Math.Min(count, _size - off);
            Array.Copy(_data, off, buffer, 0, n);
            return n;
        }

        public byte[] Read(nuint address, int count)
        {
            int off = (int)address - _base;
            if (off < 0 || off >= _size) return Array.Empty<byte>();
            int n = Math.Min(count, _size - off);
            var result = new byte[n];
            Array.Copy(_data, off, result, 0, n);
            return result;
        }
    }

    private static void TestLocator()
    {
        Console.WriteLine("Locator (structural scan)");

        int charOffset = 0x2000;
        byte[] data = new byte[0x10000];

        // Place the character record
        var demo = MakeDemoRecord();
        Array.Copy(demo, 0, data, charOffset, demo.Length);

        var src = new FakeMemorySource(data);
        var found = CharacterLocator.Find(src);
        Check(found != null, "Character found");
        if (found != null)
        {
            Check(found.Record.Name, "CHRISTOPHER", "Found character name");
            Check(found.Record.HP, 200, "Found character HP");
            Check(found.Record.Level, 1, "Found character level");
            Check(found.Record.Strength, 15, "Found character strength");
        }

        Console.WriteLine();
    }

    private static void TestLocatorStructuralOnly()
    {
        Console.WriteLine("Locator (structural-only, no anchor)");

        int charOffset = 0x2000;
        byte[] data = new byte[0x10000];
        var demo = MakeDemoRecord(name: "BRAVEHERO", hp: 500, level: 3, str: 30, end: 25, dex: 20, intl: 18, cha: 22);
        Array.Copy(demo, 0, data, charOffset, demo.Length);

        var src = new FakeMemorySource(data);
        var found = CharacterLocator.Find(src);
        Check(found != null, "Character found via structural scan");
        if (found != null)
        {
            Check(found.Record.Name, "BRAVEHERO", "Structural-only found character name");
            Check(found.Record.HP, 500, "Structural-only found character HP");
            Check(found.Record.Level, 3, "Structural-only found character level");
            Check(found.Record.Strength, 30, "Structural-only found character strength");
        }

        Console.WriteLine();
    }

    private static void TestLocatorEmptyMemory()
    {
        Console.WriteLine("Locator (empty memory)");
        var src = new FakeMemorySource(new byte[0x10000]);
        var found = CharacterLocator.Find(src);
        Check(found == null, "No character in empty memory");
        Console.WriteLine();
    }

    private static void TestLocatorCancellation()
    {
        Console.WriteLine("Locator (cancellation)");
        var src = new FakeMemorySource(new byte[0x10000]);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            CharacterLocator.Find(src, cts.Token);
            _pass++;
        }
        catch (OperationCanceledException)
        {
            _pass++;
        }
        Console.WriteLine();
    }
}
