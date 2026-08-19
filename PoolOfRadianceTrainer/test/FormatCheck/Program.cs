using System.IO;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.ViewModels;

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

// Recharge guards itself rather than trusting the caller to have checked IsRechargeable: on a
// single item the "recharge" byte is the stack count, so an unguarded call would turn one sling
// into a stack of them. This is the case the guard exists for.
var lone = new ItemEntry(sling.Raw, 0);
lone.SetCount(1);
Check("Recharging a single item is refused", lone.Recharge(99), false);
Check("...and its count byte is untouched", lone.Count, 1);

// IsChargedItem rests on the name-part byte at 0x31 alone (the type byte's wand/staff/rod ranges
// aren't verified, and a wrong guard there would reintroduce the cloning bug). That makes the three
// name-part values load-bearing, so pin them: nothing else in the 0..255 range may classify as
// charged, and each of the three must.
int chargedNameParts = 0;
for (int np = 0; np <= 255; np++)
{
    var probe = new ItemEntry(sling.Raw, 0);
    probe.Raw[ItemEntry.OffNamePart1] = (byte)np;
    if (probe.IsChargedItem) chargedNameParts++;
}
Check("Exactly three name parts read as charged", chargedNameParts, 3);
foreach (var (name, part) in new[] { ("rod", ItemEntry.NamePartRod), ("stave", ItemEntry.NamePartStave), ("wand", ItemEntry.NamePartWand) })
{
    var probe = new ItemEntry(sling.Raw, 0);
    probe.Raw[ItemEntry.OffNamePart1] = part;
    Check($"Name part {part} ({name}) is charged", probe.IsChargedItem, true);
}

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

// --- duplicate inventory: the item-count quirk -------------------------------
// DuplicateInventory copies the source's raw item-count byte instead of deriving it from the number
// of records copied. The two legitimately disagree in real saves — the bundled sample party's
// Darkstar stores count 4 with only 3 .ITM records and loads fine — so the copy mirrors a
// known-good character byte for byte rather than imposing a pairing nothing has verified. That is a
// deliberate contract, and this pins it so a later "tidy-up" can't quietly change it.
{
    string dir = Path.Combine(Path.GetTempPath(), "por-formatcheck-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        SaveCharacter Make(string tag, int countByte, int itemCount)
        {
            var savBytes = new byte[PorFormat.RecordSize];
            savBytes[PorFormat.OffNumberOfItems] = (byte)countByte;
            var c = new SaveCharacter
            {
                Index = 1,
                SavPath = Path.Combine(dir, tag + ".SAV"),
                SpcPath = Path.Combine(dir, tag + ".SPC"),
                ItmPath = Path.Combine(dir, tag + ".ITM"),
                SavBytes = savBytes,
                Record = new CharacterRecord(savBytes),
            };
            for (int i = 0; i < itemCount; i++) c.Items.Add(new ItemEntry(readiedShield.Raw));
            return c;
        }

        var from = Make("SRC", countByte: 4, itemCount: 3);   // the Darkstar shape: byte 4, 3 records
        var onto = Make("DST", countByte: 1, itemCount: 1);
        int copied = SaveGame.DuplicateInventory(from, onto);

        Check("Duplicate copies every source item", copied, 3);
        Check("Duplicate mirrors the source's count byte, not its item count",
              onto.SavBytes[PorFormat.OffNumberOfItems], (byte)4);
        Check("...even though only 3 records were written", onto.Items.Count, 3);
        Check("Duplicate points the item head at 'present'",
              onto.SavBytes[PorFormat.OffItemsPtr] != 0 || onto.SavBytes[PorFormat.OffItemsPtr + 1] != 0, true);
        Check("Duplicate wrote the .ITM file",
              new FileInfo(onto.ItmPath).Length, (long)(3 * ItemEntry.RecordSize));
    }
    finally { try { Directory.Delete(dir, recursive: true); } catch { /* temp dir */ } }
}

// --- live-record identity ----------------------------------------------------
// The poll loop re-reads each located record every tick and must notice when the game has freed
// that heap slot and handed it to something else, rather than decoding a stranger under the old
// name — and then stamping them with the old character's frozen HP. Identity is the fields a fight
// does not rewrite; HP, status, money and XP all move while the same creature is being played.
var same = new CharacterRecord(FromHex(ThrenderHex));
same.HpCurrent = 3; same.Status = 4; same.Experience = 999; same.Gold = 12;
Check("A battered record is still the same character", thrender.IsSameCreatureAs(same), true);
Check("...and identity is symmetric", same.IsSameCreatureAs(thrender), true);

var other = new CharacterRecord(FromHex(RhiannonHex));
Check("A different party member is not", thrender.IsSameCreatureAs(other), false);

var renamed = new CharacterRecord(FromHex(ThrenderHex));
renamed.Name = "GRISHNAK";
Check("A recycled slot with a new name is not", thrender.IsSameCreatureAs(renamed), false);

// Max HP is deliberately not part of identity: it moves at a training hall, and treating that as a
// different creature would stop a levelled-up party member refreshing for the rest of the session.
var levelled = new CharacterRecord(FromHex(ThrenderHex));
levelled.HpMax = thrender.HpMax + 6;
Check("A level-up does not break identity", thrender.IsSameCreatureAs(levelled), true);

var reclassed = new CharacterRecord(FromHex(ThrenderHex));
reclassed.Class = thrender.Class + 1;
Check("A different class reads as a different creature", thrender.IsSameCreatureAs(reclassed), false);

// The AC/THAC0 plausibility bounds are load-bearing — they are what keeps a stray buffer that
// happens to match the record *shape* out of the combat list — so pin what they admit and reject.
Check("Live-combatant AC lower bound", CharacterRecord.MinPlausibleAc, -12);
Check("Live-combatant AC upper bound", CharacterRecord.MaxPlausibleAc, 12);
Check("Live-combatant THAC0 upper bound", CharacterRecord.MaxPlausibleThac0, 26);
var zeroed = new CharacterRecord(new byte[PorFormat.RecordSize]);
Check("A zero-filled buffer decodes to AC 60", zeroed.ArmorClass, 60);
Check("...and is refused as a live combatant", zeroed.LooksLikeLiveCombatant, false);

// A creature the trainer has weakened sits at AC 20 — deliberately outside the plausible band,
// since that is what makes the party's next blow unmissable. The arena sweep has to go on
// recognising it anyway, or auto-weaken would weaken a fight once and then report it over while it
// was still being fought. Two tests, and the difference between them is load-bearing: LooksWeakened
// (the sweep's admission mark) reads only the armour-class pair, the one thing the game has no
// reason to move, while IsWeakened (has the auto pass anything left to do?) demands all five.
var weakened = new CharacterRecord(FromHex(OrcHex));
weakened.HpCurrent = CharacterRecord.WeakenedHp;
weakened.ArmorClass = CharacterRecord.WeakenedAc;
weakened.ArmorClassBase = CharacterRecord.WeakenedAc;
weakened.Thac0 = CharacterRecord.WeakenedThac0;
weakened.Thac0Base = CharacterRecord.WeakenedThac0;
Check("Weakened AC is outside the plausible band", weakened.ArmorClass > CharacterRecord.MaxPlausibleAc, true);
Check("A weakened orc carries the weakened mark", weakened.LooksWeakened, true);
Check("...and is fully weakened", weakened.IsWeakened, true);
Check("...and is still a live combatant", weakened.LooksLikeLiveCombatant, true);
Check("...and is still a monster", weakened.LooksLikeMonster, true);
Check("An untouched orc carries no weakened mark", orc.LooksWeakened, false);
Check("...and is not fully weakened", orc.IsWeakened, false);
Check("A zero-filled buffer carries no weakened mark", zeroed.LooksWeakened, false);

// The reason the mark is only the AC pair. A monster cleric heals its weakened ally, or the engine
// recomputes its THAC0 from the base minus a to-hit adjustment: the creature must stay in the sweep
// (it is still on the battlefield, still wearing AC 20) but must read as needing weakening again.
var healed = new CharacterRecord(weakened.Bytes);
healed.HpCurrent = healed.HpMax;
Check("A healed weakened orc keeps the mark", healed.LooksWeakened, true);
Check("...so the sweep still lists it", healed.LooksLikeLiveCombatant, true);
Check("...but it is no longer fully weakened", healed.IsWeakened, false);

var retargeted = new CharacterRecord(weakened.Bytes);
retargeted.Thac0 = 19;                             // engine recompute, not a scratch buffer
Check("A re-derived THAC0 keeps the mark", retargeted.LooksWeakened, true);
Check("...and stays a live combatant", retargeted.LooksLikeLiveCombatant, true);
Check("...but reads as needing weakening again", retargeted.IsWeakened, false);

// Half the mark is no mark: one AC byte at 20 is the shape a stray buffer could stumble into, so
// the record drops back to being judged on the plausibility band alone — which AC 20 fails.
var oneAcByte = new CharacterRecord(weakened.Bytes);
oneAcByte.ArmorClassBase = 6;
Check("One armour-class byte alone is not the mark", oneAcByte.LooksWeakened, false);
Check("...so AC 20 is refused as a live combatant", oneAcByte.LooksLikeLiveCombatant, false);

