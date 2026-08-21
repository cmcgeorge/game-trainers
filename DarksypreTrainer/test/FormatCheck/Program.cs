using System.IO;
using DarksypreTrainer.Game;
using DarksypreTrainer.Memory;
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

Console.WriteLine("GameFacts constants:");
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
Check("creature count is 35", GameFacts.CreatureCount, 35);
Check("object count is 162", GameFacts.ObjectCount, 162);
Check("object record size is 57", GameFacts.ObjectRecordSize, 57);
Console.WriteLine();

Console.WriteLine("CharacterFormat layout (confirmed against a live DOSBox session):");
Check("status block is 12 bytes", CharacterFormat.StatusSize, 12);
Check("status current HP at +0", CharacterFormat.StatusCurrentHp, 0);
Check("status current SP at +2", CharacterFormat.StatusCurrentSp, 2);
Check("status current ENC at +4", CharacterFormat.StatusCurrentEnc, 4);
Check("status max HP at +6", CharacterFormat.StatusMaxHp, 6);
Check("status max SP at +8", CharacterFormat.StatusMaxSp, 8);
Check("status max ENC at +10", CharacterFormat.StatusMaxEnc, 10);
Check("record is 12 bytes", CharacterFormat.RecordSize, 12);
Check("record attributes at +0", CharacterFormat.RecordAttributes, 0);
Check("record max HP at +6", CharacterFormat.RecordMaxHp, 6);
Check("attribute count is 6", CharacterFormat.AttributeCount, 6);
Check("attribute names count is 6", CharacterFormat.AttributeNames.Length, 6);
Check("first attribute is Strength", CharacterFormat.AttributeNames[0], "Strength");
Check("last attribute is Power", CharacterFormat.AttributeNames[^1], "Power");
Check("actor record is 0x56 bytes", CharacterFormat.ActorSize, 0x56);
Check("actor current HP at +0x10", CharacterFormat.ActorCurrentHp, 0x10);
Check("actor current SP at +0x12", CharacterFormat.ActorCurrentSp, 0x12);
Check("actor name at +0x1D", CharacterFormat.ActorName, 0x1D);
Check("actor name is 'player'", CharacterFormat.PlayerActorName, "player");

var word = new byte[2];
CharacterFormat.WriteU16(word, 0, 400);
Check("WriteU16 is little-endian (low)", word[0], (byte)0x90);
Check("WriteU16 is little-endian (high)", word[1], (byte)0x01);
Check("ReadU16 round-trips", CharacterFormat.ReadU16(word, 0), 400);
Console.WriteLine();

Console.WriteLine("CharacterFormat validation:");
// The values below are the ones observed in the live session documented in
// docs/ReverseEngineering.md: STR 15 AGI 13 END 11 ACC 10 TAL 14 PWR 12, HP/SP 39, ENC 0/75.
var status = new byte[CharacterFormat.StatusSize];
CharacterFormat.WriteU16(status, CharacterFormat.StatusCurrentHp, 39);
CharacterFormat.WriteU16(status, CharacterFormat.StatusCurrentSp, 39);
CharacterFormat.WriteU16(status, CharacterFormat.StatusCurrentEnc, 0);
CharacterFormat.WriteU16(status, CharacterFormat.StatusMaxHp, 39);
CharacterFormat.WriteU16(status, CharacterFormat.StatusMaxSp, 39);
CharacterFormat.WriteU16(status, CharacterFormat.StatusMaxEnc, 75);
Check("status block accepted", CharacterFormat.IsStatusBlock(status, 0, 39, 39), true);
Check("status block rejected for other HP", CharacterFormat.IsStatusBlock(status, 0, 40, 39), false);

var overfull = (byte[])status.Clone();
CharacterFormat.WriteU16(overfull, CharacterFormat.StatusMaxHp, 30);
Check("current above maximum is rejected", CharacterFormat.IsStatusBlock(overfull, 0, 39, 39), false);

var zeroMax = (byte[])status.Clone();
CharacterFormat.WriteU16(zeroMax, CharacterFormat.StatusMaxEnc, 0);
Check("zero max encumbrance is rejected", CharacterFormat.IsStatusBlock(zeroMax, 0, 39, 39), false);

