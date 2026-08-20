using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;

// The sample files live in the trainer's own docs folder, which is named ".docs" here and
// "docs" in the sibling trainers; take whichever is present so the harness runs either way.
string trainerRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string docs = Directory.Exists(Path.Combine(trainerRoot, "docs"))
    ? Path.Combine(trainerRoot, "docs")
    : Path.Combine(trainerRoot, ".docs");

int failures = 0;
void Check(string what, bool ok) { Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}"); if (!ok) failures++; }

Console.WriteLine($"docs = {docs}\n");

// ---- ROSTER.DTA: packed 127-byte records ----
Console.WriteLine("ROSTER.DTA (file, 127-byte stride):");
var roster = File.ReadAllBytes(Path.Combine(docs, "Roster.dta"));
var expected = new[] { "ALARIC", "BRYNN", "CORVUS", "ELARA", "DIGBY", "FARAMIR" };
var expClass = new[] { 1, 2, 3, 4, 6, 5 };          // Knight,Paladin,Archer,Cleric,Robber,Sorcerer
int found = 0;
for (int slot = 0; slot < RosterFormat.MaxSlots; slot++)
{
    int off = slot * RosterFormat.FileStride;
    if (off + RosterFormat.RecordSize > roster.Length) break;
    if (!RosterFormat.LooksLikeRecord(roster, off)) continue;
    var rec = new CharacterRecord(roster.AsSpan(off, RosterFormat.RecordSize)) { Slot = slot };
    Console.WriteLine($"    slot {slot}: {rec.Name,-8} {rec.ClassName,-9} L{rec.LevelCur} " +
        $"HP {rec.HpCur}/{rec.HpMax} SP {rec.SpCur}/{rec.SpMax} AC {rec.ArmorClass} " +
        $"XP {rec.Experience} Gold {rec.Gold} Gems {rec.Gems} Food {rec.Food} " +
        $"Cond {rec.Condition}({rec.ConditionName})");
    if (found < expected.Length)
    {
        Check($"name slot {slot} == {expected[found]}", rec.Name == expected[found]);
        Check($"class slot {slot} == {RosterFormat.ClassName((byte)expClass[found])}", rec.Class == expClass[found]);
    }
    found++;
}
Check("found exactly 6 characters", found == 6);

// SP sanity: non-casters (Knight ALARIC, Robber DIGBY) have 0 SP; casters > 0
var alaric = new CharacterRecord(roster.AsSpan(0 * RosterFormat.FileStride, RosterFormat.RecordSize));
var elara = new CharacterRecord(roster.AsSpan(3 * RosterFormat.FileStride, RosterFormat.RecordSize));
Check("ALARIC (Knight) SP == 0", alaric.SpMax == 0);
Check("ELARA (Cleric) SP > 0", elara.SpMax > 0);

// gold/gems/food/condition/xp against ALARIC's known bytes
Check("ALARIC gold == 17956", alaric.Gold == 17956);
Check("ALARIC gems == 2348", alaric.Gems == 2348);
Check("ALARIC food == 39", alaric.Food == 39);
Check("ALARIC experience == 255493", alaric.Experience == 255493);
Check("all six have condition 0 (OK)", Enumerable.Range(0, 6)
    .All(s => new CharacterRecord(roster.AsSpan(s * RosterFormat.FileStride, RosterFormat.RecordSize)).Condition == 0));
// gold/gems/food round-trip
var w = new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize));
w.Gold = 0xFFFFFF; w.Gems = 0xFFFF; w.Food = 0xFF; w.Condition = 0;
Check("gold/gems/food set to max round-trip",
    w.Gold == 0xFFFFFF && w.Gems == 0xFFFF && w.Food == 0xFF && w.Condition == 0);

// round-trip: edit + serialize must be byte-identical except changed field
var rt = new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize));
rt.HpMax = 9999; rt.HpCur = 9999;
Check("HP round-trips to 9999", rt.HpMax == 9999 && rt.HpCur == 9999);
rt.Name = "MAXIMUS";
Check("name round-trips", rt.Name == "MAXIMUS");

// inventory item ids + charges live at distinct, non-overlapping single-byte slots
Check("equipment charges at 0x4C", RosterFormat.OffEquipmentCharges == 0x4C);
Check("backpack charges at 0x52", RosterFormat.OffBackpackCharges == 0x52);
Check("charge slots don't overlap item ids",
    RosterFormat.OffEquipmentCharges == RosterFormat.OffBackpack + RosterFormat.ItemSlotCount &&
    RosterFormat.OffBackpackCharges == RosterFormat.OffEquipmentCharges + RosterFormat.ItemSlotCount);
var inv = new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize));
for (int i = 0; i < RosterFormat.ItemSlotCount; i++)
{
    inv.SetByte(RosterFormat.OffEquipment + i, (byte)(0x10 + i));
    inv.SetByte(RosterFormat.OffEquipmentCharges + i, (byte)(0x20 + i));
    inv.SetByte(RosterFormat.OffBackpack + i, (byte)(0x30 + i));
    inv.SetByte(RosterFormat.OffBackpackCharges + i, (byte)(0x40 + i));
}
bool invOk = true;
for (int i = 0; i < RosterFormat.ItemSlotCount; i++)
    invOk &= inv.GetByte(RosterFormat.OffEquipment + i) == 0x10 + i
          && inv.GetByte(RosterFormat.OffEquipmentCharges + i) == 0x20 + i
          && inv.GetByte(RosterFormat.OffBackpack + i) == 0x30 + i
          && inv.GetByte(RosterFormat.OffBackpackCharges + i) == 0x40 + i;
