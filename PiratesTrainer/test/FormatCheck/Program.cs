// Headless verification for the Pirates! trainer. The game's state is edited live in the emulator's
// memory, so this harness exercises everything that CAN be checked without a running game: the reference
// tables decoded out of DISK1 (settlements, convoy itineraries — which are also the manual's copy-
// protection answer key), the pure layout / validation helpers (PiratesLayout: offsets, three-anchor
// segment validation, city-record shape, calendar arithmetic), the reference view-model's era/filter
// logic, and the value-scanner helpers (parse / width-fit / canonicalize, plus the frozen-value
// poke/freeze/width-guard driven through a fake IScanHost). Exits 0 on success, 1 on any failure so it
// can gate the build (Run.ps1 -Test). No live process is touched.

using System.Text;
using GameTrainers.Common.Memory;
using PiratesTrainer.Game;
using PiratesTrainer.ViewModels;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {Fmt(actual)}, expected {Fmt(expected)}");
    if (!ok) failures++;
}

static string Fmt(object? v) => v switch { null => "null", _ => v.ToString() ?? "null" };

// The game's own month table, used to check every decoded convoy stop names a real month.
string[] MonthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

// --- era / settlement tables (decoded from DISK1) ------------------------------------
Console.WriteLine("Settlement tables:");
Check("6 eras", CityBook.EraYears.Count, 6);
Check("6 era names", CityBook.EraNames.Count, 6);
Check("6 era tables", CityBook.ByEra.Count, 6);
Check("first era starts in 1560", CityBook.EraYears[0], 1560);
Check("last era starts in 1680", CityBook.EraYears[^1], 1680);
// Every era's count, spelled out — the docs quote these numbers, so an unasserted era is a doc that
// can drift away from the data without anything noticing.
Check("settlement counts per era",
    string.Join(",", CityBook.ByEra.Select(e => e.Count)), "32,32,38,41,41,41");
Check("1560 has 32 settlements", CityBook.Era1560.Count, 32);
Check("1620 has 38 settlements", CityBook.Era1620.Count, 38);
Check("1680 has 41 settlements", CityBook.Era1680.Count, 41);
Check("no era exceeds the table capacity",
    CityBook.ByEra.All(e => e.Count <= PiratesLayout.MaxCities), true);
Check("EraForYear(1620) is index 2", CityBook.EraForYear(1620), 2);
Check("EraForYear(1599) is not an era", CityBook.EraForYear(1599), -1);
Check("ForEra(-1) is empty, not a crash", CityBook.ForEra(-1).Count, 0);
Check("ForEra(6) is empty, not a crash", CityBook.ForEra(6).Count, 0);

// Record indices must be dense and in table order — the game addresses cities by index.
foreach (var (era, year) in CityBook.ByEra.Select((e, i) => (e, CityBook.EraYears[i])))
    Check($"{year} indices are 0..n-1 in order", era.Select((c, i) => c.Index == i).All(x => x), true);

// Spot-checks against the raw bytes, so a regenerated table can't silently drift.
var havana1560 = CityBook.Era1560.Single(c => c.Name == "HAVANA");
Check("Havana 1560 is Spanish", havana1560.Nation, "Spanish");
Check("Havana 1560 has 3 forts", havana1560.Forts, 3);
Check("Havana 1560 garrisons 250 soldiers", havana1560.Soldiers, 250);
Check("Havana 1560 holds 50,000 gold", havana1560.Gold, 50_000);
var cartagena1560 = CityBook.Era1560.Single(c => c.Name == "CARTAGENA");
Check("Cartagena 1560 is the most fortified (4 forts)", cartagena1560.Forts, 4);
Check("Cartagena 1560 is Prospering", cartagena1560.Prosperity, "Prospering");
var eleuthera = CityBook.Era1560.Single(c => c.Name == "ELEUTHERA");
Check("Eleuthera 1560 is English", eleuthera.Nation, "English");
Check("Eleuthera 1560 is undefended", eleuthera.Forts, 0);
// Map coordinates must be geographically sane: Vera Cruz far west, Bermuda far north, Barbados far east.
Check("Vera Cruz 1560 is the western edge",
    CityBook.Era1560.OrderBy(c => c.X).First().Name, "VERA CRUZ");
