using System.IO;
using TheQuestTrainer.Adventures;
using TheQuestTrainer.Cluebooks;
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
internal static partial class Program
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
        ConditionLayoutChecks();
        ConditionTableChecks();
        ConditionReaderChecks();
        ConditionActionChecks();
        MapLayoutChecks();
        MapReaderChecks();
        TeleportChecks();
        DdsChecks();
        WorldPictureChecks();
        MapViewChecks();
        PickerChecks();
        DocumentChecks();
        ArchiveChecks();
        PalmDatabaseChecks();
        AdventureHeaderChecks();
        AdventureReaderChecks();
        CluebookChecks();
        CluebookTabChecks();

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

    private static void ConditionLayoutChecks()
    {
        Section("condition layout");

        Check("the disease vector sits at +0x3B4", ConditionLayout.DiseasesBegin == 0x3B4);
        Check("its three pointers are contiguous",
            ConditionLayout.DiseasesEnd == 0x3B8 && ConditionLayout.DiseasesCapacity == 0x3BC);
        Check("it comes after the equipment arrays",
            ConditionLayout.DiseasesBegin > ItemLayout.ActiveWeaponSet);
        Check("it comes before fame", ConditionLayout.DiseasesCapacity <= QuestLayout.Fame);

        Check("the effect groups start at +0x404", ConditionLayout.EffectGroups == 0x404);
        Check("they start past the fields the snapshot covers",
            ConditionLayout.EffectGroups >= QuestLayout.RecordBytes);
        Check("a group is one std::vector", ConditionLayout.EffectGroupBytes == 12);
        Check("there are 25 of them, slot 0 unused",
            ConditionLayout.EffectGroupSlots == 25 &&
            ConditionLayout.FirstEffectGroup == 1 && ConditionLayout.LastEffectGroup == 24);

        // The kind table abuts the group array. That is the arithmetic the game's own cure does,
        // written out, so a slip in either constant fails here rather than reading a neighbour.
        Check("the kind table abuts the group array", ConditionLayout.EffectGroupOfKind == 0x530);

        // The three groups the shipped build files these kinds under, reached the way the trainer
        // reaches them: group index times twelve, from the base.
        Check("group 23 is where +0x518 is", ConditionLayout.EffectGroupBegin(0x1000, 23) == 0x1000 + 0x518);
        Check("group 22 is where +0x50C is", ConditionLayout.EffectGroupBegin(0x1000, 22) == 0x1000 + 0x50C);
        Check("group 21 is where +0x500 is", ConditionLayout.EffectGroupBegin(0x1000, 21) == 0x1000 + 0x500);
        Check("a group's end follows its begin",
            ConditionLayout.EffectGroupEnd(0x1000, 23) == ConditionLayout.EffectGroupBegin(0x1000, 23) + 4);
        Check("the last group ends exactly where the kind table starts",
            ConditionLayout.EffectGroupBegin(0, ConditionLayout.LastEffectGroup) + ConditionLayout.EffectGroupBytes
                == ConditionLayout.EffectGroupOfKind);

        Check("the kind table is indexed by dword",
            ConditionLayout.EffectGroupSlot(0x1000, ConditionLayout.KindPoison) == 0x1000 + 0x530 + 0x1A * 4);
        Check("the three kinds are the game's own",
            ConditionLayout.KindPoison == 0x1A && ConditionLayout.KindCurse == 0x1B &&
            ConditionLayout.KindParalysis == 0x1C);

        Check("only groups the game uses are accepted",
            !ConditionLayout.IsEffectGroup(0) && ConditionLayout.IsEffectGroup(1) &&
            ConditionLayout.IsEffectGroup(24) && !ConditionLayout.IsEffectGroup(25) &&
            !ConditionLayout.IsEffectGroup(-1));

        Check("the effect's fields tile",
            ConditionLayout.EffectTypeKey == 0x04 && ConditionLayout.EffectMagnitude == 0x08 &&
            ConditionLayout.EffectDuration == 0x0C && ConditionLayout.EffectGroup == 0x10 &&
            ConditionLayout.EffectSource == 0x11 && ConditionLayout.EffectSubject == 0x12);
        Check("the effect is the size the game frees", ConditionLayout.EffectBytes == 0x14);
        Check("every field is inside the allocation",
            ConditionLayout.EffectSubject < ConditionLayout.EffectBytes);

        // The game's own cure removes these three sources and no others. Equipment, disease and
        // race are all re-derived from something that still exists.
        Check("a cure removes sources 2, 3 and 6",
            ConditionLayout.IsCurable(2) && ConditionLayout.IsCurable(3) && ConditionLayout.IsCurable(6));
        Check("a cure leaves equipment, disease and race alone",
            !ConditionLayout.IsCurable(ConditionLayout.SourceEquipment) &&
            !ConditionLayout.IsCurable(ConditionLayout.SourceDisease) &&
            !ConditionLayout.IsCurable(ConditionLayout.SourceRace));
        Check("and leaves an unknown source alone",
            !ConditionLayout.IsCurable(0) && !ConditionLayout.IsCurable(7) && !ConditionLayout.IsCurable(255));

        Check("a disease type's id and name are where the game reads them",
            ConditionLayout.DiseaseTypeId == 0x04 && ConditionLayout.DiseaseTypeName == 0x08);
    }

    private static void ConditionTableChecks()
    {
        Section("condition tables");

        Check("the game names four", ConditionTables.All.Count == 4);
        Check("they are the four with icons",
            ConditionTables.All.Contains(Condition.Poison) && ConditionTables.All.Contains(Condition.Disease) &&
            ConditionTables.All.Contains(Condition.Curse) && ConditionTables.All.Contains(Condition.Paralysis));

        Check("the labels are the game's own",
            ConditionTables.Name(Condition.Poison) == "Poisoned" &&
            ConditionTables.Name(Condition.Disease) == "Diseased" &&
            ConditionTables.Name(Condition.Curse) == "Cursed" &&
            ConditionTables.Name(Condition.Paralysis) == "Paralyzed");
        Check("every condition has a noun and an effect",
            ConditionTables.All.All(c => ConditionTables.Noun(c).Length > 0 && ConditionTables.Effect(c).Length > 0));

        Check("three conditions are effect kinds, disease is not",
            ConditionTables.EffectKind(Condition.Poison) == ConditionLayout.KindPoison &&
            ConditionTables.EffectKind(Condition.Curse) == ConditionLayout.KindCurse &&
            ConditionTables.EffectKind(Condition.Paralysis) == ConditionLayout.KindParalysis &&
            ConditionTables.EffectKind(Condition.Disease) is null);

        // The game prints poison as health per turn and both of the timed ones as turns left.
        Check("poison is described per turn",
            ConditionTables.Describe(Condition.Poison, 2, 0) == "2 health per turn");
        Check("one turn is not pluralised",
            ConditionTables.Describe(Condition.Curse, 0, 1) == "1 turn left");
        Check("more than one is",
            ConditionTables.Describe(Condition.Paralysis, 0, 14) == "14 turns left");
    }

    private static void ConditionReaderChecks()
    {
        Section("condition reader");

        var clean = FakeGame.BuildGame();
        uint record = FakeGame.LiveRecord;

        var healthy = ConditionReader.Read(clean, record);
        Check("a clean character reads as clean", healthy is { Any: false, AnyCurable: false });
        Check("and says so", healthy?.Summary == "None.");
        Check("with no diseases", healthy?.Diseases.Count == 0);

        var (mem, heap) = FakeGame.BuildAfflictedGame();
        var sick = ConditionReader.Read(mem, record);
        Check("an afflicted character reads as afflicted", sick is { Any: true, AnyCurable: true });

        // Two diseases are two afflictions, so all four conditions produce five lines.
        Check("every condition is reported", sick?.Afflictions.Count == 5);
        Check("poison is reported the way the game words it",
            sick?.Afflictions.Any(a => a.Label == "Poisoned — 2 health per turn") == true);
        Check("a curse reports its longest remaining turn count",
            sick?.Afflictions.Any(a => a.Label == "Cursed — 14 turns left") == true);
        Check("paralysis is reported too",
            sick?.Afflictions.Any(a => a.Label == "Paralyzed — 5 turns left") == true);
        Check("diseases are named through their type",
            sick?.Diseases.SequenceEqual(new[] { "Grey Fever", "Bone Rot" }) == true);
        Check("and each is its own line",
            sick?.Afflictions.Count(a => a.Condition == Condition.Disease) == 2 &&
            sick.Afflictions.Any(a => a.Label == "Diseased — Grey Fever"));
        Check("the summary is one line per affliction", sick?.Summary.Split('\n').Length == 5);

        // The racial modifier in group 2 is an effect like any other, and it is not an affliction.
        Check("a racial modifier is not reported as a condition",
            sick?.Afflictions.All(a => a.Condition != Condition.Poison || a.Entries == 1) == true);

        var curseGroup = ConditionReader.ReadGroup(mem, record, ConditionHeap.CurseGroup);
        Check("a group reports its entries and its totals",
            curseGroup is { Effects.Count: 2, LongestDuration: 14, Curable: 2 });

        var racial = ConditionReader.ReadGroup(mem, record, 2);
        Check("a group counts only the curable entries as curable",
            racial is { Effects.Count: 2, Curable: 0 });
        Check("and reports each effect's source",
            racial?.Effects.Select(e => e.Source).SequenceEqual(
                new byte[] { ConditionLayout.SourceRace, ConditionLayout.SourceDisease }) == true);

        // The reader follows the record's own kind table rather than a baked-in group number.
        var (moved, movedHeap) = FakeGame.BuildAfflictedGame();
        movedHeap.SetKind(ConditionLayout.KindPoison, 19);
        movedHeap.SetGroup(19, movedHeap.AddEffect(9, 0, source: 6, group: 19));
        movedHeap.SetGroup(ConditionHeap.PoisonGroup);
        Check("the kind table is followed, not assumed",
            ConditionReader.Read(moved, record)?.Afflictions
                .Any(a => a.Label == "Poisoned — 9 health per turn") == true);

        // The game's own test is "does the poison total more than zero", not "is the list empty".
        var (balanced, balancedHeap) = FakeGame.BuildAfflictedGame();
        balancedHeap.SetGroup(ConditionHeap.PoisonGroup,
            balancedHeap.AddEffect(4, 0, source: 6, group: ConditionHeap.PoisonGroup),
            balancedHeap.AddEffect(-4, 0, source: 6, group: ConditionHeap.PoisonGroup));
        Check("poison that nets to nothing is not poison",
            ConditionReader.Read(balanced, record)?.Afflictions
                .All(a => a.Condition != Condition.Poison) == true);

        // Every way the structures can fail to be what they should be ends in null, never in a
        // half-read list — these are the addresses the cure writes to.
        var (bad, badHeap) = FakeGame.BuildAfflictedGame();
        ConditionHeap.SetKind(bad, record, ConditionLayout.KindPoison, 0);
        Check("a kind filed under no group is refused", ConditionReader.Read(bad, record) is null);

        ConditionHeap.SetKind(bad, record, ConditionLayout.KindPoison, ConditionLayout.EffectGroupSlots);
        Check("a kind filed past the array is refused", ConditionReader.Read(bad, record) is null);

        ConditionHeap.SetKind(bad, record, ConditionLayout.KindPoison, ConditionHeap.PoisonGroup);
        Check("and putting it back makes it readable again", ConditionReader.Read(bad, record) is not null);

        badHeap.SetGroupRaw(ConditionHeap.PoisonGroup, 0x1000, 0x0FF0);
        Check("a backwards group vector is refused", ConditionReader.Read(bad, record) is null);

        badHeap.SetGroupRaw(ConditionHeap.PoisonGroup, 0x1001, 0x1005);
        Check("a misaligned group vector is refused", ConditionReader.Read(bad, record) is null);

        badHeap.SetGroupRaw(ConditionHeap.PoisonGroup, 0x1000,
            0x1000 + (uint)(ConditionLayout.MaxEffectsPerGroup + 1) * 4);
        Check("an implausibly long group vector is refused", ConditionReader.Read(bad, record) is null);

        badHeap.SetGroupRaw(ConditionHeap.PoisonGroup, 0x7000_0000, 0x7000_0004);
        Check("a group whose elements cannot be read is refused", ConditionReader.Read(bad, record) is null);

        badHeap.SetGroup(ConditionHeap.PoisonGroup, 0);
        Check("a null effect pointer is refused", ConditionReader.Read(bad, record) is null);

        var (sickly, sicklyHeap) = FakeGame.BuildAfflictedGame();
        sicklyHeap.SetDiseasesRaw(0x1004, 0x1000);
        Check("a backwards disease vector is refused", ConditionReader.Read(sickly, record) is null);

        sicklyHeap.SetDiseasesRaw(0x7000_0000, 0x7000_0004);
        Check("a disease vector whose elements cannot be read is refused",
            ConditionReader.Read(sickly, record) is null);

        var (nameless, namelessHeap) = FakeGame.BuildAfflictedGame();
        uint type = namelessHeap.AddDiseaseType("base_dis_x", "Ague");
        namelessHeap.SetDiseases(type);
        namelessHeap.SetDiseaseName(type, 0x7000_0000);
        Check("a disease whose name cannot be read is refused",
            ConditionReader.Read(nameless, record) is null);

        // An empty vector is a legitimate state, and a never-allocated one is how the game leaves a
        // character who has never been ill.
        var (empty, emptyHeap) = FakeGame.BuildAfflictedGame();
        emptyHeap.SetDiseasesRaw(0, 0);
        emptyHeap.SetGroup(ConditionHeap.PoisonGroup);
        emptyHeap.SetGroup(ConditionHeap.CurseGroup);
        emptyHeap.SetGroup(ConditionHeap.ParalysisGroup);
        Check("an emptied character reads as clean", ConditionReader.Read(empty, record) is { Any: false });
    }

    private static void ConditionActionChecks()
    {
        Section("condition actions");

        uint record = FakeGame.LiveRecord;
        var image = FakeGame.Image(FakeGame.BuildGame());

        // Nothing wrong: the cure reports so and writes nothing at all.
        var clean = FakeGame.BuildGame();
        var cleanActions = new TrainerActions(clean, image);
        uint endBefore = GroupEnd(clean, record, ConditionHeap.PoisonGroup);
        var nothing = cleanActions.CureConditions(record);
        Check("a clean character needs no cure", nothing is { Ok: true });
        Check("and nothing was written", GroupEnd(clean, record, ConditionHeap.PoisonGroup) == endBefore);

        var (mem, heap) = FakeGame.BuildAfflictedGame();
        var actions = new TrainerActions(mem, image);

        Check("the fixture starts afflicted", ConditionReader.Read(mem, record) is { Any: true });
        var cured = actions.CureConditions(record);
        Check("the cure reports success", cured.Ok);
        Check("and lists what it took", cured.Message.Contains("poison") && cured.Message.Contains("paralysis"));

        var after = ConditionReader.Read(mem, record);
        Check("nothing adverse is left", after is { Any: false });
        Check("the diseases are gone", after?.Diseases.Count == 0);
        Check("the poison group is empty",
            ConditionReader.ReadGroup(mem, record, ConditionHeap.PoisonGroup)?.Effects.Count == 0);
        Check("the curse group is empty",
            ConditionReader.ReadGroup(mem, record, ConditionHeap.CurseGroup)?.Effects.Count == 0);
        Check("the paralysis group is empty",
            ConditionReader.ReadGroup(mem, record, ConditionHeap.ParalysisGroup)?.Effects.Count == 0);

        // The whole point of reading the source byte: the racial modifier survives and the penalty
        // the disease was granting does not.
        var racial = ConditionReader.ReadGroup(mem, record, 2);
        Check("a racial modifier survives the cure",
            racial is { Effects.Count: 1 } && racial.Effects[0].Source == ConditionLayout.SourceRace);
        Check("a disease's own penalty does not", racial?.Effects.All(e => e.Magnitude == -5) == true);

        // Emptying a vector must leave begin alone: the buffer is still the game's to free.
        Check("the vector's buffer is left in place",
            ConditionReader.ReadGroup(mem, record, ConditionHeap.PoisonGroup)?.Begin
                == ConditionHeap.GroupArray(ConditionHeap.PoisonGroup));

        // A partial erase has to compact in order, not merely shorten. The one removed here sits in
        // the middle, between two the cure must leave: an effect from the character's race and one
        // from something worn.
        var (partial, partialHeap) = FakeGame.BuildAfflictedGame();
        int poison = ConditionHeap.PoisonGroup;
        uint keepFirst = partialHeap.AddEffect(1, 0, ConditionLayout.SourceRace, group: poison);
        uint drop = partialHeap.AddEffect(2, 0, source: 6, group: poison);
        uint keepLast = partialHeap.AddEffect(3, 0, ConditionLayout.SourceEquipment, group: poison);
        partialHeap.SetGroup(poison, keepFirst, drop, keepLast);
        new TrainerActions(partial, image).CureConditions(record);
        var survivors = ConditionReader.ReadGroup(partial, record, poison);
        Check("a partial erase keeps the survivors, in order",
            survivors?.Effects.Select(e => e.Address).SequenceEqual(new[] { keepFirst, keepLast }) == true);
        Check("and shortens the vector by exactly what it removed",
            survivors?.End == survivors?.Begin + 8);

        // The same compaction, reached the other way: a disease's penalties are stripped from every
        // group, and the racial modifier beside them in group 2 is not.
        var (diseased, _) = FakeGame.BuildAfflictedGame();
        new TrainerActions(diseased, image).CureConditions(record);
        var group2 = ConditionReader.ReadGroup(diseased, record, 2);
        Check("stripping a disease's effects compacts the group it was in",
            group2 is { Effects.Count: 1 } && group2.End == group2.Begin + 4);

        // An affliction the game itself would not cure is reported rather than forced away.
        var (stuck, stuckHeap) = FakeGame.BuildAfflictedGame();
        stuckHeap.SetGroup(ConditionHeap.CurseGroup);
        stuckHeap.SetGroup(ConditionHeap.ParalysisGroup);
        stuckHeap.SetDiseasesRaw(0, 0);
        stuckHeap.SetGroup(ConditionHeap.PoisonGroup,
            stuckHeap.AddEffect(3, 0, ConditionLayout.SourceEquipment, group: ConditionHeap.PoisonGroup));
        uint stuckEnd = GroupEnd(stuck, record, ConditionHeap.PoisonGroup);
        var refusedQuietly = new TrainerActions(stuck, image).CureConditions(record);
        Check("an uncurable affliction is not forced away", refusedQuietly.Ok);
        Check("and nothing was written for it", GroupEnd(stuck, record, ConditionHeap.PoisonGroup) == stuckEnd);
        Check("the reader says so on the line itself",
            ConditionReader.Read(stuck, record)?.Summary.Contains("not something a cure removes") == true);

        // Read-only and a record that stopped validating both refuse, as every other write does.
        var (locked, _) = FakeGame.BuildAfflictedGame();
        var lockedActions = new TrainerActions(locked, image) { ReadOnly = true };
        uint lockedEnd = GroupEnd(locked, record, ConditionHeap.PoisonGroup);
        Check("read-only refuses the cure", !lockedActions.CureConditions(record).Ok);
        Check("and writes nothing", GroupEnd(locked, record, ConditionHeap.PoisonGroup) == lockedEnd);

        var (broken, _) = FakeGame.BuildAfflictedGame();
        broken.PokeUInt32(record + QuestLayout.VTable, 0x1234_5678);
        var brokenResult = new TrainerActions(broken, image).CureConditions(record);
        Check("a record that stopped validating refuses the cure", !brokenResult.Ok);
        Check("and still holds its poison",
            ConditionReader.ReadGroup(broken, record, ConditionHeap.PoisonGroup)?.Effects.Count == 1);

        var (unreadable, unreadableHeap) = FakeGame.BuildAfflictedGame();
        unreadableHeap.SetGroupRaw(ConditionHeap.PoisonGroup, 0x1001, 0x1005);
        Check("conditions that cannot be read refuse the cure",
            !new TrainerActions(unreadable, image).CureConditions(record).Ok);

        // The freeze is the cure on repeat. Both halves are checked, so the first cannot pass for
        // the wrong reason: without the tick the poison really does stay.
        var (frozen, frozenHeap) = FakeGame.BuildAfflictedGame();
        var frozenActions = new TrainerActions(frozen, image);
        var freezes = new FreezeWriter();

        frozenActions.CureConditions(record);
        frozenHeap.SetGroup(ConditionHeap.PoisonGroup,
            frozenHeap.AddEffect(7, 0, source: 6, group: ConditionHeap.PoisonGroup));
        Check("without the freeze, a new poison stays",
            freezes.Tick(frozenActions, record) == 0 &&
            ConditionReader.Read(frozen, record)?.Any == true);

        freezes.Freeze(FrozenField.Conditions, 0);
        Check("the freeze counts as one write", freezes.Tick(frozenActions, record) == 1);
        Check("with the freeze, a new poison is taken off",
            ConditionReader.Read(frozen, record) is { Any: false });

        frozenHeap.SetGroup(ConditionHeap.ParalysisGroup,
            frozenHeap.AddEffect(0, 2, source: 6, group: ConditionHeap.ParalysisGroup));
        freezes.Tick(frozenActions, record);
        Check("and so is anything else the game inflicts",
            ConditionReader.Read(frozen, record) is { Any: false });

        freezes.Thaw(FrozenField.Conditions);
        Check("thawing stops it", !freezes.IsFrozen(FrozenField.Conditions));
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

    private static void MapLayoutChecks()
    {
        Section("map layout");

        Check("the manager hangs off the engine object at +0x98", MapLayout.EngineManager == 0x098);
        Check("the window's border and size are adjacent",
            MapLayout.WindowBorder == 0x44E8 && MapLayout.WindowSize == 0x44EC);
        Check("facing sits at +0x1570", MapLayout.Facing == 0x1570);
        Check("the position is a contiguous pair",
            MapLayout.PlayerX == 0x158C && MapLayout.PlayerY == 0x1590);
        Check("the world and the map are adjacent pointers",
            MapLayout.World == 0x21C8 && MapLayout.Map == 0x21CC);
        Check("the outdoor flag sits before them", MapLayout.Outdoors == 0x21C4);
        Check("the neighbour block follows the map pointer", MapLayout.NeighbourMaps == 0x21D0);
        Check("the player's own map is the middle of the block",
            MapLayout.NeighbourCentre == MapLayout.NeighbourCount / 2);
        Check("neighbour slots stride by a pointer",
            MapLayout.NeighbourSlot(0x1000, 3) == 0x1000 + MapLayout.NeighbourMaps + 12);
        Check("map slots stride by a pointer", MapLayout.MapSlot(0x1000, 5) == 0x1014);

        // The world's four strings really do abut, so each is stated from the one before it.
        Check("the world's strings tile",
            MapLayout.WorldName == 0x08 && MapLayout.WorldPack == 0x20 &&
            MapLayout.WorldIdPrefix == 0x38 && MapLayout.WorldDatabase == 0x54);
        Check("the world's map vector sits at +0x74",
            MapLayout.WorldMapsBegin == 0x74 && MapLayout.WorldMapsEnd == 0x78);
        Check("the world's cached tile pair is contiguous",
            MapLayout.WorldTileX == 0x90 && MapLayout.WorldTileY == 0x94);
        Check("the grid prefix and the picture id follow it",
            MapLayout.WorldGridPrefix == 0xA0 && MapLayout.WorldMapPicture == 0xBC);
        Check("the world snapshot covers the picture id",
            MapLayout.WorldBytes >= MapLayout.WorldMapPicture + StdString.Bytes);

        Check("a map's back-pointers come first",
            MapLayout.MapEngine == 0x00 && MapLayout.MapWorld == 0x04);
        Check("a map's id and name are adjacent pointers",
            MapLayout.MapId == 0x0C && MapLayout.MapName == 0x10);
        Check("a map's size is a contiguous pair",
            MapLayout.MapWidth == 0x2C && MapLayout.MapHeight == 0x30);
        Check("the map snapshot covers the flag word",
            MapLayout.MapFlags == 0x40 && MapLayout.MapBytes >= MapLayout.MapFlags + 2);

        // The flags were read off the game's own branches: bit 7 is the one that decides where a map
        // is laid into the window, and getting it wrong puts every teleport 14 tiles out.
        Check("the flag bits are the game's",
            MapLayout.FlagMarkDenied == 0x0008 && MapLayout.FlagOffsetByBorder == 0x0080 &&
            MapLayout.FlagRecallTarget == 0x0200 && MapLayout.FlagTeleportDenied == 0x0400);

        Check("an outdoor id splits into its one-based cell",
            MapLayout.CellFromId("base_s0804", "base_s") == (8, 4));
        Check("and the first cell is 0101",
            MapLayout.CellFromId("base_s0101", "base_s") == (1, 1));
        Check("an interior has no cell", MapLayout.CellFromId("base_house7", "base_s") is null);
        Check("a short id has no cell", MapLayout.CellFromId("base_s080", "base_s") is null);
        Check("a long id has no cell", MapLayout.CellFromId("base_s08041", "base_s") is null);
        Check("a non-numeric id has no cell", MapLayout.CellFromId("base_s08a4", "base_s") is null);
        Check("cell 0000 is refused", MapLayout.CellFromId("base_s0004", "base_s") is null);
        Check("row 00 is refused", MapLayout.CellFromId("base_s0800", "base_s") is null);
        Check("another world's prefix does not match",
            MapLayout.CellFromId("base_s0804", "isle_s") is null);
        Check("an empty id has no cell", MapLayout.CellFromId("", "base_s") is null);
        Check("an empty prefix has no cell", MapLayout.CellFromId("base_s0804", "") is null);

        Check("cell 1 starts the world", MapLayout.CellOriginTile(1) == 0);
        Check("cell 8 starts 147 tiles in", MapLayout.CellOriginTile(8) == 147);
        Check("a cell is 21 tiles", MapLayout.GridMapTiles == 21);
    }

    private static void MapReaderChecks()
    {
        Section("map reader");

        var (mem, heap) = FakeGame.BuildGameWithMap();
        var where = MapReader.Read(mem, FakeGame.LiveRecord);

        Check("the position reads", where is not null);
        if (where is null) return;

        Check("the world names itself", where.WorldName == MapHeap.WorldName);
        Check("and its pack and grid prefix",
            where.WorldPack == MapHeap.Pack && where.GridPrefix == MapHeap.GridPrefix);
        Check("and its map picture", where.PictureId == MapHeap.Picture);
        Check("the current map is named", where.Here is { Id: "base_s0804", Name: "Port of Mithria" });
        Check("and sized", where.Here.Width == 21 && where.Here.Height == 21);
        Check("and placed in the world", where.Here.Column == 8 && where.Here.Row == 4);
        Check("its origin is where the cell starts",
            where.Here.OriginX == 147 && where.Here.OriginY == 63);
        Check("the window is the border either side of a cell",
            where.WindowBorder == MapHeap.Border && where.WindowSize == MapHeap.Border * 2 + 21);
        Check("the outdoor flag is read", where.Outdoors);

        // The whole feature turns on this subtraction: an outdoor map is laid into the window a
        // border in, so local is window minus border and nothing else.
        Check("the window position is local plus the border",
            where.WindowX == 25 && where.WindowY == 23);
        Check("local is window minus the border", where.LocalX == 11 && where.LocalY == 9);
        Check("the player is on their own map", where.IsOnMap);

        // 147 + 11 and 63 + 9 — and the numbers on the right came out of the running game, so this
        // is not the same arithmetic checking itself.
        Check("the world-absolute tile is the cell's origin plus local",
            where.GlobalX == 158 && where.GlobalY == 72);
        Check("and it agrees with the pair the engine caches",
            where.GlobalX == where.CachedWorldTileX && where.GlobalY == where.CachedWorldTileY);

        Check("the map's flags are read", (where.Here.Flags & MapLayout.FlagRecallTarget) != 0);
        Check("and described", where.Here.Notes.Contains("Recall target"));

        Check("north is zero", where.Heading == Heading.North && where.HeadingLabel == "North");
        heap.SetPosition(25, 23, 90);
        Check("ninety is west", MapReader.Read(mem, FakeGame.LiveRecord)?.Heading == Heading.West);
        heap.SetPosition(25, 23, 180);
        Check("a hundred and eighty is south", MapReader.Read(mem, FakeGame.LiveRecord)?.Heading == Heading.South);
        heap.SetPosition(25, 23, 270);
        Check("two hundred and seventy is east", MapReader.Read(mem, FakeGame.LiveRecord)?.Heading == Heading.East);
        heap.SetPosition(25, 23, 45);
        Check("a turn in progress is neither", MapReader.Read(mem, FakeGame.LiveRecord)?.Heading == Heading.Unknown);
        Check("and is shown as the raw angle", MapReader.Read(mem, FakeGame.LiveRecord)?.HeadingLabel == "45°");
        heap.SetPosition(25, 23);

        // An interior carries no cell and is laid at the window's origin rather than the border, so
        // the same window position means a different tile. This is the case the flag exists for.
        uint interior = heap.Maps[3];
        heap.SetCurrentMap(interior);
        heap.SetOutdoors(false);
        heap.SetPosition(4, 6);
        var inside = MapReader.Read(mem, FakeGame.LiveRecord);
        Check("an interior is read", inside is not null);
        Check("it is not a cell of the grid", inside is { Here.IsOutdoorCell: false });
        Check("it is 35 tiles square", inside is { Here.Width: 35, Here.Height: 35 });
        Check("it is laid at the window's origin", inside is { LocalX: 4, LocalY: 6 });
        Check("so it has no world-absolute tile", inside?.GlobalX is null);
        Check("and the outdoor flag is clear", inside is { Outdoors: false });
        Check("its flags are described",
            inside is not null && inside.Here.Notes.Contains("Teleport magic denied") &&
            inside.Here.Notes.Contains("Mark denied"));

        // Standing outside the map's own tiles is what a cross-map teleport would produce, and the
        // reader has to say so rather than report a negative coordinate as if it were fine.
        heap.SetPosition(0, 0);
        Check("a tile outside the map is reported",
            MapReader.Read(mem, FakeGame.LiveRecord) is { IsOnMap: true });
        heap.SetCurrentMap(heap.Maps[0]);
        heap.SetOutdoors(true);
        heap.SetPosition(2, 2);
        Check("and so is one before an outdoor map's border",
            MapReader.Read(mem, FakeGame.LiveRecord) is { IsOnMap: false, LocalX: -12 });
        heap.SetPosition(25, 23);

        // --- the atlas ---
        var atlas = MapReader.ReadAtlas(mem, FakeGame.LiveRecord);
        Check("the atlas has every map", atlas.Count == 4);
        Check("its outdoor cells carry their column and row",
            atlas.Count(m => m.IsOutdoorCell) == 3);
        Check("and its interior does not",
            atlas.Single(m => !m.IsOutdoorCell).Id == FakeGame.InteriorId);
        Check("the current map is in it", atlas.Any(m => m.Id == "base_s0804"));
        Check("a cell reads as its column and row", atlas.First(m => m.Id == "base_s0704").CellLabel == "7, 4");
        Check("an interior's cell is a dash",
            atlas.Single(m => !m.IsOutdoorCell).CellLabel == "—");
        Check("the size is shown the way the game words it",
            atlas.First(m => m.Id == "base_s0101").SizeLabel == "21×21");

        // --- the validators ---
        heap.SetMapWorld(heap.Maps[1], 0xDEAD_BEEF);
        Check("a map that belongs to another world is dropped",
            MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 3);
        heap.SetMapWorld(heap.Maps[1], MapHeap.WorldBase);

        heap.SetMapWidth(heap.Maps[1], 4000);
        Check("a map with an implausible size is dropped",
            MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 3);
        heap.SetMapWidth(heap.Maps[1], 21);

        heap.SetMapName(heap.Maps[1], 0x7FFF_0000);
        Check("a map whose name cannot be read is dropped",
            MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 3);
        heap.SetMapName(heap.Maps[1], 0);
        Check("and so is one with no name at all",
            MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 3);

        heap.SetMapsRaw(MapHeap.VectorBase, MapHeap.VectorBase - 4);
        Check("a misordered vector yields nothing", MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 0);
        heap.SetMapsRaw(MapHeap.VectorBase, MapHeap.VectorBase + 6);
        Check("and so does a misaligned one", MapReader.ReadAtlas(mem, FakeGame.LiveRecord).Count == 0);

        var (fresh, clean) = FakeGame.BuildGameWithMap();
        clean.SetWorldEngine(0xDEAD_BEEF);
        Check("a world that is not this engine's is refused",
            MapReader.Read(fresh, FakeGame.LiveRecord) is null);

        var (fresh2, clean2) = FakeGame.BuildGameWithMap();
        clean2.SetMapWorld(clean2.Maps[0], 0xDEAD_BEEF);
        Check("and a current map that is not this world's",
            MapReader.Read(fresh2, FakeGame.LiveRecord) is null);

        var (fresh3, clean3) = FakeGame.BuildGameWithMap();
        clean3.ClearManager();
        Check("no manager means no position, not a guess",
            MapReader.Read(fresh3, FakeGame.LiveRecord) is null);
        Check("and no atlas either",
            MapReader.ReadAtlas(fresh3, FakeGame.LiveRecord).Count == 0);

        // A game with no map graph at all is the title screen, and the tab has to cope with it.
        Check("a record with no manager behind it reads as no position",
            MapReader.Read(FakeGame.BuildGame(), FakeGame.LiveRecord) is null);
    }

    private static void TeleportChecks()
    {
        Section("teleport");

        var (mem, heap) = FakeGame.BuildGameWithMap();
        var actions = new TrainerActions(mem, FakeGame.Image(mem));

        var moved = actions.Teleport(FakeGame.LiveRecord, 3, 17);
        Check("a teleport within the map succeeds", moved.Ok);
        Check("and says where it went", moved.Message.Contains("(3, 17)") && moved.Message.Contains("Port of Mithria"));
        Check("it writes local plus the border",
            heap.WindowX == 3 + MapHeap.Border && heap.WindowY == 17 + MapHeap.Border);
        Check("and the reader agrees",
            MapReader.Read(mem, FakeGame.LiveRecord) is { LocalX: 3, LocalY: 17 });
        Check("the world-absolute tile follows it",
            MapReader.Read(mem, FakeGame.LiveRecord)?.GlobalX == 150);

        Check("the corner is reachable", actions.Teleport(FakeGame.LiveRecord, 0, 0).Ok);
        Check("and so is the far corner", actions.Teleport(FakeGame.LiveRecord, 20, 20).Ok);

        // Confining the target to the current map is the load-bearing refusal: outdoors the window
        // holds the neighbours too, so one tile past the edge is a real, drawn tile of another map.
        int before = heap.WindowX;
        var off = actions.Teleport(FakeGame.LiveRecord, 21, 5);
        Check("a column past the edge is refused", !off.Ok);
        Check("and says how big the map is", off.Message.Contains("21×21"));
        Check("a row past the edge is refused", !actions.Teleport(FakeGame.LiveRecord, 5, 21).Ok);
        Check("a negative column is refused", !actions.Teleport(FakeGame.LiveRecord, -1, 5).Ok);
        Check("a negative row is refused", !actions.Teleport(FakeGame.LiveRecord, 5, -1).Ok);
        Check("and a refusal writes nothing", heap.WindowX == before);

        // An interior is laid at the window's origin, so the same local tile is a different write.
        heap.SetCurrentMap(heap.Maps[3]);
        heap.SetOutdoors(false);
        Check("an interior teleport succeeds", actions.Teleport(FakeGame.LiveRecord, 3, 17).Ok);
        Check("and writes local with no border at all", heap.WindowX == 3 && heap.WindowY == 17);
        Check("its 35 tiles are all reachable", actions.Teleport(FakeGame.LiveRecord, 34, 34).Ok);
        Check("and 35 is not", !actions.Teleport(FakeGame.LiveRecord, 35, 0).Ok);
        heap.SetCurrentMap(heap.Maps[0]);
        heap.SetOutdoors(true);

        actions.ReadOnly = true;
        before = heap.WindowX;
        var refused = actions.Teleport(FakeGame.LiveRecord, 1, 1);
        Check("read-only refuses a teleport", !refused.Ok && refused.Message.Contains("Read-only"));
        Check("and writes nothing", heap.WindowX == before);
        actions.ReadOnly = false;

        // Same rule as every other write: the record has to still be there.
        var (gone, _) = FakeGame.BuildGameWithMap();
        gone.PokeUInt32(FakeGame.LiveRecord + QuestLayout.ExperienceTable, 12345);
        var stale = new TrainerActions(gone, FakeGame.Image(gone))
            .Teleport(FakeGame.LiveRecord, 1, 1);
        Check("a record that no longer validates refuses a teleport", !stale.Ok);
        Check("and says so", stale.Message.Contains("moved or went away"));

        var (blind, blindHeap) = FakeGame.BuildGameWithMap();
        blindHeap.ClearManager();
        var nowhere = new TrainerActions(blind, FakeGame.Image(blind)).Teleport(FakeGame.LiveRecord, 1, 1);
        Check("no world means no teleport", !nowhere.Ok);
        Check("and it says why", nowhere.Message.Contains("where the player is"));

        // Teleport magic being denied is the game's rule for its own spell, not for the trainer —
        // but the message has to say so, or the player thinks the trainer is broken.
        var (denied, deniedHeap) = FakeGame.BuildGameWithMap();
        deniedHeap.SetMapFlags(deniedHeap.Maps[0],
            MapLayout.FlagOffsetByBorder | MapLayout.FlagTeleportDenied);
        var anyway = new TrainerActions(denied, FakeGame.Image(denied)).Teleport(FakeGame.LiveRecord, 2, 2);
        Check("a teleport-denied map is still reachable", anyway.Ok);
        Check("and the message says the game's own spell would not be",
            anyway.Message.Contains("Teleport magic is denied"));
    }

    private static void DdsChecks()
    {
        Section("dds");

        Check("a non-DDS is refused", DdsImage.Decode(new byte[200], out _) is null);
        Check("and says so", Refusal(new byte[200]).Contains("not a DDS"));

        Check("a truncated DDS is refused", DdsImage.Decode(Dds(8, 8, 1), out _) is null);
        Check("and says how short it is", Refusal(Dds(8, 8, 1)).Contains("truncated"));

        var dxt3 = Dds(4, 4, 1, fourCc: "DXT3");
        Check("an unsupported compression is refused", DdsImage.Decode(dxt3, out _) is null);
        Check("and names it", Refusal(dxt3).Contains("DXT3"));

        Check("implausible dimensions are refused", DdsImage.Decode(Dds(0, 8, 1), out _) is null);

        // One opaque block: endpoints red and blue with c0 > c1, so the palette is red, blue and two
        // thirds-of-the-way colours, and the indices pick one of each.
        var opaque = Dds(4, 4, 1);
        WriteBlock(opaque, 128, 0xF800, 0x001F, 0b_11_10_01_00);
        var image = DdsImage.Decode(opaque, out string detail);
        Check("an opaque DXT1 block decodes", image is not null);
        Check("with its dimensions", image is { Width: 4, Height: 4 });
        Check("and says what it decoded", detail.Contains("4×4 DXT1"));
        Check("index 0 is the first endpoint", image?.Pixel(0, 0) == 0xFFFF0000);
        Check("index 1 is the second", image?.Pixel(1, 0) == 0xFF0000FF);
        Check("index 2 is a third of the way", image?.Pixel(2, 0) == 0xFFAA0055);
        Check("index 3 is two thirds", image?.Pixel(3, 0) == 0xFF5500AA);
        Check("every pixel of the block is filled", image?.Pixel(3, 3) == 0xFF5500AA);

        // The other palette: c0 <= c1 means one midpoint and a transparent fourth entry, and reading
        // it the opaque way would silently turn holes into black.
        var punched = Dds(4, 4, 1);
        WriteBlock(punched, 128, 0x001F, 0xF800, 0b_11_10_01_00);
        var holes = DdsImage.Decode(punched, out _);
        Check("the transparent palette decodes", holes is not null);
        Check("index 0 and 1 are still the endpoints",
            holes?.Pixel(0, 0) == 0xFF0000FF && holes?.Pixel(1, 0) == 0xFFFF0000);
        Check("index 2 is the midpoint", holes?.Pixel(2, 0) == 0xFF7F007F);
        Check("index 3 is transparent", (holes?.Pixel(3, 0) & 0xFF000000) == 0);

        Check("a pixel outside the surface reads as nothing", image?.Pixel(9, 9) == 0);
        Check("the stride is four bytes a pixel", image?.Stride == 16);

        // A surface that is not a multiple of four still decodes: the last block is clipped.
        var odd = Dds(6, 6, 4);
        for (int i = 0; i < 4; i++) WriteBlock(odd, 128 + i * 8, 0xF800, 0x001F, 0);
        Check("a size that is not a multiple of four decodes", DdsImage.Decode(odd, out _) is { Width: 6, Height: 6 });
    }

    private static void WorldPictureChecks()
    {
        Section("world picture");

        Check("a resource id becomes a pak entry",
            WorldPictureLoader.EntryFor("base", "base_-WORLDMAP-") == "worlds/base/-WORLDMAP-.dds");
        Check("only the leading pack name is stripped",
            WorldPictureLoader.EntryFor("base", "base_base_map") == "worlds/base/base_map.dds");
        Check("an id that does not carry the prefix is taken as it is",
            WorldPictureLoader.EntryFor("isle", "-WORLDMAP-") == "worlds/isle/-WORLDMAP-.dds");

        Check("no game folder means no picture",
            WorldPictureLoader.Load(null, "base", "base_-WORLDMAP-", 294, out _) is null);
        Check("and says so", Note(null, "base", "base_-WORLDMAP-").Contains("game folder"));
        Check("a folder that does not exist means no picture",
            WorldPictureLoader.Load(@"C:\no\such\folder", "base", "base_-WORLDMAP-", 294, out _) is null);
        Check("a world with no picture id means no picture",
            WorldPictureLoader.Load(Path.GetTempPath(), "base", "", 294, out _) is null);

        // The whole path, against a pak this harness writes itself: no game files are involved.
        string folder = Path.Combine(Path.GetTempPath(), "TheQuestTrainer.FormatCheck." + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var surface = Dds(8, 8, 4);
            for (int i = 0; i < 4; i++) WriteBlock(surface, 128 + i * 8, 0xF800, 0x001F, 0);
            WritePak(Path.Combine(folder, "data.pak"), "worlds/base/-WORLDMAP-.dds", surface);

            var picture = WorldPictureLoader.Load(folder, "base", "base_-WORLDMAP-", 4, out string note);
            Check("a picture in a pak is found and decoded", picture is not null);
            Check("its scale comes from how wide the grid is", picture?.PixelsPerTile == 2);
            Check("a tile maps to the middle of its pixels", picture?.PixelX(3) == 7);
            Check("and the note names the pak", note.Contains("data.pak"));

            Check("a picture that is not in any pak is not invented",
                WorldPictureLoader.Load(folder, "isle", "isle_-WORLDMAP-", 294, out _) is null);
            Check("and the note says which entry was wanted",
                Note(folder, "isle", "isle_-WORLDMAP-").Contains("worlds/isle/-WORLDMAP-.dds"));

            // An expansion's pak lives one folder down and has to be searched too.
            string expansions = Path.Combine(folder, "expansions");
            Directory.CreateDirectory(expansions);
            WritePak(Path.Combine(expansions, "isle.pak"), "worlds/isle/-WORLDMAP-.dds", surface);
            Check("an expansion's pak is searched as well",
                WorldPictureLoader.Load(folder, "isle", "isle_-WORLDMAP-", 4, out _) is not null);

            // A pak holding an entry that is not a DDS must not produce a picture built from noise.
            WritePak(Path.Combine(folder, "broken.pak"), "worlds/mod/-WORLDMAP-.dds", new byte[300]);
            Check("an entry that will not decode yields no picture",
                WorldPictureLoader.Load(folder, "mod", "mod_-WORLDMAP-", 294, out _) is null);
            Check("and the note says why", Note(folder, "mod", "mod_-WORLDMAP-").Contains("could not be decoded"));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { /* best effort */ }
        }
    }

    private static void MapViewChecks()
    {
        Section("map tab");

        var (mem, heap) = FakeGame.BuildGameWithMap();
        var host = new FakeHost(mem);
        var view = new MapViewModel(host);

        Check("nothing is shown before a position arrives", !view.HasPosition && view.Tiles.Count == 0);

        view.Update(MapReader.Read(mem, FakeGame.LiveRecord));
        Check("a position fills the readout", view is { HasPosition: true, MapName: "Port of Mithria" });
        Check("the schematic is the map's own size",
            view is { TileColumns: 21, TileRows: 21 } && view.Tiles.Count == 441);
        Check("the player's square is marked",
            view.Tiles.Single(t => t.IsPlayer) is { X: 11, Y: 9 });

        // A first position aims the target at the tile the player is on, so Teleport with nothing
        // typed is a no-op rather than a jump to (0, 0).
        Check("the target starts where the player is", view is { TargetX: 11, TargetY: 9 });

        view.TargetX = 4;
        view.TargetY = 16;
        Check("moving the target moves the marker",
            view.Tiles.Single(t => t.IsTarget) is { X: 4, Y: 16 });

        view.SelectTileCommand.Execute(view.Tiles.First(t => t is { X: 7, Y: 2 }));
        Check("clicking a square aims at it", view is { TargetX: 7, TargetY: 2 });

        view.TeleportCommand.Execute(null);
        Check("Teleport writes through the host", heap.WindowX == 7 + MapHeap.Border);
        Check("and the host is told where it went", host.Reported[^1].Contains("(7, 2)"));
        view.Update(MapReader.Read(mem, FakeGame.LiveRecord));
        Check("so the next position marks the new square",
            view.Tiles.Single(t => t.IsPlayer) is { X: 7, Y: 2 });

        host.IsReadOnly = true;
        view.SelectTileCommand.Execute(view.Tiles.First(t => t is { X: 1, Y: 1 }));
        view.TeleportCommand.Execute(null);
        Check("read-only stops the tab writing", heap.WindowX == 7 + MapHeap.Border);
        Check("and says so", host.Reported[^1].Contains("Read-only"));
        host.IsReadOnly = false;

        // Walking into a building replaces a 21x21 schematic with a 35x35 one.
        heap.SetCurrentMap(heap.Maps[3]);
        heap.SetOutdoors(false);
        heap.SetPosition(2, 3);
        view.Update(MapReader.Read(mem, FakeGame.LiveRecord));
        Check("a new map rebuilds the schematic", view.Tiles.Count == 35 * 35);
        Check("and the interior's tiles are plain", view is { TileColumns: 35, TileRows: 35 });
        Check("and the target comes with you rather than staying on the old map",
            view is { TargetX: 2, TargetY: 3 });

        view.SetAtlas(MapReader.ReadAtlas(mem, FakeGame.LiveRecord));
        Check("the atlas is listed", view.AtlasView.Count == 4);
        view.AtlasFilter = "sea";
        Check("and can be narrowed by name", view.AtlasView.Count == 1);
        view.AtlasFilter = "house";
        Check("or by the internal id", view.AtlasView.Single().Id == FakeGame.InteriorId);
        view.AtlasFilter = "";
        Check("and widened again", view.AtlasView.Count == 4);

        view.Update(null);
        Check("losing the position empties the tab", !view.HasPosition && view.Tiles.Count == 0);
        Check("and forgets the atlas with it", view.AtlasView.Count == 0);
    }

    // ---- plumbing ------------------------------------------------------------------------------

    /// <summary>Why <see cref="DdsImage"/> refused these bytes.</summary>
    private static string Refusal(byte[] dds)
    {
        DdsImage.Decode(dds, out string detail);
        return detail;
    }

    /// <summary>What <see cref="WorldPictureLoader"/> said about a load.</summary>
    private static string Note(string? folder, string pack, string id)
    {
        WorldPictureLoader.Load(folder, pack, id, 294, out string note);
        return note;
    }

    /// <summary>A DDS header plus room for <paramref name="blocks"/> BC1 blocks.</summary>
    private static byte[] Dds(int width, int height, int blocks, string fourCc = "DXT1")
    {
        var dds = new byte[128 + blocks * 8];
        BitConverter.GetBytes(0x20534444).CopyTo(dds, 0);          // "DDS "
        BitConverter.GetBytes(124).CopyTo(dds, 4);
        BitConverter.GetBytes(height).CopyTo(dds, 12);
        BitConverter.GetBytes(width).CopyTo(dds, 16);
        System.Text.Encoding.ASCII.GetBytes(fourCc).CopyTo(dds, 84);
        return dds;
    }

    /// <summary>Writes one BC1 block: two RGB565 endpoints and sixteen two-bit indices.</summary>
    private static void WriteBlock(byte[] dds, int at, ushort c0, ushort c1, uint rowIndices)
    {
        BitConverter.GetBytes(c0).CopyTo(dds, at);
        BitConverter.GetBytes(c1).CopyTo(dds, at + 2);
        uint bits = rowIndices | rowIndices << 8 | rowIndices << 16 | rowIndices << 24;
        BitConverter.GetBytes(bits).CopyTo(dds, at + 4);
    }

    /// <summary>Writes a one-entry pak — the game's paks are ordinary zips.</summary>
    private static void WritePak(string path, string entry, byte[] content)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
        using var stream = zip.CreateEntry(entry).Open();
        stream.Write(content, 0, content.Length);
    }

    private static CharacterSnapshot? Read(IMemorySource mem, uint record) => CharacterReader.Read(mem, record);

    /// <summary>The one mutable word of the item at <paramref name="item"/>.</summary>
    private static int Meter(IMemorySource mem, uint item)
    {
        var word = new byte[2];
        return mem.Read(item + ItemLayout.ItemCondition, word, 2) == 2 ? BitConverter.ToUInt16(word, 0) : -1;
    }

    /// <summary>The <c>end</c> pointer of an effect group, for the checks that assert nothing moved.</summary>
    private static uint GroupEnd(IMemorySource mem, uint record, int group)
    {
        var word = new byte[4];
        return mem.Read(ConditionLayout.EffectGroupEnd(record, group), word, 4) == 4
            ? BitConverter.ToUInt32(word, 0)
            : 0;
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
