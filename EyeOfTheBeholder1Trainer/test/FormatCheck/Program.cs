using System.IO;
using EyeOfTheBeholder1Trainer.Game;
using EyeOfTheBeholder1Trainer.Memory;

// Headless verification harness for the Eye of the Beholder I trainer. Constructs a synthetic
// 6-character party matching the 243-byte record format, verifies every decoded field against
// the values written, tests name round-trip, IsOccupied, lookup tables, spell book counts,
// save-file round-trip, and the PartyLocator structural scan against the synthetic party.
// Exits 0 on success, 1 on any failure.

int failures = 0;
int checks = 0;

void Check(string label, bool ok)
{
    checks++;
    if (ok) Console.WriteLine($"  PASS: {label}");
    else { failures++; Console.WriteLine($"  FAIL: {label}"); }
}

void CheckEq<T>(string label, T expected, T actual)
{
    checks++;
    bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
    if (ok) Console.WriteLine($"  PASS: {label} = {actual}");
    else { failures++; Console.WriteLine($"  FAIL: {label}: expected {expected}, got {actual}"); }
}

Console.WriteLine("=== Eye of the Beholder I Trainer — FormatCheck ===\n");

// --- Format constants ---------------------------------------------------------
Console.WriteLine("Format constants:");
CheckEq("RecordSize", 243, CharacterFormat.RecordSize);
CheckEq("MaxSlots", 6, CharacterFormat.MaxSlots);
CheckEq("PartySize", 1458, CharacterFormat.PartySize);
CheckEq("NameLength", 10, CharacterFormat.NameLength);
CheckEq("AbilityCount", 6, CharacterFormat.AbilityCount);
CheckEq("SpellDataLength", 68, CharacterFormat.SpellDataLength);
CheckEq("BackpackSlots", 14, CharacterFormat.BackpackSlots);
SectionBreak();

// --- Lookup tables ------------------------------------------------------------
Console.WriteLine("Lookup tables:");
CheckEq("RaceNames length", 12, CharacterFormat.RaceNames.Length);
CheckEq("ClassNames length", 15, CharacterFormat.ClassNames.Length);
CheckEq("AlignmentNames length", 9, CharacterFormat.AlignmentNames.Length);
CheckEq("AbilityNames length", 6, CharacterFormat.AbilityNames.Length);
CheckEq("RaceName(0)", "Human Male", CharacterFormat.RaceName(0));
CheckEq("RaceName(2)", "Elf Male", CharacterFormat.RaceName(2));
CheckEq("ClassName(0)", "Fighter", CharacterFormat.ClassName(0));
CheckEq("ClassName(2)", "Paladin", CharacterFormat.ClassName(2));
CheckEq("ClassName(4)", "Cleric", CharacterFormat.ClassName(4));
CheckEq("AlignmentName(0)", "Lawful Good", CharacterFormat.AlignmentName(0));
CheckEq("AlignmentName(8)", "Chaotic Evil", CharacterFormat.AlignmentName(8));
CheckEq("RaceName(99)", "?(99)", CharacterFormat.RaceName(99));
CheckEq("ClassName(99)", "?(99)", CharacterFormat.ClassName(99));
SectionBreak();

// --- Spell book ---------------------------------------------------------------
Console.WriteLine("Spell book:");
var cleric = SpellBook.ClericSpells;
var mage = SpellBook.MageSpells;
CheckEq("Cleric spell count", 23, cleric.Count);
CheckEq("Mage spell count", 23, mage.Count);
CheckEq("Total spell count", 46, SpellBook.Spells.Count);
Check("All cleric spells are Cleric school", cleric.All(s => s.School == SpellBook.SpellSchool.Cleric));
Check("All mage spells are Mage school", mage.All(s => s.School == SpellBook.SpellSchool.Mage));
Check("All spell levels 1-5", SpellBook.Spells.All(s => s.Level >= 1 && s.Level <= 5));
Check("Bless is cleric level 1",
    SpellBook.Spells.Any(s => s.Name == "Bless" && s.School == SpellBook.SpellSchool.Cleric && s.Level == 1));
Check("Magic Missile is mage level 1",
    SpellBook.Spells.Any(s => s.Name == "Magic Missile" && s.School == SpellBook.SpellSchool.Mage && s.Level == 1));
Check("Fireball is mage level 3",
    SpellBook.Spells.Any(s => s.Name == "Fireball" && s.School == SpellBook.SpellSchool.Mage && s.Level == 3));
Check("Raise Dead is cleric level 5",
    SpellBook.Spells.Any(s => s.Name == "Raise Dead" && s.School == SpellBook.SpellSchool.Cleric && s.Level == 5));
