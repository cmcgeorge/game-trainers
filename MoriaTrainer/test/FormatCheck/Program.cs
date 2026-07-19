// Headless verification for the UMoria trainer. Exercises the game-knowledge layer (the Confirmed
// stat encoding, the cave cell constants, the roster/level/item counts, the curated monster roster
// including the Balrog) plus the pure value-scanner helpers (ScanValue parse / width-fit / canonicalize,
// FrozenValue width guard, ScanRecipe ranges). Exits 0 on success, 1 on any failure so it can gate
// the build (Run.ps1 -Test). No live process, emulator, or copyrighted game file is touched.

using MoriaTrainer.Game;
using MoriaTrainer.ViewModels;
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

Console.WriteLine("Player format constants (Confirmed from constant.h):");
Check("game version is 5.5.2", PlayerFormat.GameVersion, "5.5.2");
Check("max character level is 40", PlayerFormat.MaxLevel, 40);
Check("creature roster size is 279", PlayerFormat.MaxCreatures, 279);
Check("object table size is 420", PlayerFormat.MaxObjects, 420);
Check("cave rows is 66", PlayerFormat.CaveRows, 66);
Check("cave cols is 198", PlayerFormat.CaveCols, 198);
Check("backpack pack slots is 22", PlayerFormat.InvenPack, 22);
Check("total inventory slots is 34", PlayerFormat.InvenArraySize, 34);
Check("cave cell size is 4 bytes", PlayerFormat.CaveCellSize, 4);
Check("cave grid bytes is 66*198*4", PlayerFormat.CaveBytes, 66 * 198 * 4);
Check("wield slot is 22", PlayerFormat.InvenWield, 22);
Check("body slot is 25", PlayerFormat.InvenBody, 25);
Check("feet slot is 30", PlayerFormat.InvenFeet, 30);
Console.WriteLine();

Console.WriteLine("Cave fval constants (Confirmed from constant.h):");
Check("quartz vein fval is 1", PlayerFormat.FvalQuartzVein, (byte)1);
Check("granite wall fval is 3", PlayerFormat.FvalGraniteWall, (byte)3);
Check("floor fval is 5", PlayerFormat.FvalFloor, (byte)5);
Check("stair floor fval is 10", PlayerFormat.FvalStairFloor, (byte)10);
Check("rubble fval is 14", PlayerFormat.FvalRubble, (byte)14);
Console.WriteLine();

Console.WriteLine("Stat encoding (3..18/100, Confirmed):");
Check("decode 3 -> 3", PlayerFormat.DecodeStat(3, 0), 3);
Check("decode 18/00 -> 18", PlayerFormat.DecodeStat(18, 0), 18);
Check("decode 18/40 -> 58", PlayerFormat.DecodeStat(18, 40), 58);
Check("decode 18/100 -> 118", PlayerFormat.DecodeStat(18, 100), 118);
Check("encode 3 -> (3,0)", PlayerFormat.EncodeStat(3), (3, 0));
Check("encode 18 -> (18,0)", PlayerFormat.EncodeStat(18), (18, 0));
Check("encode 58 -> (18,40)", PlayerFormat.EncodeStat(58), (18, 40));
Check("encode 118 -> (18,100)", PlayerFormat.EncodeStat(118), (18, 100));
Check("encode 200 clamps to (18,100)", PlayerFormat.EncodeStat(200), (18, 100));
Check("format 3 -> '3'", PlayerFormat.FormatStat(3), "3");
Check("format 18 -> '18'", PlayerFormat.FormatStat(18), "18");
Check("format 58 -> '18/40'", PlayerFormat.FormatStat(58), "18/40");
Check("format 118 -> '18/100'", PlayerFormat.FormatStat(118), "18/100");
Console.WriteLine();

Console.WriteLine("Monster book (curated roster, Confirmed Balrog id):");
Check("Balrog id is 23", MonsterBook.BalrogId, 23);
Check("Balrog entry exists", MonsterBook.Balrog is not null, true);
Check("Balrog name", MonsterBook.Balrog!.Name, "Balrog of Moria");
Check("Balrog is on depth 49", MonsterBook.Balrog.Level, 49);
Check("Balrog is a Balrog", MonsterBook.Balrog.IsBalrog, true);
Check("roster has at least 25 entries", MonsterBook.Creatures.Count >= 25, true);
Check("Balrog is the last entry", MonsterBook.Creatures[^1].Id, MonsterBook.BalrogId);
Console.WriteLine();