Check("inventory items + charges round-trip independently", invOk);

// item-id table (extracted from MM.EXE): 255 entries, known anchors, category boundaries
Check("item table has 255 names", ItemBook.ItemNames.Count == 255);
Check("item id 0 == (empty)", ItemBook.ItemName(0) == "(empty)");
Check("item id 1 == CLUB", ItemBook.ItemName(1) == "CLUB");
Check("item id 61 == SLING", ItemBook.ItemName(61) == "SLING");
Check("item id 255 == (USELESS ITEM)", ItemBook.ItemName(255) == "(USELESS ITEM)");
Check("category id 1 == 1-handed weapons", ItemBook.ItemCategory(1) == "1-handed weapons");
Check("category id 156 == Shields", ItemBook.ItemCategory(156) == "Shields");
Check("choices include empty + all 255", ItemBook.Choices.Count == 256 && ItemBook.Choices[0].Id == 0);
Check("catalog has 255 entries with cost data", ItemBook.Catalog.Count == 255 && ItemBook.Get(6)!.Cost == 40);

// enhancement families: a weapon resolves base/+1/+2, and stepping changes the id correctly
var maceFam = ItemBook.EnhancementsFor(6);   // MACE
Check("MACE family is base/+1/+2", maceFam.Count == 3
    && maceFam[0].Id == 6 && maceFam[1].Id == 18 && maceFam[2].Id == 30);
Check("MACE +1 reports plus 1", ItemBook.PlusOf(18) == 1);
Check("MACE → +2 yields MACE +2", ItemBook.VariantId(6, 2) == 30);
Check("MACE +2 → base yields MACE", ItemBook.VariantId(30, 0) == 6);
// abbreviated armour bases link to their enchanted variants (all three aliases)
Check("PADDED ARMOR → +1 yields PADDED +1", ItemBook.VariantId(121, 1) == 128);
Check("LEATHER ARMOR → +1 yields LEATHER +1", ItemBook.VariantId(122, 1) == 129);
Check("SCALE ARMOR → +2 yields SCALE +2", ItemBook.VariantId(123, 2) == 136);
// families that the game defines up to +3 expose all four levels (base/+1/+2/+3)
Check("CHAIN MAIL family goes to +3", ItemBook.EnhancementsFor(125).Count == 4
    && ItemBook.VariantId(125, 3) == 143);
Check("GREAT AXE family goes to +3", ItemBook.VariantId(91, 3) == 112);
// named uniques have no enhancement family to step through
Check("FLAMING CLUB has no enhancements", ItemBook.EnhancementsFor(24).Count <= 1);

// item effect/usability data (transcribed from the MM1 item FAQ, name-joined to ItemBook):
// 254 of 255 ids carry effect data; OBSIDIAN BOW (id 85) is the lone gap.
Check("254 items have effect data", ItemBook.Catalog.Count(i => ItemEffectBook.For(i.Id) != null) == 254);
Check("OBSIDIAN BOW (85) has no effect data", ItemEffectBook.For(85) == null && ItemEffectBook.Describe(85) == "");
Check("SWORD OF MIGHT grants +6 Might", ItemBook.ItemName(49) == "SWORD OF MIGHT" && ItemEffectBook.EffectText(49) == "+6 Might");
Check("FLAMING CLUB gives Fire resistance +20%", ItemEffectBook.EffectText(24) == "Fire resistance +20%");
Check("DEFENSE RING gives +1 Armour Class", ItemEffectBook.EffectText(190) == "+1 Armour Class");
Check("CLUB OF NOISE is Cursed", ItemEffectBook.EffectText(25) == "Cursed");
Check("CLUB usable by all classes, any alignment", ItemEffectBook.UsedByText(1) == "All classes · any alignment");
Check("SWORD OF MIGHT usable by Knight, any alignment", ItemEffectBook.UsedByText(49) == "Knight · any alignment");
Check("SPEAR +1 (16) is Good-only", ItemEffectBook.UsedByText(16) == "Archer, Paladin, Knight · Good only");
Check("first misc item (171) cannot be equipped", ItemEffectBook.Describe(171).Contains("Cannot be equipped"));
Check("all-space mask (194) shows only its effect, no 'No class'", ItemEffectBook.Describe(194) == "Unknown special effect");
// ALARIC's equipped weapon byte (0x40) resolves to a real item via the table
Check("ALARIC equipped #1 resolves to a named item",
    ItemBook.ItemName(new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize)).GetByte(RosterFormat.OffEquipment)) == "SCIMITAR");

// ---- RollScanner: locate the temporary create-screen roll buffer ----
Console.WriteLine("\nRollScanner (create-screen roll signature):");
var rollVals = new byte[] { 6, 7, 13, 10, 4, 14, 12 };   // the example roll from the create screen
// contiguous (stride 1): pattern sits at offset 5, nothing else matches
var buf1 = new byte[64];
Array.Copy(rollVals, 0, buf1, 5, rollVals.Length);
var hits1 = MightAndMagic1Trainer.Memory.RollScanner.FindInBuffer(buf1, rollVals, 1).ToList();
Check("stride-1 signature found at offset 5", hits1.Count == 1 && hits1[0] == 5);
Check("stride-2 finds nothing in a contiguous buffer",
    !MightAndMagic1Trainer.Memory.RollScanner.FindInBuffer(buf1, rollVals, 2).Any());
