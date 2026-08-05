using TheQuestTrainer.Game;
using TheQuestTrainer.Memory;
using TheQuestTrainer.ViewModels;

namespace TheQuestTrainer.FormatCheck;

/// <summary>
/// Verification harness. Needs no game, no save files and no copyrighted data: every check runs
/// against a synthetic address space built by <see cref="FakeGame"/>.
///
/// The interesting cases are the ones a live game cannot be asked to produce — a relocated module, a
/// stale static slot, the new-character prototype sitting next to the live record, an unreadable
/// page in the middle of the heap, a record whose vtable points at writable memory — so those are
/// what most of this file is.
/// </summary>
internal static class Program
{
    private static int _passed;
    private static readonly List<string> _failures = new();

    private static int Main()
    {
        LayoutChecks();
        TableChecks();
        PeChecks();
        StdStringChecks();
        LocatorChecks();
        ReaderChecks();
        ActionChecks();
        LevelChecks();
        FreezeChecks();
        ItemLayoutChecks();
        ItemTableChecks();
        ItemTypeChecks();
        CatalogChecks();
        InventoryChecks();
        ItemActionChecks();
        PickerChecks();

        Console.WriteLine();
        Console.WriteLine($"{_passed} checks passed, {_failures.Count} failed.");
        foreach (string f in _failures) Console.WriteLine($"  FAIL  {f}");
        return _failures.Count == 0 ? 0 : 1;
    }

    // ---- checks -------------------------------------------------------------------------------

    private static void LayoutChecks()
    {
        Section("layout");

        // Each offset is restated from the one before it, so a transcription slip fails here rather
        // than reading a neighbouring field in the game.
        Check("name follows the record header", QuestLayout.Name == 0x014);
        Check("portrait follows the name", QuestLayout.PortraitId == 0x02C);
        Check("health/mana/level/experience are contiguous words",
            QuestLayout.Health == 0x046 && QuestLayout.Mana == 0x048 &&
            QuestLayout.Level == 0x04A && QuestLayout.Experience == 0x04C);
        Check("next-level threshold sits at +0x58", QuestLayout.ExperienceForNextLevel == 0x058);
        Check("experience table starts at +0x64", QuestLayout.ExperienceTable == 0x064);
        Check("gold follows the experience table", QuestLayout.Gold == 0x1F0);
        Check("attributes follow gold", QuestLayout.BaseAttributes == 0x1F4);
        Check("attribute points follow the attribute array", QuestLayout.AttributePoints == 0x200);
        Check("skill display order follows the raise allowance", QuestLayout.SkillDisplayOrder == 0x20E);
        Check("skill points follow the display order", QuestLayout.SkillPoints == 0x222);
        Check("starting skills follow the skill points", QuestLayout.StartingSkills == 0x224);
        Check("base skills follow the starting skills", QuestLayout.BaseSkills == 0x24E);
        Check("fame and crime sit at +0x3D0/+0x3D4", QuestLayout.Fame == 0x3D0 && QuestLayout.Crime == 0x3D4);
        Check("race follows crime", QuestLayout.Race == 0x3D8);
        Check("the snapshot covers every field", QuestLayout.RecordBytes > QuestLayout.Race + 4);
        Check("the record fits inside the engine object", QuestLayout.RecordInEngine == 0x3DC8);

        Check("attribute addressing lands on the array",
            QuestLayout.Attribute(0x1000, 1) == 0x1000 + QuestLayout.BaseAttributes + 2);
        Check("skill addressing lands on the array",
            QuestLayout.Skill(0x1000, 20) == 0x1000 + QuestLayout.BaseSkills + 40);
        Check("experience-table addressing strides by four",
            QuestLayout.ExperienceTableEntry(0x1000, 3) == 0x1000 + QuestLayout.ExperienceTable + 12);

        Check("the two skill arrays do not overlap",
            QuestLayout.StartingSkills + GameFacts.SkillSlots * 2 == QuestLayout.BaseSkills);
        Check("the base skill array ends before fame",
            QuestLayout.BaseSkills + GameFacts.SkillSlots * 2 <= QuestLayout.Fame);
    }

    private static void TableChecks()
    {
        Section("tables");

        Check("five attributes with ids 1..5",
            GameTables.Attributes.Count == 5 &&
            GameTables.Attributes.Select((a, i) => a.Id == i + 1).All(x => x));
        Check("twenty skills with ids 1..20",
            GameTables.Skills.Count == 20 &&
            GameTables.Skills.Select((s, i) => s.Id == i + 1).All(x => x));
        Check("every skill names a real governing attribute",
            GameTables.Skills.All(s => GameTables.Attribute(s.GoverningAttribute) is not null));
        Check("skill ids fit the array the game allocates",
            GameTables.Skills.Max(s => s.Id) < GameFacts.SkillSlots);
        Check("attribute ids fit the array the game allocates",
            GameTables.Attributes.Max(a => a.Id) < GameFacts.AttributeSlots);
        Check("skill lookup is by id, not position", GameTables.Skill(11)?.Name == "Mind Magic");
        Check("Mind Magic is governed by Personality, not Intelligence",
            GameTables.Skill(11)?.GoverningAttribute == 5);
        Check("Heavy Weapon is the one Strength skill",
            GameTables.Skills.Where(s => s.GoverningAttribute == 1).Select(s => s.Name).SequenceEqual(new[] { "Heavy Weapon" }));

        Check("six race ids", GameTables.Races.Count == 6);
        Check("race 4 is Derth", GameTables.RaceName(4) == "Derth");
        Check("an unknown race id does not throw", GameTables.RaceName(99).StartsWith("Unknown"));

        // The fame ladder is asymmetric at the ends: only +100 is Saint and only -100 is Demonic.
        Check("fame +100 is Saint", GameTables.FameBand(100) == "Saint");
        Check("fame +99 is Blessed", GameTables.FameBand(99) == "Blessed");
        Check("fame +50 is Blameless", GameTables.FameBand(50) == "Blameless");
        Check("fame +20 is Virtuous", GameTables.FameBand(20) == "Virtuous");
        Check("fame +1 is Good", GameTables.FameBand(1) == "Good");
        Check("fame 0 is Neutral", GameTables.FameBand(0) == "Neutral");
        Check("fame -1 is Immoral", GameTables.FameBand(-1) == "Immoral");
        Check("fame -20 is Corrupt", GameTables.FameBand(-20) == "Corrupt");
        Check("fame -50 is Evil", GameTables.FameBand(-50) == "Evil");
        Check("fame -80 is Pure evil", GameTables.FameBand(-80) == "Pure evil");
        Check("fame -100 is Demonic", GameTables.FameBand(-100) == "Demonic");

        Check("outfit 6 is Threadbare", GameTables.OutfitBand(6) == "Threadbare");
        Check("outfit 91 is Fashionable", GameTables.OutfitBand(91) == "Fashionable");
        Check("outfit 96 is Swell", GameTables.OutfitBand(96) == "Swell");

        Check("the experience table covers levels 2..MaxLevel",
            GameFacts.ExperienceTableEntries == GameFacts.MaxLevel - 1);
        Check("the signature is eight ascending entries",
            GameTables.ExperienceSignature.Count == 8 &&
            GameTables.ExperienceSignature.Zip(GameTables.ExperienceSignature.Skip(1)).All(p => p.First < p.Second));
        Check("the signature is 32 bytes", CharacterLocator.SignatureBytes().Length == 32);
        Check("the signature bytes are little-endian",
            BitConverter.ToUInt32(CharacterLocator.SignatureBytes(), 0) == 400);

        Check("the skill cap is twice the governing attribute", GameFacts.SkillCapFor(23) == 46);
        Check("the skill cap never exceeds the trainer's own ceiling",
            GameFacts.SkillCapFor(10_000) == GameFacts.MaxAttributeOrSkill);
    }