Check("Bermuda 1680 is the northernmost", CityBook.Era1680.OrderBy(c => c.Y).First().Name, "BERMUDA");
Check("Barbados 1680 is the eastern edge", CityBook.Era1680.OrderByDescending(c => c.X).First().Name, "BARBADOS");
Check("every 1560 nation is one of the four powers",
    CityBook.Era1560.All(c => c.Nation is "Spanish" or "English" or "French" or "Dutch"), true);
Check("every prosperity band is a known one",
    CityBook.ByEra.SelectMany(e => e).All(c => GameFacts.Prosperity.Contains(c.Prosperity)), true);
Console.WriteLine();

// --- convoy itineraries (the copy-protection answer key) -----------------------------
Console.WriteLine("Convoy itineraries / copy-protection key:");
Check("6 Treasure Fleet itineraries", FleetSchedule.TreasureFleetByEra.Count, 6);
Check("6 Silver Train itineraries", FleetSchedule.SilverTrainByEra.Count, 6);
Check("every era has both convoys",
    FleetSchedule.TreasureFleetByEra.All(r => r.Count > 0) && FleetSchedule.SilverTrainByEra.All(r => r.Count > 0), true);
Check("every route slot is within the 16-slot table",
    FleetSchedule.All.All(r => r.Slot is >= 0 and < 16), true);
Check("every half is early or late", FleetSchedule.All.All(r => r.Half is "early" or "late"), true);
Check("every month is a real month",
    FleetSchedule.All.All(r => MonthNames.Contains(r.Month)), true);
// Every stop must name a town that exists in that era's table — this is what catches an index/era mismatch.
Check("every stop names a settlement of its own era",
    FleetSchedule.All.All(r =>
    {
        int era = CityBook.EraForYear(r.Year);
        return era >= 0 && CityBook.ForEra(era).Any(c => c.Name == r.City);
    }), true);
// Slots must be strictly increasing within an itinerary — the convoy never doubles back in time.
Check("stops are in slot order within each itinerary",
    CityBook.EraYears.Select((_, e) => FleetSchedule.TreasureFleetByEra[e])
        .Concat(CityBook.EraYears.Select((_, e) => FleetSchedule.SilverTrainByEra[e]))
        .All(r => r.Zip(r.Skip(1)).All(p => p.First.Slot < p.Second.Slot)), true);

// Spot-checks against the shipped 1987 answer key.
Check("1560 Treasure Fleet opens at Cumana", FleetSchedule.Fleet1560[0].Display, "CUMANA - Oct - early");
Check("1560 Treasure Fleet second stop", FleetSchedule.Fleet1560[1].Display, "PR.CABELLO - Oct - late");
Check("1560 Silver Train opens at Cumana", FleetSchedule.Train1560[0].Display, "CUMANA - Apr - early");
Check("1560 Silver Train second stop", FleetSchedule.Train1560[1].Display, "BORBURATA - Apr - late");
Check("1600 Silver Train opens at St.Thome", FleetSchedule.Train1600[0].Display, "ST.THOME - Apr - early");
Check("1620 Treasure Fleet opens a month earlier (Sep)", FleetSchedule.Fleet1620[0].Display, "CARACAS - Sep - early");
Check("1660 Silver Train opens a month earlier (Mar)", FleetSchedule.Train1660[0].Display, "CUMANA - Mar - early");
Check("1560 Treasure Fleet ends in the Florida Channel",
    FleetSchedule.Fleet1560.Last().City, "FLORIDA CHNL");
// Stop counts per era, spelled out — the strategy guide reproduces these tables in full, so an
// unasserted count lets the prose and the data drift apart.
Check("Treasure Fleet stop counts per era",
    string.Join(",", FleetSchedule.TreasureFleetByEra.Select(r => r.Count)), "12,12,11,11,11,10");
Check("Silver Train stop counts per era",
    string.Join(",", FleetSchedule.SilverTrainByEra.Select(r => r.Count)), "11,12,11,10,10,9");
Check("130 convoy stops in total", FleetSchedule.All.Count, 130);