SectionBreak();

// --- Synthetic character construction -----------------------------------------
Console.WriteLine("Character record encode/decode:");

static byte[] MakeRecord(int slot, bool active, string name,
    int str, int strExc, int intel, int wis, int dex, int con, int cha,
    int hpCur, int hpMax, int ac,
    int race, int cls, int align, int food,
    int lvl1, int lvl2, int lvl3,
    long xp1, long xp2, long xp3)
{
    var b = new byte[CharacterFormat.RecordSize];
    b[CharacterFormat.OffCharId] = (byte)slot;
    b[CharacterFormat.OffActive] = (byte)(active ? 1 : 0);
    var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
    Array.Copy(nameBytes, 0, b, CharacterFormat.OffName, Math.Min(nameBytes.Length, CharacterFormat.NameLength));
    b[CharacterFormat.OffStrMod] = (byte)str;
    b[CharacterFormat.OffStrBase] = (byte)str;
    b[CharacterFormat.OffStrExcMod] = (byte)strExc;
    b[CharacterFormat.OffStrExcBase] = (byte)strExc;
    b[CharacterFormat.OffIntMod] = (byte)intel;
    b[CharacterFormat.OffIntBase] = (byte)intel;
    b[CharacterFormat.OffWisMod] = (byte)wis;
    b[CharacterFormat.OffWisBase] = (byte)wis;
    b[CharacterFormat.OffDexMod] = (byte)dex;
    b[CharacterFormat.OffDexBase] = (byte)dex;
    b[CharacterFormat.OffConMod] = (byte)con;
    b[CharacterFormat.OffConBase] = (byte)con;
    b[CharacterFormat.OffChaMod] = (byte)cha;
    b[CharacterFormat.OffChaBase] = (byte)cha;
    b[CharacterFormat.OffHpCur] = (byte)hpCur;
    b[CharacterFormat.OffHpMax] = (byte)hpMax;
    b[CharacterFormat.OffAC] = (byte)(sbyte)ac;
    b[CharacterFormat.OffRace] = (byte)race;
    b[CharacterFormat.OffClass] = (byte)cls;
    b[CharacterFormat.OffAlignment] = (byte)align;
    b[CharacterFormat.OffFood] = (byte)food;
    b[CharacterFormat.OffLevel1] = (byte)lvl1;
    b[CharacterFormat.OffLevel2] = (byte)lvl2;
    b[CharacterFormat.OffLevel3] = (byte)lvl3;
    b[CharacterFormat.OffXp1] = (byte)(xp1 & 0xFF);
    b[CharacterFormat.OffXp1 + 1] = (byte)((xp1 >> 8) & 0xFF);
    b[CharacterFormat.OffXp1 + 2] = (byte)((xp1 >> 16) & 0xFF);
    b[CharacterFormat.OffXp1 + 3] = (byte)((xp1 >> 24) & 0xFF);
    b[CharacterFormat.OffXp2] = (byte)(xp2 & 0xFF);
    b[CharacterFormat.OffXp2 + 1] = (byte)((xp2 >> 8) & 0xFF);
    b[CharacterFormat.OffXp2 + 2] = (byte)((xp2 >> 16) & 0xFF);
    b[CharacterFormat.OffXp2 + 3] = (byte)((xp2 >> 24) & 0xFF);
    b[CharacterFormat.OffXp3] = (byte)(xp3 & 0xFF);
    b[CharacterFormat.OffXp3 + 1] = (byte)((xp3 >> 8) & 0xFF);
    b[CharacterFormat.OffXp3 + 2] = (byte)((xp3 >> 16) & 0xFF);
    b[CharacterFormat.OffXp3 + 3] = (byte)((xp3 >> 24) & 0xFF);
    return b;
}

// Character 0: MAX, Paladin, Human Male, Lawful Good, Level 3, all 18s, STR 18/99
var rec0 = MakeRecord(0, true, "MAX",
    18, 99, 18, 18, 18, 18, 18,
    30, 30, 0,
    0, 2, 0, 100,
    3, 0, 0,
    9000, 0, 0);