// Current HP is an unsigned byte, so a creature the engine takes below zero reads back wrapped
// (-5 as 251) rather than negative. Both the record's own plausibility test and the auto pass's
// out-of-the-fight guard have to catch that, or a corpse gets stood back up on 1 hit point.
var overshot = new CharacterRecord(FromHex(OrcHex));
overshot.HpCurrent = 251;
Check("A below-zero hit point reads back wrapped", overshot.HpCurrent > overshot.HpMax, true);
Check("...and is refused as a live combatant", overshot.LooksLikeLiveCombatant, false);

// --- what the automatic combat passes will and won't touch -----------------------------------
// The auto-weaken/auto-kill toggles run off these two predicates twice a second, so what they
// exclude is the whole of their safety: a creature already out of the fight must never be written
// to (that is what would put a corpse back on the battlefield), and a creature already in the
// target state must not be re-written every tick.
static CharacterViewModel Combatant(CharacterRecord r) =>
    new(new OfflineHost(), new LocatedCharacter(0x1000, r));

Check("A healthy orc needs weakening", Combatant(new CharacterRecord(FromHex(OrcHex))).NeedsWeakening, true);
Check("...and needs killing", Combatant(new CharacterRecord(FromHex(OrcHex))).NeedsKilling, true);
Check("An already weakened orc needs nothing", Combatant(new CharacterRecord(weakened.Bytes)).NeedsWeakening, false);
Check("A healed weakened orc needs weakening again", Combatant(new CharacterRecord(healed.Bytes)).NeedsWeakening, true);

var dead = new CharacterRecord(FromHex(OrcHex));
dead.HpCurrent = 0; dead.Status = 6;
Check("A dead orc is out of the fight", Combatant(dead).IsOutOfTheFight, true);
Check("...so nothing weakens it", Combatant(dead).NeedsWeakening, false);
Check("...and nothing kills it twice", Combatant(dead).NeedsKilling, false);

var gone = new CharacterRecord(FromHex(OrcHex));
gone.Status = 8;                                   // fled, disintegrated, turned undead
Check("A creature that is gone is out of the fight", Combatant(gone).IsOutOfTheFight, true);
Check("...so it is never written back into a body", Combatant(gone).NeedsKilling, false);

Check("An overshot hit point counts as out of the fight", Combatant(overshot).IsOutOfTheFight, true);
Check("...so no pass stands it back up", Combatant(overshot).NeedsWeakening, false);

// Slept or held is not beaten — the creature wakes up and fights on, so the passes do act on it.
var slept = new CharacterRecord(FromHex(OrcHex));
slept.Status = 4;                                  // Unconscious, with hit points left
Check("A slept orc is still in the fight", Combatant(slept).IsOutOfTheFight, false);
Check("...and is weakened like any other", Combatant(slept).NeedsWeakening, true);

// --- scan deduplication ------------------------------------------------------
// DOSBox can map the same guest RAM at two host addresses, so the scan sees each record twice and
// has to drop the copy. It cannot do that on identical bytes alone: two same-species monsters are
// byte-for-byte equal at the moment a fight starts, and collapsing them would lose a combatant off
// the list — and with it whichever one the Kill/Weaken buttons were aimed at. Distance is what
// separates the cases: the party and the arena share one 640 KiB DOS heap, so real creatures are
// always close together, while a second mapping of that heap is far outside it.
{
    var kobold = FromHex(RhiannonHex);
    LocatedCharacter At(ulong addr) => new((nuint)addr, new CharacterRecord(kobold));

    var twins = CharacterLocator.Dedupe(new List<LocatedCharacter>
        { At(0x10000000), At(0x10000210) });                       // two records, 528 bytes apart
    Check("Identical records side by side are both kept", twins.Count, 2);

    var aliased = CharacterLocator.Dedupe(new List<LocatedCharacter>
        { At(0x10000000), At(0x10000000 + (ulong)CharacterLocator.ArenaRadius + 0x1000) });
    Check("The same record seen in a second mapping is dropped", aliased.Count, 1);
    Check("...and the lower (live) address is the one kept", (ulong)aliased[0].Address, 0x10000000UL);

    var different = CharacterLocator.Dedupe(new List<LocatedCharacter>
        { new((nuint)0x10000000, new CharacterRecord(FromHex(ThrenderHex))),
          new((nuint)0x20000000, new CharacterRecord(FromHex(RhiannonHex))) });
    Check("Different records far apart are both kept", different.Count, 2);
}

// --- value-scan alignment -----------------------------------------------------
// An exact-value search walks every byte offset, because game data is not reliably aligned — the
// party's indoor position is three adjacent bytes wherever the compiler put them. An unknown-value
// search keeps a candidate per examined offset, so it steps by the value width instead; scanning
// every offset there would multiply an already huge candidate set by 2 or 4 for a search that gets
// narrowed down afterwards anyway.
Check("Exact-value byte scan steps 1", MemorySearcher.StepFor(ScanWidth.Byte, exactValue: true), 1);
Check("Exact-value 16-bit scan steps 1", MemorySearcher.StepFor(ScanWidth.Int16, exactValue: true), 1);
Check("Exact-value 32-bit scan steps 1", MemorySearcher.StepFor(ScanWidth.Int32, exactValue: true), 1);
Check("Unknown 16-bit scan steps 2", MemorySearcher.StepFor(ScanWidth.Int16, exactValue: false), 2);
Check("Unknown 32-bit scan steps 4", MemorySearcher.StepFor(ScanWidth.Int32, exactValue: false), 4);

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

// Every GEO block the game ships is now decoded and named: 29 indoor levels, plus the overland map,
// which has no GEO block of its own. A count check catches an area being dropped or double-added.
Check("All 29 indoor levels plus the overland map", MapBook.Areas.Count, 30);
Check("Every GEO block is used exactly once",
      MapBook.Areas.Count(a => !a.IsWilderness), 29);

// One anchor per newly decoded area group, on a feature the clue book draws unambiguously.
// The Temple of Bane's nave is a colonnade: free-standing pillars down rows 7 and 8, which show up
// as west walls with no north wall to join them to anything.
var bane = MapBook.Areas.First(a => a.Name == "Temple of Bane").Terrain!;
Check("Temple of Bane colonnade pillar at (6,7)", bane[6, 7].West, WallKind.Wall);
Check("Temple of Bane pillar is free-standing", bane[6, 7].North, WallKind.None);

var stojanow = MapBook.Areas.First(a => a.Name == "Stojanow Gate").Terrain!;
Check("Stojanow southern gate at (8,9) is a door", stojanow[8, 9].North, WallKind.Door);
Check("Stojanow northern gate at (8,7) is a door", stojanow[8, 7].North, WallKind.Door);

// The tower fills only columns 1-8, rows 4-11 of its block, which is what pins the clue book's 8x8
// "Upper Level" drawing onto this 16x16 level. Uniquely among the levels the block has no outer
// border wall at all — the squares outside the tower are simply never used.
var tower = MapBook.Areas.First(a => a.Name.EndsWith("upper level")).Terrain!;
Check("Inner Tower west wall at (1,4)", tower[1, 4].West, WallKind.Wall);
Check("Inner Tower north wall at (1,4)", tower[1, 4].North, WallKind.Wall);
Check("Inner Tower has no outer border", tower[0, 0].North, WallKind.None);
Check("Inner Tower solid block at (1,11)", tower[1, 11].Floor, FloorKind.Stone);

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

// --- the known-spell block's ordering ------------------------------------------
// The record flags known spells one byte per spell, but not in the order SpellBook lists them: the
// game's table is grouped by spell *level* first and school second. START.EXE carries that table
// verbatim (Bless · Curse · Cure Light Wounds … · Sleep · Find Traps …), and the sample party's elf
// Fighter/Mage proves it — her four flags decode to four magic-user level-1 spells under this order
// and to cleric level-2/3 spells under any other, on a character with no cleric level at all.
Check("Known-spell block covers every spell", SpellBook.InRecordOrder.Count, PorFormat.KnownSpellsLen);
Check("Known-spell index 0 is Bless (cleric 1)", SpellBook.InRecordOrder[0].Name, "Bless");
Check("Known-spell index 8 starts the mage level 1s", SpellBook.InRecordOrder[8].Name, "Burning Hands");
Check("Known-spell index 20 is Sleep", SpellBook.InRecordOrder[20].Name, "Sleep");
Check("Known-spell index 21 returns to the clerics", SpellBook.InRecordOrder[21].Name, "Find Traps");
Check("Sleep's index is looked up by school", SpellBook.RecordIndexOf("Mage", "Sleep"), 20);
Check("Both schools' Detect Magic are distinct",
      SpellBook.RecordIndexOf("Cleric", "Detect Magic") != SpellBook.RecordIndexOf("Mage", "Detect Magic"), true);
{
    var flagged = Enumerable.Range(0, PorFormat.KnownSpellsLen)
        .Where(i => rhiannon.Bytes[PorFormat.OffKnownSpells + i] != 0)
        .Select(i => SpellBook.InRecordOrder[i])
        .ToList();
    Check("Rhiannon knows four spells", flagged.Count, 4);
    Check("...all magic-user level 1", flagged.All(s => s is { School: "Mage", Level: 1 }), true);
    Check("...and they are her starting spell book",
          string.Join(", ", flagged.Select(s => s.Name)), "Detect Magic, Read Magic, Shield, Sleep");
}