var record = new byte[CharacterFormat.RecordSize];
byte[] attrs = { 15, 13, 11, 10, 14, 12 };
Array.Copy(attrs, 0, record, CharacterFormat.RecordAttributes, attrs.Length);
CharacterFormat.WriteU16(record, CharacterFormat.RecordMaxHp, 39);
CharacterFormat.WriteU16(record, CharacterFormat.RecordMaxSp, 39);
CharacterFormat.WriteU16(record, CharacterFormat.RecordMaxEnc, 75);
Check("character record accepted", CharacterFormat.IsCharacterRecord(record, 0, 39, 39, 75), true);
Check("record rejected against other maxima", CharacterFormat.IsCharacterRecord(record, 0, 40, 39, 75), false);

var zeroAttr = (byte[])record.Clone();
zeroAttr[0] = 0;
Check("zero attribute is rejected", CharacterFormat.IsCharacterRecord(zeroAttr, 0, 39, 39, 75), false);

var hugeAttr = (byte[])record.Clone();
hugeAttr[0] = 200;
Check("out-of-range attribute is rejected", CharacterFormat.IsCharacterRecord(hugeAttr, 0, 39, 39, 75), false);

var actor = BuildActor(39, 39);
Check("player actor accepted", CharacterFormat.IsPlayerActor(actor, 0), true);
var notPlayer = (byte[])actor.Clone();
notPlayer[CharacterFormat.ActorName] = (byte)'s';
Check("other creature rejected", CharacterFormat.IsPlayerActor(notPlayer, 0), false);
var deadActor = (byte[])actor.Clone();
CharacterFormat.WriteU16(deadActor, CharacterFormat.ActorCurrentHp, 0);
Check("zero-HP actor rejected", CharacterFormat.IsPlayerActor(deadActor, 0), false);
Console.WriteLine();

Console.WriteLine("CharacterLocator over a synthetic guest RAM:");
const int RamSize = 0x40000;
nuint ramBase = 0x10000000;
var ram = new byte[RamSize];

// Decoys first, so a locator that simply takes the first plausible-looking hit fails:
// a second creature record ("slime"), and a 12-byte window that looks like a status block
// for a different character.
var slime = BuildActor(42, 7);
Array.Copy(System.Text.Encoding.ASCII.GetBytes("slime\0"), 0, slime, CharacterFormat.ActorName, 6);
Array.Copy(slime, 0, ram, 0x800, slime.Length);
var decoyStatus = new byte[CharacterFormat.StatusSize];
CharacterFormat.WriteU16(decoyStatus, CharacterFormat.StatusCurrentHp, 12);
CharacterFormat.WriteU16(decoyStatus, CharacterFormat.StatusCurrentSp, 3);
CharacterFormat.WriteU16(decoyStatus, CharacterFormat.StatusMaxHp, 12);
CharacterFormat.WriteU16(decoyStatus, CharacterFormat.StatusMaxSp, 3);
CharacterFormat.WriteU16(decoyStatus, CharacterFormat.StatusMaxEnc, 40);
Array.Copy(decoyStatus, 0, ram, 0x900, decoyStatus.Length);

const int StatusOffset = 0x2410;
const int RecordOffset = 0x11D0;
const int ActorOffset = 0x1300;
Array.Copy(status, 0, ram, StatusOffset, status.Length);
Array.Copy(record, 0, ram, RecordOffset, record.Length);
Array.Copy(actor, 0, ram, ActorOffset, actor.Length);

var fake = new FakeMemory(ramBase, ram);
var located = CharacterLocator.Find(fake);
Check("locator found a character", located != null, true);
Check("actor address", located?.ActorAddress, ramBase + ActorOffset);
Check("status address", located?.StatusAddress, ramBase + StatusOffset);
Check("record address", located?.RecordAddress, ramBase + RecordOffset);
Check("located attributes", located == null ? "" : string.Join(",", located.Record.Take(6)), "15,13,11,10,14,12");
Check("located max HP", located == null ? -1 : CharacterFormat.ReadU16(located.Status, CharacterFormat.StatusMaxHp), 39);

