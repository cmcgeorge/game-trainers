using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.FormatCheck;

public static class Program
{
    private static int _failures;
    private static int _checks;

    public static int Main()
    {
        Console.WriteLine("BardsTaleTrilogyTrainer FormatCheck");
        Console.WriteLine("====================================");

        CheckGameFacts();
        CheckCharacterFormat();
        CheckSpellbook();
        CheckItemBook();
        CheckFakeMemorySource();
        CheckCharacterRecordRoundTrip();
        CheckGameLocatorStructuralScan();

        Console.WriteLine($"\n{_checks} checks, {_failures} failures.");
        return _failures > 0 ? 1 : 0;
    }

    private static void Check(string label, bool ok)
    {
        _checks++;
        if (!ok) _failures++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} {label}");
    }

    private static void CheckGameFacts()
    {
        Console.WriteLine("\n--- GameFacts ---");
        Check("ProcessName is TheBardsTaleTrilogy", GameFacts.ProcessName == "TheBardsTaleTrilogy");
        Check("GameModuleName is GameAssembly.dll", GameFacts.GameModuleName == "GameAssembly.dll");
        Check("PartySlots is 7", GameFacts.PartySlots == 7);
        Check("GlobalPointerRva is 0xE40338", GameFacts.GlobalPointerRva == 0xE40338);
        Check("GameStatePartyOffset is 0xB8", GameFacts.GameStatePartyOffset == 0xB8);
        Check("PartyGoldOffset is 0x68", GameFacts.PartyGoldOffset == 0x68);
        Check("MaxLevel is 99", GameFacts.MaxLevel == 99);
        Check("MaxAttribute is 100", GameFacts.MaxAttribute == 100);
    }

    private static void CheckCharacterFormat()
    {
        Console.WriteLine("\n--- CharacterFormat ---");
        Check("ObjectHeaderSize is 0x10", CharacterFormat.ObjectHeaderSize == 0x10);
        Check("OffExperience is 0x50 [Confirmed]", CharacterFormat.OffExperience == 0x50);
        Check("OffHpCur is 0x84 [Confirmed]", CharacterFormat.OffHpCur == 0x84);
        Check("OffSpCur is 0x8C [Confirmed]", CharacterFormat.OffSpCur == 0x8C);
        Check("InventorySlots is 8", CharacterFormat.InventorySlots == 8);
        Check("Classes has 10 entries", CharacterFormat.Classes.Length == 10);
        Check("Races has 7 entries", CharacterFormat.Races.Length == 7);
        Check("ClassName(0) is Warrior", CharacterFormat.ClassName(0) == "Warrior");
        Check("ClassName(9) is Wizard", CharacterFormat.ClassName(9) == "Wizard");
        Check("RaceName(0) is Human", CharacterFormat.RaceName(0) == "Human");
        Check("RaceName(6) is Gnome", CharacterFormat.RaceName(6) == "Gnome");

        // LooksLikeCharacter with valid data
        var buf = new byte[0x100];
        WriteI32(buf, CharacterFormat.OffExperience, 50000);
        WriteI32(buf, CharacterFormat.OffHpCur, 100);
        WriteI32(buf, CharacterFormat.OffHpMax, 100);
        WriteI32(buf, CharacterFormat.OffSpCur, 50);
        WriteI32(buf, CharacterFormat.OffSpMax, 50);
        WriteI32(buf, CharacterFormat.OffRace, 0);
        WriteI32(buf, CharacterFormat.OffClass, 1);
        WriteI32(buf, CharacterFormat.OffLevel, 5);
        for (int i = 0; i < 5; i++)
        {
            WriteI32(buf, CharacterFormat.OffStrCur + i * 4, 18);
            WriteI32(buf, CharacterFormat.OffStrMax + i * 4, 18);
        }
        Check("LooksLikeCharacter accepts valid data", CharacterFormat.LooksLikeCharacter(buf));

        // Reject bad XP
        var badBuf = (byte[])buf.Clone();
        WriteI32(badBuf, CharacterFormat.OffExperience, -1);
        Check("LooksLikeCharacter rejects negative XP", !CharacterFormat.LooksLikeCharacter(badBuf));

        // Reject bad HP
        badBuf = (byte[])buf.Clone();
        WriteI32(badBuf, CharacterFormat.OffHpCur, 99999);
        Check("LooksLikeCharacter rejects implausible HP", !CharacterFormat.LooksLikeCharacter(badBuf));

        // Reject bad race
        badBuf = (byte[])buf.Clone();
        WriteI32(badBuf, CharacterFormat.OffRace, 99);
        Check("LooksLikeCharacter rejects invalid race", !CharacterFormat.LooksLikeCharacter(badBuf));

        // Reject HP > HPMax
        badBuf = (byte[])buf.Clone();
        WriteI32(badBuf, CharacterFormat.OffHpCur, 200);
        WriteI32(badBuf, CharacterFormat.OffHpMax, 100);
        Check("LooksLikeCharacter rejects HP > HPMax", !CharacterFormat.LooksLikeCharacter(badBuf));

        // Reject SP > SPMax
        badBuf = (byte[])buf.Clone();
        WriteI32(badBuf, CharacterFormat.OffSpCur, 200);
        WriteI32(badBuf, CharacterFormat.OffSpMax, 50);
        Check("LooksLikeCharacter rejects SP > SPMax", !CharacterFormat.LooksLikeCharacter(badBuf));
    }

    private static void CheckSpellbook()
    {
        Console.WriteLine("\n--- Spellbook ---");
        Check("Spellbook has spells", Spellbook.All.Count > 100);

        var zzgo = Spellbook.FindByCode("ZZGO");
        Check("ZZGO found", zzgo != null);
        Check("ZZGO is Dream Spell", zzgo?.Name == "Dream Spell");
        Check("ZZGO is AnyMagicUser class", zzgo?.Class == SpellClass.AnyMagicUser);

        var nuke = Spellbook.FindByCode("NUKE");
        Check("NUKE found", nuke != null);
        Check("NUKE is Gotterdammerung", nuke?.Name == "Gotterdammerung");
        Check("NUKE is AnyMagicUser class", nuke?.Class == SpellClass.AnyMagicUser);

        var arfi = Spellbook.FindByCode("ARFI");
        Check("ARFI found", arfi != null);
        Check("ARFI is Arc Fire", arfi?.Name == "Arc Fire");
        Check("ARFI is Conjurer level 1", arfi?.Class == SpellClass.Conjurer && arfi?.Level == 1);

        var grsu = Spellbook.FindByCode("GRSU");
        Check("GRSU found", grsu != null);
        Check("GRSU is Greater Summoning", grsu?.Name == "Greater Summoning");
        Check("GRSU is Wizard level 7", grsu?.Class == SpellClass.Wizard && grsu?.Level == 7);

        // Case insensitive
        Check("FindByCode is case-insensitive (zzgo)", Spellbook.FindByCode("zzgo")?.Code == "ZZGO");
        Check("FindByCode returns null for unknown", Spellbook.FindByCode("XXXX") == null);

        // Conjurer spells count (22 in BT1)
        var conjurer = Spellbook.For(SpellClass.Conjurer).ToList();
        Check("Conjurer has 22 spells", conjurer.Count == 22);

        // Wizard spells count (13 in BT1)
        var wizard = Spellbook.For(SpellClass.Wizard).ToList();
        Check("Wizard has 13 spells", wizard.Count == 13);

        // AnyMagicUser spells (4: GILL, DIVA, ZZGO, NUKE)
        var any = Spellbook.For(SpellClass.AnyMagicUser).ToList();
        Check("AnyMagicUser has 4 spells", any.Count == 4);

        // ArtForClass mapping
        Check("ArtForClass(6) is Conjurer", Spellbook.ArtForClass(6) == SpellClass.Conjurer);
        Check("ArtForClass(7) is Magician", Spellbook.ArtForClass(7) == SpellClass.Magician);
        Check("ArtForClass(8) is Sorcerer", Spellbook.ArtForClass(8) == SpellClass.Sorcerer);
        Check("ArtForClass(9) is Wizard", Spellbook.ArtForClass(9) == SpellClass.Wizard);
        Check("ArtForClass(0) is None", Spellbook.ArtForClass(0) == SpellClass.None);
    }

    private static void CheckItemBook()
    {
        Console.WriteLine("\n--- ItemBook ---");
        Check("ItemNames has 127 entries", ItemBook.ItemNames.Length == 127);
        Check("MaxItemId is 127", ItemBook.MaxItemId == 127);
        Check("ItemName(1) is Torch", ItemBook.ItemName(1) == "Torch");
        Check("ItemName(0) is (empty)", ItemBook.ItemName(0) == "(empty)");
        Check("ItemName(127) is Spectre Snare", ItemBook.ItemName(127) == "Spectre Snare");
        Check("Choices has 128 entries (0 + 127)", ItemBook.Choices.Count == 128);

        // Garth's shop basic items
        Check("GarthShopBasicItems has 22 entries", ItemBook.GarthShopBasicItems.Length == 22);
        Check("GarthShopBasicItems[0] is Torch (1)", ItemBook.GarthShopBasicItems[0] == 1);
        Check("GarthShopBasicItems contains Broadsword (3)", ItemBook.GarthShopBasicItems.Contains(3));
        Check("GarthShopBasicItems contains Plate Armor (15)", ItemBook.GarthShopBasicItems.Contains(15));

        // AllItemIds
        Check("AllItemIds has 127 entries", ItemBook.AllItemIds.Length == 127);
        Check("AllItemIds[0] is 1", ItemBook.AllItemIds[0] == 1);
        Check("AllItemIds[126] is 127", ItemBook.AllItemIds[126] == 127);

        // Categories
        Check("CategoryOf(3) is Weapons", ItemBook.CategoryOf(3) == "Weapons");
        Check("CategoryOf(12) is Armor", ItemBook.CategoryOf(12) == "Armor");
        Check("CategoryOf(17) is Helmets", ItemBook.CategoryOf(17) == "Helmets");
    }

    private static void CheckFakeMemorySource()
    {
        Console.WriteLine("\n--- FakeMemorySource ---");
        var fake = new FakeMemorySource();
        var data = new byte[0x200];
        fake.Map(0x10000, data);

        // Write and read back
        var writeBuf = new byte[] { 0x42, 0x00, 0x00, 0x00 };
        fake.Write(0x10050, writeBuf);
        var readBuf = new byte[4];
        fake.Read(0x10050, readBuf, 4);
        Check("FakeMemorySource write/read round-trip", readBuf[0] == 0x42);

        // Enumerate regions
        var regions = fake.EnumerateRegions().ToList();
        Check("FakeMemorySource has 1 region", regions.Count == 1);
        Check("FakeMemorySource region base is 0x10000", regions[0].Base == 0x10000);
    }

    private static void CheckCharacterRecordRoundTrip()
    {
        Console.WriteLine("\n--- CharacterRecord round-trip ---");
        var fake = new FakeMemorySource();
        var charData = new byte[0x200];

        // Set up a valid character
        WriteI32(charData, CharacterFormat.OffExperience, 75000);
        WriteI32(charData, CharacterFormat.OffHpCur, 150);
        WriteI32(charData, CharacterFormat.OffHpMax, 150);
        WriteI32(charData, CharacterFormat.OffSpCur, 80);
        WriteI32(charData, CharacterFormat.OffSpMax, 80);
        WriteI32(charData, CharacterFormat.OffRace, 1); // Elf
        WriteI32(charData, CharacterFormat.OffClass, 6); // Conjurer
        WriteI32(charData, CharacterFormat.OffLevel, 12);
        WriteI32(charData, CharacterFormat.OffArmorClass, -3);
        for (int i = 0; i < 5; i++)
        {
            WriteI32(charData, CharacterFormat.OffStrCur + i * 4, 20);
            WriteI32(charData, CharacterFormat.OffStrMax + i * 4, 20);
        }
        charData[CharacterFormat.OffConjurerLevel] = 7;
        charData[CharacterFormat.OffMagicianLevel] = 0;
        charData[CharacterFormat.OffSorcererLevel] = 0;
        charData[CharacterFormat.OffWizardLevel] = 0;

        nuint charAddr = 0x20000;
        fake.Map(charAddr, charData);

        var record = new CharacterRecord(fake, charAddr, 1);

        Check("Record Experience round-trip", record.Experience == 75000);
        Check("Record HpCur round-trip", record.HpCur == 150);
        Check("Record SpCur round-trip", record.SpCur == 80);
        Check("Record Race is 1 (Elf)", record.Race == 1);
        Check("Record Class is 6 (Conjurer)", record.Class == 6);
        Check("Record Level is 12", record.Level == 12);
        Check("Record ArmorClass is -3", record.ArmorClass == -3);
        Check("Record StrCur is 20", record.GetStatCur(0) == 20);
        Check("Record ConjurerLevel is 7", record.GetSpellLevel(0) == 7);

        // Write and verify
        record.Experience = 99999;
        Check("Record Experience write", record.Experience == 99999);

        record.HpCur = 200;
        Check("Record HpCur write", record.HpCur == 200);

        // Learn all spells
        record.LearnAllClassSpells();
        Check("LearnAllClassSpells sets Conjurer to 7", record.GetSpellLevel(0) == 7);
        Check("LearnAllClassSpells sets Magician to 7", record.GetSpellLevel(1) == 7);
        Check("LearnAllClassSpells sets Sorcerer to 7", record.GetSpellLevel(2) == 7);
        Check("LearnAllClassSpells sets Wizard to 7", record.GetSpellLevel(3) == 7);

        // IsOccupied
        Check("Record IsOccupied is true", record.IsOccupied);
    }

    private static void CheckGameLocatorStructuralScan()
    {
        Console.WriteLine("\n--- GameLocator structural scan ---");
        var fake = new FakeMemorySource();

        // Create a region with a valid character object
        var region = new byte[0x10000];
        int charOffset = 0x1000;

        // Fill with a valid character
        WriteI32(region, charOffset + CharacterFormat.OffExperience, 100000);
        WriteI32(region, charOffset + CharacterFormat.OffHpCur, 250);
        WriteI32(region, charOffset + CharacterFormat.OffHpMax, 250);
        WriteI32(region, charOffset + CharacterFormat.OffSpCur, 120);
        WriteI32(region, charOffset + CharacterFormat.OffSpMax, 120);
        WriteI32(region, charOffset + CharacterFormat.OffRace, 0);
        WriteI32(region, charOffset + CharacterFormat.OffClass, 9); // Wizard
        WriteI32(region, charOffset + CharacterFormat.OffLevel, 20);
        for (int i = 0; i < 5; i++)
        {
            WriteI32(region, charOffset + CharacterFormat.OffStrCur + i * 4, 18);
            WriteI32(region, charOffset + CharacterFormat.OffStrMax + i * 4, 18);
        }

        fake.Map(0x100000, region);

        var location = GameLocator.Locate(fake, 0);
        Check("Structural scan finds character", location != null);
        Check("Structural scan reports fallback", location?.UsedFallback == true);
        Check("Structural scan finds at least 1 character", location?.CharacterCount >= 1);

        // Empty memory should not find anything
        var empty = new FakeMemorySource();
        var emptyRegion = new byte[0x10000];
        empty.Map(0x200000, emptyRegion);
        var emptyLocation = GameLocator.Locate(empty, 0);
        Check("Structural scan finds nothing in empty memory", emptyLocation == null);
    }

    private static void WriteI32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