    private static void PeChecks()
    {
        Section("PE header");

        var mem = FakeGame.BuildGame();
        var image = FakeGame.Image(mem);

        Check("parses as 32-bit x86", image.IsWin32X86);
        Check("is an executable, not a DLL", !image.IsDll);
        Check("reports ASLR", image.HasAslr);
        Check("preferred base is not where it is mapped", image.ImageBase != mem.ModuleBase);
        Check("build stamp matches the documented one", image.TimeDateStamp == GameFacts.KnownTimeDateStamp);
        Check("three sections", image.Sections.Count == 3);
        Check(".data is writable", image.IsWritableDataRva(QuestLayout.EngineSlotRva));
        Check(".rdata is not writable", !image.IsWritableDataRva(FakeGame.VTableRva));
        Check(".rdata counts as read-only data", image.IsReadOnlyDataRva(FakeGame.VTableRva));
        Check(".data does not count as read-only data", !image.IsReadOnlyDataRva(QuestLayout.EngineSlotRva));
        Check("an RVA past the image is in no section", !image.IsWritableDataRva(0x00FF_FFFF));

        Check("a truncated header is refused", PeImage.Parse(new byte[16]) is null);
        Check("a non-MZ page is refused", PeImage.Parse(new byte[PeImage.HeaderBytes]) is null);

        var bad = FakeGame.BuildHeader();
        BitConverter.GetBytes((ushort)0x8664).CopyTo(bad, 0x80 + 4);    // x86-64
        Check("a 64-bit machine type is reported as not x86", PeImage.Parse(bad)?.IsWin32X86 == false);
    }

    private static void StdStringChecks()
    {
        Section("std::string");

        var mem = FakeGame.BuildGame();
        var record = new byte[QuestLayout.RecordBytes];
        mem.Read(FakeGame.LiveRecord, record, record.Length);

        Check("reads an inline name", StdString.Read(mem, record, (int)QuestLayout.Name) == "Gerth the Derth");
        Check("a 15-character name is still inline", "Gerth the Derth".Length == StdString.InlineCapacity);

        // The portrait id is 21 characters, so the union holds a pointer rather than characters.
        Check("reads a spilled string through its pointer",
            StdString.Read(mem, record, (int)QuestLayout.PortraitId) == FakeGame.PortraitValue);

        // The same record with the heap buffer unmapped must fail rather than invent characters.
        mem.Unmap(FakeGame.PortraitHeap);
        Check("an unreadable spilled buffer reads as null",
            StdString.Read(mem, record, (int)QuestLayout.PortraitId) is null);
        Check("and the record then fails validation",
            !CharacterLocator.Validate(mem, FakeGame.Image(mem), FakeGame.LiveRecord, out _));

        var broken = new byte[StdString.Bytes];
        BitConverter.GetBytes(5u).CopyTo(broken, 16);
        BitConverter.GetBytes(3u).CopyTo(broken, 20);       // capacity below the inline minimum
        Check("capacity under 15 is refused", StdString.Read(mem, broken, 0) is null);

        BitConverter.GetBytes(99u).CopyTo(broken, 16);
        BitConverter.GetBytes(15u).CopyTo(broken, 20);      // size beyond capacity
        Check("size beyond capacity is refused", StdString.Read(mem, broken, 0) is null);

        var unterminated = new byte[StdString.Bytes];
        for (int i = 0; i < 16; i++) unterminated[i] = (byte)'x';
        BitConverter.GetBytes(4u).CopyTo(unterminated, 16);
        BitConverter.GetBytes(15u).CopyTo(unterminated, 20);
        Check("an inline value with no terminator is refused", StdString.Read(mem, unterminated, 0) is null);

        Check("an empty name is well-formed but not 'non-empty'",
            StdString.IsPlausible(mem, MakeEmptyString(), 0, requireNonEmpty: false) &&
            !StdString.IsPlausible(mem, MakeEmptyString(), 0, requireNonEmpty: true));

        Check("a reader offset past the buffer is refused",
            StdString.Read(mem, new byte[8], 0) is null);

        // The name is free text the player types in a localised commercial game, so a Latin-1
        // accented character must not cost them their character.
        var accented = FakeGame.BuildGame(b => b.Name("Grünwald"));
        var accentedRecord = new byte[QuestLayout.RecordBytes];
        accented.Read(FakeGame.LiveRecord, accentedRecord, accentedRecord.Length);
        Check("an accented name reads back intact",
            StdString.Read(accented, accentedRecord, (int)QuestLayout.Name) == "Grünwald");
        Check("an accented name is plausible",
            StdString.IsPlausible(accented, accentedRecord, (int)QuestLayout.Name, requireNonEmpty: true));
        Check("and the record still validates",
            CharacterLocator.Validate(accented, FakeGame.Image(accented), FakeGame.LiveRecord, out _));

        var controlChars = FakeGame.BuildGame(b => b.Name("bad" + (char)1 + "name"));
        var controlRecord = new byte[QuestLayout.RecordBytes];
        controlChars.Read(FakeGame.LiveRecord, controlRecord, controlRecord.Length);
        Check("a name containing a control character is not plausible",
            !StdString.IsPlausible(controlChars, controlRecord, (int)QuestLayout.Name, requireNonEmpty: true));
        Check("and such a record does not validate",
            !CharacterLocator.Validate(controlChars, FakeGame.Image(controlChars), FakeGame.LiveRecord, out _));

        // Both edges of what counts as a control character, so a future narrowing is caught. The
        // upper edge is the one that matters: the check used to be "printable ASCII", and nothing
        // else here would notice if it went back to that.
        Check("0x1F is still a control character", !NamePlausible((char)0x1F));
        Check("0x7F (DEL) is still a control character", !NamePlausible((char)0x7F));
        Check("0x20 (space) is not", NamePlausible(' '));
        Check("a C1 byte is deliberately allowed", NamePlausible((char)0x9F));
        Check("a high Latin-1 letter is allowed", NamePlausible('ÿ'));
    }

    /// <summary>Whether a name built around <paramref name="c"/> passes the locator's name check.</summary>
    private static bool NamePlausible(char c)
    {
        var mem = FakeGame.BuildGame(b => b.Name("ab" + c + "cd"));
        var record = new byte[QuestLayout.RecordBytes];
        mem.Read(FakeGame.LiveRecord, record, record.Length);
        return StdString.IsPlausible(mem, record, (int)QuestLayout.Name, requireNonEmpty: true);
    }

    private static byte[] MakeEmptyString()
    {
        var s = new byte[StdString.Bytes];
        BitConverter.GetBytes(0u).CopyTo(s, 16);
        BitConverter.GetBytes(15u).CopyTo(s, 20);
        return s;
    }

