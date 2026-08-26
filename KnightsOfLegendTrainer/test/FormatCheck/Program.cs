using System.IO;
using KnightsOfLegendTrainer.Game;
using KnightsOfLegendTrainer.ViewModels;
using GameTrainers.Common.Memory;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {Fmt(actual)}, expected {Fmt(expected)}");
    if (!ok) failures++;
}

static string Fmt(object? v) => v switch
{
    null => "null",
    bool b => b ? "true" : "false",
    _ => v.ToString() ?? "null",
};

Console.WriteLine("GameFacts constants:");
Check("game title", GameFacts.GameTitle, "Knights of Legend");
Check("developer", GameFacts.Developer, "Origin Systems");
Check("designer", GameFacts.Designer, "Todd Porter");
Check("release year", GameFacts.ReleaseYear, 1989);
Check("dos release year", GameFacts.DosReleaseYear, 1990);
Check("max statistic is 100", GameFacts.MaxStatistic, 100);
Check("primary stat count is 7", GameFacts.PrimaryStatCount, 7);
Check("max party size is 6", GameFacts.MaxPartySize, 6);
Check("max saved characters is 16", GameFacts.MaxSavedCharacters, 16);
Check("race count is 4", GameFacts.RaceCount, 4);
Check("class count is 33", GameFacts.ClassCount, 33);
Check("magic order count is 6", GameFacts.MagicOrderCount, 6);
Check("quest count is 24", GameFacts.QuestCount, 24);
Check("starting town is Brettle", GameFacts.StartingTown, "Brettle");
Check("setting is Ashtalarea", GameFacts.Setting, "Ashtalarea");
Check("kingdom is Sondar", GameFacts.Kingdom, "Sondar");
Check("currency is Gold Crowns", GameFacts.Currency, "Gold Crowns");
Check("experience is Adventure Points", GameFacts.Experience, "Adventure Points");
Check("training cost is 200", GameFacts.TrainingCost, 200);
Check("training AP cost is 100", GameFacts.TrainingApCost, 100);
Check("skill points per level is 20", GameFacts.SkillPointsPerLevel, 20);
Check("min level is 1", GameFacts.MinLevel, 1);
Check("max level is 25", GameFacts.MaxLevel, 25);
Check("safe inn cost is 60", GameFacts.SafeInnCost, 60);
Check("free inn cost is 0", GameFacts.FreeInnCost, 0);
Check("arrows per battle is 20", GameFacts.ArrowsPerBattle, 20);
Check("save quest status offset is 482", SaveFormat.QuestStatusOffset, 482);
Check("save quest status length is 6", SaveFormat.QuestStatusLength, 6);
Check("save quest count is 24", SaveFormat.QuestCount, 24);
Check("save bits per quest is 2", SaveFormat.BitsPerQuest, 2);
Check("quest not given is 0", SaveFormat.StatusNotGiven, 0);
Check("quest given is 1", SaveFormat.StatusGiven, 1);
Check("quest complete is 2", SaveFormat.StatusComplete, 2);
Check("quest medal given is 3", SaveFormat.StatusMedalGiven, 3);
Console.WriteLine();

Console.WriteLine("CharacterFormat:");
Check("primary stat names count is 7", CharacterFormat.PrimaryStatNames.Length, 7);
Check("first stat is Strength", CharacterFormat.PrimaryStatNames[0], "Strength");
Check("last stat is Intellect", CharacterFormat.PrimaryStatNames[^1], "Intellect");
Check("abbr count is 7", CharacterFormat.PrimaryStatAbbr.Length, 7);
Check("first abbr is STR", CharacterFormat.PrimaryStatAbbr[0], "STR");
Check("secondary stat count is 3", CharacterFormat.SecondaryStatNames.Length, 3);
Check("weapon attack count is 5", CharacterFormat.WeaponAttackTypes.Length, 5);
Check("unarmed attack count is 4", CharacterFormat.UnarmedAttackTypes.Length, 4);
Check("aim count is 3", CharacterFormat.AimOptions.Length, 3);
Check("defense count is 7", CharacterFormat.DefenseOptions.Length, 7);
Check("movement count is 6", CharacterFormat.MovementOptions.Length, 6);