// paired [value, active] layout (stride 2): each stat byte duplicated at the next byte
var buf2 = new byte[64];
for (int k = 0; k < rollVals.Length; k++) { buf2[8 + k * 2] = rollVals[k]; buf2[8 + k * 2 + 1] = rollVals[k]; }
var hits2 = MightAndMagic1Trainer.Memory.RollScanner.FindInBuffer(buf2, rollVals, 2).ToList();
Check("stride-2 signature found at offset 8", hits2.Contains(8));
// range gate accepts a normal roll, rejects an out-of-range byte
Check("InRange accepts a plausible roll", MightAndMagic1Trainer.Memory.RollScanner.InRange(new[] { 3, 9, 18, 11, 7, 15, 12 }));
Check("InRange rejects a 0 byte", !MightAndMagic1Trainer.Memory.RollScanner.InRange(new[] { 0, 9, 18, 11, 7, 15, 12 }));

// ---- MM.CEM: same records, 128-byte stride, offset 0x1B ----
Console.WriteLine("\nMM.CEM (memory dump, 128-byte stride at 0x1B):");
var cem = File.ReadAllBytes(Path.Combine(docs, "MM.CEM"));
int cemBase = 0x1B, cemFound = 0;
for (int slot = 0; slot < RosterFormat.MaxSlots; slot++)
{
    int off = cemBase + slot * RosterFormat.MemoryStride;
    if (off + RosterFormat.RecordSize > cem.Length) break;
    if (!RosterFormat.LooksLikeRecord(cem, off)) break;
    var rec = new CharacterRecord(cem.AsSpan(off, RosterFormat.RecordSize));
    Console.WriteLine($"    @0x{off:X3}: {rec.Name,-8} {rec.ClassName}");
    cemFound++;
}
Check("memory layout: 6 consecutive records at 128-byte stride", cemFound == 6);

// the scanner predicate must NOT match the CHEATENGINE header bytes
Check("predicate rejects file header at 0", !RosterFormat.LooksLikeRecord(cem, 0));

// ---- ThreeD6: the 3d6 odds model behind the roller's statistics ----
Console.WriteLine("\nThreeD6 (3d6 probability model):");
bool Near(double a, double b, double eps) => Math.Abs(a - b) < eps;
Check("P(>=3) == 1 (every roll clears the floor)", ThreeD6.PAtLeast(3) == 1.0);
Check("P(>=2) == 1 (below the 3d6 minimum)", ThreeD6.PAtLeast(2) == 1.0);
Check("P(>=19) == 0 (above the 3d6 maximum)", ThreeD6.PAtLeast(19) == 0.0);
Check("P(>=18) == 1/216 (only 6-6-6)", Near(ThreeD6.PAtLeast(18), 1.0 / 216, 1e-12));
Check("P(>=4) == 215/216 (every roll except 1-1-1)", Near(ThreeD6.PAtLeast(4), 215.0 / 216, 1e-12));
Check("P(>=17) == 4/216 (the four ways to roll 17 or 18)", Near(ThreeD6.PAtLeast(17), 4.0 / 216, 1e-12));
Check("mean == 10.5", Near(ThreeD6.Mean, 10.5, 1e-12));
Check("stddev ≈ 2.9580", Near(ThreeD6.StdDev, 2.95804, 1e-4));
Check("PMeetsAll(no minimums) == 1", ThreeD6.PMeetsAll(new[] { 0, 0, 0, 0, 0, 0, 0 }) == 1.0);
Check("PMeetsAll(one min 16) == P(>=16)",
    Near(ThreeD6.PMeetsAll(new[] { 16, 0, 0, 0, 0, 0, 0 }), ThreeD6.PAtLeast(16), 1e-12));
Check("PMeetsAll(all 18) == (1/216)^7",
    Near(ThreeD6.PMeetsAll(Enumerable.Repeat(18, 7)), Math.Pow(1.0 / 216, 7), 1e-30));
Check("PMeetsAll(any min 19) == 0", ThreeD6.PMeetsAll(new[] { 19, 5, 5, 5, 5, 5, 5 }) == 0.0);

// ---- RollHistory: the observed-roll tally + fairness verdict ----
Console.WriteLine("\nRollHistory (observed-roll statistics):");
var h = new RollHistory(7);
h.Add(new[] { 10, 10, 10, 10, 10, 10, 10 });   // total 70
h.Add(new[] { 12, 12, 12, 12, 12, 12, 12 });   // total 84
Check("counts two distinct rolls", h.Count == 2);
var snap2 = h.Snapshot();
Check("per-stat mean averages the two rolls", Near(snap2.StatMean[0], 11.0, 1e-9));
Check("total mean averages the two totals (77)", Near(snap2.TotalMean, 77.0, 1e-9));
Check("total range is 70–84", snap2.TotalMin == 70 && snap2.TotalMax == 84);

// a back-to-back duplicate (stale read) is ignored
var hDup = new RollHistory(7);
hDup.Add(new[] { 9, 9, 9, 9, 9, 9, 9 });
bool dupAdded = hDup.Add(new[] { 9, 9, 9, 9, 9, 9, 9 });
Check("consecutive duplicate roll is dropped", !dupAdded && hDup.Count == 1);

// a value outside 3–18 means it can't be plain 3d6
var hOob = new RollHistory(7);
for (int i = 0; i < RollHistory.MinSamplesForVerdict + 5; i++)
    hOob.Add(new[] { 10, 10, 10, 10, 10, 10, 3 + (i % 9) });   // vary so dedup doesn't drop
hOob.Add(new[] { 21, 10, 10, 10, 10, 10, 10 });                // 21 is impossible for 3d6
Check("out-of-range stat ⇒ OutOfRange verdict", hOob.Snapshot().Fairness == RollFairness.OutOfRange);