Console.WriteLine("Level book (51-level descent):");
Check("level count is 21 (curated subset)", LevelBook.Levels.Count, 21);
Check("town is depth 0", LevelBook.Town.Depth, 0);
Check("town is town", LevelBook.Town.IsTown, true);
Check("balrog level is depth 50", LevelBook.BalrogLevel.Depth, 50);
Check("balrog level is balrog", LevelBook.BalrogLevel.IsBalrogLevel, true);
Check("ByDepth(0) is town", LevelBook.ByDepth(0)?.IsTown, true);
Check("ByDepth(50) is balrog", LevelBook.ByDepth(50)?.IsBalrogLevel, true);
Check("ByDepth(99) is null", LevelBook.ByDepth(99), null);
Console.WriteLine();

Console.WriteLine("Item book (Confirmed tval categories):");
Check("item count is 27", ItemBook.Items.Count, 27);
Check("first item is Shovel (tval 1)", ItemBook.Items[0].Tval, 1);
Check("ByTval(1) is Shovel", ItemBook.ByTval(1)?.Category, "Shovel");
Check("ego weapons count is 8", ItemBook.EgoWeapons.Count, 8);
Check("crowns count is 6", ItemBook.Crowns.Count, 6);
Check("wearable flags count is 16", ItemBook.WearableFlags.Count, 16);
Console.WriteLine();

Console.WriteLine("Spell book (Confirmed 31 mage + 31 priest):");
Check("mage spell count is 31", SpellBook.MageSpells.Count, 31);
Check("priest prayer count is 31", SpellBook.PriestPrayers.Count, 31);
Check("first mage spell is Magic Missile", SpellBook.MageSpells[0].Name, "Magic Missile");
Check("first mage letter is 'a'", SpellBook.MageSpells[0].Letter, "a");
Check("last mage letter is 'E'", SpellBook.MageSpells[^1].Letter, "E");
Check("last priest letter is 'E'", SpellBook.PriestPrayers[^1].Letter, "E");
Check("all spell letters are unique within mage", SpellBook.MageSpells.Select(s => s.Letter).Distinct().Count(), 31);
Check("all spell letters are unique within priest", SpellBook.PriestPrayers.Select(s => s.Letter).Distinct().Count(), 31);
Check("Word of Destruction is offensive", SpellBook.MageSpells.First(s => s.Name == "Word of Destruction").IsOffensive, false);
Check("Magic Missile is offensive", SpellBook.MageSpells.First(s => s.Name == "Magic Missile").IsOffensive, true);
Console.WriteLine();

Console.WriteLine("Race & class books (Confirmed):");
Check("race count is 8", RaceBook.Races.Count, 8);
Check("first race is Human", RaceBook.Races[0].Name, "Human");
Check("last race is Half-Troll", RaceBook.Races[^1].Name, "Half-Troll");
Check("Half-Troll is Warrior only", RaceBook.Races[^1].AllowedClasses, "0");
Check("Human can be Mage (1)", RaceBook.Races[0].CanBe(1), true);
// P0 regression guards: the fixed race/class tables. Halfling/Gnome/Half-Orc must allow Rogue (3),
// not Paladin (5); Elf uses the "any except Paladin" rule.
Check("Halfling allows Warrior/Mage/Rogue", RaceBook.Races[RaceBook.RaceHalfling].AllowedClasses, "0,1,3");
Check("Halfling can be Rogue (3)", RaceBook.Races[RaceBook.RaceHalfling].CanBe(ClassBook.ClassRogue), true);
Check("Halfling cannot be Paladin (5)", RaceBook.Races[RaceBook.RaceHalfling].CanBe(ClassBook.ClassPaladin), false);
Check("Gnome allows Warrior/Mage/Priest/Rogue", RaceBook.Races[RaceBook.RaceGnome].AllowedClasses, "0,1,2,3");
Check("Gnome can be Priest (2)", RaceBook.Races[RaceBook.RaceGnome].CanBe(ClassBook.ClassPriest), true);
Check("Half-Orc allows Warrior/Priest/Rogue", RaceBook.Races[RaceBook.RaceHalfOrc].AllowedClasses, "0,2,3");
Check("Elf cannot be Paladin (any-except)", RaceBook.Races[RaceBook.RaceElf].CanBe(ClassBook.ClassPaladin), false);
Check("Elf can be Warrior", RaceBook.Races[RaceBook.RaceElf].CanBe(ClassBook.ClassWarrior), true);
Check("class count is 6", ClassBook.Classes.Count, 6);
Check("first class is Warrior", ClassBook.Classes[0].Name, "Warrior");
Check("Warrior prime is STR", ClassBook.Classes[0].PrimeStat, "STR");
Check("Mage hit die is d4", ClassBook.Classes[1].HitDie, "d4");
Console.WriteLine();