// A map position that carries different names in different eras is the game modelling a settlement
// changing hands and being renamed — Borburata becomes Caracas, Isabella becomes La Vega, St. Kitts
// becomes St. Christoph, Santiago de la Vega becomes Port Royale, San Catalina becomes Providence.
// Pinning the exact set documents those renamings and catches a regeneration that alters the data.
// GRAN GRANADA / GRAN GRANDA is the odd one out: it is not a renaming but MicroProse's own typo in the
// 1680 block (verified against the DISK1 bytes — the generator reproduces it faithfully).
Check("the set of settlements renamed between eras",
    string.Join(" | ", CityBook.ByEra.SelectMany(e => e.Select(c => (c.X, c.Y, c.Name)))
        .GroupBy(t => (t.X, t.Y))
        .Where(g => g.Select(t => t.Name).Distinct().Count() > 1)
        .Select(g => string.Join("=", g.Select(t => t.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal)))
        .OrderBy(s => s, StringComparer.Ordinal)),
    "BORBURATA=CARACAS | GRAN GRANADA=GRAN GRANDA | ISABELLA=LA VEGA | " +
    "PORT ROYALE=SANTIGO VEGA | PROVIDENCE=SAN.CATALINA | ST.CHRISTOPH=ST.KITTS");
Console.WriteLine();

// --- layout facts + three-anchor segment validation (pure; no live process) ----------
Console.WriteLine("Layout / segment validation:");
Check("gold is at DGROUP 0x4847", PiratesLayout.GoldOffset, 0x4847);
Check("gold saturates at 65535", PiratesLayout.MaxGold, 0xFFFF);
Check("the save block starts at DGROUP 0x4130", PiratesLayout.SaveBlockOffset, 0x4130);
Check("the save block is 1,940 bytes", PiratesLayout.SaveBlockBytes, 1940);
Check("the settlement table is inside the save block",
    PiratesLayout.CityTableOffset >= PiratesLayout.SaveBlockOffset &&
    PiratesLayout.CityTableOffset < PiratesLayout.SaveBlockOffset + PiratesLayout.SaveBlockBytes, true);
Check("gold is inside the save block",
    PiratesLayout.GoldOffset >= PiratesLayout.SaveBlockOffset &&
    PiratesLayout.GoldOffset < PiratesLayout.SaveBlockOffset + PiratesLayout.SaveBlockBytes, true);
Check("city 0 is the table base", PiratesLayout.CityOffset(0), PiratesLayout.CityTableOffset);
Check("city 10 is 240 bytes in", PiratesLayout.CityOffset(10), PiratesLayout.CityTableOffset + 240);
Check("41 settlements fit before the convoy routes", PiratesLayout.MaxCities, 41);
Check("the largest shipped era fits", CityBook.ByEra.Max(e => e.Count) <= PiratesLayout.MaxCities, true);
Check("the convoy routes follow the settlement table",
    PiratesLayout.FleetRouteOffset, PiratesLayout.CityTableOffset + 0x3E0);

// Era codes are 0, 2, 3, 4, 5, 6 — the menu maps only choice 1 down to 0 — so `1560 + 20*code`
// reproduces exactly the six offered start years.
Check("6 era codes", PiratesLayout.EraCodes.Count, 6);
Check("era codes reproduce the six start years",
    PiratesLayout.EraCodes.Select(c => PiratesLayout.DisplayYear(c, 0)).SequenceEqual(CityBook.EraYears), true);
Check("code 0 year 0 is 1560", PiratesLayout.DisplayYear(0, 0), 1560);
Check("code 6 year 0 is 1680", PiratesLayout.DisplayYear(6, 0), 1680);
Check("code 3 plus 7 years is 1627", PiratesLayout.DisplayYear(3, 7), 1627);
Check("the default era code is 1660", PiratesLayout.DisplayYear(PiratesLayout.DefaultEraCode, 0), 1660);
Check("code 3 is the third period", PiratesLayout.EraIndexFromCode(3), 2);
Check("code 1 is never offered", PiratesLayout.EraIndexFromCode(1), -1);
Check("index 5 is code 6", PiratesLayout.EraCodeFromIndex(5), 6);
Check("an out-of-range index has no code", PiratesLayout.EraCodeFromIndex(9), -1);
Check("code/index round-trip",
    PiratesLayout.EraCodes.Select((c, i) => PiratesLayout.EraIndexFromCode(c) == i).All(x => x), true);