// --- the party generator ------------------------------------------------------
// A generated party has to be *legal* (race/class combinations the game offers, ability minimums
// met, good alignments) and *consistent* (every derived number matching the abilities it was rolled
// from). The two sample records are the ground truth the derived numbers are anchored to.
{
    // Independent copies of the AD&D 1e level-1 rows, so a change to the generator's own tables has
    // to be made deliberately in two places rather than silently agreeing with itself.
    var expectedSaves = new Dictionary<int, int[]>
    {
        [PorFormat.ClassFighter] = new[] { 14, 15, 16, 17, 17 },
        [PorFormat.ClassCleric] = new[] { 10, 13, 14, 16, 15 },
        [PorFormat.ClassMage] = new[] { 14, 13, 11, 15, 12 },
        [PorFormat.ClassThief] = new[] { 13, 12, 14, 16, 15 },
    };
    var expectedThac0 = new Dictionary<int, int>
    {
        [PorFormat.ClassFighter] = 20, [PorFormat.ClassCleric] = 20,
        [PorFormat.ClassMage] = 21, [PorFormat.ClassThief] = 21,
    };
    var expectedDie = new Dictionary<int, int>
    {
        [PorFormat.ClassFighter] = 10, [PorFormat.ClassCleric] = 8,
        [PorFormat.ClassMage] = 4, [PorFormat.ClassThief] = 6,
    };
    var expectedMinimums = new Dictionary<int, (int Stat, int Min)[]>
    {
        [PorFormat.ClassFighter] = new[] { (0, 9), (4, 7) },
        [PorFormat.ClassCleric] = new[] { (2, 9) },
        [PorFormat.ClassMage] = new[] { (1, 9) },
        [PorFormat.ClassThief] = new[] { (3, 9) },
    };
    // What each race may be, from the Rule Book's table (docs/strategy-guide.md §2).
    var allowedClasses = new Dictionary<int, int[]>
    {
        [PorFormat.RaceHuman] = new[]
            { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief },
        [PorFormat.RaceElf] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief, PorFormat.ClassFighterMage,
              PorFormat.ClassFighterThief, PorFormat.ClassMageThief, PorFormat.ClassFighterMageThief },
        // Cleric/Thief is deliberately absent: it is a half-orc combination, not a half-elf one.
        [PorFormat.RaceHalfElf] = new[]
            { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief,
              PorFormat.ClassClericFighter, PorFormat.ClassClericFighterMage, PorFormat.ClassClericMage,
              PorFormat.ClassFighterMage, PorFormat.ClassFighterThief,
              PorFormat.ClassMageThief, PorFormat.ClassFighterMageThief },
        [PorFormat.RaceDwarf] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
        [PorFormat.RaceGnome] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
        [PorFormat.RaceHalfling] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
    };

    int ConBonus(int con, bool warrior) => con <= 3 ? -2 : con <= 6 ? -1 : con <= 14 ? 0
        : con == 15 ? 1 : con == 16 ? 2 : con == 17 ? (warrior ? 3 : 2) : (warrior ? 4 : 2);
    int DexAc(int dex) => dex <= 3 ? -4 : dex == 4 ? -3 : dex == 5 ? -2 : dex == 6 ? -1
        : dex <= 14 ? 0 : dex == 15 ? 1 : dex == 16 ? 2 : dex == 17 ? 3 : 4;
    int StrHit(int str, int pct) => str <= 3 ? -3 : str <= 5 ? -2 : str <= 7 ? -1 : str <= 16 ? 0
        : str == 17 ? 1 : pct <= 50 ? 1 : pct <= 99 ? 2 : 3;

    // 200 parties, so the checks below see every roster pick rather than one lucky draw.
    var parties = Enumerable.Range(0, 200)
        .Select(seed => PartyGenerator.Generate(new Random(seed)))
        .ToList();
    var everyone = parties.SelectMany(p => p).ToList();

    Check("A party is six characters", parties.All(p => p.Count == 6), true);
    Check("Names are unique within a party",
          parties.All(p => p.Select(c => c.Name).Distinct().Count() == p.Count), true);
    Check("Every character is good-aligned",
          everyone.All(c => PartyGenerator.GoodAlignments.Contains(c.Alignment)), true);
    // A thief cannot be lawful good (strategy guide §2), which is the one alignment restriction the
    // classes Pool of Radiance offers actually carries.
    Check("No thief is lawful good",
          everyone.Where(c => c.SingleClasses.Contains(PorFormat.ClassThief)).All(c => c.Alignment != 0), true);
    Check("Every race/class combination is one the game offers",
          everyone.All(c => allowedClasses[c.Race].Contains(c.Class)), true);
    Check("Every party has a fighter, a cleric, a magic-user and a thief",
          parties.All(p => new[] { PorFormat.ClassFighter, PorFormat.ClassCleric, PorFormat.ClassMage, PorFormat.ClassThief }
                            .All(cls => p.Any(c => c.SingleClasses.Contains(cls)))), true);
    Check("The front of the marching order is a fighter",
          parties.All(p => p[0].SingleClasses.Contains(PorFormat.ClassFighter)), true);

    Check("Ability scores are in range",
          everyone.All(c => c.Stats.Length == PorFormat.StatCount && c.Stats.All(v => v >= 3 && v <= 18)), true);
    Check("Every class minimum is met",
          everyone.All(c => c.SingleClasses.All(cls =>
              expectedMinimums[cls].All(m => c.Stats[m.Stat] >= m.Min))), true);
    // Exceptional Strength belongs to fighters at Strength 18 alone, and a female fighter's tops
    // out at 18/50 (strategy guide §2).
    Check("Exceptional Strength only for fighters at STR 18",
          everyone.All(c => c.StrengthPercent == 0 ||
              (c.Stats[0] == 18 && c.SingleClasses.Contains(PorFormat.ClassFighter))), true);
    Check("Female exceptional Strength caps at 18/50",
          everyone.All(c => c.Gender == 0 || c.StrengthPercent <= 50), true);
    Check("STR% is in range", everyone.All(c => c.StrengthPercent is >= 0 and <= 100), true);

    Check("Level 1 in each of the character's classes",
          everyone.All(c => Enumerable.Range(0, PorFormat.ClassLevelCount)
              .All(i => c.ClassLevels[i] == (c.SingleClasses.Contains(i) ? 1 : 0))), true);
    Check("Hit points are the averaged maximum die plus the Constitution bonus",
          everyone.All(c =>
          {
              int rolled = Math.Max(1, c.SingleClasses.Sum(cls => expectedDie[cls]) / c.SingleClasses.Length);
              int hp = Math.Max(1, rolled + ConBonus(c.Stats[4], c.SingleClasses.Contains(PorFormat.ClassFighter)));
              return c.HpRolled == rolled && c.HpMax == hp;
          }), true);
    Check("THAC0 is the class base less the Strength bonus to hit",
          everyone.All(c => c.Thac0Base == c.SingleClasses.Min(cls => expectedThac0[cls]) &&
                            c.Thac0 == c.Thac0Base - StrHit(c.Stats[0], c.StrengthPercent)), true);
    Check("Armor Class is the unarmored 10 less the Dexterity adjustment",
          everyone.All(c => c.ArmorClassBase == PartyGenerator.UnarmoredAc &&
                            c.ArmorClass == PartyGenerator.UnarmoredAc - DexAc(c.Stats[3])), true);
    Check("A multiclass saves as its best class in every category",
          everyone.All(c => Enumerable.Range(0, PorFormat.SavesLen)
              .All(i => c.Saves[i] == c.SingleClasses.Min(cls => expectedSaves[cls][i]))), true);
    Check("Only thieves have thief skills",
          everyone.All(c => c.SingleClasses.Contains(PorFormat.ClassThief)
              ? c.ThiefSkills.Any(v => v > 0)
              : c.ThiefSkills.All(v => v == 0)), true);
    Check("Thief skills are percentages",
          everyone.All(c => c.ThiefSkills.All(v => v is >= 0 and <= 95)), true);
    // Sleep is the spell the early game is won with, so every generated magic-user starts with it.
    Check("Every magic-user knows Sleep and Magic Missile",
          everyone.Where(c => c.SingleClasses.Contains(PorFormat.ClassMage))
              .All(c => c.KnownSpells[SpellBook.RecordIndexOf("Mage", "Sleep")] &&
                        c.KnownSpells[SpellBook.RecordIndexOf("Mage", "Magic Missile")]), true);
    Check("A magic-user starts with four level-1 spells",
          everyone.Where(c => c.SingleClasses.Contains(PorFormat.ClassMage) && !c.SingleClasses.Contains(PorFormat.ClassCleric))
              .All(c => c.KnownSpells.Count(k => k) == 4), true);
    Check("A magic-user knows nothing above level 1",
          everyone.Where(c => c.SingleClasses.Contains(PorFormat.ClassMage))
              .All(c => Enumerable.Range(0, PorFormat.KnownSpellsLen)
                  .All(i => !c.KnownSpells[i] || SpellBook.InRecordOrder[i].Level == 1)), true);
    Check("Non-casters know no spells",
          everyone.Where(c => !c.SingleClasses.Contains(PorFormat.ClassMage) && !c.SingleClasses.Contains(PorFormat.ClassCleric))
              .All(c => c.KnownSpells.All(k => !k) && c.ClericSlots.All(s => s == 0) && c.MageSlots.All(s => s == 0)), true);
    Check("A magic-user gets its one spell a day", everyone
              .Where(c => c.SingleClasses.Contains(PorFormat.ClassMage)).All(c => c.MageSlots[0] == 1), true);
    // A level-1 cleric gets one spell from its class and up to two more from Wisdom — which is why
    // the roller deals Wisdom the best roll for a cleric.
    Check("A cleric's spells a day are its class slot plus its Wisdom bonus", everyone
              .Where(c => c.SingleClasses.Contains(PorFormat.ClassCleric))
              .All(c => c.ClericSlots[0] == 1 + (c.Stats[2] <= 12 ? 0 : c.Stats[2] == 13 ? 1 : 2)), true);
    Check("...so every generated cleric can actually cast something", everyone
              .Where(c => c.SingleClasses.Contains(PorFormat.ClassCleric)).All(c => c.ClericSlots[0] >= 1), true);

    // Same seed, same party — the preview a user sees must be the party that gets written.
    var a = PartyGenerator.Generate(new Random(4242));
    var b = PartyGenerator.Generate(new Random(4242));
    Check("Generation is deterministic for a seed",
          a.Zip(b).All(p => p.First.Name == p.Second.Name && p.First.Class == p.Second.Class &&
                            p.First.Stats.SequenceEqual(p.Second.Stats)), true);

    // A short party drops the second fighter and the support caster, not the roles that carry the game.
    var four = PartyGenerator.Generate(new Random(7), 4);
    Check("A four-character party is four characters", four.Count, 4);
    Check("...and still covers all four classes",
          new[] { PorFormat.ClassFighter, PorFormat.ClassCleric, PorFormat.ClassMage, PorFormat.ClassThief }
              .All(cls => four.Any(c => c.SingleClasses.Contains(cls))), true);
    Check("A one-character party is a fighter",
          PartyGenerator.Generate(new Random(7), 1)[0].SingleClasses.Contains(PorFormat.ClassFighter), true);
    Check("Party size is clamped to what the game allows",
          PartyGenerator.Generate(new Random(7), 99).Count, PartyGenerator.MaxParty);

    // Anchor the derived numbers to the two records captured from the running game: a generated
    // level-1 fighter and Fighter/Mage must come out carrying what the real ones do.
    var genFighter = everyone.First(c => c.Class == PorFormat.ClassFighter);
    Check("A generated fighter's THAC0 base matches Thrender's", genFighter.Thac0Base, thrender.Thac0Base);
    Check("...and its saving throws do too",
          genFighter.Saves.SequenceEqual(Enumerable.Range(0, PorFormat.SavesLen).Select(thrender.GetSave)), true);
    Check("...and its movement", genFighter.Movement, thrender.Bytes[PorFormat.OffMovementBase]);

    var genFighterMage = everyone.First(c => c.Class == PorFormat.ClassFighterMage);
    Check("A generated Fighter/Mage's THAC0 base matches Rhiannon's", genFighterMage.Thac0Base, rhiannon.Thac0Base);
    Check("...and its saving throws do too",
          genFighterMage.Saves.SequenceEqual(Enumerable.Range(0, PorFormat.SavesLen).Select(rhiannon.GetSave)), true);
    // Rhiannon has 7 HP at Constitution 14 — a maximised d10 and d4 averaged, with no bonus.
    Check("...and so do its hit points at her Constitution",
          everyone.First(c => c.Class == PorFormat.ClassFighterMage && c.Stats[4] <= 14).HpMax, rhiannon.HpMax);

    // --- stamping a generated character into a record --------------------------
    Check("Written ranges are ordered, non-overlapping and inside the record", Enumerable
        .Range(0, RolledCharacter.WrittenRanges.Length)
        .All(i =>
        {
            var (off, len) = RolledCharacter.WrittenRanges[i];
            bool ok = off >= 0 && len > 0 && off + len <= PorFormat.RecordSize;
            if (i > 0)
            {
                var (prevOff, prevLen) = RolledCharacter.WrittenRanges[i - 1];
                ok &= prevOff + prevLen <= off;
            }
            return ok;
        }), true);

    var hero = PartyGenerator.Generate(new Random(99))[0];
    var sheet = thrender.Clone();
    hero.StampOnto(sheet);

    // What the generator must NOT touch: the sheet's possessions and the game's own pointers.
    Check("Stamping leaves the money alone", sheet.Gold == thrender.Gold && sheet.Platinum == thrender.Platinum &&
          sheet.GetMoney(1) == thrender.GetMoney(1) && sheet.Gems == thrender.Gems, true);
    Check("Stamping leaves the carried items alone",
          sheet.Bytes[PorFormat.OffNumberOfItems], thrender.Bytes[PorFormat.OffNumberOfItems]);
    Check("Stamping leaves the item-list pointer alone",
          sheet.Bytes.Skip(PorFormat.OffItemsPtr).Take(4).SequenceEqual(thrender.Bytes.Skip(PorFormat.OffItemsPtr).Take(4)), true);
    Check("Stamping leaves the equipped-item pointers alone",
          sheet.Bytes.Skip(PorFormat.OffEquipWeapon).Take(52).SequenceEqual(thrender.Bytes.Skip(PorFormat.OffEquipWeapon).Take(52)), true);
    Check("Stamping leaves the effects pointer alone",
          sheet.Bytes.Skip(PorFormat.OffEffectsPtr).Take(4).SequenceEqual(thrender.Bytes.Skip(PorFormat.OffEffectsPtr).Take(4)), true);
    Check("Stamping leaves the party linked-list pointer alone",
          sheet.Bytes.Skip(PorFormat.OffNextCharPtr).Take(4).SequenceEqual(thrender.Bytes.Skip(PorFormat.OffNextCharPtr).Take(4)), true);
    Check("Stamping leaves encumbrance alone",
          sheet.Bytes.Skip(PorFormat.OffEncumbrance).Take(2).SequenceEqual(thrender.Bytes.Skip(PorFormat.OffEncumbrance).Take(2)), true);
    Check("Stamping leaves the combat icon alone",
          Enumerable.Range(0, PorFormat.IconColorLen).All(i => sheet.GetIconColor(i) == thrender.GetIconColor(i)) &&
          sheet.Bytes[PorFormat.OffIconSize] == thrender.Bytes[PorFormat.OffIconSize], true);
    // The declared ranges are what the live edit pokes into the running game, so a byte that changes
    // outside them would be a change the game never sees.
    Check("Nothing changes outside the declared ranges", Enumerable
        .Range(0, PorFormat.RecordSize)
        .All(o => sheet.Bytes[o] == thrender.Bytes[o] ||
                  RolledCharacter.WrittenRanges.Any(r => o >= r.Offset && o < r.Offset + r.Length)), true);

    // And what it must write: the record has to read back as the character that was rolled.
    Check("Stamped name", sheet.Name, hero.Name);
    Check("Stamped race", sheet.Race, hero.Race);
    Check("Stamped class", sheet.Class, hero.Class);
    Check("Stamped alignment", sheet.Alignment, hero.Alignment);
    Check("Stamped gender", sheet.Gender, hero.Gender);
    Check("Stamped age", sheet.Age, hero.Age);
    Check("Stamped abilities",
          Enumerable.Range(0, PorFormat.StatCount).All(i => sheet.GetStat(i) == hero.Stats[i]), true);
    Check("Stamped exceptional Strength", sheet.StrengthPercent, hero.StrengthPercent);
    Check("Stamped hit points", $"{sheet.HpCurrent}/{sheet.HpMax}", $"{hero.HpMax}/{hero.HpMax}");
    Check("Stamped rolled hit points", sheet.HpRolled, hero.HpRolled);
    Check("Stamped AC (effective and base)", $"{sheet.ArmorClass}/{sheet.ArmorClassBase}",
          $"{hero.ArmorClass}/{hero.ArmorClassBase}");
    Check("Stamped THAC0 (effective and base)", $"{sheet.Thac0}/{sheet.Thac0Base}", $"{hero.Thac0}/{hero.Thac0Base}");
    Check("Stamped saving throws",
          Enumerable.Range(0, PorFormat.SavesLen).All(i => sheet.GetSave(i) == hero.Saves[i]), true);
    Check("Stamped thief skills",
          Enumerable.Range(0, PorFormat.ThiefSkillsLen).All(i => sheet.GetThiefSkill(i) == hero.ThiefSkills[i]), true);
    Check("Stamped class levels",
          Enumerable.Range(0, PorFormat.ClassLevelCount).All(i => sheet.GetClassLevel(i) == hero.ClassLevels[i]), true);
    // A generated character replaces whoever held the slot, so its class bitmask must be rewritten
    // too — the same byte a class change has to carry.
    Check("Stamped class bitmask",
          sheet.Bytes[PorFormat.OffClassMask], (byte)ClassTables.ClassMaskFor(hero.Class));
    Check("Stamped known spells", Enumerable.Range(0, PorFormat.KnownSpellsLen)
          .All(i => (sheet.Bytes[PorFormat.OffKnownSpells + i] != 0) == hero.KnownSpells[i]), true);
    Check("Stamped a fresh experience total", sheet.Experience, 0L);
    Check("Stamped status Okay", sheet.Status, 0);
    Check("Stamped level and attack level",
          $"{sheet.Bytes[PorFormat.OffLevelHighest]}/{sheet.Bytes[PorFormat.OffAttackLevel]}", "1/1");
    Check("Stamped movement",
          $"{sheet.Bytes[PorFormat.OffMovementBase]}/{sheet.Bytes[PorFormat.OffMovementCur]}",
          $"{hero.Movement}/{hero.Movement}");
    // The sheet's previous occupant may have been drained by undead, or have had spells memorized
    // that the new character's class can't cast.
    Check("Stamping clears any level drain",
          sheet.Bytes[PorFormat.OffDrainedLevels] == 0 && sheet.Bytes[PorFormat.OffDrainedHp] == 0 &&
          sheet.Bytes[PorFormat.OffUndeadLevel] == 0, true);
    Check("Stamping clears the memorized spells", Enumerable
        .Range(0, PorFormat.MemorizedSpellsLen)
        .All(i => sheet.Bytes[PorFormat.OffMemorizedSpells + i] == 0), true);
    // The poll loop only adopts a re-read record when it is still the same creature, so a stamped
    // sheet has to read as somebody new — otherwise the trainer would keep showing the old character.
    Check("A stamped sheet is a different creature", thrender.IsSameCreatureAs(sheet), false);
    Check("...and still passes the record signature", CharacterSignature.Looks(sheet.Bytes, 0), true);
    Check("...and reads as a live party member, not a monster",
          sheet.LooksLikeLiveCombatant && !sheet.LooksLikeMonster, true);

    // --- writing a generated party into save files -----------------------------
    // The offline path end to end: load a save folder, stamp a generated party over its characters,
    // write the .SAV files, and load them again. What comes back has to be the party that was
    // rolled — and the files have to stay the size the game wrote them.
    {
        string dir = Path.Combine(Path.GetTempPath(), "por-partygen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "CHRDATA1.SAV"), thrender.Bytes);
            File.WriteAllBytes(Path.Combine(dir, "CHRDATA2.SAV"), rhiannon.Bytes);

            var save = SaveGame.Load(dir);
            Check("The save folder loads both characters", save.Characters.Count, 2);

            var rolled = PartyGenerator.Generate(new Random(2024), 2);
            for (int i = 0; i < save.Characters.Count; i++)
            {
                rolled[i].StampOnto(save.Characters[i].Record);
                SaveGame.WriteRecord(save.Characters[i]);
            }

            var reloaded = SaveGame.Load(dir);
            Check("The rewritten save still holds both characters", reloaded.Characters.Count, 2);
            Check("...whose names came back as rolled",
                  string.Join(", ", reloaded.Characters.Select(c => c.Name)),
                  string.Join(", ", rolled.Select(c => c.Name)));
            Check("...with their classes",
                  reloaded.Characters.Select(c => c.Record.Class).SequenceEqual(rolled.Select(c => c.Class)), true);
            Check("...their abilities",
                  reloaded.Characters.Zip(rolled).All(p => Enumerable.Range(0, PorFormat.StatCount)
                      .All(i => p.First.Record.GetStat(i) == p.Second.Stats[i])), true);
            Check("...and their hit points",
                  reloaded.Characters.Zip(rolled).All(p => p.First.Record.HpMax == p.Second.HpMax &&
                                                           p.First.Record.HpCurrent == p.Second.HpMax), true);
            Check("The .SAV file is still one record long",
                  new FileInfo(Path.Combine(dir, "CHRDATA1.SAV")).Length, (long)PorFormat.RecordSize);
            // The record the game keeps its own bookkeeping in must survive the rewrite intact.
            Check("The rewritten record keeps the slot's money",
                  reloaded.Characters[0].Record.GetMoney(1), thrender.GetMoney(1));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { /* temp dir */ } }
    }

    // Class byte constants must keep pointing at the classes they are named for.
    Check("Class constants match the class table",
          PorFormat.Classes[PorFormat.ClassFighterMage] == "Fighter/Mage" &&
          PorFormat.Classes[PorFormat.ClassClericFighterMage] == "Cleric/Fighter/Mage" &&
          PorFormat.Classes[PorFormat.ClassThief] == "Thief", true);
    Check("Race constants match the race table",
          PorFormat.Races[PorFormat.RaceHalfElf] == "Half-Elf" && PorFormat.Races[PorFormat.RaceHuman] == "Human", true);
    // The class-level bytes are indexed by the same numbers as the single-class values, which is
    // what lets a multiclass write its levels straight from its class list.
    Check("Class bytes double as class-level indices",
          PorFormat.ClassLevelNames[PorFormat.ClassCleric] == "Cleric" &&
          PorFormat.ClassLevelNames[PorFormat.ClassFighter] == "Fighter" &&
          PorFormat.ClassLevelNames[PorFormat.ClassMage] == "Mage" &&
          PorFormat.ClassLevelNames[PorFormat.ClassThief] == "Thief", true);
}

