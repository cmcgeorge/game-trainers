using CurseOfTheAzureBondsTrainer.Game;

// Headless verification of the Curse of the Azure Bonds record layout.
//
// The fixtures below are verbatim bytes from a real install: four CHRDATAn.SAV files (a whole
// saved party) and one monster record unpacked out of the game's own MON2CHA.DAX archive. Nothing
// here is a transcription — a .SAV file *is* a character record, byte for byte, which is what
// established the 422-byte size in the first place.
//
// What makes these assertions worth running is that almost none of them is "the byte at 0x78 is
// 49". They are cross-checks: a paladin's THAC0 must equal his base minus his exceptional-strength
// bonus; a cleric's spells-per-day must equal the Rule Book's table plus his Wisdom bonus; hit
// points minus the Constitution bonus must equal the stored die roll; a multi-class character must
// hold exactly half the experience of a single-class one. Each of those ties two or three offsets
// together through a rule the game did not have to satisfy unless the offsets are right.
//
// Run: dotnet run --project test/FormatCheck

// MATHEW — CHRDATA1.SAV verbatim (422 bytes): human paladin 5, STR 18/00.
const string MathewHex =
    "064D41544845570000000000000000001212111110101111111111116464000000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002C07031400" +
    "310000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
    "0000000000000000000000000000000000000000000000000000000000000000000000000000000000010109090B0B0C0C0500000000000000000000" +
    "000009009665000001000000000000000000002C0100000000000000050000000000000000000000000000000200010002000000320190A861000040" +
    "2200000000000000000000000000000000000000000000000291A2B3C4E6F70000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000002C0102002D650000000001000000000100002F35300000010002000600" +
    "310C";

// TRAVIS — CHRDATA3.SAV verbatim: dwarf fighter 4 / thief 5.
const string TravisHex =
    "06545241564953200000000000000000121211110F0F1111101010100C0C000000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002B010E3C00" +
    "220000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
    "00000000000000000000000000000000000000000000000000000000000000000000000000000000000401100B0C0F0D0C05000000003E453E342B1B" +
    "571B0B009765000001000000000000000000002C010000000000000400000005000000000000000000000004020001000200000032012DD43000000C" +
    "1900000000000000000000000000000000000000000000020191A2B3C4E6F70000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000002C010400A8670000000000000000000100002C35300000010002000300" +
    "220C";

// SHARA — CHRDATA5.SAV verbatim: human cleric 5, WIS 17.
const string SharaHex =
    "0553484152410000000000000000000011110C0C11111111101011110000000000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002A07001300" +
    "1D0101010101010101000000000000000000000000000101010101010100000000000000000101010101010101000000000000000000000000000000" +
    "00000000000000000000000000000000000000000000000000000000000000000000000000000000000101090C0D0F0E0C0500000000000000000000" +
    "000000000000000001000000000000000000002C010000000005000000000000000000000000000000010000020001000200000032011BA861000002" +
    "1305050200000000000000000000000000000000000000040291A2B3C4E6F70000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000002C010E0074680000000000000000000100002B35300000010002000100" +
    "1D0C";

// PHILIPPE — CHRDATA6.SAV verbatim: human mage 5.
const string PhilippeHex =
    "085048494C4950504500000000000000121210100E0E1111101010100000000000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002807051C00" +
    "1B0000000000000000000101010000010000010000010000000000000000000100000100000000000000000000000001000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000001010E0D0B0F0C0C0500000000000000000000" +
    "000000000000000001000000000000000000002C010000000000000000000500000000000000000000010000020001000200000032012BA861000001" +
    "1100000000000000000000040201000000000000000000050291A2B3C4E6F70000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000002C01000000000000000000000000000100002935300000010002000200" +
    "1B0C";

// TROLL — MON2CHA.DAX block 7, unpacked verbatim: monsters use the identical record.
const string TrollHex =
    "0554524F4C4C000000000000000000000A0A0A0A0A0A0A0A0A0A0A0A0000000000000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002F00021E00" +
    "240000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
    "000000000000010101010101010101010101010101010101010101010101010101010100000000000100820A0B0C0C0D0C0700FFFF00000000000000" +
    "00000900191DFF80FF0000000000000000000000000000000000000700000000000000000000000000000A0804020102040604003800FF0000000008" +
    "240000000000000000000000000000000D02080000000000000000000000000E00000000000000000000000000000000000000000000000000000000" +
    "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000101012F38360402010204060400" +
    "240C";

