using System.IO;
using DarkDesigns1Trainer.Game;

// Headless verification harness for the Dark Designs I character format.
// It builds a synthetic DDCHARS.DAT from the sample, asserts every decoded field,
// tests the record validation, the save-file round-trip, and the reference tables.
// Exits 0 on success, 1 on any failure.

using DarkDesigns1Trainer.Memory;
using DarkDesigns1Trainer.ViewModels;
using GameTrainers.Common.Memory;

int failures = 0;

// --- format constants --------------------------------------------------------
Console.WriteLine("Format constants:");
Check("record size", CharacterFormat.RecordSize, 72);
Check("max slots", CharacterFormat.MaxSlots, 15);
Check("header size", CharacterFormat.HeaderSize, 144);
Check("file size", CharacterFormat.FileSize, 1224);
Check("name length", CharacterFormat.NameLength, 12);
Check("attribute count", CharacterFormat.AttributeCount, 5);
Check("party size", CharacterFormat.PartySize, 4);
Check("item slots", CharacterFormat.ItemSlotCount, 10);
Check("anchor string length", GameFacts.AnchorString.Length, 34);
// The pack is the tail of the record: ten slots ending exactly on the record boundary.
Check("pack ends at the record boundary",
      CharacterFormat.OffItems + CharacterFormat.ItemSlotCount, CharacterFormat.RecordSize);
Console.WriteLine();

// --- build a synthetic DDCHARS.DAT from the sample ---------------------------
byte[] fileData = new byte[CharacterFormat.FileSize];
fileData[0] = 1; // header active flag

int off = CharacterFormat.HeaderSize;
fileData[off + CharacterFormat.OffExists] = 1;
fileData[off + CharacterFormat.OffNameLen] = 11;
var nameBytes = System.Text.Encoding.ASCII.GetBytes("CHRISTOPHER");
Array.Copy(nameBytes, 0, fileData, off + CharacterFormat.OffName, nameBytes.Length);
fileData[off + CharacterFormat.OffStatus] = CharacterFormat.StatusFine;
fileData[off + CharacterFormat.OffClass] = CharacterFormat.ClassFighter;
WriteU16(fileData, off + CharacterFormat.OffStr, 17);
WriteU16(fileData, off + CharacterFormat.OffDex, 16);
WriteU16(fileData, off + CharacterFormat.OffCon, 14);
WriteU16(fileData, off + CharacterFormat.OffInt, 14);
WriteU16(fileData, off + CharacterFormat.OffPie, 14);
WriteU16(fileData, off + CharacterFormat.OffLevel, 1);
WriteU32(fileData, off + CharacterFormat.OffExperience, 0);
WriteU32(fileData, off + CharacterFormat.OffNextLevel, 1000);
WriteU16(fileData, off + CharacterFormat.OffMagicCur, 0);
WriteU16(fileData, off + CharacterFormat.OffMagicMax, 0);
WriteU16(fileData, off + CharacterFormat.OffBodyCur, 35);
WriteU16(fileData, off + CharacterFormat.OffBodyMax, 35);
WriteU16(fileData, off + CharacterFormat.OffGold, 100);

Console.WriteLine("Character record decode:");
var rec0 = new CharacterRecord(fileData, CharacterFormat.HeaderSize);
Check("name", rec0.Name, "CHRISTOPHER");
Check("class", rec0.Class, CharacterFormat.ClassFighter);
Check("class name", rec0.ClassName, "Fighter");
Check("level", rec0.Level, 1);
Check("STR", rec0.Strength, 17);
Check("DEX", rec0.Dexterity, 16);
Check("CON", rec0.Constitution, 14);
Check("INT", rec0.Intelligence, 14);
Check("PIE", rec0.Piety, 14);
Check("gold", rec0.Gold, 100);
Check("body current", rec0.BodyCurrent, 35);
Check("body max", rec0.BodyMax, 35);
Check("experience", rec0.Experience, 0L);
Check("next level", rec0.NextLevel, 1000L);
Check("magic current", rec0.MagicCurrent, 0);
Check("magic max", rec0.MagicMax, 0);
Check("status", rec0.Status, 1);
Check("status name", rec0.StatusName, "fine");
Check("IsOccupied", rec0.IsOccupied, true);
Check("pack starts empty", rec0.ItemCount, 0);
Console.WriteLine();

// --- name round-trip ---------------------------------------------------------
Console.WriteLine("Name encode / decode round-trip:");
foreach (var name in new[] { "A", "Bo", "CHRISTOPHER", "Max12Chars" })
{
    var rec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    rec.Name = name;
    Check($"round-trip \"{name}\"", rec.Name, name);
    Check($"name length for \"{name}\"", rec.Bytes[CharacterFormat.OffNameLen], Math.Min(name.Length, 12));
}
Console.WriteLine();

// --- long name truncation ----------------------------------------------------
Console.WriteLine("Name truncation:");
var longRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
longRec.Name = "VeryLongNameHere";
Check("truncated to 12", longRec.Name, "VeryLongName");
Check("name length byte", longRec.Bytes[CharacterFormat.OffNameLen], 12);
Console.WriteLine();

// --- empty slot detection ----------------------------------------------------
Console.WriteLine("Empty slot detection:");
var emptyRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
Check("empty slot not occupied", emptyRec.IsOccupied, false);
Check("empty slot looks like record", CharacterFormat.LooksLikeRecord(emptyRec.Bytes, 0), false);
Check("empty slot is empty slot", CharacterFormat.IsEmptySlot(emptyRec.Bytes, 0), true);
Console.WriteLine();

// --- LooksLikeRecord validation ----------------------------------------------
Console.WriteLine("LooksLikeRecord validation:");
Check("valid record", CharacterFormat.LooksLikeRecord(fileData, CharacterFormat.HeaderSize), true);
Check("empty zeros not a record",
      CharacterFormat.LooksLikeRecord(new byte[CharacterFormat.RecordSize], 0), false);

var badStatus = (byte[])fileData.Clone();
badStatus[CharacterFormat.HeaderSize + CharacterFormat.OffStatus] = 9;
Check("bad status rejected", CharacterFormat.LooksLikeRecord(badStatus, CharacterFormat.HeaderSize), false);

var badClass = (byte[])fileData.Clone();
badClass[CharacterFormat.HeaderSize + CharacterFormat.OffClass] = 9;
Check("bad class rejected", CharacterFormat.LooksLikeRecord(badClass, CharacterFormat.HeaderSize), false);

var badLevel = (byte[])fileData.Clone();
badLevel[CharacterFormat.HeaderSize + CharacterFormat.OffLevel] = 0;
Check("level 0 rejected", CharacterFormat.LooksLikeRecord(badLevel, CharacterFormat.HeaderSize), false);

var badName = (byte[])fileData.Clone();
badName[CharacterFormat.HeaderSize + CharacterFormat.OffName] = (byte)'1';
Check("non-letter name rejected", CharacterFormat.LooksLikeRecord(badName, CharacterFormat.HeaderSize), false);
Console.WriteLine();

// --- roster geometry ---------------------------------------------------------
Console.WriteLine("Roster geometry:");
Check("roster bytes", CharacterFormat.MaxSlots * CharacterFormat.RecordSize, 1080);
Check("file = header + roster", CharacterFormat.HeaderSize + CharacterFormat.MaxSlots * CharacterFormat.RecordSize, CharacterFormat.FileSize);
Console.WriteLine();

// --- attribute offsets -------------------------------------------------------
Console.WriteLine("Attribute offsets:");
Check("STR offset", CharacterFormat.OffStr, 0x11);
Check("DEX offset", CharacterFormat.OffDex, 0x13);
Check("CON offset", CharacterFormat.OffCon, 0x15);
Check("INT offset", CharacterFormat.OffInt, 0x17);
Check("PIE offset", CharacterFormat.OffPie, 0x19);
Check("attributes at 2-byte stride", CharacterFormat.OffDex - CharacterFormat.OffStr, 2);
Console.WriteLine();

// --- setAttribute / write round-trip -----------------------------------------
Console.WriteLine("Attribute set round-trip:");
var attrRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
attrRec.Name = "TEST";
attrRec.Strength = 30;
attrRec.Dexterity = 25;
attrRec.Constitution = 18;
attrRec.Intelligence = 20;
attrRec.Piety = 15;
Check("set STR", attrRec.Strength, 30);
Check("set DEX", attrRec.Dexterity, 25);
Check("set CON", attrRec.Constitution, 18);
Check("set INT", attrRec.Intelligence, 20);
Check("set PIE", attrRec.Piety, 15);
Console.WriteLine();

// --- vitals set round-trip ---------------------------------------------------
Console.WriteLine("Vitals set round-trip:");
attrRec.BodyCurrent = 999;
attrRec.BodyMax = 999;
attrRec.MagicCurrent = 500;
attrRec.MagicMax = 500;
attrRec.Gold = 65535;
attrRec.Experience = 999999;
attrRec.NextLevel = 1000000;
attrRec.Level = 30;
Check("set body current", attrRec.BodyCurrent, 999);
Check("set body max", attrRec.BodyMax, 999);
Check("set magic", attrRec.MagicCurrent, 500);
Check("set magic max", attrRec.MagicMax, 500);
Check("set gold", attrRec.Gold, 65535);
Check("set experience", attrRec.Experience, 999999L);
Check("set next level", attrRec.NextLevel, 1000000L);
Check("set level", attrRec.Level, 30);
// Experience is a 32-bit field; a 16-bit accessor would silently wrap here.
attrRec.Experience = 70000;
Check("experience holds > 65535", attrRec.Experience, 70000L);
Console.WriteLine();

// --- inventory and readied equipment -----------------------------------------
Console.WriteLine("Inventory and readied equipment:");
var packRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
packRec.Name = "PACKRAT";
Check("pack starts empty", packRec.ItemCount, 0);

packRec.SetItem(0, 5);                     // Long Sword
packRec.SetItem(9, 20);                    // Healing Potion
Check("slot A", packRec.GetItem(0), 5);
Check("slot J", packRec.GetItem(9), 20);
Check("slot A is Long Sword", ItemBook.Name(packRec.GetItem(0)), "Long Sword");
Check("slot J is Healing Potion", ItemBook.Name(packRec.GetItem(9)), "Healing Potion");
Check("pack count", packRec.ItemCount, 2);
// Slots are the last ten bytes of the record, in order.
Check("slot A byte offset", CharacterFormat.ItemOffset(0), 0x3E);
Check("slot J byte offset", CharacterFormat.ItemOffset(9), 0x47);
Check("slot A lands on its byte", packRec.Bytes[0x3E], (byte)5);
Check("slot J lands on its byte", packRec.Bytes[0x47], (byte)20);

Check("AddItem uses the first hole", packRec.AddItem(1), 1);
Check("AddItem wrote it", packRec.GetItem(1), 1);
packRec.ClearItems();
Check("ClearItems empties the pack", packRec.ItemCount, 0);

// A full pack has nowhere to put an eleventh item.
for (int i = 0; i < CharacterFormat.ItemSlotCount; i++) packRec.SetItem(i, 1);
Check("full pack refuses AddItem", packRec.AddItem(2), -1);
packRec.ClearItems();

// Ids are clamped into the range the game will accept.
packRec.SetItem(0, 999);
Check("item id clamps to 63", packRec.GetItem(0), CharacterFormat.MaxItemId);
packRec.SetItem(0, -5);
Check("item id clamps to 0", packRec.GetItem(0), 0);

packRec.SetReadied(ItemBook.ReadySlot.RightHand, 7);   // Two Hand Sword
packRec.SetReadied(ItemBook.ReadySlot.LeftHand, 8);    // Shield
packRec.SetReadied(ItemBook.ReadySlot.Armor, 14);      // Plate Mail
packRec.SetReadied(ItemBook.ReadySlot.Ring, 24);       // Speed Ring
Check("right hand", packRec.RightHand, 7);
Check("left hand", packRec.LeftHand, 8);
Check("armor", packRec.Armor, 14);
Check("ring", packRec.Ring, 24);
Check("right hand offset", ItemBook.ReadyOffset(ItemBook.ReadySlot.RightHand), 0x30);
Check("left hand offset", ItemBook.ReadyOffset(ItemBook.ReadySlot.LeftHand), 0x31);
Check("armor offset", ItemBook.ReadyOffset(ItemBook.ReadySlot.Armor), 0x33);
Check("ring offset", ItemBook.ReadyOffset(ItemBook.ReadySlot.Ring), 0x34);
// Readied equipment and the pack must not overlap.
Check("pack starts after the equipment slots",
      CharacterFormat.OffItems > ItemBook.ReadyOffset(ItemBook.ReadySlot.Ring), true);
