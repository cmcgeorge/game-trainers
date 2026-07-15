// Headless verification harness for the BattleTech: The Crescent Hawk's Inception trainer. It asserts
// the game-knowledge layer (the Confirmed 17-byte weapon-table format, the 'Mech / weapon / skill
// references, the detection signatures) plus the pure value-scanner helpers and the frozen-value
// write/clamp logic. Exits 0 on success, 1 on any failure so it can gate the build (Run.ps1 -Test).
// No live process or emulator is touched.

using System.Text;
using BattleTech1Trainer.Game;
using BattleTech1Trainer.ViewModels;
using GameTrainers.Common.Memory;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {Fmt(actual)}, expected {Fmt(expected)}");
    if (!ok) failures++;
}

void CheckThrows<TException>(string name, Action act) where TException : Exception
{
    bool threw = false;
    try { act(); }
    catch (TException) { threw = true; }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {name}: threw {ex.GetType().Name}, expected {typeof(TException).Name}"); failures++; return; }
    Console.WriteLine($"  [{(threw ? "PASS" : "FAIL")}] {name}: {(threw ? "threw" : "did not throw")} {typeof(TException).Name}");
    if (!threw) failures++;
}

static string Fmt(object? v) => v switch
{
    null => "null",
    byte[] b => "[" + string.Join(",", b) + "]",
    _ => v.ToString() ?? "null",
};

// Builds a single 17-byte weapon record: an 11-byte NUL-padded name + 5 stat bytes + 1 class-tag byte,
// exactly the Confirmed on-disk layout (see .docs/ReverseEngineering.md §3.1).
static byte[] Record(string name, byte s0, byte s1, byte s2, byte s3, byte s4, byte classTag)
{
    var rec = new byte[WeaponTable.RecordSize];
    var bytes = Encoding.ASCII.GetBytes(name);
    Array.Copy(bytes, rec, Math.Min(bytes.Length, WeaponTable.NameLength));
    rec[11] = s0; rec[12] = s1; rec[13] = s2; rec[14] = s3; rec[15] = s4; rec[16] = classTag;
    return rec;
}

Console.WriteLine("Weapon table format (Confirmed 17-byte stride):");
Check("record size", WeaponTable.RecordSize, 17);
Check("name length", WeaponTable.NameLength, 11);

// A fixture of the first four Confirmed personal-weapon names, packed at the real 17-byte stride with
// representative stat/class-tag bytes. Verifies stride, name trimming, and class-tag decoding.
byte[] table =
    Record("Cudgel",     1, 0, 0, 0, 0, 0x01)
    .Concat(Record("Knife",      2, 0, 0, 0, 0, 0x01))
    .Concat(Record("Sword",      3, 0, 0, 0, 0, 0x01))
    .Concat(Record("VibroBlade", 5, 0, 0, 0, 0, 0x01))
    .ToArray();

var single = WeaponTable.Decode(Record("SR Missile", 9, 1, 2, 3, 4, 0x02));
Check("single record name is trimmed", single.Name, "SR Missile");
Check("single record class tag", single.ClassTag, (byte)0x02);
Check("single record class name", single.ClassName, "Ballistic");
Check("single record stat byte", single.S0, (byte)9);

var decoded = WeaponTable.DecodeTable(table);
Check("decoded row count", decoded.Count, 4);
Check("row 0 name", decoded[0].Name, "Cudgel");
Check("row 1 name", decoded[1].Name, "Knife");
Check("row 2 name", decoded[2].Name, "Sword");
Check("row 3 name (padded)", decoded[3].Name, "VibroBlade");
Check("row 3 index tracks position", decoded[3].Index, 3);
Check("small-arms class name", decoded[0].ClassName, "Small-arms");
CheckThrows<ArgumentException>("Decode rejects a short record", () => WeaponTable.Decode(new byte[16]));
CheckThrows<ArgumentException>("DecodeTable rejects a non-multiple block", () => WeaponTable.DecodeTable(new byte[18]));
CheckThrows<ArgumentException>("DecodeTable rejects an empty block", () => WeaponTable.DecodeTable(Array.Empty<byte>()));
Console.WriteLine();

Console.WriteLine("Weapon reference (Confirmed names):");
Check("personal weapon count", WeaponReference.Personal.Count, 12);
Check("mech weapon count", WeaponReference.Mech.Count, 20);
Check("combined count", WeaponReference.All.Count, 32);
Check("first personal weapon", WeaponReference.Personal[0].Name, "Cudgel");
Check("last personal weapon", WeaponReference.Personal[^1].Name, "Inferno");
Check("first mech weapon", WeaponReference.Mech[0].Name, "LaserPistl");
Check("last mech weapon", WeaponReference.Mech[^1].Name, "Kick");
Console.WriteLine();

