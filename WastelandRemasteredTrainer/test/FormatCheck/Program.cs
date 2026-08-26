using WastelandRemasteredTrainer.Game;
using WastelandRemasteredTrainer.Memory;
using WastelandRemasteredTrainer.ViewModels;

namespace WastelandRemasteredTrainer.FormatCheck;

internal sealed class Checker
{
    private int _pass;
    private int _fail;

    public void Run()
    {
        CheckGameFacts();
        CheckCharacterFormat();
        CheckSkillBook();
        CheckAttributeBook();
        CheckItemBook();
        CheckLooksLikePlayer();
        CheckCharacterRecordRoundTrip();
        CheckPackedSkills();
        CheckPackedItems();
        CheckIl2CppHelpers();
        CheckNativeStringSemantics();
        CheckTypedPartyWalk();
        CheckPeSectionSweep();
        CheckPartyStateReader();
        CheckGameLocatorStructuralScan();
        CheckCharacterViewModel();
        CheckWriteRoutesEveryField();
        CheckRegressions();
        CheckXamlLoads();

        Console.WriteLine();
        Console.WriteLine($"  {_pass + _fail} checks: {_pass} passed, {_fail} FAILED");
        if (_fail > 0) Environment.Exit(1);
    }

    private void True(bool condition, string label)
    {
        if (condition) { _pass++; Console.WriteLine($"  [PASS] {label}"); }
        else { _fail++; Console.WriteLine($"  [FAIL] {label}"); }
    }

    private void Eq<T>(T actual, T expected, string label) where T : IEquatable<T>
    {
        bool ok = actual is null ? expected is null : actual.Equals(expected);
        if (ok) { _pass++; Console.WriteLine($"  [PASS] {label}"); }
        else
        {
            _fail++;
            Console.WriteLine($"  [FAIL] {label}: expected {expected}, got {actual}");
        }
    }

    // =========================================================================
    private void CheckGameFacts()
    {
        Console.WriteLine("\n--- GameFacts ---");
        Eq(GameFacts.ProcessName, "Wasteland Remastered", "ProcessName");
        Eq(GameFacts.GameModuleName, "GameAssembly.dll", "GameModuleName");
        Eq(GameFacts.GameNamespace, "", "GameNamespace is empty");
        Eq(GameFacts.PlayerTypeName, "Player", "PlayerTypeName");
        Eq(GameFacts.PartyTypeName, "Party", "PartyTypeName");
        Eq(GameFacts.PartySlots, 7, "PartySlots");
        True(GameFacts.MaxPartyListEntries >= GameFacts.PartySlots, "Party list clamp is not tighter than the slot count");
        Eq(GameFacts.SkillSlots, 30, "SkillSlots");
        Eq(GameFacts.ItemSlots, 30, "ItemSlots");
        True(GameFacts.MaxLevel == 99, "MaxLevel");
        True(GameFacts.MaxAttribute == 99, "MaxAttribute");
        True(GameFacts.MaxCon == 5000, "MaxCon");
        True(GameFacts.MaxAmmo <= CharacterFormat.InventoryCountMask,
            "MaxAmmo fits the 7-bit count field (cannot set the jam bit)");
        True(SkillBook.Skills.Count > GameFacts.SkillSlots,
            "There are more skills than slots — 'learn all' must report the overflow");
    }

    private void CheckCharacterFormat()
    {
        Console.WriteLine("\n--- CharacterFormat ---");
        Eq(CharacterFormat.OffName, 0x10, "OffName");
        Eq(CharacterFormat.OffUniqueId, 0x18, "OffUniqueId");
        Eq(CharacterFormat.OffCName, 0x20, "OffCName");
        Eq(CharacterFormat.OffStrength, 0x28, "OffStrength");
        Eq(CharacterFormat.OffIQ, 0x29, "OffIQ");
        Eq(CharacterFormat.OffLuck, 0x2A, "OffLuck");
        Eq(CharacterFormat.OffSpeed, 0x2B, "OffSpeed");
        Eq(CharacterFormat.OffAgility, 0x2C, "OffAgility");
        Eq(CharacterFormat.OffDextermity, 0x2D, "OffDextermity");
        Eq(CharacterFormat.OffCharisma, 0x2E, "OffCharisma");
        Eq(CharacterFormat.OffMoney, 0x30, "OffMoney");
        Eq(CharacterFormat.OffSex, 0x34, "OffSex");
        Eq(CharacterFormat.OffNationality, 0x35, "OffNationality");
        Eq(CharacterFormat.OffAC, 0x36, "OffAC");
        Eq(CharacterFormat.OffMaxCon, 0x38, "OffMaxCon");
        Eq(CharacterFormat.OffCurCon, 0x3C, "OffCurCon");
        Eq(CharacterFormat.OffWeapon, 0x40, "OffWeapon");
        Eq(CharacterFormat.OffSkillPoints, 0x41, "OffSkillPoints");
        Eq(CharacterFormat.OffExperience, 0x44, "OffExperience");
        Eq(CharacterFormat.OffLevel, 0x48, "OffLevel");
        Eq(CharacterFormat.OffArmor, 0x49, "OffArmor");
        Eq(CharacterFormat.OffUncCon, 0x4C, "OffUncCon");
        Eq(CharacterFormat.OffDisease, 0x50, "OffDisease");
        Eq(CharacterFormat.OffNPC, 0x51, "OffNPC");
        Eq(CharacterFormat.OffNPCCom, 0x52, "OffNPCCom");
        Eq(CharacterFormat.OffNPCItem, 0x53, "OffNPCItem");
        Eq(CharacterFormat.OffNPCSkill, 0x54, "OffNPCSkill");
        Eq(CharacterFormat.OffNPCAtt, 0x55, "OffNPCAtt");
        Eq(CharacterFormat.OffNPCTrade, 0x56, "OffNPCTrade");
        Eq(CharacterFormat.OffNPCGreed, 0x57, "OffNPCGreed");
        Eq(CharacterFormat.OffNPCIMsg, 0x58, "OffNPCIMsg");
        Eq(CharacterFormat.OffNPCRecChr, 0x59, "OffNPCRecChr");
        Eq(CharacterFormat.OffRank, 0x60, "OffRank");
        Eq(CharacterFormat.OffWlsWon, 0x68, "OffWlsWon");
        Eq(CharacterFormat.OffWlsVer, 0x69, "OffWlsVer");
        Eq(CharacterFormat.OffSkills, 0x70, "OffSkills");
        Eq(CharacterFormat.OffItems, 0x78, "OffItems");
        Eq(CharacterFormat.OffSEName, 0x80, "OffSEName");
        Eq(CharacterFormat.OffSERank, 0x88, "OffSERank");
        Eq(CharacterFormat.OffHardwiredCameo, 0x90, "OffHardwiredCameo");
        Eq(CharacterFormat.OffSEItems, 0x98, "OffSEItems");
        Eq(CharacterFormat.OffSESkills, 0xA0, "OffSESkills");
        Eq(CharacterFormat.OffFireType, 0xA8, "OffFireType");
        Eq(CharacterFormat.OffClipSize, 0xAC, "OffClipSize");
        Eq(CharacterFormat.ObjectSize, 0xB0, "ObjectSize");
        Eq(CharacterFormat.ProbeSize, 0x60, "ProbeSize");
        Eq(CharacterFormat.PartyPlayers, 0x10, "PartyPlayers");
        Eq(CharacterFormat.PartyInstanceStatic, 0x00, "PartyInstanceStatic");
        Eq(CharacterFormat.WastelandInstanceStatic, 0x00, "WastelandInstanceStatic");
        Eq(CharacterFormat.WastelandPartyManager, 0x98, "WastelandPartyManager");
        Eq(CharacterFormat.PartyManagerInstanceStatic, 0x00, "PartyManagerInstanceStatic");
        Eq(CharacterFormat.PartyManagerSaveData, 0x28, "PartyManagerSaveData");
        Eq(CharacterFormat.CoreSaveMapX, 0x10, "CoreSaveMapX");
        Eq(CharacterFormat.CoreSaveMapY, 0x11, "CoreSaveMapY");
        Eq(CharacterFormat.CoreSaveNumberInParty, 0x18, "CoreSaveNumberInParty");
        Eq(CharacterFormat.CoreSaveCurrentMap, 0x1A, "CoreSaveCurrentMap");
        Eq(CharacterFormat.CoreSaveClock, 0x20, "CoreSaveClock");
        Eq(CharacterFormat.AttributeCount, 7, "AttributeCount");
        True(CharacterFormat.AttributeNames.Length == 7, "AttributeNames has 7 entries");
        Eq(CharacterFormat.AttributeNames[0], "STR", "AttributeNames[0] = STR");
        Eq(CharacterFormat.AttributeNames[6], "CHR", "AttributeNames[6] = CHR");

        // ProbeSize must cover every field LooksLikePlayer inspects.
        True(CharacterFormat.OffExperience + 4 <= CharacterFormat.ProbeSize,
            "ProbeSize covers every field the shape check reads");

        // Quantity-byte packing
        Eq(CharacterFormat.AmmoOf(0x94), 20, "AmmoOf strips the jam bit");
        True(CharacterFormat.IsJammed(0x94), "IsJammed detects bit 7");
        True(!CharacterFormat.IsJammed(0x14), "IsJammed false when bit 7 clear");
        Eq(CharacterFormat.PackQuantity(20, true), (byte)0x94, "PackQuantity sets the jam bit");
        Eq(CharacterFormat.PackQuantity(20, false), (byte)0x14, "PackQuantity clears the jam bit");
        Eq(CharacterFormat.PackQuantity(200, false), (byte)(200 & 0x7F), "PackQuantity masks an over-large count");
    }