// too few samples ⇒ no verdict yet
var hFew = new RollHistory(7);
for (int i = 0; i < 10; i++) hFew.Add(new[] { 8 + i % 6, 9, 10, 11, 12, 13, 9 });
Check("under the sample threshold ⇒ NeedMoreData", hFew.Snapshot().Fairness == RollFairness.NeedMoreData);

// fair 3d6 (seeded) over many rolls ⇒ reads as consistent
var rng = new Random(20260610);
int Roll3d6() => rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
var hFair = new RollHistory(7);
for (int i = 0; i < 5000; i++) hFair.Add(new[] { Roll3d6(), Roll3d6(), Roll3d6(), Roll3d6(), Roll3d6(), Roll3d6(), Roll3d6() });
var fairSnap = hFair.Snapshot();
Console.WriteLine($"    fair 3d6: mean total {fairSnap.TotalMean:0.0} (exp {fairSnap.ExpectedTotalMean:0.0}), "
    + $"σ {fairSnap.TotalStdDev:0.00} (exp {fairSnap.ExpectedTotalStdDev:0.00}), z {fairSnap.TotalMeanZ:0.00}, verdict {fairSnap.Fairness}");
Check("fair 3d6 sample ⇒ ConsistentWith3d6", fairSnap.Fairness == RollFairness.ConsistentWith3d6);

// a game that pins the total to ~73 (tiny spread) ⇒ reads as constrained
var rng2 = new Random(424242);
var hPinned = new RollHistory(7);
for (int i = 0; i < 5000; i++)
{
    // seven stats each 10 or 11 → totals cluster tightly around 73–74, far below 3d6's spread
    var roll = new int[7];
    for (int k = 0; k < 7; k++) roll[k] = 10 + rng2.Next(0, 2);
    hPinned.Add(roll);
}
var pinnedSnap = hPinned.Snapshot();
Console.WriteLine($"    pinned:   mean total {pinnedSnap.TotalMean:0.0}, σ {pinnedSnap.TotalStdDev:0.00} "
    + $"(exp {pinnedSnap.ExpectedTotalStdDev:0.00}), verdict {pinnedSnap.Fairness}");
Check("pinned-total sample ⇒ LikelyConstrained", pinnedSnap.Fairness == RollFairness.LikelyConstrained);

// ---- ClassBook: the six classes + the experience-per-level table ----
Console.WriteLine("\nClassBook (class reference):");
Check("six classes, ids 1..6 in order",
    ClassBook.Classes.Count == 6 && ClassBook.Classes.Select(c => c.Id).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }));
Check("class ids match RosterFormat class names",
    ClassBook.Classes.All(c => RosterFormat.ClassName(c.Id) == c.Name));
Check("Knight prime stat is Might only", ClassBook.ById(1)!.PrimeStats.SequenceEqual(new[] { "Might" }));
Check("Paladin needs Might, Personality, Endurance",
    ClassBook.ById(2)!.PrimeStats.SequenceEqual(new[] { "Might", "Personality", "Endurance" }));
Check("Robber has no prime stats (any roll qualifies)", ClassBook.ById(6)!.PrimeStats.Count == 0);
Check("Paladin casts Cleric spells", ClassBook.ById(2)!.School == SpellSchool.Cleric);
Check("Archer casts Sorcerer spells", ClassBook.ById(3)!.School == SpellSchool.Sorcerer);
Check("class school matches Spellbook.SchoolForClass",
    ClassBook.Classes.All(c => c.School == Spellbook.SchoolForClass(c.Id)));
Check("minimum prime value is 12", ClassBook.MinPrimeValue == 12);
Check("every class has a non-empty HP-per-level range",
    ClassBook.Classes.All(c => !string.IsNullOrWhiteSpace(c.HitPointsPerLevel)));
Check("Knight HP range is 1–12, Sorcerer 1–6",
    ClassBook.ById(1)!.HitPointsPerLevel == "1–12" && ClassBook.ById(5)!.HitPointsPerLevel == "1–6");
Check("XP table has the configured number of rows", ClassBook.ExperienceTable.Count == ClassBook.LevelRows);
Check("XP table starts at level 2 with ~2000 points",
    ClassBook.ExperienceTable[0].Level == 2 && ClassBook.ExperienceTable[0].FromPrevious == 2000);
Check("XP requirement doubles each level",
    ClassBook.ExperienceTable[1].FromPrevious == 4000 && ClassBook.ExperienceTable[2].FromPrevious == 8000);
Check("XP cumulative is the running sum",
    ClassBook.ExperienceTable[0].Cumulative == 2000 && ClassBook.ExperienceTable[1].Cumulative == 6000);

// ---- Walkthrough: the sectioned solution guide ----
Console.WriteLine("\nWalkthrough (solution guide):");
Check("walkthrough has multiple sections", Walkthrough.Sections.Count >= 8);
Check("every section has a title and at least one step",
    Walkthrough.Sections.All(s => !string.IsNullOrWhiteSpace(s.Title) && s.Steps.Count > 0));
Check("no blank steps", Walkthrough.Sections.All(s => s.Steps.All(t => !string.IsNullOrWhiteSpace(t))));

// ---- Resistances: the 8 × [normal, current] pairs at 0x58 ----
Console.WriteLine("\nResistances (0x58..0x67):");
Check("resistance block is 8 pairs ending at 0x67",
    RosterFormat.OffResistances == 0x58
    && RosterFormat.OffResistances + RosterFormat.ResistanceCount * 2 - 1 == 0x67
    && RosterFormat.Resistances.Length == RosterFormat.ResistanceCount);
Check("scan marker is the Fear pair",
    RosterFormat.MarkerOffsetA == RosterFormat.OffResistances + 5 * 2
    && RosterFormat.Resistances[5] == "Fear");