    private static void LocatorChecks()
    {
        Section("locator");

        var mem = FakeGame.BuildGame();
        var image = FakeGame.Image(mem);

        var viaSlot = CharacterLocator.LocateViaStaticSlot(mem, image);
        Check("chain A finds the record", viaSlot.Found && viaSlot.Record == FakeGame.LiveRecord);
        Check("chain A says so", viaSlot.Chain == LocateChain.StaticSlot);

        var viaScan = CharacterLocator.LocateViaHeapScan(mem, image);
        Check("chain B finds the same record", viaScan.Found && viaScan.Record == FakeGame.LiveRecord);
        Check("chain B says so", viaScan.Chain == LocateChain.HeapScan);
        Check("chain B rejects the new-character prototype", viaScan.Candidates == 1);

        Check("both chains agree", viaSlot.Record == viaScan.Record);
        Check("the combined entry point finds it", CharacterLocator.Locate(mem, image).Found);

        // The prototype on its own must never validate.
        Check("the prototype fails validation",
            !CharacterLocator.Validate(mem, image, FakeGame.PrototypeRecord, out _));

        // A stale slot: the pointer is there but points at nothing.
        var stale = FakeGame.BuildGame();
        stale.PokeUInt32(stale.ModuleBase + QuestLayout.EngineSlotRva, 0x7000_0000);
        Check("a stale static slot is rejected", !CharacterLocator.LocateViaStaticSlot(stale, image).Found);
        Check("the sweep still finds the record when the slot is stale",
            CharacterLocator.Locate(stale, image).Chain == LocateChain.HeapScan);

        // An empty slot: no game loaded.
        var empty = FakeGame.BuildGame();
        empty.PokeUInt32(empty.ModuleBase + QuestLayout.EngineSlotRva, 0);
        Check("an empty static slot is reported, not followed",
            !CharacterLocator.LocateViaStaticSlot(empty, image).Found);

        // A build whose .data does not cover the slot must not have its slot read at all.
        var moved = FakeGame.BuildGame();
        var header = FakeGame.BuildHeader();
        int dataSection = 0x80 + 4 + 20 + 0xE0 + 80;
        BitConverter.GetBytes(0x0000_1000u).CopyTo(header, dataSection + 8);   // shrink .data to nothing useful
        moved.Map(moved.ModuleBase, header);
        var movedImage = PeImage.Parse(header)!;
        Check("the slot is skipped when its RVA is outside writable data",
            !CharacterLocator.LocateViaStaticSlot(moved, movedImage).Found);
        Check("the sweep is unaffected by the header change",
            CharacterLocator.LocateViaHeapScan(moved, movedImage).Found);

        // Validation individually.
        var noVtable = FakeGame.BuildGame();
        noVtable.PokeUInt32(FakeGame.LiveRecord + QuestLayout.VTable, 0x1234_5678);
        Check("a record whose first dword is not in the module is rejected",
            !CharacterLocator.Validate(noVtable, image, FakeGame.LiveRecord, out _));

        var writableVtable = FakeGame.BuildGame();
        writableVtable.PokeUInt32(FakeGame.LiveRecord + QuestLayout.VTable, FakeGame.ModuleBase + QuestLayout.EngineSlotRva);
        Check("a vtable pointing into writable data is rejected",
            !CharacterLocator.Validate(writableVtable, image, FakeGame.LiveRecord, out _));

        var badLevel = FakeGame.BuildGame(b => b.Level(0));
        Check("level 0 is rejected", !CharacterLocator.Validate(badLevel, image, FakeGame.LiveRecord, out _));

        var hugeLevel = FakeGame.BuildGame(b => b.Level(GameFacts.MaxLevel + 1));
        Check("a level past the table is rejected", !CharacterLocator.Validate(hugeLevel, image, FakeGame.LiveRecord, out _));

        var zeroAttribute = FakeGame.BuildGame(b => b.Attribute(3, 0));
        Check("a zero attribute is rejected", !CharacterLocator.Validate(zeroAttribute, image, FakeGame.LiveRecord, out _));

        var badRace = FakeGame.BuildGame(b => b.Race(9));
        Check("an unknown race id is rejected", !CharacterLocator.Validate(badRace, image, FakeGame.LiveRecord, out _));

        var brokenTable = FakeGame.BuildGame(b => b.BreakExperienceTable());
        Check("a record without the experience table is rejected",
            !CharacterLocator.Validate(brokenTable, image, FakeGame.LiveRecord, out _));
        Check("and the sweep then finds nothing but the prototype's table",
            !CharacterLocator.LocateViaHeapScan(brokenTable, image).Found);

        Check("an unaligned address is rejected", !CharacterLocator.Validate(mem, image, FakeGame.LiveRecord + 1, out _));
        Check("a null address is rejected", !CharacterLocator.Validate(mem, image, 0, out _));

        // An unreadable heap: everything must fail cleanly rather than throw.
        var gone = FakeGame.BuildGame();
        gone.Unmap(FakeGame.EngineAddress);
        Check("an unmapped engine object is rejected", !CharacterLocator.Validate(gone, image, FakeGame.LiveRecord, out _));
        Check("and the whole locator reports failure", !CharacterLocator.Locate(gone, image).Found);
        Check("the failure explains itself", CharacterLocator.Locate(gone, image).Detail.Length > 0);

        // Two live-looking records: the one that has played more wins.
        var twin = FakeGame.BuildGame();
        var richer = new RecordBuilder(FakeGame.ModuleBase + FakeGame.VTableRva).Experience(999_999).Level(20).Name("Rich");
        const uint twinAddress = 0x0600_0000;
        var twinBlock = new byte[QuestLayout.RecordBytes];
        richer.Bytes.CopyTo(twinBlock, 0);
        twin.Map(twinAddress, twinBlock);
        var twinResult = CharacterLocator.LocateViaHeapScan(twin, image);
        Check("two live records are both accepted", twinResult.Candidates == 2);
        Check("the more experienced one is chosen", twinResult.Record == twinAddress);

        // Validation must work with no header at all (the module list route can fail to give one).
        Check("validation still works without a parsed header",
            CharacterLocator.Validate(mem, null, FakeGame.LiveRecord, out _));
    }

    private static void ReaderChecks()
    {
        Section("reader");

        var mem = FakeGame.BuildGame();
        var snap = CharacterReader.Read(mem, FakeGame.LiveRecord);
        Check("reads a snapshot", snap is not null);
        if (snap is null) return;

        Check("name", snap.Name == "Gerth the Derth");
        Check("race id and name", snap.RaceId == 4 && snap.RaceName == "Derth");
        Check("level", snap.Level == 5);
        Check("experience", snap.Experience == 2915);
        Check("next-level threshold", snap.ExperienceForNextLevel == 4000);
        Check("health and mana", snap.Health == 72 && snap.Mana == 125);
        Check("gold", snap.Gold == 2561);
        Check("fame band", snap.Fame == 0 && snap.FameBand == "Neutral");
        Check("attribute and skill points", snap.AttributePoints == 20 && snap.SkillPoints == 40);
        Check("attributes are indexed by id", snap.Attributes[1] == 23 && snap.Attributes.Count == GameFacts.AttributeSlots);
        Check("skills are indexed by id", snap.Skills[20] == 10 && snap.Skills.Count == GameFacts.SkillSlots);
        Check("starting skills are separate from base skills", snap.StartingSkills[1] == 8 && snap.Skills[1] == 10);
        Check("the experience table came from the record", snap.ExperienceTable[0] == 400);
        Check("the table is as long as the game's", snap.ExperienceTable.Count == GameFacts.ExperienceTableEntries);

        Check("level 1 starts at zero experience", snap.ThresholdForLevel(1) == 0);
        Check("level 2 needs the first table entry", snap.ThresholdForLevel(2) == RecordBuilder.Thresholds[0]);
        Check("level 5 needs the fourth", snap.ThresholdForLevel(5) == RecordBuilder.Thresholds[3]);
        Check("a level past the table reports -1", snap.ThresholdForLevel(GameFacts.MaxLevel + 5) == -1);
        Check("this level's floor is the previous row", snap.ExperienceForThisLevel == RecordBuilder.Thresholds[3]);

        // Negative fame must survive the round trip as a signed word.
        var evil = FakeGame.BuildGame(b => b.Fame(-100));
        Check("fame is signed", CharacterReader.Read(evil, FakeGame.LiveRecord)?.Fame == -100);

        // Gold above int.MaxValue must not come back negative.
        var loaded = FakeGame.BuildGame(b => b.Gold(4_000_000_000));
        Check("gold is read unsigned", CharacterReader.Read(loaded, FakeGame.LiveRecord)?.Gold == 4_000_000_000);

        var gone = FakeGame.BuildGame();
        gone.Unmap(FakeGame.EngineAddress);
        Check("an unreadable record reads as null", CharacterReader.Read(gone, FakeGame.LiveRecord) is null);

        // A name that will not decode means the block is not a character any more. Returning a
        // snapshot with an empty name would claim the read succeeded.
        var nameless = FakeGame.BuildGame();
        nameless.PokeUInt32(FakeGame.LiveRecord + QuestLayout.Name + 20, 3);   // capacity below the inline minimum
        Check("a malformed name makes the whole read fail",
            CharacterReader.Read(nameless, FakeGame.LiveRecord) is null);

        // A missing portrait is survivable — it is decoration, not identity.
        var faceless = FakeGame.BuildGame();
        faceless.Unmap(FakeGame.PortraitHeap);
        var facelessSnap = CharacterReader.Read(faceless, FakeGame.LiveRecord);
        Check("a missing portrait still yields a snapshot", facelessSnap is not null);
        Check("with the name intact", facelessSnap?.Name == "Gerth the Derth");
    }