Check("day 0 is month 0", PiratesLayout.MonthFromDayOfYear(0), 0);
Check("day 29 is still month 0", PiratesLayout.MonthFromDayOfYear(29), 0);
Check("day 30 is month 1", PiratesLayout.MonthFromDayOfYear(30), 1);
Check("day 359 is month 11", PiratesLayout.MonthFromDayOfYear(359), 11);
Check("an over-range day clamps to month 11", PiratesLayout.MonthFromDayOfYear(9999), 11);
Check("a negative day clamps to month 0", PiratesLayout.MonthFromDayOfYear(-5), 0);
Check("the year is 360 days", PiratesLayout.DaysPerYear, 360);
Check("wealth 250 is 2,500 gold", PiratesLayout.WealthToGold(250), 2500);
Check("land 6 is 300 acres", PiratesLayout.LandToAcres(6), 300);
Check("1560 is a plausible year", PiratesLayout.IsPlausibleYear(1560), true);
Check("1500 is not a plausible year", PiratesLayout.IsPlausibleYear(1500), false);
Check("era code 6 is plausible", PiratesLayout.IsPlausibleEra(6), true);
Check("era code 1 is not (never offered)", PiratesLayout.IsPlausibleEra(1), false);
Check("era code 7 is not", PiratesLayout.IsPlausibleEra(7), false);
Console.WriteLine();

// --- convoy slot arithmetic (the derivation the itineraries are built from) ----------
Console.WriteLine("Convoy slot arithmetic:");
Check("Treasure Fleet slot 0 is the first half of October (even era)",
    PiratesLayout.TreasureFleetSlot(270, 0), 0);
Check("Treasure Fleet slot 1 is the second half of October",
    PiratesLayout.TreasureFleetSlot(285, 0), 1);
Check("Treasure Fleet slot 0 shifts to September in an odd era",
    PiratesLayout.TreasureFleetSlot(240, 3), 0);
Check("Silver Train slot 0 is the first half of April (even era)",
    PiratesLayout.SilverTrainSlot(90, 0), 0);
Check("Silver Train slot 0 shifts to March in an odd era",
    PiratesLayout.SilverTrainSlot(60, 5), 0);
Check("a slot before the convoy sails wraps into 0..23",
    PiratesLayout.TreasureFleetSlot(0, 0), 6);
Check("every day of the year yields a slot in 0..23",
    Enumerable.Range(0, PiratesLayout.DaysPerYear)
        .All(d => PiratesLayout.EraCodes.All(c =>
            PiratesLayout.TreasureFleetSlot(d, c) is >= 0 and < PiratesLayout.HalfMonthsPerYear &&
            PiratesLayout.SilverTrainSlot(d, c) is >= 0 and < PiratesLayout.HalfMonthsPerYear)), true);
// MonthForSlot / IsEarlyHalf must invert the slot arithmetic exactly — that inversion is what turns a
// decoded route row into the manual's "city - month - early/late" chart.
Check("MonthForSlot inverts the fleet slot for every day and era",
    Enumerable.Range(0, PiratesLayout.DaysPerYear).All(d => PiratesLayout.EraCodes.All(c =>
    {
        int slot = PiratesLayout.TreasureFleetSlot(d, c);
        return PiratesLayout.MonthForSlot(slot, c, PiratesLayout.TreasureFleetSlotBias)
                   == PiratesLayout.MonthFromDayOfYear(d)
               && PiratesLayout.IsEarlyHalf(slot, c, PiratesLayout.TreasureFleetSlotBias)
                   == (d % PiratesLayout.DaysPerMonth < PiratesLayout.DaysPerHalfMonth);
    })), true);
Check("MonthForSlot inverts the train slot for every day and era",
    Enumerable.Range(0, PiratesLayout.DaysPerYear).All(d => PiratesLayout.EraCodes.All(c =>
    {
        int slot = PiratesLayout.SilverTrainSlot(d, c);
        return PiratesLayout.MonthForSlot(slot, c, PiratesLayout.SilverTrainSlotBias)
                   == PiratesLayout.MonthFromDayOfYear(d);
    })), true);