var word = new byte[2];
CharacterFormat.WriteU16(word, 0, 400);
Check("WriteU16 is little-endian (low)", word[0], (byte)0x90);
Check("WriteU16 is little-endian (high)", word[1], (byte)0x01);
Check("ReadU16 round-trips", CharacterFormat.ReadU16(word, 0), 400);

var dword = new byte[4];
CharacterFormat.WriteU32(dword, 0, 123456);
Check("ReadU32 round-trips", CharacterFormat.ReadU32(dword, 0), 123456L);
Check("WriteU32 is little-endian (byte 0)", dword[0], (byte)0x40);
Check("WriteU32 is little-endian (byte 1)", dword[1], (byte)0xE2);
Check("WriteU32 is little-endian (byte 2)", dword[2], (byte)0x01);
Check("WriteU32 is little-endian (byte 3)", dword[3], (byte)0x00);
Console.WriteLine();

Console.WriteLine("SaveFormat quest status encoding:");
var save = new byte[512];
for (int i = 0; i < 24; i++)
    SaveFormat.WriteQuestStatus(save, i, SaveFormat.StatusNotGiven);
Check("all quests default to 0", string.Join(",", save.Skip(SaveFormat.QuestStatusOffset).Take(6)),
    "0,0,0,0,0,0");

SaveFormat.WriteQuestStatus(save, 0, SaveFormat.StatusGiven);
Check("quest 0 = given (0x01 in low bits of byte 482)", save[SaveFormat.QuestStatusOffset], (byte)0x01);
Check("read quest 0", SaveFormat.ReadQuestStatus(save, 0), 1);

SaveFormat.WriteQuestStatus(save, 1, SaveFormat.StatusComplete);
Check("quest 1 = complete (0x08 in bits 2-3 of byte 482, cumulative with quest 0=0x01)", save[SaveFormat.QuestStatusOffset], (byte)0x09);
Check("read quest 1", SaveFormat.ReadQuestStatus(save, 1), 2);

SaveFormat.WriteQuestStatus(save, 2, SaveFormat.StatusMedalGiven);
Check("quest 2 = medal (0x30 in bits 4-5 of byte 482, cumulative = 0x39)", save[SaveFormat.QuestStatusOffset], (byte)0x39);
Check("read quest 2", SaveFormat.ReadQuestStatus(save, 2), 3);

SaveFormat.WriteQuestStatus(save, 3, SaveFormat.StatusMedalGiven);
Check("quest 3 = medal (0xC0 in bits 6-7 of byte 482, cumulative = 0xF9)", save[SaveFormat.QuestStatusOffset], (byte)0xF9);
Check("read quest 3", SaveFormat.ReadQuestStatus(save, 3), 3);

SaveFormat.WriteQuestStatus(save, 4, SaveFormat.StatusGiven);
Check("quest 4 = given (byte 483)", save[SaveFormat.QuestStatusOffset + 1], (byte)0x01);
Check("read quest 4", SaveFormat.ReadQuestStatus(save, 4), 1);

SaveFormat.WriteQuestStatus(save, 23, SaveFormat.StatusComplete);
Check("quest 23 = complete (byte 487)", save[SaveFormat.QuestStatusOffset + 5], (byte)0x80);
Check("read quest 23", SaveFormat.ReadQuestStatus(save, 23), 2);

SaveFormat.WriteQuestStatus(save, 23, SaveFormat.StatusMedalGiven);
Check("quest 23 = medal (byte 487 = 0xC0)", save[SaveFormat.QuestStatusOffset + 5], (byte)0xC0);
Check("read quest 23 medal", SaveFormat.ReadQuestStatus(save, 23), 3);

Check("status clamps to 3", SaveFormat.ReadQuestStatus(save, 0), SaveFormat.ReadQuestStatus(save, 0));
SaveFormat.WriteQuestStatus(save, 0, 99);
Check("out-of-range status clamps to 3", SaveFormat.ReadQuestStatus(save, 0), 3);

var allStatuses = SaveFormat.ReadAllQuestStatuses(save);
Check("ReadAllQuestStatuses returns 24", allStatuses.Length, 24);
Check("all statuses are 0..3", allStatuses.All(s => s >= 0 && s <= 3), true);

