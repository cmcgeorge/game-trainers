using GameTrainers.Common.Memory;
using WastelandTrainer.Game;
using WastelandTrainer.Memory;

// Headless verification harness for the Wasteland roster parser. It decodes a captured 7-record
// slice of the live party roster — the default party (Hell Razor, Angela Deth, Thrasher,
// Snake Vargas) followed by three empty slots — and asserts every parsed field against the
// ground-truth stats recorded in .data/memdump.md, then checks the name round-trip, the packed
// skill/inventory decoding, and IsOccupied. Exits 0 on success, 1 on any failure, so it can gate
// the build (Run.ps1 -Test).
//
// The slice was cut from dosbox-x-16124-20260715-112525-175.bin (party inside the Ranger Center),
// starting at roster slot 0 (Hell Razor's name); see .data/extract.ps1.

// Optional live smoke test: FormatCheck --live <pid> runs the structural PartyLocator against a
// running emulator instead of the embedded fixture.
if (args.Length >= 2 && args[0] == "--live")
{
    if (!int.TryParse(args[1], out int livePid))
    {
        Console.WriteLine("Usage: FormatCheck --live <pid>   (pid must be a number)");
        return 2;
    }
    using var liveMem = ProcessMemory.Open(livePid);
    Console.WriteLine($"Attached to pid {livePid} (IsOpen={liveMem.IsOpen}). Running PartyLocator.Find…");
    int regionCount = 0;
    foreach (var _ in liveMem.EnumerateRegions()) regionCount++;
    Console.WriteLine($"EnumerateRegions yielded {regionCount} region(s).");
    var party = PartyLocator.Find(liveMem);
    if (party == null) { Console.WriteLine("No party located."); return 2; }
    Console.WriteLine($"Found roster at 0x{(ulong)party.RosterBase:X} with {party.Members.Count} character(s).");
    foreach (var lc in party.Members)
    {
        var r = lc.Record;
        Console.WriteLine($"  slot {lc.Slot}: {r.Name} @ 0x{(ulong)lc.Address:X} (CON {r.Con}/{r.MaxCon}, weaponByte 0x{r.Bytes[CharacterFormat.OffWeaponState]:X2})");
        for (int s = 0; s < CharacterFormat.ItemSlots; s++)
        {
            int id = r.GetItemId(s);
            if (id == 0) continue;
            int qty = r.GetItemQty(s);
            Console.WriteLine($"      inv[{s,2}] id {id,3} qty {qty,3} (0x{qty:X2}){(qty >= 0x80 ? " JAMMED?" : "")}  {ItemCatalog.ItemName(id)}");
        }
    }
    return party.Members.Count == 0 ? 2 : 0;
}

const string Roster7B64 =
    "SGVsbCBSYXpvcgAAAAAMDg0JDg8LAAAAAAAAHAAcAAwBAAAAAQAAAAAAAAAAAAAAAABQcml2YXRlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgIBAwEHAQkCDAEQAQ0BCAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADQceAB4AHgAeAB4AHgAeAB4ANgEsAC0ABAAxADQoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEFuZ2VsYSBEZXRoAAAACA8OCwoRDgAAAAEAABsAGwABAQAAAAEAAAAAAAAAAAAAAAAAUHJpdmF0ZQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwIHAQkBFAESAQoBFgEPARoBGQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABASIAAgACAAIAAgACAAIAAgADYBLAAtAAQAMQA0KAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABUaHJhc2hlcgAAAAAAABEKCwwQCQgAAAAAAAAiACIADQEAAAABAAAAAAAAAAAAAAAAAFByaXZhdGUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAECBAEDAQcBCQELAQ4BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAANBx4AHgAeAB4AHgAeAB4AHgA2ASwALQAEADEANCgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAU25ha2UgVmFyZ2FzAAAKEQwNCQ0MAAAAAAIAHwAfAAEBAAAAAQAAAAAAAAAAAAAAAABQcml2YXRlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABARgBAwEHAQkBBgEXARkCHAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEBIgACAAIAAgACAAIAAgACAANgEsAC0ABAAxADQoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

int failures = 0;
byte[] roster = Convert.FromBase64String(Roster7B64);
Console.WriteLine($"Decoded roster buffer: {roster.Length} bytes ({roster.Length / CharacterFormat.RecordSize} records)");
Console.WriteLine();