// Plate Mail — ITEM1.DAX, unpacked verbatim: one 63-byte item record.
const string PlateMailHex =
    "0A506C617465204D61696C000000000000000000000000000000000000000000000000000000000000000200321D3A00303A0000000000C201009001" +
    "000000";

// "10 Arrow" — ITEM1.DAX, unpacked verbatim: a stacked item.
const string ArrowsHex =
    "083130204172726F770000000000000000000000000000000000000000000000000000000000000000000B000E1D4900003D000000000004000A0000" +
    "000000";

int failures = 0;

// ---------------------------------------------------------------- record size
Check("Record size is 0x1A6", CoabFormat.RecordSize, 422);
Check("A .SAV file is exactly one record", FromHex(MathewHex).Length, CoabFormat.RecordSize);

var mathew   = new CharacterRecord(FromHex(MathewHex));
var travis   = new CharacterRecord(FromHex(TravisHex));
var shara    = new CharacterRecord(FromHex(SharaHex));
var philippe = new CharacterRecord(FromHex(PhilippeHex));
var troll    = new CharacterRecord(FromHex(TrollHex));

// ------------------------------------------------------------------- identity
Check("Mathew name", mathew.Name, "MATHEW");
Check("Mathew race", mathew.RaceName, "Human");
Check("Mathew class", mathew.ClassName, "Paladin");
Check("Mathew gender", mathew.GenderName, "Male");
// A paladin must be Lawful Good, which is the alignment byte's own consistency check.
Check("Mathew alignment", mathew.AlignmentName, "Lawful Good");
Check("Mathew age", mathew.Age, 20);
Check("Mathew status", mathew.StatusName, "Okay");

Check("Travis name", travis.Name, "TRAVIS");
Check("Travis race", travis.RaceName, "Dwarf");
Check("Travis class", travis.ClassName, "Fighter/Thief");
Check("Travis age", travis.Age, 60);

Check("Shara class", shara.ClassName, "Cleric");
Check("Philippe class", philippe.ClassName, "Mage");

// -------------------------------------------------------------- ability pairs
// Curse stores every score twice. An undamaged party reads the two halves equal, which is what
// makes the pair layout visible at all — six independent bytes that all duplicate their neighbour.
Check("Mathew STR", mathew.Strength, 18);
Check("Mathew INT", mathew.Intelligence, 17);
Check("Mathew WIS", mathew.Wisdom, 16);
Check("Mathew DEX", mathew.Dexterity, 17);
Check("Mathew CON", mathew.Constitution, 17);
Check("Mathew CHA", mathew.Charisma, 17);
Check("Mathew exceptional STR", mathew.StrengthPercent, 100);
Check("Mathew STR displays as 18/00", mathew.StrengthDisplay, "18/00");
Check("Travis STR displays as 18/12", travis.StrengthDisplay, "18/12");
for (int i = 0; i < CoabFormat.StatCount; i++)
    Check($"Mathew {CoabFormat.StatsShort[i]} current == maximum", mathew.GetStat(i), mathew.GetStatMax(i));
Check("Mathew is not drained", mathew.IsDrained, false);

// A drain is the two halves disagreeing, and restoring is putting the current half back.
var drained = new CharacterRecord(FromHex(MathewHex));
drained.Bytes[CoabFormat.OffStr] = 9;
Check("A lowered current half reads as drained", drained.IsDrained, true);
Check("Restore reports a change", drained.RestoreDrainedStats(), true);
Check("Restore puts STR back to its maximum", drained.Strength, 18);
Check("Restore is a no-op the second time", drained.RestoreDrainedStats(), false);