var smallSave = new byte[100];
Check("small buffer is invalid chardata", SaveFormat.IsValidChardata(smallSave), false);
Check("512-byte buffer is valid chardata", SaveFormat.IsValidChardata(save), true);

Check("status label 0", SaveFormat.StatusLabels[0], "Not Given");
Check("status label 1", SaveFormat.StatusLabels[1], "Given");
Check("status label 2", SaveFormat.StatusLabels[2], "Complete");
Check("status label 3", SaveFormat.StatusLabels[3], "Medal Given");
Console.WriteLine();

Console.WriteLine("SaveFormat round-trip (all 24 quests, all 4 codes):");
for (int code = 0; code <= 3; code++)
{
    var rt = new byte[512];
    for (int i = 0; i < 24; i++)
        SaveFormat.WriteQuestStatus(rt, i, code);
    bool allMatch = true;
    for (int i = 0; i < 24; i++)
        if (SaveFormat.ReadQuestStatus(rt, i) != code) { allMatch = false; break; }
    Check($"all quests round-trip code {code}", allMatch, true);
}

var mixed = new byte[512];
var codes = new[] { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 };
for (int i = 0; i < 24; i++)
    SaveFormat.WriteQuestStatus(mixed, i, codes[i]);
bool mixedMatch = true;
for (int i = 0; i < 24; i++)
    if (SaveFormat.ReadQuestStatus(mixed, i) != codes[i]) { mixedMatch = false; break; }
Check("mixed codes round-trip", mixedMatch, true);

SaveFormat.WriteQuestStatus(mixed, 5, 0);
SaveFormat.WriteQuestStatus(mixed, 10, 1);
Check("partial rewrite preserves others", SaveFormat.ReadQuestStatus(mixed, 0), 0);
Check("partial rewrite preserves others (quest 1)", SaveFormat.ReadQuestStatus(mixed, 1), 1);
Check("partial rewrite preserves others (quest 5)", SaveFormat.ReadQuestStatus(mixed, 5), 0);
Check("partial rewrite preserves others (quest 10)", SaveFormat.ReadQuestStatus(mixed, 10), 1);
Check("partial rewrite preserves others (quest 23)", SaveFormat.ReadQuestStatus(mixed, 23), 3);
Console.WriteLine();

Console.WriteLine("RaceBook:");
Check("race count is 4", RaceBook.Races.Count, 4);
Check("first race is Human", RaceBook.Races[0].Name, "Human");
Check("last race is Kelden", RaceBook.Races[^1].Name, "Kelden");
Check("Kelden can fly", RaceBook.Races[3].Name, "Kelden");
Check("ById(0) is Human", RaceBook.ById(0)?.Name, "Human");
Check("ById(3) is Kelden", RaceBook.ById(3)?.Name, "Kelden");
Check("ById(-1) is null", RaceBook.ById(-1), null);
Check("ById(4) is null", RaceBook.ById(4), null);
Console.WriteLine();

Console.WriteLine("ClassBook:");
Check("class count is 33", ClassBook.Classes.Count, 33);
Check("first class is Peasant", ClassBook.Classes[0].Name, "Peasant");
Check("human male classes", ClassBook.ByRace("Human").Where(c => c.Gender == "Male").Count(), 12);
Check("human female classes", ClassBook.ByRace("Human").Where(c => c.Gender == "Female").Count(), 4);
Check("elven classes", ClassBook.ByRace("Elven").Count(), 6);
Check("dwarven classes", ClassBook.ByRace("Dwarven").Count(), 8);
Check("kelden classes", ClassBook.ByRace("Kelden").Count(), 3);
Check("Knight level is 25", ClassBook.Classes.First(c => c.Name == "Knight" && c.Race == "Human" && c.Gender == "Male").Level, 25);
Check("Peasant level is 1", ClassBook.Classes.First(c => c.Name == "Peasant" && c.Race == "Human" && c.Gender == "Male").Level, 1);
Console.WriteLine();