Console.WriteLine("Paragraph book (recall renderer):");
var balrog = MonsterBook.Balrog!;
string para = ParagraphBook.Render(balrog);
Check("Balrog paragraph mentions the win condition", para.Contains("WIN CONDITION", StringComparison.OrdinalIgnoreCase), true);
Check("Balrog paragraph mentions its name", para.Contains("Balrog", StringComparison.OrdinalIgnoreCase), true);
Check("Render(unknown id) returns null", ParagraphBook.Render(9999), null);
var mice = ParagraphBook.Search("Mouse").ToList();
Check("search 'Mouse' finds the Mouse", mice.Count >= 1, true);
Check("search 'Mouse' first result is Mouse", mice[0].Name, "Mouse");
Console.WriteLine();

Console.WriteLine("ScanGuide (guided scan recipes):");
Check("recipe count is 14", ScanGuide.Recipes.Count, 14);
var chp = ScanGuide.Recipes.First(r => r.Field == "chp");
Check("chp is Int32", chp.Width, ScanWidth.Int32);
Check("chp suggested default is 30", chp.SuggestedDefault, 30L);
var lev = ScanGuide.Recipes.First(r => r.Field == "lev");
Check("lev is Int16", lev.Width, ScanWidth.Int16);
Check("lev max is 40 (MaxLevel)", lev.TypicalMax, (long)PlayerFormat.MaxLevel);
var str = ScanGuide.Recipes.First(r => r.Field == "str");
Check("str is Byte", str.Width, ScanWidth.Byte);
Check("str range is '3..100'", str.Range, "3..100");
Console.WriteLine();

Console.WriteLine("ScanValue helpers (parse / fit / canonicalize):");
Check("parse '30' -> 30", ScanValue.TryParse("30", out long v30) ? v30 : -1, 30L);
Check("parse '0x1E' -> 30", ScanValue.TryParse("0x1E", out long vhex) ? vhex : -1, 30L);
Check("parse '1Eh' -> 30", ScanValue.TryParse("1Eh", out long vh) ? vh : -1, 30L);
Check("parse '' -> false", ScanValue.TryParse("", out _), false);
Check("parse '   ' -> false", ScanValue.TryParse("   ", out _), false);
Check("parse 'garbage' -> false", ScanValue.TryParse("garbage", out _), false);
Check("fit 30 in Byte", ScanValue.FitsWidth(30, ScanWidth.Byte), true);
Check("fit 300 in Byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("fit 300 in Int16", ScanValue.FitsWidth(300, ScanWidth.Int16), true);
Check("fit 70000 in Int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("fit 70000 in Int32", ScanValue.FitsWidth(70000, ScanWidth.Int32), true);
Check("canonicalize -1 Byte -> 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), (long)0xFF);
Check("canonicalize -1 Int16 -> 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), (long)0xFFFF);
Check("canonicalize 30 Int32 -> 30", ScanValue.Canonicalize(30, ScanWidth.Int32), 30L);
Console.WriteLine();

Console.WriteLine("FrozenValueViewModel width guard:");
// A fake host that records writes but never touches real memory.
var writes = new List<(nuint addr, long value, ScanWidth width)>();
IScanHost fakeHost = new FakeHost(writes);
var frozen = new FrozenValueViewModel(fakeHost, (nuint)0x1000, ScanWidth.Byte, 30)
{
    Label = "Current HP"
};
Check("frozen live reads 30", frozen.Live, 30L);
Check("frozen target reads 30", frozen.Target, 30L);
frozen.Target = 50;
Check("set Target=50 writes through host", writes.Count, 1);
Check("write value is 50", writes[0].value, 50L);
Check("write width is Byte", writes[0].width, ScanWidth.Byte);
int before = writes.Count;
frozen.Target = 500;  // doesn't fit Byte -> rejected
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