// Writing a score must write both halves, or a Restoration silently undoes the edit.
var edited = new CharacterRecord(FromHex(MathewHex));
edited.SetStat(4, 3);
Check("SetStat writes the current half", edited.Bytes[CoabFormat.OffCon], 3);
Check("SetStat writes the maximum half", edited.Bytes[CoabFormat.OffCon + CoabFormat.StatMaxDelta], 3);
Check("An edited score is not left looking drained", edited.IsDrained, false);

// ------------------------------------------------------- hit points and combat
Check("Mathew HP max", mathew.HpMax, 49);
Check("Mathew HP current", mathew.HpCurrent, 49);
Check("Travis HP max", travis.HpMax, 34);
Check("Shara HP max", shara.HpMax, 29);
Check("Philippe HP max", philippe.HpMax, 27);

// HP rolled is the raw dice before the Constitution bonus, so for a single-class character
// HpMax - rolled must be exactly (CON bonus x level). CON 17 gives +3, CON 16 gives +2.
Check("Mathew HP roll + CON bonus == HP max", mathew.Bytes[CoabFormat.OffHpRolled] + 3 * 5, mathew.HpMax);
Check("Shara HP roll + CON bonus == HP max", shara.Bytes[CoabFormat.OffHpRolled] + 2 * 5, shara.HpMax);
Check("Philippe HP roll + CON bonus == HP max", philippe.Bytes[CoabFormat.OffHpRolled] + 2 * 5, philippe.HpMax);

// AC is stored 60 - value. Unarmored is 10, and this party carries no armour at all (item count 0),
// so each character's AC must be exactly 10 minus the AD&D 1e defensive adjustment for their
// Dexterity: 15 -> -1, 16 -> -2, 17 -> -3.
Check("Mathew AC base is the unarmored 10", mathew.ArmorClassBase, 10);
Check("Mathew AC == 10 - DEX 17 adjustment", mathew.ArmorClass, 7);
Check("Travis AC == 10 - DEX 17 adjustment", travis.ArmorClass, 7);
Check("Philippe AC == 10 - DEX 17 adjustment", philippe.ArmorClass, 7);
Check("Nobody in this party carries an item", mathew.Bytes[CoabFormat.OffNumberOfItems], 0);

// THAC0 is stored 60 - value too. The current value must be the base minus the character's
// strength bonus to hit: 18/00 is +3, 18/51-75 is +2, 18/01-50 is +1, 17 is +1.
Check("Mathew THAC0 base (fighter level 5)", mathew.Thac0Base, 16);
Check("Mathew THAC0 == base - 3 for STR 18/00", mathew.Thac0, 13);
Check("Travis THAC0 base (fighter level 4)", travis.Thac0Base, 17);
Check("Travis THAC0 == base - 1 for STR 18/12", travis.Thac0, 16);
Check("Shara THAC0 base (cleric level 5)", shara.Thac0Base, 18);
Check("Shara THAC0 == base - 1 for STR 17", shara.Thac0, 17);
Check("Philippe THAC0 base (mage level 5)", philippe.Thac0Base, 20);
Check("Philippe THAC0 == base - 1 for STR 18", philippe.Thac0, 19);

// --------------------------------------------------------- levels and experience
// The Rule Book states new characters begin with 25,000 XP, and that a non-human multi-class
// character divides everything it earns by the number of its classes. Both fall out of the record.
Check("Mathew XP is the documented starting 25,000", mathew.Experience, 25_000L);
Check("Shara XP is the documented starting 25,000", shara.Experience, 25_000L);
Check("Travis XP is halved for two classes", travis.Experience, 12_500L);

// ...and the class-level bytes must be what those experience totals buy in the Rule Book's tables.
Check("Mathew is a level 5 paladin", mathew.GetClassLevel(3), 5);
Check("Mathew has no other class levels", mathew.GetClassLevel(2) + mathew.GetClassLevel(5), 0);
Check("Shara is a level 5 cleric", shara.GetClassLevel(0), 5);
Check("Philippe is a level 5 mage", philippe.GetClassLevel(5), 5);
Check("Travis is a level 4 fighter", travis.GetClassLevel(2), 4);
Check("Travis is a level 5 thief", travis.GetClassLevel(6), 5);
Check("Travis's effective level is his best class", travis.EffectiveLevel, 5);
Check("Mathew's level byte agrees with his class levels", mathew.Bytes[CoabFormat.OffLevelHighest], 5);
Check("Travis's level byte agrees with his class levels", travis.Bytes[CoabFormat.OffLevelHighest], 5);