Console.WriteLine("'Mech reference (Confirmed names):");
Check("mech count", MechReference.Mechs.Count, MechReference.Count);
Check("first mech is Locust", MechReference.Mechs[0].Name, "Locust");
Check("last mech is UrbanMech", MechReference.Mechs[^1].Name, "UrbanMech");
var urban = MechReference.Mechs[^1];
Check("UrbanMech tonnage", urban.Tons, 30);
Check("UrbanMech chassis", urban.Chassis, "UM-R60");
Console.WriteLine();

Console.WriteLine("Skill sheet (Confirmed skills + levels):");
Check("skill count", SkillSheet.Skills.Count, SkillSheet.SkillCount);
Check("skill count is 7", SkillSheet.SkillCount, 7);
Check("first skill", SkillSheet.Skills[0].Name, "Bow & Blade");
Check("Gunnery is index 3", SkillSheet.Skills[3].Name, "Gunnery");
Check("last skill is Medical", SkillSheet.Skills[^1].Name, "Medical");
Check("level count", SkillSheet.Levels.Count, 5);
Check("max level ordinal", SkillSheet.MaxLevel, 4);
Check("level 0 label", SkillSheet.DescribeLevel(0), "Unskilled");
Check("level 3 label", SkillSheet.DescribeLevel(3), "Good");
Check("level 4 label", SkillSheet.DescribeLevel(4), "Excellent");
Check("over-max level is annotated", SkillSheet.DescribeLevel(9), "Excellent+ (9)");
Check("negative level is annotated", SkillSheet.DescribeLevel(-1), "(invalid -1)");
Console.WriteLine();

Console.WriteLine("Detection signatures (Confirmed bytes):");
Check("inspect-field block length", GameSignatures.InspectFields.Length, 23);
Check("inspect-field block decodes to the field labels",
    Encoding.ASCII.GetString(GameSignatures.InspectFields), "Name  :\rWeapon:\rArmor :");
Check("title signature decodes to the game name",
    Encoding.ASCII.GetString(GameSignatures.Title), GameSignatures.GameName);
Check("two signatures are offered", GameSignatures.All.Count, 2);
Check("inspect fields are tried first", GameSignatures.All[0].Name, "Inspect-Character fields");
Console.WriteLine();

Console.WriteLine("Scan-value parsing:");
Check("decimal parse", TryParse("350000"), 350000L);
Check("hex 0x parse", TryParse("0x27"), 39L);
Check("hex suffix parse", TryParse("27h"), 39L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("zz", out _), false);
Check("39 fits a byte", ScanValue.FitsWidth(39, ScanWidth.Byte), true);
Check("300 does not fit a byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("350000 fits a dword", ScanValue.FitsWidth(350000, ScanWidth.Int32), true);
Check("350000 does not fit a word", ScanValue.FitsWidth(350000, ScanWidth.Int16), false);
Check("-1 fits a byte (signed)", ScanValue.FitsWidth(-1, ScanWidth.Byte), true);
Check("-1 canonicalizes to a byte's 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), 0xFFL);
Check("-1 canonicalizes to a word's 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("-1 canonicalizes to a dword's 0xFFFFFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int32), 0xFFFFFFFFL);
Check("a positive value canonicalizes unchanged", ScanValue.Canonicalize(350000, ScanWidth.Int32), 350000L);
Console.WriteLine();

Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Byte, 39, "Health");
Check("label is carried", pin.Label, "Health");
Check("target starts at the captured value", pin.Target, 39L);
pin.Target = 99;
Check("editing target pokes RAM", host.LastWrite, 99L);
Check("the poke uses the pin's captured width", host.LastWidth, ScanWidth.Byte);
Check("target updates", pin.Target, 99L);
pin.Target = 300;                       // does not fit a byte pin
Check("an out-of-width target is rejected", pin.Target, 99L);
pin.Frozen = true;
host.LastWrite = null;
pin.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 99L);

// A pin keeps its own width even if the host's active scan width later differs: a Byte pin must still
// write a single byte (widths are per-pin, not per-searcher).
var cbills = new FrozenValueViewModel(host, (nuint)0x3000, ScanWidth.Int32, 100000, "C-Bills");
cbills.Target = 350000;
Check("a dword pin writes at dword width", host.LastWidth, ScanWidth.Int32);
Check("a large C-Bills target is accepted", cbills.Target, 350000L);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x2000, ScanWidth.Byte, 10);
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s) { ScanValue.TryParse(s, out long v); return v; }

// A fake read/write channel: records the last value written and can simulate a failing write, so the
// frozen-value view-model's poke / freeze / width-guard / failure logic runs headlessly.
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
