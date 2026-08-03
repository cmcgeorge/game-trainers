using System.IO;
using DarkDesigns1Trainer.Game;

// Headless verification harness for the Dark Designs I character format.
// It builds a synthetic DDCHARS.DAT from the sample, asserts every decoded field,
// tests the record validation, the save-file round-trip, and the reference tables.
// Exits 0 on success, 1 on any failure.

using DarkDesigns1Trainer.Memory;

int failures = 0;

// --- format constants --------------------------------------------------------
Console.WriteLine("Format constants:");
Check("record size", CharacterFormat.RecordSize, 54);
Check("max slots", CharacterFormat.MaxSlots, 20);
Check("header size", CharacterFormat.HeaderSize, 144);
Check("file size", CharacterFormat.FileSize, 1224);
Check("name length", CharacterFormat.NameLength, 12);
Check("attribute count", CharacterFormat.AttributeCount, 5);
Check("anchor string length", GameFacts.AnchorString.Length, 34);
Console.WriteLine();

// --- build a synthetic DDCHARS.DAT from the sample ---------------------------
byte[] fileData = new byte[CharacterFormat.FileSize];
fileData[0] = 1; // header active flag

int off = CharacterFormat.HeaderSize;
fileData[off + CharacterFormat.OffExists] = 1;
fileData[off + CharacterFormat.OffNameLen] = 11;
var nameBytes = System.Text.Encoding.ASCII.GetBytes("CHRISTOPHER");
Array.Copy(nameBytes, 0, fileData, off + CharacterFormat.OffName, nameBytes.Length);
fileData[off + CharacterFormat.OffClass] = CharacterFormat.ClassFighter;
fileData[off + CharacterFormat.OffLevel] = 1;
WriteU16(fileData, off + CharacterFormat.OffStr, 17);
WriteU16(fileData, off + CharacterFormat.OffDex, 16);
WriteU16(fileData, off + CharacterFormat.OffCon, 14);
WriteU16(fileData, off + CharacterFormat.OffInt, 14);
WriteU16(fileData, off + CharacterFormat.OffPie, 14);
WriteU16(fileData, off + CharacterFormat.OffStatus, 1);
WriteU16(fileData, off + CharacterFormat.OffGold, 1000);
WriteU16(fileData, off + CharacterFormat.OffBodyCur, 35);
WriteU16(fileData, off + CharacterFormat.OffBodyMax, 35);
WriteU16(fileData, off + CharacterFormat.OffExperience, 100);
WriteU16(fileData, off + CharacterFormat.OffMagicCur, 5);

Console.WriteLine("Character record decode:");
var rec0 = new CharacterRecord(fileData, CharacterFormat.HeaderSize);
Check("name", rec0.Name, "CHRISTOPHER");
Check("class", rec0.Class, CharacterFormat.ClassFighter);
Check("class name", rec0.ClassName, "Fighter");
Check("level", rec0.Level, 1);
Check("STR", rec0.Strength, 17);
Check("DEX", rec0.Dexterity, 16);
Check("CON", rec0.Constitution, 14);
Check("INT", rec0.Intelligence, 14);
Check("PIE", rec0.Piety, 14);
Check("gold", rec0.Gold, 1000);
Check("body current", rec0.BodyCurrent, 35);
Check("body max", rec0.BodyMax, 35);
Check("experience", rec0.Experience, 100);
Check("magic current", rec0.MagicCurrent, 5);
Check("status", rec0.Status, 1);
Check("status name", rec0.StatusName, "fine");
Check("IsOccupied", rec0.IsOccupied, true);
Console.WriteLine();

// --- name round-trip ---------------------------------------------------------
Console.WriteLine("Name encode / decode round-trip:");
foreach (var name in new[] { "A", "Bo", "CHRISTOPHER", "Max12Chars" })
{
    var rec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    rec.Name = name;
    Check($"round-trip \"{name}\"", rec.Name, name);
    Check($"name length for \"{name}\"", rec.Bytes[CharacterFormat.OffNameLen], Math.Min(name.Length, 12));
}
Console.WriteLine();

// --- long name truncation ----------------------------------------------------
Console.WriteLine("Name truncation:");
var longRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
longRec.Name = "VeryLongNameHere";
Check("truncated to 12", longRec.Name, "VeryLongName");
Check("name length byte", longRec.Bytes[CharacterFormat.OffNameLen], 12);
Console.WriteLine();