    private void CheckSkillBook()
    {
        Console.WriteLine("\n--- SkillBook ---");
        True(SkillBook.Skills.Count == 35, "35 skills");
        Eq(SkillBook.SkillName(1), "Brawling", "Skill 1 = Brawling");
        Eq(SkillBook.SkillName(35), "Cyborg Tech", "Skill 35 = Cyborg Tech");
        Eq(SkillBook.SkillName(0), "Skill #0", "Unknown skill id returns placeholder");
        True(SkillBook.Find(9) is { Name: "Perception" }, "Skill 9 = Perception");
        True(SkillBook.Find(99) == null, "Skill 99 not found");

        var ids = SkillBook.Skills.Select(s => s.Id).OrderBy(x => x).ToArray();
        for (int i = 0; i < 35; i++)
            True(ids[i] == i + 1, $"Skill id {i + 1} present");

        True(SkillBook.Find(1)!.MinIq == 0, "Brawling MinIq = 0");
        True(SkillBook.Find(9)!.MinIq == 10, "Perception MinIq = 10");
        True(SkillBook.Find(35)!.MinIq == 24, "Cyborg Tech MinIq = 24");
    }

    private void CheckAttributeBook()
    {
        Console.WriteLine("\n--- AttributeBook ---");
        True(AttributeBook.Attributes.Count == 7, "7 attributes");
        Eq(AttributeBook.ByIndex(0)!.Abbr, "STR", "Index 0 = STR");
        Eq(AttributeBook.ByIndex(1)!.Abbr, "IQ", "Index 1 = IQ");
        Eq(AttributeBook.ByIndex(6)!.Abbr, "CHR", "Index 6 = CHR");
        True(AttributeBook.ByIndex(7) == null, "Index 7 out of range");
        True(AttributeBook.DescriptionOf(0).Length > 0, "Description for STR not empty");
        True(AttributeBook.DescriptionOf(99).Length == 0, "Description for out-of-range is empty");

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            Eq(AttributeBook.ByIndex(i)!.Abbr, CharacterFormat.AttributeNames[i],
                $"AttributeBook[{i}] matches record order");
    }

    private void CheckItemBook()
    {
        Console.WriteLine("\n--- ItemBook ---");
        True(ItemBook.Items.Count >= 90, "At least 90 items (incl. None)");
        Eq(ItemBook.ItemName(0), "(empty)", "Item 0 = (empty)");
        Eq(ItemBook.ItemName(4), "Knife", "Item 4 = Knife");
        Eq(ItemBook.ItemName(13), "M1911A1 45 pistol", "Item 13 = M1911A1 45 pistol");
        Eq(ItemBook.ItemName(29), "Meson cannon", "Item 29 = Meson cannon");
        Eq(ItemBook.ItemName(94), "Cash", "Item 94 = Cash");
        True(ItemBook.Find(99) == null, "Item 99 not found");
        True(ItemBook.IsAmmoItem(13), "Pistol is ammo item");
        True(!ItemBook.IsAmmoItem(4), "Knife is not ammo item");
        True(ItemBook.IsAmmoItem(30), "45 clip is ammo item");
        True(!ItemBook.IsAmmoItem(35), "Power armor is not ammo item");
        True(!ItemBook.IsAmmoItem(0), "Empty slot is not an ammo item");

        var ids = ItemBook.Items.Select(i => i.Id).ToArray();
        True(ids.Distinct().Count() == ids.Length, "All item ids are unique");
        True(ids.All(id => id >= 0 && id <= byte.MaxValue), "Every item id fits in a byte");
    }

    private void CheckLooksLikePlayer()
    {
        Console.WriteLine("\n--- LooksLikePlayer ---");

        var buf = ValidPlayerBuffer();
        True(CharacterFormat.LooksLikePlayer(buf), "Valid player passes");

        var tooShort = new byte[CharacterFormat.ProbeSize - 1];
        True(!CharacterFormat.LooksLikePlayer(tooShort), "Short buffer rejected");

        var b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffMaxCon, 0);
        True(!CharacterFormat.LooksLikePlayer(b), "MaxCon=0 rejected");

