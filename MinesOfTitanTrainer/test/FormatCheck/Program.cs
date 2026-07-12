// Headless verification harness for the Mines of Titan character parser. It builds a synthetic
// 86-byte record from the confirmed SAVEGAME.DAT layout, asserts every parsed field, checks the
// name encode/decode round-trip and IsOccupied, and validates the save-slot geometry (IJKM magic
// + record stride). If .game/SAVEGAME.DAT is present it also parses the first real slot. Exits 0
// on success, 1 on any failure, so it can gate the build (Run.ps1 -Test).

using System.IO;
using System.Text;
using MinesOfTitanTrainer.Game;

int failures = 0;

Console.WriteLine("Synthetic character record (confirmed SAVEGAME.DAT layout):");
byte[] buf = BuildRecord("Tom Jetland", 'M', 22,
    attrs: new[] { 15, 14, 13, 12, 11, 10 },
    skills: FillSkills(),
    credits: 100_000);

var rec = new CharacterRecord(buf);
Check("name", rec.Name, "Tom Jetland");
Check("sex", rec.Sex, 'M');
Check("sex name", rec.SexName, "Male");
Check("age", rec.Age, 22);
for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    Check($"attr {CharacterFormat.AttributeNames[i]}", rec.GetAttribute(i), 15 - i);
Check("skill 0", rec.GetSkill(0), 1);
Check("skill 26", rec.GetSkill(26), (26 % CharacterFormat.MaxSkill) + 1);
Check("credits", rec.Credits, 100_000);
Console.WriteLine();

Console.WriteLine("Layout geometry:");
Check("record size", CharacterFormat.RecordSize, 0x56);
Check("credits offset", CharacterFormat.OffCredits, 0x48);
Check("skills fit before credits",
    CharacterFormat.OffSkills + CharacterFormat.SkillCount <= CharacterFormat.OffCredits, true);
Check("attribute count", CharacterFormat.AttributeCount, 6);
Check("skill count", CharacterFormat.SkillCount, 27);
Check("named + reserved skills", CharacterFormat.SkillNames.Length, 27);
Check("skill 0 name", CharacterFormat.SkillNames[0], "Administration");
Check("skill 15 name", CharacterFormat.SkillNames[15], "Throwing");
Console.WriteLine();

Console.WriteLine("Name round-trip:");
foreach (var name in new[] { "A", "Bo", "Tom Jetland", "SixteenCharsName" })
{
    var r = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    r.Name = name;
    Check($"round-trip \"{name}\"", r.Name, name);
}
Console.WriteLine();

Console.WriteLine("IsOccupied:");
Check("occupied record", rec.IsOccupied, true);
Check("empty (0x00) record", new CharacterRecord(new byte[CharacterFormat.RecordSize]).IsOccupied, false);
Check("empty (0xFF) record", new CharacterRecord(Filled(0xFF)).IsOccupied, false);
Check("bad sex byte rejected", new CharacterRecord(BuildRecord("Zed", 'Q', 22,
    new[] { 5, 5, 5, 5, 5, 5 }, FillSkills(), 0)).IsOccupied, false);