// --- a level-5 fighter, verbatim from a real saved game -----------------------
// ALTHARION — the 285-byte CHRDATA1.SAV of a GOG install's own save. The two dump records above are
// both level 1, so nothing in them could tell a per-level table from a constant; this one is level
// 5 and pins the other end of the line. Its THAC0 base of 16 is four points better than the level-1
// fighter's 20, four levels up, and its saving throws are the fighter level-5 row exactly.
const string AltharionHex =
    "09414C54484152494F4E000000000000120E0C0D0E0F59000000000000000000000000000000000000000000002C0702130019000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005010B0C0D0D0E0C0500000000000000000000000000000000000000000000000000000000B1020200050000000500000000000000000200010002000000320136578500000819000000000000000000010100000002817C2AA516CA0F08004A3E0000263E0800183E0800423E0000000000000000000000000000000000000000000000000000000000000000000000000000000002006E06000000000000000000010000303A360100010006000700190C";

var altharion = new CharacterRecord(FromHex(AltharionHex));
Check("Altharion name", altharion.Name, "ALTHARION");
Check("Altharion race", altharion.RaceName, "Human");
Check("Altharion class", altharion.ClassName, "Fighter");
Check("Altharion is level 5", altharion.GetClassLevel(PorFormat.ClassFighter), 5);
Check("...and the level byte agrees", altharion.Bytes[PorFormat.OffLevelHighest], (byte)5);
Check("...as does the attack level", altharion.Bytes[PorFormat.OffAttackLevel], (byte)5);
Check("Altharion STR 18/89", altharion.StrengthDisplay, "18/89");
Check("Altharion HP", $"{altharion.HpCurrent}/{altharion.HpMax}", "25/25");
Check("Altharion XP", altharion.Experience, 34135L);
Check("Altharion THAC0 base", altharion.Thac0Base, 16);
Check("Altharion AC base is the unarmored 10", altharion.ArmorClassBase, 10);
Check("Altharion movement", altharion.Bytes[PorFormat.OffMovementBase], (byte)12);
Check("Altharion carries no thief skills",
      Enumerable.Range(0, PorFormat.ThiefSkillsLen).All(i => altharion.GetThiefSkill(i) == 0), true);