    private static void ActionChecks()
    {
        Section("actions");

        var mem = FakeGame.BuildGame();
        var image = FakeGame.Image(mem);
        var actions = new TrainerActions(mem, image);
        uint record = FakeGame.LiveRecord;

        // Every successful write reports the value that actually landed, so the UI can put an
        // editor back in step after a clamp instead of leaving it showing a number the game
        // never took.
        Check("a write reports what landed", actions.SetGold(record, 12345).Written == 12345);
        Check("a clamped write reports the clamped value",
            actions.SetGold(record, long.MaxValue).Written == GameFacts.MaxGold);
        Check("a clamped attribute reports the clamped value",
            actions.SetAttribute(record, 1, 9999).Written == GameFacts.MaxAttributeOrSkill);
        Check("a clamped fame reports the clamped value", actions.SetFame(record, 999).Written == 100);
        Check("a clamped level reports the clamped value",
            actions.SetLevel(record, 9999).Written == GameFacts.MaxLevel);
        Check("a refused write reports no value", new TrainerActions(mem, image) { ReadOnly = true }
            .SetGold(record, 1).Written is null);
        actions.SetLevel(record, 5);

        Check("sets gold", actions.SetGold(record, 12345).Ok && Read(mem, record)!.Gold == 12345);
        Check("clamps gold to the field's ceiling",
            actions.SetGold(record, long.MaxValue).Ok && Read(mem, record)!.Gold == GameFacts.MaxGold);
        Check("refuses to write negative gold as a huge unsigned value",
            actions.SetGold(record, -5).Ok && Read(mem, record)!.Gold == 0);
        Check("says when a value was clamped", actions.SetGold(record, long.MaxValue).Message.Contains("asked for"));

        Check("sets health", actions.SetHealth(record, 500).Ok && Read(mem, record)!.Health == 500);
        Check("sets mana", actions.SetMana(record, 999).Ok && Read(mem, record)!.Mana == 999);
        Check("clamps health to the word's range",
            actions.SetHealth(record, 1_000_000).Ok && Read(mem, record)!.Health == GameFacts.MaxHealthOrMana);

        Check("sets fame", actions.SetFame(record, -60).Ok && Read(mem, record)!.Fame == -60);
        Check("clamps fame at -100", actions.SetFame(record, -500).Ok && Read(mem, record)!.Fame == -100);
        Check("clamps fame at +100", actions.SetFame(record, 500).Ok && Read(mem, record)!.Fame == 100);
        Check("names the band it landed in", actions.SetFame(record, 100).Message.Contains("Saint"));

        Check("sets crime", actions.SetCrime(record, 700).Ok && Read(mem, record)!.Crime == 700);
        Check("clears crime", actions.SetCrime(record, 0).Ok && Read(mem, record)!.Crime == 0);

        Check("sets an attribute", actions.SetAttribute(record, 4, 60).Ok && Read(mem, record)!.Attributes[4] == 60);
        Check("clamps an attribute", actions.SetAttribute(record, 4, 9999).Ok &&
            Read(mem, record)!.Attributes[4] == GameFacts.MaxAttributeOrSkill);
        Check("refuses attribute 0", !actions.SetAttribute(record, 0, 10).Ok);
        Check("refuses attribute 6", !actions.SetAttribute(record, 6, 10).Ok);
        Check("an attribute never goes to zero", actions.SetAttribute(record, 2, 0).Ok &&
            Read(mem, record)!.Attributes[2] == GameFacts.MinAttribute);

        Check("sets a skill", actions.SetSkill(record, 11, 77).Ok && Read(mem, record)!.Skills[11] == 77);
        Check("refuses skill 0", !actions.SetSkill(record, 0, 10).Ok);
        Check("refuses skill 21", !actions.SetSkill(record, 21, 10).Ok);
        Check("a skill may be zero", actions.SetSkill(record, 11, 0).Ok && Read(mem, record)!.Skills[11] == 0);

        Check("sets points", actions.SetAttributePoints(record, 60).Ok && actions.SetSkillPoints(record, 60).Ok &&
            Read(mem, record)!.AttributePoints == 60 && Read(mem, record)!.SkillPoints == 60);

        // Read-only mode.
        var guarded = new TrainerActions(mem, image) { ReadOnly = true };
        long before = Read(mem, record)!.Gold;
        var refused = guarded.SetGold(record, 1);
        Check("read-only refuses the write", !refused.Ok && Read(mem, record)!.Gold == before);
        Check("read-only says why", refused.Message.Contains("Read-only"));

        // A record that no longer validates must refuse rather than write.
        var moved = FakeGame.BuildGame();
        var movedActions = new TrainerActions(moved, image);
        moved.PokeUInt32(FakeGame.LiveRecord + QuestLayout.VTable, 0);
        var stale = movedActions.SetGold(FakeGame.LiveRecord, 500);
        Check("a record that stopped validating refuses the write", !stale.Ok);
        Check("and tells the user to re-attach", stale.Message.Contains("Attach"));

        // Unwritable memory.
        var unwritable = FakeGame.BuildGame();
        var unwritableActions = new TrainerActions(unwritable, image);
        Check("a write outside every block fails cleanly", !unwritableActions.SetGold(0x7FFF_0000, 1).Ok);

        // Race-locked schools.
        Check("a Derth may learn Healing Magic", TrainerActions.SkillAvailableTo(8, 4));
        Check("a Derth may not learn Undead Magic", !TrainerActions.SkillAvailableTo(12, 4));
        Check("a Rasvim may learn Undead Magic", TrainerActions.SkillAvailableTo(12, 1));
        Check("a Rasvim may not learn Healing Magic", !TrainerActions.SkillAvailableTo(8, 1));

        var maxed = FakeGame.BuildGame();
        var maxActions = new TrainerActions(maxed, image);
        Check("max skills reports success", maxActions.MaxSkills(FakeGame.LiveRecord).Ok);
        var after = Read(maxed, FakeGame.LiveRecord)!;
        Check("max skills raises to twice the governing attribute", after.Skills[1] == 46);
        Check("max skills leaves the race-locked school alone", after.Skills[12] == 10);
        Check("max skills mentions the race-locked school", maxActions.MaxSkills(FakeGame.LiveRecord).Message.Contains("race-locked"));

        var high = FakeGame.BuildGame(b => b.Skill(1, 200));
        var highActions = new TrainerActions(high, image);
        highActions.MaxSkills(FakeGame.LiveRecord);
        Check("max skills never lowers a skill", Read(high, FakeGame.LiveRecord)!.Skills[1] == 200);

        var rasvim = FakeGame.BuildGame(b => b.Race(1));
        var rasvimActions = new TrainerActions(rasvim, image);
        rasvimActions.MaxSkills(FakeGame.LiveRecord);
        var rasvimAfter = Read(rasvim, FakeGame.LiveRecord)!;
        Check("an undead's Undead Magic is raised", rasvimAfter.Skills[12] == 46);
        Check("an undead's Healing Magic is left alone", rasvimAfter.Skills[8] == 10);
    }