        b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffCurCon, 100);
        True(!CharacterFormat.LooksLikePlayer(b), "CurCon>MaxCon rejected");

        // A dying ranger runs CON below zero — the scan must still recognise the record.
        b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffCurCon, -5);
        True(CharacterFormat.LooksLikePlayer(b), "Negative CurCon still recognised (dying ranger)");

        b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffCurCon, CharacterFormat.MinPlausibleCon - 1);
        True(!CharacterFormat.LooksLikePlayer(b), "Absurdly negative CurCon rejected");

        b = (byte[])buf.Clone();
        b[CharacterFormat.OffLevel] = 200;
        True(!CharacterFormat.LooksLikePlayer(b), "Level>MaxLevel rejected");

        b = (byte[])buf.Clone();
        b[CharacterFormat.OffStrength] = 200;
        True(!CharacterFormat.LooksLikePlayer(b), "Attribute>MaxAttribute rejected");

        b = (byte[])buf.Clone();
        b[CharacterFormat.OffStrength] = 0;
        True(!CharacterFormat.LooksLikePlayer(b), "Zero attribute rejected");

        b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffMoney, -1);
        True(!CharacterFormat.LooksLikePlayer(b), "Negative money rejected");

        b = (byte[])buf.Clone();
        WriteI32(b, CharacterFormat.OffExperience, -1);
        True(!CharacterFormat.LooksLikePlayer(b), "Negative experience rejected");

        b = (byte[])buf.Clone();
        b[CharacterFormat.OffSex] = 5;
        True(!CharacterFormat.LooksLikePlayer(b), "Sex>1 rejected");

        // A character the trainer has already maxed out must still be found next time.
        b = (byte[])buf.Clone();
        for (int i = 0; i < 7; i++) b[CharacterFormat.OffStrength + i] = (byte)GameFacts.MaxAttribute;
        WriteI32(b, CharacterFormat.OffMoney, GameFacts.MaxMoney);
        WriteI32(b, CharacterFormat.OffExperience, GameFacts.MaxExperience);
        b[CharacterFormat.OffLevel] = (byte)GameFacts.MaxLevel;
        True(CharacterFormat.LooksLikePlayer(b), "A maxed-out character still passes the shape check");
    }

    private void CheckCharacterRecordRoundTrip()
    {
        Console.WriteLine("\n--- CharacterRecord round-trip ---");

        var mem = new FakeMemorySource();
        nuint playerAddr = 0x10000;
        var playerBuf = new byte[0x200];
        WriteI32(playerBuf, CharacterFormat.OffMaxCon, 50);
        WriteI32(playerBuf, CharacterFormat.OffCurCon, 45);
        WriteI32(playerBuf, CharacterFormat.OffMoney, 500);
        WriteI32(playerBuf, CharacterFormat.OffExperience, 1000);
        playerBuf[CharacterFormat.OffLevel] = 3;
        playerBuf[CharacterFormat.OffSkillPoints] = 5;
        for (int i = 0; i < 7; i++) playerBuf[CharacterFormat.OffStrength + i] = 15;
        mem.Map(playerAddr, playerBuf);

        var record = new CharacterRecord(mem, playerAddr, 0);

        Eq(record.MaxCon, 50, "Read MaxCon");
        Eq(record.CurCon, 45, "Read CurCon");
        Eq(record.Money, 500, "Read Money");
        Eq(record.Experience, 1000, "Read Experience");
        Eq(record.Level, 3, "Read Level");
        Eq(record.SkillPoints, 5, "Read SkillPoints");
        Eq(record.GetAttribute(0), 15, "Read Strength");
        True(record.IsReadable, "IsReadable true for a mapped object");

        record.MaxCon = 99; Eq(record.MaxCon, 99, "Write MaxCon round-trip");
        record.Money = 9999; Eq(record.Money, 9999, "Write Money round-trip");
        record.SetAttribute(0, 30); Eq(record.GetAttribute(0), 30, "Write Strength round-trip");
        record.Level = 10; Eq(record.Level, 10, "Write Level round-trip");
        record.UncCon = 12; Eq(record.UncCon, 12, "Write UncCon round-trip");
        record.AC = 7; Eq(record.AC, 7, "Write AC round-trip");
        record.Weapon = 23; Eq(record.Weapon, 23, "Write Weapon round-trip");
        record.Armor = 35; Eq(record.Armor, 35, "Write Armor round-trip");

        // Clamping
        record.Level = 200; Eq(record.Level, GameFacts.MaxLevel, "Level clamped to MaxLevel");
        record.SetAttribute(0, 0); Eq(record.GetAttribute(0), GameFacts.MinAttribute, "Attribute clamped to Min");
        record.Money = int.MaxValue; Eq(record.Money, GameFacts.MaxMoney, "Money clamped to MaxMoney");
        record.Experience = int.MaxValue; Eq(record.Experience, GameFacts.MaxExperience, "Experience clamped");
        record.MaxCon = 999_999; Eq(record.MaxCon, GameFacts.MaxCon, "MaxCon clamped");

        // Disease is a byte: an out-of-range value must clamp, never wrap to 0.
        record.Disease = 300; Eq(record.Disease, 255, "Disease clamped, not wrapped");
        record.Disease = -1; Eq(record.Disease, 0, "Negative disease clamped to 0");

        record.Sex = 9; Eq(record.Sex, CharacterFormat.Genders.Length - 1, "Sex clamped to the known range");
        record.Nationality = 99;
        Eq(record.Nationality, CharacterFormat.Nationalities.Length - 1, "Nationality clamped to the known range");

        // Attribute index is bounded — index 7 would otherwise land on Money's low byte.
        True(!record.SetAttribute(7, 50), "SetAttribute rejects an out-of-range index");
        True(!record.SetAttribute(-1, 50), "SetAttribute rejects a negative index");
        Eq(record.GetAttribute(7), 0, "GetAttribute returns 0 for an out-of-range index");
        record.Money = 4242;
        True(!record.SetAttribute(7, 50), "Out-of-range attribute write is refused");
        Eq(record.Money, 4242, "Money untouched by the out-of-range attribute write");

        record.CurCon = 10;
        record.FullHeal();
        Eq(record.CurCon, record.MaxCon, "FullHeal sets CurCon=MaxCon");

        record.MaxAttributes();
        for (int i = 0; i < 7; i++)
            Eq(record.GetAttribute(i), GameFacts.MaxAttribute, $"Attribute {i} maxed");
    }

    // =========================================================================
    private void CheckPackedSkills()
    {
        Console.WriteLine("\n--- Packed skills ---");

        var (mem, record, skills, _) = BuildPlayerWithArrays();

        // Seed: Brawling L2, Perception L4
        WriteSlot(mem, skills, 0, 1, 2);
        WriteSlot(mem, skills, 1, 9, 4);

        var read = record.ReadSkills();
        Eq(read.Count, 2, "ReadSkills finds both seeded skills");
        Eq(read[0].Id, 1, "Skill 0 id");
        Eq(read[0].Level, 2, "Skill 0 level");
        Eq(read[0].Slot, 0, "Skill 0 slot");
        Eq(read[0].Name, "Brawling", "Skill 0 name");
        Eq(read[1].Id, 9, "Skill 1 id");
        Eq(read[1].Name, "Perception", "Skill 1 name");

        // A skill sitting at level 0 is still a real entry and must be visible/fixable.
        WriteSlot(mem, skills, 2, 15, 0);
        read = record.ReadSkills();
        Eq(read.Count, 3, "A level-0 skill is still listed");
        True(record.SetSkill(15, 6), "SetSkill updates an existing skill");
        Eq(record.ReadSkills().First(s => s.Id == 15).Level, 6, "Level-0 skill raised to 6");

        // Adding into the first free slot
        True(record.SetSkill(31, 3), "SetSkill adds a new skill");
        var added = record.ReadSkills().FirstOrDefault(s => s.Id == 31);
        Eq(added.Level, 3, "Newly added skill has the right level");
        Eq(record.ReadSkills().Count, 4, "Skill count grew by one");

        // Bounds
        True(!record.SetSkill(0, 5), "SetSkill rejects id 0");
        True(!record.SetSkill(5, GameFacts.MaxSkillLevel + 1), "SetSkill rejects an over-range level");
        True(!record.SetSkill(5, -1), "SetSkill rejects a negative level");

        Eq(record.FreeSkillSlots(), GameFacts.SkillSlots - 4, "FreeSkillSlots counts the remaining slots");

        int raised = record.MaxSkills();
        Eq(raised, 4, "MaxSkills raised every known skill");
        True(record.ReadSkills().All(s => s.Level == GameFacts.MaxSkillLevel), "All skills at max level");
        Eq(record.MaxSkills(), 0, "MaxSkills is a no-op when everything is already maxed");

        // Learn-all cannot fit 35 skills into 30 slots and must say so.
        var result = record.LearnAllSkills(GameFacts.MaxSkillLevel);
        Eq(record.ReadSkills().Count, GameFacts.SkillSlots, "Learn-all filled every slot");
        True(!result.Complete, "Learn-all reports that it could not fit every skill");
        Eq(result.NotLearned.Count, SkillBook.Skills.Count - GameFacts.SkillSlots,
            "Exactly the overflow skills are reported as not learned");
        True(result.Learned == GameFacts.SkillSlots - 4, "Learn-all added only into the free slots");

        // A short array must still work rather than disabling writes entirely.
        var (mem2, record2, shortSkills, _) = BuildPlayerWithArrays(skillArrayBytes: 8);
        WriteSlot(mem2, shortSkills, 0, 1, 1);
        Eq(record2.ReadSkills().Count, 1, "Short skill array still reads");
        True(record2.SetSkill(1, 9), "Short skill array still writes");
        Eq(record2.ReadSkills()[0].Level, 9, "Short skill array write took effect");
        True(record2.SetSkill(2, 5), "Short skill array accepts a second skill");
        True(record2.SetSkill(3, 5), "Short skill array accepts a third skill");
        True(record2.SetSkill(4, 5), "Short skill array accepts a fourth skill");
        True(!record2.SetSkill(5, 5), "Short skill array refuses a fifth skill (it holds only four)");
        Eq(record2.FreeSkillSlots(), 0, "Short skill array reports no free slots when full");
    }

    private void CheckPackedItems()
    {
        Console.WriteLine("\n--- Packed inventory ---");

        var (mem, record, _, items) = BuildPlayerWithArrays();

        // AK-97 (id 23) jammed with 20 rounds: quantity byte 0x94.
        WriteSlot(mem, items, 0, 23, 0x94);
        WriteSlot(mem, items, 1, 4, 1);          // Knife

        var read = record.ReadItems();
        Eq(read.Count, 2, "ReadItems finds both items");
        Eq(read[0].Id, 23, "Item 0 id");
        Eq(read[0].Quantity, 0x94, "Item 0 raw quantity byte preserved");
        Eq(read[0].Ammo, 20, "Item 0 ammo has the jam bit masked off");
        True(read[0].Jammed, "Item 0 reports jammed");
        True(!read[1].Jammed, "Knife is not jammed");
        Eq(read[0].Name, "AK 97 assault rifle", "Item 0 name");

        // SetItem must never let a big ammo number set the jam bit.
        True(record.SetItem(1, 30, 200), "SetItem writes a slot");
        var slot1 = record.ReadItems()[1];
        Eq(slot1.Id, 30, "SetItem changed the item id");
        Eq(slot1.Ammo, CharacterFormat.InventoryCountMask,
            "SetItem clamps an over-large ammo request to the field maximum (never wraps it)");
        True(!slot1.Jammed, "A large ammo value did not set the jam bit");

        True(record.SetItem(1, 30, 10, jammed: true), "SetItem can set the jam bit deliberately");
        True(record.ReadItems()[1].Jammed, "Deliberate jam flag round-trips");

        True(!record.SetItem(-1, 4, 1), "SetItem rejects a negative slot");
        True(!record.SetItem(GameFacts.ItemSlots, 4, 1), "SetItem rejects a slot past the end");

        // Add / remove
        True(record.AddItem(34, 12), "AddItem uses the first free slot");
        Eq(record.ReadItems().Count, 3, "Item count grew");
        Eq(record.ReadItems()[2].Id, 34, "Added item landed in slot 2");

        True(record.RemoveItem(0), "RemoveItem clears a slot");
        var after = record.ReadItems();
        Eq(after.Count, 2, "Item count shrank");
        Eq(after[0].Id, 30, "Remove closed the gap — the next item moved up");
        Eq(after[1].Id, 34, "Second item moved up too");

        // Max ammo and jam clearing
        record.SetItem(0, 23, 3, jammed: true);
        int topped = record.MaxAmmo();
        True(topped >= 1, "MaxAmmo topped up at least one item");
        var rifle = record.ReadItems()[0];
        Eq(rifle.Ammo, GameFacts.MaxAmmo, "MaxAmmo set the count to the ceiling");
        True(!rifle.Jammed, "MaxAmmo cleared the jam flag");

        record.SetItem(0, 23, 5, jammed: true);
        Eq(record.ClearJams(), 1, "ClearJams cleared one jam");
        var cleared = record.ReadItems()[0];
        True(!cleared.Jammed, "Jam flag gone");
        Eq(cleared.Ammo, 5, "ClearJams left the ammo count alone");

        // Non-ammo items are left alone by MaxAmmo.
        record.SetItem(0, 4, 1);                  // Knife
        record.SetItem(1, 0, 0);                  // terminate after slot 0
        Eq(record.MaxAmmo(), 0, "MaxAmmo skips items that carry no ammo");
    }

    private void CheckIl2CppHelpers()
    {
        Console.WriteLine("\n--- IL2CPP helpers ---");

        var mem = new FakeMemorySource();

        nuint ptrAddr = 0x1000;
        mem.Map(ptrAddr, new byte[16]);
        mem.WritePtr(ptrAddr, unchecked((nuint)0x1234567890ABCDEF));
        Eq(mem.ReadPtr(ptrAddr), unchecked((nuint)0x1234567890ABCDEF), "WritePtr/ReadPtr round-trip");

        mem.WriteI32(ptrAddr, -12345);
        Eq(mem.ReadI32(ptrAddr), -12345, "WriteI32/ReadI32 round-trip");

        mem.WriteByte(ptrAddr, 0xAB);
        Eq(mem.ReadByte(ptrAddr), (byte)0xAB, "WriteByte/ReadByte round-trip");

        // TryRead* must distinguish "unreadable" from "zero".
        True(mem.TryReadI32(ptrAddr, out _), "TryReadI32 succeeds on mapped memory");
        True(!mem.TryReadI32(0xDEAD0000, out int stray), "TryReadI32 fails on unmapped memory");
        Eq(stray, 0, "Failed TryReadI32 yields 0");
        True(!mem.TryReadPtr(0xDEAD0000, out _), "TryReadPtr fails on unmapped memory");
        True(!mem.TryReadByte(0xDEAD0000, out _), "TryReadByte fails on unmapped memory");
        Eq(mem.ReadI32(0xDEAD0000), 0, "ReadI32 returns 0 for unreadable memory");

        nuint arrayAddr = 0x2000;
        var arrayBuf = new byte[0x100];
        WriteI32(arrayBuf, Il2Cpp.ArrayLengthOffset, 30);
        mem.Map(arrayAddr, arrayBuf);
        Eq(mem.ReadArrayLength(arrayAddr), 30, "ReadArrayLength");
        Eq(mem.ReadArrayLength(0), 0, "ReadArrayLength of a null array is 0");

        mem.WriteByteArrayElement(arrayAddr, 5, 0x42);
        Eq(mem.ReadByteArrayElement(arrayAddr, 5), (byte)0x42, "ByteArray element round-trip");
        Eq(Il2Cpp.ByteArrayElement(arrayAddr, 5), arrayAddr + Il2Cpp.ArrayHeaderSize + 5, "ByteArrayElement address");
        Eq(Il2Cpp.ArrayElement(arrayAddr, 3), arrayAddr + Il2Cpp.ArrayHeaderSize + 24, "ArrayElement address");

        nuint listAddr = 0x3000;
        var listBuf = new byte[0x40];
        nuint itemsArrayAddr = 0x4000;
        var itemsBuf = new byte[0x200];
        WriteI64(listBuf, Il2Cpp.ListItemsOffset, itemsArrayAddr);
        WriteI32(listBuf, Il2Cpp.ListSizeOffset, 3);
        WriteI32(itemsBuf, Il2Cpp.ArrayLengthOffset, 4);
        WriteI64(itemsBuf, Il2Cpp.ArrayHeaderSize + 0 * 8, 0x10000);
        WriteI64(itemsBuf, Il2Cpp.ArrayHeaderSize + 1 * 8, 0x20000);
        WriteI64(itemsBuf, Il2Cpp.ArrayHeaderSize + 2 * 8, 0x30000);
        mem.Map(listAddr, listBuf);
        mem.Map(itemsArrayAddr, itemsBuf);

        Eq(mem.ReadListCount(listAddr), 3, "ReadListCount");
        Eq(mem.ReadListRef(listAddr, 0), (nuint)0x10000, "ReadListRef[0]");
        Eq(mem.ReadListRef(listAddr, 1), (nuint)0x20000, "ReadListRef[1]");
        Eq(mem.ReadListRef(listAddr, 2), (nuint)0x30000, "ReadListRef[2]");
        Eq(mem.ReadListCount(0), 0, "ReadListCount of a null list is 0");
    }

    private void CheckNativeStringSemantics()
    {
        Console.WriteLine("\n--- Native string reads ---");

        var mem = new FakeMemorySource();
        nuint at = 0x8000;
        var page = new byte[0x100];
        // "Party\0" at +0, empty string at +0x20, unterminated run at +0x40
        "Party"u8.CopyTo(page.AsSpan(0));
        page[0x20] = 0;
        for (int i = 0x40; i < 0x100; i++) page[i] = (byte)'A';
        mem.Map(at, page);

        True(mem.TryReadNativeString(at, out string party) && party == "Party", "Reads a terminated ASCII name");
        True(mem.TryReadNativeString(at + 0x20, out string empty) && empty == "",
            "An empty string reads as success with an empty value");

        // The critical distinction: a failed read must NOT look like the empty namespace.
        True(!mem.TryReadNativeString(0xDEAD0000, out string bad), "An unreadable pointer reports failure");
        Eq(bad, "", "A failed read yields an empty value");
        True(!mem.TryReadNativeString(0, out _), "A null pointer reports failure");
        True(!mem.TryReadNativeString(at + 0x40, out _), "An unterminated run is rejected");

        // ClassMatches must reject a candidate whose namespace pointer is unreadable, even
        // though the game's own namespace is the empty string.
        var heap = new FakeMemorySource();
        nuint strings = 0x9000;
        var strPage = new byte[0x100];
        "Player"u8.CopyTo(strPage.AsSpan(0x00));
        strPage[0x20] = 0;                                  // empty namespace
        heap.Map(strings, strPage);

        nuint klass = 0xA000;
        var klassPage = new byte[0x100];
        WriteI64(klassPage, Il2Cpp.ClassNameOffset, strings + 0x00);
        WriteI64(klassPage, Il2Cpp.ClassNamespaceOffset, strings + 0x20);
        heap.Map(klass, klassPage);
        True(heap.ClassMatches(klass, "Player", ""), "ClassMatches accepts the right name and namespace");
        True(!heap.ClassMatches(klass, "Party", ""), "ClassMatches rejects the wrong name");

        nuint badKlass = 0xB000;
        var badPage = new byte[0x100];
        WriteI64(badPage, Il2Cpp.ClassNameOffset, strings + 0x00);
        WriteI64(badPage, Il2Cpp.ClassNamespaceOffset, 0xDEAD0000);   // unreadable namespace
        heap.Map(badKlass, badPage);
        True(!heap.ClassMatches(badKlass, "Player", ""),
            "ClassMatches rejects an unreadable namespace instead of treating it as global");

        nuint obj = 0xC000;
        var objPage = new byte[0x40];
        WriteI64(objPage, Il2Cpp.ObjectClassOffset, klass);
        heap.Map(obj, objPage);
        Eq(heap.ReadObjectClass(obj), klass, "ReadObjectClass reads the header class pointer");
        True(heap.IsInstanceOf(obj, klass), "IsInstanceOf true for the matching class");
        True(!heap.IsInstanceOf(obj, badKlass), "IsInstanceOf false for a different class");
        True(!heap.IsInstanceOf(0, klass), "IsInstanceOf false for a null object");
    }

    // =========================================================================
    /// <summary>
    /// Drives the primary locate path end to end over a synthetic IL2CPP image: PE headers with
    /// one data section, a class pointer in that section, statics, the Party singleton, and a
    /// List&lt;Player&gt; of real objects. This is the route the trainer actually uses in a live
    /// game, and it was previously never executed by the harness.
    /// </summary>
    private void CheckTypedPartyWalk()
    {
        Console.WriteLine("\n--- Typed party walk (Party.m_instance -> players) ---");

        var heap = BuildSyntheticGame(out nuint moduleBase, out nuint moduleSize,
            out nuint partyClass, out nuint playerClass, out nuint[] players, out nuint imposter);

        var location = GameLocator.Locate(heap, moduleBase, moduleSize);
        True(location != null, "Locate found the party through the class sweep");
        if (location == null) return;

        True(!location.UsedFallback, "The typed path was used, not the structural fallback");
        Eq(location.Classes.Party, partyClass, "Swept the right Party class pointer");
        Eq(location.PlayerClass, playerClass, "Derived the Player class from a party member's header");
        Eq(location.CharacterCount, 3, "All three rangers were returned");
        Eq(location.RejectedEntries, 1, "The non-Player entry was rejected and counted");
        True(location.Summary.Contains("could not be confirmed"), "The rejected entry is surfaced in the summary");

        for (int i = 0; i < players.Length; i++)
            Eq(location.CharacterAddresses[i], players[i], $"Ranger {i} address");

        True(!location.CharacterAddresses.Contains(imposter), "The imposter object was not returned");

        // Ranger 2 is dying (negative CON) and ranger 3 has been maxed past the natural range.
        // Both used to be dropped by the plausibility gate; both must survive an identity check.
        var dying = new CharacterRecord(heap, players[1], 1);
        True(dying.CurCon < 0, "Ranger 2 really is at negative CON in the fixture");
        Eq(location.CharacterAddresses[1], players[1], "A dying ranger is still listed");

        var maxed = new CharacterRecord(heap, players[2], 2);
        True(maxed.GetAttribute(0) > GameFacts.MaxAttribute, "Ranger 3 really is above the attribute ceiling");
        Eq(location.CharacterAddresses[2], players[2], "An over-maxed ranger is still listed");

        // The sweep only has to find Party; the optional classes may legitimately be missing.
        True(location.Classes.IsValid, "GameClasses.IsValid keys off Party alone");
        True(location.Classes.Method.Contains("data-section"),
            "The PE section table was parsed and only data sections were swept");
    }

    private void CheckGameLocatorStructuralScan()
    {
        Console.WriteLine("\n--- GameLocator structural scan ---");

        var emptyMem = new FakeMemorySource();
        True(GameLocator.Locate(emptyMem, 0, 0) == null, "Empty memory returns null");

        var mem = new FakeMemorySource();
        nuint playerAddr = 0x200000;
        var playerBuf = new byte[0x200];
        ValidPlayerBuffer().CopyTo(playerBuf.AsSpan(0));
        mem.Map(playerAddr, playerBuf);

        var loc = GameLocator.Locate(mem, 0, 0);
        True(loc != null, "Player object found via structural scan");
        True(loc!.UsedFallback, "UsedFallback is true");
        True(loc.CharacterCount == 1, "One character found");
        True(loc.CharacterAddresses[0] == playerAddr, "Correct address");

        // Progress is reported when a caller asks for it.
        int progressReports = 0;
        var progress = new Progress<double>(_ => Interlocked.Increment(ref progressReports));
        GameLocator.Locate(mem, 0, 0, progress);
        True(GameLocator.Locate(mem, 0, 0, progress) != null,
            "Structural scan still succeeds when a progress reporter is supplied");

        // A cancelled scan surfaces as OperationCanceledException rather than running to the end.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool cancelled = false;
        try { GameLocator.Locate(mem, 0, 0, null, cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        True(cancelled, "A cancelled token stops the scan");
    }

    // =========================================================================
    private sealed class RecordingHost : ICharacterHost
    {
        public List<string> Messages { get; } = new();
        public int RefreshCount { get; private set; }
        public void OnMessage(string message) => Messages.Add(message);
        public void RefreshSelected() => RefreshCount++;
    }

    private void CheckCharacterViewModel()
    {
        Console.WriteLine("\n--- CharacterViewModel (edit tracking and freezes) ---");

        var (mem, record, skills, items) = BuildPlayerWithArrays();
        record.MaxCon = 50;
        record.CurCon = 45;
        record.Money = 500;
        record.Experience = 1000;
        record.Level = 3;
        for (int i = 0; i < 7; i++) record.SetAttribute(i, 15);

        var host = new RecordingHost();
        var vm = new CharacterViewModel(record, host);

        Eq(vm.Strength, 15, "VM loaded Strength");
        Eq(vm.Experience, 1000, "VM loaded Experience");
        True(!vm.HasPendingEdits, "No pending edits after load");

        // The user edits one stat.
        vm.Strength = 60;
        True(vm.HasPendingEdits, "Editing a field marks it pending");

        // Meanwhile the game awards experience and a level.
        record.Experience = 40_000;
        record.Level = 9;
        record.Money = 3000;

        // A poll-driven refresh must pick up the game's changes without losing the edit.
        vm.RefreshScalars();
        Eq(vm.Experience, 40_000, "Refresh picked up the game's new experience");
        Eq(vm.Level, 9, "Refresh picked up the game's new level");
        Eq(vm.Strength, 60, "Refresh left the pending edit alone");

        // Writing must touch only the edited field.
        vm.Write();
        Eq(record.GetAttribute(0), 60, "Write applied the edited attribute");
        Eq(record.Experience, 40_000, "Write did not roll back experience");
        Eq(record.Level, 9, "Write did not roll back level");
        Eq(record.Money, 3000, "Write did not roll back money");
        True(!vm.HasPendingEdits, "Pending edits cleared after write");

        // Revert throws an edit away without writing it.
        vm.IQ = 77;
        True(vm.HasPendingEdits, "Second edit marked pending");
        vm.Revert();
        Eq(record.GetAttribute(1), 15, "Reverted edit was never written");
        Eq(vm.IQ, 15, "Revert restored the displayed value");
        True(!vm.HasPendingEdits, "Pending edits cleared after revert");

        // Freeze CON pins to the game's live maximum, not to a half-typed edit box.
        record.CurCon = 5;
        vm.FreezeCon = true;
        vm.MaxCon = 7;                     // user has started typing "700"
        vm.ApplyFreezes();
        Eq(record.CurCon, 50, "Freeze CON used the game's live MaxCon, not the edit box");
        vm.Revert();

        // Freeze money pins to the value showing when the box was ticked.
        record.Money = 1234;
        vm.RefreshScalars();
        vm.FreezeMoney = true;
        record.Money = 7;                  // the game spent the cash
        vm.ApplyFreezes();
        Eq(record.Money, 1234, "Freeze money restored the pinned amount");

        // Freeze ammo tops the pack up each tick.
        WriteSlot(mem, items, 0, 23, 3);
        vm.FreezeAmmo = true;
        vm.ApplyFreezes();
        Eq(record.ReadItems()[0].Ammo, GameFacts.MaxAmmo, "Freeze ammo topped the rifle up");

        // Quick actions supersede a pending edit rather than fighting it.
        vm.Strength = 5;
        vm.MaxAttributesCommand.Execute(null);
        Eq(vm.Strength, GameFacts.MaxAttribute, "Max attributes replaced the pending edit");
        True(!vm.HasPendingEdits, "Max attributes cleared the superseded edit");

        // Skill rows write through immediately.
        WriteSlot(mem, skills, 0, 1, 2);
        vm.Refresh();
        True(vm.Skills.Count >= 1, "Skill rows built");
        vm.Skills[0].Level = 8;
        Eq(record.ReadSkills()[0].Level, 8, "Editing a skill row wrote straight through");

        // Item rows write through immediately.
        True(vm.Items.Count >= 1, "Item rows built");
        vm.Items[0].Ammo = 42;
        Eq(record.ReadItems()[0].Ammo, 42, "Editing an item row wrote straight through");
        vm.Items[0].Jammed = true;
        True(record.ReadItems()[0].Jammed, "Toggling the jam flag wrote straight through");

        True(host.Messages.Count > 0, "The host received status messages");
    }

    /// <summary>
    /// Builds the real MainWindow so the compiled XAML is actually executed.
    ///
    /// <para>Everything else here is headless, but StaticResource lookups, x:Static references and
    /// converter resolution all happen when the window is constructed — a typo there compiles
    /// cleanly and only blows up at launch. This is the one check that would catch it.</para>
    /// </summary>
    private void CheckXamlLoads()
    {
        Console.WriteLine();
        Console.WriteLine("--- XAML smoke test ---");

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                if (window.DataContext is MainViewModel vm) vm.Dispose();
                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        True(failure == null,
            failure == null
                ? "MainWindow builds: every StaticResource and x:Static reference resolves"
                : $"MainWindow failed to build: {failure.Message}");
    }

    /// <summary>
    /// Drives every editable property through Write() at once, each with a distinct value.
    ///
    /// <para>Write() dispatches on the property name through a hand-written switch. A single
    /// mis-pasted case — say `nameof(Nationality) =&gt; Assign(() =&gt; _record.Sex = _nationality)` —
    /// compiles, and a test that only ever dirties one field would never see it. Distinct values
    /// mean a cross-wired case lands the wrong number in the wrong field and fails here.</para>
    ///
    /// <para>A property missing from the switch is caught too: it would stay dirty-but-unwritten,
    /// so the record keeps its old value.</para>
    /// </summary>
    private void CheckWriteRoutesEveryField()
    {
        Console.WriteLine();
        Console.WriteLine("--- Write() routes every editable field ---");

        var (_, record, _, _) = BuildPlayerWithArrays();
        var host = new RecordingHost();
        var vm = new CharacterViewModel(record, host);

        // Distinct values, all inside their clamps, so a swapped case cannot coincidentally pass.
        vm.Strength = 21; vm.IQ = 22; vm.Luck = 23; vm.Speed = 24;
        vm.Agility = 25; vm.Dextermity = 26; vm.Charisma = 27;
        vm.MaxCon = 1500; vm.CurCon = 1400; vm.UncCon = 13;
        vm.Money = 123_456; vm.Experience = 654_321; vm.Level = 42; vm.SkillPoints = 44;
        vm.AC = 55; vm.Weapon = 23; vm.Armor = 35; vm.Disease = 3;
        vm.Sex = 1; vm.Nationality = 3;

        var pendingBefore = vm.PendingFieldNames.ToArray();
        Eq(vm.PendingCount, CharacterViewModel.EditableFieldNames.Count,
            "Every declared editable field is pending before the write");
        vm.Write();
        True(!vm.HasPendingEdits, "Nothing left pending after the write");

        Eq(record.GetAttribute(0), 21, "Write routed Strength");
        Eq(record.GetAttribute(1), 22, "Write routed IQ");
        Eq(record.GetAttribute(2), 23, "Write routed Luck");
        Eq(record.GetAttribute(3), 24, "Write routed Speed");
        Eq(record.GetAttribute(4), 25, "Write routed Agility");
        Eq(record.GetAttribute(5), 26, "Write routed Dextermity");
        Eq(record.GetAttribute(6), 27, "Write routed Charisma");
        Eq(record.MaxCon, 1500, "Write routed MaxCon");
        Eq(record.CurCon, 1400, "Write routed CurCon");
        Eq(record.UncCon, 13, "Write routed UncCon");
        Eq(record.Money, 123_456, "Write routed Money");
        Eq(record.Experience, 654_321, "Write routed Experience");
        Eq(record.Level, 42, "Write routed Level");
        Eq(record.SkillPoints, 44, "Write routed SkillPoints");
        Eq(record.AC, 55, "Write routed AC");
        Eq(record.Weapon, 23, "Write routed Weapon");
        Eq(record.Armor, 35, "Write routed Armor");
        Eq(record.Disease, 3, "Write routed Disease");
        Eq(record.Sex, 1, "Write routed Sex");
        Eq(record.Nationality, 3, "Write routed Nationality");

        // The real guard: the set of fields this test dirtied must be exactly the set Write()
        // knows how to commit. Adding an editable property without a case in WriteField — or
        // without covering it here — fails this.
        var dirtied = new HashSet<string>(pendingBefore, StringComparer.Ordinal);
        var declared = new HashSet<string>(CharacterViewModel.EditableFieldNames, StringComparer.Ordinal);
        True(dirtied.SetEquals(declared),
            "Every declared editable field was exercised: " +
            $"missing from this test [{string.Join(", ", declared.Except(dirtied))}], " +
            $"not declared editable [{string.Join(", ", dirtied.Except(declared))}]");
    }

    /// <summary>
    /// Regressions for bugs found in review. Each one describes a way the trainer could lose or
    /// corrupt a player's data, so each gets a test that fails if the fix is ever undone.
    /// </summary>
    private void CheckRegressions()
    {
        Console.WriteLine();
        Console.WriteLine("--- Regressions ---");

        // 1. A readable-but-empty roster must NOT trigger the structural fallback. Scanning would
        //    be slow and could hand back character-creation objects that edit nothing real.
        {
            var mem = BuildEmptyRosterGame(out nuint moduleBase, out nuint moduleSize, out nuint decoy);
            var loc = GameLocator.Locate(mem, moduleBase, moduleSize);

            True(loc != null, "An empty roster still returns a location");
            True(loc is { UsedFallback: false }, "An empty roster does not fall back to a memory scan");
            Eq(loc!.CharacterCount, 0, "An empty roster yields no characters");
            True(!loc.CharacterAddresses.Contains(decoy),
                "The player-shaped decoy elsewhere in memory was not picked up");
            True(loc.Summary.Contains("no rangers yet"), "The status explains that no game is loaded");
        }

        // 2. A write that fails must leave the edit pending, not report success and discard it.
        {
            var mem = new FakeMemorySource();
            mem.Map(0x900000, new byte[0x100]);                 // somewhere else entirely
            var record = new CharacterRecord(mem, 0x500000, 0); // deliberately unmapped
            var host = new RecordingHost();
            var vm = new CharacterViewModel(record, host);

            vm.Strength = 50;
            True(vm.HasPendingEdits, "The edit is pending before the write");
            vm.Write();
            True(vm.HasPendingEdits, "A failed write keeps the edit pending");
            Eq(vm.PendingCount, 1, "The unwritten field is still counted");
            True(host.Messages[^1].Contains("still pending"), "The status says the edit was not written");

            // ...and a write that succeeds still clears it.
            var (_, good, _, _) = BuildPlayerWithArrays();
            var vm2 = new CharacterViewModel(good, new RecordingHost());
            vm2.Strength = 50;
            vm2.Write();
            True(!vm2.HasPendingEdits, "A successful write clears the pending edit");
            Eq(good.GetAttribute(0), 50, "A successful write reached the record");
        }

        // 3. Setting an inventory row to "(empty)" must remove the item and close the gap, not
        //    write a 0x00 terminator mid-pack and orphan everything behind it.
        {
            var (mem, record, _, items) = BuildPlayerWithArrays();
            WriteSlot(mem, items, 0, 23, 10);   // AK-97
            WriteSlot(mem, items, 1, 4, 1);     // Knife
            WriteSlot(mem, items, 2, 34, 5);    // Power pack

            var host = new RecordingHost();
            var vm = new CharacterViewModel(record, host);
            Eq(vm.Items.Count, 3, "Three items before the removal");

            vm.Items[1].ItemId = 0;             // user picks "(empty)" on the middle row

            var after = record.ReadItems();
            Eq(after.Count, 2, "Choosing (empty) removed exactly one item");
            Eq(after[0].Id, 23, "The item before the removed one is untouched");
            Eq(after[1].Id, 34, "The item after it moved up instead of being orphaned");
            True(host.RefreshCount > 0, "The host was asked to refresh the reshaped list");
        }

        // 4. A stored value outside a drop-down's range must not mark the character edited just
        //    by being selected — and must never be silently rewritten to 0.
        {
            var (_, record, _, _) = BuildPlayerWithArrays();
            record.WriteByte(CharacterFormat.OffNationality, 7);   // outside the 5-entry table
            var vm = new CharacterViewModel(record, new RecordingHost());

            Eq(vm.Nationality, CharacterFormat.Nationalities.Length - 1,
                "An out-of-range nationality is clamped for display");
            True(!vm.HasPendingEdits, "Loading an out-of-range value does not mark the field edited");

            vm.Nationality = -1;                // what Selector coercion pushes back
            True(!vm.HasPendingEdits, "A negative write-back from the Selector is ignored");
            Eq(record.Nationality, 7, "The stored value was not rewritten");
        }

        // 5. Full Heal must supersede a half-typed CON edit, or a later Write undoes the heal.
        {
            var (_, record, _, _) = BuildPlayerWithArrays();
            record.MaxCon = 100;
            record.CurCon = 10;
            var vm = new CharacterViewModel(record, new RecordingHost());

            vm.CurCon = 1;                       // mid-typing "100"
            vm.FullHealCommand.Execute(null);
            Eq(record.CurCon, 100, "Full Heal reached the record");
            True(!vm.HasPendingEdits, "Full Heal discarded the superseded CON edit");
            vm.Write();
            Eq(record.CurCon, 100, "A later Write did not undo the heal");
        }

        // 6. Money can be frozen at zero — the pin is a flag, not a "greater than zero" test.
        {
            var (_, record, _, _) = BuildPlayerWithArrays();
            record.Money = 0;
            var vm = new CharacterViewModel(record, new RecordingHost());
            vm.FreezeMoney = true;
            record.Money = 5000;
            vm.ApplyFreezes();
            Eq(record.Money, 0, "Money frozen at zero is re-pinned");
        }

        // 7. An out-of-range entry in a clamped box notifies, so the UI cannot keep showing a
        //    number the view model rejected.
        {
            var (_, record, _, _) = BuildPlayerWithArrays();
            var vm = new CharacterViewModel(record, new RecordingHost());
            int notifications = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.LearnLevel)) notifications++; };

            vm.LearnLevel = GameFacts.MaxSkillLevel;      // already the default: no change
            vm.LearnLevel = 500;                          // clamps back to the same value
            Eq(vm.LearnLevel, GameFacts.MaxSkillLevel, "Learn level clamped");
            True(notifications > 0, "The clamped value was announced so the box corrects itself");
        }
    }

    /// <summary>
    /// The sweep looks only at readable, non-executable sections, and falls back to the whole
    /// module when the PE headers cannot be parsed. Both branches matter: the first is what keeps
    /// the scan to seconds, the second is what stops an unusual image making the trainer useless.
    /// </summary>
    private void CheckPeSectionSweep()
    {
        Console.WriteLine();
        Console.WriteLine("--- PE-aware class sweep ---");

        const uint MemRead = 0x40000000, MemExecute = 0x20000000;

        var classes = SweepFixture(validPe: true, characteristics: MemRead, out nuint partyClass);
        Eq(classes.Party, partyClass, "A class pointer in a data section is found");
        True(classes.Method.Contains("data-section"), "The data-section path was taken");
        True(classes.IsValid, "IsValid is true once Party resolves");
        True(!classes.ProbeBudgetExhausted, "The probe budget was not exhausted");

        // The same pointer in an executable section must be skipped. This is what proves .text is
        // genuinely excluded rather than merely scanned later.
        classes = SweepFixture(validPe: true, characteristics: MemRead | MemExecute, out _);
        Eq(classes.Party, (nuint)0, "A pointer only in an executable section is not swept");
        Eq(classes.Method, "not found", "An all-executable image reports nothing found");

        classes = SweepFixture(validPe: true, characteristics: 0, out _);
        Eq(classes.Party, (nuint)0, "A non-readable section is not swept");

        classes = SweepFixture(validPe: false, characteristics: MemRead, out partyClass);
        Eq(classes.Party, partyClass, "Unparseable PE headers fall back to a full-module sweep");
        True(classes.Method.Contains("module sweep") && !classes.Method.Contains("data-section"),
            "The fallback reports a plain module sweep");

        var empty = Il2CppClassLocator.Resolve(new FakeMemorySource(), 0, 0);
        Eq(empty.Party, (nuint)0, "A missing module resolves nothing");
        True(!empty.IsValid, "IsValid is false with no Party class");
    }

    /// <summary>Heap with one Party class, plus a module whose single section points at it.</summary>
    private static GameClasses SweepFixture(bool validPe, uint characteristics, out nuint partyClass)
    {
        var mem = new FakeMemorySource();

        const nuint heapBase = 0x200000;
        var heap = new byte[0x2000];
        int strParty = 0x100, strEmpty = 0x110, klass = 0x1000;
        "Party"u8.CopyTo(heap.AsSpan(strParty));
        heap[strEmpty] = 0;
        WriteI64(heap, klass + Il2Cpp.ClassNameOffset, heapBase + (nuint)strParty);
        WriteI64(heap, klass + Il2Cpp.ClassNamespaceOffset, heapBase + (nuint)strEmpty);
        mem.Map(heapBase, heap);

        nuint moduleBase = 0x400000;
        nuint moduleSize = 0x2000;
        var module = new byte[moduleSize];

        if (validPe)
        {
            int peOff = 0x80;
            module[0] = (byte)'M'; module[1] = (byte)'Z';
            WriteI32(module, 0x3C, peOff);
            WriteI32(module, peOff, 0x00004550);
            WriteI16(module, peOff + 4 + 2, 1);
            WriteI16(module, peOff + 4 + 16, 0xF0);

            int sectionTable = peOff + 4 + 20 + 0xF0;
            ".data"u8.CopyTo(module.AsSpan(sectionTable));
            WriteI32(module, sectionTable + 8, 0x100);
            WriteI32(module, sectionTable + 12, 0x1000);
            WriteI32(module, sectionTable + 36, unchecked((int)characteristics));
        }
        // else: the header stays zeroed, the MZ check fails, and the whole module is swept.

        WriteI64(module, 0x1000 + 0x40, heapBase + (nuint)klass);
        mem.Map(moduleBase, module);

        partyClass = heapBase + (nuint)klass;
        return Il2CppClassLocator.Resolve(mem, moduleBase, moduleSize);
    }

    /// <summary>
    /// The party-position block is reached through an unverified singleton chain, so what the
    /// harness can prove is that the chain is walked correctly and that a missing link yields
    /// null rather than a bogus reading presented as fact.
    /// </summary>
    private void CheckPartyStateReader()
    {
        Console.WriteLine();
        Console.WriteLine("--- PartyStateReader ---");

        const nuint heapBase = 0x300000;
        var heap = new byte[0x2000];
        int managerClass = 0x100, statics = 0x200, manager = 0x300, save = 0x400;

        WriteI64(heap, managerClass + Il2Cpp.ClassStaticFieldsOffset, heapBase + (nuint)statics);
        WriteI64(heap, statics + CharacterFormat.PartyManagerInstanceStatic, heapBase + (nuint)manager);
        WriteI64(heap, manager + CharacterFormat.PartyManagerSaveData, heapBase + (nuint)save);
        heap[save + CharacterFormat.CoreSaveMapX] = 12;
        heap[save + CharacterFormat.CoreSaveMapY] = 34;
        heap[save + CharacterFormat.CoreSaveCurrentMap] = 5;
        heap[save + CharacterFormat.CoreSaveNumberInParty] = 4;
        WriteI32(heap, save + CharacterFormat.CoreSaveClock, 9999);

        var mem = new FakeMemorySource();
        mem.Map(heapBase, heap);

        var viaManager = new GameClasses { PartyManager = heapBase + (nuint)managerClass };
        var state = PartyStateReader.Read(mem, viaManager);
        True(state != null, "Read follows PartyManager.m_instance to the save block");
        if (state != null)
        {
            Eq(state.MapX, 12, "MapX");
            Eq(state.MapY, 34, "MapY");
            Eq(state.CurrentMap, 5, "CurrentMap");
            Eq(state.NumberInParty, 4, "NumberInParty");
            Eq(state.Clock, 9999, "Clock");
            Eq(state.PositionText, "map 5 at (12, 34)", "PositionText");
            Eq(state.PartyText, "4 rangers", "PartyText pluralises");
            Eq(new PartyState(0, 0, 0, 1, 0).PartyText, "1 ranger", "PartyText is singular for one");
        }

        // The Wasteland singleton is the alternate route to the same block.
        int gameClass = 0x600, gameStatics = 0x700, gameObj = 0x800;
        WriteI64(heap, gameClass + Il2Cpp.ClassStaticFieldsOffset, heapBase + (nuint)gameStatics);
        WriteI64(heap, gameStatics + CharacterFormat.WastelandInstanceStatic, heapBase + (nuint)gameObj);
        WriteI64(heap, gameObj + CharacterFormat.WastelandPartyManager, heapBase + (nuint)manager);

        var viaGame = new GameClasses { Wasteland = heapBase + (nuint)gameClass };
        True(PartyStateReader.Read(mem, viaGame) != null, "Read falls back through the Wasteland singleton");

        True(PartyStateReader.Read(mem, new GameClasses()) == null, "No classes resolved yields null");
        WriteI64(heap, manager + CharacterFormat.PartyManagerSaveData, 0);
        True(PartyStateReader.Read(mem, viaManager) == null, "A null save-data pointer yields null");
    }

    /// <summary>A synthetic game whose Party exists but whose roster is empty, plus a decoy.</summary>
    private static FakeMemorySource BuildEmptyRosterGame(
        out nuint moduleBase, out nuint moduleSize, out nuint decoy)
    {
        var mem = new FakeMemorySource();

        const nuint heapBase = 0x200000;
        var heap = new byte[0x4000];
        int strParty = 0x100, strEmpty = 0x110;
        int partyClass = 0x1000, statics = 0x1200, partyObj = 0x1400, list = 0x1600, items = 0x1800;
        int decoyOff = 0x2000;

        "Party"u8.CopyTo(heap.AsSpan(strParty));
        heap[strEmpty] = 0;
        WriteI64(heap, partyClass + Il2Cpp.ClassNameOffset, heapBase + (nuint)strParty);
        WriteI64(heap, partyClass + Il2Cpp.ClassNamespaceOffset, heapBase + (nuint)strEmpty);
        WriteI64(heap, partyClass + Il2Cpp.ClassStaticFieldsOffset, heapBase + (nuint)statics);
        WriteI64(heap, statics + CharacterFormat.PartyInstanceStatic, heapBase + (nuint)partyObj);
        WriteI64(heap, partyObj + CharacterFormat.PartyPlayers, heapBase + (nuint)list);
        WriteI64(heap, list + Il2Cpp.ListItemsOffset, heapBase + (nuint)items);
        WriteI32(heap, list + Il2Cpp.ListSizeOffset, 0);          // readable, and empty
        WriteI32(heap, items + Il2Cpp.ArrayLengthOffset, 0);

        // A perfectly player-shaped object that a structural scan would happily return.
        ValidPlayerBuffer().CopyTo(heap.AsSpan(decoyOff));
        mem.Map(heapBase, heap);

        moduleBase = 0x400000;
        moduleSize = 0x2000;
        var module = new byte[moduleSize];
        module[0] = (byte)'M'; module[1] = (byte)'Z';
        int peOff = 0x80;
        WriteI32(module, 0x3C, peOff);
        WriteI32(module, peOff, 0x00004550);
        WriteI16(module, peOff + 4 + 2, 1);
        WriteI16(module, peOff + 4 + 16, 0xF0);
        int sectionTable = peOff + 4 + 20 + 0xF0;
        ".data"u8.CopyTo(module.AsSpan(sectionTable));
        WriteI32(module, sectionTable + 8, 0x100);
        WriteI32(module, sectionTable + 12, 0x1000);
        WriteI32(module, sectionTable + 36, unchecked((int)0x40000000));
        WriteI64(module, 0x1000 + 0x40, heapBase + (nuint)partyClass);
        mem.Map(moduleBase, module);

        decoy = heapBase + (nuint)decoyOff;
        return mem;
    }

    // =========================================================================
    // Fixtures
    // =========================================================================
    private static byte[] ValidPlayerBuffer()
    {
        var buf = new byte[CharacterFormat.ProbeSize];
        WriteI32(buf, CharacterFormat.OffMaxCon, 50);
        WriteI32(buf, CharacterFormat.OffCurCon, 45);
        buf[CharacterFormat.OffLevel] = 3;
        WriteI32(buf, CharacterFormat.OffMoney, 500);
        WriteI32(buf, CharacterFormat.OffExperience, 1000);
        for (int i = 0; i < 7; i++) buf[CharacterFormat.OffStrength + i] = 15;
        buf[CharacterFormat.OffSex] = 0;
        return buf;
    }

    /// <summary>A single mapped Player object with real SKILLS and ITEMS byte arrays behind it.</summary>
    private static (FakeMemorySource Mem, CharacterRecord Record, nuint Skills, nuint Items)
        BuildPlayerWithArrays(int skillArrayBytes = GameFacts.SkillSlots * 2,
                              int itemArrayBytes = GameFacts.ItemSlots * 2)
    {
        var mem = new FakeMemorySource();

        nuint playerAddr = 0x100000;
        nuint skillsAddr = 0x110000;
        nuint itemsAddr = 0x120000;

        var player = new byte[0x200];
        ValidPlayerBuffer().CopyTo(player.AsSpan(0));
        WriteI64(player, CharacterFormat.OffSkills, skillsAddr);
        WriteI64(player, CharacterFormat.OffItems, itemsAddr);
        mem.Map(playerAddr, player);

        var skills = new byte[Il2Cpp.ArrayHeaderSize + skillArrayBytes];
        WriteI32(skills, Il2Cpp.ArrayLengthOffset, skillArrayBytes);
        mem.Map(skillsAddr, skills);

        var items = new byte[Il2Cpp.ArrayHeaderSize + itemArrayBytes];
        WriteI32(items, Il2Cpp.ArrayLengthOffset, itemArrayBytes);
        mem.Map(itemsAddr, items);

        return (mem, new CharacterRecord(mem, playerAddr, 0), skillsAddr, itemsAddr);
    }

    private static void WriteSlot(FakeMemorySource mem, nuint array, int slot, int id, int value)
    {
        mem.WriteByteArrayElement(array, slot * CharacterFormat.SlotSize, (byte)id);
        mem.WriteByteArrayElement(array, slot * CharacterFormat.SlotSize + 1, (byte)value);
    }

    /// <summary>
    /// Builds a synthetic IL2CPP image: a PE-headered module whose single data section holds the
    /// Party class pointer, and a heap holding the class structures, the Party singleton, its
    /// List&lt;Player&gt; and the Player objects.
    /// </summary>
    private static FakeMemorySource BuildSyntheticGame(
        out nuint moduleBase, out nuint moduleSize,
        out nuint partyClass, out nuint playerClass, out nuint[] players, out nuint imposter)
    {
        var mem = new FakeMemorySource();

        const nuint heapBase = 0x200000;
        var heap = new byte[0x8000];

        // --- strings ---
        int strParty = 0x100, strEmpty = 0x110, strPlayer = 0x120, strOther = 0x130;
        "Party"u8.CopyTo(heap.AsSpan(strParty));
        heap[strEmpty] = 0;
        "Player"u8.CopyTo(heap.AsSpan(strPlayer));
        "Widget"u8.CopyTo(heap.AsSpan(strOther));

        // --- classes ---
        int partyClassOff = 0x1000, playerClassOff = 0x1A00, otherClassOff = 0x1C00;
        int staticsOff = 0x1200, partyObjOff = 0x1400, listOff = 0x1600, itemsOff = 0x1800;

        WriteI64(heap, partyClassOff + Il2Cpp.ClassNameOffset, heapBase + (nuint)strParty);
        WriteI64(heap, partyClassOff + Il2Cpp.ClassNamespaceOffset, heapBase + (nuint)strEmpty);
        WriteI64(heap, partyClassOff + Il2Cpp.ClassStaticFieldsOffset, heapBase + (nuint)staticsOff);

        WriteI64(heap, playerClassOff + Il2Cpp.ClassNameOffset, heapBase + (nuint)strPlayer);
        WriteI64(heap, playerClassOff + Il2Cpp.ClassNamespaceOffset, heapBase + (nuint)strEmpty);

        WriteI64(heap, otherClassOff + Il2Cpp.ClassNameOffset, heapBase + (nuint)strOther);
        WriteI64(heap, otherClassOff + Il2Cpp.ClassNamespaceOffset, heapBase + (nuint)strEmpty);

        // statics[0] = the Party singleton
        WriteI64(heap, staticsOff + CharacterFormat.PartyInstanceStatic, heapBase + (nuint)partyObjOff);

        // Party.players -> List<Player>
        WriteI64(heap, partyObjOff + Il2Cpp.ObjectClassOffset, heapBase + (nuint)partyClassOff);
        WriteI64(heap, partyObjOff + CharacterFormat.PartyPlayers, heapBase + (nuint)listOff);

        // The list holds four entries: three rangers and one object of the wrong type.
        WriteI64(heap, listOff + Il2Cpp.ListItemsOffset, heapBase + (nuint)itemsOff);
        WriteI32(heap, listOff + Il2Cpp.ListSizeOffset, 4);
        WriteI32(heap, itemsOff + Il2Cpp.ArrayLengthOffset, 4);

        int[] playerOffsets = { 0x2000, 0x2200, 0x2400 };
        int imposterOff = 0x2600;

        for (int i = 0; i < playerOffsets.Length; i++)
        {
            int at = playerOffsets[i];
            ValidPlayerBuffer().CopyTo(heap.AsSpan(at));
            WriteI64(heap, at + Il2Cpp.ObjectClassOffset, heapBase + (nuint)playerClassOff);
            WriteI64(heap, itemsOff + Il2Cpp.ArrayHeaderSize + i * 8, heapBase + (nuint)at);
        }

        // Ranger 2 is dying: negative CON. Ranger 3 has been maxed past the natural range.
        WriteI32(heap, playerOffsets[1] + CharacterFormat.OffCurCon, -5);
        heap[playerOffsets[2] + CharacterFormat.OffStrength] = 120;

        // The imposter carries a different class pointer and must be rejected.
        ValidPlayerBuffer().CopyTo(heap.AsSpan(imposterOff));
        WriteI64(heap, imposterOff + Il2Cpp.ObjectClassOffset, heapBase + (nuint)otherClassOff);
        WriteI64(heap, itemsOff + Il2Cpp.ArrayHeaderSize + 3 * 8, heapBase + (nuint)imposterOff);

        mem.Map(heapBase, heap);

        // --- module with PE headers and one data section ---
        moduleBase = 0x400000;
        moduleSize = 0x2000;
        var module = new byte[moduleSize];

        module[0] = (byte)'M'; module[1] = (byte)'Z';
        int peOff = 0x80;
        WriteI32(module, 0x3C, peOff);
        WriteI32(module, peOff, 0x00004550);                 // "PE\0\0"
        WriteI16(module, peOff + 4 + 2, 1);                  // NumberOfSections
        WriteI16(module, peOff + 4 + 16, 0xF0);              // SizeOfOptionalHeader

        int sectionTable = peOff + 4 + 20 + 0xF0;
        ".data"u8.CopyTo(module.AsSpan(sectionTable));
        WriteI32(module, sectionTable + 8, 0x100);           // VirtualSize
        WriteI32(module, sectionTable + 12, 0x1000);         // VirtualAddress
        WriteI32(module, sectionTable + 36, unchecked((int)0x40000000));   // MEM_READ, not executable

        // The class pointer the sweep is looking for, sitting in the data section.
        WriteI64(module, 0x1000 + 0x40, heapBase + (nuint)partyClassOff);
        mem.Map(moduleBase, module);

        partyClass = heapBase + (nuint)partyClassOff;
        playerClass = heapBase + (nuint)playerClassOff;
        players = playerOffsets.Select(o => heapBase + (nuint)o).ToArray();
        imposter = heapBase + (nuint)imposterOff;
        return mem;
    }

    // =========================================================================
    private static void WriteI16(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteI32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteI64(byte[] buf, int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
            buf[offset + i] = (byte)((value >> (i * 8)) & 0xFF);
    }

    private static void WriteI64(byte[] buf, int offset, nuint value) => WriteI64(buf, offset, (ulong)value);
}

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Wasteland Remastered Trainer — FormatCheck");
        Console.WriteLine("==========================================");
        new Checker().Run();
    }
}