var resChar = new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize));   // ALARIC
Check("ALARIC has the innate Fear 70/70",
    resChar.GetResistanceNormal(5) == 70 && resChar.GetResistanceActive(5) == 70);
Check("ALARIC has the innate Sleep 25/25",
    resChar.GetResistanceNormal(7) == 25 && resChar.GetResistanceActive(7) == 25);
Check("ALARIC has Fire 5/5 (equipment)",
    resChar.GetResistanceNormal(1) == 5 && resChar.GetResistanceActive(1) == 5);
resChar.SetResistanceNormal(0, 100); resChar.SetResistanceActive(0, 99);
Check("resistance set/get round-trips per byte",
    resChar.GetResistanceNormal(0) == 100 && resChar.GetResistanceActive(0) == 99
    && resChar.GetByte(0x58) == 100 && resChar.GetByte(0x59) == 99);

// The trainer's "Max resistances" command rewrites Fear (the scan marker) to 100, so the
// predicate must still recognise such a record — otherwise a re-scan after maxing would
// fail to relocate the (still valid) roster. A broken marker must still be rejected.
var maxedFear = (byte[])roster.Clone();
maxedFear[RosterFormat.MarkerOffsetA] = 100;
maxedFear[RosterFormat.MarkerOffsetA + 1] = 100;
Check("predicate still matches a record whose Fear was maxed to 100",
    RosterFormat.LooksLikeRecord(maxedFear, 0));
maxedFear[RosterFormat.MarkerOffsetA + 1] = 0;   // mismatched pair = not a marker
Check("predicate rejects a record with a broken Fear marker",
    !RosterFormat.LooksLikeRecord(maxedFear, 0));

// ---- PartySnapshot: roster-format snapshot build + read round-trip ----
Console.WriteLine("\nPartySnapshot (build/read round-trip):");
var snapRecords = new List<CharacterRecord>();
foreach (var slot in new[] { 0, 3, 17 })
{
    var rec = new CharacterRecord(roster.AsSpan(0, RosterFormat.RecordSize)) { Slot = slot };
    rec.Name = $"SNAP{slot}";
    snapRecords.Add(rec);
}
var snapFile = PartySnapshot.Build(snapRecords);
Check("snapshot file is a full 18-slot roster", snapFile.Length == RosterFormat.MaxSlots * RosterFormat.FileStride);
var snapBack = PartySnapshot.Read(snapFile);
Check("snapshot reads back the 3 characters at their slots",
    snapBack.Count == 3 && snapBack.Select(s => s.Slot).SequenceEqual(new[] { 0, 3, 17 }));
Check("snapshot records are byte-identical",
    snapBack.All(s => s.Record.SequenceEqual(snapRecords.First(r => r.Slot == s.Slot).Raw)));
Check("empty slots read as no character", PartySnapshot.Read(new byte[RosterFormat.MaxSlots * RosterFormat.FileStride]).Count == 0);

// ---- MapCalibration: two-anchor linear transform ----
Console.WriteLine("\nMapCalibration (two-anchor transform):");
var calA = new MapAnchor(100, 200, 0, 0);
var calB = new MapAnchor(260, 40, 16, 16);   // 10 px per cell in X, -10 in Y (north is up)
var cal = MapCalibration.FromAnchors(calA, calB);
Check("two good anchors calibrate", cal != null);
var px = cal!.ToPixel(8, 8);
Check("ToPixel maps the midpoint", Math.Abs(px.X - 180) < 1e-9 && Math.Abs(px.Y - 120) < 1e-9);
Check("ToGame inverts ToPixel (with rounding)", cal.ToGame(183, 116) == (8, 8));
Check("anchors are interchangeable", MapCalibration.FromAnchors(calB, calA)!.ToGame(183, 116) == (8, 8));
Check("anchors sharing a row can't calibrate",
    MapCalibration.FromAnchors(new MapAnchor(0, 0, 3, 7), new MapAnchor(50, 50, 9, 7)) == null);
Check("anchors sharing a column can't calibrate",
    MapCalibration.FromAnchors(new MapAnchor(0, 0, 3, 7), new MapAnchor(50, 50, 3, 12)) == null);
Check("coincident pixels can't calibrate",
    MapCalibration.FromAnchors(new MapAnchor(10, 10, 0, 0), new MapAnchor(10, 10, 5, 5)) == null);

// ---- MazeData / BuiltInMazes: every area's walls, and the live-maze fingerprint ----
Console.WriteLine("\nMazeData (the 55 mazes and their walls):");
var mazes = MazeData.BuiltIn();
Check($"all {MazeData.MapCount} areas are bundled", mazes.Maps.Count == MazeData.MapCount);
Check("the bundled set knows it is not the exact bytes", !mazes.IsExact);
Check("records keep the game's own order (Sorpigal, the caves, the overworld, the Astral Plane)",
    mazes.Maps[0].RawName == "sorpigal" && mazes.Maps[5].RawName == "cave1"
    && mazes.Maps[14].RawName == "areaa1" && mazes.Maps[54].RawName == "astral"
    && mazes.Maps.Select(m => m.Index).SequenceEqual(Enumerable.Range(0, MazeData.MapCount)));
Check("every grid is 33 lines of 33 characters",
    BuiltInMazes.Records.Count == MazeData.MapCount
    && BuiltInMazes.Records.All(r => r.Length == 33 && r.All(line => line.Length == 33)));