// A structure that straddles the page-sized read boundary must still be found.
var ram2 = new byte[RamSize];
Array.Copy(actor, 0, ram2, 0x1000 - 4, actor.Length);
Array.Copy(status, 0, ram2, 0x2000 - 6, status.Length);
Array.Copy(record, 0, ram2, 0x3000 - 5, record.Length);
var straddled = CharacterLocator.Find(new FakeMemory(ramBase, ram2));
Check("structures spanning a page boundary are found", straddled != null, true);
Check("straddled actor address", straddled?.ActorAddress, ramBase + 0x1000 - 4);

// No character in play (the game's menus) must report nothing rather than guess.
Check("empty RAM yields no character", CharacterLocator.Find(new FakeMemory(ramBase, new byte[RamSize])), null);

// An actor with no matching status block is not enough on its own.
var actorOnly = new byte[RamSize];
Array.Copy(actor, 0, actorOnly, 0x1300, actor.Length);
Check("actor without status block yields nothing", CharacterLocator.Find(new FakeMemory(ramBase, actorOnly)), null);
Console.WriteLine();

// Optional: point the harness at a raw guest-RAM dump (`FormatCheck <dump.bin>`) to re-run the
// locator against real memory. Dumps are not committed, so this is skipped when no path is given.
if (args.Length > 0 && File.Exists(args[0]))
{
    Console.WriteLine($"CharacterLocator over the dump {Path.GetFileName(args[0])}:");
    var dump = File.ReadAllBytes(args[0]);
    var hit = CharacterLocator.Find(new FakeMemory(0, dump));
    Check("dump yields a character", hit != null, true);
    if (hit != null)
    {
        Console.WriteLine($"         actor  0x{(ulong)hit.ActorAddress:X}");
        Console.WriteLine($"         status 0x{(ulong)hit.StatusAddress:X}");
        Console.WriteLine($"         record 0x{(ulong)hit.RecordAddress:X}");
        Console.WriteLine($"         attributes {string.Join(",", hit.Record.Take(6))}");
        Check("dump attributes are in range", hit.Record.Take(6).All(b => b >= 1 && b <= 20), true);
        Check("dump maxima agree across structures",
            CharacterFormat.ReadU16(hit.Status, CharacterFormat.StatusMaxHp)
                == CharacterFormat.ReadU16(hit.Record, CharacterFormat.RecordMaxHp), true);
    }
    Console.WriteLine();
}

Console.WriteLine("CharacterViewModel writes to the right structure:");
var host = new FakeCharacterHost();
var vm = new CharacterViewModel(host, located!);
Check("current HP read from the actor snapshot", vm.CurrentHp, 39);
Check("current SP read from the actor snapshot", vm.CurrentSp, 39);
Check("actor vitals captured by the locator", located!.ActorVitals.Length, 4);
Check("max HP read from record", vm.MaxHp, 39);
Check("max ENC read from record", vm.MaxEncumbrance, 75);
Check("attribute rows", vm.Attributes.Count, 6);
Check("first attribute row is Strength 15", $"{vm.Attributes[0].Name} {vm.Attributes[0].Value}", "Strength 15");

vm.CurrentHp = 250;
Check("current HP writes to the actor", host.Last.address, located!.ActorAddress + CharacterFormat.ActorCurrentHp);
Check("current HP writes two bytes", host.Last.length, 2);
Check("current HP value", host.LastWord, 250);

vm.MaxHp = 400;
Check("max HP writes to the record", host.Last.address, located.RecordAddress + CharacterFormat.RecordMaxHp);
Check("max HP value", host.LastWord, 400);

vm.Attributes[2].Value = 19;
Check("attribute writes one byte to the record",
    host.Last.address, located.RecordAddress + (nuint)(CharacterFormat.RecordAttributes + 2));
Check("attribute write length", host.Last.length, 1);
Check("attribute value", host.Last.bytes[0], (byte)19);

vm.Attributes[2].Value = 99;
Check("attribute edits clamp to the game's cap of 20", host.Last.bytes[0], (byte)GameFacts.MaxAttribute);

vm.MaxAttributes();
Check("MaxAttributes raises every attribute", vm.Attributes.All(a => a.Value == GameFacts.MaxAttribute), true);

vm.Refill();
Check("Refill sets current HP to the maximum", vm.CurrentHp, vm.MaxHp);
Check("Refill sets current SP to the maximum", vm.CurrentSp, vm.MaxSp);