// --- the class bitmask at 0xB0 ------------------------------------------------
// The rest of the bundled sample party, verbatim from the game's own CHRDATAn.SAV files. Thrender
// and Rhiannon (above) are a fighter and a Fighter/Mage; these four complete the six and, between
// them, cover every class combination the party has — which is what identifies 0xB0.
//
// The byte was undecoded until a caster was diffed against a non-caster to see what a class change
// might be missing. It is a bitmask: mage 0x01, cleric 0x02, thief 0x04, fighter 0x08. Checked
// against 71 character records found across four save folders, in 15 distinct characters covering
// six class combinations, it matches the class-level bytes every single time.
const string BakshiHex =
    "0642414B5348490000000000000000001210110E0C0C5A00000000000000000000000000000000000303051500280409300007010101010101010100000100000000000001010001000000000000000000000000000000000000000000000000000000000000000000000001010A0D0B0F0C0C0100000000000000000000000D00014F00000100000000240000000000020000000000010001000001000000000602000100020000003201B30A0000000B050300000100000000000B050911010291A2B3C4E6F7020F00F94E0F00F94E000000000E00FD4E0000000000000000000000000000000000000000000000000000000000000000000000000000000002007E0209003A4F00000000000100002A3836000001000A0004000709";
const string BrotherSeanHex =
    "0C42524F54484552205345414E000000100C110F1012000000000000000000000000000000000000000103030028070016000A010101010101010100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001010A0D0E100F0C0100000000000000000000000000000000000100000000240000000000020000000000010000000000000000000002000100020000003201FA2000000002050300000000000000000304051A030291A2B3C4E6F7030100D04F0100D04F0F00D74F0000D44F0000000000000000000000000000000000000000000000000000000000000000000000000000000002007E020E00DB4F0000000000010000283A3601000100060002000A06";
const string DarkstarHex =
    "084441524B53544152000000000000000C120B110F0E00000000000000000000000000000000000000000015002807051B0005000000000000000000000100000000000001010001000000000000000000000000000000000000000000000000000000000000000000000001010E0D0B0F0C0C010000000000000000000000000000000000010000000024000000000002000000000000000000000100000100030200010002000000320155200000000103000000010000000000060A091D040291A2B3C4E6F7010E0021500E00215000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001005D000D00255000000000000100002835300300010003000000050C";
const string PhineasHex =
    "075048494E4541530000000000000000100C0A120E1000000000000000000000000000000000000000000000002805062E0006000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001010D0C0E100F0C010000002D2D1E23230F46000A003750000001000000002400000000000100000000000000000000000100000001020001000200000032017E20000000040600000000000000000001060D05050191A2B3C4E6F7030D0068500D006850000000000B007050000000000000000000000000000000000000000000000000000000000000000000000000000000000100D9000000000000000000000100002838320300010004000100060C";

var bakshi = new CharacterRecord(FromHex(BakshiHex));
var brotherSean = new CharacterRecord(FromHex(BrotherSeanHex));
var darkstar = new CharacterRecord(FromHex(DarkstarHex));
var phineas = new CharacterRecord(FromHex(PhineasHex));