// Finally: the generated itineraries must agree with that arithmetic, month for month.
Check("every generated stop's month matches the slot arithmetic",
    FleetSchedule.All.All(r =>
    {
        int code = PiratesLayout.EraCodeFromIndex(CityBook.EraForYear(r.Year));
        int bias = r.Convoy == "Treasure Fleet"
            ? PiratesLayout.TreasureFleetSlotBias : PiratesLayout.SilverTrainSlotBias;
        return MonthNames[PiratesLayout.MonthForSlot(r.Slot, code, bias)] == r.Month
               && PiratesLayout.IsEarlyHalf(r.Slot, code, bias) == (r.Half == "early");
    }), true);

// --- the anchor literals themselves ---------------------------------------------------
// These must be asserted against ground truth spelled out HERE, independently of the constants.
// SegmentWindow() below builds its fixture from the same arrays ValidateSegment compares against, so
// the validation checks alone are tautological with respect to content: they would pass for any anchor
// bytes at all, including bytes that do not exist in the real program image. (They did: the month table
// was first written as a C# string literal, where "\xff" is a *greedy* hex escape that swallowed the
// following month letters, and Encoding.ASCII then folded every remaining 0xFF to '?'.)
Console.WriteLine("Anchor literals (ground truth):");
Check("the anchor is the 1987 copyright line",
    Encoding.ASCII.GetString(PiratesLayout.AnchorBytes), "COPYRIGHT (C)  1987  MICROPROSE INC.");
Check("the save magic is the 8-byte constant, not the 16-byte pair",
    Encoding.ASCII.GetString(PiratesLayout.ValidateBytes), "PIRATES!");
Check("the save magic stops before the save block",
    PiratesLayout.ValidateOffset + PiratesLayout.ValidateBytes.Length <= PiratesLayout.SaveBlockOffset, true);
Check("the month table is 47 bytes", PiratesLayout.MonthTableBytes.Length, 47);
Check("the month table holds 11 separators",
    PiratesLayout.MonthTableBytes.Count(b => b == PiratesLayout.StringSeparator), 11);
Check("the separator is 0xFF", PiratesLayout.StringSeparator, (byte)0xFF);
Check("the month table starts JAN",
    Encoding.ASCII.GetString(PiratesLayout.MonthTableBytes, 0, 3), "JAN");
Check("the month table ends DEC",
    Encoding.ASCII.GetString(PiratesLayout.MonthTableBytes, PiratesLayout.MonthTableBytes.Length - 3, 3), "DEC");
Check("the month table is JAN<FF>FEB<FF>...",
    string.Join("|", SplitOn(PiratesLayout.MonthTableBytes, PiratesLayout.StringSeparator)),
    "JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC");
// A general guard: Encoding.ASCII silently replaces anything above 0x7F with '?', so a stray '?' in a
// literal that should not contain one is the signature of exactly that mistake.
Check("no anchor byte was folded to '?'",
    PiratesLayout.AnchorBytes.Concat(PiratesLayout.MonthTableBytes).Concat(PiratesLayout.ValidateBytes)
        .Any(b => b == (byte)'?'), false);
// The literal, not a restatement of the Math.Max: the window must end exactly where the save block
// begins, which is what proves the save magic and the mutable slot header do not overlap.
Check("the validation window is 0x4130 bytes", PiratesLayout.ValidationWindowBytes, 0x4130);
Check("the validation window ends exactly at the save block",
    PiratesLayout.ValidationWindowBytes, PiratesLayout.SaveBlockOffset);
Check("the anchors do not overlap each other",
    PiratesLayout.AnchorOffset + PiratesLayout.AnchorBytes.Length <= PiratesLayout.MonthTableOffset &&
    PiratesLayout.MonthTableOffset + PiratesLayout.MonthTableBytes.Length <= PiratesLayout.ValidateOffset, true);
Console.WriteLine();