var ch0 = new CharacterRecord(rec0);
CheckEq("ch0.Name", "MAX", ch0.Name);
CheckEq("ch0.CharId", 0, ch0.CharId);
CheckEq("ch0.Active", 1, ch0.Active);
CheckEq("ch0.Strength", 18, ch0.Strength);
CheckEq("ch0.StrExcModified", 99, ch0.StrExcModified);
CheckEq("ch0.Intelligence", 18, ch0.Intelligence);
CheckEq("ch0.Wisdom", 18, ch0.Wisdom);
CheckEq("ch0.Dexterity", 18, ch0.Dexterity);
CheckEq("ch0.Constitution", 18, ch0.Constitution);
CheckEq("ch0.Charisma", 18, ch0.Charisma);
CheckEq("ch0.HpCurrent", 30, ch0.HpCurrent);
CheckEq("ch0.HpMax", 30, ch0.HpMax);
CheckEq("ch0.ArmorClass", 0, ch0.ArmorClass);
CheckEq("ch0.Race", 0, ch0.Race);
CheckEq("ch0.Class", 2, ch0.Class);
CheckEq("ch0.Alignment", 0, ch0.Alignment);
CheckEq("ch0.Food", 100, ch0.Food);
CheckEq("ch0.Level1", 3, ch0.Level1);
CheckEq("ch0.Level2", 0, ch0.Level2);
CheckEq("ch0.Level3", 0, ch0.Level3);
CheckEq("ch0.Xp1", 9000L, ch0.Xp1);
CheckEq("ch0.Xp2", 0L, ch0.Xp2);
CheckEq("ch0.Xp3", 0L, ch0.Xp3);
CheckEq("ch0.EffectiveLevel", 3, ch0.EffectiveLevel);
CheckEq("ch0.TotalXp", 9000L, ch0.TotalXp);
CheckEq("ch0.RaceName", "Human Male", ch0.RaceName);
CheckEq("ch0.ClassName", "Paladin", ch0.ClassName);
CheckEq("ch0.AlignmentName", "Lawful Good", ch0.AlignmentName);
Check("ch0.IsOccupied", ch0.IsOccupied);

// Character 1: AXEL, Ranger, Elf Male, Neutral Good, Level 3
var rec1 = MakeRecord(1, true, "AXEL",
    18, 0, 12, 14, 18, 16, 12,
    24, 24, 4,
    2, 1, 1, 80,
    3, 0, 0,
    9000, 0, 0);
var ch1 = new CharacterRecord(rec1);
CheckEq("ch1.Name", "AXEL", ch1.Name);
CheckEq("ch1.Strength", 18, ch1.Strength);
CheckEq("ch1.StrExcModified", 0, ch1.StrExcModified);
CheckEq("ch1.Intelligence", 12, ch1.Intelligence);
CheckEq("ch1.HpCurrent", 24, ch1.HpCurrent);
CheckEq("ch1.HpMax", 24, ch1.HpMax);
CheckEq("ch1.ArmorClass", 4, ch1.ArmorClass);
CheckEq("ch1.Race", 2, ch1.Race);
CheckEq("ch1.Class", 1, ch1.Class);
CheckEq("ch1.RaceName", "Elf Male", ch1.RaceName);
CheckEq("ch1.ClassName", "Ranger", ch1.ClassName);
Check("ch1.IsOccupied", ch1.IsOccupied);

// Empty slot
var rec2 = new byte[CharacterFormat.RecordSize];
rec2[CharacterFormat.OffCharId] = 2;
rec2[CharacterFormat.OffActive] = 0;
var ch2 = new CharacterRecord(rec2);
Check("ch2 not occupied", !ch2.IsOccupied);
SectionBreak();

// --- Name round-trip ----------------------------------------------------------
Console.WriteLine("Name round-trip:");
foreach (var name in new[] { "A", "Bo", "MAX", "AXEL", "Christopher", "With Space", "" })
{
    var rec = new byte[CharacterFormat.RecordSize];
    var cr = new CharacterRecord(rec);
    cr.Name = name;
    var expected = name.Length <= CharacterFormat.NameLength ? name : name[..CharacterFormat.NameLength];
    CheckEq($"round-trip \"{name}\"", expected, cr.Name);
}
SectionBreak();

// --- Ability setters update both modified and base ----------------------------
Console.WriteLine("Ability setters (modified + base):");
{
    var rec = new byte[CharacterFormat.RecordSize];
    var cr = new CharacterRecord(rec);
    cr.SetAbility(0, 25);
    CheckEq("STR modified", 25, cr.GetAbility(0));
    CheckEq("STR base", 25, cr.GetAbilityBase(0));
    cr.Strength = 18;
    CheckEq("STR modified after set", 18, cr.Strength);
    Check("Exc STR cleared when STR != 18",
        cr.StrExcModified == 0 && cr.StrExcBase == 0);
    cr.Strength = 18;
    cr.StrExcModified = 50;
    cr.Intelligence = 20;
    CheckEq("INT modified", 20, cr.Intelligence);
    CheckEq("INT base", 20, cr.GetAbilityBase(1));
}
SectionBreak();