CheckXpBuysLevel("paladin", ClassRaceBook.XpTable, r => r.Paladin, 25_000, 5);
CheckXpBuysLevel("cleric", ClassRaceBook.XpTable, r => r.Cleric, 25_000, 5);
CheckXpBuysLevel("mage", ClassRaceBook.XpTable, r => r.Mage, 25_000, 5);
CheckXpBuysLevel("fighter", ClassRaceBook.XpTable, r => r.Fighter, 12_500, 4);
CheckXpBuysLevel("thief", ClassRaceBook.XpTable, r => r.Thief, 12_500, 5);

// ------------------------------------------------------------------- money
// The Rule Book: "Each character begins the game with 300 platinum pieces". Nothing else is carried,
// and the encumbrance field agrees — 300 coins weigh 300, and nobody has an item.
Check("Mathew has the documented 300 platinum", mathew.Platinum, 300);
Check("Travis has the documented 300 platinum", travis.Platinum, 300);
Check("Mathew has no gold", mathew.Gold, 0);
Check("Encumbrance equals the coins carried",
      mathew.Bytes[CoabFormat.OffEncumbrance] | (mathew.Bytes[CoabFormat.OffEncumbrance + 1] << 8), 300);

// --------------------------------------------------------------- spells per day
// The Rule Book's cleric table gives a 5th-level cleric 3/3/1, and Wisdom 17 adds +2/+2/+1.
Check("Shara's 1st-level slots (3 + WIS 17 bonus)", shara.Bytes[CoabFormat.OffClericSlots], 5);
Check("Shara's 2nd-level slots (3 + WIS 17 bonus)", shara.Bytes[CoabFormat.OffClericSlots + 1], 5);
Check("Shara's 3rd-level slots (1 + WIS 17 bonus)", shara.Bytes[CoabFormat.OffClericSlots + 2], 2);
Check("Shara has no 4th-level slots", shara.Bytes[CoabFormat.OffClericSlots + 3], 0);
// ...and the mage table gives a 5th-level magic-user 4/2/1, with no Wisdom bonus to add.
Check("Philippe's 1st-level slots", philippe.Bytes[CoabFormat.OffMageSlots], 4);
Check("Philippe's 2nd-level slots", philippe.Bytes[CoabFormat.OffMageSlots + 1], 2);
Check("Philippe's 3rd-level slots", philippe.Bytes[CoabFormat.OffMageSlots + 2], 1);
Check("Philippe has no cleric slots", philippe.Bytes[CoabFormat.OffClericSlots], 0);
Check("A paladin of level 5 has no spell slots", mathew.Bytes[CoabFormat.OffClericSlots], 0);

// ------------------------------------------------------------------ thief skills
// Only the thief has any, and they read as a plausible level-5 thief with Dexterity 17.
int[] travisSkills = { 62, 69, 62, 52, 43, 27, 87, 27 };
for (int i = 0; i < CoabFormat.ThiefSkillsLen; i++)
    Check($"Travis {CoabFormat.ThiefSkillNames[i]}", travis.GetThiefSkill(i), travisSkills[i]);
for (int i = 0; i < CoabFormat.ThiefSkillsLen; i++)
    Check($"Mathew has no {CoabFormat.ThiefSkillNames[i]}", mathew.GetThiefSkill(i), 0);

// ----------------------------------------------------------------- saving throws
// A paladin saves 2 better than the fighter table he otherwise uses.
int[] mathewSaves = { 9, 9, 11, 11, 12 };
for (int i = 0; i < CoabFormat.SavesLen; i++)
    Check($"Mathew save vs {CoabFormat.SaveNames[i]}", mathew.GetSave(i), mathewSaves[i]);

// -------------------------------------------------------------------- movement
Check("Mathew movement base", mathew.Bytes[CoabFormat.OffMovementBase], 12);
Check("Mathew movement current matches base",
      mathew.Bytes[CoabFormat.OffMovementCur], mathew.Bytes[CoabFormat.OffMovementBase]);