    private static void LevelChecks()
    {
        Section("level");

        var mem = FakeGame.BuildGame();
        var image = FakeGame.Image(mem);
        var actions = new TrainerActions(mem, image);
        uint record = FakeGame.LiveRecord;

        Check("sets the level", actions.SetLevel(record, 12).Ok && Read(mem, record)!.Level == 12);

        var after = Read(mem, record)!;
        Check("raises experience to the level's floor", after.Experience == RecordBuilder.Thresholds[10]);
        Check("rewrites the cached next-level threshold", after.ExperienceForNextLevel == RecordBuilder.Thresholds[11]);
        Check("the cached threshold comes from the record's own table",
            after.ExperienceForNextLevel == after.ThresholdForLevel(13));

        // Going down must not throw experience away.
        var rich = FakeGame.BuildGame(b => b.Experience(5_000_000).Level(40));
        var richActions = new TrainerActions(rich, image);
        richActions.SetLevel(FakeGame.LiveRecord, 3);
        var lowered = Read(rich, FakeGame.LiveRecord)!;
        Check("lowering the level keeps the experience", lowered.Level == 3 && lowered.Experience == 5_000_000);
        Check("but still fixes the threshold", lowered.ExperienceForNextLevel == RecordBuilder.Thresholds[2]);

        Check("clamps to level 1", actions.SetLevel(record, -3).Ok && Read(mem, record)!.Level == 1);
        Check("clamps to the top of the table",
            actions.SetLevel(record, 5000).Ok && Read(mem, record)!.Level == GameFacts.MaxLevel);

        var top = Read(mem, record)!;
        Check("at the top level the threshold does not run off the table",
            top.ExperienceForNextLevel == RecordBuilder.Thresholds[GameFacts.MaxLevel - 2]);

        var guarded = new TrainerActions(mem, image) { ReadOnly = true };
        int level = Read(mem, record)!.Level;
        Check("read-only refuses a level change", !guarded.SetLevel(record, 2).Ok && Read(mem, record)!.Level == level);
    }

    private static void FreezeChecks()
    {
        Section("freezes");

        var mem = FakeGame.BuildGame();
        var image = FakeGame.Image(mem);
        var actions = new TrainerActions(mem, image);
        uint record = FakeGame.LiveRecord;
        var freezes = new FreezeWriter();

        Check("nothing frozen to start", !freezes.Any && freezes.Tick(actions, record) == 0);

        freezes.Freeze(FrozenField.Health, 500);
        Check("the target is latched, not derived", freezes.TargetOf(FrozenField.Health) == 500);

        actions.SetHealth(record, 3);
        freezes.Tick(actions, record);
        Check("a frozen field is put back", Read(mem, record)!.Health == 500);

        // The latch must not follow the value it is holding down.
        actions.SetHealth(record, 1);
        freezes.Tick(actions, record);
        freezes.Tick(actions, record);
        Check("the latch does not drift", Read(mem, record)!.Health == 500);

        freezes.Freeze(FrozenField.Gold, 777);
        freezes.Freeze(FrozenField.Crime, 0);
        actions.SetGold(record, 1);
        actions.SetCrime(record, 900);
        Check("all frozen fields are written each tick", freezes.Tick(actions, record) == 3);
        Check("gold was restored", Read(mem, record)!.Gold == 777);
        Check("crime was restored", Read(mem, record)!.Crime == 0);

        freezes.Thaw(FrozenField.Gold);
        Check("thawing removes just that field", !freezes.IsFrozen(FrozenField.Gold) && freezes.IsFrozen(FrozenField.Health));
        actions.SetGold(record, 42);
        freezes.Tick(actions, record);
        Check("a thawed field is left alone", Read(mem, record)!.Gold == 42);

        freezes.ThawAll();
        Check("thaw-all clears everything", !freezes.Any);

        // A freeze against a record that stopped validating must not throw.
        freezes.Freeze(FrozenField.Health, 100);
        var gone = FakeGame.BuildGame();
        gone.Unmap(FakeGame.EngineAddress);
        Check("a freeze over a vanished record writes nothing",
            freezes.Tick(new TrainerActions(gone, image), record) == 0);

        Check("a freeze with no record does nothing", freezes.Tick(actions, 0) == 0);

        // The sequence the view model performs when the user edits a field that is frozen, or
        // presses Clear crime with Crime frozen. Both halves are checked, so the first cannot pass
        // for the wrong reason: without the re-latch the tick really does undo the edit.
        var undone = new FreezeWriter();
        actions.SetCrime(record, 500);
        undone.Freeze(FrozenField.Crime, 500);
        actions.SetCrime(record, 0);
        undone.Tick(actions, record);
        Check("without a re-latch, the next tick undoes an explicit edit", Read(mem, record)!.Crime == 500);

        var relatched = new FreezeWriter();
        actions.SetCrime(record, 500);
        relatched.Freeze(FrozenField.Crime, 500);
        var cleared = actions.SetCrime(record, 0);
        Check("the write reports what to re-latch to", cleared.Written == 0);
        if (cleared.Written is { } w) relatched.Freeze(FrozenField.Crime, w);
        relatched.Tick(actions, record);
        relatched.Tick(actions, record);
        Check("with a re-latch, an explicit edit sticks", Read(mem, record)!.Crime == 0);
        Check("and the latch really moved", relatched.TargetOf(FrozenField.Crime) == 0);

        actions.SetCrime(record, 0);
    }

    private static void ItemLayoutChecks()
    {
        Section("item layout");

        Check("the carried-items vector sits at +0x320", ItemLayout.InventoryBegin == 0x320);
        Check("its three pointers are contiguous",
            ItemLayout.InventoryEnd == 0x324 && ItemLayout.InventoryCapacity == 0x328);

        // The two equipment arrays and the weapon-set byte tile exactly, which is how the game's
        // own "is this item equipped" loop indexes them.
        Check("the first equipment array starts at +0x334", ItemLayout.EquipmentSlots == 0x334);
        Check("the second follows it", ItemLayout.EquipmentSlotsSet2 == 0x36C);
        Check("the active-set byte follows the second", ItemLayout.ActiveWeaponSet == 0x3A4);
        Check("both arrays are fourteen slots wide",
            ItemLayout.EquipmentSlotsSet2 - ItemLayout.EquipmentSlots == ItemLayout.EquipmentSlotCount * 4);
        Check("the equipment arrays end before fame", ItemLayout.ActiveWeaponSet + 4 <= QuestLayout.Fame);
        Check("the inventory vector does not overlap the equipment arrays",
            ItemLayout.InventoryCapacity + 4 <= ItemLayout.EquipmentSlots);

        Check("an item is a type pointer, an enchantment pointer and a word",
            ItemLayout.ItemType == 0 && ItemLayout.ItemEnchantments == 4 && ItemLayout.ItemCondition == 8);
        Check("the reader only claims the bytes the game touches", ItemLayout.ItemBytes == 12);

        Check("a type starts with the engine back-pointer and its vtable",
            ItemLayout.TypeEngine == 0 && ItemLayout.TypeVTable == 4);
        Check("its three strings sit at +0x08, +0x10 and +0x14",
            ItemLayout.TypeId == 0x08 && ItemLayout.TypeResourceId == 0x10 && ItemLayout.TypeName == 0x14);
        Check("weight, damage and the two ceilings are where the item panel reads them",
            ItemLayout.TypeWeight == 0x32 && ItemLayout.TypeDamageMin == 0x36 &&
            ItemLayout.TypeDamageMax == 0x38 && ItemLayout.TypeEnchantStorage == 0x3C &&
            ItemLayout.TypeMaxCondition == 0x3E);
        Check("category, sub-type, alignment and flags are four consecutive bytes",
            ItemLayout.TypeCategory == 0x45 && ItemLayout.TypeSubtype == 0x46 &&
            ItemLayout.TypeAlignment == 0x47 && ItemLayout.TypeFlags == 0x48);
        Check("every type field fits inside the object", ItemLayout.TypeFlags < ItemLayout.TypeBytes);

        Check("item slots stride by four", ItemLayout.ItemSlot(0x1000, 3) == 0x100C);
        Check("equipment slots address the first set", ItemLayout.EquipmentSlot(0x1000, 0, 2) == 0x1000 + 0x334 + 8);
        Check("and the second", ItemLayout.EquipmentSlot(0x1000, 1, 2) == 0x1000 + 0x36C + 8);
    }