// Every area is a closed space with a way through it: an all-open map would mean the grid was
// read at the wrong offset, and an all-walled one that the glyphs were misread.
var degenerate = new List<string>();
foreach (var map in mazes.Maps)
    if (Edges(map, EdgeKind.Wall) == 0 || Edges(map, EdgeKind.Open) == 0) degenerate.Add(map.RawName);
Check($"every area has both walls and open ground{Detail(degenerate)}", degenerate.Count == 0);

Check("the towns are door-fronted and the mazes carry secret edges",
    Edges(mazes.Maps[0], EdgeKind.Door) > 0 && Edges(mazes.Maps[0], EdgeKind.Special) > 0
    && mazes.Maps.Sum(m => Edges(m, EdgeKind.Special)) > 100);
Check("illusory walls survive: drawn, but walkable", mazes.Maps.Sum(Illusory) > 1000);

// The passability plane must repack exactly the way the game stores it, since that is what the
// fingerprint compares against the live maze buffer.
bool repacks = true;
foreach (var map in mazes.Maps)
    for (int y = 0; y < MazeMap.Size && repacks; y++)
        for (int x = 0; x < MazeMap.Size; x++)
        {
            int packed = 0;
            for (int dir = 0; dir < 4; dir++) packed |= (int)map.Edge(x, y, dir) << (dir * 2);
            if (packed == map.Plane2[y * MazeMap.Size + x]) continue;
            repacks = false;
            break;
        }
Check("each map's passability plane repacks to the game's own byte layout", repacks);

// The fingerprint has to pick the right map out of all 55 from its plane alone. Feeding each map
// its own plane is the strictest form of that: it passes only if no other map comes within the
// near-match tolerance, which is what stops a wrong area being auto-selected in game.
var confused = mazes.Maps.Where(m => mazes.MatchAt(m.Plane2, 0) != m.Index).Select(m => m.RawName).ToList();
int closest = int.MaxValue;
for (int i = 0; i < mazes.Maps.Count; i++)
    for (int j = i + 1; j < mazes.Maps.Count; j++)
        closest = Math.Min(closest, PlaneDistance(mazes.Maps[i].Plane2, mazes.Maps[j].Plane2));
Check($"every area fingerprints to itself and to nothing else (closest two mazes are {closest} " +
      $"of 1024 edges apart){Detail(confused)}", confused.Count == 0);

Check("a plane that is nobody's matches nothing",
    mazes.MatchAt(new byte[256], 0) < 0
    && mazes.MatchAt(Enumerable.Repeat((byte)0xA5, 256).ToArray(), 0) < 0);

// A plane with a handful of edges recorded from the wrong side is exactly what the bundled data
// does to one-sided doors, and it must still land on the right map.
var scuffed = (byte[])mazes.Maps[19].Plane2.Clone();
for (int k = 0; k < 8; k++) scuffed[k * 7] ^= 0x01;
Check("a near-match still finds its map", mazes.MatchAt(scuffed, 0) == 19);

// ...but only a near one. Corrupting a quarter of the plane must not be forgiven.
var wrecked = (byte[])mazes.Maps[19].Plane2.Clone();
for (int k = 0; k < 64; k++) wrecked[k] ^= 0x55;
Check("a far-off plane is refused rather than guessed at", mazes.MatchAt(wrecked, 0) < 0);

Check("the maze is found wherever it sits in the buffer", FindsInBuffer(mazes, scuff: 0));

// The scan filters candidates on the window's non-blank-field count before measuring the
// distance, and a one-sided door shifts that count. If the filter were tightened past the
// tolerance it would reject exactly the real-world case it exists to speed up, and the
// exact-plane test above would not notice.
Check("a near-match is found by the scan too, not just by a direct compare",
    FindsInBuffer(mazes, scuff: 8));

// The exact path is the one that runs once the player loads their own Mazedata.dta: it matches
// the wall-graphic plane byte for byte and cannot false-positive.
var exact = MazeData.FromBytes(SyntheticMazedata());
Check("a 28,160-byte Mazedata.dta parses into 55 exact maps",
    exact is { IsExact: true } && exact.Maps.Count == MazeData.MapCount);
Check("a short file is refused", MazeData.FromBytes(new byte[MazeData.FileSize - 1]) == null);
// Over every record, not a sampled one: the graphic plane identifies its own map, and the
// passability plane — which is what the bundled path matches on — is nobody's graphic plane.
var exactMisses = Enumerable.Range(0, MazeData.MapCount)
    .Where(i => exact!.MatchAt(exact.Maps[i].Plane1!, 0) != i || exact.MatchAt(exact.Maps[i].Plane2, 0) >= 0)
    .ToList();
Check($"exact data fingerprints on the wall-graphic plane, and on nothing else{Detail(exactMisses.Select(i => i.ToString()).ToList())}",
    exactMisses.Count == 0);

// If the real file is to hand, the bundled grids must agree with it. The one place they cannot
// is a one-sided door, which the atlas they were transcribed from drew as a single edge.
var mazePath = Path.Combine(docs, "Mazedata.dta");
if (File.Exists(mazePath))
{
    var real = MazeData.FromBytes(File.ReadAllBytes(mazePath));
    if (real == null) Check("the bundled Mazedata.dta parses", false);
    else
    {
        int worst = 0;
        string worstName = "";
        for (int i = 0; i < MazeData.MapCount; i++)
        {
            int d = PlaneDistance(real.Maps[i].Plane2, mazes.Maps[i].Plane2);
            if (d > worst) { worst = d; worstName = real.Maps[i].RawName; }
        }
        Check($"the bundled walls match the real file (worst area differs in {worst} of 1024 edges, {worstName})",
            worst <= 48);
    }
}
else
{
    Console.WriteLine("  (no Mazedata.dta in docs\\ \u2014 skipping the bundled-vs-real comparison)");
}

