using FountainOfDreamsTrainer.Game;
using FountainOfDreamsTrainer.Memory;

namespace FormatCheck;

internal sealed class Program
{
    private static int _pass, _fail;

    private static void Main()
    {
        TestFormatConstants();
        TestAttributeBook();
        TestProfessionBook();
        TestSkillBook();
        TestItemBook();
        TestCharacterRecordRoundTrip();
        TestIsValidRecord();
        TestLocator();
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

    private static byte[] MakeRecord(string name, int[] attrs, int con, int maxCon,
        int level = 1, int profession = 0, int cash = 0, int rank = 6,
        long xp = 0, int nextLvlXp = 1500)
    {
        var buf = new byte[CharacterFormat.RecordSize];
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, buf, CharacterFormat.OffName, Math.Min(nameBytes.Length, CharacterFormat.NameFieldLength - 1));
        WriteU32(buf, CharacterFormat.OffCash, (uint)cash);
        for (int i = 0; i < 7 && i < attrs.Length; i++)
            buf[CharacterFormat.OffAttributes + i] = (byte)attrs[i];
        buf[CharacterFormat.OffProfession] = (byte)profession;
        buf[CharacterFormat.OffCon] = (byte)con;
        WriteU16(buf, CharacterFormat.OffMaxCon, (ushort)maxCon);
        buf[CharacterFormat.OffArmorClass] = 5;
        buf[CharacterFormat.OffEquipFlag] = 0xFF;
        buf[CharacterFormat.OffLevel] = (byte)level;
        WriteU16(buf, CharacterFormat.OffRank, (ushort)rank);
        WriteU32(buf, CharacterFormat.OffExperience, (uint)xp);
        WriteU16(buf, CharacterFormat.OffNextLevelXp, (ushort)nextLvlXp);
        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
            buf[CharacterFormat.OffInventory + i * CharacterFormat.InventorySlotSize] = (byte)CharacterFormat.InventoryEmpty;
        return buf;
    }

    private static void WriteU16(byte[] buf, int off, ushort v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteU32(byte[] buf, int off, uint v)
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
        Check(CharacterFormat.RecordSize == 332, "RecordSize == 332");
        Check(CharacterFormat.MaxSlots == 3, "MaxSlots == 3");
        Check(CharacterFormat.OffName == 0x00, "OffName == 0x00");
        Check(CharacterFormat.OffCash == 0x14, "OffCash == 0x14");
        Check(CharacterFormat.OffAttributes == 0x18, "OffAttributes == 0x18");
        Check(CharacterFormat.AttributeCount == 7, "AttributeCount == 7");
        Check(CharacterFormat.OffCon == 0x23, "OffCon == 0x23");
        Check(CharacterFormat.OffMaxCon == 0x46, "OffMaxCon == 0x46");
        Check(CharacterFormat.OffLevel == 0x50, "OffLevel == 0x50");
        Check(CharacterFormat.OffRank == 0x52, "OffRank == 0x52");
        Check(CharacterFormat.OffExperience == 0x54, "OffExperience == 0x54");
        Check(CharacterFormat.OffNextLevelXp == 0x5E, "OffNextLevelXp == 0x5E");
        Check(CharacterFormat.OffInventory == 0x80, "OffInventory == 0x80");
        Check(CharacterFormat.InventorySlots == 27, "InventorySlots == 27");
        Check(CharacterFormat.InventorySlotSize == 6, "InventorySlotSize == 6");
        Check(CharacterFormat.InventoryBytes == 162, "InventoryBytes == 162");
        Check(CharacterFormat.AttributeNames.Length == 7, "AttributeNames.Length == 7");
        Check(CharacterFormat.AttributeNames[0] == "ST", "AttributeNames[0] == ST");
        Check(CharacterFormat.AttributeNames[6] == "LK", "AttributeNames[6] == LK");
        Check(CharacterFormat.Professions.Length == 7, "Professions.Length == 7");
    }

    private static void TestAttributeBook()
    {
        Console.WriteLine("AttributeBook");
        Check(AttributeBook.Attributes.Count == 7, "Attributes.Count == 7");
        Check(AttributeBook.Attributes[0].Abbr == "ST", "Attributes[0].Abbr == ST");
        Check(AttributeBook.Attributes[1].Abbr == "IQ", "Attributes[1].Abbr == IQ");
        Check(AttributeBook.Attributes[6].Abbr == "LK", "Attributes[6].Abbr == LK");
        Check(!string.IsNullOrEmpty(AttributeBook.DescriptionOf(0)), "DescriptionOf(0) not empty");
        Check(AttributeBook.DescriptionOf(99) == "", "DescriptionOf(99) empty");
    }