Console.WriteLine("Segment validation:");
// A synthetic data segment (all three literals at their known offsets) validates; corruptions fail.
Check("a well-formed segment window validates", PiratesLayout.ValidateSegment(SegmentWindow()), true);
byte[] badAnchor = SegmentWindow(); badAnchor[PiratesLayout.AnchorOffset] ^= 0xFF;
Check("a corrupt anchor is rejected", PiratesLayout.ValidateSegment(badAnchor), false);
byte[] badMonths = SegmentWindow(); badMonths[PiratesLayout.MonthTableOffset + 4] ^= 0xFF;
Check("a corrupt month table is rejected", PiratesLayout.ValidateSegment(badMonths), false);
byte[] badMagic = SegmentWindow(); badMagic[PiratesLayout.ValidateOffset + 1] ^= 0xFF;
Check("a corrupt save magic is rejected", PiratesLayout.ValidateSegment(badMagic), false);
Check("a too-short window is rejected", PiratesLayout.ValidateSegment(new byte[8]), false);
Console.WriteLine();

// --- settlement-record shape check ---------------------------------------------------
Console.WriteLine("Settlement-record decoding:");
byte[] good = CityRecord("HAVANA", nation: 0);
Check("a well-formed record is accepted", PiratesLayout.LooksLikeCityRecord(good), true);
Check("its name decodes", PiratesLayout.CityName(good), "HAVANA");
Check("a dotted name is accepted", PiratesLayout.LooksLikeCityRecord(CityRecord("PR.CABELLO", 0)), true);
Check("a full-width name is accepted", PiratesLayout.LooksLikeCityRecord(CityRecord("FLORIDA CHNL", 0)), true);
byte[] badNation = CityRecord("HAVANA", nation: 7);
Check("an impossible nation byte is rejected", PiratesLayout.LooksLikeCityRecord(badNation), false);
byte[] lowercase = CityRecord("Havana", 0);
Check("a lower-case name is rejected", PiratesLayout.LooksLikeCityRecord(lowercase), false);
byte[] blank = CityRecord("", 0);
Check("an all-blank name is rejected", PiratesLayout.LooksLikeCityRecord(blank), false);
Check("one internal space is accepted", PiratesLayout.LooksLikeCityRecord(CityRecord("SANTA MARTA", 0)), true);
byte[] gapped = CityRecord("HAV", 0); gapped[PiratesLayout.CityNameOffset + 8] = (byte)'X';
Check("a double-space gap is rejected", PiratesLayout.LooksLikeCityRecord(gapped), false);
Check("a leading space is rejected", PiratesLayout.LooksLikeCityRecord(CityRecord(" HAVANA", 0)), false);
byte[] binary = CityRecord("HAVANA", 0); binary[PiratesLayout.CityNameOffset + 1] = 0x00;
Check("a NUL inside the name is rejected", PiratesLayout.LooksLikeCityRecord(binary), false);
Check("a short span is rejected", PiratesLayout.LooksLikeCityRecord(new byte[10]), false);
Check("CityName on a short span is empty, not a crash", PiratesLayout.CityName(new byte[10]), "");

// Every shipped settlement name must survive a round-trip through the record shape check —
// this is what guarantees the live reader will not stop early on a legitimate town.
Check("every shipped settlement name passes the shape check",
    CityBook.ByEra.SelectMany(e => e).All(c => PiratesLayout.LooksLikeCityRecord(CityRecord(c.Name, 0))), true);
Check("every shipped settlement name round-trips",
    CityBook.ByEra.SelectMany(e => e).All(c => PiratesLayout.CityName(CityRecord(c.Name, 0)) == c.Name), true);
Console.WriteLine();

// --- known-value pin set -------------------------------------------------------------
Console.WriteLine("Auto-locate pin set:");
Check("gold leads the pin list", PiratesLayout.KnownValues[0].Label, "Gold");
Check("gold is marked Confirmed", PiratesLayout.KnownValues[0].Evidence, Evidence.Confirmed);
Check("every pin is 1 or 2 bytes wide",
    PiratesLayout.KnownValues.All(v => v.Bytes is 1 or 2), true);
Check("every pin has a note", PiratesLayout.KnownValues.All(v => v.Note.Length > 0), true);
Check("pin labels are unique",
    PiratesLayout.KnownValues.Select(v => v.Label).Distinct().Count(), PiratesLayout.KnownValues.Count);