byte[] badAttr = BuildRecord("Zed", 'M', 22, new[] { 5, 5, 5, 5, 5, 5 }, FillSkills(), 0);
badAttr[CharacterFormat.OffAttributes] = 99;   // poke a raw byte: the setter now clamps, but a scan sees raw memory
Check("out-of-range attribute rejected", new CharacterRecord(badAttr).IsOccupied, false);
byte[] badSkill = BuildRecord("Zed", 'M', 22, new[] { 5, 5, 5, 5, 5, 5 }, FillSkills(), 0);
badSkill[CharacterFormat.OffSkills + 3] = 99;
Check("out-of-range skill rejected", new CharacterRecord(badSkill).IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Editor setters clamp to the game's value bounds:");
var clamp = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
clamp.SetAttribute(0, 99);
Check("attribute clamped to MaxAttribute", clamp.GetAttribute(0), CharacterFormat.MaxAttribute);
clamp.SetSkill(0, 99);
Check("skill clamped to MaxSkill", clamp.GetSkill(0), CharacterFormat.MaxSkill);
clamp.Age = 250;
Check("age clamped to MaxAge", clamp.Age, CharacterFormat.MaxAge);
clamp.Age = 0;
Check("age clamped to MinAge", clamp.Age, CharacterFormat.MinAge);
var ctrlName = new CharacterRecord(new byte[CharacterFormat.RecordSize]) { Name = "Zoe\tX" };
Check("name truncates at a control char", ctrlName.Name, "Zoe");
Console.WriteLine();

Console.WriteLine("IsOccupiedAt (allocation-free scanner probe):");
byte[] framed = new byte[7 + CharacterFormat.RecordSize];   // record embedded at a non-zero offset
Array.Copy(buf, 0, framed, 7, buf.Length);
Check("valid window at offset 7", CharacterRecord.IsOccupiedAt(framed, 7), true);
Check("empty window rejected", CharacterRecord.IsOccupiedAt(new byte[CharacterFormat.RecordSize], 0), false);
Check("offset past end rejected", CharacterRecord.IsOccupiedAt(framed, framed.Length - 4), false);
Console.WriteLine();

Console.WriteLine("Save-slot geometry (IJKM magic + record stride):");
byte[] save = BuildSaveSlot(buf);
Check("magic is IJKM", Encoding.ASCII.GetString(save, 0, 4), "IJKM");
var firstRecord = new CharacterRecord(save, CharacterFormat.SlotToFirstRecord);
Check("record at anchor+0x1A parses name", firstRecord.Name, "Tom Jetland");
Check("record at anchor+0x1A parses credits", firstRecord.Credits, 100_000);
Console.WriteLine();

// Optional: parse the real save file if it's present (git-ignored, so not always there).
string saveFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".game", "SAVEGAME.DAT");
if (File.Exists(saveFile))
{
    Console.WriteLine($"Real SAVEGAME.DAT found ({saveFile}):");
    byte[] data = File.ReadAllBytes(saveFile);
    int slot0 = 0x102;
    Check("slot 0 magic is IJKM", Encoding.ASCII.GetString(data, slot0, 4), "IJKM");
    var real = new CharacterRecord(data, slot0 + CharacterFormat.SlotToFirstRecord);
    Console.WriteLine($"  first character: \"{real.Name}\" ({real.SexName}, age {real.Age}, credits {real.Credits:N0})");
    Check("first real character is occupied", real.IsOccupied, true);
    Console.WriteLine();
}
else
{
    Console.WriteLine("(.game/SAVEGAME.DAT not present — skipping the live-file check.)");
    Console.WriteLine();
}

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

// --- helpers ----------------------------------------------------------------
int[] FillSkills()
{
    // Distinct-but-valid ranks (1..MaxSkill, cycling) so the record still validates as occupied
    // while proving GetSkill/SetSkill index arithmetic.
    var s = new int[CharacterFormat.SkillCount];
    for (int i = 0; i < s.Length; i++) s[i] = (i % CharacterFormat.MaxSkill) + 1;
    return s;
}

byte[] Filled(byte value)
{
    var b = new byte[CharacterFormat.RecordSize];
    Array.Fill(b, value);
    return b;
}

byte[] BuildRecord(string name, char sex, int age, int[] attrs, int[] skills, long credits)
{
    var b = new byte[CharacterFormat.RecordSize];
    var r = new CharacterRecord(b) { Name = name, Sex = sex, Age = age, Credits = credits };
    for (int i = 0; i < attrs.Length; i++) r.SetAttribute(i, attrs[i]);
    for (int i = 0; i < skills.Length; i++) r.SetSkill(i, skills[i]);
    return r.Bytes;
}

byte[] BuildSaveSlot(byte[] record)
{
    var slot = new byte[CharacterFormat.SlotToFirstRecord + CharacterFormat.RecordSize];
    Array.Copy(CharacterFormat.SlotMagic, 0, slot, 0, CharacterFormat.SlotMagic.Length);
    Array.Copy(record, 0, slot, CharacterFormat.SlotToFirstRecord, record.Length);
    return slot;
}

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label}: {actual}" + (ok ? "" : $"  (expected {expected})"));
    if (!ok) failures++;
}