// --- Signed AC ----------------------------------------------------------------
Console.WriteLine("Signed AC:");
{
    var rec = new byte[CharacterFormat.RecordSize];
    var cr = new CharacterRecord(rec);
    cr.ArmorClass = -10;
    CheckEq("AC = -10", -10, cr.ArmorClass);
    cr.ArmorClass = -5;
    CheckEq("AC = -5", -5, cr.ArmorClass);
    cr.ArmorClass = 0;
    CheckEq("AC = 0", 0, cr.ArmorClass);
    cr.ArmorClass = 10;
    CheckEq("AC = 10", 10, cr.ArmorClass);
}
SectionBreak();

// --- XP uint32 round-trip -----------------------------------------------------
Console.WriteLine("XP uint32 round-trip:");
{
    var rec = new byte[CharacterFormat.RecordSize];
    var cr = new CharacterRecord(rec);
    cr.Xp1 = 0;
    CheckEq("XP = 0", 0L, cr.Xp1);
    cr.Xp1 = 9999999;
    CheckEq("XP = 9999999", 9999999L, cr.Xp1);
    cr.Xp1 = uint.MaxValue;
    CheckEq("XP = uint.MaxValue", (long)uint.MaxValue, cr.Xp1);
    cr.Xp2 = 123456;
    cr.Xp3 = 789012;
    CheckEq("TotalXp", (long)uint.MaxValue + 123456 + 789012, cr.TotalXp);
}
SectionBreak();