Check("pin offsets are unique",
    PiratesLayout.KnownValues.Select(v => v.Offset).Distinct().Count(), PiratesLayout.KnownValues.Count);
Console.WriteLine();

// --- game-facts reference tables -----------------------------------------------------
Console.WriteLine("Reference tables:");
Check("9 hull types", GameFacts.Ships.Count, 9);
Check("the smallest hull is the Pinnace", GameFacts.Ships[0].Name, "Pinnace");
Check("the largest hull is the Fast Galleon", GameFacts.Ships[^1].Name, "Fast Galleon");
Check("8 ranks", GameFacts.Ranks.Count, 8);
Check("rank 0 is Ensign", GameFacts.Ranks[0].Name, "Ensign");
Check("rank 7 is Marquis", GameFacts.Ranks[^1].Name, "Marquis");
Check("ranks are indexed in order", GameFacts.Ranks.Select((r, i) => r.Index == i).All(x => x), true);
Check("5 specialities", GameFacts.Specialities.Count, 5);
Check("4 difficulty levels", GameFacts.Difficulties.Count, 4);
Check("hardest level is Swashbuckler", GameFacts.Difficulties[^1].Name, "Swashbuckler");
Check("6 famous expeditions", GameFacts.Expeditions.Count, 6);
Check("Hawkins sails in 1569", GameFacts.Expeditions[0].Year, 1569);
Check("De Pointis sails last, in 1697", GameFacts.Expeditions[^1].Year, 1697);
Check("expeditions are in chronological order",
    GameFacts.Expeditions.Zip(GameFacts.Expeditions.Skip(1)).All(p => p.First.Year <= p.Second.Year), true);
Check("3 duelling weapons", GameFacts.Weapons.Count, 3);
Check("8 named rivals", GameFacts.RivalPirates.Count, 8);
Check("7 morale bands", GameFacts.MoraleBands.Count, 7);
Check("4 prosperity bands", GameFacts.Prosperity.Count, 4);
Check("F10 quits", GameFacts.Controls.Any(c => c.Input == "F10" && c.Effect.Contains("Quit")), true);
Check("dosbox is a recognised emulator", GameFacts.EmulatorProcessHints.Contains("dosbox"), true);
Console.WriteLine();

// --- reference view-model filtering ---------------------------------------------------
Console.WriteLine("Reference view-model:");
var reference = new ReferenceViewModel();
Check("defaults to the 1560 era", reference.SelectedEraIndex, 0);
Check("lists all 32 settlements of 1560", reference.Cities.Count, 32);
Check("lists only 1560 convoy stops", reference.Schedule.All(r => r.Year == 1560), true);
reference.SelectedEraIndex = 5;
Check("switching era reloads the settlements", reference.Cities.Count, 41);
Check("switching era reloads the convoys", reference.Schedule.All(r => r.Year == 1680), true);
reference.CityFilter = "havana";
Check("the filter is case-insensitive", reference.Cities.All(c => c.Name.Contains("HAVANA")), true);
Check("the filter narrows to one town", reference.Cities.Count, 1);
reference.CityFilter = "zzzz";
Check("a filter that matches nothing empties the grid", reference.Cities.Count, 0);
reference.CityFilter = "";
Check("clearing the filter restores the grid", reference.Cities.Count, 41);

// An out-of-range write must be REJECTED, not clamped. A WPF Selector writes -1 back when its items
// detach (switching tabs tears the content tree down), and clamping that to 0 would silently reset the
// user's era to 1560 every time. These two cases distinguish the designs: under clamping, the -1 case
// would leave SelectedEraIndex at 0.
reference.SelectedEraIndex = 99;
Check("an over-range era is rejected", reference.SelectedEraIndex, 5);
Check("...and the grid is untouched", reference.Cities.Count, 41);
reference.SelectedEraIndex = -1;   // what a detaching Selector writes back
Check("a -1 write-back is rejected, not clamped to 1560", reference.SelectedEraIndex, 5);
Check("...and the grid still shows the selected era", reference.Cities.Count, 41);
reference.SelectedEraIndex = 0;
Check("a valid era still applies", reference.Cities.Count, 32);
Console.WriteLine();