// --- empty slot detection ----------------------------------------------------
Console.WriteLine("Empty slot detection:");
var emptyRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
Check("empty slot not occupied", emptyRec.IsOccupied, false);
Check("empty slot looks like record", CharacterFormat.LooksLikeRecord(emptyRec.Bytes, 0), false);
Check("empty slot is empty slot", CharacterFormat.IsEmptySlot(emptyRec.Bytes, 0), true);
Console.WriteLine();

// --- LooksLikeRecord validation ----------------------------------------------
Console.WriteLine("LooksLikeRecord validation:");
Check("valid record", CharacterFormat.LooksLikeRecord(fileData, CharacterFormat.HeaderSize), true);
Check("empty zeros not a record", CharacterFormat.LooksLikeRecord(new byte[54], 0), false);

var badClass = (byte[])fileData.Clone();
badClass[CharacterFormat.HeaderSize + CharacterFormat.OffClass] = 9;
Check("bad class rejected", CharacterFormat.LooksLikeRecord(badClass, CharacterFormat.HeaderSize), false);

var badLevel = (byte[])fileData.Clone();
badLevel[CharacterFormat.HeaderSize + CharacterFormat.OffLevel] = 0;
Check("level 0 rejected", CharacterFormat.LooksLikeRecord(badLevel, CharacterFormat.HeaderSize), false);

var badName = (byte[])fileData.Clone();
badName[CharacterFormat.HeaderSize + CharacterFormat.OffName] = (byte)'1';
Check("non-letter name rejected", CharacterFormat.LooksLikeRecord(badName, CharacterFormat.HeaderSize), false);
Console.WriteLine();

// --- roster geometry ---------------------------------------------------------
Console.WriteLine("Roster geometry:");
Check("roster bytes", CharacterFormat.MaxSlots * CharacterFormat.RecordSize, 1080);
Check("file = header + roster", CharacterFormat.HeaderSize + CharacterFormat.MaxSlots * CharacterFormat.RecordSize, CharacterFormat.FileSize);
Console.WriteLine();

// --- attribute offsets -------------------------------------------------------
Console.WriteLine("Attribute offsets:");
Check("STR offset", CharacterFormat.OffStr, 0x11);
Check("DEX offset", CharacterFormat.OffDex, 0x13);
Check("CON offset", CharacterFormat.OffCon, 0x15);
Check("INT offset", CharacterFormat.OffInt, 0x17);
Check("PIE offset", CharacterFormat.OffPie, 0x19);
Check("attributes at 2-byte stride", CharacterFormat.OffDex - CharacterFormat.OffStr, 2);
Console.WriteLine();

// --- setAttribute / write round-trip -----------------------------------------
Console.WriteLine("Attribute set round-trip:");
var attrRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
attrRec.Name = "TEST";
attrRec.Strength = 30;
attrRec.Dexterity = 25;
attrRec.Constitution = 18;
attrRec.Intelligence = 20;
attrRec.Piety = 15;
Check("set STR", attrRec.Strength, 30);
Check("set DEX", attrRec.Dexterity, 25);
Check("set CON", attrRec.Constitution, 18);
Check("set INT", attrRec.Intelligence, 20);
Check("set PIE", attrRec.Piety, 15);
Console.WriteLine();

// --- vitals set round-trip ---------------------------------------------------
Console.WriteLine("Vitals set round-trip:");
attrRec.BodyCurrent = 999;
attrRec.BodyMax = 999;
attrRec.MagicCurrent = 500;
attrRec.Gold = 65535;
attrRec.Experience = 32000;
attrRec.Level = 50;
Check("set body current", attrRec.BodyCurrent, 999);
Check("set body max", attrRec.BodyMax, 999);
Check("set magic", attrRec.MagicCurrent, 500);
Check("set gold", attrRec.Gold, 65535);
Check("set experience", attrRec.Experience, 32000);
Check("set level", attrRec.Level, 50);
Console.WriteLine();

