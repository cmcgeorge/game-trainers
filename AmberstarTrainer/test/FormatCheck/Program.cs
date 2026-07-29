using AmberstarTrainer.Game;

// Headless verification harness for the Amberstar character record parser. It builds a
// synthetic 1146-byte record with known values, asserts every parsed field, and checks
// the name round-trip and IsOccupied. Exits 0 on success, 1 on any failure, so it can
// gate the build (Run.ps1 -Test).

int failures = 0;

// --- build a synthetic character record --------------------------------------
byte[] rec = new byte[CharacterFormat.RecordSize];

// magic header (big-endian 00 FF)
rec[0] = 0x00; rec[1] = 0xFF;

// type = Person
rec[CharacterFormat.OffType] = 0;

// gender = Male
rec[CharacterFormat.OffGender] = 0;

// race = Human
rec[CharacterFormat.OffRace] = 0;

// class = Warrior
rec[CharacterFormat.OffClass] = 1;

// skills (current): 50, 45, 20, 30, 25, 25, 40, 35, 10, 15
int[] skillsCur = { 50, 45, 20, 30, 25, 25, 40, 35, 10, 15 };
for (int i = 0; i < CharacterFormat.SkillCount; i++)
    rec[CharacterFormat.OffSkillsCur + i] = (byte)skillsCur[i];

// skills (max): 99, 90, 50, 60, 50, 50, 80, 70, 20, 30
int[] skillsMax = { 99, 90, 50, 60, 50, 50, 80, 70, 20, 30 };
for (int i = 0; i < CharacterFormat.SkillCount; i++)
    rec[CharacterFormat.OffSkillsMax + i] = (byte)skillsMax[i];

// magic schools = white (2)
rec[CharacterFormat.OffMagicSchools] = 2;

// level = 5
rec[CharacterFormat.OffLevel] = 5;

// attributes (current, big-endian Words): STR=80, INT=20, DEX=60, SPE=50, CON=70, CHA=40, LUC=30, MAG=10, AGE=25
int[] attrsCur = { 80, 20, 60, 50, 70, 40, 30, 10, 25 };
for (int i = 0; i < CharacterFormat.AttributeCount; i++)
{
    rec[CharacterFormat.OffAttrCur + i * 2] = (byte)(attrsCur[i] >> 8);
    rec[CharacterFormat.OffAttrCur + i * 2 + 1] = (byte)(attrsCur[i] & 0xFF);
}

// attributes (max, big-endian Words): same but higher
int[] attrsMax = { 99, 50, 99, 99, 99, 60, 50, 20, 100 };
for (int i = 0; i < CharacterFormat.AttributeCount; i++)
{
    rec[CharacterFormat.OffAttrMax + i * 2] = (byte)(attrsMax[i] >> 8);
    rec[CharacterFormat.OffAttrMax + i * 2 + 1] = (byte)(attrsMax[i] & 0xFF);
}

// HP: cur=40, max=50 (big-endian)
rec[CharacterFormat.OffHpCur] = 0; rec[CharacterFormat.OffHpCur + 1] = 40;
rec[CharacterFormat.OffHpMax] = 0; rec[CharacterFormat.OffHpMax + 1] = 50;

// SP: cur=15, max=30 (big-endian)
rec[CharacterFormat.OffSpCur] = 0; rec[CharacterFormat.OffSpCur + 1] = 15;
rec[CharacterFormat.OffSpMax] = 0; rec[CharacterFormat.OffSpMax + 1] = 30;

// SLP: 10 (big-endian)
rec[CharacterFormat.OffSlp] = 0; rec[CharacterFormat.OffSlp + 1] = 10;

// Gold: 1500 (big-endian)
rec[CharacterFormat.OffGold] = 0x05; rec[CharacterFormat.OffGold + 1] = 0xDC;

// Food: 50 (big-endian)
rec[CharacterFormat.OffFood] = 0; rec[CharacterFormat.OffFood + 1] = 50;

// Experience: 12000 (big-endian Long = 0x00002EE0)
rec[CharacterFormat.OffExperience] = 0x00;
rec[CharacterFormat.OffExperience + 1] = 0x00;
rec[CharacterFormat.OffExperience + 2] = 0x2E;
rec[CharacterFormat.OffExperience + 3] = 0xE0;

// Spells white: bit 0 = Healing 1 (bit value 2)
rec[CharacterFormat.OffSpellsWhite] = 0x00;
rec[CharacterFormat.OffSpellsWhite + 1] = 0x00;
rec[CharacterFormat.OffSpellsWhite + 2] = 0x00;
rec[CharacterFormat.OffSpellsWhite + 3] = 0x02;

// Name: "TestHero" (ASCII, null-terminated)
var nameBytes = System.Text.Encoding.ASCII.GetBytes("TestHero");
Array.Copy(nameBytes, 0, rec, CharacterFormat.OffName, nameBytes.Length);

// --- parse and assert -------------------------------------------------------
var record = new CharacterRecord(rec);

Console.WriteLine("=== Amberstar Character Record Parser Tests ===");
Console.WriteLine();

