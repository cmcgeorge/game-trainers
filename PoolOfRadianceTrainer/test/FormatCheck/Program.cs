using PoolOfRadianceTrainer.Game;

// Headless verification of the CharacterRecord parser against ground-truth bytes captured
// from a live DOSBox-X memory dump of the sample party (see .docs/reverse-engineering.md).
// These two 285-byte records are copied verbatim from the dump; every asserted value was
// independently confirmed by the differential analysis. Run: dotnet run --project test/FormatCheck

// THRENDER GRONE — @ pa 0x1F1791489D8 in the "Exploring / Slums" dump (285 bytes, verbatim).
const string ThrenderHex =
    "0E544852454E4445522047524F4E4500110C0C11100F000000000000000000000000000000000000000000000028010234000B000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001010E0F1011110C0100000000000000000000000800DE41000001000000002400000000000200000000000000010000000000000000020001000200000032015320000000080A00000000000000000004010B03000191A2B3C4E6F7020800B1440000DE4E000000000F00E14E0000000000000000000000000000000000000000000000000000000000000000000000000000000001001A020800BA440000000000010000293B3601000100060002000B09";

// RHIANNON — @ pa 0x1F179148D98 in the same dump (285 bytes, verbatim).
const string RhiannonHex =
    "08524849414E4E4F4E000000000000000F110E110E0F000000000000000000000000000000000000000000150028020DB40007000000000000000000000100000000000001010001000000000000000000000000000000000000000000000000000000000000000000000001010E0D0B0F0C0C0100000000000000000000000800F34400000100000000240000000000020000000000000001000001000001000402000100020000003201890E000000090400000001000000000009090A18020291A2B3C4E6F7030800E74406004C4F0600024F0500504F00000000000000000000000000000000000000000000000000000000000000000000000000000000020033020800F4440000000000010000283C3600000200040000000709";

int failures = 0;

var thrender = new CharacterRecord(FromHex(ThrenderHex));
Check("Thrender name", thrender.Name, "THRENDER GRONE");
Check("Thrender race", thrender.RaceName, "Dwarf");
Check("Thrender class", thrender.ClassName, "Fighter");
Check("Thrender alignment", thrender.AlignmentName, "Lawful Good");
Check("Thrender gender", thrender.GenderName, "Male");
Check("Thrender STR", thrender.Strength, 17);
Check("Thrender INT", thrender.Intelligence, 12);
Check("Thrender WIS", thrender.Wisdom, 12);
Check("Thrender DEX", thrender.Dexterity, 17);
Check("Thrender CON", thrender.Constitution, 16);
Check("Thrender CHA", thrender.Charisma, 15);
Check("Thrender HP cur", thrender.HpCurrent, 11);
Check("Thrender HP max", thrender.HpMax, 11);
Check("Thrender AC", thrender.ArmorClass, 1);
Check("Thrender THAC0", thrender.Thac0, 19);
Check("Thrender age", thrender.Age, 52);
Check("Thrender fighter level", thrender.GetClassLevel(2), 1);
Check("Thrender XP", thrender.Experience, 32);
Check("Thrender status", thrender.StatusName, "Okay");
// Combat-icon color bytes (0xC1..0xC6) — verbatim default template palette from the dump.
Check("Thrender icon color 0 (body)", thrender.GetIconColor(0), 0x91);
Check("Thrender icon color 1 (arm)", thrender.GetIconColor(1), 0xA2);
Check("Thrender icon color 2 (leg)", thrender.GetIconColor(2), 0xB3);
Check("Thrender icon color 3 (hair/face)", thrender.GetIconColor(3), 0xC4);
Check("Thrender icon color 4 (shield)", thrender.GetIconColor(4), 0xE6);
Check("Thrender icon color 5 (weapon)", thrender.GetIconColor(5), 0xF7);