// Ground truth from .data/memdump.md (party inside the Ranger Center).
CheckCharacter(0, "Hell Razor", str: 12, iq: 14, lck: 13, spd: 9, agl: 14, dex: 15, chr: 11,
    maxCon: 28, gender: 0, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(1, "Angela Deth", str: 8, iq: 15, lck: 14, spd: 11, agl: 10, dex: 17, chr: 14,
    maxCon: 27, gender: 1, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(2, "Thrasher", str: 17, iq: 10, lck: 11, spd: 12, agl: 16, dex: 9, chr: 8,
    maxCon: 34, gender: 0, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(3, "Snake Vargas", str: 10, iq: 17, lck: 12, spd: 13, agl: 9, dex: 13, chr: 12,
    maxCon: 31, gender: 0, nationality: 2, level: 1, skp: 1, rank: "Private");

Console.WriteLine("Hell Razor's skills (packed id/level list):");
var hr = new CharacterRecord(roster, 0);
Check("Brawling (1)", hr.GetSkillLevel(1), 2);
Check("Climb (2)", hr.GetSkillLevel(2), 1);
Check("Clip Pistol (3)", hr.GetSkillLevel(3), 1);
Check("Swim (7)", hr.GetSkillLevel(7), 1);
Check("Perception (9)", hr.GetSkillLevel(9), 2);
Check("SMG (12)", hr.GetSkillLevel(12), 1);
Check("Acrobat (13)", hr.GetSkillLevel(13), 1);
Check("Silent Move (16)", hr.GetSkillLevel(16), 1);
Check("Knife Throw (8)", hr.GetSkillLevel(8), 1);
Check("skill he lacks (Doctor 32) = 0", hr.GetSkillLevel(32), 0);
Console.WriteLine();

Console.WriteLine("Hell Razor's inventory (packed id/qty list):");
Check("slot 0 id (M1911A1 = 13)", hr.GetItemId(0), 13);
Check("slot 0 ammo (7 rounds)", hr.GetItemQty(0), 7);
Check("slot 1 id (.45 clip = 30)", hr.GetItemId(1), 30);
Check("item count", hr.ItemCount, 15);
Console.WriteLine();

Console.WriteLine("Inventory compaction (the game reads the list only up to the first empty slot):");
var inv = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
inv.SetItem(0, 13, 7);    // M1911A1, 7 rounds
inv.SetItem(1, 30, 0);    // .45 clip
inv.SetItem(5, 4, 0);     // Knife written past a gap — invisible in-game until the list is compacted
inv.CompactInventory();
Check("gap closed: slot 2 id (Knife)", inv.GetItemId(2), 4);
Check("gap closed: slot 3 empty", inv.GetItemId(3), 0);
Check("compaction preserves ammo", inv.GetItemQty(0), 7);
Check("item count after compaction", inv.ItemCount, 3);
inv.SetItem(0, 0, 0);     // clear the leading item; later items must not be truncated
inv.CompactInventory();
Check("middle clear keeps later items", inv.ItemCount, 2);
Check("order preserved after clear: slot 0", inv.GetItemId(0), 30);
Check("order preserved after clear: slot 1", inv.GetItemId(1), 4);
Console.WriteLine();

Console.WriteLine("Ammo freeze tops ammo-bearing items to the max, leaving other items alone:");
var af = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
af.SetItem(0, 13, 7);    // M1911A1 (Firearm), 7 rounds  -> topped to MaxAmmo
af.SetItem(1, 30, 0);    // 45 clip (Ammo)               -> topped to MaxAmmo
af.SetItem(2, 54, 1);    // Rope (Gear & Quest)          -> left untouched
af.SetItem(3,  4, 0);    // Knife (Melee)                -> left untouched
af.SetItem(4, 38, 0);    // Leather jacket (Armor)       -> left untouched
var topped = new List<int>();
Check("top-up reports it raised something", AmmoFreeze.TopUp(af, topped), true);
Check("firearm topped to the max", af.GetItemQty(0), CharacterFormat.MaxAmmo);
Check("clip topped to the max", af.GetItemQty(1), CharacterFormat.MaxAmmo);
Check("rope (gear) left alone", af.GetItemQty(2), 1);
Check("knife (melee) left alone", af.GetItemQty(3), 0);
Check("armor left alone", af.GetItemQty(4), 0);
Check("only the two ammo slots were raised", topped.Count == 2 && topped.Contains(0) && topped.Contains(1), true);
Check("second pass is a no-op once full", AmmoFreeze.TopUp(af), false);
Console.WriteLine();

Console.WriteLine("Ammo freeze never lowers ammo and preserves the jammed-weapon flag (qty byte bit 7):");
var jr = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
jr.SetItem(0, 29, 120);                                        // Meson cannon (Energy Weapon) already above the max
Check("above-max ammo is not reduced", AmmoFreeze.TopUp(jr), false);
Check("above-max value kept", jr.GetItemQty(0), 120);
jr.SetItem(0, 29, CharacterFormat.InventoryJammedFlag | 10);   // jammed, 10 charges (below the max)
Check("tops up a jammed weapon", AmmoFreeze.TopUp(jr), true);
Check("count raised to the max", jr.GetItemQty(0) & CharacterFormat.InventoryCountMask, CharacterFormat.MaxAmmo);
Check("jam flag preserved", jr.GetItemQty(0) & CharacterFormat.InventoryJammedFlag, CharacterFormat.InventoryJammedFlag);
Console.WriteLine();

Console.WriteLine("Drop-down parsing (pick a name or type a raw id; the separate id box is gone):");
Check("blank = empty slot", ItemCatalog.ParseSelection(""), 0);
Check("bare id", ItemCatalog.ParseSelection("27"), 27);
Check("label form", ItemCatalog.ParseSelection("13  M1911A1 45 pistol"), 13);
Check("known name", ItemCatalog.ParseSelection("Knife"), 4);
Check("name is case-insensitive", ItemCatalog.ParseSelection("rope"), 54);
Check("id past a byte rejected", ItemCatalog.ParseSelection("999"), -1);
Check("gibberish rejected", ItemCatalog.ParseSelection("zzz"), -1);
// A catalog name that begins with a digit must resolve to that item, not to the leading number as a
// raw id (regression: "45 clip" once parsed as id 45 = Crowbar, "7.62mm clip" as id 7 = explosive).
Check("digit-leading name '45 clip' -> item 30", ItemCatalog.ParseSelection("45 clip"), 30);
Check("digit-leading name '7.62mm clip' -> item 31", ItemCatalog.ParseSelection("7.62mm clip"), 31);
Check("digit-leading name is case-insensitive", ItemCatalog.ParseSelection("9MM CLIP"), 32);
Console.WriteLine();

Console.WriteLine("Name encode / decode round-trip (a NUL terminator is always reserved):");
int nameMax = CharacterFormat.NameLength - 1;   // 13; one byte is kept for the terminator
foreach (var name in new[] { "A", "Bo", "Hell Razor", "Snake Vargas", "ThirteenChar!", "FourteenChars!" })
{
    var rec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    rec.Name = name;
    string expected = name.Length > nameMax ? name[..nameMax] : name;
    Check($"round-trip \"{name}\"", rec.Name, expected);
}
Console.WriteLine();

Console.WriteLine("Occupancy discriminators (reject stray byte runs the scanner would misread as a member):");
Check("2-char name occupied", MakeValid("Bo").IsOccupied, true);
Check("13-char name occupied", MakeValid("ThirteenChar!").IsOccupied, true);
Check("1-char name rejected", MakeValid("A").IsOccupied, false);
// Force the out-of-range value at the byte level: the setters now clamp gender/nationality to the
// locatable ranges (0/1 and 0..4), so assigning 4/79 through them would be clamped, not rejected.
var badGender = MakeValid("Test"); badGender.Bytes[CharacterFormat.OffGender] = 4;
Check("gender 4 rejected", badGender.IsOccupied, false);
var badNat = MakeValid("Test"); badNat.Bytes[CharacterFormat.OffNationality] = 79;
Check("nationality 79 rejected", badNat.IsOccupied, false);
var conOverMax = MakeValid("Test");                                  // CON == MAXCON by construction
conOverMax.Bytes[CharacterFormat.OffCon] = 0xFF;                     // force CON above MAXCON at the byte level
conOverMax.Bytes[CharacterFormat.OffCon + 1] = 0xFF;                 // (the setter itself clamps CON <= MAXCON)
Check("CON > MAXCON rejected", conOverMax.IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Edit safety — writes stay within the ranges the locator treats as a real record:");
var edited = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
edited.Name = "Editor";
edited.SetAttribute(0, 150);   // over-range → clamped so the slot never becomes un-locatable
Check("attribute clamped to MaxAttribute", edited.GetAttribute(0), CharacterFormat.MaxAttribute);
edited.SetAttribute(1, 0);     // under-range → clamped up to 1
Check("attribute clamped to >= 1", edited.GetAttribute(1), 1);
for (int a = 2; a < CharacterFormat.AttributeCount; a++) edited.SetAttribute(a, 10);
edited.MaxCon = 20000;         // over-range → clamped to the plausibility ceiling
Check("MaxCon clamped to MaxPlausibleCon", edited.MaxCon, CharacterFormat.MaxPlausibleCon);
edited.Gender = 4;             // over-range → clamped into the locatable 0/1 range
Check("Gender clamped to 0/1", edited.Gender, 1);
edited.Nationality = 79;       // over-range → clamped into the locatable 0..4 range
Check("Nationality clamped to 0..4", edited.Nationality, 4);
Check("edited record stays occupied", edited.IsOccupied, true);
// IsOccupied and the scanner share one validator (CharacterRecord.IsValidRecord); confirm the
// instance property and the static test agree on the same bytes so they can't drift apart.
Check("IsOccupied matches IsValidRecord (occupied)",
    edited.IsOccupied, CharacterRecord.IsValidRecord(edited.Bytes, 0));
Check("IsValidRecord rejects an empty record",
    CharacterRecord.IsValidRecord(new byte[CharacterFormat.RecordSize], 0), false);
Console.WriteLine();

Console.WriteLine("IsOccupied (occupied slots pack from 0; the rest are empty):");
Check("occupied slot 0", new CharacterRecord(roster, 0).IsOccupied, true);
Check("occupied slot 3", new CharacterRecord(roster, 3 * CharacterFormat.RecordSize).IsOccupied, true);
Check("empty slot 4", new CharacterRecord(roster, 4 * CharacterFormat.RecordSize).IsOccupied, false);
Check("empty (0x00) record", new CharacterRecord(new byte[CharacterFormat.RecordSize]).IsOccupied, false);
Check("empty (0xFF) record", new CharacterRecord(FilledWith(0xFF)).IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Record geometry:");
Check("skill block precedes inventory",
    CharacterFormat.OffSkills + CharacterFormat.SkillBlockBytes <= CharacterFormat.OffInventory, true);
Check("inventory block fits the record",
    CharacterFormat.OffInventory + CharacterFormat.ItemBlockBytes <= CharacterFormat.RecordSize, true);
Console.WriteLine();

Console.WriteLine("Reference tables:");
Check("skill count", SkillBook.Skills.Count, 35);
Check("skill 1 name", SkillBook.SkillName(1), "Brawling");
Check("skill 35 name", SkillBook.SkillName(35), "Cyborg Tech");
Check("unknown skill name", SkillBook.SkillName(200), "Skill #200");
Check("item 0 name", ItemCatalog.ItemName(0), "(empty)");
Check("item 13 name (decoded from WL.EXE)", ItemCatalog.ItemName(13), "M1911A1 45 pistol");
Check("item 29 name", ItemCatalog.ItemName(29), "Meson cannon");
Check("item 35 name", ItemCatalog.ItemName(35), "Power armor");
Check("item 94 name", ItemCatalog.ItemName(94), "Cash");
Check("catalog covers 91 items + empty", ItemCatalog.Items.Count, 92);
Check("unused id 70 is unknown", ItemCatalog.ItemName(70), "Item #70");
Check("unknown item name", ItemCatalog.ItemName(200), "Item #200");
Check("gender 1 name", CharacterFormat.GenderName(1), "Female");
Check("nationality 2 name", CharacterFormat.NationalityName(2), "Mexican");
Console.WriteLine();

Console.WriteLine("Party-state header offsets (X/Y are adjacent inside the header):");
Check("Party X at 0x08", CharacterFormat.HeaderPartyX, 0x08);
Check("Party Y at 0x09", CharacterFormat.HeaderPartyY, 0x09);
Check("Y immediately follows X", CharacterFormat.HeaderPartyY, CharacterFormat.HeaderPartyX + 1);
Console.WriteLine();

Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the 256-byte record layout decodes the sample party correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

void CheckCharacter(int slot, string name, int str, int iq, int lck, int spd, int agl, int dex, int chr,
    int maxCon, int gender, int nationality, int level, int skp, string rank)
{
    var rec = new CharacterRecord(roster, slot * CharacterFormat.RecordSize);
    Console.WriteLine($"Slot {slot}: {rec.Name}");
    Check("name", rec.Name, name);
    Check("STR", rec.GetAttribute(0), str);
    Check("IQ", rec.GetAttribute(1), iq);
    Check("LCK", rec.GetAttribute(2), lck);
    Check("SPD", rec.GetAttribute(3), spd);
    Check("AGL", rec.GetAttribute(4), agl);
    Check("DEX", rec.GetAttribute(5), dex);
    Check("CHR", rec.GetAttribute(6), chr);
    Check("MAXCON", rec.MaxCon, maxCon);
    Check("CON = MAXCON at full", rec.Con, maxCon);
    Check("gender", rec.Gender, gender);
    Check("nationality", rec.Nationality, nationality);
    Check("level", rec.Level, level);
    Check("skill points", rec.SkillPoints, skp);
    Check("rank", rec.Rank, rank);
    Check("is occupied", rec.IsOccupied, true);
    Console.WriteLine();
}

byte[] FilledWith(byte b)
{
    var a = new byte[CharacterFormat.RecordSize];
    Array.Fill(a, b);
    return a;
}

// Builds a minimally-valid occupied record (letter-leading name, in-range attributes, CON==MAXCON,
// gender/nationality 0) for the occupancy-discriminator checks.
CharacterRecord MakeValid(string name)
{
    var r = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    r.Name = name;
    for (int a = 0; a < CharacterFormat.AttributeCount; a++) r.SetAttribute(a, 10);
    r.MaxCon = 30;
    r.Con = 30;
    return r;
}

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-24} = {actual}" + (ok ? "" : $"   (expected {expected})"));
}