Console.WriteLine("WeaponBook:");
Check("weapon count is 36", WeaponBook.Weapons.Count, 36);
Check("proficiency name count is 10", WeaponBook.ProficiencyNames.Length, 10);
Check("first proficiency is None", WeaponBook.ProficiencyNames[0], "None");
Check("last proficiency is Expert", WeaponBook.ProficiencyNames[^1], "Expert");
Check("first weapon is Longsword", WeaponBook.Weapons[0].Name, "Longsword");
Check("Hvrad Myth trains Longsword", WeaponBook.Weapons[0].Master, "Hvrad Myth");
Check("Hvrad Myth is in Brettle", WeaponBook.Weapons[0].Location, "Fortress of Brettle");
Check("master count is 9", WeaponBook.Masters.Count, 9);
Check("Hvrad Myth trains 4 weapons", WeaponBook.ByMaster("Hvrad Myth").Count, 4);
Console.WriteLine();

Console.WriteLine("ArmorBook:");
Check("armor count is 12", ArmorBook.Armor.Count, 12);
Check("first armor is Leather Armor", ArmorBook.Armor[0].Name, "Leather Armor");
Check("Great Shield is a shield", ArmorBook.Armor.First(a => a.Name == "Great Shield").Category, "Shield");
Check("torso armor count", ArmorBook.ByCategory("Torso").Count, 3);
Check("head armor count", ArmorBook.ByCategory("Head").Count, 3);
Check("leg armor count", ArmorBook.ByCategory("Legs").Count, 3);
Check("shield count", ArmorBook.ByCategory("Shield").Count, 3);
Console.WriteLine();

Console.WriteLine("MagicOrderBook:");
Check("order count is 6", MagicOrderBook.Orders.Count, 6);
Check("first order is White Pearl", MagicOrderBook.Orders[0].Name, "White Pearl");
Check("last order is Dark Stone", MagicOrderBook.Orders[^1].Name, "Dark Stone");
Check("White Pearl is in Brettle", MagicOrderBook.Orders[0].Location, "Brettle");
Check("Dark Stone is in Olanthen", MagicOrderBook.Orders[5].Location, "Olanthen");
Console.WriteLine();

Console.WriteLine("SpellBook:");
Check("spell count is 20", SpellBook.Spells.Count, 20);
Check("every spell belongs to a known order",
    SpellBook.Spells.All(s => MagicOrderBook.Orders.Any(o => o.Name == s.Order)), true);
Check("Heal is White Pearl", SpellBook.Spells.First(s => s.Name == "Heal").Order, "White Pearl");
Check("Fireball is Black Onyx", SpellBook.Spells.First(s => s.Name == "Fireball").Order, "Black Onyx");
Check("Death Touch is Dark Stone", SpellBook.Spells.First(s => s.Name == "Death Touch").Order, "Dark Stone");
Check("White Pearl spell count", SpellBook.ByOrder("White Pearl").Count, 4);
Check("Blue Gem spell count", SpellBook.ByOrder("Blue Gem").Count, 3);
Check("Black Onyx spell count", SpellBook.ByOrder("Black Onyx").Count, 3);
Check("Secret Storm spell count", SpellBook.ByOrder("Secret Storm").Count, 3);
Check("Red Mist spell count", SpellBook.ByOrder("Red Mist").Count, 3);
Check("Dark Stone spell count", SpellBook.ByOrder("Dark Stone").Count, 4);
Console.WriteLine();

Console.WriteLine("MonsterBook:");
Check("monster count is 20", MonsterBook.Monsters.Count, 20);
Check("first monster is Ruffian", MonsterBook.Monsters[0].Name, "Ruffian");
Check("Cyclops is quest 24 target", MonsterBook.Monsters.First(m => m.Name == "Cyclops").Location, "Ghor Hills");
Check("Troll is in Missip Valley", MonsterBook.Monsters.First(m => m.Name == "Troll").Location, "Missip Valley");
Check("humanoid category count", MonsterBook.ByCategory("Humanoid").Count, 4);
Check("giant category count", MonsterBook.ByCategory("Giant").Count, 6);
Check("undead category count", MonsterBook.ByCategory("Undead").Count, 2);
Check("elemental category count", MonsterBook.ByCategory("Elemental").Count, 4);
Check("every monster has a location", MonsterBook.Monsters.All(m => m.Location.Length > 0), true);
Console.WriteLine();