Check("readying did not disturb the pack", packRec.ItemCount, 0);
Console.WriteLine();

// --- the game's own ready-slot rules ------------------------------------------
Console.WriteLine("Ready-slot rules (the game's \"Wrong type!\" check):");
Check("two-handed sword in the right hand", ItemBook.CanReady(ItemBook.ReadySlot.RightHand, 7), true);
Check("two-handed sword in the left hand", ItemBook.CanReady(ItemBook.ReadySlot.LeftHand, 7), false);
Check("dagger in the left hand", ItemBook.CanReady(ItemBook.ReadySlot.LeftHand, 1), true);
Check("shield in the left hand", ItemBook.CanReady(ItemBook.ReadySlot.LeftHand, 8), true);
Check("plate mail in the armor slot", ItemBook.CanReady(ItemBook.ReadySlot.Armor, 14), true);
Check("plate mail in the right hand", ItemBook.CanReady(ItemBook.ReadySlot.RightHand, 14), false);
Check("speed ring in the ring slot", ItemBook.CanReady(ItemBook.ReadySlot.Ring, 24), true);
Check("potion is not readyable", ItemBook.CanReady(ItemBook.ReadySlot.RightHand, 20), false);
Check("emptying a slot is always allowed", ItemBook.CanReady(ItemBook.ReadySlot.Armor, 0), true);
Console.WriteLine();

// --- item potency (the game's stand-in for charges) ---------------------------
// Dark Designs has no charge counters: on (U)se it rolls random(256) and destroys the item
// unless potency > roll. These pin the table values the "never break" patch depends on.
Console.WriteLine("Item potency:");
Check("potency 256 always beats random(256)", ItemBook.PotencyAlways, 256);
Check("healing potion survives half the time", ItemBook.Get(20).Potency, 128);
Check("cureall almost always survives", ItemBook.Get(22).Potency, 255);
Check("paralyze wand rarely survives", ItemBook.Get(18).Potency, 10);
Check("recall scroll almost always survives", ItemBook.Get(26).Potency, 250);
Check("ordinary gear never rolls", ItemBook.Get(5).Potency, 0);
Check("keys are consumed on use", ItemBook.Get(60).Potency, 0);
// Magic weapons carry a potency too — it's their special-effect chance in combat.
Check("holy sword triggers sometimes", ItemBook.Get(31).Potency, 77);
Check("active axe triggers often", ItemBook.Get(40).Potency, 200);
Check("consumable set is the usable player items", ItemBook.Consumables.All(i => i.Type == ItemBook.ItemType.Usable), true);
Check("magic weapon set is weapons only",
      ItemBook.MagicWeapons.All(i => i.Type is ItemBook.ItemType.Light or ItemBook.ItemType.Medium or ItemBook.ItemType.TwoHanded), true);
Check("magic weapons all roll", ItemBook.MagicWeapons.All(i => i.Potency > 0), true);
Check("the two patch sets never overlap",
      ItemBook.Consumables.Select(i => i.Id).Intersect(ItemBook.MagicWeapons.Select(i => i.Id)).Any(), false);
// Item table geometry the live patch indexes by.
Check("item entry size", ItemBook.EntrySize, 40);
Check("name is 2 bytes into the entry", ItemBook.EntryOffName, 0x02);
Check("potency offset in the entry", ItemBook.EntryOffPotency, 0x14);
Console.WriteLine();

// --- party-mirror staleness ---------------------------------------------------
// The party array is a fixed set of slots the game reuses. If the player reforms the party, an
// address we cached now belongs to somebody else — writing there would corrupt that character.
Console.WriteLine("Party-mirror staleness:");
{
    const int rosterAt = 100, mirrorAt = 500, otherAt = 900;
    var host = new FakeHost(2048);
    PlantCharacter(host.Mem, rosterAt, "HERO", CharacterFormat.ClassFighter, gold: 50);
    PlantCharacter(host.Mem, mirrorAt, "HERO", CharacterFormat.ClassFighter, gold: 50);
    PlantCharacter(host.Mem, otherAt, "RIVAL", CharacterFormat.ClassPriest, gold: 7);

    var located = new LocatedCharacter((nuint)rosterAt, 0, new CharacterRecord(host.Mem, rosterAt));
    located.Mirrors.Add((nuint)mirrorAt);
    var vm = new CharacterViewModel(host, located);

    vm.Poll();
    Check("healthy mirror is kept", vm.IsInParty, true);
    Check("not stale", vm.IsStale, false);
    vm.Gold = 111;
    Check("roster written", ReadU16(host.Mem, rosterAt + CharacterFormat.OffGold), 111);
    Check("mirror written", ReadU16(host.Mem, mirrorAt + CharacterFormat.OffGold), 111);

    // The game hands that party slot to a different character.
    PlantCharacter(host.Mem, mirrorAt, "RIVAL", CharacterFormat.ClassPriest, gold: 7);
    vm.Poll();
    Check("stale mirror dropped", vm.IsInParty, false);
    vm.Gold = 222;
    Check("roster still written", ReadU16(host.Mem, rosterAt + CharacterFormat.OffGold), 222);
    Check("the other character is left alone", ReadU16(host.Mem, mirrorAt + CharacterFormat.OffGold), 7);

    // Now the roster slot itself changes hands.
    PlantCharacter(host.Mem, rosterAt, "RIVAL", CharacterFormat.ClassPriest, gold: 7);
    vm.Poll();
    Check("stale roster detected", vm.IsStale, true);
    int before = host.WriteCount;
    vm.Gold = 333;
    Check("no writes once stale", host.WriteCount, before);
    Check("roster untouched", ReadU16(host.Mem, rosterAt + CharacterFormat.OffGold), 7);
}
Console.WriteLine();

// --- the editable slot view-model --------------------------------------------
// This is what both the live party editor and the save editor bind a dropdown to, so its
// read/write path and its option list are worth pinning without standing up a window.
Console.WriteLine("Item slot view-model:");
var slotRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
var packSlot = new ItemSlotViewModel("A", () => slotRec.GetItem(0), id => slotRec.SetItem(0, id));
Check("starts empty", packSlot.Selected.Id, 0);
Check("empty slot has no detail line", packSlot.Detail, "");
packSlot.Selected = ItemBook.Get(16);                       // Hell Dagger
Check("writing through the view-model", slotRec.GetItem(0), 16);
Check("reads back", packSlot.Selected.Name, "Hell Dagger");
Check("detail mentions the id", packSlot.Detail.Contains("#16"), true);
Check("pack options exclude monster gear", packSlot.Options.Any(o => o.Id == 41), false);
Check("pack options include the empty entry", packSlot.Options.Any(o => o.Id == 0), true);

// A value the game put there that the dropdown wouldn't normally offer must still be selectable,
// or the ComboBox would blank it and the next edit would silently erase it.
slotRec.SetItem(0, 41);                                     // monster Hide, set behind our back
packSlot.Refresh();
Check("off-list value is adopted into the options", packSlot.Options.Any(o => o.Id == 41), true);
Check("off-list value shows as itself", packSlot.Selected.Name, "Hide");
Check("off-list value is flagged as monster gear", packSlot.Detail.Contains("monster gear"), true);

var armorSlot = new ItemSlotViewModel("Armor",
    () => slotRec.GetReadied(ItemBook.ReadySlot.Armor),
    id => slotRec.SetReadied(ItemBook.ReadySlot.Armor, id),
    ItemBook.ReadySlot.Armor);
Check("armor options are armor only",
      armorSlot.Options.All(o => o.Id == 0 || o.Type == ItemBook.ItemType.Armor), true);
armorSlot.Selected = ItemBook.Get(15);                      // Full Plate
Check("readied armor written", slotRec.Armor, 15);
Check("legal readied item", armorSlot.IsLegal, true);
slotRec.SetReadied(ItemBook.ReadySlot.Armor, 7);            // Two Hand Sword — the game would refuse
armorSlot.Refresh();
Check("illegal readied item is flagged", armorSlot.IsLegal, false);
Console.WriteLine();

// --- duplicating an item ------------------------------------------------------
// Items are destroyed on use, so a spare is the practical substitute for a recharge.
Console.WriteLine("Duplicate item:");
var dupRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
dupRec.Name = "DUPER";
var pack = new TestPack(dupRec);
var dupSlot = new ItemSlotViewModel("A", () => dupRec.GetItem(0), id => dupRec.SetItem(0, id), pack: pack);

Check("cannot duplicate an empty slot", dupSlot.DuplicateCommand.CanExecute(null), false);
dupSlot.Selected = ItemBook.Get(22);                        // Cureall Potion
Check("can duplicate once the slot is filled", dupSlot.DuplicateCommand.CanExecute(null), true);
dupSlot.DuplicateCommand.Execute(null);
Check("copy landed in the first free slot", ItemBook.Name(dupRec.GetItem(1)), "Cureall Potion");
Check("original is untouched", ItemBook.Name(dupRec.GetItem(0)), "Cureall Potion");
Check("pack count after one duplicate", dupRec.ItemCount, 2);

// Duplicating repeatedly fills the pack and then stops cleanly.
while (dupSlot.DuplicateCommand.CanExecute(null)) dupSlot.DuplicateCommand.Execute(null);
Check("duplicating fills the pack", dupRec.ItemCount, CharacterFormat.ItemSlotCount);
Check("a full pack disables duplicate", dupSlot.DuplicateCommand.CanExecute(null), false);
Check("no free slot reported", pack.HasFreeSlot, false);
Check("adding to a full pack fails", pack.TryAddItem(1), false);
Check("every slot holds the copy",
      dupRec.Items.All(id => id == 22), true);

// A readied slot has no pack, so it offers no duplicate button.
Check("readied slots have no duplicate", armorSlot.CanShowDuplicate, false);
Check("pack slots do", dupSlot.CanShowDuplicate, true);
Console.WriteLine();

// --- an item byte the game never issues ---------------------------------------
// The pack bytes are raw memory: a value above 63 is reachable. ItemBook.Get falls back to
// entry 0 for those, which previously meant EnsureOption could never satisfy its own guard and
// appended a duplicate "(empty)" to the bound option list on every poll tick, forever.
Console.WriteLine("Out-of-range item byte:");
var oddRec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
oddRec.Name = "ODD";
var oddSlot = new ItemSlotViewModel("A", () => oddRec.GetItem(0), id => oddRec.SetItem(0, id));
int baseline = oddSlot.Options.Count;
oddRec.Bytes[CharacterFormat.ItemOffset(0)] = 200;          // straight past the clamp, as memory would
for (int i = 0; i < 50; i++) oddSlot.Refresh();
Check("option list does not grow", oddSlot.Options.Count, baseline);
Check("no duplicate (empty) entries", oddSlot.Options.Count(o => o.Id == 0), 1);
Check("the raw value is reported", oddSlot.Detail.Contains("200"), true);
Check("it is not silently shown as an item", oddSlot.Detail.Contains("not a known item id"), true);
Console.WriteLine();

