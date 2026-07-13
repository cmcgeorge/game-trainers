// Headless verification harness for The Perfect General II trainer. It asserts the game-knowledge layer
// (the confirmed purchased-unit count-array format and the UNITINFO.DOC unit reference) plus the pure
// value-scanner helpers and the frozen-value write/clamp logic. Exits 0 on success, 1 on any failure so
// it can gate the build (Run.ps1 -Test). No live process or emulator is touched.

using GameTrainers.Common.Memory;
using ThePerfectGeneral2Trainer.Game;
using ThePerfectGeneral2Trainer.ViewModels;

// The Confirmed purchase-screen count array from .data/memdump.md (dump 1): per-type counts summing to 36.
byte[] SampleCounts = { 0x03, 0x02, 0x01, 0x04, 0x03, 0x02, 0x01, 0x04,
                        0x03, 0x02, 0x01, 0x04, 0x03, 0x00, 0x02, 0x01 };

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

Console.WriteLine("Purchase count-array format (Confirmed dump):");
Check("type count", PurchaseFormat.TypeCount, 16);
Check("type-order length", PurchaseFormat.TypeOrder.Count, 16);
Check("order[0] is Mine", PurchaseFormat.TypeOrder[0], "Mine");
Check("order[13] is Plane", PurchaseFormat.TypeOrder[13], "Plane");
Check("order[15] is Elephant Tank", PurchaseFormat.TypeOrder[15], "Elephant Tank");
Check("sample sums to 36 (Units Purchased)", PurchaseFormat.TotalUnits(SampleCounts), 36);
var decoded = PurchaseFormat.Decode(SampleCounts);
Check("decoded row count", decoded.Count, 16);
Check("Mine count = 3", decoded[0].Count, 3);
Check("Engineer count = 4", decoded[3].Count, 4);
Check("Plane count = 0", decoded[13].Count, 0);
Check("Elephant Tank count = 1", decoded[15].Count, 1);
Check("decoded labels track TypeOrder", decoded[6].Type, "Armored Car");
CheckThrows<ArgumentException>("Decode rejects a short block", () => PurchaseFormat.Decode(new byte[15]));
CheckThrows<ArgumentException>("TotalUnits rejects a long block", () => PurchaseFormat.TotalUnits(new byte[17]));
Console.WriteLine();

Console.WriteLine("Unit reference (UNITINFO.DOC):");
Check("unit count", UnitReference.Units.Count, UnitReference.TypeCount);
Check("first unit is Infantry", UnitReference.Units[0].Code, "INF");
Check("last unit is Elephant Tank", UnitReference.Units[^1].Code, "ETANK");
var etank = UnitReference.Units[^1];
Check("ETANK hit points = 21", etank.HitPoints, 21);
Check("ETANK is AA-capable", etank.AntiAir, true);
var hart = UnitReference.Units.First(u => u.Code == "HART");
Check("HART cost = 20", hart.Cost, 20);
Check("HART bombard range = 26", hart.Bombard, "26");
var htank = UnitReference.Units.First(u => u.Code == "HTANK");
Check("HTANK hit points = 15", htank.HitPoints, 15);
var mine = UnitReference.Units.First(u => u.Code == "MINE");
Check("MINE has no hit points", mine.HitPoints, null);
Check("MINE cost = 3", mine.Cost, 3);
var plane = UnitReference.Units.First(u => u.Code == "PLANE");
Check("PLANE damage keeps the (ET 50%) qualifier", plane.Damage, "66% kill (ET 50%)");
Console.WriteLine();

Console.WriteLine("Scan-value parsing:");
Check("decimal parse", TryParse("39"), 39L);
Check("hex 0x parse", TryParse("0x27"), 39L);
Check("hex suffix parse", TryParse("27h"), 39L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("zz", out _), false);
Check("39 fits a byte", ScanValue.FitsWidth(39, ScanWidth.Byte), true);
Check("300 does not fit a byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("300 fits a word", ScanValue.FitsWidth(300, ScanWidth.Int16), true);
Check("-1 fits a byte (signed)", ScanValue.FitsWidth(-1, ScanWidth.Byte), true);
Check("-1 canonicalizes to a byte's 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), 0xFFL);
Check("-1 canonicalizes to a word's 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("-1 canonicalizes to a dword's 0xFFFFFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int32), 0xFFFFFFFFL);
Check("a positive value canonicalizes unchanged", ScanValue.Canonicalize(300, ScanWidth.Int16), 300L);
Console.WriteLine();

Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Byte, 39);
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
// write a single byte (the P1 fix — widths are per-pin, not per-searcher).
var narrowPin = new FrozenValueViewModel(host, (nuint)0x3000, ScanWidth.Byte, 5);
narrowPin.Target = 7;
Check("a byte pin writes at byte width regardless of host state", host.LastWidth, ScanWidth.Byte);

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
