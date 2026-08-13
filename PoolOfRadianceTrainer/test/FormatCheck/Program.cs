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

// --- Monster records, and telling them from look-alikes ---------------------------------------
// Both captured verbatim from a live DOSBox fight (four orcs in the Slums). The first is one of
// the orcs. The second starts 0x30 bytes earlier on a stray "Silver 96" string and runs *into* the
// orc's record, so it satisfies the signature scan — a Pascal name, in-range ability scores, race
// 0 — while its combat fields land on the zero padding ahead of the real record. That is exactly
// the record the Combat tab used to list instead of the orcs, and what LooksLikeLiveCombatant is
// there to reject: AC/THAC0 are stored as 60 − value, so zeroed bytes decode to the absurd 60.
const string OrcHex =
    "034F52430000000000000000000000000A060A0A0A0A00000000000000000000000000000000000000000000002900021E0005000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001010E0F1011110901FFFF00000000000000000000000000FFFF0000000000180000000000000000000000000001000000000000010202000100080000003600FF0000000008050000000000000A0001000000000800E6C491C4A2C4000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000018000800A63F0800623E0001010129363400000100080000000509";
const string StrayBufferHex =
    "0953696C7665722039360000000000000A060A0A0A0A0000000000000000000000000000000000000000000000000002034F52430000000000000000000000000A060A0A0A0A00000000000000000000000000000000000000000000002900021E0005000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001010E0F1011110901FFFF00000000000000000000000000FFFF0000000000180000000000000000000000000001000000000000010202000100080000003600FF0000000008050000000000000A0001000000000800E6C491C4A2C40000000000000000000000000000000000000000000000000000000000000000000000000000";

var orc = new CharacterRecord(FromHex(OrcHex));
Check("Orc name", orc.Name, "ORC");
Check("Orc race", orc.RaceName, "Monster");
Check("Orc HP", $"{orc.HpCurrent}/{orc.HpMax}", "5/5");
Check("Orc AC", orc.ArmorClass, 6);
Check("Orc THAC0", orc.Thac0, 19);
Check("Orc status", orc.StatusName, "Okay");
Check("Orc reads as a monster", orc.LooksLikeMonster, true);
Check("Orc reads as a live combatant", orc.LooksLikeLiveCombatant, true);
Check("Signature recognises the orc", CharacterSignature.Looks(orc.Bytes, 0), true);
// Party members are live combatants too — the check must not be monster-specific.
Check("Thrender reads as a live combatant", thrender.LooksLikeLiveCombatant, true);

var stray = new CharacterRecord(FromHex(StrayBufferHex));
Check("Stray buffer fools the signature", CharacterSignature.Looks(stray.Bytes, 0), true);
Check("Stray buffer reads as a monster", stray.LooksLikeMonster, true);
Check("Stray buffer AC is impossible", stray.ArmorClass, 60);
Check("Stray buffer rejected as a combatant", stray.LooksLikeLiveCombatant, false);

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
// Everything except the destination's own next-item link, which must survive so the owner's list
// stays intact — see the duplicate checks further down.
Check("CopyFrom clones every byte but the link",
    dup.Raw.Where((_, i) => i < ItemEntry.OffNextLink || i >= ItemEntry.OffNextLink + 4)
        .SequenceEqual(sling.Raw.Where((_, i) => i < ItemEntry.OffNextLink || i >= ItemEntry.OffNextLink + 4)),
    true);

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
// The cached name the game renders starts with its READY column ("No "), which DisplayName drops —
// the list shows readied as its own checkbox, and "No Wand" reads like part of the item's name.
Check("Wand name drops the readied column", wand.DisplayName, "* Wand");
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

// --- item name rendering ----------------------------------------------------
// Real .ITM records read out of a GOG install's CHRDATA1.ITM (verbatim 63-byte records). The name
// field is the game's own inventory *line*, so it carries the readied column and, for stacked
// pseudo-items, a trailing count that exists nowhere else in the record.
var readiedShield = new ItemEntry(FromHex(
    "0D205965732020536869656C6420" +                                  // " Yes  Shield "
    "000000000000000000000000000000000000000000000000000000000000" +  // rest of the 42-byte name field
    "6B403B00A23B0100010600320000C409000000"), 0);                    // type 0x3B, readied, hidden 6
