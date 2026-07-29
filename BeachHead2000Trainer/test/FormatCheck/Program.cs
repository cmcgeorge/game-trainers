// Headless verification for the BeachHead 2000 trainer. Exercises the level-file parser
// (parse + round-trip + field extraction), the game-facts constants, the scan-value parsing
// helpers, and the frozen-value write / freeze / width-guard logic. Exits 0 on success,
// 1 on any failure so it can gate the build (Run.ps1 -Test). No live process or copyrighted
// game file is touched — the level-file tests use a synthetic fixture built from the Confirmed
// format observed in the shipped Level_00.

using BeachHead2000Trainer.Game;
using BeachHead2000Trainer.ViewModels;
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
    bool b => b.ToString(),
    int i => i.ToString(),
    long l => l.ToString(),
    string s => $"\"{s}\"",
    _ => v.ToString() ?? "null",
};

// Builds a synthetic level file that reproduces the format of the shipped Level_00
// (Ammo 100 1 1, Time 60, Aggression 1 1 1 1, Artillery 0, one Object section, End).
static string BuildLevelFixture() => """
//Bullets, Projectiles, Missiles
Ammo 100 1 1
Time 60
//Tank, Jet, HelicopterGun, HelicopterRocket (range:1-9)
Aggression 1 1 1 1
Artillery 0


/*** Infantry Barges ***/
Object Barge
Visible 0

ObjectInc
Visible 0

End
""";

Console.WriteLine("Level-file parser (parse + fields):");
var lf = LevelFile.Parse(BuildLevelFixture());
Check("bullets parsed", lf.Bullets, 100);
Check("projectiles parsed", lf.Projectiles, 1);
Check("missiles parsed", lf.Missiles, 1);
Check("time parsed", lf.Time, 60);
Check("aggr tank parsed", lf.AggressionTank, 1);
Check("aggr jet parsed", lf.AggressionJet, 1);
Check("aggr heliGun parsed", lf.AggressionHeliGun, 1);
Check("aggr heliRocket parsed", lf.AggressionHeliRocket, 1);
Check("artillery parsed", lf.Artillery, 0);
Check("lines preserved", lf.Lines.Count > 0, true);
Console.WriteLine();

Console.WriteLine("Level-file round-trip (edit + serialize):");
lf.Bullets = 999;
lf.Projectiles = 99;
lf.Missiles = 99;
lf.Time = 200;
lf.AggressionTank = 9;
var text = lf.ToText();
var lf2 = LevelFile.Parse(text);
Check("round-trip bullets", lf2.Bullets, 999);
Check("round-trip projectiles", lf2.Projectiles, 99);
Check("round-trip missiles", lf2.Missiles, 99);
Check("round-trip time", lf2.Time, 200);
Check("round-trip aggr tank", lf2.AggressionTank, 9);
Check("round-trip preserves End marker", lf2.Lines.Contains("End"), true);
Check("round-trip preserves comment", lf2.Lines.Any(l => l.Contains("Infantry Barges")), true);
Console.WriteLine();

Console.WriteLine("Level-file edge cases:");
var minimal = LevelFile.Parse("Ammo 50 5 5\r\nTime 30\r\nEnd\r\n");
Check("minimal bullets", minimal.Bullets, 50);
Check("minimal time", minimal.Time, 30);
var empty = LevelFile.Parse("");
Check("empty file produces zero bullets", empty.Bullets, 0);
Check("empty file produces zero time", empty.Time, 0);
var level60 = LevelFile.Parse("Ammo 200 3 10\r\nTime 200\r\nAggression 9 9 9 9\r\nArtillery 0\r\nEnd\r\n");
Check("level-60-style bullets", level60.Bullets, 200);
Check("level-60-style missiles", level60.Missiles, 10);
Check("level-60-style aggr all max", (level60.AggressionTank, level60.AggressionJet, level60.AggressionHeliGun, level60.AggressionHeliRocket), (9, 9, 9, 9));
Console.WriteLine();