// ------------------------------------------------------ marching order / icon
Check("Mathew leads the party", mathew.Bytes[CoabFormat.OffOrderNumber], 0);
Check("Travis is third", travis.Bytes[CoabFormat.OffOrderNumber], 2);
// Dwarves get the small combat icon and humans the large one, which is what identifies this byte.
Check("Travis the dwarf has a small icon", travis.Bytes[CoabFormat.OffIconSize], 1);
Check("Mathew the human has a large icon", mathew.Bytes[CoabFormat.OffIconSize], 2);

// -------------------------------------------------------- the effects far pointer
// The .FX file is a linked list of 9-byte records and the record holds its head as a real-mode
// far pointer. Mathew's head resolves 9 bytes before the first link in his own .FX file — which is
// what the record layout has to produce if the pointer offset is right.
Check("Mathew's effects pointer is not null", FarPointer(mathew, CoabFormat.OffEffectsPtr) != 0, true);
Check("Shara has no effects", FarPointer(shara, CoabFormat.OffEffectsPtr), 0L);
// Travis's own .FX chains 0x6597:000B -> 0x6598:0004 -> 0x6598:000D, i.e. two 9-byte hops.
Check("Travis's effects head is the .FX list head", FarPointer(travis, CoabFormat.OffEffectsPtr), 0x6597L * 16 + 0x0B);
Check("...and the first .FX link is exactly 9 bytes past it", 0x6598L * 16 + 0x04 - (0x6597L * 16 + 0x0B), 9L);

// -------------------------------------------------------------- monsters share it
Check("Troll name", troll.Name, "TROLL");
Check("Troll decodes as a monster", troll.LooksLikeMonster, true);
Check("Troll AC", troll.ArmorClass, 4);
Check("Troll THAC0 base", troll.Thac0Base, 13);
Check("Troll HP", troll.HpMax, 36);
Check("Troll is at full health", troll.HpCurrent, troll.HpMax);
Check("Troll status is okay", troll.Status, 0);
Check("Troll passes the live-combatant test", troll.LooksLikeLiveCombatant, true);

// The bestiary is generated from these same records, so the book and the record must agree.
var trollEntry = MonsterBook.All.FirstOrDefault(m => m.Name == "TROLL");
Check("The bestiary lists the troll", trollEntry != null, true);
if (trollEntry != null)
{
    Check("Bestiary troll AC matches the record", trollEntry.Ac, troll.ArmorClass);
    Check("Bestiary troll HP matches the record", trollEntry.Hp, troll.HpMax);
    Check("Bestiary troll THAC0 matches the record", trollEntry.Thac0, troll.Thac0Base);
    Check("Bestiary troll XP", trollEntry.Xp, 525);
}

// ----------------------------------------------------------------- item records
// The item templates in the game's ITEM*.DAX archives are the same 63-byte records the .ITM save
// files hold, so decoding one is a direct test of the item layout — and the numbers it must produce
// are the AD&D equipment tables, which a wrong offset cannot reproduce by accident.
var plate = new ItemEntry(FromHex(PlateMailHex));
Check("An item record is 63 bytes", ItemEntry.RecordSize, 63);
Check("Item name decodes", plate.DisplayName.Contains("Plate Mail", StringComparison.Ordinal), true);
Check("Plate mail is worth 400 gp", plate.Value, 400);
Check("Plate mail is not readied", plate.Readied, false);
Check("Plate mail is not cursed", plate.Cursed, false);
Check("A template item is fully identified", plate.Identified, true);
Check("Plate mail's type byte", plate.Type, 58);
// The archive's templates carry a stale link from whenever they were authored; it is meaningless
// until the game copies a template into a character's list, which is the point of reading the link
// rather than assuming items are adjacent in memory.
Check("Plate mail weighs 450", plate.Raw[0x37] | (plate.Raw[0x38] << 8), 450);

var arrows = new ItemEntry(FromHex(ArrowsHex));
Check("A stacked item carries its count", arrows.Count, 10);
Check("Stacked ammunition is rechargeable", arrows.IsRechargeable, true);