    private static void ItemTableChecks()
    {
        Section("item tables");

        Check("category names are the game's own",
            ItemTables.CategoryName(1) == "Weapon" && ItemTables.CategoryName(2) == "Heavy armor" &&
            ItemTables.CategoryName(3) == "Light armor" && ItemTables.CategoryName(9) == "Magic" &&
            ItemTables.CategoryName(14) == "Comestible");
        Check("an unknown category still says something", ItemTables.CategoryName(99).Contains("99"));

        Check("sub-type names are the game's own",
            ItemTables.SubtypeName(1, 2) == "long sword" && ItemTables.SubtypeName(2, 4) == "Helm" &&
            ItemTables.SubtypeName(9, 4) == "Wand" && ItemTables.SubtypeName(14, 2) == "Water");
        Check("the game's own placeholder reads as nothing", ItemTables.SubtypeName(2, 0) == "");
        Check("an out-of-range sub-type reads as nothing", ItemTables.SubtypeName(2, 40) == "");

        Check("a weapon is labelled light or heavy, as the item panel does",
            ItemTables.Describe(1, 2, lightWeapon: true) == "Light weapon · long sword" &&
            ItemTables.Describe(1, 2, lightWeapon: false) == "Heavy weapon · long sword");
        Check("anything else takes its category name",
            ItemTables.Describe(2, 4, false) == "Heavy armor · Helm");
        Check("a category with no sub-type name shows just the category",
            ItemTables.Describe(15, 0, false) == "Gem");
        Check("a sub-type that only repeats its category is dropped",
            ItemTables.Describe(10, 1, false) == "Money" && ItemTables.Describe(8, 1, false) == "Potion" &&
            ItemTables.Describe(5, 1, false) == "Book");
        Check("but a sub-type that adds something is kept",
            ItemTables.Describe(14, 1, false) == "Comestible · Food");

        // The meter's meaning follows the game's own item panel exactly, including that ammunition
        // sub-types of category 1 count units instead of wearing out.
        Check("a weapon wears out", ItemTables.MeterFor(1, 2) == ItemMeter.Condition);
        Check("but a quiver counts units", ItemTables.MeterFor(1, 11) == ItemMeter.Units);
        Check("as does a throwing weapon and a bolt quiver",
            ItemTables.MeterFor(1, 8) == ItemMeter.Units && ItemTables.MeterFor(1, 13) == ItemMeter.Units);
        Check("a bow does not", ItemTables.MeterFor(1, 9) == ItemMeter.Condition);
        Check("armour wears out",
            ItemTables.MeterFor(2, 4) == ItemMeter.Condition && ItemTables.MeterFor(3, 6) == ItemMeter.Condition);
        Check("a wand holds charges", ItemTables.MeterFor(9, 4) == ItemMeter.Charges);
        Check("a scroll holds nothing", ItemTables.MeterFor(9, 1) == ItemMeter.None);
        Check("a lockpick wears out but a key does not",
            ItemTables.MeterFor(11, 2) == ItemMeter.Condition && ItemTables.MeterFor(11, 1) == ItemMeter.None);
        Check("repair hammers and alchemy gear wear out",
            ItemTables.MeterFor(12, 1) == ItemMeter.Condition && ItemTables.MeterFor(6, 1) == ItemMeter.Condition);
        Check("a book has no meter", ItemTables.MeterFor(5, 1) == ItemMeter.None);

        // The game's ladder, boundary by boundary.
        Check("under a tenth is broken", ItemTables.ConditionBand(9, 100) == "broken");
        Check("a tenth is poor", ItemTables.ConditionBand(10, 100) == "poor");
        Check("under thirty per cent is still poor", ItemTables.ConditionBand(29, 100) == "poor");
        Check("thirty per cent is average", ItemTables.ConditionBand(30, 100) == "average");
        Check("under seventy is still average", ItemTables.ConditionBand(69, 100) == "average");
        Check("seventy per cent is good", ItemTables.ConditionBand(70, 100) == "good");
        Check("ninety-nine per cent is still good", ItemTables.ConditionBand(99, 100) == "good");
        Check("only a full hundred is perfect", ItemTables.ConditionBand(100, 100) == "perfect");
        Check("a type with no maximum has no band", ItemTables.ConditionBand(5, 0) == "");
    }

    private static void ItemTypeChecks()
    {
        Section("item types");

        var (mem, heap) = FakeGame.BuildGameWithItems();
        uint engine = FakeGame.EngineAddress;

        var sword = ItemTypeReader.Read(mem, heap.Types[0], engine);
        Check("a type reads back", sword is not null);
        Check("its name and id are the strings it points at",
            sword?.Name == "Longsword" && sword?.Id == "base_weap_longsword");
        Check("its category and sub-type decode", sword?.Category == 1 && sword?.Subtype == 2);
        Check("its weight, damage and ceiling decode",
            sword?.Weight == 1000 && sword?.DamageMin == 6 && sword?.DamageMax == 17 && sword?.MaxCondition == 10000);
        Check("weight is printed the way the game prints it", sword?.WeightLabel == "10.0");
        Check("its category label reads as the item panel writes it", sword?.CategoryLabel == "Heavy weapon · long sword");
        Check("the picker label carries both", sword?.PickerLabel.StartsWith("Longsword") == true);

        // Each validation rule, defeated one at a time.
        Check("a null address is not a type", ItemTypeReader.Read(mem, 0, engine) is null);
        Check("an unaligned address is not a type", ItemTypeReader.Read(mem, heap.Types[0] + 1, engine) is null);
        Check("an unmapped address is not a type", ItemTypeReader.Read(mem, 0x7000_0000, engine) is null);
        Check("a type belonging to another engine is rejected",
            ItemTypeReader.Read(mem, heap.Types[0], engine + 0x10) is null);

        var scratch = FakeGame.BuildGameWithItems();
        scratch.Heap.SetEngine(scratch.Heap.Types[1], 0xDEAD_BEEF);
        Check("so is one whose back-pointer was overwritten",
            ItemTypeReader.Read(scratch.Memory, scratch.Heap.Types[1], engine) is null);

        scratch = FakeGame.BuildGameWithItems();
        scratch.Memory.PokeUInt32(scratch.Heap.Types[1] + ItemLayout.TypeVTable, 0x7FFF_0000);
        Check("a vtable outside the game module is rejected",
            ItemTypeReader.Read(scratch.Memory, scratch.Heap.Types[1], engine) is null);

        scratch = FakeGame.BuildGameWithItems();
        scratch.Heap.SetCategory(scratch.Heap.Types[1], 0);
        Check("category 0 is not an item", ItemTypeReader.Read(scratch.Memory, scratch.Heap.Types[1], engine) is null);
        scratch.Heap.SetCategory(scratch.Heap.Types[1], ItemTables.MaxCategory + 1);
        Check("nor is a category past the game's last",
            ItemTypeReader.Read(scratch.Memory, scratch.Heap.Types[1], engine) is null);

        scratch = FakeGame.BuildGameWithItems();
        scratch.Memory.PokeUInt32(scratch.Heap.Types[1] + ItemLayout.TypeName, 0);
        Check("a type with no name is rejected",
            ItemTypeReader.Read(scratch.Memory, scratch.Heap.Types[1], engine) is null);

        // The string check is what separates a type from an arbitrary run of heap bytes, so its
        // boundaries are pinned rather than assumed.
        Check("a control character is not a name", ItemTypeReader.ReadText(mem, TextAt(mem, new string(new[] { 'a', (char)0x01, 'b' }))) is null);
        Check("a high byte is not a name either", ItemTypeReader.ReadText(mem, TextAt(mem, "café")) is null);
        Check("an empty string is not a name", ItemTypeReader.ReadText(mem, TextAt(mem, "")) is null);
        Check("plain ASCII is", ItemTypeReader.ReadText(mem, TextAt(mem, "Longsword")) == "Longsword");
        Check("a null pointer reads nothing", ItemTypeReader.ReadText(mem, 0) is null);
    }