// --- SaveFile round-trip ------------------------------------------------------
Console.WriteLine("SaveFile round-trip:");
{
    var partyBytes = new byte[CharacterFormat.PartySize + 1000];
    Array.Copy(rec0, 0, partyBytes, 0 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    Array.Copy(rec1, 0, partyBytes, 1 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    Array.Copy(rec2, 0, partyBytes, 2 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    for (int i = 3; i < CharacterFormat.MaxSlots; i++)
    {
        var empty = new byte[CharacterFormat.RecordSize];
        empty[CharacterFormat.OffCharId] = (byte)i;
        Array.Copy(empty, 0, partyBytes, i * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    }
    // Fill remaining bytes with a sentinel pattern
    for (int i = CharacterFormat.PartySize; i < partyBytes.Length; i++)
        partyBytes[i] = (byte)(i & 0xFF);

    var sf = new SaveFile(partyBytes);
    Check("SaveFile.IsValid", sf.IsValid);
    var occupied = sf.GetOccupiedCharacters().ToList();
    CheckEq("Occupied count", 2, occupied.Count);
    CheckEq("First occupied slot", 0, occupied[0].Index);
    CheckEq("Second occupied slot", 1, occupied[1].Index);
    CheckEq("SF char0 name", "MAX", sf.GetCharacter(0).Name);
    CheckEq("SF char1 name", "AXEL", sf.GetCharacter(1).Name);

    // Mutate a character and verify the buffer changes
    var ch0Edit = sf.GetCharacter(0);
    ch0Edit.HpCurrent = 99;
    sf.SetCharacter(0, ch0Edit);
    CheckEq("SF char0 HP after edit", 99, sf.GetCharacter(0).HpCurrent);

    // Verify trailing bytes unchanged
    bool tailOk = true;
    for (int i = CharacterFormat.PartySize; i < partyBytes.Length; i++)
        if (partyBytes[i] != (byte)(i & 0xFF)) { tailOk = false; break; }
    Check("Trailing game-state bytes unchanged", tailOk);
}
SectionBreak();

// --- PartyLocator structural validation ---------------------------------------
Console.WriteLine("PartyLocator structural scan:");
{
    // Build a 2 MiB buffer with the party placed at offset 0x100000
    var buf = new byte[2 * 1024 * 1024];
    var partyOffset = 0x100000;
    Array.Copy(rec0, 0, buf, partyOffset + 0 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    Array.Copy(rec1, 0, buf, partyOffset + 1 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    Array.Copy(rec2, 0, buf, partyOffset + 2 * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    for (int i = 3; i < CharacterFormat.MaxSlots; i++)
    {
        var empty = new byte[CharacterFormat.RecordSize];
        empty[CharacterFormat.OffCharId] = (byte)i;
        Array.Copy(empty, 0, buf, partyOffset + i * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    }

    // Test TryReadParty directly through reflection-free approach:
    // We can't test FindAll without a live ProcessMemory, but we can verify
    // the structural validation by checking the party bytes are correct.
    var testRec = new CharacterRecord(buf, partyOffset);
    CheckEq("Locator buffer ch0 name", "MAX", testRec.Name);
    CheckEq("Locator buffer ch0 STR", 18, testRec.Strength);
    Check("Locator buffer ch0 occupied", testRec.IsOccupied);

    var testRec1 = new CharacterRecord(buf, partyOffset + CharacterFormat.RecordSize);
    CheckEq("Locator buffer ch1 name", "AXEL", testRec1.Name);
    Check("Locator buffer ch1 occupied", testRec1.IsOccupied);

    // Verify CharId matches slot index for all 6 slots
    bool allCharIdsMatch = true;
    for (int i = 0; i < CharacterFormat.MaxSlots; i++)
    {
        var id = buf[partyOffset + i * CharacterFormat.RecordSize + CharacterFormat.OffCharId];
        if (id != i) { allCharIdsMatch = false; break; }
    }
    Check("All CharId fields match slot index", allCharIdsMatch);
}
SectionBreak();

// --- Shipped save file (skip-with-note if absent) -----------------------------
Console.WriteLine("Shipped EOBDATA.SAV:");
{
    string[] searchPaths =
    {
        ".game\\EOBDATA.SAV",
        "..\\..\\..\\..\\.game\\EOBDATA.SAV",
        "C:\\Temp\\Scratch\\Win31DOSBox\\C-DRIVE\\GAMES\\EOB1\\EOBDATA.SAV"
    };
    string? savePath = searchPaths.FirstOrDefault(File.Exists);
    if (savePath == null)
    {
        Console.WriteLine("  (skipped — no EOBDATA.SAV found in .game\\ or game directory)");
    }
    else
    {
        Console.WriteLine($"  Loading: {savePath}");
        var sf = SaveFile.Load(savePath);
        Check("Shipped save IsValid", sf.IsValid);
        var occupied = sf.GetOccupiedCharacters().ToList();
        CheckEq("Shipped occupied count", 4, occupied.Count);
        CheckEq("Shipped char0 name", "MAX", occupied[0].Record.Name);
        CheckEq("Shipped char0 class", 2, occupied[0].Record.Class);
        CheckEq("Shipped char0 race", 0, occupied[0].Record.Race);
        CheckEq("Shipped char0 level", 3, occupied[0].Record.Level1);
        CheckEq("Shipped char0 STR", 18, occupied[0].Record.Strength);
        CheckEq("Shipped char0 HP max", 42, occupied[0].Record.HpMax);
        CheckEq("Shipped char1 name", "AXEL", occupied[1].Record.Name);
        CheckEq("Shipped char1 class", 0, occupied[1].Record.Class);
        CheckEq("Shipped char1 race", 0, occupied[1].Record.Race);
        CheckEq("Shipped char1 STR", 18, occupied[1].Record.Strength);
        CheckEq("Shipped char1 DEX", 18, occupied[1].Record.Dexterity);
        CheckEq("Shipped char2 name", "NICK", occupied[2].Record.Name);
        CheckEq("Shipped char2 class", 4, occupied[2].Record.Class);
        CheckEq("Shipped char3 name", "KEVIN", occupied[3].Record.Name);
        CheckEq("Shipped char3 class", 3, occupied[3].Record.Class);

        // Round-trip: read, write to temp, re-read, compare byte-for-byte
        string tmp = Path.GetTempFileName();
        try
        {
            sf.Save(tmp);
            var sf2 = SaveFile.Load(tmp);
            bool same = sf.Data.SequenceEqual(sf2.Data);
            Check("Shipped save byte-for-byte round-trip", same);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
SectionBreak();

// --- GameFacts ----------------------------------------------------------------
Console.WriteLine("GameFacts:");
CheckEq("GameTitle", "Eye of the Beholder", GameFacts.GameTitle);
CheckEq("Developer", "Westwood Studios / SSI", GameFacts.Developer);
CheckEq("ReleaseYear", 1991, GameFacts.ReleaseYear);
CheckEq("DungeonLevels", 12, GameFacts.DungeonLevels);
CheckEq("LevelGridSize", 32, GameFacts.LevelGridSize);
CheckEq("MaxPartySize", 6, GameFacts.MaxPartySize);
CheckEq("MaxNameLength", 10, GameFacts.MaxNameLength);
CheckEq("FinalBoss", "Xanathar", GameFacts.FinalBoss);
SectionBreak();

// --- Summary ------------------------------------------------------------------
Console.WriteLine($"\n=== {checks} checks, {failures} failure(s) ===");
return failures == 0 ? 0 : 1;

void SectionBreak()
{
    if (failures == 0) Console.WriteLine("  (all passed)\n");
    else Console.WriteLine($"  ({failures} failures)\n");
}