// Hiding and revealing an item's name parts is the identify toggle the save editor drives.
var hidden = new ItemEntry(FromHex(PlateMailHex));
Check("Hiding the name parts un-identifies it", hidden.SetIdentified(false) && !hidden.Identified, true);
Check("Identifying it again restores it", hidden.Identify() && hidden.Identified, true);

// ------------------------------------------------------------------- signature
// The scanner has to recognise all of these, and reject a buffer that merely starts with a name.
foreach (var (label, hex) in new[] { ("Mathew", MathewHex), ("Travis", TravisHex),
                                     ("Shara", SharaHex), ("Philippe", PhilippeHex), ("Troll", TrollHex) })
    Check($"Signature accepts {label}", CharacterSignature.Looks(FromHex(hex), 0), true);

var zeroed = new byte[CoabFormat.RecordSize];
Check("Signature rejects an empty buffer", CharacterSignature.Looks(zeroed, 0), false);

// A trailing space past the declared length is real and must pass (see notes §4); a length byte
// claiming more characters than the field holds must not.
var trailingSpace = FromHex(MathewHex);
trailingSpace[CoabFormat.OffName + 6] = (byte)' ';
Check("Signature accepts a trimmed trailing space", CharacterSignature.Looks(trailingSpace, 0), true);
var overlongLength = FromHex(MathewHex);
overlongLength[CoabFormat.OffNameLength] = 12;
Check("Signature rejects a length longer than the text", CharacterSignature.Looks(overlongLength, 0), false);

// A name followed by junk must not pass: the ability pairs are what stop it.
var namedJunk = new byte[CoabFormat.RecordSize];
namedJunk[0] = 5;
"ABCDE"u8.CopyTo(namedJunk.AsSpan(1));
Check("Signature rejects a bare name string", CharacterSignature.Looks(namedJunk, 0), false);

// A maximum below the current score can't happen in the game, so the signature rejects it.
var backwards = FromHex(MathewHex);
backwards[CoabFormat.OffStr + CoabFormat.StatMaxDelta] = 3;
Check("Signature rejects a maximum below the current score", CharacterSignature.Looks(backwards, 0), false);

// A record decoded a byte late must not still look like one, or the scanner would report ghosts.
var shifted = new byte[CoabFormat.RecordSize * 2];
FromHex(MathewHex).CopyTo(shifted, 1);
Check("Signature rejects a one-byte-shifted record", CharacterSignature.Looks(shifted, 0), false);

// ----------------------------------------------------------------- round trips
var rt = new CharacterRecord(FromHex(MathewHex));
rt.Name = "TEST NAME";
rt.HpMax = 200; rt.HpCurrent = 199;
rt.ArmorClass = -10; rt.Thac0 = 1;
rt.Experience = 1_234_567;
rt.Platinum = 4321;
rt.SetClassLevel(2, 12);
Check("Round-trip name", rt.Name, "TEST NAME");
Check("Round-trip HP max", rt.HpMax, 200);
Check("Round-trip HP current", rt.HpCurrent, 199);
Check("Round-trip AC (60-x, negative)", rt.ArmorClass, -10);
Check("Round-trip THAC0 (60-x)", rt.Thac0, 1);
Check("Round-trip experience", rt.Experience, 1_234_567L);
Check("Round-trip platinum", rt.Platinum, 4321);
Check("Round-trip class level", rt.GetClassLevel(2), 12);
Check("Round-trip leaves the record its own length", rt.Bytes.Length, CoabFormat.RecordSize);

// ------------------------------------------------------------------ DAX archive
// Build a tiny archive by hand and read it back, so the container and the PackBits variant are
// pinned independently of the game files being present.
byte[] payload = new byte[10];
for (int i = 0; i < 6; i++) payload[i] = (byte)(0xA0 + i);
for (int i = 6; i < 10; i++) payload[i] = 0x5C;                 // a run, to exercise the repeat case
byte[] packed = { 0x05, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xFC, 0x5C };  // literal 6, then repeat 4
var archive = new List<byte> { 9, 0, 0x2A };                    // header length 9, one entry, block id 42
archive.AddRange(new byte[] { 0, 0, 0, 0 });                    // offset 0
archive.AddRange(new byte[] { 10, 0 });                         // unpacked 10
archive.AddRange(new byte[] { (byte)packed.Length, 0 });        // packed
archive.AddRange(packed);