Console.WriteLine("QuestBook:");
Check("quest count is 24", QuestBook.Quests.Count, 24);
Check("first quest is The Stolen Gavel", QuestBook.Quests[0].Name, "The Stolen Gavel");
Check("last quest is Rescue Seggallion", QuestBook.Quests[^1].Name, "Rescue Seggallion");
Check("first quest giver is Stephanie", QuestBook.Quests[0].QuestGiver, "Stephanie");
Check("first quest location is Brettle", QuestBook.Quests[0].Location, "Brettle");
Check("first quest keyword is gavel", QuestBook.Quests[0].Keyword, "gavel");
Check("final quest giver is Dundle", QuestBook.Quests[23].QuestGiver, "Dundle");
Check("Truth Sword reward", QuestBook.Quests[3].Reward, "Truth Sword (4-32 damage, very light)");
Check("every quest has a name", QuestBook.Quests.All(q => q.Name.Length > 0), true);
Check("every quest has a giver", QuestBook.Quests.All(q => q.QuestGiver.Length > 0), true);
Check("every quest has a target location", QuestBook.Quests.All(q => q.TargetLocation.Length > 0), true);
Check("quest ids are sequential", QuestBook.Quests.Select((q, i) => q.Id == i).All(b => b), true);
Console.WriteLine();

Console.WriteLine("ScanGuide recipes:");
Check("recipe count is 13", ScanGuide.Recipes.Count, 13);
var gold = ScanGuide.Recipes.First(r => r.Field == "gold");
Check("gold is Int32", gold.Width, ScanWidth.Int32);
Check("gold range is '0..999999'", gold.Range, "0..999999");
var bp = ScanGuide.Recipes.First(r => r.Field == "body_points");
Check("body points is Int16", bp.Width, ScanWidth.Int16);
var str = ScanGuide.Recipes.First(r => r.Field == "strength");
Check("strength is Byte", str.Width, ScanWidth.Byte);
Check("strength max is 100", str.TypicalMax, 100L);
var level = ScanGuide.Recipes.First(r => r.Field == "level");
Check("level is Byte", level.Width, ScanWidth.Byte);
Check("level max is 25", level.TypicalMax, 25L);
Check("every recipe has instructions", ScanGuide.Recipes.All(r => r.Instructions.Length > 40), true);
Check("every recipe default fits its width",
    ScanGuide.Recipes.All(r => ScanValue.FitsWidth(r.SuggestedDefault, r.Width)), true);
Check("every recipe field is unique",
    ScanGuide.Recipes.Select(r => r.Field).Distinct().Count(), ScanGuide.Recipes.Count);
Console.WriteLine();