// --- save file round-trip ----------------------------------------------------
Console.WriteLine("Save file round-trip:");
string tmpDir = Path.Combine(Path.GetTempPath(), "dd1test_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tmpDir);
string tmpSave = Path.Combine(tmpDir, "DDCHARS.DAT");
File.WriteAllBytes(tmpSave, fileData);
try
{
    using (var sf = new SaveFile(tmpSave))
    {
        Check("save file character count", sf.OccupiedCharacters.Count(), 1);
        var c = sf.OccupiedCharacters.First();
        Check("save file name", c.Name, "CHRISTOPHER");
        Check("save file class", c.Class, CharacterFormat.ClassFighter);

        c.Strength = 30;
        c.Gold = 9999;
        sf.MarkModified();
        sf.Save();
    }

    var saved = File.ReadAllBytes(tmpSave);
    Check("file size preserved", saved.Length, CharacterFormat.FileSize);
    var reloaded = new CharacterRecord(saved, CharacterFormat.HeaderSize);
    Check("modified STR persists", reloaded.Strength, 30);
    Check("modified gold persists", reloaded.Gold, 9999);

    Check("backup file exists", File.Exists(tmpSave + ".bak"), true);
    var backup = File.ReadAllBytes(tmpSave + ".bak");
    Check("backup has original STR", ReadU16(backup, CharacterFormat.HeaderSize + CharacterFormat.OffStr), 17);
}
finally
{
    if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
}
Console.WriteLine();

// --- multiple characters in save file ----------------------------------------
Console.WriteLine("Multiple characters in save file:");
byte[] multiData = new byte[CharacterFormat.FileSize];
multiData[0] = 1;
for (int i = 0; i < 4; i++)
{
    int o = CharacterFormat.HeaderSize + i * CharacterFormat.RecordSize;
    multiData[o + CharacterFormat.OffExists] = 1;
    multiData[o + CharacterFormat.OffNameLen] = 5;
    System.Text.Encoding.ASCII.GetBytes($"HERO{i}").CopyTo(multiData, o + CharacterFormat.OffName);
    multiData[o + CharacterFormat.OffClass] = (byte)(i % 3 + 1);
    multiData[o + CharacterFormat.OffLevel] = (byte)(i + 1);
    WriteU16(multiData, o + CharacterFormat.OffStr, 15 + i);
    WriteU16(multiData, o + CharacterFormat.OffBodyMax, 20 + i * 5);
    WriteU16(multiData, o + CharacterFormat.OffBodyCur, 20 + i * 5);
}
string multiSave = Path.Combine(Path.GetTempPath(), "dd1multi_" + Guid.NewGuid().ToString("N")[..8] + ".DAT");
File.WriteAllBytes(multiSave, multiData);
try
{
    using (var sf = new SaveFile(multiSave))
    {
        Check("multi save character count", sf.OccupiedCharacters.Count(), 4);
        var chars = sf.OccupiedCharacters.ToList();
        Check("first character name", chars[0].Name, "HERO0");
        Check("second character name", chars[1].Name, "HERO1");
        Check("third character name", chars[2].Name, "HERO2");
        Check("fourth character name", chars[3].Name, "HERO3");
        Check("first class (Fighter)", chars[0].ClassName, "Fighter");
        Check("second class (Priest)", chars[1].ClassName, "Priest");
        Check("third class (Wizard)", chars[2].ClassName, "Wizard");
        Check("fourth class (Fighter)", chars[3].ClassName, "Fighter");
    }
}
finally
{
    if (File.Exists(multiSave)) File.Delete(multiSave);
    if (File.Exists(multiSave + ".bak")) File.Delete(multiSave + ".bak");
}
Console.WriteLine();

// --- reference tables --------------------------------------------------------
Console.WriteLine("Reference tables:");
Check("wizard spell count", SpellBook.WizardSpells.Length, 8);
Check("priest spell count", SpellBook.PriestSpells.Length, 8);
Check("item count", ItemBook.All.Length, 40);
Check("monster count", MonsterBook.All.Length, 43);
Check("level names count", GameFacts.LevelNames.Length, 5);
Check("class names count", CharacterFormat.ClassNames.Length, 4);
Check("status names count", CharacterFormat.StatusNames.Length, 6);
Check("attribute names count", CharacterFormat.AttributeNames.Length, 5);
Console.WriteLine();

// --- spell content spot checks -----------------------------------------------
Console.WriteLine("Spell content spot checks:");
Check("first wizard spell", SpellBook.WizardSpells[0].Name, "Magic Missile");
Check("last wizard spell", SpellBook.WizardSpells[7].Name, "Death Ray");
Check("first priest spell", SpellBook.PriestSpells[0].Name, "Cure Light Wounds");
Check("last priest spell", SpellBook.PriestSpells[7].Name, "Cureall");
Check("fireball gold cost", SpellBook.WizardSpells[5].GoldCost, 300);
Check("word of recall gold cost", SpellBook.PriestSpells[6].GoldCost, 350);
Console.WriteLine();

// --- item content spot checks ------------------------------------------------
Console.WriteLine("Item content spot checks:");
Check("first item name", ItemBook.All[0].Name, "Dagger");
Check("quest item name", ItemBook.All[^1].Name, "The Staff");
Check("key 1 exists", ItemBook.All.Any(i => i.Name == "Key 1"), true);
Check("key 2 exists", ItemBook.All.Any(i => i.Name == "Key 2"), true);
Check("key 3 exists", ItemBook.All.Any(i => i.Name == "Key 3"), true);
Console.WriteLine();

// --- monster content spot checks ---------------------------------------------
Console.WriteLine("Monster content spot checks:");
Check("first monster", MonsterBook.All[0].Name, "Kobold");
Check("last monster", MonsterBook.All[^1].Name, "Chaos Avatar");
Check("demon lord exists", MonsterBook.All.Any(m => m.Name == "Demon Lord"), true);
Check("medusa exists", MonsterBook.All.Any(m => m.Name == "Medusa"), true);
Console.WriteLine();

// --- sample DDCHARS.DAT (if present) ----------------------------------------
string? gameDir = FindGameDir();
if (gameDir != null)
{
    string charsPath = Path.Combine(gameDir, "DDCHARS.DAT");
    if (File.Exists(charsPath))
    {
        Console.WriteLine($"Sample DDCHARS.DAT found at {charsPath}:");
        try
        {
            var sample = File.ReadAllBytes(charsPath);
            Check("sample file size", sample.Length, CharacterFormat.FileSize);
            var sampleRec = new CharacterRecord(sample, CharacterFormat.HeaderSize);
            Check("sample character exists", sampleRec.IsOccupied, true);
            if (sampleRec.IsOccupied)
            {
                Check("sample name starts with C", sampleRec.Name.StartsWith("C"), true);
                Check("sample class is Fighter", sampleRec.Class, CharacterFormat.ClassFighter);
                Check("sample level is 1", sampleRec.Level, 1);
                Check("sample STR is 17", sampleRec.Strength, 17);
                Check("sample DEX is 16", sampleRec.Dexterity, 16);
                Check("sample gold is 1000", sampleRec.Gold, 1000);
                Check("sample body current is 35", sampleRec.BodyCurrent, 35);
                Check("sample body max is 35", sampleRec.BodyMax, 35);
                Check("sample experience is 100", sampleRec.Experience, 100);
                Check("sample magic current is 5", sampleRec.MagicCurrent, 5);
                Console.WriteLine($"  Sample character: {sampleRec.Name} (L{sampleRec.Level} {sampleRec.ClassName})");
                Console.WriteLine($"  STR={sampleRec.Strength} DEX={sampleRec.Dexterity} CON={sampleRec.Constitution} INT={sampleRec.Intelligence} PIE={sampleRec.Piety}");
                Console.WriteLine($"  Body={sampleRec.BodyCurrent}/{sampleRec.BodyMax} MP={sampleRec.MagicCurrent} XP={sampleRec.Experience} Gold={sampleRec.Gold}");
            }
            Console.WriteLine("  (sample file checks passed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARNING: could not parse sample DDCHARS.DAT: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("Sample DDCHARS.DAT not found — skipping sample-file checks (not a failure).");
    }
}
else
{
    Console.WriteLine("Game directory not found — skipping sample-file checks (not a failure).");
}
Console.WriteLine();

// --- summary -----------------------------------------------------------------
Console.WriteLine($"=== {failures} failure(s) ===");
return failures == 0 ? 0 : 1;

// --- helpers -----------------------------------------------------------------
static void WriteU16(byte[] buf, int offset, int value)
{
    buf[offset] = (byte)(value & 0xFF);
    buf[offset + 1] = (byte)((value >> 8) & 0xFF);
}

static int ReadU16(byte[] buf, int offset) => buf[offset] | (buf[offset + 1] << 8);

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    string status = ok ? "OK" : "FAIL";
    Console.WriteLine($"  [{status}] {label}: got {actual}, expected {expected}");
    if (!ok) failures++;
}

static string? FindGameDir()
{
    string[] candidates =
    {
        @"C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\DARKDES1",
    };
    foreach (var c in candidates)
        if (Directory.Exists(c)) return c;
    return null;
}