Check("Bakshi is a Cleric/Fighter/Mage", $"{bakshi.Name} {bakshi.RaceName} {bakshi.ClassName}", "BAKSHI Half-Elf Cleric/Fighter/Mage");
Check("Brother Sean is a cleric", $"{brotherSean.Name} {brotherSean.ClassName}", "BROTHER SEAN Cleric");
Check("Darkstar is a magic-user", $"{darkstar.Name} {darkstar.ClassName}", "DARKSTAR Mage");
Check("Phineas is a thief", $"{phineas.Name} {phineas.RaceName} {phineas.ClassName}", "PHINEAS Halfling Thief");

// One bit per class, and a multiclass carries the union of its classes' bits.
Check("Mage bit", darkstar.Bytes[PorFormat.OffClassMask], (byte)0x01);
Check("Cleric bit", brotherSean.Bytes[PorFormat.OffClassMask], (byte)0x02);
Check("Thief bit", phineas.Bytes[PorFormat.OffClassMask], (byte)0x04);
Check("Fighter bit", thrender.Bytes[PorFormat.OffClassMask], (byte)0x08);
Check("Fighter/Mage is the two bits together", rhiannon.Bytes[PorFormat.OffClassMask], (byte)0x09);
Check("Cleric/Fighter/Mage is the three", bakshi.Bytes[PorFormat.OffClassMask], (byte)0x0B);
Check("A level-5 fighter carries the same fighter bit", altharion.Bytes[PorFormat.OffClassMask], (byte)0x08);

// Every record decoded here must agree with the mask its own class byte implies — the mask is
// derived from the class, so a character whose two disagreed would break the whole reading.
Check("Every record's mask matches its class byte",
      new[] { thrender, rhiannon, altharion, bakshi, brotherSean, darkstar, phineas }
          .All(r => r.Bytes[PorFormat.OffClassMask] == ClassTables.ClassMaskFor(r.Class)), true);
// ...and its per-class level bytes, which is the same claim read the other way round.
Check("...and the classes its level bytes name",
      new[] { thrender, rhiannon, altharion, bakshi, brotherSean, darkstar, phineas }
          .All(r => r.Bytes[PorFormat.OffClassMask] == ClassTables.ClassMask(
              Enumerable.Range(0, PorFormat.ClassLevelCount).Where(i => r.GetClassLevel(i) > 0))), true);

// --- spells per day, solved from real casters ---------------------------------
// Two more characters from another saved game: the only level-6 casters available, and the only
// evidence for the top of either table. Between these and the two level-1 casters above, the class
// rows and the Wisdom bonus are determined rather than assumed — which is how the reference table
// in ClassRaceBook was found to be wrong (its cleric column was a level out, and its magic-user
// column wrong at 5 and 6).
const string AlfredHex =
    "06414C4652454400000000000000000012121212111264000000000000000003030303031717171717292929002A070013002401010101010101010000000000000000000000000001010101010101000000000000000101010101010101010000000000000000000000000101090C0D0F0E0C06000000000000000000000006000F4500000100000000000000000000ED00130008000600000000000000000000020001000200000032014C79830500021505050300000000000002040004020291A2B3C4E6F70A0000E8440000E8440A00FF440E00EF440900034500000000000000000000000000000000000000000D00F3440C00F74400000000000000000200E2030800104500000000000100002F423901000100060009002409";
const string TarryHex =
    "0554415252590000000000000000000012121212120E64000000000000000000000000000F0F0F0F21222F33002907051C001C000000000000000001000100000001000001010001000000000000000000010101010000000000000000000001000101000101000000010001010D0B090D0A0C0600000000000000000000000500704500000100000000000000000000EC0013000800000000000006000001000002000100020000003201B438D70500010F000000040202000000090A061C040291A2B3C4E6F70A0F00484500000000000000000000000006006C4500000000000000000000000007006845000000000B005845080064450000000000000000010034060E00704500000000000100002C423C01000100020006001C0C";

var alfred = new CharacterRecord(FromHex(AlfredHex));
var tarry = new CharacterRecord(FromHex(TarryHex));
Check("Alfred is a level-6 cleric",
      $"{alfred.ClassName} {alfred.GetClassLevel(PorFormat.ClassCleric)} WIS {alfred.Wisdom}", "Cleric 6 WIS 18");
Check("Tarry is a level-6 magic-user",
      $"{tarry.ClassName} {tarry.GetClassLevel(PorFormat.ClassMage)}", "Mage 6");
Check("Alfred's class bit", alfred.Bytes[PorFormat.OffClassMask], (byte)0x02);
Check("Tarry's class bit", tarry.Bytes[PorFormat.OffClassMask], (byte)0x01);

// Each caster's stored spells a day must come back out of the tables exactly.
string Slots(CharacterRecord r, int off) => $"{r.Bytes[off]}/{r.Bytes[off + 1]}/{r.Bytes[off + 2]}";
Check("Level-1 cleric, Wisdom 17 (Brother Sean)",
      string.Join("/", ClassTables.ClericSlots(1, brotherSean.Wisdom)), Slots(brotherSean, PorFormat.OffClericSlots));
Check("Level-1 cleric of a Cleric/Fighter/Mage (Bakshi)",
      string.Join("/", ClassTables.ClericSlots(1, bakshi.Wisdom)), Slots(bakshi, PorFormat.OffClericSlots));
Check("Level-6 cleric, Wisdom 18 (Alfred)",
      string.Join("/", ClassTables.ClericSlots(6, alfred.Wisdom)), Slots(alfred, PorFormat.OffClericSlots));
Check("Level-1 magic-user (Darkstar)",
      string.Join("/", ClassTables.MageSlots(1)), Slots(darkstar, PorFormat.OffMageSlots));
Check("Level-1 magic-user of a Cleric/Fighter/Mage (Bakshi)",
      string.Join("/", ClassTables.MageSlots(1)), Slots(bakshi, PorFormat.OffMageSlots));
Check("Level-6 magic-user (Tarry)",
      string.Join("/", ClassTables.MageSlots(6)), Slots(tarry, PorFormat.OffMageSlots));
// A level-1 cleric gets one spell from its class and the rest from Wisdom — the row this trainer
// used to show as none at all. Wisdom's 2nd and 3rd-level bonus spells wait for the levels that
// can cast them, which is what Brother Sean's 3/0/0 at Wisdom 17 shows.
Check("A level-1 cleric's own class slot",
      string.Join("/", ClassTables.ClericSlots(1, 9)), "1/0/0");
Check("Wisdom's higher bonuses wait for the levels that can cast them",
      string.Join("/", ClassTables.ClericSlots(1, 18)), "3/0/0");
Check("...and arrive with them", string.Join("/", ClassTables.ClericSlots(5, 18)), "5/5/2");
// The displayed reference table must say the same thing as the table the trainer computes from.
Check("The Rules tab's cleric row matches the computed slots",
      ClassRaceBook.LevelProgression.First(r => r.Level == 6).ClericSpells,
      string.Join("/", ClassTables.ClericSlots(6, 9)));
Check("The Rules tab's magic-user row matches too",
      ClassRaceBook.LevelProgression.First(r => r.Level == 6).MageSpells,
      string.Join("/", ClassTables.MageSlots(6)));

// Brother Sean also shows how a cleric's spell book is stored: it knows every cleric spell of the
// levels it can cast, which is what a class change to cleric writes.
Check("Brother Sean's spells a day",
      brotherSean.Bytes[PorFormat.OffClericSlots], (byte)3);
Check("...and he knows every cleric level-1 spell",
      Enumerable.Range(0, PorFormat.KnownSpellsLen)
          .Count(i => brotherSean.Bytes[PorFormat.OffKnownSpells + i] != 0), 8);
Check("...all of them cleric level 1",
      Enumerable.Range(0, PorFormat.KnownSpellsLen)
          .Where(i => brotherSean.Bytes[PorFormat.OffKnownSpells + i] != 0)
          .All(i => SpellBook.InRecordOrder[i] is { School: "Cleric", Level: 1 }), true);
// Which is exactly what a class change to cleric writes — the game does it this way itself.
Check("A generated cleric's spell book matches the real one's shape",
      ClassChange.Plan(brotherSean.Clone(), PorFormat.ClassCleric).KnownSpells
          .Select((k, i) => k == (brotherSean.Bytes[PorFormat.OffKnownSpells + i] != 0)).All(same => same), true);
Check("Darkstar the level-1 mage knows four spells",
      Enumerable.Range(0, PorFormat.KnownSpellsLen)
          .Count(i => darkstar.Bytes[PorFormat.OffKnownSpells + i] != 0), 4);
Check("...and has one spell a day", darkstar.Bytes[PorFormat.OffMageSlots], (byte)1);

// --- the class/race tables ----------------------------------------------------
// The two fighter anchors are four levels apart, so a wrong per-level rule cannot satisfy both.
Check("Fighter THAC0 at level 1 matches Thrender", ClassTables.Thac0(PorFormat.ClassFighter, 1), thrender.Thac0Base);
Check("Fighter THAC0 at level 5 matches Altharion", ClassTables.Thac0(PorFormat.ClassFighter, 5), altharion.Thac0Base);
Check("Fighter saves at level 1 match Thrender",
      ClassTables.Saves(PorFormat.ClassFighter, 1).SequenceEqual(
          Enumerable.Range(0, PorFormat.SavesLen).Select(thrender.GetSave)), true);
Check("Fighter saves at level 5 match Altharion",
      ClassTables.Saves(PorFormat.ClassFighter, 5).SequenceEqual(
          Enumerable.Range(0, PorFormat.SavesLen).Select(altharion.GetSave)), true);