// ---- MonsterBook: the bestiary extracted from MM.EXE ----
Console.WriteLine("\nMonsterBook (bestiary):");
Check("bestiary has 195 monsters with sequential ids",
    MonsterBook.Bestiary.Count == 195
    && MonsterBook.Bestiary.Select(m => m.Id).SequenceEqual(Enumerable.Range(1, 195)));
var sprite = MonsterBook.Bestiary[14];
Check("SPRITE (15) matches the known stats",
    sprite.Name == "SPRITE" && sprite.ArmorClass == 10 && sprite.Damage == 2
    && sprite.Attacks == 1 && sprite.Speed == 20 && sprite.Experience == 250);
var goldDragon = MonsterBook.Bestiary[147];
Check("GOLD DRAGON (148) matches the known stats",
    goldDragon.Name == "GOLD DRAGON" && goldDragon.HpBase == 150 && goldDragon.ArmorClass == 10
    && goldDragon.Damage == 20 && goldDragon.Attacks == 5 && goldDragon.Experience == 50000);
Check("OKRIM (195) is the final special entry",
    MonsterBook.Bestiary[194].Name == "OKRIM" && MonsterBook.Bestiary[194].Experience == 20000);
Check("group boundaries: 1/16 → I, 17 → II, 160 → X, 161/176 → Aquatic, 177/195 → Special",
    MonsterBook.GroupOf(1) == "Group I (easiest)" && MonsterBook.GroupOf(16) == "Group I (easiest)"
    && MonsterBook.GroupOf(17) == "Group II" && MonsterBook.GroupOf(160) == "Group X (hardest)"
    && MonsterBook.GroupOf(161) == "Aquatic" && MonsterBook.GroupOf(176) == "Aquatic"
    && MonsterBook.GroupOf(177) == "Special (fixed encounters)"
    && MonsterBook.GroupOf(195) == "Special (fixed encounters)");
Check("every monster has sane byte-range stats",
    MonsterBook.Bestiary.All(m => m.MaxCount is > 0 and <= 255 && m.HpBase is >= 0 and <= 255
        && m.ArmorClass is >= 0 and <= 255 && m.Attacks is > 0 and <= 255
        && m.Experience is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(m.Name)));