Check("Readied item drops its 'Yes' column", readiedShield.DisplayName, "Shield");
Check("Readied item still reads as readied", readiedShield.Readied, true);
Check("Unidentified item (hidden-names 6)", readiedShield.Identified, false);
Check("Ticking ID'd identifies it", readiedShield.SetIdentified(true), true);
Check("...and it is now identified", readiedShield.Identified, true);
Check("Un-ticking restores the original masking", readiedShield.SetIdentified(false), true);
Check("...back to the value the save had", readiedShield.Raw[ItemEntry.OffHiddenNames], (byte)6);

// 0x2A..0x2D is the far pointer to the owner's next item — the link the game walks to build an item
// list. This record's reads 406B:0000, which is exactly the link the same Shield had in the running
// game (it pointed at the Silver Mirror behind it in the list).
Check("Item next-link offset", readiedShield.NextLink.Offset, (ushort)0x0000);
Check("Item next-link segment", readiedShield.NextLink.Segment, (ushort)0x406B);
Check("Far pointer resolves seg*16+off", new FarPointer(0x0008, 0x3E4A).Linear, 0x3E4A8u);
Check("Null far pointer ends a chain", new FarPointer(0, 0).IsNull, true);

// Duplicating an item must NOT copy the source's link over the destination's, or the owner's list
// would be spliced onto wherever the source sat in its own list.
var slot = new ItemEntry(readiedShield.Raw);
var donor = new ItemEntry(readiedShield.Raw);
donor.Raw[ItemEntry.OffNextLink + 2] = 0x11;      // a donor sitting elsewhere in its own list
donor.Raw[ItemEntry.OffNextLink + 3] = 0x22;
donor.Raw[ItemEntry.OffType] = 0x24;
Check("Donor has a different link", donor.NextLink.Segment, (ushort)0x2211);
slot.CopyFrom(donor);
Check("Duplicate keeps the slot's own link", slot.NextLink.Segment, (ushort)0x406B);
Check("Duplicate takes the donor's other bytes", slot.Raw[ItemEntry.OffType], (byte)0x24);

// A stacked pseudo-item: the "3" is three pieces of jewelry and lives only in the rendered text
// (the count byte is 0), so DisplayName must keep it.
var jewelry = new ItemEntry(FromHex(
    "094A6577656C72792033" +                                                      // "Jewelry 3"
    "000000000000000000000000000000000000000000000000000000000000000000000000" +  // rest of the name field
    "3B000800623E000001000000C409000000"), 0);
Check("Stacked pseudo-item keeps its count text", jewelry.DisplayName, "Jewelry 3");
Check("...even though the count byte is 0", jewelry.Count, 0);

// --- map terrain ------------------------------------------------------------
// Walls/doors are decoded from the game's own GEO*.DAX geometry (see Game/MapTerrainData.cs).
// These spot-checks anchor the decode to landmarks that are verifiable in the game and clue book.
foreach (var area in MapBook.Areas)
    Check($"{area.Name}: has decoded terrain", area.Terrain != null, true);

var slums = MapBook.Areas.First(a => a.Name == "Slums").Terrain!;
// The Slums treasure at (0,0) is reached from the east through an illusory wall: an edge that can be
// walked through but whose wall graphic is also used as a real wall elsewhere in the level.
Check("Slums illusory wall at (1,0) west", slums[1, 0].West, WallKind.SecretDoor);
Check("Slums outer wall at (0,0) west", slums[0, 0].West, WallKind.Wall);
Check("Slums east exit to New Phlan is a door", slums[15, 4].East, WallKind.Door);
// The map's outer east/south walls have no neighbouring square to hold them, so they are kept on the
// edge squares themselves — without which the schematic would draw only two sides of the border.
Check("Slums outer south wall at (0,15)", slums[0, 15].South, WallKind.Wall);
Check("Slums interior square has no south edge", slums[0, 8].South, WallKind.None);

