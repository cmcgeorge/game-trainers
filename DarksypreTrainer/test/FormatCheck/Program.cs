using DarksypreTrainer.Game;
using DarksypreTrainer.ViewModels;
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

Console.WriteLine("GameFacts constants (Confirmed from manual/walkthrough):");
Check("game title", GameFacts.GameTitle, "DarkSpyre");
Check("developer", GameFacts.Developer, "Event Horizon Software");
Check("release year", GameFacts.ReleaseYear, 1990);
Check("max attribute is 20", GameFacts.MaxAttribute, 20);
Check("max spell points is 100", GameFacts.MaxSpellPoints, 100);
Check("total levels is 50", GameFacts.TotalLevels, 50);
Check("required levels is 39", GameFacts.RequiredLevels, 39);
Check("attribute count is 6", GameFacts.AttributeCount, 6);
Check("weapon type count is 7", GameFacts.WeaponTypeCount, 7);
Check("weapon proficiency levels is 10", GameFacts.WeaponProficiencyLevels, 10);
Check("magic class count is 6", GameFacts.MagicClassCount, 6);
Check("magic proficiency levels is 7", GameFacts.MagicProficiencyLevels, 7);
Check("armor protection levels is 15", GameFacts.ArmorProtectionLevels, 15);
Check("armor condition levels is 7", GameFacts.ArmorConditionLevels, 7);
Check("power rune count is 5", GameFacts.PowerRuneCount, 5);
Check("total runes is 25", GameFacts.TotalRunes, 25);
Console.WriteLine();

Console.WriteLine("ScanGuide recipes:");
Check("recipe count is 11", ScanGuide.Recipes.Count, 11);
var hp = ScanGuide.Recipes.First(r => r.Field == "hp");
Check("hp is Int16", hp.Width, ScanWidth.Int16);
Check("hp suggested default is 20", hp.SuggestedDefault, 20L);
var sp = ScanGuide.Recipes.First(r => r.Field == "sp");
Check("sp is Byte", sp.Width, ScanWidth.Byte);
Check("sp max is 100", sp.TypicalMax, (long)GameFacts.MaxSpellPoints);
var str = ScanGuide.Recipes.First(r => r.Field == "str");
Check("str is Byte", str.Width, ScanWidth.Byte);
Check("str range is '1..20'", str.Range, "1..20");
var score = ScanGuide.Recipes.First(r => r.Field == "score");
Check("score is Int32", score.Width, ScanWidth.Int32);
Check("score range is '0..999999'", score.Range, "0..999999");
var level = ScanGuide.Recipes.First(r => r.Field == "level");
Check("level is Int16", level.Width, ScanWidth.Int16);
Check("level max is 50", level.TypicalMax, (long)GameFacts.TotalLevels);
Console.WriteLine();

Console.WriteLine("SpellBook (Confirmed from manual/walkthrough):");
Check("spell count is 14", SpellBook.Spells.Count, 14);
Check("class name count is 6", SpellBook.ClassNames.Length, 6);
Check("proficiency name count is 7", SpellBook.ProficiencyNames.Length, 7);
Check("first class is Healing", SpellBook.ClassNames[0], "Healing");
Check("last class is Enchantry", SpellBook.ClassNames[^1], "Enchantry");
Check("Liquify is Healing", SpellBook.Spells.First(s => s.Name == "Liquify").Class, "Healing");
Check("Liquify costs 10 SP", SpellBook.Spells.First(s => s.Name == "Liquify").SpCost, 10);
Check("Fireball is Wizardry", SpellBook.Spells.First(s => s.Name == "Fireball").Class, "Wizardry");
Check("Fireball costs 20 SP", SpellBook.Spells.First(s => s.Name == "Fireball").SpCost, 20);
Check("Freeze is Enchantry", SpellBook.Spells.First(s => s.Name == "Freeze").Class, "Enchantry");
Check("Freeze costs 40 SP", SpellBook.Spells.First(s => s.Name == "Freeze").SpCost, 40);
var healingSpells = SpellBook.ByClass("Healing");
Check("Healing has 1 spell", healingSpells.Count, 1);
var sorcerySpells = SpellBook.ByClass("Sorcery");
Check("Sorcery has 3 spells", sorcerySpells.Count, 3);
Console.WriteLine();

Console.WriteLine("WeaponBook (Confirmed from manual):");
Check("weapon type count is 7", WeaponBook.Types.Count, 7);
Check("proficiency name count is 10", WeaponBook.ProficiencyNames.Length, 10);
Check("first type is Clubbing", WeaponBook.Types[0].Name, "Clubbing");
Check("last type is Thrusting", WeaponBook.Types[^1].Name, "Thrusting");
Check("first proficiency is None", WeaponBook.ProficiencyNames[0], "None");
Check("last proficiency is Expert", WeaponBook.ProficiencyNames[^1], "Expert");
Check("ById(0) is Clubbing", WeaponBook.ById(0)?.Name, "Clubbing");
Check("ById(6) is Thrusting", WeaponBook.ById(6)?.Name, "Thrusting");
Check("ById(-1) is null", WeaponBook.ById(-1), null);
Check("ById(7) is null", WeaponBook.ById(7), null);
Console.WriteLine();

Console.WriteLine("MonsterBook (Confirmed from walkthrough):");
Check("monster count is 14", MonsterBook.Monsters.Count, 14);
Check("category count is 5", MonsterBook.Categories.Count, 5);
Check("first monster is Wraith", MonsterBook.Monsters[0].Name, "Wraith");
Check("last monster is Djinn", MonsterBook.Monsters[^1].Name, "Djinn");
Check("Jester is Ground Projectile", MonsterBook.Monsters.First(m => m.Name == "Jester").Category, "Ground Projectile");
Check("Slime is Slither Poison", MonsterBook.Monsters.First(m => m.Name == "Slime").Category, "Slither Poison");
Check("Beholder is Flying Projectile", MonsterBook.Monsters.First(m => m.Name == "Beholder").Category, "Flying Projectile");
Console.WriteLine();

Console.WriteLine("RuneBook (Confirmed from manual):");
Check("rune count is 25", RuneBook.Runes.Count, 25);
Check("power rune count is 5", RuneBook.PowerRunes.Count, 5);
Check("first rune is Uraz (Strength)", RuneBook.Runes[0].Norse, "Uraz");
Check("first rune is power rune", RuneBook.Runes[0].IsPowerRune, true);
Check("Raido is not power rune", RuneBook.Runes.First(r => r.Norse == "Raido").IsPowerRune, false);
Check("Raido saves game", RuneBook.Runes.First(r => r.Norse == "Raido").Effect, "Saves the game (one use per rune)");
Check("Thurisaz is Gateway", RuneBook.Runes.First(r => r.Norse == "Thurisaz").English, "Gateway");
Check("all 5 power runes have IsPowerRune", RuneBook.PowerRunes.All(r => r.IsPowerRune), true);
Check("power rune names", string.Join(",", RuneBook.PowerRunes.Select(r => r.Norse)),
    "Uraz,Ehwaz,Eihwaz,Teiwaz,Inguz");
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
var frozen = new FrozenValueViewModel(fakeHost, (nuint)0x1000, ScanWidth.Byte, 20, "HP");
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