var rhiannon = new CharacterRecord(FromHex(RhiannonHex));
Check("Rhiannon name", rhiannon.Name, "RHIANNON");
Check("Rhiannon race", rhiannon.RaceName, "Elf");
Check("Rhiannon class", rhiannon.ClassName, "Fighter/Mage");
Check("Rhiannon alignment", rhiannon.AlignmentName, "True Neutral");
Check("Rhiannon gender", rhiannon.GenderName, "Female");
Check("Rhiannon STR", rhiannon.Strength, 15);
Check("Rhiannon INT", rhiannon.Intelligence, 17);
Check("Rhiannon HP cur", rhiannon.HpCurrent, 7);
Check("Rhiannon HP max", rhiannon.HpMax, 7);
Check("Rhiannon AC", rhiannon.ArmorClass, 0);
Check("Rhiannon age (elves are long-lived)", rhiannon.Age, 180);
Check("Rhiannon status", rhiannon.StatusName, "Okay");

// Round-trip: editing a field and reading it back must be stable, and the buffer size fixed.
Check("Record size", thrender.Bytes.Length, PorFormat.RecordSize);
var edit = thrender.Clone();
edit.Strength = 18; edit.StrengthPercent = 100; edit.HpMax = 99; edit.ArmorClass = -5; edit.Thac0 = 3;
Check("Round-trip STR", edit.Strength, 18);
Check("Round-trip STR%", edit.StrengthPercent, 100);
Check("Round-trip HP max", edit.HpMax, 99);
Check("Round-trip AC (60-x encoding)", edit.ArmorClass, -5);
Check("Round-trip THAC0 (60-x encoding)", edit.Thac0, 3);
Check("Signature recognises a real record", CharacterSignature.Looks(thrender.Bytes, 0), true);

// RandomizeIconColors must rewrite only the six icon-color bytes (in-range palette nibbles),
// leaving the neighbouring icon-size and item-count bytes untouched.
var recolor = thrender.Clone();
recolor.RandomizeIconColors(new Random(1234));
bool colorsInRange = true, colorsChanged = false;
for (int i = 0; i < PorFormat.IconColorLen; i++)
{
    int v = recolor.GetIconColor(i);
    if ((v & 0x0F) > 15 || ((v >> 4) & 0x0F) > 15) colorsInRange = false;
    if (v != thrender.GetIconColor(i)) colorsChanged = true;
}
Check("Randomized icon colors are valid nibbles", colorsInRange, true);
Check("Randomized icon colors actually changed", colorsChanged, true);
Check("Randomize left icon-size byte alone", recolor.Bytes[PorFormat.OffIconSize], thrender.Bytes[PorFormat.OffIconSize]);
Check("Randomize left item-count byte alone", recolor.Bytes[PorFormat.OffNumberOfItems], thrender.Bytes[PorFormat.OffNumberOfItems]);

// --- Item records (CHRDATAn.ITM — 63-byte records) ------------------------------------------
// Verbatim from THRENDER GRONE's real CHRDATA1.ITM: record 1 (Sling) + record 2 (Ring of
// Protection, unidentified), then record 7 (Shield, readied + unidentified). Cross-checked
// against coab's Item.cs (StructSize 0x3F; type@0x2E, readied@0x34, hidden-names@0x35).
Check("Item record size", ItemEntry.RecordSize, 0x3F);
var slingRing = FromHex(
    "0c204e6f202020536c696e672020202020202020202020202020202020203100000000000000000000000800be482f00002f000000000002000001000000" +
    "000d204e6f2020202a2052696e6720000000000000000000000000000000000000000000000000000000000800c2485da2e04201000006000000001027000000");
var sling = new ItemEntry(slingRing, 0);
var ring = new ItemEntry(slingRing, ItemEntry.RecordSize);
Check("Sling type (0x2F)", (int)sling.Type, 0x2F);
Check("Sling identified", sling.Identified, true);
Check("Sling readied", sling.Readied, false);
Check("Ring type (0x5D RingOfProt)", (int)ring.Type, 0x5D);
Check("Ring unidentified", ring.Identified, false);
Check("Ring value", ring.Value, 10000);
var shield = new ItemEntry(FromHex(
    "0f2059657320202a20536869656c6420000000000000000000000000000000000000000000000000000008009e493b00a23b0100010600960000c409000000"), 0);