// A multiclass takes the best of its classes in each category, which is what Rhiannon carries.
Check("Fighter/Mage saves match Rhiannon",
      ClassTables.SavesFor(new[] { PorFormat.ClassFighter, PorFormat.ClassMage }, new[] { 1, 1 })
          .SequenceEqual(Enumerable.Range(0, PorFormat.SavesLen).Select(rhiannon.GetSave)), true);
Check("Fighter/Mage THAC0 base matches Rhiannon",
      Math.Min(ClassTables.Thac0(PorFormat.ClassFighter, 1), ClassTables.Thac0(PorFormat.ClassMage, 1)),
      rhiannon.Thac0Base);
// Saving throws improve (or hold) with level, never worsen — a transcription slip in a row would
// usually break this before it broke anything else.
Check("Saving throws never get worse with level", ClassTables.BaseClasses.All(cls =>
    Enumerable.Range(2, ClassTables.MaxLevel - 1).All(l =>
        ClassTables.Saves(cls, l).Zip(ClassTables.Saves(cls, l - 1)).All(p => p.First <= p.Second))), true);
Check("THAC0 never gets worse with level", ClassTables.BaseClasses.All(cls =>
    Enumerable.Range(2, ClassTables.MaxLevel - 1).All(l =>
        ClassTables.Thac0(cls, l) <= ClassTables.Thac0(cls, l - 1))), true);
Check("Thief skills never get worse with level",
    Enumerable.Range(2, ClassTables.MaxLevel - 1).All(l =>
        ClassTables.ThiefSkillBase(l).Zip(ClassTables.ThiefSkillBase(l - 1)).All(p => p.First >= p.Second)), true);

// Level caps: the lower of the racial limit and the training hall's.
Check("Elf fighters stop at 7", ClassTables.LevelCap(PorFormat.RaceElf, PorFormat.ClassFighter), 7);
Check("Dwarf fighters stop at the hall's 8", ClassTables.LevelCap(PorFormat.RaceDwarf, PorFormat.ClassFighter), 8);
Check("Halfling fighters stop at 6", ClassTables.LevelCap(PorFormat.RaceHalfling, PorFormat.ClassFighter), 6);
Check("Half-elf clerics stop at 5", ClassTables.LevelCap(PorFormat.RaceHalfElf, PorFormat.ClassCleric), 5);
Check("Human clerics stop at the hall's 6", ClassTables.LevelCap(PorFormat.RaceHuman, PorFormat.ClassCleric), 6);
Check("Thieves have no racial ceiling below the hall's 9",
      ClassTables.LevelCap(PorFormat.RaceHalfling, PorFormat.ClassThief), 9);
Check("A dwarf cannot be a magic-user at all", ClassTables.LevelCap(PorFormat.RaceDwarf, PorFormat.ClassMage), 0);
Check("An elf cannot be a cleric at all", ClassTables.CanTake(PorFormat.RaceElf, PorFormat.ClassCleric), false);
Check("A multiclass stops at its shortest cap",
      ClassTables.LevelCapFor(PorFormat.RaceHalfElf,
          new[] { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage }), 5);

// Race legality, entered a second time from the Rule Book's table so the two have to agree.
{
    var expectedLegal = new Dictionary<int, int[]>
    {
        [PorFormat.RaceHuman] = new[]
            { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief },
        [PorFormat.RaceElf] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief, PorFormat.ClassFighterMage,
              PorFormat.ClassFighterThief, PorFormat.ClassFighterMageThief, PorFormat.ClassMageThief },
        [PorFormat.RaceHalfElf] = new[]
            { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief,
              PorFormat.ClassClericFighter, PorFormat.ClassClericFighterMage, PorFormat.ClassClericMage,
              PorFormat.ClassFighterMage, PorFormat.ClassFighterThief, PorFormat.ClassFighterMageThief,
              PorFormat.ClassMageThief },
        [PorFormat.RaceDwarf] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
        [PorFormat.RaceGnome] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
        [PorFormat.RaceHalfling] = new[]
            { PorFormat.ClassFighter, PorFormat.ClassThief, PorFormat.ClassFighterThief },
    };
    foreach (var (race, classes) in expectedLegal)
        Check($"{PorFormat.RaceName(race)} class options",
              string.Join(",", ClassTables.LegalClasses(race).OrderBy(c => c)),
              string.Join(",", classes.OrderBy(c => c)));
    Check("Humans are single-class only",
          ClassTables.LegalClasses(PorFormat.RaceHuman).All(c => ClassTables.SingleClassesOf(c).Length == 1), true);
    Check("Only the half-elf can be a Cleric/Fighter/Mage",
          PorFormat.Races.Select((_, r) => r).Count(r => ClassTables.IsLegal(r, PorFormat.ClassClericFighterMage)), 1);
    Check("The engine's non-PoR classes are not playable",
          new[] { 1, 3, 4, 7, 10, 17 }.Any(ClassTables.IsPlayableClass), false);
}

// The XP table drives what the training hall will honour, so the level it implies has to line up.
Check("Altharion's XP supports his level 5",
      ClassTables.LevelForXp(PorFormat.ClassFighter, altharion.Experience), 5);
Check("...and one more XP short of 5 would be level 4",
      ClassTables.LevelForXp(PorFormat.ClassFighter, ClassTables.XpForLevel(PorFormat.ClassFighter, 5) - 1), 4);
Check("Level 1 costs nothing", ClassTables.XpForLevel(PorFormat.ClassFighter, 1), 0L);
Check("A cleric cannot reach level 7", ClassTables.XpForLevel(PorFormat.ClassCleric, 7), -1L);
Check("XP never buys past the training cap",
      ClassTables.BaseClasses.All(c => ClassTables.LevelForXp(c, 10_000_000) == ClassTables.TrainingCap(c)), true);