    private static void TestProfessionBook()
    {
        Console.WriteLine("ProfessionBook");
        Check(ProfessionBook.Professions.Count == 7, "Professions.Count == 7");
        Check(ProfessionBook.Professions[0].Name == "Survivalist", "Professions[0].Name == Survivalist");
        Check(ProfessionBook.Professions[1].Name == "Vigilante", "Professions[1].Name == Vigilante");
        Check(ProfessionBook.Professions[4].Name == "Mechanic", "Professions[4].Name == Mechanic");
        Check(ProfessionBook.Playable.Count == 5, "Playable.Count == 5");
        Check(ProfessionBook.Name(0) == "Survivalist", "Name(0) == Survivalist");
        Check(ProfessionBook.Find(0)!.ConMin == 20, "Survivalist ConMin == 20");
        Check(ProfessionBook.Find(0)!.ConMax == 25, "Survivalist ConMax == 25");
        Check(ProfessionBook.Find(2)!.ConMin == 15, "Medic ConMin == 15");
    }

    private static void TestSkillBook()
    {
        Console.WriteLine("SkillBook");
        Check(SkillBook.Skills.Count > 0, "Skills.Count > 0");
        Check(SkillBook.Find(1)!.Name == "Brawling", "Skill 1 == Brawling");
        Check(SkillBook.SkillName(1) == "Brawling", "SkillName(1) == Brawling");
        Check(SkillBook.Find(999) == null, "Find(999) == null");
    }

    private static void TestItemBook()
    {
        Console.WriteLine("ItemBook");
        Check(ItemBook.Items.Count > 0, "Items.Count > 0");
        Check(ItemBook.Find(CharacterFormat.InventoryEmpty)!.Name == "(empty)", "Empty item name");
        Check(ItemBook.Find(1)!.Name == "Knife", "Item 1 == Knife");
        Check(ItemBook.ItemName(999) == "Item #999", "Unknown item name");
    }

    private static void TestCharacterRecordRoundTrip()
    {
        Console.WriteLine("CharacterRecord round-trip");
        var buf = MakeRecord("TestChar", new[] { 15, 16, 14, 12, 13, 11, 17 }, 20, 25,
            level: 5, profession: 1, cash: 1234, rank: 10, xp: 5000, nextLvlXp: 6000);
        var rec = new CharacterRecord(buf);

        Check(rec.Name == "TestChar", "Name round-trip");
        Check(rec.GetAttribute(0) == 15, "ST round-trip");
        Check(rec.GetAttribute(6) == 17, "LK round-trip");
        Check(rec.Cash == 1234, "Cash round-trip");
        Check(rec.Con == 20, "Con round-trip");
        Check(rec.MaxCon == 25, "MaxCon round-trip");
        Check(rec.Level == 5, "Level round-trip");
        Check(rec.Profession == 1, "Profession round-trip");
        Check(rec.Rank == 10, "Rank round-trip");
        Check(rec.Experience == 5000, "Experience round-trip");
        Check(rec.NextLevelXp == 6000, "NextLevelXp round-trip");

        // Write back
        rec.Cash = 99999;
        Check(rec.Cash == 99999, "Cash write");
        rec.SetAttribute(0, 20);
        Check(rec.GetAttribute(0) == 20, "ST write");
        rec.Con = 25;
        Check(rec.Con == 25, "Con write");
        rec.Level = 99;
        Check(rec.Level == 99, "Level write");

        // Name change
        rec.Name = "Bob";
        Check(rec.Name == "Bob", "Name change round-trip");

        // Inventory
        rec.ClearItem(0);
        Check(rec.GetItemId(0) == CharacterFormat.InventoryEmpty, "ClearItem sets 0xFF");
        rec.SetItem(0, 10, new byte[] { 10, 5, 0, 0, 0, 30 });
        Check(rec.GetItemId(0) == 10, "SetItem id");
        Check(rec.ItemCount == 1, "ItemCount == 1 after SetItem");
        rec.ClearItem(0);
        Check(rec.ItemCount == 0, "ItemCount == 0 after ClearItem");
    }