Check("Shield type (0x3B)", (int)shield.Type, 0x3B);
Check("Shield readied", shield.Readied, true);
Check("Shield unidentified", shield.Identified, false);
Check("Identify() reveals the ring", ring.Identify() && ring.Identified, true);
Check("Identify() clears hidden-names byte 0x35", ring.Raw[0x35], (byte)0);

// Item signature: recognises real records, rejects an empty span. Sling@0 and Ring@0x3F share
// the slingRing buffer; Shield@0 is its own record. An all-zero buffer must never match.
Check("Signature matches Sling", ItemSignature.Looks(slingRing, 0), true);
Check("Signature matches Ring", ItemSignature.Looks(slingRing, ItemEntry.RecordSize), true);
Check("Signature matches Shield", ItemSignature.Looks(shield.Raw, 0), true);
Check("Signature rejects zero buffer", ItemSignature.Looks(new byte[ItemEntry.RecordSize], 0), false);

// SetCount clamps into 1..255 and reports change; CopyFrom clones the whole 63-byte record.
var stack = new ItemEntry(shield.Raw, 0);
Check("SetCount changes count", stack.SetCount(99), true);
Check("SetCount stored 99", stack.Count, 99);
Check("SetCount clamps 0 to 1", stack.SetCount(0) && stack.Count == 1, true);
Check("SetCount no-op when unchanged", stack.SetCount(1), false);
var dup = new ItemEntry(new byte[ItemEntry.RecordSize], 0);
dup.CopyFrom(sling);
Check("CopyFrom clones name", dup.DisplayName, sling.DisplayName);
Check("CopyFrom clones type", (int)dup.Type, (int)sling.Type);
Check("CopyFrom clones all bytes", dup.Raw.SequenceEqual(sling.Raw), true);

// IsRechargeable: single items (count 0/1) are not; ammo stacks (count > 1) are; wands/staves/rods
// always are (their resource is charges, not a stack). The Sling is a single, uncharged item.
Check("Sling not charged", sling.IsChargedItem, false);
Check("Sling not rechargeable (count 0/1)", sling.IsRechargeable, false);
Check("Sling recharge targets count byte (0x39)", sling.RechargeOffset, ItemEntry.OffCount);
var ammo = new ItemEntry(sling.Raw, 0);
ammo.SetCount(40);
Check("Ammo stack rechargeable (count 40)", ammo.IsRechargeable, true);
Check("Ammo recharge targets count byte (0x39)", ammo.RechargeOffset, ItemEntry.OffCount);
ammo.SetCount(1);
Check("Depleted stack not rechargeable (count 1)", ammo.IsRechargeable, false);

// A real unidentified "Wand of Magic Missiles" (THRENDER's party) captured verbatim from a memory
// dump: NamePart1 = 69 (Wand) at 0x31, Quantity 0 (single item) at 0x39, charges 67 at 0x3C.
var wand = new ItemEntry(FromHex(
    "0D204E6F2020202A2057616E6420" +
    "00000000000000000000000000000000000000000000000000000000" +
    "0800FB454FCEA7450A00000600000000B888435800"), 0);
Check("Wand type (0x4F)", (int)wand.Type, 0x4F);
Check("Wand name", wand.DisplayName, "No * Wand");
Check("Wand is a charged item", wand.IsChargedItem, true);
Check("Wand is a single item (count 0)", wand.Count, 0);
Check("Wand charges read from 0x3C", wand.Charges, 67);
Check("Wand rechargeable despite count 0", wand.IsRechargeable, true);
Check("Wand recharge targets charges byte (0x3C)", wand.RechargeOffset, ItemEntry.OffCharges);
Check("Wand RechargeValue is its charges", wand.RechargeValue, 67);
// Recharge must top up charges (0x3C) and NEVER the stack count (0x39) — bumping 0x39 clones the wand.
Check("Recharge changed the wand", wand.Recharge(99), true);
Check("Recharge set charges to 99", wand.Charges, 99);
Check("Recharge left count at 0 (no clone)", wand.Count, 0);

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the 285-byte record layout decodes the sample party correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-40} = {actual}" + (ok ? "" : $"   (expected {expected})"));
}

static byte[] FromHex(string hex)
{
    hex = hex.Replace(" ", "").Replace("\n", "");
    var bytes = new byte[hex.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
    return bytes;
}