// ---- DumpComparer: address-space diff of two dump files ----
Console.WriteLine("\nDumpComparer (dump diff):");
string diffDir = Path.Combine(Path.GetTempPath(), "mm1-formatcheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diffDir);
try
{
    // Old dump: region 0x1000 (32 bytes) + region 0x3000 (16 bytes).
    // New dump: same two regions but stored in the opposite file order (layout shift),
    // plus a region 0x5000 that only the new dump has.
    var oldR1 = new byte[32]; var oldR2 = new byte[16];
    var newR1 = (byte[])oldR1.Clone(); var newR2 = (byte[])oldR2.Clone();
    newR1[4] = 0xAA;                      // one-byte change at 0x1004
    newR1[8] = 0xBB;                      // 4-byte gap from the last diff → merges into one run
    newR1[20] = 0xCC; newR1[21] = 0xCD;   // separate two-byte run at 0x1014
    newR2[3] = 0x11;                      // run in the second region at 0x3003
    File.WriteAllBytes(Path.Combine(diffDir, "old.bin"), oldR1.Concat(oldR2).ToArray());
    File.WriteAllText(Path.Combine(diffDir, "old.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x1000,0x20,0x0\n0x20,0x3000,0x10,0x0\n");
    File.WriteAllBytes(Path.Combine(diffDir, "new.bin"), newR2.Concat(newR1).Concat(new byte[8]).ToArray());
    File.WriteAllText(Path.Combine(diffDir, "new.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x3000,0x10,0x0\n0x10,0x1000,0x20,0x0\n0x30,0x5000,0x8,0x0\n");

    var oldIdx = DumpComparer.ReadIndex(Path.Combine(diffDir, "old.bin.csv"));
    var newIdx = DumpComparer.ReadIndex(Path.Combine(diffDir, "new.bin.csv"));
    Check("index parses offsets/addresses/sizes",
        oldIdx.Count == 2 && oldIdx[1].Address == 0x3000 && oldIdx[1].FileOffset == 0x20 && oldIdx[1].Size == 0x10);

    var diff = DumpComparer.Compare(
        Path.Combine(diffDir, "old.bin"), oldIdx,
        Path.Combine(diffDir, "new.bin"), newIdx,
        maxRuns: 100, progress: null, CancellationToken.None);
    Check("compares only the shared 48 bytes", diff.BytesCompared == 48);
    Check("counts the 5 changed bytes", diff.BytesChanged == 5);
    Check("reports the new-only region as uncompared", diff.BytesOnlyInOne == 8);
    Check("finds 3 runs (≤4-byte gaps merged)", diff.Runs.Count == 3 && !diff.Truncated);
    var run0 = diff.Runs.FirstOrDefault(r => r.Address == 0x1004);
    Check("merged run spans 0x1004..0x1008 with both changed bytes",
        run0 != null && run0.Length == 5
        && run0.OldBytes.SequenceEqual(new byte[] { 0, 0 }) && run0.NewBytes.SequenceEqual(new byte[] { 0xAA, 0xBB }));
    Check("two-byte run at 0x1014 reads CC CD",
        diff.Runs.Any(r => r.Address == 0x1014 && r.Length == 2 && r.NewBytes.SequenceEqual(new byte[] { 0xCC, 0xCD })));
    Check("second region's run lands at 0x3003",
        diff.Runs.Any(r => r.Address == 0x3003 && r.NewBytes.SequenceEqual(new byte[] { 0x11 })));

    var truncated = DumpComparer.Compare(
        Path.Combine(diffDir, "old.bin"), oldIdx,
        Path.Combine(diffDir, "new.bin"), newIdx,
        maxRuns: 1, progress: null, CancellationToken.None);
    Check("run cap marks the result truncated", truncated.Truncated && truncated.Runs.Count <= 2);

    // A changed run straddling a streamed-chunk boundary must come back as ONE run —
    // shrink the chunk size so the 64-byte region spans several chunks (boundary at 16).
    var oldBig = new byte[64];
    var newBig = (byte[])oldBig.Clone();
    foreach (var i in new[] { 14, 15, 16, 17, 18 }) newBig[i] = 0xEE;   // contiguous across the boundary
    newBig[21] = 0xEF;                                                  // 2-byte gap, still merges (≤4)
    File.WriteAllBytes(Path.Combine(diffDir, "oldbig.bin"), oldBig);
    File.WriteAllText(Path.Combine(diffDir, "oldbig.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x7000,0x40,0x0\n");
    File.WriteAllBytes(Path.Combine(diffDir, "newbig.bin"), newBig);
    File.WriteAllText(Path.Combine(diffDir, "newbig.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x7000,0x40,0x5\n");
    var bigIdxOld = DumpComparer.ReadIndex(Path.Combine(diffDir, "oldbig.bin.csv"));
    var bigIdxNew = DumpComparer.ReadIndex(Path.Combine(diffDir, "newbig.bin.csv"));
    var bigDiff = DumpComparer.Compare(
        Path.Combine(diffDir, "oldbig.bin"), bigIdxOld,
        Path.Combine(diffDir, "newbig.bin"), bigIdxNew,
        maxRuns: 100, progress: null, CancellationToken.None, chunkSize: 16);
    Check("run straddling a chunk boundary merges into one",
        bigDiff.Runs.Count == 1 && bigDiff.Runs[0].Address == 0x700E && bigDiff.Runs[0].Length == 8
        && bigDiff.BytesChanged == 6 && bigDiff.BytesCompared == 64);
    Check("unreadable byte counts are surfaced", bigDiff.BytesUnreadable == 5);
}
finally
{
    Directory.Delete(diffDir, recursive: true);
}

Console.WriteLine(failures == 0 ? "\nALL CHECKS PASSED" : $"\n{failures} CHECK(S) FAILED");
return failures;


// --- maze helpers (local functions live after the top-level statements) -------------
static int Edges(MazeMap map, EdgeKind kind)
{
    int n = 0;
    for (int y = 0; y < MazeMap.Size; y++)
        for (int x = 0; x < MazeMap.Size; x++)
            for (int dir = 0; dir < 4; dir++)
                if (map.Edge(x, y, dir) == kind) n++;
    return n;
}

static int Illusory(MazeMap map)
{
    int n = 0;
    for (int y = 0; y < MazeMap.Size; y++)
        for (int x = 0; x < MazeMap.Size; x++)
            for (int dir = 0; dir < 4; dir++)
                if (map.IsIllusory(x, y, dir)) n++;
    return n;
}

/// <summary>Differing two-bit edge fields between two 256-byte planes.</summary>
static int PlaneDistance(byte[] a, byte[] b)
{
    int n = 0;
    for (int k = 0; k < 256; k++)
    {
        int diff = a[k] ^ b[k];
        for (int dir = 0; dir < 4; dir++) if (((diff >> (dir * 2)) & 3) != 0) n++;
    }
    return n;
}

/// <summary>
/// Plants a map's plane in the middle of a junk buffer and expects the scan to find it.
/// <paramref name="scuff"/> flips that many edges first, standing in for the one-sided doors
/// the bundled data records from a single side.
/// </summary>
static bool FindsInBuffer(MazeData mazes, int scuff)
{
    var buffer = new byte[4096];
    new Random(1234).NextBytes(buffer);
    const int at = 1500;
    var plane = (byte[])mazes.Maps[31].Plane2.Clone();
    for (int k = 0; k < scuff; k++) plane[k * 7] ^= 0x01;
    Array.Copy(plane, 0, buffer, at, 256);
    return mazes.FindInBuffer(buffer) == (31, at);
}

/// <summary>
/// A stand-in Mazedata.dta: 55 records whose two planes are distinct per record, which is all
/// the exact fingerprint path needs to be exercised without shipping the game's own file.
///
/// <para>The bytes have to be genuinely unrelated between records, not an affine ramp. An
/// earlier version used <c>i*7 + k*3</c>, under which record i's plane 2 came out equal to
/// record (i + 51)'s plane 1 — so the "plane 2 is nobody's graphic plane" assertion below
/// passed only because the index it happened to pick had no such partner in range.</para>
/// </summary>
static byte[] SyntheticMazedata()
{
    var bytes = new byte[MazeData.FileSize];
    for (int i = 0; i < MazeData.MapCount; i++)
        for (int k = 0; k < MazeData.RecordSize; k++)
        {
            uint h = (uint)(i * 0x9E3779B1 + k * 0x85EBCA6B);   // a cheap avalanche, not a ramp
            h ^= h >> 15;
            h *= 0x2545F491;
            h ^= h >> 13;
            bytes[i * MazeData.RecordSize + k] = (byte)h;
        }
    return bytes;
}

/// <summary>" — a, b, c" for a list of offenders, or nothing when there are none.</summary>
static string Detail(List<string> names) => names.Count == 0 ? "" : " \u2014 " + string.Join(", ", names);