int writesBefore = host.Writes.Count;
vm.ApplyFreezes();
Check("no freeze means no writes", host.Writes.Count, writesBefore);
vm.FreezeHp = true;
vm.ApplyFreezes();
Check("freezing HP re-writes the actor", host.Writes.Count, writesBefore + 1);
Check("freeze targets the actor HP field", host.Last.address, located.ActorAddress + CharacterFormat.ActorCurrentHp);
Check("freeze re-writes the value held when it was ticked", host.LastWord, vm.MaxHp);

var stale = new byte[CharacterFormat.RecordSize];
Check("refresh rejects a record that no longer validates",
    vm.Refresh(status, stale, new byte[4]), false);
Console.WriteLine();

Console.WriteLine("ScanGuide recipes (only what the locator does not cover):");
Check("recipe count is 4", ScanGuide.Recipes.Count, 4);
var level = ScanGuide.Recipes.First(r => r.Field == "level");
Check("level is Byte", level.Width, ScanWidth.Byte);
Check("level max is 50", level.TypicalMax, (long)GameFacts.TotalLevels);
var score = ScanGuide.Recipes.First(r => r.Field == "score");
Check("score is Int16", score.Width, ScanWidth.Int16);
Check("score range is '0..65535'", score.Range, "0..65535");
Check("every recipe has instructions", ScanGuide.Recipes.All(r => r.Instructions.Length > 40), true);
Check("every recipe default fits its width",
    ScanGuide.Recipes.All(r => ScanValue.FitsWidth(r.SuggestedDefault, r.Width)), true);
Console.WriteLine();

Console.WriteLine("SpellBook (manual and walkthrough):");
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
Check("Healing has 1 spell", SpellBook.ByClass("Healing").Count, 1);
Check("Sorcery has 3 spells", SpellBook.ByClass("Sorcery").Count, 3);
Check("every spell belongs to a known class",
    SpellBook.Spells.All(s => SpellBook.ClassNames.Contains(s.Class)), true);
Console.WriteLine();

Console.WriteLine("WeaponBook (manual):");
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

Console.WriteLine("MonsterBook (decoded from CR.DAT):");
Check("monster count matches CR.DAT", MonsterBook.Monsters.Count, GameFacts.CreatureCount);
Check("first creature is Slime", MonsterBook.Monsters[0].Name, "Slime");
Check("the player is not listed", MonsterBook.Monsters.Any(m => m.Name == "Player"), false);
Check("Wraith is melee", MonsterBook.Monsters.First(m => m.Name == "Wraith").Ranged, false);
Check("Beholder attacks at range", MonsterBook.Monsters.First(m => m.Name == "Beholder").Ranged, true);
Check("Djinn attacks at range", MonsterBook.Monsters.First(m => m.Name == "Djinn").Ranged, true);
Check("Jester attacks at range", MonsterBook.Monsters.First(m => m.Name == "Jester").Ranged, true);
Check("Spartan Warrior has 25 Strength",
    MonsterBook.Monsters.First(m => m.Name == "Spartan Warrior").Strength, 25);
Check("ranged creatures", MonsterBook.Ranged.Count, 7);
Check("every creature has a name", MonsterBook.Monsters.All(m => m.Name.Length > 2), true);
Check("attributes stay inside the byte the record holds",
    MonsterBook.Monsters.All(m => m.Strength <= 40 && m.Agility <= 40 && m.Endurance <= 40
                               && m.Accuracy <= 40 && m.Talent <= 40 && m.Power <= 40), true);
Check("Attack column reads from Ranged",
    MonsterBook.Monsters.First(m => m.Name == "Beholder").Attack, "Ranged");
Console.WriteLine();

Console.WriteLine("ItemBook (decoded from OBJ.DAT):");
Check("object count matches OBJ.DAT", ItemBook.Items.Count, GameFacts.ObjectCount);
Check("ids are the table order", ItemBook.Items.Select((it, i) => it.Id == i).All(b => b), true);
Check("first object is Random Object", ItemBook.Items[0].Name, "Random Object");
Check("ById(1) is Bolt", ItemBook.ById(1)?.Name, "Bolt");
Check("ById(-1) is null", ItemBook.ById(-1), null);
Check("ById(past end) is null", ItemBook.ById(ItemBook.Items.Count), null);
Check("25 runes in the object table", ItemBook.Items.Count(i => i.Category == "Rune"), GameFacts.TotalRunes);
Check("Uraz Rune is a rune", ItemBook.Items.First(i => i.Name == "Uraz Rune").Category, "Rune");
Check("Fireball Scroll is magic", ItemBook.Items.First(i => i.Name == "Fireball Scroll").Category, "Magic");
Check("Jera Potion is a potion", ItemBook.Items.First(i => i.Name == "Jera Potion").Category, "Potion");
Check("Gold Key is a key", ItemBook.Items.First(i => i.Name == "Gold Key").Category, "Key");
Check("every object is named", ItemBook.Items.All(i => i.Name.Length > 0), true);
Console.WriteLine();