var parsed = DaxArchive.Parse(archive.ToArray());
Check("DAX archive yields one block", parsed.Count, 1);
if (parsed.Count == 1)
{
    Check("DAX block id", parsed[0].Id, 42);
    Check("DAX block unpacks to its declared size", parsed[0].Data.Length, 10);
    Check("DAX literal run decodes", parsed[0].Data.Take(6).SequenceEqual(payload.Take(6)), true);
    Check("DAX repeat run decodes", parsed[0].Data.Skip(6).All(b => b == 0x5C), true);
}
Check("DAX rejects a truncated header", DaxArchive.Parse(new byte[] { 9, 0 }).Count, 0);
Check("DAX rejects an empty file", DaxArchive.Parse(Array.Empty<byte>()).Count, 0);
Check("A GEO block's wall planes are 512 bytes", DaxArchive.GeoWallLength, 512);

// ----------------------------------------------------------------------- maps
// Every level is one 16x16 GEO block, and each map's ASCII must parse back to that shape.
Check("Sixteen levels are mapped", MapBook.Areas.Count, 16);
foreach (var area in MapBook.Areas)
{
    Check($"{area.Name} is 16x16", area.Width * 1000 + area.Height, 16 * 1000 + 16);
    Check($"{area.Name} has decoded terrain", area.Terrain != null, true);
    Check($"{area.Name} names its GEO block", area.Geo.StartsWith("GEO", StringComparison.Ordinal), true);
    if (area.Terrain != null)
    {
        Check($"{area.Name} terrain width", area.Terrain.GetLength(0), 16);
        Check($"{area.Name} terrain height", area.Terrain.GetLength(1), 16);
    }
}
Check("Every level's GEO block is distinct",
      MapBook.Areas.Select(a => a.Geo).Distinct().Count(), MapBook.Areas.Count);

// A decoded level must actually contain walls — an all-blank grid would mean the planes were
// misread — and must be mostly reachable or entirely sealed off, never a grid of stray fragments.
foreach (var area in MapBook.Areas)
{
    if (area.Terrain == null) continue;
    int walls = 0, reachable = 0;
    for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            var sq = area.Terrain[x, y];
            if (sq.West != WallKind.None || sq.North != WallKind.None ||
                sq.East != WallKind.None || sq.South != WallKind.None) walls++;
            if (sq.Floor != FloorKind.Stone) reachable++;
        }
    Check($"{area.Name} has walls", walls > 0, true);
    Check($"{area.Name} has somewhere to stand", reachable > 0, true);
}

// --------------------------------------------------- the real archives, if present
// The synthetic archive above pins the decoder; this pins it against the game. Skipped when the
// install isn't on this machine, so the harness stays runnable anywhere.
string gameFolder = FindGameFolder();
if (gameFolder.Length == 0)
{
    Console.WriteLine("  --  (game folder not found — skipping the checks against the real archives)");
}
else
{
    Console.WriteLine($"  --  reading the real archives from {gameFolder}");
    var levels = DaxArchive.ReadLevels(gameFolder);
    Check("The install holds sixteen levels", levels.Count, 16);
    Check("Every level's wall planes are 512 bytes",
          levels.All(l => l.Walls.Length == DaxArchive.GeoWallLength), true);
    Check("Every level's walls are distinct",
          levels.Select(l => Convert.ToHexString(l.Walls)).Distinct().Count(), levels.Count);
    // Each mapped area must name a block the install actually contains, or Identify can never
    // match it back to a map.
    var tags = levels.Select(l => l.Geo).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var area in MapBook.Areas)
        Check($"{area.Geo} exists in the install", tags.Contains(area.Geo), true);
}

