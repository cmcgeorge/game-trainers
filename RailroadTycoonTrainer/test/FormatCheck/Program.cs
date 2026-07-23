// Headless verification for the Railroad Tycoon trainer. The game's cash is not a discrete field in the
// save file (it lives in the data segment and is edited live), so this harness exercises the parts that
// CAN be checked without a running game: the reference tables (locomotives / stations / scenarios), the
// pure layout + validation helpers (RtLayout: offsets, segment validation, conversions), and the value-
// scanner helpers (parse / width-fit / canonicalize, and the frozen-value poke/freeze/width-guard driven
// through a fake IScanHost). Exits 0 on success, 1 on any failure so it can gate the build (Run.ps1
// -Test). No live process is touched.

using RailroadTycoonTrainer.Game;
using RailroadTycoonTrainer.ViewModels;
using GameTrainers.Common.Memory;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {Fmt(actual)}, expected {Fmt(expected)}");
    if (!ok) failures++;
}

static string Fmt(object? v) => v switch { null => "null", _ => v.ToString() ?? "null" };

// --- locomotive rosters (answer key + buy-list) --------------------------------------
Console.WriteLine("Locomotive rosters:");
Check("11 US engines", LocomotiveBook.UnitedStates.Count, 11);
Check("10 England engines", LocomotiveBook.England.Count, 10);
Check("10 Europe engines", LocomotiveBook.Europe.Count, 10);
Check("31 engines total", LocomotiveBook.All.Count, 31);
Check("US starter is the Grasshopper", LocomotiveBook.UnitedStates[0].Name, "0-4-0 Grasshopper");
Check("US starter costs $10,000", LocomotiveBook.UnitedStates[0].Price, 10_000);
Check("US starter is a 1820 engine", LocomotiveBook.UnitedStates[0].Year, 1820);
Check("England starter is the Planet", LocomotiveBook.England[0].Name, "2-2-0 Planet");
Check("Europe list ends with the TGV", LocomotiveBook.Europe[^1].Name, "TGV");
Check("the buy-list keeps the Mallet name (quiz calls it Challenger)",
    LocomotiveBook.UnitedStates.Any(l => l.Name.Contains("Mallet")), true);
Check("engines are in intro-year order within the US roster",
    LocomotiveBook.UnitedStates.Zip(LocomotiveBook.UnitedStates.Skip(1)).All(p => p.First.Year <= p.Second.Year), true);
Check("ForRegion(England) returns the England roster", LocomotiveBook.ForRegion(LocoRegion.England).Count, 10);
Console.WriteLine();

// --- game-facts reference tables -----------------------------------------------------
Console.WriteLine("Reference tables:");
Check("4 station types", GameFacts.Stations.Count, 4);
Check("Depot costs $50,000", GameFacts.Stations.First(s => s.Name == "Depot").Cost, 50_000);
Check("Terminal covers 7x7", GameFacts.Stations.First(s => s.Name == "Terminal").Coverage, "7×7");
Check("Engine Shop is a listed improvement",
    GameFacts.Improvements.Any(i => i.Name == "Engine Shop"), true);
Check("4 scenarios", GameFacts.Scenarios.Count, 4);
Check("Eastern US starts in 1830", GameFacts.Scenarios.First(s => s.Name.Contains("Eastern")).StartYear, 1830);
Check("4 difficulty levels", GameFacts.Difficulties.Count, 4);
Check("Investor runs 40 years", GameFacts.Difficulties.First(d => d.Name == "Investor").GameYears, 40);
Check("Tycoon runs 100 years", GameFacts.Difficulties.First(d => d.Name == "Tycoon").GameYears, 100);
Check("3 reality switches", GameFacts.RealitySwitches.Count, 3);
Check("32 trains max", GameFacts.MaxTrains, 32);
Check("32 stations max", GameFacts.MaxStations, 32);
Check("96 signal-towers + stations max", GameFacts.MaxSignalTowersPlusStations, 96);
Check("8 cars per train", GameFacts.MaxCarsPerTrain, 8);
Check("100 cities max", GameFacts.MaxCities, 100);
Check("4 players (1 human + 3 AI)", GameFacts.MaxPlayers, 4);
Check("cash unit is $1,000", GameFacts.CashUnitDollars, 1000);
Check("worst retirement title is Hobo", GameFacts.WorstRetirementTitle, "Hobo");
Check("best retirement title mentions President", GameFacts.BestRetirementTitle.Contains("President"), true);
Console.WriteLine();

// --- layout facts + segment validation (pure; no live process) -----------------------
Console.WriteLine("Layout / segment validation:");
Check("cash is at DGROUP 0x957A", RtLayout.CashOffset, 0x957A);
Check("cash is a 16-bit word", RtLayout.CashBytes, 2);
Check("year is at DGROUP 0x96C0", RtLayout.YearOffset, 0x96C0);
Check("$30M ceiling stored as 30000", RtLayout.MaxCashThousands, 30000);
Check("1000 stored → $1,000,000", RtLayout.ThousandsToDollars(1000), 1_000_000L);
Check("$1,000,000 → 1000 stored", RtLayout.DollarsToThousands(1_000_000), 1000);
Check("1830 is a plausible year", RtLayout.IsPlausibleYear(1830), true);
Check("1700 is not a plausible year", RtLayout.IsPlausibleYear(1700), false);