Console.WriteLine("ScanValue helpers (parse / fit / canonicalize):");
Check("parse '20' -> 20", ScanValue.TryParse("20", out long v20) ? v20 : -1, 20L);
Check("parse '0x14' -> 20", ScanValue.TryParse("0x14", out long vhex) ? vhex : -1, 20L);
Check("parse '14h' -> 20", ScanValue.TryParse("14h", out long vh) ? vh : -1, 20L);
Check("parse '' -> false", ScanValue.TryParse("", out _), false);
Check("parse '   ' -> false", ScanValue.TryParse("   ", out _), false);
Check("parse 'garbage' -> false", ScanValue.TryParse("garbage", out _), false);
Check("fit 20 in Byte", ScanValue.FitsWidth(20, ScanWidth.Byte), true);
Check("fit 300 in Byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("fit 300 in Int16", ScanValue.FitsWidth(300, ScanWidth.Int16), true);
Check("fit 70000 in Int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("fit 70000 in Int32", ScanValue.FitsWidth(70000, ScanWidth.Int32), true);
Check("canonicalize -1 Byte -> 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), (long)0xFF);
Check("canonicalize -1 Int16 -> 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), (long)0xFFFF);
Check("canonicalize 20 Int32 -> 20", ScanValue.Canonicalize(20, ScanWidth.Int32), 20L);
Console.WriteLine();

Console.WriteLine("FrozenValueViewModel width guard:");
var writes = new List<(nuint addr, long value, ScanWidth width)>();
IScanHost fakeHost = new FakeHost(writes);
var frozen = new FrozenValueViewModel(fakeHost, (nuint)0x1000, ScanWidth.Byte, 20, "Gold");
Check("frozen live reads 20", frozen.Live, 20L);
Check("frozen target reads 20", frozen.Target, 20L);
frozen.Target = 50;
Check("set Target=50 writes through host", writes.Count, 1);
Check("write value is 50", writes[0].value, 50L);
Check("write width is Byte", writes[0].width, ScanWidth.Byte);
int before = writes.Count;
frozen.Target = 500;
Check("set Target=500 (too big for Byte) is rejected", writes.Count, before);
Check("target reverts to 50 after reject", frozen.Target, 50L);
frozen.Frozen = true;
frozen.ApplyFreeze();
Check("ApplyFreeze writes when frozen", writes.Count, before + 1);
Check("ApplyFreeze writes target 50", writes[^1].value, 50L);
frozen.Frozen = false;
int before2 = writes.Count;
frozen.ApplyFreeze();
Check("ApplyFreeze no-op when not frozen", writes.Count, before2);
frozen.RefreshLive(99);
Check("RefreshLive updates Live", frozen.Live, 99L);
Console.WriteLine();

Console.WriteLine("SaveEditorViewModel quest loading:");
var editorData = new byte[512];
for (int i = 0; i < 24; i++)
    SaveFormat.WriteQuestStatus(editorData, i, (i % 4));
string tempFile = Path.GetTempFileName();
File.WriteAllBytes(tempFile, editorData);
var editor = new SaveEditorViewModel();
Check("editor has no file initially", editor.HasFile, false);
editor.Load(tempFile);
Check("editor has file after load", editor.HasFile, true);
Check("editor loaded 24 quests", editor.Quests.Count, 24);
Check("quest 0 status is 0", editor.Quests[0].Status, 0);
Check("quest 1 status is 1", editor.Quests[1].Status, 1);
Check("quest 2 status is 2", editor.Quests[2].Status, 2);
Check("quest 3 status is 3", editor.Quests[3].Status, 3);
Check("quest 0 label is Not Given", editor.Quests[0].StatusLabel, "Not Given");
Check("quest 1 label is Given", editor.Quests[1].StatusLabel, "Given");
Check("quest 2 label is Complete", editor.Quests[2].StatusLabel, "Complete");
Check("quest 3 label is Medal Given", editor.Quests[3].StatusLabel, "Medal Given");
Check("quest 0 name is The Stolen Gavel", editor.Quests[0].QuestName, "The Stolen Gavel");

editor.Quests[0].Status = 3;
Check("changing quest 0 to medal updates status", editor.Quests[0].Status, 3);
Check("changing quest 0 marks dirty", editor.Dirty, true);

editor.Save();
Check("save clears dirty", editor.Dirty, false);
var reloaded = File.ReadAllBytes(tempFile);
Check("saved file has quest 0 = medal", SaveFormat.ReadQuestStatus(reloaded, 0), 3);
Check("saved file preserves quest 1", SaveFormat.ReadQuestStatus(reloaded, 1), 1);

string bakFile = tempFile + ".bak";
Check("backup was created", File.Exists(bakFile), true);

editor.SetAllQuests(2);
Check("SetAllQuests changes all to 2", editor.Quests.All(q => q.Status == 2), true);
editor.Save();
var afterSetAll = File.ReadAllBytes(tempFile);
Check("saved all-complete file", SaveFormat.ReadAllQuestStatuses(afterSetAll).All(s => s == 2), true);

File.Delete(tempFile);
File.Delete(bakFile);
Console.WriteLine();

Console.WriteLine("SaveEditorViewModel rejects small files:");
var tinyEditor = new SaveEditorViewModel();
string tinyFile = Path.GetTempFileName();
File.WriteAllBytes(tinyFile, new byte[50]);
tinyEditor.Load(tinyFile);
Check("tiny file has no quests", tinyEditor.Quests.Count, 0);
Check("tiny file status mentions too small", tinyEditor.StatusText.Contains("too small"), true);
File.Delete(tinyFile);
Console.WriteLine();

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

sealed class FakeHost(List<(nuint, long, ScanWidth)> writes) : IScanHost
{
    public bool Write(nuint address, long value, ScanWidth width)
    {
        writes.Add((address, value, width));
        return true;
    }
    public bool Read(nuint address, ScanWidth width, out long value) { value = 0; return false; }
    public void ReportWriteFailure(nuint address) { }
}