// --- save file round-trip ----------------------------------------------------
Console.WriteLine("Save file round-trip:");
string tmpDir = Path.Combine(Path.GetTempPath(), "dd1test_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tmpDir);
string tmpSave = Path.Combine(tmpDir, "DDCHARS.DAT");
File.WriteAllBytes(tmpSave, fileData);
try
{
    using (var sf = new SaveFile(tmpSave))
    {
        Check("save file character count", sf.OccupiedCharacters.Count(), 1);
        var c = sf.OccupiedCharacters.First();
        Check("save file name", c.Name, "CHRISTOPHER");
        Check("save file class", c.Class, CharacterFormat.ClassFighter);

        c.Strength = 30;
        c.Gold = 9999;
        c.SetItem(0, 16);                                     // Hell Dagger
        c.SetItem(3, 22);                                     // Cureall Potion
        c.SetReadied(ItemBook.ReadySlot.Armor, 15);           // Full Plate
        sf.MarkModified();
        sf.Save();
    }

    var saved = File.ReadAllBytes(tmpSave);
    Check("file size preserved", saved.Length, CharacterFormat.FileSize);
    var reloaded = new CharacterRecord(saved, CharacterFormat.HeaderSize);
    Check("modified STR persists", reloaded.Strength, 30);
    Check("modified gold persists", reloaded.Gold, 9999);
    Check("pack slot A persists", ItemBook.Name(reloaded.GetItem(0)), "Hell Dagger");
    Check("pack slot D persists", ItemBook.Name(reloaded.GetItem(3)), "Cureall Potion");
    Check("readied armor persists", ItemBook.Name(reloaded.Armor), "Full Plate");
    Check("untouched pack slots stay empty", reloaded.ItemCount, 2);

    Check("backup file exists", File.Exists(tmpSave + ".bak"), true);
    var backup = File.ReadAllBytes(tmpSave + ".bak");
    Check("backup has original STR", ReadU16(backup, CharacterFormat.HeaderSize + CharacterFormat.OffStr), 17);
}
finally
{
    if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
}
Console.WriteLine();

// --- multiple characters in save file ----------------------------------------
Console.WriteLine("Multiple characters in save file:");
byte[] multiData = new byte[CharacterFormat.FileSize];
multiData[0] = 1;
for (int i = 0; i < 4; i++)
{
    int o = CharacterFormat.HeaderSize + i * CharacterFormat.RecordSize;
    multiData[o + CharacterFormat.OffExists] = 1;
    multiData[o + CharacterFormat.OffNameLen] = 5;
    System.Text.Encoding.ASCII.GetBytes($"HERO{i}").CopyTo(multiData, o + CharacterFormat.OffName);
    multiData[o + CharacterFormat.OffStatus] = CharacterFormat.StatusFine;
    multiData[o + CharacterFormat.OffClass] = (byte)(i % 3 + 1);
    WriteU16(multiData, o + CharacterFormat.OffLevel, i + 1);
    WriteU16(multiData, o + CharacterFormat.OffStr, 15 + i);
    WriteU16(multiData, o + CharacterFormat.OffBodyMax, 20 + i * 5);
    WriteU16(multiData, o + CharacterFormat.OffBodyCur, 20 + i * 5);
}
string multiSave = Path.Combine(Path.GetTempPath(), "dd1multi_" + Guid.NewGuid().ToString("N")[..8] + ".DAT");
File.WriteAllBytes(multiSave, multiData);
try
{
    using (var sf = new SaveFile(multiSave))
    {
        Check("multi save character count", sf.OccupiedCharacters.Count(), 4);
        var chars = sf.OccupiedCharacters.ToList();
        Check("first character name", chars[0].Name, "HERO0");
        Check("second character name", chars[1].Name, "HERO1");
        Check("third character name", chars[2].Name, "HERO2");
        Check("fourth character name", chars[3].Name, "HERO3");
        Check("first class (Fighter)", chars[0].ClassName, "Fighter");
        Check("second class (Priest)", chars[1].ClassName, "Priest");
        Check("third class (Wizard)", chars[2].ClassName, "Wizard");
        Check("fourth class (Fighter)", chars[3].ClassName, "Fighter");
    }
}
finally
{
    if (File.Exists(multiSave)) File.Delete(multiSave);
    if (File.Exists(multiSave + ".bak")) File.Delete(multiSave + ".bak");
}
Console.WriteLine();

// --- reference tables --------------------------------------------------------
Console.WriteLine("Reference tables:");
Check("wizard spell count", SpellBook.WizardSpells.Length, 8);
Check("priest spell count", SpellBook.PriestSpells.Length, 8);
Check("item id count", ItemBook.All.Length, CharacterFormat.MaxItemId + 1);
Check("monster count", MonsterBook.All.Length, 43);
Check("level names count", GameFacts.LevelNames.Length, 5);
Check("class names count", CharacterFormat.ClassNames.Length, 4);
Check("status names count", CharacterFormat.StatusNames.Length, 6);
Check("attribute names count", CharacterFormat.AttributeNames.Length, 5);
Console.WriteLine();

// --- spell content spot checks -----------------------------------------------
Console.WriteLine("Spell content spot checks:");
Check("first wizard spell", SpellBook.WizardSpells[0].Name, "Magic Missile");
Check("last wizard spell", SpellBook.WizardSpells[7].Name, "Death Ray");
Check("first priest spell", SpellBook.PriestSpells[0].Name, "Cure Light Wounds");
Check("last priest spell", SpellBook.PriestSpells[7].Name, "Cureall");
Check("fireball gold cost", SpellBook.WizardSpells[5].GoldCost, 300);
Check("word of recall gold cost", SpellBook.PriestSpells[6].GoldCost, 350);
Console.WriteLine();

// --- item content spot checks ------------------------------------------------
Console.WriteLine("Item content spot checks:");
// Every entry must sit at its own index — the index *is* the byte the game stores.
for (int id = 0; id < ItemBook.All.Length; id++)
    if (ItemBook.All[id].Id != id) { Console.WriteLine($"  [FAIL] item {id} has Id {ItemBook.All[id].Id}"); failures++; }
Check("ids are their own index", true, true);
Check("id 0 is the empty slot", ItemBook.All[0].Name, "(empty)");
Check("id 1 is the Dagger", ItemBook.Name(1), "Dagger");
Check("id 63 is the quest staff", ItemBook.Name(63), "The Staff");
Check("keys are 60-62", $"{ItemBook.Name(60)},{ItemBook.Name(61)},{ItemBook.Name(62)}", "Key 1,Key 2,Key 3");
Check("out-of-range id falls back to empty", ItemBook.Get(200).Id, 0);
Check("negative id falls back to empty", ItemBook.Get(-1).Id, 0);
// Prices and class masks, straight off the EXE table.
Check("dagger price", ItemBook.Get(1).Price, 5);
Check("full plate price", ItemBook.Get(15).Price, 250);
Check("magic shield protection", ItemBook.Get(11).Protection, 55);
Check("priests cannot use the dagger", ItemBook.Get(1).UsableBy(CharacterFormat.ClassPriest), false);
Check("fighters can use the dagger", ItemBook.Get(1).UsableBy(CharacterFormat.ClassFighter), true);
Check("everyone can use the staff", ItemBook.Get(2).ClassLabel, "F P W");
Check("mace is fighter and priest", ItemBook.Get(3).ClassLabel, "F P");
Check("paralyze wand is wizard-only", ItemBook.Get(18).ClassLabel, "W");
// Monster gear is addressable but not player gear.
Check("monster hide is not a player item", ItemBook.Get(41).IsPlayerItem, false);
Check("blank entries are not player items", ItemBook.Get(27).IsPlayerItem, false);
// Gaze carries a full class mask but is a monster attack with no price — it must not be offered
// as player gear, or it would show up in every dropdown and in the magic-weapon patch set.
Check("Gaze is not a player item", ItemBook.Get(32).IsPlayerItem, false);
Check("the priceless quest staff still is", ItemBook.Get(63).IsPlayerItem, true);
Check("every player item is priced or the quest staff",
      ItemBook.PlayerItems.All(i => i.Price > 0 || i.Id == ItemBook.QuestStaffId), true);
Check("player item count", ItemBook.PlayerItems.Count(), 41);
// Every readied slot must offer "(empty)" plus only things it will accept.
foreach (ItemBook.ReadySlot rs in Enum.GetValues<ItemBook.ReadySlot>())
{
    var opts = ItemBook.ReadyOptions(rs).ToList();
    Check($"{rs} options include (empty)", opts.Any(o => o.Id == 0), true);
    Check($"{rs} options are all legal", opts.All(o => ItemBook.CanReady(rs, o.Id)), true);
    Check($"{rs} has something to offer", opts.Count > 1, true);
}
Console.WriteLine();

// --- monster content spot checks ---------------------------------------------
Console.WriteLine("Monster content spot checks:");
Check("first monster", MonsterBook.All[0].Name, "Kobold");
Check("last monster", MonsterBook.All[^1].Name, "Chaos Avatar");
Check("demon lord exists", MonsterBook.All.Any(m => m.Name == "Demon Lord"), true);
Check("medusa exists", MonsterBook.All.Any(m => m.Name == "Medusa"), true);
Console.WriteLine();

// --- creation roll: format constants -----------------------------------------
Console.WriteLine("Creation roll format:");
Check("rolled count", CreationFormat.RolledCount, 5);
Check("value size", CreationFormat.ValueSize, 2);
Check("pool bytes", CreationFormat.PoolBytes, 10);
Check("min roll", CreationFormat.MinRoll, 10);
Check("max roll", CreationFormat.MaxRoll, 18);
Check("min total", CreationFormat.MinTotal, 50);
Check("max total", CreationFormat.MaxTotal, 90);
Check("slot name count", CreationFormat.SlotNames.Length, 5);
Check("rank name count", CreationFormat.RankNames.Length, 5);
Check("attribute description count", AttributeBook.Descriptions.Length, 5);
// The target boxes must accept numbers the dice can't reach, or an over-ambitious minimum is
// silently rewritten into an achievable one instead of being reported as out of reach.
Check("target cap exceeds the highest roll", CreationFormat.MaxTargetValue > CreationFormat.MaxRoll, true);
Check("target total cap exceeds the highest total", CreationFormat.MaxTargetTotal > CreationFormat.MaxTotal, true);
Console.WriteLine();

// --- creation roll: encode / decode ------------------------------------------
Console.WriteLine("Creation roll encode / decode:");
var poolValues = new[] { 14, 18, 12, 16, 11 };
byte[] poolBytes = CreationFormat.Encode(poolValues);
Check("encoded length", poolBytes.Length, CreationFormat.PoolBytes);
Check("encoded first value LE low", poolBytes[0], (byte)14);
Check("encoded first value LE high", poolBytes[1], (byte)0);
var poolBack = new int[CreationFormat.RolledCount];
Check("decode succeeds", CreationFormat.Decode(poolBytes, 0, poolBack), true);
Check("decode round-trip", string.Join(",", poolBack), "14,18,12,16,11");
Check("decode rejects short buffer", CreationFormat.Decode(new byte[4], 0, poolBack), false);
Check("total", CreationFormat.Total(poolValues), 71);
Console.WriteLine();

// --- creation roll: plausibility gate ----------------------------------------
Console.WriteLine("Creation roll plausibility:");
Check("real roll accepted", CreationFormat.LooksLikeRoll(new[] { 10, 18, 14, 13, 15 }), true);
Check("game's attribute floor accepted", CreationFormat.LooksLikeRoll(new[] { 3, 3, 3, 3, 3 }), true);
Check("zero rejected", CreationFormat.LooksLikeRoll(new[] { 0, 14, 14, 14, 14 }), false);
Check("above 18 rejected", CreationFormat.LooksLikeRoll(new[] { 19, 14, 14, 14, 14 }), false);
Check("short list rejected", CreationFormat.LooksLikeRoll(new[] { 14, 14 }), false);
Console.WriteLine();

// --- creation roll: arranging the pool onto the attributes -------------------
Console.WriteLine("Creation roll arrangement:");
var roll = new[] { 14, 18, 12, 16, 11 };            // slots #1..#5
// STR wants 17: only the 18 in slot #2 can serve it.
var arranged = CreationFormat.Arrange(roll, new[] { 17, 0, 0, 0, 0 });
Check("feasible target arranges", arranged != null, true);
Check("STR takes the 18 (slot index 1)", arranged![0], 1);
Check("arrangement uses every slot once", string.Join(",", arranged.OrderBy(x => x)), "0,1,2,3,4");
// Two attributes both wanting 16+ can be served by the 18 and the 16.
Check("two high minimums feasible",
      CreationFormat.Arrange(roll, new[] { 16, 0, 16, 0, 0 }) != null, true);
// Three can't: only 18, 16 and 14 are left and 14 < 16.
Check("three high minimums infeasible",
      CreationFormat.Arrange(roll, new[] { 16, 0, 16, 16, 0 }) is null, true);
Check("minimum above every value infeasible",
      CreationFormat.Arrange(roll, new[] { 0, 0, 0, 0, 19 }) is null, true);
Check("no minimums always feasible", CreationFormat.Arrange(roll, new[] { 0, 0, 0, 0, 0 }) != null, true);
Check("null minimums always feasible", CreationFormat.Arrange(roll, null) != null, true);
// Feasibility depends on the pool as a set, not the order the values came out in.
Check("arrangement is order-insensitive",
      CreationFormat.Arrange(new[] { 11, 16, 12, 18, 14 }, new[] { 16, 0, 16, 0, 0 }) != null, true);
Check("meets target with total", CreationFormat.MeetsTarget(roll, new[] { 17, 0, 0, 0, 0 }, 71), true);
Check("total minimum can fail an otherwise-good roll",
      CreationFormat.MeetsTarget(roll, new[] { 17, 0, 0, 0, 0 }, 72), false);
Console.WriteLine();

// --- creation roll: shortfall ranking ----------------------------------------
Console.WriteLine("Creation roll shortfall:");
Check("met target has no shortfall", CreationFormat.Shortfall(roll, new[] { 17, 0, 0, 0, 0 }, 0), 0);
Check("single gap", CreationFormat.Shortfall(roll, new[] { 0, 0, 0, 0, 19 }, 0), 1);
Check("total-only gap", CreationFormat.Shortfall(roll, new[] { 0, 0, 0, 0, 0 }, 75), 4);
Check("gaps add up", CreationFormat.Shortfall(roll, new[] { 19, 0, 0, 0, 0 }, 75), 5);
Console.WriteLine();

// --- creation roll: the measured distribution --------------------------------
Console.WriteLine("Roll distribution (10 + random(5) + random(5)):");
double pmfSum = 0;
for (int v = CreationFormat.MinRoll; v <= CreationFormat.MaxRoll; v++) pmfSum += RollOdds.P(v);
CheckClose("pmf sums to 1", pmfSum, 1.0);
CheckClose("P(10) = 1/25", RollOdds.P(10), 1 / 25.0);
CheckClose("P(14) = 5/25", RollOdds.P(14), 5 / 25.0);
CheckClose("P(18) = 1/25", RollOdds.P(18), 1 / 25.0);
CheckClose("distribution is symmetric", RollOdds.P(11), RollOdds.P(17));
CheckClose("P(value >= 10) = 1", RollOdds.PAtLeast(10), 1.0);
CheckClose("P(value >= 18) = 1/25", RollOdds.PAtLeast(18), 1 / 25.0);
CheckClose("P(value >= 19) = 0", RollOdds.PAtLeast(19), 0.0);
double mean = 0;
for (int v = CreationFormat.MinRoll; v <= CreationFormat.MaxRoll; v++) mean += v * RollOdds.P(v);
CheckClose("mean value is 14", mean, 14.0);
Console.WriteLine();

// --- creation roll: odds cross-checked against brute force -------------------
// PMeetsTarget enumerates the 1,287 sorted combinations with multinomial weights; this replays all
// 59,049 ordered outcomes through the same predicate the roller actually stops on, so a mistake in
// either the combinatorics or Arrange's feasibility rule shows up as a mismatch.
Console.WriteLine("Roll odds vs brute force over all 59,049 outcomes:");
(string Label, int[] Mins, int TotalMin)[] targets =
{
    ("no target",             new[] { 0, 0, 0, 0, 0 },      0),
    ("STR >= 17",             new[] { 17, 0, 0, 0, 0 },     0),
    ("STR >= 17, CON >= 16",  new[] { 17, 0, 16, 0, 0 },    0),
    ("every attribute >= 13", new[] { 13, 13, 13, 13, 13 }, 0),
    ("every attribute >= 15", new[] { 15, 15, 15, 15, 15 }, 0),
    ("every attribute >= 18", new[] { 18, 18, 18, 18, 18 }, 0),
    ("total >= 80",           new[] { 0, 0, 0, 0, 0 },     80),
    ("STR >= 18, total >= 78",new[] { 18, 0, 0, 0, 0 },    78),
};
foreach (var (label, mins, totalMin) in targets)
    CheckClose($"P({label})", RollOdds.PMeetsTarget(mins, totalMin), BruteForce(mins, totalMin));

CheckClose("P(no target) = 1", RollOdds.PMeetsTarget(new[] { 0, 0, 0, 0, 0 }, 0), 1.0);
CheckClose("P(all 18s) = (1/25)^5", RollOdds.PMeetsTarget(new[] { 18, 18, 18, 18, 18 }, 0),
           Math.Pow(1 / 25.0, 5));
CheckClose("P(total >= 90) = (1/25)^5", RollOdds.PMeetsTarget(new[] { 0, 0, 0, 0, 0 }, 90),
           Math.Pow(1 / 25.0, 5));
Check("out-of-reach minimum is impossible", RollOdds.PMeetsTarget(new[] { 19, 0, 0, 0, 0 }, 0), 0.0);
Check("out-of-reach total is impossible", RollOdds.PMeetsTarget(new[] { 0, 0, 0, 0, 0 }, 91), 0.0);

// The figures quoted in docs/StrategyGuide.md, pinned to the model so the two can't drift apart.
CheckClose("guide: a roll contains an 18 about 1 in 5.4",
           RollOdds.PMeetsTarget(new[] { 18, 0, 0, 0, 0 }, 0), 1 - Math.Pow(24 / 25.0, 5));
CheckClose("guide: a roll contains a 17+ nearly half the time",
           RollOdds.PMeetsTarget(new[] { 17, 0, 0, 0, 0 }, 0), 1 - Math.Pow(22 / 25.0, 5));
CheckClose("guide: every value 15+ is about 1 in 98",
           RollOdds.PMeetsTarget(new[] { 15, 15, 15, 15, 15 }, 0), Math.Pow(0.4, 5));
Console.WriteLine();

// --- creation roll: signature scan -------------------------------------------
Console.WriteLine("Creation pool signature scan:");
// 0xFF filler can never match: every plausible value has a zero high byte.
byte[] haystack = Enumerable.Repeat((byte)0xFF, 256).ToArray();
const int plantedAt = 100;
Array.Copy(CreationFormat.Encode(roll), 0, haystack, plantedAt, CreationFormat.PoolBytes);

int[] sortedWanted = (int[])roll.Clone();
Array.Sort(sortedWanted);
var scanHits = CreationScanner.FindInBuffer(haystack, sortedWanted).ToList();
Check("finds the planted pool", scanHits.Count, 1);
Check("at the planted offset", scanHits.Count == 1 ? scanHits[0] : -1, plantedAt);

// Typing the numbers in a different order still locks on: the scan matches the set.
int[] shuffledWanted = { 18, 11, 16, 12, 14 };
Array.Sort(shuffledWanted);
Check("order-insensitive match",
      CreationScanner.FindInBuffer(haystack, shuffledWanted).Count(), 1);

int[] wrongWanted = { 10, 11, 12, 13, 14 };
Check("a different roll doesn't match", CreationScanner.FindInBuffer(haystack, wrongWanted).Count(), 0);
Check("empty buffer yields nothing", CreationScanner.FindInBuffer(Array.Empty<byte>(), sortedWanted).Count(), 0);
// Find must reject a short capture list rather than throwing from inside the caller's Task.Run.
Check("Find rejects a short capture list", CreationScanner.Find(null!, new[] { 14, 18 }).Count, 0);
Check("Find rejects a null capture list", CreationScanner.Find(null!, null!).Count, 0);
Check("scan gate accepts a real roll", CreationScanner.InRange(roll), true);
Check("scan gate rejects garbage", CreationScanner.InRange(new[] { 300, 14, 14, 14, 14 }), false);
Console.WriteLine();

// --- creation roll: "set the roll" parsing ------------------------------------
Console.WriteLine("Set-roll parsing:");
Check("five values parse",
      CreationFormat.TryParseValues("14 18 12 16 11", out var setFive, out _), true);
Check("five values kept in order", string.Join(",", setFive), "14,18,12,16,11");
Check("commas accepted",
      CreationFormat.TryParseValues("14,18,12,16,11", out _, out _), true);
Check("one value fills all five",
      CreationFormat.TryParseValues("18", out var setOne, out _), true);
Check("filled with the single value", string.Join(",", setOne), "18,18,18,18,18");
Check("values clamp to the game's range",
      CreationFormat.TryParseValues("99 0 18 3 12", out var setClamped, out _), true);
Check("clamped result", string.Join(",", setClamped), "18,3,18,3,12");
Check("wrong count rejected",
      CreationFormat.TryParseValues("14 18 12", out _, out _), false);
Check("non-numeric rejected",
      CreationFormat.TryParseValues("14 18 x 16 11", out _, out _), false);
Check("empty rejected", CreationFormat.TryParseValues("", out _, out _), false);
Console.WriteLine();

// --- sample DDCHARS.DAT (if present) ----------------------------------------
string? gameDir = FindGameDir();
if (gameDir != null)
{
    string charsPath = Path.Combine(gameDir, "DDCHARS.DAT");
    if (File.Exists(charsPath))
    {
        Console.WriteLine($"Sample DDCHARS.DAT found at {charsPath}:");
        try
        {
            var sample = File.ReadAllBytes(charsPath);
            Check("sample file size", sample.Length, CharacterFormat.FileSize);
            var sampleRec = new CharacterRecord(sample, CharacterFormat.HeaderSize);
            Check("sample character exists", sampleRec.IsOccupied, true);
            if (sampleRec.IsOccupied)
            {
                Check("sample name starts with C", sampleRec.Name.StartsWith("C"), true);
                Check("sample class is Fighter", sampleRec.Class, CharacterFormat.ClassFighter);
                Check("sample level is 1", sampleRec.Level, 1);
                Check("sample STR is 17", sampleRec.Strength, 17);
                Check("sample DEX is 16", sampleRec.Dexterity, 16);
                Check("sample gold is 100", sampleRec.Gold, 100);
                Check("sample body current is 35", sampleRec.BodyCurrent, 35);
                Check("sample body max is 35", sampleRec.BodyMax, 35);
                Check("sample experience is 0", sampleRec.Experience, 0L);
                Check("sample next level is 1000", sampleRec.NextLevel, 1000L);
                Check("sample magic is 0 (a Fighter)", sampleRec.MagicCurrent, 0);
                Check("sample status is fine", sampleRec.Status, CharacterFormat.StatusFine);
                Check("sample pack is empty", sampleRec.ItemCount, 0);
                Console.WriteLine($"  Sample character: {sampleRec.Name} (L{sampleRec.Level} {sampleRec.ClassName})");
                Console.WriteLine($"  STR={sampleRec.Strength} DEX={sampleRec.Dexterity} CON={sampleRec.Constitution} INT={sampleRec.Intelligence} PIE={sampleRec.Piety}");
                Console.WriteLine($"  Body={sampleRec.BodyCurrent}/{sampleRec.BodyMax} Magic={sampleRec.MagicCurrent}/{sampleRec.MagicMax} XP={sampleRec.Experience}/{sampleRec.NextLevel} Gold={sampleRec.Gold}");
                Console.WriteLine($"  Readied: R={ItemBook.Name(sampleRec.RightHand)} L={ItemBook.Name(sampleRec.LeftHand)} " +
                                  $"Armor={ItemBook.Name(sampleRec.Armor)} Ring={ItemBook.Name(sampleRec.Ring)}");
                Console.WriteLine($"  Pack: {string.Join(", ", sampleRec.Items.Select(ItemBook.Name))}");
            }
            Console.WriteLine("  (sample file checks passed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARNING: could not parse sample DDCHARS.DAT: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("Sample DDCHARS.DAT not found — skipping sample-file checks (not a failure).");
    }
}
else
{
    Console.WriteLine("Game directory not found — skipping sample-file checks (not a failure).");
}
Console.WriteLine();

// =============================================================================
//  MAPS: level format, party position, and the map locator
// =============================================================================

// --- map format constants ----------------------------------------------------
Console.WriteLine("Map format constants:");
Check("map file size", MapFormat.FileSize, 12648);
Check("grid size", MapFormat.GridSize, 32);
Check("directions", MapFormat.Directions, 4);
Check("wall section length", MapFormat.WallsLength, 4096);
Check("contents section length", MapFormat.ContentsLength, 1024);
Check("contents follow the walls", MapFormat.OffWalls + MapFormat.WallsLength, MapFormat.OffContents);
Check("wall X stride", MapFormat.WallStrideX, 128);
Check("wall Y stride", MapFormat.WallStrideY, 4);
// The text block is the tail of the file: 127 forty-byte lines ending exactly on the file size,
// which is what accounts for every one of the 12,648 bytes.
Check("text block ends at the file size",
      MapFormat.OffTextLines + MapFormat.TextLineCount * MapFormat.TextLineSize, MapFormat.FileSize);
Check("the two text index tables are 64 entries apart",
      MapFormat.OffTextCount - MapFormat.OffTextFirst, MapFormat.EventCodeCount);
Check("undecoded span sits between the tables and the text",
      MapFormat.OffTextCount + MapFormat.EventCodeCount, MapFormat.OffUndecoded);
Check("position block size", MapFormat.PositionBlockSize, 8);
Check("levels", MapBook.Levels.Count, 5);
Check("level 3 is the Ground Level", MapBook.LevelName(3), "Ground Level");
Check("level 0 is town", MapBook.LevelName(0), "Town");
Check("map file name", MapBook.MapFileName(3), "DDMAP3.DAT");

// The locator's arithmetic is a chain of data-segment offsets the disassembly pinned; assert the
// deltas against the offsets themselves so a typo in one cannot quietly move the other.
Check("position block sits 0xEFC past the roster array",
      MapFormat.PositionFromRosterArray, 0x1320 - 0x0424);
Check("position block sits 0xEB4 past the file's first record",
      MapFormat.PositionFromRosterFirstFileSlot, 0x1320 - 0x046C);
Check("map buffer sits 0x3DD4 past the position block", MapFormat.MapFromPosition, 0x3DD4);
Check("the two roster deltas differ by one record",
      MapFormat.PositionFromRosterArray - MapFormat.PositionFromRosterFirstFileSlot,
      CharacterFormat.RecordSize);
Console.WriteLine();

// --- indexing ----------------------------------------------------------------
Console.WriteLine("Map indexing:");
Check("first wall byte", MapFormat.WallIndex(0, 0, 0), 0);
Check("last wall byte", MapFormat.WallIndex(31, 31, 3), MapFormat.WallsLength - 1);
Check("first content byte", MapFormat.ContentIndex(0, 0), MapFormat.OffContents);
Check("last content byte", MapFormat.ContentIndex(31, 31), MapFormat.OffContents + MapFormat.ContentsLength - 1);
Check("north then south returns", MapFormat.Opposite(MapFormat.North), MapFormat.South);
Check("east then west returns", MapFormat.Opposite(MapFormat.East), MapFormat.West);
// The deltas are the game's own tables: north walks Y down, east walks X up.
Check("north steps Y down", MapFormat.DeltaY[MapFormat.North], -1);
Check("north holds X", MapFormat.DeltaX[MapFormat.North], 0);
Check("east steps X up", MapFormat.DeltaX[MapFormat.East], 1);
Check("south steps Y up", MapFormat.DeltaY[MapFormat.South], 1);
Check("west steps X down", MapFormat.DeltaX[MapFormat.West], -1);
Check("facing 0 is North", MapFormat.FacingName(0), "North");
Check("facing 3 is West", MapFormat.FacingName(3), "West");
for (int d = 0; d < MapFormat.Directions; d++)
{
    int back = MapFormat.Opposite(d);
    if (MapFormat.DeltaX[d] != -MapFormat.DeltaX[back] || MapFormat.DeltaY[d] != -MapFormat.DeltaY[back])
        Check($"direction {d} and its opposite cancel", false, true);
}
Console.WriteLine("  [OK] every direction and its opposite cancel out");
Console.WriteLine();

// --- wall bytes --------------------------------------------------------------
// Both tables below are the game's, not a judgement call: the classification is the 16-byte array
// the main loop builds on its stack for the automap renderer, and the passability rule is the
// movement routine's own test (0 and 2 outright, 8..16, everything else blocked).
Console.WriteLine("Wall byte semantics:");
Check("0 is open", MapFormat.Classify(0), WallKind.Open);
Check("1 is a wall", MapFormat.Classify(1), WallKind.Wall);
Check("2 is a door", MapFormat.Classify(2), WallKind.Door);
Check("a secret door draws as a wall", MapFormat.Classify(MapFormat.WallSecretDoor), WallKind.Wall);
Check("a found secret door draws as a door", MapFormat.Classify(MapFormat.WallOpenedSecret), WallKind.Door);
Check("an unlocked door draws as a door", MapFormat.Classify(MapFormat.WallUnlocked1), WallKind.Door);
Check("out-of-table values draw as a wall", MapFormat.Classify(200), WallKind.Wall);
Check("open is passable", MapFormat.IsPassable(0), true);
Check("a wall is not", MapFormat.IsPassable(1), false);
Check("a door is passable", MapFormat.IsPassable(2), true);
Check("a locked door is not", MapFormat.IsPassable(MapFormat.WallLocked2), false);
Check("a secret door is not, until it is found", MapFormat.IsPassable(MapFormat.WallSecretDoor), false);
Check("a found secret door is", MapFormat.IsPassable(MapFormat.WallOpenedSecret), true);
Check("16 is the last passable value", MapFormat.IsPassable(16), true);
Check("17 is not", MapFormat.IsPassable(17), false);
Check("key 1 opens lock 3", MapFormat.KeyFor(MapFormat.WallLocked1), 1);
Check("key 3 opens lock 5", MapFormat.KeyFor(MapFormat.WallLocked3), 3);
Check("a plain wall needs no key", MapFormat.KeyFor(1), 0);
Console.WriteLine();

// --- party position ----------------------------------------------------------
Console.WriteLine("Party position encode / decode:");
var pos = new PartyPosition(3, 16, 31, 0);
var posBytes = pos.ToBytes();
Check("encodes to 8 bytes", posBytes.Length, 8);
Check("level word", ReadU16(posBytes, MapFormat.PosOffLevel), 3);
Check("X word", ReadU16(posBytes, MapFormat.PosOffX), 16);
Check("Y word", ReadU16(posBytes, MapFormat.PosOffY), 31);
Check("facing word", ReadU16(posBytes, MapFormat.PosOffFacing), 0);
Check("round-trips", PartyPosition.FromBytes(posBytes), pos);
Check("in the dungeon", pos.InDungeon, true);
Check("is plausible", pos.IsPlausible, true);
Check("town is plausible but not in the dungeon", new PartyPosition(0, 0, 0, 0).InDungeon, false);
Check("town position is still plausible", new PartyPosition(0, 0, 0, 0).IsPlausible, true);
Check("level 6 is not plausible", new PartyPosition(6, 0, 0, 0).IsPlausible, false);
Check("X 32 is not plausible", new PartyPosition(1, 32, 0, 0).IsPlausible, false);
Check("facing 4 is not plausible", new PartyPosition(1, 0, 0, 4).IsPlausible, false);
Check("clamping pulls a wild value into range",
      new PartyPosition(99, -4, 400, 9).Clamped(), new PartyPosition(5, 0, 31, 3));

// The teleport writes X, Y and facing and deliberately leaves the level word alone: the game only
// loads a level's map when it takes a stairway, so moving the level on its own would leave the
// party walking a map it is not on. Drive the exact range MapsViewModel.Teleport uses.
{
    var host = new FakeHost(64);
    new PartyPosition(3, 16, 31, MapFormat.North).WriteTo(host.Mem, 0);
    var target = new PartyPosition(1, 20, 13, MapFormat.East);      // a level the write must ignore
    Check("teleport write accepted",
          ((ICharacterHost)host).WriteBytes(0, target.ToBytes(), MapFormat.PosOffX,
                                            MapFormat.PositionBlockSize - MapFormat.PosOffX), true);
    var landed = PartyPosition.FromBytes(host.Mem, 0);
    Check("teleport moved X", landed.X, 20);
    Check("teleport moved Y", landed.Y, 13);
    Check("teleport turned the party", landed.Facing, MapFormat.East);
    Check("teleport left the level alone", landed.Level, 3);
    Check("teleport touched exactly six bytes",
          MapFormat.PositionBlockSize - MapFormat.PosOffX, 6);
}

// The reveal write is the easier of the two to get wrong: WriteRange applies the offset to both
// source and destination, so a regression to offset 0 would blast 1,024 bytes over the wall
// section of a live map instead of the contents section. Drive the exact call.
{
    var host = new FakeHost(MapFormat.FileSize);
    var original = BuildTestMap();
    Array.Copy(original, host.Mem, MapFormat.FileSize);

    var map = new DungeonMap(host.Mem, 0, 3);
    Check("reveal marks the unmapped squares", map.RevealAll() > 0, true);
    Check("reveal write accepted",
          ((ICharacterHost)host).WriteBytes(0, host.Mem, MapFormat.OffContents, MapFormat.ContentsLength), true);

    bool wallsIntact = host.Mem.AsSpan(MapFormat.OffWalls, MapFormat.WallsLength)
                       .SequenceEqual(original.AsSpan(MapFormat.OffWalls, MapFormat.WallsLength));
    bool textIntact = host.Mem.AsSpan(MapFormat.OffTextLines)
                      .SequenceEqual(original.AsSpan(MapFormat.OffTextLines));
    Check("reveal left the wall section untouched", wallsIntact, true);
    Check("reveal left the text section untouched", textIntact, true);
    Check("reveal only set the mapped bit — event codes survive",
          new DungeonMap(host.Mem, 0, 3).EventCode(5, 5), MapFormat.CodeStairsUp);
    Check("a revealed map still validates", MapFormat.LooksLikeMap(host.Mem, 0), true);
}
Console.WriteLine();

// --- a synthetic level -------------------------------------------------------
Console.WriteLine("Synthetic level decode:");
byte[] mapData = BuildTestMap();
Check("validates as a map", MapFormat.LooksLikeMap(mapData, 0), true);
Check("walls are reciprocal", MapFormat.HasWallReciprocity(mapData, 0), true);

var dm = new DungeonMap(mapData, 0, 3);
Check("level name", dm.LevelName, "Ground Level");
Check("outer north edge is a wall", dm.Wall(4, 0, MapFormat.North), WallKind.Wall);
Check("outer south edge is a wall", dm.Wall(4, 31, MapFormat.South), WallKind.Wall);
Check("the planted door reads as a door", dm.Wall(10, 10, MapFormat.East), WallKind.Door);
Check("and the square east of it agrees", dm.Wall(11, 10, MapFormat.West), WallKind.Door);
Check("the planted door is passable", dm.CanWalk(10, 10, MapFormat.East), true);
Check("the planted secret door is not", dm.CanWalk(12, 10, MapFormat.East), false);
Check("but it draws as a wall", dm.Wall(12, 10, MapFormat.East), WallKind.Wall);
Check("stairs up", dm.Kind(5, 5), SquareKind.StairsUp);
Check("stairs down", dm.Kind(6, 6), SquareKind.StairsDown);
Check("chest", dm.Kind(7, 7), SquareKind.TreasureChest);
Check("item", dm.Kind(8, 8), SquareKind.Item);
Check("edge", dm.Kind(9, 9), SquareKind.Edge);
Check("a described room is not a special square", dm.Kind(3, 3), SquareKind.Plain);
// The game retires a looted square by stamping a code past 0x3F over it — 0xF7 for a chest, 0xF8
// for an item. Both have bit 6 set, so a validator that assumed six-bit codes would reject any map
// the player had actually looted, and a six-bit mask would keep reading them back as treasure.
Check("an emptied chest reads as emptied, not as a chest", dm.Kind(10, 3), SquareKind.Emptied);
Check("an emptied item reads as emptied, not as an item", dm.Kind(11, 3), SquareKind.Emptied);
Check("an emptied square has no event code left", dm.EventCode(10, 3), 0);
Check("an emptied square is still marked visited", dm.IsVisited(10, 3), true);
Check("0xF7 decodes to nothing", MapFormat.DecodeEventCode(0xF7), 0);
Check("0xF8 decodes to nothing", MapFormat.DecodeEventCode(0xF8), 0);
Check("0xF7 is recognised as emptied", MapFormat.IsEmptied(0xF7), true);
Check("a visited chest is not emptied", MapFormat.IsEmptied(MapFormat.VisitedFlag | MapFormat.CodeTreasureChest), false);
Check("a visited chest still decodes as a chest",
      MapFormat.DecodeEventCode(MapFormat.VisitedFlag | MapFormat.CodeTreasureChest), MapFormat.CodeTreasureChest);
Check("the code field is seven bits, not six", MapFormat.EventCodeMask, 0x7F);
Check("its event code", dm.EventCode(3, 3), 1);
Check("visited bit is read, not confused with the code", dm.EventCode(3, 4), 1);
Check("the visited square is visited", dm.IsVisited(3, 4), true);
Check("its neighbour is not", dm.IsVisited(3, 3), false);
// One square walked, plus the looted chest and item the game stamped its mapped bit onto.
Check("visited count", dm.VisitedCount, 3);

var text1 = dm.TextFor(1);
Check("room 1 has three lines", text1.Count, 3);
Check("room 1 name", text1[0], "GREAT HALL");
Check("room 1 body", text1[2], "banners.");
Check("room 2 name", dm.TextFor(2)[0], "GUARD ROOM");
Check("an unused code has no text", dm.TextFor(40).Count, 0);
Check("code 0 has no text", dm.TextFor(0).Count, 0);

var rooms = dm.Rooms();
Check("great hall is listed", rooms.Any(r => r.Name == "GREAT HALL"), true);
Check("great hall covers both squares", rooms.First(r => r.Name == "GREAT HALL").Squares.Count, 2);
Check("stairs up are listed as a place", rooms.Any(r => r.Code == MapFormat.CodeStairsUp), true);
Check("stairs up name", rooms.First(r => r.Code == MapFormat.CodeStairsUp).Name, "Stairs up");
Check("special squares carry no description",
      rooms.First(r => r.Code == MapFormat.CodeStairsUp).Description, "");
Check("stairs-up lookup finds the square",
      dm.SquaresOfKind(SquareKind.StairsUp).Single().Coord, "(5, 5)");
Check("blank squares are left out of the drawing",
      dm.DrawableSquares().Any(s => s.X == 20 && s.Y == 20), false);
Check("the stairs square is drawn",
      dm.DrawableSquares().Any(s => s.X == 5 && s.Y == 5), true);

int revealed = dm.RevealAll();
Check("reveal marks the rest of the level", revealed, MapFormat.ContentsLength - 3);
Check("and then everything is mapped", dm.VisitedCount, MapFormat.ContentsLength);
Check("revealing again changes nothing", dm.RevealAll(), 0);
Check("revealing does not disturb the event codes", dm.EventCode(5, 5), MapFormat.CodeStairsUp);
Check("a revealed map still validates", MapFormat.LooksLikeMap(mapData, 0), true);
Console.WriteLine();

// --- rejecting things that are not levels -----------------------------------
Console.WriteLine("Map validation rejects:");
Check("all zeros", MapFormat.LooksLikeMap(new byte[MapFormat.FileSize], 0), false);
Check("a short buffer", MapFormat.LooksLikeMap(new byte[MapFormat.FileSize - 1], 0), false);
{
    // One wall byte edited from one side only: exactly what a bad address, or a partial read
    // landing mid-structure, looks like — and the single strongest signal that this is not a map.
    var broken = (byte[])BuildTestMap().Clone();
    broken[MapFormat.WallIndex(10, 10, MapFormat.East)] = 1;
    Check("a one-sided wall edit", MapFormat.LooksLikeMap(broken, 0), false);
    Check("and reciprocity says why", MapFormat.HasWallReciprocity(broken, 0), false);
}
{
    var broken = (byte[])BuildTestMap().Clone();
    broken[MapFormat.WallIndex(3, 3, MapFormat.North)] = MapFormat.MaxWallValue + 1;
    Check("a wall byte the game would not walk", MapFormat.LooksLikeMap(broken, 0), false);
}
{
    // An *unvisited* square still holds the code the file shipped, which cannot exceed 0x3F.
    // A visited one constrains nothing, because the game writes 0xF7/0xF8 over looted squares —
    // which is why this check is conditioned on the mapped bit rather than on bit 6.
    var broken = (byte[])BuildTestMap().Clone();
    broken[MapFormat.ContentIndex(3, 3)] = 0x40;
    Check("an unvisited square with a code above 0x3F", MapFormat.LooksLikeMap(broken, 0), false);

    var looted = (byte[])BuildTestMap().Clone();
    looted[MapFormat.ContentIndex(3, 3)] = 0xF7;
    Check("but a looted (visited) square is fine", MapFormat.LooksLikeMap(looted, 0), true);
    Check("as is a taken item square",
          MapFormat.IsPlausibleContentByte(0xF8), true);
    Check("and an unvisited 0x78 is not", MapFormat.IsPlausibleContentByte(0x78), false);
}
{
    // The regression that started this: a level the player had looted must still locate.
    var played = (byte[])BuildTestMap().Clone();
    played[MapFormat.ContentIndex(7, 7)] = 0xF7;    // the chest, opened
    played[MapFormat.ContentIndex(8, 8)] = 0xF8;    // the item, taken
    Check("a level whose chest and item have been taken still validates",
          MapFormat.LooksLikeMap(played, 0), true);
    Check("and still passes the sweep's quick probe",
          MapFormat.PassesReciprocityProbe(played, 0), true);
}
{
    // The text tables are what pin the buffer's byte alignment; wall reciprocity cannot, because it
    // only relates squares a fixed distance apart and so survives sliding the whole grid. Each of
    // these corruptions is one the alignment check has to catch on its own.
    var runsPastEnd = (byte[])BuildTestMap().Clone();
    runsPastEnd[MapFormat.OffTextFirst + 1] = MapFormat.TextLineCount - 1;
    runsPastEnd[MapFormat.OffTextCount + 1] = 5;               // would read past the last line
    Check("a text run that overruns the block", MapFormat.HasConsistentTextTables(runsPastEnd, 0), false);
    Check("and LooksLikeMap refuses it too", MapFormat.LooksLikeMap(runsPastEnd, 0), false);
    Check("even though its walls are still perfect", MapFormat.HasWallReciprocity(runsPastEnd, 0), true);

    var runsFromZero = (byte[])BuildTestMap().Clone();
    runsFromZero[MapFormat.OffTextFirst + 1] = 0;              // the game would read line -1
    Check("a text run starting before line 1", MapFormat.HasConsistentTextTables(runsFromZero, 0), false);

    var badLength = (byte[])BuildTestMap().Clone();
    badLength[MapFormat.OffTextLines + 40 * MapFormat.TextLineSize] = MapFormat.TextLineSize;
    Check("a line whose length prefix overflows its slot",
          MapFormat.HasConsistentTextTables(badLength, 0), false);

    Check("a good map has consistent tables", MapFormat.HasConsistentTextTables(BuildTestMap(), 0), true);
}
{
    // The probe is the sweep's fast reject; it has to actually catch a broken map.
    var broken = (byte[])BuildTestMap().Clone();
    broken[MapFormat.WallIndex(0, 0, MapFormat.East)] = 3;
    Check("the reciprocity probe rejects a one-sided edit", MapFormat.PassesReciprocityProbe(broken, 0), false);
    Check("the probe accepts a good map", MapFormat.PassesReciprocityProbe(BuildTestMap(), 0), true);
    // All-zero memory is trivially reciprocal, so the probe waves it through by design — the
    // nonzero-wall floor in LooksLikeMap is what rejects it. Pinned so nobody "fixes" the probe.
    Check("the probe alone lets a blank buffer past",
          MapFormat.PassesReciprocityProbe(new byte[MapFormat.FileSize], 0), true);
    Check("and the full check still rejects it", MapFormat.LooksLikeMap(new byte[MapFormat.FileSize], 0), false);
}
{
    var bare = (byte[])BuildTestMap().Clone();
    Array.Clear(bare, MapFormat.OffContents, MapFormat.ContentsLength);
    Check("a level with no square contents at all", MapFormat.LooksLikeMap(bare, 0), false);
}
{
    var thin = new byte[MapFormat.FileSize];
    for (int i = 0; i < MapFormat.MinWallBytes - 1; i++) thin[i] = 1;   // not reciprocal, but the
    thin[MapFormat.ContentIndex(0, 0)] = 1;                             // count filter fires first
    Check("too few walls to be a level", MapFormat.LooksLikeMap(thin, 0), false);
}
Console.WriteLine();

// --- the locator over a synthetic address space ------------------------------
Console.WriteLine("Map locator:");
{
    const int Base = 0x100000;
    const int Size = 6 * 1024 * 1024;
    // Put the roster where the in-memory array starts, so the position block is one full record
    // further from it than the file-anchored case — the delta the locator has to pick between.
    const int RosterAt = 0x40000;
    int positionAt = RosterAt + MapFormat.PositionFromRosterArray;
    int mapAt = positionAt + MapFormat.MapFromPosition;

    var space = new byte[Size];
    Array.Copy(BuildTestMap(), 0, space, mapAt, MapFormat.FileSize);
    new PartyPosition(3, 16, 31, MapFormat.North).WriteTo(space, positionAt);

    var fake = new FakeMemory(space, (nuint)Base);

    var fromRoster = MapLocator.FindFromRoster(fake, (nuint)(Base + RosterAt));
    Check("found from the roster", fromRoster != null, true);
    if (fromRoster != null)
    {
        Check("position address", (ulong)fromRoster.PositionAddress, (ulong)(Base + positionAt));
        Check("map address", (ulong)fromRoster.MapAddress, (ulong)(Base + mapAt));
        Check("decoded position", fromRoster.Position, new PartyPosition(3, 16, 31, 0));
        Check("method", fromRoster.Method, MapLocateMethod.Roster);
    }

    // The same space, anchored on the record the file's first character occupies instead: the other
    // delta has to be the one that lands.
    var fromFileSlot = MapLocator.FindFromRoster(
        fake, (nuint)(Base + positionAt - MapFormat.PositionFromRosterFirstFileSlot));
    Check("found from a roster anchored one record later", fromFileSlot != null, true);
    if (fromFileSlot != null)
        Check("same position address either way",
              (ulong)fromFileSlot.PositionAddress, (ulong)(Base + positionAt));

    // A roster address that leads nowhere must report nothing rather than a confident guess.
    Check("a wrong roster address finds nothing",
          MapLocator.FindFromRoster(fake, (nuint)(Base + RosterAt + 0x1000)) == null, true);

    // An implausible position block is rejected even though the map behind it is real.
    {
        var spoiled = (byte[])space.Clone();
        WriteU16(spoiled, positionAt + MapFormat.PosOffLevel, 9);   // no such level
        var f = new FakeMemory(spoiled, (nuint)Base);
        Check("an out-of-range level is rejected",
              MapLocator.FindFromRoster(f, (nuint)(Base + RosterAt)) == null, true);
    }
    {
        var spoiled = (byte[])space.Clone();
        WriteU16(spoiled, positionAt + MapFormat.PosOffX, 40);      // off the 32×32 grid
        var f = new FakeMemory(spoiled, (nuint)Base);
        Check("an off-grid X is rejected",
              MapLocator.FindFromRoster(f, (nuint)(Base + RosterAt)) == null, true);
    }
    {
        // Before the party first walks into the castle the map buffer is empty, and there is
        // nothing left to recognise: a zeroed position block reads as a perfectly plausible "in
        // town", so accepting a merely in-range buffer would let the wrong address through. This
        // deliberately reports nothing rather than guess.
        var blank = (byte[])space.Clone();
        Array.Clear(blank, mapAt, MapFormat.FileSize);
        WriteU16(blank, positionAt + MapFormat.PosOffLevel, MapFormat.TownLevel);
        var f = new FakeMemory(blank, (nuint)Base);
        Check("with no map loaded there is nothing to find",
              MapLocator.FindFromRoster(f, (nuint)(Base + RosterAt)) == null, true);

        var stillBlank = (byte[])blank.Clone();
        WriteU16(stillBlank, positionAt + MapFormat.PosOffLevel, 3);   // claims to be underground
        var f2 = new FakeMemory(stillBlank, (nuint)Base);
        Check("and a party claiming to be in the castle with no map is rejected too",
              MapLocator.FindFromRoster(f2, (nuint)(Base + RosterAt)) == null, true);
    }
    {
        // The trap the strict rule exists for: the roster scan can anchor one record early or
        // late, so the other delta lands the map window a record away from the real one. Every
        // byte in that window is still in range, and the position block in front of it is zeros
        // that read as "in town" — only reciprocity rejects it.
        var shifted = (byte[])space.Clone();
        WriteU16(shifted, positionAt + MapFormat.PosOffLevel, 9);   // spoil the real candidate
        var f = new FakeMemory(shifted, (nuint)Base);
        Check("a map window shifted by one record is not mistaken for the real one",
              MapLocator.FindFromRoster(f, (nuint)(Base + RosterAt)) == null, true);
    }

    // Structural sweep: no roster at all, and the map deliberately straddles a 1 MiB scan seam.
    {
        var seam = new byte[Size];
        int seamMapAt = (1 << 20) - (MapFormat.FileSize / 2);          // half in each chunk
        int seamPosAt = seamMapAt - MapFormat.MapFromPosition;
        Array.Copy(BuildTestMap(), 0, seam, seamMapAt, MapFormat.FileSize);
        new PartyPosition(2, 7, 8, MapFormat.West).WriteTo(seam, seamPosAt);

        var f = new FakeMemory(seam, (nuint)Base);
        var found = MapLocator.FindByStructure(f);
        Check("structural sweep finds a map across a chunk seam", found != null, true);
        if (found != null)
        {
            Check("structural map address", (ulong)found.MapAddress, (ulong)(Base + seamMapAt));
            Check("structural position", found.Position, new PartyPosition(2, 7, 8, 3));
            Check("structural method", found.Method, MapLocateMethod.Structural);
        }

        Check("structural sweep finds nothing in empty memory",
              MapLocator.FindByStructure(new FakeMemory(new byte[Size], (nuint)Base)) == null, true);

        // The position block precedes the map, so a map that close to address zero cannot be real
        // and must not be reached for by wrapping the address around.
        var low = new byte[MapFormat.FileSize + 64];
        Array.Copy(BuildTestMap(), 0, low, 16, MapFormat.FileSize);
        Check("a map too close to address zero is skipped, not wrapped",
              MapLocator.FindByStructure(new FakeMemory(low, 0)) == null, true);

        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool threw = false;
        try { MapLocator.FindByStructure(f, cancelled.Token); }
        catch (OperationCanceledException) { threw = true; }
        Check("structural sweep honours cancellation", threw, true);

        // Past the first scan window, so the sweep has to step correctly rather than finding it in
        // the very first chunk it reads.
        var far = new byte[Size];
        int farMapAt = (2 << 20) + 5000;
        Array.Copy(BuildTestMap(), 0, far, farMapAt, MapFormat.FileSize);
        new PartyPosition(4, 3, 4, MapFormat.South).WriteTo(far, farMapAt - MapFormat.MapFromPosition);
        var farFound = MapLocator.FindByStructure(new FakeMemory(far, (nuint)Base));
        Check("structural sweep steps past the first window", farFound != null, true);
        if (farFound != null)
            Check("and finds the map there", (ulong)farFound.MapAddress, (ulong)(Base + farMapAt));
    }

    // A region the target will only serve in pieces. ProcessMemory reads all-or-nothing, and the
    // sweep halves its window down on failure — so a readable prefix that lands *between* two
    // halving steps (13,000 bytes: more than one map, less than the 16 KB trial above it) fails
    // every attempt unless the halving ends with an explicit try at exactly one map's worth.
    // Without that final try the sweep walks straight past a map at the head of such a region.
    {
        const int CappedMapAt = 3 << 20;
        var capped = new byte[Size];
        Array.Copy(BuildTestMap(), 0, capped, CappedMapAt, MapFormat.FileSize);
        new PartyPosition(5, 11, 12, MapFormat.South).WriteTo(capped, CappedMapAt - MapFormat.MapFromPosition);

        // Two regions, so the sweep's window for the second one starts exactly at the map — the
        // position block still reads fine, since reads do not care about region boundaries.
        var f = new FakeMemory(capped, (nuint)Base)
        {
            MaxReadable = 13000,
            Regions = new[] { (0, CappedMapAt), (CappedMapAt, MapFormat.FileSize) },
        };
        var found = MapLocator.FindByStructure(f);
        Check("a region that only reads 13,000 bytes at a time is still swept", found != null, true);
        if (found != null)
        {
            Check("and the map in it is found", (ulong)found.MapAddress, (ulong)(Base + CappedMapAt));
            Check("with its position", found.Position, new PartyPosition(5, 11, 12, 2));
        }
    }

    // The candidate filter on its own, so a change to the fast path is caught even if the sweep
    // around it still happens to work.
    {
        var buf = new byte[MapFormat.FileSize * 3];
        int at = 1234;
        Array.Copy(BuildTestMap(), 0, buf, at, MapFormat.FileSize);
        var hits = MapLocator.FindCandidates(buf, buf.Length).ToList();
        Check("candidate filter finds the planted map", hits.Contains(at), true);
        Check("and nothing else", hits.Count, 1);
    }

    // Reading a located position back is what the poll loop does every tick. The two failure modes
    // are kept apart on purpose: an unreadable address means the game is gone, whereas bytes that
    // simply do not decode is a state the game puts itself in — stepping off a ledge on the bottom
    // level increments the level past 5 — and must not tear down a good locate.
    {
        Check("position re-read",
              MapLocator.TryReadPosition(fake, (nuint)(Base + positionAt), out var live),
              MapLocator.ReadOutcome.Ok);
        Check("re-read value", live, new PartyPosition(3, 16, 31, 0));
        Check("re-read of unreadable memory reports Unreadable",
              MapLocator.TryReadPosition(fake, (nuint)(Base + Size + 0x1000), out _),
              MapLocator.ReadOutcome.Unreadable);

        var offLedge = (byte[])space.Clone();
        WriteU16(offLedge, positionAt + MapFormat.PosOffLevel, 6);   // what walking off the bottom does
        Check("a level past the bottom reports Implausible, not Unreadable",
              MapLocator.TryReadPosition(new FakeMemory(offLedge, (nuint)Base),
                                         (nuint)(Base + positionAt), out _),
              MapLocator.ReadOutcome.Implausible);
    }

    // Re-validating before a write is what stops a cached address outliving the layout it belongs
    // to: the position's four values are only range-checked, so the map behind them has to agree.
    {
        Check("revalidate accepts a live locate",
              MapLocator.TryRevalidate(fake, (nuint)(Base + positionAt), out var live, out var bytes), true);
        Check("and hands back the bytes it checked", bytes.Length, MapFormat.FileSize);
        Check("with the position", live, new PartyPosition(3, 16, 31, 0));

        // A position block that still range-checks, over a map that no longer does.
        var rotted = (byte[])space.Clone();
        rotted[mapAt + MapFormat.WallIndex(10, 10, MapFormat.East)] = 1;   // break reciprocity
        Check("revalidate rejects a position whose map stopped decoding",
              MapLocator.TryRevalidate(new FakeMemory(rotted, (nuint)Base),
                                       (nuint)(Base + positionAt), out _, out _), false);
    }
}
Console.WriteLine();

// --- the saved position in DDCHARS.DAT ---------------------------------------
Console.WriteLine("Saved party position:");
{
    string dir = Path.Combine(Path.GetTempPath(), "dd1-mapcheck-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        // A file whose header carries the position the shipped sample has: Ground Level, the
        // castle entrance at the bottom edge, facing north into the gate.
        string path = Path.Combine(dir, "DDCHARS.DAT");
        var data = new byte[CharacterFormat.FileSize];
        PlantCharacter(data, CharacterFormat.HeaderSize, "TESTER", CharacterFormat.ClassFighter, 100);
        WriteU16(data, CharacterFormat.HdrOffPartySlots, 1);
        new PartyPosition(3, 16, 31, MapFormat.North).WriteTo(data, CharacterFormat.HdrOffPosition);
        data[CharacterFormat.HdrOffUnknown] = 0xAB;      // a byte nothing decodes — must survive
        File.WriteAllBytes(path, data);

        using (var sf = new SaveFile(path))
        {
            Check("reads the saved position", sf.Position, new PartyPosition(3, 16, 31, 0));
            Check("party position 1 holds roster slot 1", sf.PartySlot(1), 1);
            Check("party position 2 is empty", sf.PartySlot(2), 0);
            Check("out-of-range party position reads 0", sf.PartySlot(9), 0);

            sf.Position = new PartyPosition(5, 9, 1, MapFormat.East);
            sf.Save();
        }

        var written = File.ReadAllBytes(path);
        Check("the edit reached the file",
              PartyPosition.FromBytes(written, CharacterFormat.HdrOffPosition),
              new PartyPosition(5, 9, 1, 1));
        Check("undecoded header bytes round-trip", written[CharacterFormat.HdrOffUnknown], (byte)0xAB);
        Check("the party slots are left alone", ReadU16(written, CharacterFormat.HdrOffPartySlots), 1);
        Check("a .bak was taken", File.Exists(path + ".bak"), true);
        Check("the .bak holds the original position",
              PartyPosition.FromBytes(File.ReadAllBytes(path + ".bak"), CharacterFormat.HdrOffPosition),
              new PartyPosition(3, 16, 31, 0));
        Check("the character survived the header edit",
              new SaveFile(path).Characters[0].Name, "TESTER");
    }
    finally
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}
Console.WriteLine();

// --- the shipped map files ---------------------------------------------------
if (gameDir != null)
{
    Console.WriteLine("Shipped DDMAP files:");
    if (MapBook.TryLoadFromFolder(gameDir, out var shipped, out var mapError))
    {
        Check("all five levels parse", shipped.Count, 5);
        foreach (var (level, map) in shipped.OrderBy(kv => kv.Key))
        {
            Check($"{MapBook.MapFileName(level)} walls are reciprocal",
                  MapFormat.HasWallReciprocity(map.Bytes, map.Offset), true);
            var up = map.SquaresOfKind(SquareKind.StairsUp);
            var down = map.SquaresOfKind(SquareKind.StairsDown);
            Console.WriteLine($"  {MapBook.LevelName(level),-16} rooms={map.Rooms().Count,3}  " +
                              $"up={Describe(up)}  down={Describe(down)}  " +
                              $"chests={map.SquaresOfKind(SquareKind.TreasureChest).Count}  " +
                              $"visited={map.VisitedCount}");
            // Going down increases the level number, so the top level has no way up and the
            // bottom no way down — the same ordering the level-name table prints.
            if (level == MapFormat.MinLevel) Check("  the top level has no stairs up", up.Count, 0);
            if (level == MapFormat.MaxLevel) Check("  the bottom level has no stairs down", down.Count, 0);
        }

        // The game hard-codes two squares by coordinate: the quest staff on the top level and the
        // other fixed item at the bottom. Both must land on a square the map marks as an item.
        if (shipped.TryGetValue(1, out var top))
            Check("THE STAFF sits on the item square the game names (20, 22)",
                  top.Kind(20, 22), SquareKind.Item);
        if (shipped.TryGetValue(5, out var bottom))
            Check("the deep item sits on the square the game names (9, 1)",
                  bottom.Kind(9, 1), SquareKind.Item);

        // The description text is indexed by a per-code line number, and the synthetic fixture is
        // built with the decoder's own convention — so only the real files can prove the decoder
        // reads the same lines the game does. A shift of one would take the first line from the
        // previous room and cut this room's last sentence off mid-clause, so assert both ends.
        if (shipped.TryGetValue(3, out var ground))
        {
            var gate = ground.TextFor(1);
            Check("DDMAP3 room 1 is the MAIN GATE", gate.Count > 0 ? gate[0] : "", "MAIN GATE");
            Check("and its description runs to the end of its own sentence",
                  gate.Count > 0 ? gate[^1] : "", "bastillion towers.");
            Check("the square by the entrance carries that room",
                  ground.Square(15, 29).RoomName, "MAIN GATE");
            Check("the room list names it too",
                  ground.Rooms().Any(r => r.Name == "MAIN GATE"), true);
        }
        if (shipped.TryGetValue(1, out var top2))
        {
            var stair = top2.TextFor(2);
            Check("DDMAP1 room 2 is the STAIRWAY DOWN", stair.Count > 0 ? stair[0] : "", "STAIRWAY DOWN");
        }

        // The regression that a live run caught and 537 synthetic checks did not. In memory the map
        // buffer is preceded by a few hundred zero bytes, and wall reciprocity — being a relation
        // between squares a fixed distance apart — holds just as well for the whole grid slid along
        // by whole squares. Measured against the running game, 113 offsets around the real buffer
        // passed the wall test. Reproduce that here with a real level and assert that only the true
        // offset survives the full check.
        if (shipped.TryGetValue(3, out var real))
        {
            const int Pad = 512;
            var padded = new byte[Pad + MapFormat.FileSize];
            Array.Copy(real.Bytes, real.Offset, padded, Pad, MapFormat.FileSize);

            int reciprocal = 0, validated = 0;
            for (int shift = 0; shift <= Pad; shift += MapFormat.WallStrideY)   // whole squares
            {
                int at = Pad - shift;
                if (MapFormat.HasWallReciprocity(padded, at)) reciprocal++;
                if (MapFormat.LooksLikeMap(padded, at)) validated++;
            }
            Console.WriteLine($"  shifted windows over a real level: {reciprocal} pass the wall test, {validated} pass in full");
            Check("more than one shifted window fools wall reciprocity alone", reciprocal > 1, true);
            Check("but exactly one passes the full check", validated, 1);
            Check("and it is the true offset", MapFormat.LooksLikeMap(padded, Pad), true);
        }
    }
    else
    {
        Console.WriteLine($"  WARNING: {mapError}");
    }
    Console.WriteLine();
}
else
{
    Console.WriteLine("Game directory not found — skipping shipped-map checks (not a failure).");
    Console.WriteLine();
}

// --- summary -----------------------------------------------------------------
Console.WriteLine($"=== {failures} failure(s) ===");
return failures == 0 ? 0 : 1;

// --- helpers -----------------------------------------------------------------
static void WriteU16(byte[] buf, int offset, int value)
{
    buf[offset] = (byte)(value & 0xFF);
    buf[offset + 1] = (byte)((value >> 8) & 0xFF);
}

static void WriteU32(byte[] buf, int offset, long value)
{
    buf[offset] = (byte)(value & 0xFF);
    buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    buf[offset + 2] = (byte)((value >> 16) & 0xFF);
    buf[offset + 3] = (byte)((value >> 24) & 0xFF);
}

static int ReadU16(byte[] buf, int offset) => buf[offset] | (buf[offset + 1] << 8);

/// <summary>Writes a minimal valid character record into a buffer at <paramref name="at"/>.</summary>
static void PlantCharacter(byte[] buf, int at, string name, int cls, int gold)
{
    Array.Clear(buf, at, CharacterFormat.RecordSize);
    buf[at + CharacterFormat.OffExists] = 1;
    buf[at + CharacterFormat.OffNameLen] = (byte)name.Length;
    System.Text.Encoding.ASCII.GetBytes(name).CopyTo(buf, at + CharacterFormat.OffName);
    buf[at + CharacterFormat.OffStatus] = CharacterFormat.StatusFine;
    buf[at + CharacterFormat.OffClass] = (byte)cls;
    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        WriteU16(buf, at + CharacterFormat.AttributeOffsets[i], 14);
    WriteU16(buf, at + CharacterFormat.OffLevel, 1);
    WriteU16(buf, at + CharacterFormat.OffBodyCur, 20);
    WriteU16(buf, at + CharacterFormat.OffBodyMax, 20);
    WriteU16(buf, at + CharacterFormat.OffGold, gold);
}

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    string status = ok ? "OK" : "FAIL";
    Console.WriteLine($"  [{status}] {label}: got {actual}, expected {expected}");
    if (!ok) failures++;
}

// Probabilities are built by different summations on each side of a comparison, so they are
// compared to within floating-point noise rather than for exact equality.
void CheckClose(string label, double actual, double expected, double tolerance = 1e-12)
{
    bool ok = Math.Abs(actual - expected) <= tolerance;
    string status = ok ? "OK" : "FAIL";
    Console.WriteLine($"  [{status}] {label}: got {actual:G10}, expected {expected:G10}");
    if (!ok) failures++;
}

// The odds, the slow and obvious way: every ordered outcome of five rolled values, weighted by its
// probability and judged by the same CreationFormat.MeetsTarget the roller stops on.
static double BruteForce(int[] mins, int totalMin)
{
    var v = new int[CreationFormat.RolledCount];
    double p = 0;
    for (v[0] = CreationFormat.MinRoll; v[0] <= CreationFormat.MaxRoll; v[0]++)
    for (v[1] = CreationFormat.MinRoll; v[1] <= CreationFormat.MaxRoll; v[1]++)
    for (v[2] = CreationFormat.MinRoll; v[2] <= CreationFormat.MaxRoll; v[2]++)
    for (v[3] = CreationFormat.MinRoll; v[3] <= CreationFormat.MaxRoll; v[3]++)
    for (v[4] = CreationFormat.MinRoll; v[4] <= CreationFormat.MaxRoll; v[4]++)
    {
        if (!CreationFormat.MeetsTarget(v, mins, totalMin)) continue;
        double w = 1;
        foreach (var x in v) w *= RollOdds.P(x);
        p += w;
    }
    return p;
}

/// <summary>Renders a list of squares as coordinates, for the shipped-map summary lines.</summary>
static string Describe(IReadOnlyList<MapSquare> squares) =>
    squares.Count == 0 ? "-" : string.Join(" ", squares.Select(s => s.Coord));

/// <summary>
/// Builds a synthetic Dark Designs level: a walled border, a wall down every other column, a door,
/// a secret door, one square of each special kind, and two described rooms with their text. Every
/// wall is written through <c>SetWall</c>, which sets both sides, so the result satisfies the
/// reciprocity invariant the locator leans on — that is the point of building it this way.
/// </summary>
static byte[] BuildTestMap()
{
    var m = new byte[MapFormat.FileSize];

    void SetWall(int x, int y, int d, int v)
    {
        m[MapFormat.WallIndex(x, y, d)] = (byte)v;
        int nx = x + MapFormat.DeltaX[d], ny = y + MapFormat.DeltaY[d];
        if (MapFormat.InBounds(nx, ny)) m[MapFormat.WallIndex(nx, ny, MapFormat.Opposite(d))] = (byte)v;
    }

    for (int i = 0; i < MapFormat.GridSize; i++)
    {
        SetWall(i, 0, MapFormat.North, MapFormat.WallSolid);
        SetWall(i, MapFormat.GridSize - 1, MapFormat.South, MapFormat.WallSolid);
        SetWall(0, i, MapFormat.West, MapFormat.WallSolid);
        SetWall(MapFormat.GridSize - 1, i, MapFormat.East, MapFormat.WallSolid);
    }
    // Walls down the western half only, so the eastern half has genuinely blank squares to prove
    // the drawing pass leaves them out.
    for (int x = 2; x < 16; x += 2)
        for (int y = 0; y < MapFormat.GridSize; y++)
            SetWall(x, y, MapFormat.West, MapFormat.WallSolid);

    SetWall(10, 10, MapFormat.East, MapFormat.WallDoor);
    SetWall(12, 10, MapFormat.East, MapFormat.WallSecretDoor);

    void SetContent(int x, int y, int code, bool visited = false)
        => m[MapFormat.ContentIndex(x, y)] = (byte)(code | (visited ? MapFormat.VisitedFlag : 0));

    SetContent(3, 3, 1);
    SetContent(3, 4, 1, visited: true);
    SetContent(4, 4, 2);
    // A looted chest and a taken item, exactly as the game stamps them: the whole byte, mapped bit
    // and all, with a code past 0x3F.
    m[MapFormat.ContentIndex(10, 3)] = 0xF7;
    m[MapFormat.ContentIndex(11, 3)] = 0xF8;
    SetContent(5, 5, MapFormat.CodeStairsUp);
    SetContent(6, 6, MapFormat.CodeStairsDown);
    SetContent(7, 7, MapFormat.CodeTreasureChest);
    SetContent(8, 8, MapFormat.CodeItem);
    SetContent(9, 9, MapFormat.CodeEdge);

    // Room 1 uses lines 1-3, room 2 lines 4-5 — the game reads `first .. first + count - 1`.
    m[MapFormat.OffTextFirst + 1] = 1; m[MapFormat.OffTextCount + 1] = 3;
    m[MapFormat.OffTextFirst + 2] = 4; m[MapFormat.OffTextCount + 2] = 2;
    WriteTextLine(m, 1, "GREAT HALL");
    WriteTextLine(m, 2, "A long hall with");
    WriteTextLine(m, 3, "banners.");
    WriteTextLine(m, 4, "GUARD ROOM");
    WriteTextLine(m, 5, "Two bunks.");
    return m;
}

/// <summary>
/// Writes one 40-byte text slot the way the game stores them: a length byte, the text, the game's
/// <c>]</c> end-of-line marker, and <c>0x02</c> padding — all three of which the decoder has to
/// drop rather than print.
/// </summary>
static void WriteTextLine(byte[] m, int line, string text)
{
    int at = MapFormat.OffTextLines + line * MapFormat.TextLineSize;
    int len = MapFormat.TextLineSize - 1;
    m[at] = (byte)len;
    for (int i = 0; i < len; i++) m[at + 1 + i] = 0x02;
    var bytes = System.Text.Encoding.ASCII.GetBytes(text);
    Array.Copy(bytes, 0, m, at + 1, Math.Min(bytes.Length, len - 1));
    m[at + 1 + Math.Min(bytes.Length, len - 1)] = (byte)']';
}

static string? FindGameDir()
{
    string[] candidates =
    {
        @"C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\DARKDES1",
    };
    foreach (var c in candidates)
        if (Directory.Exists(c)) return c;
    return null;
}

/// <summary>Minimal <see cref="IItemPack"/> over a bare record, for the duplicate checks.</summary>
sealed class TestPack : IItemPack
{
    private readonly CharacterRecord _rec;
    public TestPack(CharacterRecord rec) => _rec = rec;
    public bool HasFreeSlot => _rec.ItemCount < CharacterFormat.ItemSlotCount;
    public bool TryAddItem(int itemId) => itemId != 0 && _rec.AddItem(itemId) >= 0;
}

/// <summary>
/// A flat byte array standing in for the emulator's address space, so <see cref="MapLocator"/> can
/// be driven without a running game — including the cases a real process could not be made to
/// produce on demand, such as a map buffer straddling a scan seam or sitting near address zero.
/// Reads are all-or-nothing, matching <c>ProcessMemory</c>.
/// </summary>
sealed class FakeMemory : IMemorySource
{
    private readonly byte[] _mem;
    private readonly nuint _base;

    public FakeMemory(byte[] mem, nuint baseAddress)
    {
        _mem = mem;
        _base = baseAddress;
    }

    /// <summary>
    /// Largest single read the fixture will satisfy, standing in for a region whose tail is not
    /// committed. Reads are all-or-nothing either way, matching <c>ProcessMemory</c>.
    /// </summary>
    public int MaxReadable { get; init; } = int.MaxValue;

    /// <summary>
    /// Which spans of the array the sweep is told about, as (start, length) pairs relative to the
    /// base address. Defaults to one region covering everything. Reads ignore this — a real
    /// process happily reads across a region boundary too.
    /// </summary>
    public IReadOnlyList<(int Start, int Length)>? Regions { get; init; }

    public IEnumerable<MemoryRegion> EnumerateRegions()
    {
        if (Regions == null)
        {
            yield return new MemoryRegion(_base, (nuint)_mem.Length);
            yield break;
        }
        foreach (var (start, length) in Regions)
            yield return new MemoryRegion(_base + (nuint)start, (nuint)length);
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        if (address < _base) return 0;
        long at = (long)(address - _base);
        if (at < 0 || count < 0 || at + count > _mem.Length) return 0;
        if (count > MaxReadable) return 0;
        Array.Copy(_mem, at, buffer, 0, count);
        return count;
    }

    public byte[] Read(nuint address, int count)
    {
        var buf = new byte[count];
        return Read(address, buf, count) == count ? buf : Array.Empty<byte>();
    }
}

/// <summary>
/// A flat byte array standing in for the game's address space, so the mirror-staleness logic can
/// be exercised without a process. Mirrors <c>ProcessMemory.WriteRange</c>'s convention of writing
/// <c>source[offset..offset+length]</c> at <c>address + offset</c>.
/// </summary>
sealed class FakeHost : ICharacterHost
{
    public byte[] Mem { get; }
    public int WriteCount { get; private set; }

    public FakeHost(int size) => Mem = new byte[size];

    public bool IsAttached => true;

    public bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
    {
        int at = (int)recordAddress + offset;
        if (at < 0 || at + length > Mem.Length) return false;
        Array.Copy(source, offset, Mem, at, length);
        WriteCount++;
        return true;
    }

    public bool ReadBytes(nuint address, byte[] destination, int length)
    {
        int at = (int)address;
        if (at < 0 || at + length > Mem.Length) return false;
        Array.Copy(Mem, at, destination, 0, length);
        return true;
    }
}
