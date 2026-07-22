// Headless verification for the Imperialism II trainer. There is no save format to parse (the .imp
// save has no matching linker map), so this harness exercises the parts that CAN be checked without a
// live game: the commodity reference table recovered from the exe, and the pure value-scanner helpers
// (parse / width-fit / canonicalize, and the frozen-value poke/freeze/width-guard driven through a
// fake IScanHost). Exits 0 on success, 1 on any failure so it can gate the build (Run.ps1 -Test). No
// live process is touched.

using ImperialismIITrainer.Game;
using ImperialismIITrainer.ViewModels;
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
    _ => v.ToString() ?? "null",
};

// --- commodity reference book (from the Age of Exploration manual) -------------------
Console.WriteLine("Commodity reference (from the manual's Possible Commodities to Transport):");
Check("28 commodities", CommodityBook.Commodities.Count, 28);
Check("names count matches", CommodityBook.Names.Count, 28);
Check("rows are in catalogue order (Id == index)",
    CommodityBook.Commodities.Select((c, i) => c.Id == i).All(b => b), true);
Check("row 0 is Wool", CommodityBook.Commodities[0].Name, "Wool");
Check("row 4 is Iron Ore", CommodityBook.Commodities[4].Name, "Iron Ore");
Check("row 8 is Fabric", CommodityBook.Commodities[8].Name, "Fabric");
Check("row 12 is Bronze", CommodityBook.Commodities[12].Name, "Bronze");
Check("row 13 is Steel", CommodityBook.Commodities[13].Name, "Steel");
Check("row 27 is Diamonds", CommodityBook.Commodities[27].Name, "Diamonds");
Check("8 raw materials", CommodityBook.Commodities.Count(c => c.Kind == CommodityBook.Raw), 8);
Check("6 refined materials", CommodityBook.Commodities.Count(c => c.Kind == CommodityBook.Refined), 6);
Check("3 food goods", CommodityBook.Commodities.Count(c => c.Kind == CommodityBook.Food), 3);
Check("6 luxuries", CommodityBook.Commodities.Count(c => c.Kind == CommodityBook.Luxury), 6);
Check("5 riches", CommodityBook.Commodities.Count(c => c.Kind == CommodityBook.Riches), 5);
// The ten materials confirmed against the live warehouse are all present.
Check("live-confirmed goods all present",
    new[] { "Wool", "Timber", "Tin", "Copper", "Iron Ore", "Fabric", "Lumber", "Paper", "Bronze", "Cast Iron" }
        .All(n => CommodityBook.Names.Contains(n)), true);
Check("Iron Ore is a raw material", CommodityBook.Commodities[4].Kind, CommodityBook.Raw);
Check("Cast Iron is refined", CommodityBook.Commodities[11].Kind, CommodityBook.Refined);
Check("Gold is a rich", CommodityBook.Commodities[25].Kind, CommodityBook.Riches);
Console.WriteLine();

// --- auto-locator layout + validation (pure; no live process) ------------------------
Console.WriteLine("Nation-object layout / validation:");
Check("treasury is at object +0x130", NationLayout.TreasuryOffset, 0x130);
Check("10 confirmed warehouse slots", NationLayout.WarehouseSlots.Length, 10);
Check("warehouse slots are within the header window",
    NationLayout.WarehouseSlots.All(s => s.Offset + 2 <= NationLayout.HeaderBytes), true);
Check("Iron Ore slot is +0xDDC", NationLayout.WarehouseSlots.First(s => s.Name == "Iron Ore").Offset, 0xDDC);

Check("treasury 50000 is plausible", NationLayout.IsPlausibleTreasury(50000), true);
Check("a debt (-5000) is plausible", NationLayout.IsPlausibleTreasury(-5000), true);
Check("2.1 billion is not plausible", NationLayout.IsPlausibleTreasury(2_100_000_000L), false);
Check("a real vtable (0x6FDAC8) is in .rdata", NationLayout.LooksLikeVtable(0x006FDAC8), true);
Check("a .data address is not a vtable", NationLayout.LooksLikeVtable(0x00750000), false);
Check("a heap pointer is recognised", NationLayout.LooksLikeHeapPointer(0x0E9231B0), true);
Check("a static address is not a heap pointer", NationLayout.LooksLikeHeapPointer(0x00760650), false);
Check("an unaligned pointer is rejected", NationLayout.LooksLikeHeapPointer(0x0E9231B2), false);

