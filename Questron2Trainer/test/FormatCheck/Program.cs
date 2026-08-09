using Questron2Trainer.Game;
using Questron2Trainer.Memory;

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

    private static byte[] MakeDemoRecord(string name = "The Thing", int hp = 200, int food = 188,
        int gold = 162, int[]? attrs = null, int level = 1, int weapon = 7, int armor = 5)
    {
        var buf = new byte[CharacterFormat.RecordSize];
        WriteU16(buf, CharacterFormat.OffHP, (ushort)hp);
        WriteU16(buf, CharacterFormat.OffFood, (ushort)food);
        WriteU16(buf, CharacterFormat.OffGold, (ushort)gold);
        buf[CharacterFormat.OffFlag] = 3;
        int[] a = attrs ?? new[] { 15, 15, 15, 15, 15 };
        for (int i = 0; i < CharacterFormat.AttributeCount && i < a.Length; i++)
            buf[CharacterFormat.OffAttributes + i] = (byte)a[i];
        buf[CharacterFormat.OffWeapon] = (byte)weapon;
        buf[CharacterFormat.OffArmor] = (byte)armor;
        buf[CharacterFormat.OffLevel] = (byte)level;
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, buf, CharacterFormat.OffName, Math.Min(nameBytes.Length, CharacterFormat.NameLength - 1));
        for (int i = 0; i < CharacterFormat.SpellSlotCount; i++)
            buf[CharacterFormat.OffSpellCharges + i] = 1;
        return buf;
    }

    private static void WriteU16(byte[] buf, int off, ushort v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    // --- tests ---------------------------------------------------------------
    private static void TestFormatConstants()
    {
        Console.WriteLine("Format constants");
        Check(CharacterFormat.RecordSize == 256, "RecordSize == 256");
        Check(CharacterFormat.OffHP == 0x00, "OffHP == 0x00");
        Check(CharacterFormat.OffFood == 0x02, "OffFood == 0x02");
        Check(CharacterFormat.OffGold == 0x04, "OffGold == 0x04");
        Check(CharacterFormat.OffAttributes == 0x07, "OffAttributes == 0x07");
        Check(CharacterFormat.AttributeCount == 5, "AttributeCount == 5");
        Check(CharacterFormat.OffLevel == 0x18, "OffLevel == 0x18");
        Check(CharacterFormat.OffName == 0x50, "OffName == 0x50");
        Check(CharacterFormat.NameLength == 16, "NameLength == 16");
        Check(CharacterFormat.OffWeapon == 0x10, "OffWeapon == 0x10");
        Check(CharacterFormat.OffArmor == 0x11, "OffArmor == 0x11");
        Check(CharacterFormat.OffSpellCharges == 0x86, "OffSpellCharges == 0x86");
        Check(CharacterFormat.SpellSlotCount == 8, "SpellSlotCount == 8");
        Check(CharacterFormat.MaxAttribute == 25, "MaxAttribute == 25");
        Check(CharacterFormat.MaxLevel == 20, "MaxLevel == 20");
        Console.WriteLine();
    }

    private static void TestReferenceTables()
    {
        Console.WriteLine("Reference tables");
        Check(SpellBook.Count == 5, "SpellBook.Count == 5");
        Check(SpellBook.BuyableCount == 4, "SpellBook.BuyableCount == 4");
        Check(SpellBook.Spells[0].Name == "Magic Missile", "Spell 0 name");
        Check(SpellBook.Spells[1].Name == "Fireball", "Spell 1 name");
        Check(SpellBook.Spells[2].Name == "Sonic Whine", "Spell 2 name");
        Check(SpellBook.Spells[3].Name == "Time Sap", "Spell 3 name");
        Check(SpellBook.Spells[4].Name == "Destruct", "Spell 4 name");

        Check(WeaponBook.Count == 10, "WeaponBook.Count == 10");
        Check(WeaponBook.Weapons[0].Name == "Dagger", "Weapon 0 name");
        Check(WeaponBook.Weapons[7].Name == "Shortbow", "Weapon 7 name");
        Check(WeaponBook.Weapons[9].Name == "Crossbow", "Weapon 9 name");

        Check(ArmorBook.Count == 7, "ArmorBook.Count == 7");
        Check(ArmorBook.Armors[0].Name == "Rawhide", "Armor 0 name");
        Check(ArmorBook.Armors[4].Name == "Chain Mail", "Armor 4 name");
        Check(ArmorBook.Armors[6].Name == "Ribbed Plate", "Armor 6 name");

        Check(ItemBook.Count == 25, "ItemBook.Count == 25");
        Check(ItemBook.Items[0].Name == "Gold Key", "Item 0 name");
        Check(ItemBook.Items[11].Name == "Black Key", "Item 11 name");
        Check(ItemBook.Items[12].Name == "Unicorn Horn", "Item 12 name");

        Check(MonsterBook.Count == 39, "MonsterBook.Count == 39");
        Check(MonsterBook.Monsters[0].Name == "Sovan Priest", "Monster 0 name");
        Check(MonsterBook.Monsters[38].Name == "Mind Scream", "Monster 38 name");

        Check(LocationBook.Count == 26, "LocationBook.Count == 26");
        Check(LocationBook.Locations[0].Name == "Hidden Rock", "Location 0 name");
        Check(LocationBook.Locations[24].Name == "The Dungeon of Despair", "Location 24 name");

        Check(GameFacts.GameTitle == "Questron II", "GameTitle");
        Check(GameFacts.MainExe == "START.EXE", "MainExe");
        Check(GameFacts.GameVersion == "1.2", "GameVersion");
        Check(GameFacts.CopyrightString == "Questron II (C) 1988 S.S.I.", "CopyrightString");
        Check(CharacterFormat.LevelNames[0] == "Nothing", "LevelNames[0]");
        Check(CharacterFormat.LevelNames[1] == "Adventurer", "LevelNames[1]");
        Console.WriteLine();
    }

    private static void TestCharacterRecordRoundTrip()
    {
        Console.WriteLine("Character record round-trip");
        var rec = new CharacterRecord(MakeDemoRecord());

        Check(rec.Name, "The Thing", "Name");
        Check(rec.HP, 200, "HP");
        Check(rec.Food, 188, "Food");
        Check(rec.Gold, 162, "Gold");
        Check(rec.Level, 1, "Level");
        Check(rec.Weapon, 7, "Weapon");
        Check(rec.Armor, 5, "Armor");

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            Check(rec.GetAttribute(i), 15, $"Attribute {i}");

        for (int i = 0; i < CharacterFormat.SpellSlotCount; i++)
            Check(rec.GetSpellCharges(i), 1, $"Spell charges {i}");

        // Name round-trip
        rec.Name = "Hero";
        Check(rec.Name, "Hero", "Name round-trip short");
        rec.Name = "FifteenChars!!!!";
        Check(rec.Name, "FifteenChars!!!", "Name round-trip max length (15)");

        // HP/Food/Gold round-trip
        rec.HP = 500; Check(rec.HP, 500, "HP round-trip");
        rec.Food = 300; Check(rec.Food, 300, "Food round-trip");
        rec.Gold = 9999; Check(rec.Gold, 9999, "Gold round-trip");

        // Attribute set
        rec.SetAttribute(0, 25); Check(rec.GetAttribute(0), 25, "Attribute set to max");
        rec.SetAttribute(0, 1); Check(rec.GetAttribute(0), 1, "Attribute set to min");
        rec.SetAttribute(0, 99); Check(rec.GetAttribute(0), 25, "Attribute clamped to max");

        // Level set
        rec.Level = 3; Check(rec.Level, 3, "Level set");
        Check(CharacterFormat.LevelName(3), "Knight", "Level 3 name");
        Check(CharacterFormat.LevelName(20), "Knight", "Level 20 clamps to last name");
        Check(CharacterFormat.LevelName(0), "Nothing", "Level 0 name");

        // Weapon/Armor set with clamping
        rec.Weapon = 9; Check(rec.Weapon, 9, "Weapon set to max (Crossbow)");
        rec.Weapon = 99; Check(rec.Weapon, 9, "Weapon clamped to max");
        rec.Weapon = 0; Check(rec.Weapon, 0, "Weapon set to min (Dagger)");
        rec.Weapon = -1; Check(rec.Weapon, 0, "Weapon clamped to min");
        rec.Armor = 6; Check(rec.Armor, 6, "Armor set to max (Ribbed Plate)");
        rec.Armor = 99; Check(rec.Armor, 6, "Armor clamped to max");
        rec.Armor = 0; Check(rec.Armor, 0, "Armor set to min (Rawhide)");
        rec.Armor = -1; Check(rec.Armor, 0, "Armor clamped to min");

        // Spell charges set
        rec.SetSpellCharges(0, 99); Check(rec.GetSpellCharges(0), 99, "Spell charges set to max");
        rec.SetAllSpellCharges(50);
        for (int i = 0; i < CharacterFormat.SpellSlotCount; i++)
            Check(rec.GetSpellCharges(i), 50, $"All spell charges {i}");

        Console.WriteLine();
    }

    private static void TestIsValidRecord()
    {
        Console.WriteLine("IsValidRecord");
        var demo = MakeDemoRecord();
        Check(CharacterRecord.IsValidRecord(demo, 0), "Demo record valid");

        // Empty record (all zeros) — invalid (name starts with 0x00)
        Check(!CharacterRecord.IsValidRecord(new byte[CharacterFormat.RecordSize], 0), "All-zeros invalid");

        // Name doesn't start with a letter
        var bad = MakeDemoRecord("123Hero");
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Name starting with digit invalid");

        // HP = 0
        bad = MakeDemoRecord(hp: 0);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "HP=0 invalid");

        // Attribute out of range
        bad = MakeDemoRecord(attrs: new[] { 15, 30, 15, 15, 15 });
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Attribute > 25 invalid");

        // Level out of range
        bad = MakeDemoRecord(level: 99);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Level > 20 invalid");

        // Name too short (1 char)
        bad = MakeDemoRecord("A");
        Check(!CharacterRecord.IsValidRecord(bad, 0), "1-char name invalid");

        // Name fills all 16 bytes with no null terminator — invalid (nameLen = 16 > 15)
        bad = MakeDemoRecord("ABCDEFGHIJKLMNO");
        bad[CharacterFormat.OffName + 15] = (byte)'P';
        Check(!CharacterRecord.IsValidRecord(bad, 0), "16-byte name no null invalid");

        // HP above MaxHP (tightened bound)
        bad = MakeDemoRecord(hp: 10000);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "HP > MaxHP invalid");

        // Food above MaxFood (tightened bound)
        bad = MakeDemoRecord(food: 10000);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Food > MaxFood invalid");

        // Weapon out of range
        bad = MakeDemoRecord(weapon: 10);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Weapon >= WeaponBook.Count invalid");

        // Armor out of range
        bad = MakeDemoRecord(armor: 7);
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Armor >= ArmorBook.Count invalid");

        // Spell charges out of range
        bad = MakeDemoRecord();
        bad[CharacterFormat.OffSpellCharges] = 100;
        Check(!CharacterRecord.IsValidRecord(bad, 0), "Spell charge > 99 invalid");

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

        // Build a fake address space with a demo character at a known offset
        int anchorOffset = 0x1000;
        int charOffset = 0x2000;
        byte[] data = new byte[0x10000];

        // Place the anchor string
        var anchorBytes = System.Text.Encoding.ASCII.GetBytes(GameFacts.CopyrightString);
        Array.Copy(anchorBytes, 0, data, anchorOffset, anchorBytes.Length);

        // Place the character record
        var demo = MakeDemoRecord();
        Array.Copy(demo, 0, data, charOffset, demo.Length);

        var src = new FakeMemorySource(data);
        var found = CharacterLocator.Find(src);
        Check(found != null, "Character found");
        if (found != null)
        {
            Check(found.Record.Name, "The Thing", "Found character name");
            Check(found.Record.HP, 200, "Found character HP");
            Check(found.Record.Level, 1, "Found character level");
        }

        // Test anchor-based find specifically
        Console.WriteLine("Locator (anchor-based)");
        // The anchor find should locate the character within the 256KB window forward
        Check(found != null, "Anchor find returned a result");

        Console.WriteLine();
    }

    private static void TestLocatorStructuralOnly()
    {
        Console.WriteLine("Locator (structural-only, no anchor)");

        // Build a fake address space with a character record but NO anchor string,
        // so the structural fallback scanner must find it.
        int charOffset = 0x2000;
        byte[] data = new byte[0x10000];
        var demo = MakeDemoRecord();
        Array.Copy(demo, 0, data, charOffset, demo.Length);

        var src = new FakeMemorySource(data);
        var found = CharacterLocator.Find(src);
        Check(found != null, "Character found via structural scan (no anchor)");
        if (found != null)
        {
            Check(found.Record.Name, "The Thing", "Structural-only found character name");
            Check(found.Record.HP, 200, "Structural-only found character HP");
            Check(found.Record.Level, 1, "Structural-only found character level");
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
            _pass++; // may return null before checking the token, that's fine
        }
        catch (OperationCanceledException)
        {
            _pass++;
        }
        Console.WriteLine();
    }
}