// --- scan-value helpers --------------------------------------------------------------
Console.WriteLine("Value-scanner helpers:");
Check("decimal parse", TryParse("1500"), 1500L);
Check("hex 0x parse", TryParse("0x5DC"), 1500L);
Check("hex suffix parse", TryParse("5DCh"), 1500L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("abc", out _), false);
Check("1500 fits an int16", ScanValue.FitsWidth(1500, ScanWidth.Int16), true);
Check("65535 (max gold) fits an int16", ScanValue.FitsWidth(PiratesLayout.MaxGold, ScanWidth.Int16), true);
Check("70000 does not fit an int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("255 fits a byte", ScanValue.FitsWidth(255, ScanWidth.Byte), true);
Check("256 does not fit a byte", ScanValue.FitsWidth(256, ScanWidth.Byte), false);
Check("-1 canonicalizes to 0xFFFF (Int16)", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("in-range value passes through", ScanValue.Canonicalize(1500, ScanWidth.Int16), 1500L);
Console.WriteLine();

// --- frozen-value write / freeze / width guard ---------------------------------------
Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Int16, 1500, "Gold");
Check("target starts at the captured value", pin.Target, 1500L);
Check("width label reflects the pin", pin.WidthLabel, "Int16");
pin.Target = PiratesLayout.MaxGold;
Check("editing target pokes RAM", host.LastWrite, (long)PiratesLayout.MaxGold);
Check("the poke uses the pin's width", host.LastWidth, ScanWidth.Int16);

var gold = new FrozenValueViewModel(host, (nuint)0x2000, ScanWidth.Int16, 500, "Gold");
gold.Target = 70000;   // does not fit the 16-bit purse
Check("an out-of-width target is rejected", gold.Target, 500L);
gold.Target = 5000;
Check("an in-width target is accepted", gold.Target, 5000L);
gold.Frozen = true;
host.LastWrite = null;
gold.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 5000L);
gold.Frozen = false;
host.LastWrite = null;
gold.ApplyFreeze();
Check("an unfrozen pin does not re-write", host.LastWrite, null);

// The land byte is a one-byte pin: it must reject anything a byte can't hold.
var land = new FrozenValueViewModel(host, (nuint)0x2500, ScanWidth.Byte, 6, "Land (x50 acres)");
land.Target = 300;
Check("a byte pin rejects 300", land.Target, 6L);
land.Target = 200;
Check("a byte pin accepts 200", land.Target, 200L);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x3000, ScanWidth.Int16, 10, "Gold");
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

// Splits a 0xFF-delimited run of ASCII records, so the month table can be asserted record by record
// against a literal spelled out in the test rather than against the constant under test.
static IEnumerable<string> SplitOn(byte[] data, byte separator)
{
    var current = new List<byte>();
    foreach (byte b in data)
    {
        if (b == separator) { yield return Encoding.ASCII.GetString(current.ToArray()); current.Clear(); }
        else current.Add(b);
    }
    yield return Encoding.ASCII.GetString(current.ToArray());
}

// Builds a synthetic DGROUP window with all three anchor literals at their known offsets, so
// PiratesLayout.ValidateSegment runs against a fixture without a live game. NOTE: this fixture is built
// from the constants it validates, so it proves the offset arithmetic and the rejection paths — never
// the anchors' content. That is what the "Anchor literals (ground truth)" block above is for.
static byte[] SegmentWindow()
{
    var w = new byte[PiratesLayout.ValidationWindowBytes];
    PiratesLayout.AnchorBytes.CopyTo(w, PiratesLayout.AnchorOffset);
    PiratesLayout.MonthTableBytes.CopyTo(w, PiratesLayout.MonthTableOffset);
    PiratesLayout.ValidateBytes.CopyTo(w, PiratesLayout.ValidateOffset);
    return w;
}

// Builds a synthetic 24-byte settlement record: twelve data bytes then a space-padded name.
static byte[] CityRecord(string name, int nation)
{
    var r = new byte[PiratesLayout.CityRecordBytes];
    r[3] = (byte)nation;
    var padded = name.PadRight(PiratesLayout.CityNameLength).Substring(0, PiratesLayout.CityNameLength);
    Encoding.ASCII.GetBytes(padded).CopyTo(r, PiratesLayout.CityNameOffset);
    return r;
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