    private static void TestIsValidRecord()
    {
        Console.WriteLine("IsValidRecord");

        // Valid record
        var good = MakeRecord("Alice", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25);
        Check(CharacterRecord.IsValidRecord(good, 0), "Valid record accepted");

        // Empty buffer
        var empty = new byte[CharacterFormat.RecordSize];
        Check(!CharacterRecord.IsValidRecord(empty, 0), "Empty buffer rejected");

        // Name not starting with a letter
        var badName = MakeRecord("1abc", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25);
        Check(!CharacterRecord.IsValidRecord(badName, 0), "Name starting with digit rejected");

        // Attributes out of range
        var badAttr = MakeRecord("Bob", new[] { 0, 16, 11, 11, 16, 11, 11 }, 21, 25);
        Check(!CharacterRecord.IsValidRecord(badAttr, 0), "Attribute 0 rejected");
        var badAttr2 = MakeRecord("Bob", new[] { 21, 16, 11, 11, 16, 11, 11 }, 21, 25);
        Check(!CharacterRecord.IsValidRecord(badAttr2, 0), "Attribute > 20 rejected");

        // MaxCON out of range
        var badCon = MakeRecord("Bob", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25);
        WriteU16(badCon, CharacterFormat.OffMaxCon, 0);
        Check(!CharacterRecord.IsValidRecord(badCon, 0), "MaxCON 0 rejected");
        WriteU16(badCon, CharacterFormat.OffMaxCon, 1000);
        Check(!CharacterRecord.IsValidRecord(badCon, 0), "MaxCON > 999 rejected");

        // Level out of range
        var badLvl = MakeRecord("Bob", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25, level: 0);
        Check(!CharacterRecord.IsValidRecord(badLvl, 0), "Level 0 rejected");
        var badLvl2 = MakeRecord("Bob", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25, level: 100);
        Check(!CharacterRecord.IsValidRecord(badLvl2, 0), "Level > 99 rejected");

        // Profession out of range
        var badProf = MakeRecord("Bob", new[] { 11, 16, 11, 11, 16, 11, 11 }, 21, 25, profession: 7);
        Check(!CharacterRecord.IsValidRecord(badProf, 0), "Profession > 6 rejected");

        // Buffer too small
        Check(!CharacterRecord.IsValidRecord(new byte[10], 0), "Small buffer rejected");
    }

    // --- fake memory source for locator tests --------------------------------
    private sealed class FakeMemorySource : IMemorySource
    {
        private readonly byte[] _data;
        private readonly nuint _base;

        public FakeMemorySource(byte[] data, nuint baseAddr = 0x10000)
        {
            _data = data;
            _base = baseAddr;
        }

        public IEnumerable<MemoryRegion> EnumerateRegions()
        {
            yield return new MemoryRegion(_base, (nuint)_data.Length);
        }

        public int Read(nuint address, byte[] buffer, int count)
        {
            long off = (long)address - (long)_base;
            if (off < 0 || off >= _data.Length) return 0;
            int n = Math.Min(count, _data.Length - (int)off);
            Array.Copy(_data, (int)off, buffer, 0, n);
            return n;
        }

        public byte[] Read(nuint address, int count)
        {
            long off = (long)address - (long)_base;
            if (off < 0 || off >= _data.Length) return Array.Empty<byte>();
            int n = Math.Min(count, _data.Length - (int)off);
            var result = new byte[n];
            Array.Copy(_data, (int)off, result, 0, n);
            return result;
        }
    }

    private static byte[] MakeRoster(params string[] names)
    {
        var roster = new byte[CharacterFormat.MaxSlots * CharacterFormat.RecordSize];
        var attrs = new[] { 11, 16, 11, 11, 16, 11, 11 };
        for (int i = 0; i < names.Length; i++)
        {
            var rec = MakeRecord(names[i], attrs, 21, 25);
            Array.Copy(rec, 0, roster, i * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
        }
        return roster;
    }

    private static void TestLocator()
    {
        Console.WriteLine("PartyLocator");
        var roster = MakeRoster("Alice", "Bob", "Carol");
        var src = new FakeMemorySource(roster);

        var party = PartyLocator.Find(src);
        Check(party != null, "Party found");
        Check(party!.Members.Count == 3, "3 members found");
        Check(party.Members[0].Record.Name == "Alice", "Member 0 name == Alice");
        Check(party.Members[1].Record.Name == "Bob", "Member 1 name == Bob");
        Check(party.Members[2].Record.Name == "Carol", "Member 2 name == Carol");

        // Partial roster (1 character, 2 empty slots)
        var roster2 = MakeRoster("Solo");
        var src2 = new FakeMemorySource(roster2);
        var party2 = PartyLocator.Find(src2);
        Check(party2 != null, "Solo party found");
        Check(party2!.Members.Count == 1, "Solo party has 1 member");
        Check(party2.Members[0].Record.Name == "Solo", "Solo member name == Solo");
    }

    private static void TestLocatorEmptyMemory()
    {
        Console.WriteLine("PartyLocator empty memory");
        var empty = new byte[1024];
        var src = new FakeMemorySource(empty);
        var party = PartyLocator.Find(src);
        Check(party == null, "No party in empty memory");
    }

    private static void TestLocatorCancellation()
    {
        Console.WriteLine("PartyLocator cancellation");
        var roster = MakeRoster("Alice", "Bob", "Carol");
        var src = new FakeMemorySource(roster);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            _ = PartyLocator.Find(src, cts.Token);
            Check(false, "Cancellation should throw");
        }
        catch (OperationCanceledException)
        {
            Check(true, "Cancellation honoured");
        }
    }
}