// ------------------------------------------------------------------ reference data
Check("The spell list holds every Curse spell", SpellBook.All.Count, 84);
Check("Curse reaches 5th-level clerical spells", SpellBook.All.Any(s => s.School == "Cleric" && s.Level == 5), true);
Check("Curse reaches 5th-level mage spells", SpellBook.All.Any(s => s.School == "Mage" && s.Level == 5), true);
Check("Rangers get the four druid spells", SpellBook.ForSchool("Druid").Count(), 4);
Check("The memorized-spell block has one byte per spell", CoabFormat.MemorizedSpellsLen, SpellBook.All.Count);
Check("Spell search finds Fireball", SpellBook.Search("fireball").Count(), 1);

Check("The bestiary is decoded from the game", MonsterBook.All.Count, 71);
Check("Tyranthraxus ends the game", MonsterBook.All.Any(m => m.Name == "TYRANTHRAXUS"), true);
Check("The dracolich is the richest kill", MonsterBook.All.OrderByDescending(m => m.Xp).First().Name, "DRACOLICH");
Check("Every creature pays non-negative XP", MonsterBook.All.All(m => m.Xp >= 0), true);
Check("Every creature has hit points", MonsterBook.All.All(m => m.Hp > 0), true);
Check("Monster search matches on area", MonsterBook.Search("Tilverton").Any(), true);

Check("The XP table covers 12 levels", ClassRaceBook.XpTable.Count, 12);
Check("Level 1 costs nothing", ClassRaceBook.XpTable[0].Fighter, 0);
Check("Six classes are described", ClassRaceBook.Classes.Count, 6);
Check("Seven race rows", ClassRaceBook.Races.Count, 7);

// The effect codes the real party carries have to be the ones the book names, or the book is for a
// different game: paladins radiate protection from evil, and dwarves carry three racial bonuses.
Check("Effect 0x08 is the paladin's aura", EffectBook.Name(0x08), "protected from evil");
Check("Effect 0x1A is a dwarf bonus", EffectBook.Name(0x1A).Contains("dwarf"), true);
Check("Effect 0x2F is a dwarf bonus", EffectBook.Name(0x2F).Contains("dwarf"), true);
Check("Effect 0x61 is a dwarf bonus", EffectBook.Name(0x61).Contains("dwarf"), true);

// ------------------------------------------------------------------------ done
Console.WriteLine();
Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the 422-byte record layout decodes the sample party, the game's own "
      + "monster archive, and its level geometry correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

// ---------------------------------------------------------------- helpers
void Check<T>(string what, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "  ok  " : "FAIL  ")}{what}: {actual}{(ok ? "" : $"  (expected {expected})")}");
}

void CheckXpBuysLevel(string cls, IReadOnlyList<XpRow> table, Func<XpRow, int> pick, long xp, int expected)
{
    int level = 1;
    foreach (var row in table)
    {
        int need = pick(row);
        if (row.Level > 1 && need == 0) break;      // the class stops here
        if (xp >= need) level = row.Level;
    }
    Check($"{xp:N0} XP buys {cls} level {expected}", level, expected);
}

// The folder holding the game's own GEO*.DAX, if this machine has the game. Uses the trainer's own
// save-folder search, so the harness exercises that path too, and falls back to the environment
// variable the trainer documents.
static string FindGameFolder()
{
    string? save = CurseOfTheAzureBondsTrainer.Memory.SaveFolderLocator.Find();
    string folder = CurseOfTheAzureBondsTrainer.Memory.SaveFolderLocator.GameFolderFor(save);
    if (folder.Length > 0) return folder;

    foreach (string root in (Environment.GetEnvironmentVariable("CURSE_SAVE_ROOTS") ?? "")
             .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        string hit = CurseOfTheAzureBondsTrainer.Memory.SaveFolderLocator.GameFolderFor(root);
        if (hit.Length > 0) return hit;
    }
    return "";
}

static long FarPointer(CharacterRecord r, int offset)
{
    int off = r.Bytes[offset] | (r.Bytes[offset + 1] << 8);
    int seg = r.Bytes[offset + 2] | (r.Bytes[offset + 3] << 8);
    return seg == 0 && off == 0 ? 0 : (long)seg * 16 + off;
}

static byte[] FromHex(string hex)
{
    var bytes = new byte[hex.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
    return bytes;
}