Console.WriteLine("RuneBook (game object table plus the manual's meanings):");
Check("rune count is 25", RuneBook.Runes.Count, GameFacts.TotalRunes);
Check("power rune count is 5", RuneBook.PowerRunes.Count, GameFacts.PowerRuneCount);
Check("first rune is Uraz", RuneBook.Runes[0].Norse, "Uraz");
Check("first rune is a power rune", RuneBook.Runes[0].IsPowerRune, true);
Check("Raido is not a power rune", RuneBook.Runes.First(r => r.Norse == "Raido").IsPowerRune, false);
Check("Raido saves the game", RuneBook.Runes.First(r => r.Norse == "Raido").Effect,
    "Saves the game (one save per rune)");
Check("Thurisaz is Gateway", RuneBook.Runes.First(r => r.Norse == "Thurisaz").English, "Gateway");
Check("power rune names", string.Join(",", RuneBook.PowerRunes.Select(r => r.Norse)),
    "Uraz,Ehwaz,Eihwaz,Teiwaz,Inguz");
Check("Kano keeps the manual's spelling as a variant",
    RuneBook.Runes.First(r => r.Norse == "Kano").ManualSpelling, "Keno");
Check("Othila keeps the manual's spelling as a variant",
    RuneBook.Runes.First(r => r.Norse == "Othila").ManualSpelling, "Othilia");
Check("Variant is blank when the spellings agree",
    RuneBook.Runes.First(r => r.Norse == "Uraz").Variant, "");
Check("every rune name appears in the game's object table",
    RuneBook.Runes.All(r => ItemBook.Items.Any(i => i.Name == r.Norse + " Rune")), true);
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

static byte[] BuildActor(int hp, int sp)
{
    var buf = new byte[CharacterFormat.ActorSize];
    CharacterFormat.WriteU16(buf, CharacterFormat.ActorCurrentHp, hp);
    CharacterFormat.WriteU16(buf, CharacterFormat.ActorCurrentSp, sp);
    var name = System.Text.Encoding.ASCII.GetBytes(CharacterFormat.PlayerActorName + "\0");
    Array.Copy(name, 0, buf, CharacterFormat.ActorName, name.Length);
    return buf;
}

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

/// <summary>A single flat region of fake guest RAM, so the locator can run with no game attached.</summary>
sealed class FakeMemory(nuint baseAddress, byte[] ram) : IMemorySource
{
    public IEnumerable<MemoryRegion> EnumerateRegions()
    {
        yield return new MemoryRegion(baseAddress, (nuint)ram.Length);
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        if (address < baseAddress) return 0;
        long offset = (long)(address - baseAddress);
        if (offset >= ram.Length) return 0;
        int n = (int)Math.Min(count, ram.Length - offset);
        if (n > buffer.Length) n = buffer.Length;
        Array.Copy(ram, offset, buffer, 0, n);
        return n;
    }
}

sealed class FakeCharacterHost : ICharacterHost
{
    public List<(nuint address, byte[] bytes, int length)> Writes { get; } = new();

    public bool IsAttached => true;

    // Mirrors ProcessMemory.WriteRange: the offset applies to the address as well as the source,
    // so what is recorded is the effective address the game would see.
    public bool WriteBytes(nuint structureAddress, byte[] source, int offset, int length)
    {
        var slice = new byte[length];
        Array.Copy(source, offset, slice, 0, length);
        Writes.Add((structureAddress + (nuint)offset, slice, length));
        return true;
    }

    public (nuint address, byte[] bytes, int length) Last => Writes[^1];

    public int LastWord => Last.length >= 2 ? Last.bytes[0] | (Last.bytes[1] << 8) : Last.bytes[0];
}