// A synthetic nation header (matching the live object) validates; corruptions are rejected.
short[] wh = { 10, 25, 12, 12, 40, 0, 0, 0, 0, 0, 6, 4, 20, 10, 8, 15, 0, 0, 0, 0, 99, 69, 30, 0 };
Check("a well-formed nation header validates", NationLayout.ValidateHeader(NationHeader(0x006FDAC8, 50000, wh)), true);
Check("a bad vtable is rejected", NationLayout.ValidateHeader(NationHeader(0x12345678, 50000, wh)), false);
Check("an impossible treasury is rejected", NationLayout.ValidateHeader(NationHeader(0x006FDAC8, 2_100_000_000, wh)), false);
short[] whNeg = (short[])wh.Clone(); whNeg[0] = -5;
Check("a negative warehouse slot is rejected", NationLayout.ValidateHeader(NationHeader(0x006FDAC8, 50000, whNeg)), false);
Check("an empty warehouse is rejected", NationLayout.ValidateHeader(NationHeader(0x006FDAC8, 50000, new short[24])), false);
Console.WriteLine();

// --- scan-value helpers -------------------------------------------------------------
Console.WriteLine("Value-scanner helpers:");
Check("decimal parse", TryParse("12000"), 12000L);
Check("hex 0x parse", TryParse("0x2EE0"), 12000L);
Check("hex suffix parse", TryParse("2EE0h"), 12000L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("abc", out _), false);
Check("500 fits an int16", ScanValue.FitsWidth(500, ScanWidth.Int16), true);
Check("70000 does not fit an int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("a million fits an int32", ScanValue.FitsWidth(1_000_000, ScanWidth.Int32), true);
// Treasury goes negative in debt: -1 must fold to the 0xFFFFFFFF the searcher decodes.
Check("-1 canonicalizes to 0xFFFFFFFF (Int32)", ScanValue.Canonicalize(-1, ScanWidth.Int32), 0xFFFFFFFFL);
Check("-100 canonicalizes to 0xFF9C (Int16)", ScanValue.Canonicalize(-100, ScanWidth.Int16), 0xFF9CL);
Check("in-range value passes through", ScanValue.Canonicalize(12000, ScanWidth.Int32), 12000L);
Console.WriteLine();

// --- frozen-value write / freeze / width guard --------------------------------------
Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Int32, 100, "Treasury");
Check("target starts at the captured value", pin.Target, 100L);
Check("width label reflects the pin", pin.WidthLabel, "Int32");
pin.Target = 1_000_000;
Check("editing target pokes RAM", host.LastWrite, 1_000_000L);
Check("the poke uses the pin's width", host.LastWidth, ScanWidth.Int32);

var res = new FrozenValueViewModel(host, (nuint)0x2000, ScanWidth.Int16, 40, "Steel");
res.Target = 70000;   // does not fit an int16 warehouse slot
Check("an out-of-width target is rejected", res.Target, 40L);
res.Target = 5000;
Check("an in-width target is accepted", res.Target, 5000L);
res.Frozen = true;
host.LastWrite = null;
res.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 5000L);
res.Frozen = false;
host.LastWrite = null;
res.ApplyFreeze();
Check("an unfrozen pin does not re-write", host.LastWrite, null);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x3000, ScanWidth.Int32, 10, "Treasury");
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s)
{
    if (!ScanValue.TryParse(s, out long v))
        throw new InvalidOperationException($"TryParse helper: '{s}' failed to parse (test bug).");
    return v;
}

// Builds a synthetic nation-object header (vtable at +0, treasury at +0x130, warehouse at +0xDD4) so
// NationLayout.ValidateHeader runs against a fixture without a live game.
static byte[] NationHeader(uint vtable, int treasury, short[] warehouse)
{
    var h = new byte[ImperialismIITrainer.Game.NationLayout.HeaderBytes];
    BitConverter.GetBytes(vtable).CopyTo(h, 0);
    BitConverter.GetBytes(treasury).CopyTo(h, ImperialismIITrainer.Game.NationLayout.TreasuryOffset);
    for (int i = 0; i < warehouse.Length; i++)
        BitConverter.GetBytes(warehouse[i]).CopyTo(h, ImperialismIITrainer.Game.NationLayout.WarehouseBase + i * 2);
    return h;
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