Console.WriteLine("Identity:");
Check("magic header", record.Magic, (ushort)0x00FF);
Check("type", record.Type, 0);
Check("name", record.Name, "TestHero");
Check("gender", record.Gender, 0);
Check("race", record.Race, 0);
Check("race name", record.RaceName, "Human");
Check("class", record.Class, 1);
Check("class name", record.ClassName, "Warrior");
Check("level", record.Level, 5);
Console.WriteLine();

Console.WriteLine("Attributes (big-endian):");
for (int i = 0; i < CharacterFormat.AttributeCount; i++)
{
    Check($"{CharacterFormat.AttributeNames[i]} cur", record.GetAttrCur(i), attrsCur[i]);
    Check($"{CharacterFormat.AttributeNames[i]} max", record.GetAttrMax(i), attrsMax[i]);
}
Console.WriteLine();

Console.WriteLine("Skills:");
for (int i = 0; i < CharacterFormat.SkillCount; i++)
{
    Check($"{CharacterFormat.SkillNames[i]} cur", record.GetSkillCur(i), skillsCur[i]);
    Check($"{CharacterFormat.SkillNames[i]} max", record.GetSkillMax(i), skillsMax[i]);
}
Console.WriteLine();

Console.WriteLine("Vitals (big-endian):");
Check("HP cur", record.HpCur, 40);
Check("HP max", record.HpMax, 50);
Check("SP cur", record.SpCur, 15);
Check("SP max", record.SpMax, 30);
Check("SLP", record.Slp, 10);
Console.WriteLine();

Console.WriteLine("Resources (big-endian):");
Check("gold", record.Gold, 1500);
Check("food", record.Food, 50);
Check("experience", record.Experience, 12000L);
Console.WriteLine();

Console.WriteLine("Spells (big-endian bitfields):");
Check("white spells", record.SpellsWhite, 2L);
Check("grey spells", record.SpellsGrey, 0L);
Check("black spells", record.SpellsBlack, 0L);
Check("special spells", record.SpellsSpecial, 0L);
Console.WriteLine();

Console.WriteLine("IsOccupied:");
Check("occupied record", record.IsOccupied, true);

// Empty record (all zeros)
var empty = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
Check("empty record not occupied", empty.IsOccupied, false);

// Monster record (type=1)
var monster = new CharacterRecord((byte[])rec.Clone());
monster.Type = 1;
Check("monster record not occupied", monster.IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Name round-trip:");
foreach (var name in new[] { "A", "Bo", "TestHero", "MaxCharName15" })
{
    var r = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    r.Name = name;
    Check($"round-trip \"{name}\"", r.Name, name);
}
Console.WriteLine();

Console.WriteLine("Ailments:");
Check("physical ailments name (0)", CharacterFormat.PhysicalAilmentsName(0), "Okay");
Check("physical ailments name (0x22)", CharacterFormat.PhysicalAilmentsName(0x22), "Poisoned, Dead");
Check("mental ailments name (0)", CharacterFormat.MentalAilmentsName(0), "Okay");
Check("mental ailments name (0x30)", CharacterFormat.MentalAilmentsName(0x30), "Blind, Overloaded");
Console.WriteLine();

Console.WriteLine("Spell book:");
Check("white spell count", SpellBook.WhiteSpells.Length, 28);
Check("grey spell count", SpellBook.GreySpells.Length, 26);
Check("black spell count", SpellBook.BlackSpells.Length, 22);
Check("special spell count", SpellBook.SpecialSpells.Length, 20);
Check("total spell count", SpellBook.TotalCount, 96);
Console.WriteLine();

Console.WriteLine("Race/Class books:");
Check("race Human", RaceBook.Name(0), "Human");
Check("race Elf", RaceBook.Name(1), "Elf");
Check("race Animal", RaceBook.Name(13), "Animal");
Check("class Warrior", ClassBook.Name(1), "Warrior");
Check("class Black Mage", ClassBook.Name(8), "Black Mage");
Console.WriteLine();

Console.WriteLine("Set operations:");
record.SetAttribute(0, 999);
Check("set STR to 999", record.GetAttrCur(0), 999);
Check("set STR max to 999", record.GetAttrMax(0), 999);
record.SetSkill(0, 99);
Check("set ATK cur to 99", record.GetSkillCur(0), 99);
Check("set ATK max to 99", record.GetSkillMax(0), 99);
record.LearnAllSpells();
Check("learn all white", record.SpellsWhite, (long)0xFFFFFFFF);
Check("learn all grey", record.SpellsGrey, (long)0xFFFFFFFF);
Check("learn all black", record.SpellsBlack, (long)0xFFFFFFFF);
Check("learn all special", record.SpellsSpecial, (long)0xFFFFFFFF);
Console.WriteLine();

Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the Amberstar record layout decodes correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

void Check<T>(string label, T actual, T expected) where T : notnull
{
    bool ok = actual.Equals(expected);
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {label}: expected={expected}, actual={actual}");
    if (!ok) failures++;
}