// --- changing a character's class ---------------------------------------------
{
    Check("Class-change ranges are ordered, non-overlapping and inside the record", Enumerable
        .Range(0, ClassChange.WrittenRanges.Length)
        .All(i =>
        {
            var (off, len) = ClassChange.WrittenRanges[i];
            bool ok = off >= 0 && len > 0 && off + len <= PorFormat.RecordSize;
            if (i > 0)
            {
                var (prevOff, prevLen) = ClassChange.WrittenRanges[i - 1];
                ok &= prevOff + prevLen <= off;
            }
            return ok;
        }), true);

    // The level-5 fighter becomes a level-5 magic-user: keeps his level, his hit points and his
    // experience, and takes the magic-user's THAC0, saves, spell book and spells a day.
    var mage = altharion.Clone();
    var toMage = ClassChange.Plan(mage, PorFormat.ClassMage);
    Check("Class change keeps the level", toMage.Level, 5);
    Check("...in the new class's level byte", toMage.ClassLevels[PorFormat.ClassMage], 5);
    Check("...and clears the old one", toMage.ClassLevels[PorFormat.ClassFighter], 0);
    Check("A level-5 mage's THAC0 base", toMage.Thac0Base, 21);
    // Altharion's stored current THAC0 is 4 better than his base (Strength 18/89 and a magic
    // weapon); a class change keeps that equipment credit rather than discarding it.
    Check("...and its current keeps the equipment credit", toMage.Thac0, 21 - (altharion.Thac0Base - altharion.Thac0));
    Check("A level-5 mage's saves", string.Join("/", toMage.Saves), "14/13/11/15/12");
    Check("A level-5 mage's spells a day", string.Join("/", toMage.MageSlots), "4/2/1");
    Check("...and no cleric spells", toMage.ClericSlots.Sum(), 0);
    Check("A level-5 mage knows every spell it can cast",
          toMage.KnownSpells.Count(k => k),
          SpellBook.InRecordOrder.Count(s => s.School == "Mage" && s.Level <= 3));
    Check("...and no cleric spells at all",
          Enumerable.Range(0, PorFormat.KnownSpellsLen)
              .Any(i => toMage.KnownSpells[i] && SpellBook.InRecordOrder[i].School == "Cleric"), false);
    Check("A human magic-user raises no warnings", toMage.Warnings.Count, 0);

    ClassChange.Apply(mage, toMage);
    Check("Applied class", mage.ClassName, "Mage");
    // The class bitmask has to follow the class byte, or the record still says "fighter" in the
    // place the engine may well be reading. A fighter's 0x08 must become a magic-user's 0x01.
    Check("Applied class bitmask", mage.Bytes[PorFormat.OffClassMask], (byte)0x01);
    Check("...and it no longer carries the fighter bit",
          (mage.Bytes[PorFormat.OffClassMask] & 0x08) != 0, false);
    Check("Applied level byte", mage.Bytes[PorFormat.OffLevelHighest], (byte)5);
    Check("Applied THAC0 base", mage.Thac0Base, 21);
    Check("Applied saves",
          Enumerable.Range(0, PorFormat.SavesLen).Select(mage.GetSave).SequenceEqual(toMage.Saves), true);
    Check("Applied spells a day",
          $"{mage.Bytes[PorFormat.OffMageSlots]}/{mage.Bytes[PorFormat.OffMageSlots + 1]}/{mage.Bytes[PorFormat.OffMageSlots + 2]}",
          "4/2/1");
    // The chosen policy: the character keeps what it earned.
    Check("Hit points are kept", $"{mage.HpCurrent}/{mage.HpMax}", $"{altharion.HpCurrent}/{altharion.HpMax}");
    Check("Experience is kept", mage.Experience, altharion.Experience);
    Check("Abilities are kept",
          Enumerable.Range(0, PorFormat.StatCount).All(i => mage.GetStat(i) == altharion.GetStat(i)), true);
    Check("Armor Class is kept (it comes from Dexterity and armour, not class)",
          mage.ArmorClass, altharion.ArmorClass);
    Check("Money is kept", mage.Platinum == altharion.Platinum && mage.Gems == altharion.Gems, true);
    Check("Carried items are kept", mage.Bytes[PorFormat.OffNumberOfItems], altharion.Bytes[PorFormat.OffNumberOfItems]);
    Check("The item-list pointer is kept",
          mage.Bytes.Skip(PorFormat.OffItemsPtr).Take(4).SequenceEqual(altharion.Bytes.Skip(PorFormat.OffItemsPtr).Take(4)), true);
    Check("Nothing changes outside the declared ranges", Enumerable
        .Range(0, PorFormat.RecordSize)
        .All(o => mage.Bytes[o] == altharion.Bytes[o] ||
                  ClassChange.WrittenRanges.Any(r => o >= r.Offset && o < r.Offset + r.Length)), true);
    Check("A class-changed record still passes the signature", CharacterSignature.Looks(mage.Bytes, 0), true);

    // Changing back has to land on the real record's own numbers — which is the strongest statement
    // the per-level tables can make: the round trip is measured against a record from a real save.
    var back = mage.Clone();
    ClassChange.Apply(back, ClassChange.Plan(back, PorFormat.ClassFighter));
    Check("Changing back restores the fighter's THAC0 base", back.Thac0Base, altharion.Thac0Base);
    Check("...its current THAC0", back.Thac0, altharion.Thac0);
    Check("...its saving throws",
          Enumerable.Range(0, PorFormat.SavesLen).All(i => back.GetSave(i) == altharion.GetSave(i)), true);
    Check("...and its class level", back.GetClassLevel(PorFormat.ClassFighter), 5);

    // Multiclass: the dwarf fighter picks up a thief level and the skills that come with it.
    var rogue = thrender.Clone();
    var toFighterThief = ClassChange.Plan(rogue, PorFormat.ClassFighterThief);
    Check("A Fighter/Thief holds a level in each",
          $"{toFighterThief.ClassLevels[PorFormat.ClassFighter]}/{toFighterThief.ClassLevels[PorFormat.ClassThief]}", "1/1");
    Check("...saves as its best class",
          toFighterThief.Saves.SequenceEqual(ClassTables.SavesFor(
              new[] { PorFormat.ClassFighter, PorFormat.ClassThief }, new[] { 1, 1 })), true);
    Check("...and gains thief skills with its dwarf and Dexterity adjustments",
          toFighterThief.ThiefSkills.SequenceEqual(
              ClassTables.ThiefSkills(1, PorFormat.RaceDwarf, thrender.Dexterity)), true);
    Check("...and carries both classes' bits", toFighterThief.ClassMask, 0x08 | 0x04);
    ClassChange.Apply(rogue, toFighterThief);
    Check("Applied thief skills",
          Enumerable.Range(0, PorFormat.ThiefSkillsLen).Select(rogue.GetThiefSkill)
              .SequenceEqual(toFighterThief.ThiefSkills), true);
    Check("Applied class bitmask for a multiclass", rogue.Bytes[PorFormat.OffClassMask], (byte)0x0C);
    // A Cleric/Fighter/Mage must come out reading exactly what the real one in the sample party does.
    var trinity = thrender.Clone();
    trinity.Race = PorFormat.RaceHalfElf;
    ClassChange.Apply(trinity, ClassChange.Plan(trinity, PorFormat.ClassClericFighterMage));
    Check("A made Cleric/Fighter/Mage's mask matches the real Bakshi's",
          trinity.Bytes[PorFormat.OffClassMask], bakshi.Bytes[PorFormat.OffClassMask]);
    Check("...and it holds a level in each of the three",
          $"{trinity.GetClassLevel(PorFormat.ClassCleric)}{trinity.GetClassLevel(PorFormat.ClassFighter)}{trinity.GetClassLevel(PorFormat.ClassMage)}",
          "111");
    Check("...casting as both a cleric and a magic-user",
          trinity.Bytes[PorFormat.OffClericSlots] > 0 && trinity.Bytes[PorFormat.OffMageSlots] > 0, true);
    // Thrender is lawful good, and a thief cannot be — so the one warning here is that, not the
    // race/class pairing, which is legal for a dwarf.
    Check("The only complaint about a dwarf Fighter/Thief is his alignment",
          string.Join(" | ", toFighterThief.Warnings.Select(w => w.Contains("lawful good") ? "alignment" : w)),
          "alignment");
    var neutralDwarf = thrender.Clone();
    neutralDwarf.Alignment = 3;   // neutral good
    Check("...and a neutral-good dwarf Fighter/Thief raises none",
          ClassChange.Plan(neutralDwarf, PorFormat.ClassFighterThief).Warnings.Count, 0);

    // Leaving a caster class has to take the spell book with it.
    var exMage = mage.Clone();
    exMage.Bytes[PorFormat.OffMemorizedSpells] = 21;          // something memorized from the old class
    ClassChange.Apply(exMage, ClassChange.Plan(exMage, PorFormat.ClassFighter));
    Check("A former mage knows no spells", Enumerable
        .Range(0, PorFormat.KnownSpellsLen).All(i => exMage.Bytes[PorFormat.OffKnownSpells + i] == 0), true);
    Check("...has no spells a day",
          exMage.Bytes[PorFormat.OffMageSlots] + exMage.Bytes[PorFormat.OffClericSlots], 0);
    Check("...and has nothing memorized", Enumerable
        .Range(0, PorFormat.MemorizedSpellsLen).All(i => exMage.Bytes[PorFormat.OffMemorizedSpells + i] == 0), true);
    Check("...and no thief skills", Enumerable
        .Range(0, PorFormat.ThiefSkillsLen).All(i => exMage.GetThiefSkill(i) == 0), true);

    // A cleric prays for its spells rather than learning them, so it knows the lot up to its level.
    var priest = altharion.Clone();
    var toCleric = ClassChange.Plan(priest, PorFormat.ClassCleric);
    Check("A level-5 cleric knows every cleric spell it can cast",
          toCleric.KnownSpells.Count(k => k),
          SpellBook.InRecordOrder.Count(s => s.School == "Cleric" && s.Level <= 3));
    // Class table 2/2/1 at level 5, plus the Wisdom bonus on the first-level slots.
    Check("...and its spells a day come from the class table plus Wisdom",
          string.Join("/", toCleric.ClericSlots),
          string.Join("/", ClassTables.ClericSlots(5, altharion.Wisdom)));

    // The warnings: an illegal race/class, a capped level, an ability below the class minimum.
    var dwarfMage = thrender.Clone();
    Check("A dwarven magic-user is refused by the race table",
          ClassChange.Plan(dwarfMage, PorFormat.ClassMage).Warnings.Any(w => w.Contains("cannot be")), true);

    var elfHero = altharion.Clone();
    elfHero.Race = PorFormat.RaceElf;
    elfHero.SetClassLevel(PorFormat.ClassFighter, 8);
    var elfPlan = ClassChange.Plan(elfHero, PorFormat.ClassFighter);
    Check("An elf fighter is capped at 7", elfPlan.Level, 7);
    Check("...and the plan says so", elfPlan.Warnings.Any(w => w.Contains("capped")), true);

    var dullard = altharion.Clone();
    dullard.Intelligence = 8;
    Check("An Intelligence of 8 is below what a magic-user needs",
          ClassChange.Plan(dullard, PorFormat.ClassMage).Warnings.Any(w => w.Contains("Intelligence")), true);

    var pauper = altharion.Clone();
    pauper.Experience = 100;
    Check("Experience short of the level is flagged",
          ClassChange.Plan(pauper, PorFormat.ClassFighter).Warnings.Any(w => w.Contains("short of")), true);

    var goodThief = altharion.Clone();
    goodThief.Alignment = 0;   // lawful good
    Check("A lawful-good thief is flagged",
          ClassChange.Plan(goodThief, PorFormat.ClassThief).Warnings.Any(w => w.Contains("lawful good")), true);

    // Re-applying the same class is a repair, not a change: it recomputes the derived numbers and
    // leaves the class byte where it was.
    var repaired = altharion.Clone();
    repaired.SetSave(0, 3); repaired.Thac0Base = 5;            // numbers that don't match the class
    var repair = ClassChange.Plan(repaired, PorFormat.ClassFighter);
    Check("Re-picking the same class reads as a repair", repair.IsSameClass, true);
    ClassChange.Apply(repaired, repair);
    Check("...and it puts the class's own numbers back",
          repaired.Thac0Base == altharion.Thac0Base && repaired.GetSave(0) == altharion.GetSave(0), true);

    bool refusedPaladin;
    try { ClassChange.Plan(altharion.Clone(), 3); refusedPaladin = false; }   // paladin — not in this game
    catch (ArgumentOutOfRangeException) { refusedPaladin = true; }
    Check("An unplayable class byte is refused", refusedPaladin, true);
}

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

/// <summary>An <see cref="ICharacterHost"/> that is never attached, so a view-model built on it
/// edits its record buffer and writes nowhere. Enough to exercise the predicates the automatic
/// combat passes gate on without a running game.</summary>
file sealed class OfflineHost : ICharacterHost
{
    public bool IsAttached => false;
    public bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length) => false;
}