    private static void CatalogChecks()
    {
        Section("item catalog");

        var (mem, heap) = FakeGame.BuildGameWithItems();

        // A heap block that carries the engine back-pointer, a real category and readable strings,
        // and differs from a real type only in its vtable. Without it, "the sweep finds only real
        // types" would pass on the strength of the category check alone and prove nothing.
        uint decoy = heap.AddDecoy("Longsword");

        var found = ItemCatalog.Sweep(mem, FakeGame.EngineAddress);

        Check("the sweep finds every planted type", found.Count == heap.Types.Count);
        Check("and finds them all by address",
            heap.Types.TrueForAll(t => found.Any(f => f.Address == t)));
        Check("names come back with them", found.Any(f => f.Name == "Longsword") && found.Any(f => f.Name == "Bread"));

        Check("a heap block that only looks like a type is skipped", found.All(f => f.Address != decoy));

        // The module's own .data slot holds the engine pointer too — the false positive a real
        // session actually contains.
        Check("the static engine slot is not mistaken for a type",
            found.All(f => f.Address != FakeGame.ModuleBase + QuestLayout.EngineSlotRva));

        Check("a sweep for the wrong engine finds nothing",
            ItemCatalog.Sweep(mem, FakeGame.EngineAddress + 0x1000).Count == 0);
        Check("a sweep with no engine finds nothing", ItemCatalog.Sweep(mem, 0).Count == 0);

        var sword = found.First(f => f.Name == "Longsword");
        Check("a normal type may be placed", ItemCatalog.CanReplaceWith(sword, out _));

        // A type that shows a condition but has no maximum would make the game's own item panel
        // divide by zero. No shipped type is like that; the check is what keeps it that way.
        var broken = sword with { MaxCondition = 0 };
        Check("a condition type with no maximum may not be placed", !ItemCatalog.CanReplaceWith(broken, out string why));
        Check("and the refusal says why", why.Contains("divide by zero"));

        var bread = found.First(f => f.Name == "Bread");
        Check("a type with no meter at all is fine", ItemCatalog.CanReplaceWith(bread with { MaxCondition = 0 }, out _));
    }

    private static void InventoryChecks()
    {
        Section("inventory");

        var (mem, heap) = FakeGame.BuildGameWithItems();
        uint record = FakeGame.LiveRecord;

        var pack = InventoryReader.Read(mem, record);
        Check("the pack reads back", pack is not null);
        Check("with every carried item", pack?.Items.Count == 5);
        Check("the engine address comes with it", pack?.Engine == FakeGame.EngineAddress);
        Check("items keep the game's order",
            pack!.Items[0].Type.Name == "Longsword" && pack.Items[2].Type.Name == "Bread");
        Check("each row knows its own address", pack.Items[0].Address == heap.Items[0]);

        Check("total weight is summed from the types", pack.TotalWeight == 1000 + 200 + 30 + 20 + 50);
        Check("and printed the way the game prints it", pack.TotalWeightLabel == "13.0");

        var sword = pack.Items[0];
        Check("a worn weapon reports its condition", sword.Meter == 4000 && sword.MeterMax == 10000);
        Check("and can be restored", sword.CanRestore);
        Check("its label uses the game's wear band", sword.MeterLabel.StartsWith("average"));

        var helm = pack.Items[1];
        Check("an item already at full condition has nothing to restore", !helm.CanRestore);
        Check("and reads as perfect", helm.MeterLabel.StartsWith("perfect"));

        var bread = pack.Items[2];
        Check("an item with no meter has no maximum", bread.MeterMax == 0 && !bread.CanRestore);
        Check("and shows no meter text", bread.MeterLabel == "");

        // The wand's ceiling is not in its type: it comes from the first entry of the enchantment
        // vector, which is where the game's own recharge code reads it.
        var wand = pack.Items[3];
        Check("a wand's charges come from its enchantment",
            wand.Meter == 3 && wand.MeterMax == FakeGame.WandCharges);
        Check("and read as a fraction", wand.MeterLabel == "3/12 charges");

        var quiver = pack.Items[4];
        Check("a quiver counts units", quiver.MeterLabel == "7 units");
        Check("and one arrow is singular", (quiver with { Meter = 1 }).MeterLabel == "1 unit");

        // Equipment is discovered by searching both arrays for the item's pointer; there is no flag
        // on the item itself.
        Check("an item in the first weapon set is equipped",
            helm.IsEquipped && helm.EquippedSlot == 1 && helm.EquippedSet == 0);
        Check("an item in the second set is equipped too",
            sword.IsEquipped && sword.EquippedSlot == 4 && sword.EquippedSet == 1);
        Check("everything else is not", !bread.IsEquipped && !wand.IsEquipped);

        // An empty pack is a legitimate state, not a failure.
        var empty = FakeGame.BuildGame(r => r.Inventory(0, 0));
        Check("an empty vector reads as an empty pack", InventoryReader.Read(empty, record)?.Items.Count == 0);

        // Every way the two pointers can fail to be a vector.
        Check("a reversed vector is refused",
            InventoryReader.Read(FakeGame.BuildGame(r => r.Inventory(ItemHeap.VectorBase + 16, ItemHeap.VectorBase)), record) is null);
        Check("a misaligned length is refused",
            InventoryReader.Read(FakeGame.BuildGame(r => r.Inventory(ItemHeap.VectorBase, ItemHeap.VectorBase + 6)), record) is null);
        Check("an implausibly long vector is refused",
            InventoryReader.Read(FakeGame.BuildGame(r =>
                r.Inventory(ItemHeap.VectorBase, ItemHeap.VectorBase + (uint)(ItemLayout.MaxItems + 1) * 4)), record) is null);
        Check("a vector whose elements cannot be read is refused",
            InventoryReader.Read(FakeGame.BuildGame(r => r.Inventory(0x7000_0000, 0x7000_0010)), record) is null);

        // A single bad element is skipped rather than losing the whole pack: the player can be
        // holding something the trainer does not recognise, and the rest still matters.
        var damaged = FakeGame.BuildGameWithItems();
        damaged.Heap.SetEngine(damaged.Heap.Types[2], 0xDEAD_BEEF);
        var partial = InventoryReader.Read(damaged.Memory, record);
        Check("an item whose type no longer validates is skipped", partial?.Items.Count == 4);
        Check("and the rest of the pack survives", partial!.Items.All(i => i.Type.Name != "Bread"));
    }