Console.WriteLine("Game facts (Confirmed constants):");
Check("process name is Bh", GameFacts.ProcessName, "Bh");
Check("image base is 0x400000", GameFacts.ImageBase, 0x00400000u);
Check("level count is 61", GameFacts.LevelCount, 61);
Check("first level is 0", GameFacts.FirstLevel, 0);
Check("last level is 60", GameFacts.LastLevel, 60);
Check("aggression min is 1", GameFacts.AggressionMin, 1);
Check("aggression max is 9", GameFacts.AggressionMax, 9);
Check("default health is 100", GameFacts.DefaultHealth, 100);
Check("max bullets is 999", GameFacts.MaxBullets, 999);
Check("weapon count is 3", WeaponInfo.Weapons.Count, 3);
Check("first weapon is Bullets", WeaponInfo.Weapons[0].Name, "Bullets");
Check("second weapon is Projectiles", WeaponInfo.Weapons[1].Name, "Projectiles");
Check("third weapon is Missiles", WeaponInfo.Weapons[2].Name, "Missiles");
Check("enemy count is 9", EnemyInfo.Enemies.Count, 9);
Check("first enemy is Infantry Barge", EnemyInfo.Enemies[0].Name, "Infantry Barge");
Check("last enemy is C-130", EnemyInfo.Enemies[^1].Name, "C-130");
Check("object types count is 8", GameFacts.ObjectTypes.Count, 8);
Check("aggression axes count is 4", GameFacts.AggressionAxes.Count, 4);
Console.WriteLine();

Console.WriteLine("Scan-value parsing:");
Check("decimal parse", TryParse("100"), 100L);
Check("hex 0x parse", TryParse("0x64"), 100L);
Check("hex suffix parse", TryParse("64h"), 100L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("zz", out _), false);
Check("100 fits a byte", ScanValue.FitsWidth(100, ScanWidth.Byte), true);
Check("300 does not fit a byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("30000 fits a word", ScanValue.FitsWidth(30000, ScanWidth.Int16), true);
Check("70000 does not fit a word", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("999 fits int32", ScanValue.FitsWidth(999, ScanWidth.Int32), true);
Check("-1 fits a byte (signed)", ScanValue.FitsWidth(-1, ScanWidth.Byte), true);
Check("-1 canonicalizes to a byte's 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), 0xFFL);
Check("-1 canonicalizes to a word's 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("a positive value canonicalizes unchanged", ScanValue.Canonicalize(999, ScanWidth.Int32), 999L);
Console.WriteLine();

Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Int32, 100, "Health");
Check("label is carried", pin.Label, "Health");
Check("target starts at the captured value", pin.Target, 100L);
pin.Target = 999;
Check("editing target pokes RAM", host.LastWrite, 999L);
Check("the poke uses the pin's captured width", host.LastWidth, ScanWidth.Int32);
Check("target updates", pin.Target, 999L);
pin.Frozen = true;
host.LastWrite = null;
pin.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 999L);

// A pin keeps its own width even if the host's active scan width later differs
var bytePin = new FrozenValueViewModel(host, (nuint)0x2000, ScanWidth.Byte, 50, "Bullets");
bytePin.Target = 300;  // does not fit a byte pin (byte range is 0..255)
Check("an out-of-width target is rejected", bytePin.Target, 50L);
bytePin.Target = 255;
Check("a max-byte target is accepted", bytePin.Target, 255L);
Check("byte pin writes at byte width", host.LastWidth, ScanWidth.Byte);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x3000, ScanWidth.Int32, 10);
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);

var failingFreeze = new CaptureHost { Succeed = false };
var pin3 = new FrozenValueViewModel(failingFreeze, (nuint)0x4000, ScanWidth.Int32, 10) { Frozen = true };
pin3.ApplyFreeze();
Check("a failed freeze re-write is reported", failingFreeze.Failures, 1);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s) { ScanValue.TryParse(s, out long v); return v; }

sealed class CaptureHost : IScanHost
{
    public bool Succeed { get; init; } = true;
    public long? LastWrite { get; set; }
    public ScanWidth LastWidth { get; private set; }
    public int Failures { get; private set; }

    public bool Write(nuint address, long value, ScanWidth width)
    {
        if (!Succeed) return false;
        LastWrite = value;
        LastWidth = width;
        return true;
    }

    public bool Read(nuint address, ScanWidth width, out long value) { value = 0; return false; }

    public void ReportWriteFailure(nuint address) => Failures++;
}