var phlan = MapBook.Areas.First(a => a.Name == "New Phlan").Terrain!;
Check("New Phlan west exit to the Slums is a door", phlan[0, 4].West, WallKind.Door);
Check("New Phlan harbour at (14,0) is water", phlan[14, 0].Floor, FloorKind.Water);
Check("New Phlan street at (2,3) is walkable", phlan[2, 3].Floor, FloorKind.Normal);
Check("New Phlan is 15 rows deep", MapBook.Areas.First(a => a.Name == "New Phlan").Height, 15);
// The harbour runs out through the south edge at x=13, so that stretch of the border is open water,
// not sea wall — proof the bottom boundary row is really being read for a 15-row map.
Check("New Phlan south wall at (0,14)", phlan[0, 14].South, WallKind.Wall);
Check("New Phlan harbour opens south at (13,14)", phlan[13, 14].South, WallKind.None);

// --- wilderness (overland Moonsea map) --------------------------------------
// Terrain here is transcribed from the clue-book map, not decoded from the game (see
// Game/WildernessMap.cs). These checks pin the parse and the coordinate origin, so a future edit to
// the ASCII can't silently shift every square the teleport targets.
var wild = MapBook.Areas.First(a => a.IsWilderness);
Check("Wilderness is the only overland area", MapBook.Areas.Count(a => a.IsWilderness), 1);
Check("Wilderness is 42 columns wide", wild.Width, WildernessMap.Width);
Check("Wilderness is 33 rows deep", wild.Height, WildernessMap.Height);
Check("Wilderness has terrain", wild.Terrain != null, true);
Check("Wilderness has no walls (overland)", wild.Terrain![0, 32].West, WallKind.None);

var w = wild.Terrain!;
// Row 32 is ". i ........ =====…": plains at the west edge, then deep water from x=10 east.
Check("Wilderness (0,32) is plains", w[0, 32].Floor, FloorKind.Plains);
Check("Wilderness (10,32) is deep water", w[10, 32].Floor, FloorKind.DeepWater);
// The far north-west is the mountain wall; the swamp band sits under x=5-6 in the south-west.
Check("Wilderness (0,5) is mountains", w[0, 5].Floor, FloorKind.Mountains);
Check("Wilderness (5,20) is swamp", w[5, 20].Floor, FloorKind.Swamp);
Check("Wilderness (31,25) is river", w[31, 25].Floor, FloorKind.River);
// (27,25) is where the party stood while the position encoding was recovered live — the clue book
// draws forest there and the game drew a forest backdrop, which is what anchors the origin.
Check("Wilderness (27,25) is forest", w[27, 25].Floor, FloorKind.Forest);
// Row 2 opens "...&&&^^…", so the first three squares are plains and the hills start at x=3.
Check("Wilderness (2,2) is plains", w[2, 2].Floor, FloorKind.Plains);
Check("Wilderness (3,2) is hills", w[3, 2].Floor, FloorKind.Hills);
// Rows 0-1 are blank on the clue-book map and columns 40-41 are past the transcription: both must
// stay Unknown rather than being filled in with a guess.
Check("Wilderness row 0 is not transcribed", w[10, 0].Floor, FloorKind.Unknown);
Check("Wilderness column 41 is not transcribed", w[41, 20].Floor, FloorKind.Unknown);
// A landmark letter leaves its own square's terrain unrecorded, but is listed as a keyed location.
Check("Wilderness landmark square is not terrain", w[30, 15].Floor, FloorKind.Unknown);
Check("Kobold Caves is keyed at (30,15)",
      wild.Locations.Any(l => l.Name.Contains("Kobold Caves") && l.X == 30 && l.Y == 15), true);
Check("Every wilderness landmark is inside the grid",
      wild.Locations.All(l => l.X >= 0 && l.X < wild.Width && l.Y >= 0 && l.Y < wild.Height), true);

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