    private static void ItemActionChecks()
    {
        Section("item actions");

        var (mem, heap) = FakeGame.BuildGameWithItems();
        var image = FakeGame.Image(mem);
        var actions = new TrainerActions(mem, image);
        uint record = FakeGame.LiveRecord;

        uint swordItem = heap.Items[0];
        uint helmItem = heap.Items[1];
        uint breadItem = heap.Items[2];
        uint wandItem = heap.Items[3];

        // --- restore -------------------------------------------------------------------------
        var restored = actions.RestoreItem(record, swordItem);
        Check("restoring a weapon fills its condition", restored.Ok && Meter(mem, swordItem) == 10000);
        Check("and reports what landed", restored.Written == 10000);

        Check("recharging a wand uses the enchantment's count",
            actions.RestoreItem(record, wandItem).Ok && Meter(mem, wandItem) == FakeGame.WandCharges);

        var nothing = actions.RestoreItem(record, breadItem);
        Check("an item with nothing to restore is refused", !nothing.Ok);
        Check("and says so by name", nothing.Message.Contains("Bread"));

        // --- restore all ----------------------------------------------------------------------
        var fresh = FakeGame.BuildGameWithItems();
        var freshActions = new TrainerActions(fresh.Memory, FakeGame.Image(fresh.Memory));
        var all = freshActions.RestoreAllItems(record);
        Check("restore-all fills everything that can be filled", all.Ok);
        Check("the weapon came up", Meter(fresh.Memory, fresh.Heap.Items[0]) == 10000);
        Check("the wand came up", Meter(fresh.Memory, fresh.Heap.Items[3]) == FakeGame.WandCharges);
        Check("the quiver did not, having no ceiling", Meter(fresh.Memory, fresh.Heap.Items[4]) == 7);
        Check("running it again finds nothing left to do",
            freshActions.RestoreAllItems(record).Message.Contains("already"));

        // --- explicit edits ---------------------------------------------------------------------
        Check("a meter can be set outright",
            actions.SetItemMeter(record, swordItem, 1234).Ok && Meter(mem, swordItem) == 1234);
        var clamped = actions.SetItemMeter(record, swordItem, 999_999);
        Check("and is clamped to the word it goes into",
            clamped.Ok && Meter(mem, swordItem) == GameFacts.MaxItemMeter);
        Check("the clamp is reported rather than hidden", clamped.Written == GameFacts.MaxItemMeter);
        Check("a negative meter clamps to zero",
            actions.SetItemMeter(record, swordItem, -5).Ok && Meter(mem, swordItem) == 0);

        // Writes are addressed to an item the game still holds, not to a position in the pack. An
        // address that is no longer in the vector is refused — without that lookup this would
        // happily write into a freed heap block.
        uint orphan = heap.AddItem(heap.Types[0], meter: 1);
        Check("an item that is not in the pack is refused", !actions.SetItemMeter(record, orphan, 500).Ok);
        Check("and it really was left alone", Meter(mem, orphan) == 1);
        Check("as is restoring one", !actions.RestoreItem(record, orphan).Ok);

        // --- replace -------------------------------------------------------------------------
        var target = ItemTypeReader.Read(mem, heap.Types[1], FakeGame.EngineAddress)!;   // Helm
        var replaced = actions.ReplaceItem(record, breadItem, target);
        Check("an item can be turned into another type", replaced.Ok);
        Check("its type pointer really moved", TypeOf(mem, breadItem) == heap.Types[1]);
        Check("and it arrives at full condition", Meter(mem, breadItem) == 2500);

        var afterwards = InventoryReader.Read(mem, record)!;
        Check("the pack now reports the new item", afterwards.Items[2].Type.Name == "Helm");
        Check("and its weight follows the new type", afterwards.Items[2].Type.Weight == 200);

        // Equipment slots hold raw pointers, so retyping in place would leave a body slot holding
        // something the game never put there.
        var equipped = actions.ReplaceItem(record, helmItem, target);
        Check("an equipped item may not be replaced", !equipped.Ok);
        Check("and the refusal explains what to do", equipped.Message.Contains("unequip"));
        Check("the equipped item is untouched", TypeOf(mem, helmItem) == heap.Types[1]);

        var swordType = ItemTypeReader.Read(mem, heap.Types[0], FakeGame.EngineAddress)!;
        Check("a type that would divide by zero is refused",
            !actions.ReplaceItem(record, breadItem, swordType with { MaxCondition = 0 }).Ok);

        var stale = swordType with { Address = 0x7000_0000 };
        var gone = actions.ReplaceItem(record, breadItem, stale);
        Check("a catalog entry that no longer validates is refused", !gone.Ok);
        Check("and the refusal suggests a rescan", gone.Message.Contains("rescan"));

        // --- the safety catch -------------------------------------------------------------------
        var locked = new TrainerActions(mem, image) { ReadOnly = true };
        Check("read-only refuses a restore", !locked.RestoreItem(record, swordItem).Ok);
        Check("read-only refuses a meter edit", !locked.SetItemMeter(record, swordItem, 10).Ok);
        Check("read-only refuses a replacement", !locked.ReplaceItem(record, breadItem, target).Ok);
        Check("and nothing moved", TypeOf(mem, breadItem) == heap.Types[1]);

        // A record that stopped validating must refuse every item write too, not just the scalars.
        var vanished = FakeGame.BuildGameWithItems();
        var vanishedActions = new TrainerActions(vanished.Memory, FakeGame.Image(vanished.Memory));
        vanished.Memory.PokeUInt32(record + QuestLayout.ExperienceTable, 12345);
        Check("a record that stopped validating refuses a restore",
            !vanishedActions.RestoreItem(record, vanished.Heap.Items[0]).Ok);
    }

    private static void PickerChecks()
    {
        Section("process picker");

        Check("the game's own name is an exact match", ProcessPicker.Rank("TheQuest") == ProcessMatch.Exact);
        Check("case does not matter", ProcessPicker.Rank("thequest") == ProcessMatch.Exact);
        Check("the trainer itself is only a hint", ProcessPicker.Rank("TheQuestTrainer") == ProcessMatch.Hint);
        Check("an unrelated process matches nothing", ProcessPicker.Rank("explorer") == ProcessMatch.None);
        Check("the trainer never offers itself", !ProcessPicker.IsSelectable(42, 42));

        var entries = new List<ProcessEntry>
        {
            new(1, "TheQuestTrainer", ""),
            new(2, "TheQuest", "The Quest"),
            new(3, "explorer", ""),
        };
        var ordered = ProcessPicker.Order(entries, e => e.Match, e => e.Name).ToList();
        Check("the exact match sorts first", ordered[0].Id == 2);

        var chosen = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, null);
        Check("the exact match is chosen by default", chosen?.Id == 2);

        var hintsOnly = ProcessPicker.Order(new List<ProcessEntry> { new(1, "TheQuestTrainer", "") },
            e => e.Match, e => e.Name).ToList();
        Check("a hint-only match is never chosen automatically",
            ProcessPicker.ChooseDefault(hintsOnly, e => e.Match, e => e.Id, null) is null);

        Check("a previous selection survives a refresh",
            ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, 3)?.Id == 3);

        Check("the display label includes the window title", entries[1].Display.Contains("The Quest"));
        Check("and copes without one", entries[0].Display == "TheQuestTrainer (1)");
    }

    // ---- plumbing ------------------------------------------------------------------------------

    private static CharacterSnapshot? Read(IMemorySource mem, uint record) => CharacterReader.Read(mem, record);

    /// <summary>The one mutable word of the item at <paramref name="item"/>.</summary>
    private static int Meter(IMemorySource mem, uint item)
    {
        var word = new byte[2];
        return mem.Read(item + ItemLayout.ItemCondition, word, 2) == 2 ? BitConverter.ToUInt16(word, 0) : -1;
    }

    /// <summary>The type pointer of the item at <paramref name="item"/>.</summary>
    private static uint TypeOf(IMemorySource mem, uint item)
    {
        var word = new byte[4];
        return mem.Read(item + ItemLayout.ItemType, word, 4) == 4 ? BitConverter.ToUInt32(word, 0) : 0;
    }

    /// <summary>
    /// Maps <paramref name="value"/> as a NUL-terminated Latin-1 string in a scratch page and
    /// returns its address, so the C-string reader's boundaries can be checked against real bytes.
    /// Control characters in the value are written literally, which is the point.
    /// </summary>
    private static uint TextAt(FakeMemory mem, string value)
    {
        const uint scratch = 0x0600_0000;
        var bytes = System.Text.Encoding.Latin1.GetBytes(value);
        var page = new byte[Math.Max(ItemTypeReader.MaxTextLength, bytes.Length + 1)];
        bytes.CopyTo(page, 0);
        page[bytes.Length] = 0;
        mem.Map(scratch, page);
        return scratch;
    }

    private static void Section(string name) => Console.WriteLine($"-- {name}");

    private static void Check(string what, bool ok)
    {
        if (ok) _passed++;
        else _failures.Add(what);
    }
}