// A synthetic data segment (both label strings at their known offsets) validates; corruptions fail.
Check("a well-formed segment window validates", RtLayout.ValidateSegment(SegmentWindow()), true);
byte[] badAnchor = SegmentWindow(); badAnchor[RtLayout.AnchorOffset] ^= 0xFF;
Check("a corrupt anchor is rejected", RtLayout.ValidateSegment(badAnchor), false);
byte[] badValidate = SegmentWindow(); badValidate[RtLayout.ValidateOffset + 1] ^= 0xFF;
Check("a corrupt validation string is rejected", RtLayout.ValidateSegment(badValidate), false);
Check("a too-short window is rejected", RtLayout.ValidateSegment(new byte[8]), false);
Console.WriteLine();

// --- scan-value helpers --------------------------------------------------------------
Console.WriteLine("Value-scanner helpers:");
Check("decimal parse", TryParse("1000"), 1000L);
Check("hex 0x parse", TryParse("0x3E8"), 1000L);
Check("hex suffix parse", TryParse("3E8h"), 1000L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("abc", out _), false);
Check("1000 fits an int16", ScanValue.FitsWidth(1000, ScanWidth.Int16), true);
Check("70000 does not fit an int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("30000 (max cash) fits an int16", ScanValue.FitsWidth(RtLayout.MaxCashThousands, ScanWidth.Int16), true);
// Cash can go negative in the red: -1 must fold to the 0xFFFF the searcher decodes.
Check("-1 canonicalizes to 0xFFFF (Int16)", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("in-range value passes through", ScanValue.Canonicalize(1000, ScanWidth.Int16), 1000L);
Console.WriteLine();

// --- frozen-value write / freeze / width guard ---------------------------------------
Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Int16, 1000, "Cash ($000s)");
Check("target starts at the captured value", pin.Target, 1000L);
Check("width label reflects the pin", pin.WidthLabel, "Int16");
pin.Target = 30000;
Check("editing target pokes RAM", host.LastWrite, 30000L);
Check("the poke uses the pin's width", host.LastWidth, ScanWidth.Int16);

var cash = new FrozenValueViewModel(host, (nuint)0x2000, ScanWidth.Int16, 500, "Cash ($000s)");
cash.Target = 70000;   // does not fit an int16 cash word
Check("an out-of-width target is rejected", cash.Target, 500L);
cash.Target = 5000;
Check("an in-width target is accepted", cash.Target, 5000L);
cash.Frozen = true;
host.LastWrite = null;
cash.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 5000L);
cash.Frozen = false;
host.LastWrite = null;
cash.ApplyFreeze();
Check("an unfrozen pin does not re-write", host.LastWrite, null);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x3000, ScanWidth.Int16, 10, "Cash ($000s)");
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);
Console.WriteLine();

// --- signed cash pin: sign-extension + signed-range width guard ----------------------
Console.WriteLine("Signed cash pin (Int16, in debt):");
var signedHost = new CaptureHost();
// 0xFE0C read unsigned is 65036; as a signed int16 cash word it is -500 (a $500,000 debt).
var cashPin = new FrozenValueViewModel(signedHost, (nuint)0x4000, ScanWidth.Int16, 65036, "Cash ($000s)", signed: true);
Check("captured debt shows signed", cashPin.Target, -500L);
Check("live column shows signed", cashPin.Live, -500L);
cashPin.RefreshLive(65036);
Check("RefreshLive sign-extends a debt read", cashPin.Live, -500L);
cashPin.Target = 40000;   // above the signed int16 max (32767) — would wrap negative in-game
Check("a signed pin rejects an out-of-signed-range target", cashPin.Target, -500L);
cashPin.Target = 30000;   // the $30M ceiling fits the signed range
Check("a signed pin accepts an in-range target", cashPin.Target, 30000L);
Check("the accepted target was written", signedHost.LastWrite, 30000L);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s)
{
    if (!ScanValue.TryParse(s, out long v))
        throw new InvalidOperationException($"TryParse helper: '{s}' failed to parse (test bug).");
    return v;
}

// Builds a synthetic DGROUP window with both label strings placed at their known offsets, so
// RtLayout.ValidateSegment runs against a fixture without a live game.
static byte[] SegmentWindow()
{
    var w = new byte[RtLayout.ValidationWindowBytes];
    RtLayout.AnchorBytes.CopyTo(w, RtLayout.AnchorOffset);
    RtLayout.ValidateBytes.CopyTo(w, RtLayout.ValidateOffset);
    return w;
}

// A fake read/write channel that records the last write and can simulate failure, so the frozen-value
// view-model's poke / freeze / width-guard / failure logic runs headlessly.
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
