using System.IO;
using System.Text;
using LegendOfFaerghailTrainer.FormatCheck;
using LegendOfFaerghailTrainer.Game;
using LegendOfFaerghailTrainer.Memory;
using LegendOfFaerghailTrainer.ViewModels;

// Headless verification harness for the Legend of Faerghail trainer. Runs with no emulator and no
// copyrighted game files present; if a copy of the game is supplied with --game <dir> it also parses
// ROST\ROST and GAMES\GAMEn and checks them against the same format constants.
//
//   dotnet run --project test\FormatCheck
//   dotnet run --project test\FormatCheck -- --game "C:\...\GAMES\LOF"
//   dotnet run --project test\FormatCheck -- --live <dosbox-pid>
//
// Exits 0 (all checks passed) or 1 (at least one failed).

int passed = 0, failed = 0, skipped = 0;
var failures = new List<string>();

void Check(string what, bool ok)
{
    if (ok) passed++;
    else { failed++; failures.Add(what); }
}

void CheckEq<T>(string what, T actual, T expected) =>
    Check($"{what}: expected {expected}, got {actual}", EqualityComparer<T>.Default.Equals(actual, expected));

void Section(string name) => Console.WriteLine($"\n== {name} ==");

string? gameDir = null;
int livePid = 0;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--game" && i + 1 < args.Length) gameDir = args[++i];
    else if (args[i] == "--live" && i + 1 < args.Length && int.TryParse(args[i + 1], out int pid)) { livePid = pid; i++; }
}

// ---------------------------------------------------------------- format constants
Section("Format constants");
CheckEq("record size", CharacterFormat.RecordSize, 410);
CheckEq("party slots", CharacterFormat.PartySlots, 6);
CheckEq("roster slots", CharacterFormat.RosterSlots, 32);
CheckEq("name field length", CharacterFormat.NameFieldLength, 14);
CheckEq("max name length", CharacterFormat.MaxNameLength, 10);
CheckEq("level offset", CharacterFormat.OffLevel, 0x17);
CheckEq("sex offset", CharacterFormat.OffSex, 0x18);
CheckEq("alignment offset", CharacterFormat.OffAlignment, 0x19);
CheckEq("race offset", CharacterFormat.OffRace, 0x1A);
CheckEq("class offset", CharacterFormat.OffClass, 0x1B);
CheckEq("armour offset", CharacterFormat.OffArmourPercent, 0x1E);
CheckEq("status offset", CharacterFormat.OffStatus, 0x1F);
CheckEq("max HP offset", CharacterFormat.OffMaxHp, 0x20);
CheckEq("current HP offset", CharacterFormat.OffCurHp, 0x22);
CheckEq("max magic offset", CharacterFormat.OffMaxMagic, 0x68);
CheckEq("current magic offset", CharacterFormat.OffCurMagic, 0x69);
CheckEq("spell count offset", CharacterFormat.OffSpellCount, 0x6A);
CheckEq("item count offset", CharacterFormat.OffItemCount, 0x6B);
CheckEq("constitution offset", CharacterFormat.OffConstitution, 0x44);
CheckEq("strength offset", CharacterFormat.OffStrength, 0x45);
CheckEq("dexterity offset", CharacterFormat.OffDexterity, 0x46);
CheckEq("intelligence offset", CharacterFormat.OffIntelligence, 0x47);
CheckEq("wisdom offset", CharacterFormat.OffWisdom, 0x48);
CheckEq("max weight offset", CharacterFormat.OffMaxWeight, 0x64);
CheckEq("current weight offset", CharacterFormat.OffCurWeight, 0x66);
CheckEq("experience offset", CharacterFormat.OffExperience, 0x6C);
CheckEq("rations offset", CharacterFormat.OffRations, 0x70);
CheckEq("gold offset", CharacterFormat.OffGold, 0x72);
CheckEq("unknown counter offset", CharacterFormat.OffUnknownCounter, 0x76);
CheckEq("languages offset", CharacterFormat.OffLanguages, 0x7A);
CheckEq("inventory offset", CharacterFormat.OffInventory, 0x82);
CheckEq("spells offset", CharacterFormat.OffSpells, 0x142);
CheckEq("inventory slots", CharacterFormat.InventorySlots, 48);
CheckEq("spell slots", CharacterFormat.SpellSlots, 44);
CheckEq("inventory fills the gap", CharacterFormat.OffInventory + CharacterFormat.InventorySlots * 4, CharacterFormat.OffSpells);
CheckEq("spells fill the record", CharacterFormat.OffSpells + CharacterFormat.SpellSlots * 2, CharacterFormat.RecordSize);
CheckEq("ability offsets counted", CharacterFormat.AbilityOffsets.Length, 9);
CheckEq("attribute offsets counted", CharacterFormat.AttributeOffsets.Length, 5);
Check("ability offsets are the measured ones",
    CharacterFormat.AbilityOffsets.SequenceEqual(new[] { 0x25, 0x27, 0x28, 0x2B, 0x2D, 0x30, 0x32, 0x34, 0x36 }));
Check("attribute offsets are contiguous 0x44..0x48",
    CharacterFormat.AttributeOffsets.SequenceEqual(new[] { 0x44, 0x45, 0x46, 0x47, 0x48 }));
Check("every ability offset is inside the record", CharacterFormat.AbilityOffsets.All(o => o is >= 0 and < 410));
Check("ability offsets are strictly ascending",
    CharacterFormat.AbilityOffsets.Zip(CharacterFormat.AbilityOffsets.Skip(1), (a, b) => b > a).All(x => x));

// ---------------------------------------------------------------- locator constants
Section("Locator constants");
CheckEq("primary anchor offset", GameFacts.PrimaryAnchorOffset, 0xF371);
CheckEq("party pointer offset", GameFacts.PartyPointerOffset, 0x0030);
CheckEq("roster pointer offset", GameFacts.RosterPointerOffset, 0x3FF6);
CheckEq("roster-to-party delta", GameFacts.RosterToPartyDelta, 32 * 410 + 2);
CheckEq("roster-to-party delta is the measured one", GameFacts.RosterToPartyDelta, 0x3342);
CheckEq("secondary anchors", GameFacts.SecondaryAnchors.Length, 4);
Check("every anchor offset fits a 16-bit data group",
    GameFacts.PrimaryAnchorOffset <= 0xFFFF && GameFacts.SecondaryAnchors.All(a => a.Offset is >= 0 and <= 0xFFFF));
// Every pair, not just each secondary against the primary: two secondaries overlapping would let
// CountValidators score one string twice and reach the threshold on a single match.
{
    var spans = GameFacts.SecondaryAnchors
        .Select(a => (a.Offset, End: a.Offset + a.Text.Length))
        .Append((Offset: GameFacts.PrimaryAnchorOffset,
                 End: GameFacts.PrimaryAnchorOffset + GameFacts.PrimaryAnchorText.Length))
        .OrderBy(s => s.Offset)
        .ToArray();
    bool disjoint = true;
    for (int i = 1; i < spans.Length; i++)
        if (spans[i].Offset < spans[i - 1].End) disjoint = false;
    Check("no two anchors overlap", disjoint);
    Check("every anchor is distinct text",
        GameFacts.SecondaryAnchors.Select(a => a.Text).Append(GameFacts.PrimaryAnchorText)
            .Distinct().Count() == GameFacts.SecondaryAnchors.Length + 1);
}
Check("primary anchor is long enough to be distinctive", GameFacts.PrimaryAnchorText.Length >= 16);
Check("emulator hints are lower case", GameFacts.EmulatorProcessHints.All(h => h == h.ToLowerInvariant()));

// ---------------------------------------------------------------- reference tables
Section("Reference tables");
CheckEq("item count", ItemBook.Count, 186);
CheckEq("spell count", SpellBook.Count, 142);
CheckEq("race count", RaceBook.Count, 6);
CheckEq("class count", ClassBook.Count, 13);
CheckEq("status count", StatusBook.Count, 8);
CheckEq("language count", LanguageBook.Count, 8);
CheckEq("ability count", AbilityBook.Count, 9);
CheckEq("class names match manual-name table length", ClassBook.Names.Length, ClassBook.ManualNames.Length);
CheckEq("ability names match description table length", AbilityBook.Names.Length, AbilityBook.Descriptions.Length);
CheckEq("language count matches the format constant", LanguageBook.Count, CharacterFormat.LanguageCount);

// ids fixed by the running game, not guessed
CheckEq("item 0 is the empty sentinel", ItemBook.NameOf(0), "(none)");
CheckEq("item 1", ItemBook.NameOf(1), "Club");
CheckEq("item 13 (Connar's sword)", ItemBook.NameOf(13), "Short sword");
CheckEq("item 26", ItemBook.NameOf(26), "Robe");
CheckEq("item 27 (Connar's armour)", ItemBook.NameOf(27), "Leather armour");
CheckEq("item 34 (Connar's shield)", ItemBook.NameOf(34), "Small shield");
CheckEq("item 42 (Assanla's picks)", ItemBook.NameOf(42), "Thieves' picks");
CheckEq("item 120 (the Count's gift)", ItemBook.NameOf(120), "The Amulet");
CheckEq("item 185 (last)", ItemBook.NameOf(185), "The Staff");
CheckEq("spell 0 is the empty sentinel", SpellBook.NameOf(0), "(none)");
CheckEq("spell 1 (Merlin's first)", SpellBook.NameOf(1), "Burning hands");
CheckEq("spell 2 (Merlin's second)", SpellBook.NameOf(2), "Light");
CheckEq("spell 100 (Cassandra's)", SpellBook.NameOf(100), "Cure light wounds");
CheckEq("spell 141 (last)", SpellBook.NameOf(141), "Drachenatem II");
CheckEq("race 0", RaceBook.NameOf(0), "Human");
CheckEq("race 4 (Gorth)", RaceBook.NameOf(4), "Dwarf");
CheckEq("race 5 (Connar)", RaceBook.NameOf(5), "Half-Orc");
CheckEq("class 0", ClassBook.NameOf(0), "Warrior");
CheckEq("class 2 (Assanla)", ClassBook.NameOf(2), "Rogue");
CheckEq("class 7 (Merlin)", ClassBook.NameOf(7), "Magician");
CheckEq("class 10 (Cassandra)", ClassBook.NameOf(10), "Healer");
CheckEq("class 12 is the NPC slot", ClassBook.NameOf(12), "??");
CheckEq("status 0", StatusBook.NameOf(0), "Good");
CheckEq("status 7", StatusBook.NameOf(7), "Dead");
CheckEq("language 0", LanguageBook.NameOf(0), "Common tongue");
CheckEq("language 2 (Half-Orc)", LanguageBook.NameOf(2), "Orc tongue");
CheckEq("language 4 (Dwarf)", LanguageBook.NameOf(4), "Dwarven tongue");
CheckEq("language 7", LanguageBook.NameOf(7), "Magic tongue");
CheckEq("ability 0", AbilityBook.NameOf(0), "Negotiating");
CheckEq("ability 8", AbilityBook.NameOf(8), "Lock picking");
Check("out-of-range lookups do not throw", ItemBook.NameOf(9999).Length > 0 && SpellBook.NameOf(-1).Length > 0
    && RaceBook.NameOf(99).Length > 0 && ClassBook.NameOf(99).Length > 0 && StatusBook.NameOf(99).Length > 0
    && LanguageBook.NameOf(99).Length > 0 && AbilityBook.NameOf(99).Length > 0);
Check("no item name is blank", ItemBook.All.All(i => !string.IsNullOrWhiteSpace(i.Name)));
Check("no spell name is blank", SpellBook.All.All(s => !string.IsNullOrWhiteSpace(s.Name)));
Check("item ids are their own index", ItemBook.All.Select((it, i) => it.Id == i).All(x => x));
Check("spell ids are their own index", SpellBook.All.Select((sp, i) => sp.Id == i).All(x => x));
Check("no item price is negative", ItemBook.All.All(i => i.Price >= 0));
CheckEq("Leather armour price", ItemBook.PriceOf(27), 150L);
CheckEq("Two handed sword price", ItemBook.PriceOf(17), 456L);

// ---------------------------------------------------------------- interop layout
Section("Interop layout");
{
    // SendInput rejects any cbSize that is not exactly the native INPUT size, so getting this wrong
    // does not crash - it makes every keystroke injection fail silently and the speed hotkeys look
    // like a focus problem. The union must be sized by its largest arm (MOUSEINPUT), not by the
    // keyboard arm the trainer actually uses.
    int expected = IntPtr.Size == 8 ? 40 : 28;
    CheckEq($"marshalled INPUT size on a {IntPtr.Size * 8}-bit process", DosBoxSpeed.InputStructSize, expected);
}

// ---------------------------------------------------------------- record round-trip
Section("Character record round-trip");
{
    var rec = new CharacterRecord();
    rec.Occupied = true;
    rec.Name = "Gwendolyn";
    CheckEq("name round-trip", rec.Name, "Gwendolyn");
    rec.Name = "Averylongname";
    CheckEq("name truncated at 10", rec.Name, "Averylongn");
    rec.Name = "";
    CheckEq("empty name", rec.Name, "");
    rec.Name = "Sarian";

    rec.Level = 12; CheckEq("level", rec.Level, 12);
    rec.Level = 0; CheckEq("level 0 is kept (non-player character)", rec.Level, 0);
    rec.Level = -3; CheckEq("level clamps low", rec.Level, 0);
    rec.Level = 500; CheckEq("level clamps high", rec.Level, 99);
    rec.Level = 1;

    rec.Race = 5; CheckEq("race", rec.Race, 5);
    rec.Race = 99; CheckEq("race clamps", rec.Race, RaceBook.Count - 1);
    rec.Class = 10; CheckEq("class", rec.Class, 10);
    rec.Class = 99; CheckEq("class clamps", rec.Class, ClassBook.Count - 1);
    rec.Status = 7; CheckEq("status", rec.Status, 7);
    rec.Status = 99; CheckEq("status clamps", rec.Status, StatusBook.Count - 1);
    rec.Sex = 1; CheckEq("sex name male", rec.SexName, "Male");
    rec.Sex = 0; CheckEq("sex name female", rec.SexName, "Female");
    rec.Sex = 5; CheckEq("sex clamps", rec.Sex, 1);
    rec.Alignment = 1; CheckEq("alignment name", rec.AlignmentName, "Chaotic");
    rec.Alignment = 9; CheckEq("alignment clamps", rec.Alignment, 1);

    rec.MaxHp = 250; rec.CurHp = 100;
    CheckEq("max HP", rec.MaxHp, 250);
    CheckEq("current HP", rec.CurHp, 100);
    CheckEq("max HP is stored little-endian",
        rec.Bytes[CharacterFormat.OffMaxHp] | (rec.Bytes[CharacterFormat.OffMaxHp + 1] << 8), 250);
    rec.MaxHp = 0; CheckEq("max HP clamps low", rec.MaxHp, 1);
    rec.MaxHp = 99999; CheckEq("max HP clamps high", rec.MaxHp, 9999);

    rec.MaxMagic = 40; rec.CurMagic = 12;
    CheckEq("max magic", rec.MaxMagic, 40);
    CheckEq("current magic", rec.CurMagic, 12);
    rec.MaxMagic = 999; CheckEq("max magic clamps", rec.MaxMagic, 255);

    rec.Gold = 12345; CheckEq("gold", rec.Gold, 12345L);
    // Gold is a full uint32 in the record and the validator does not constrain it, so the editor
    // must not either: a ceiling below what the field holds would make "Freeze gold" destructive.
    rec.Gold = 999999; CheckEq("gold above five digits is kept", rec.Gold, 999999L);
    rec.Gold = uint.MaxValue; CheckEq("gold saturates at the field width", rec.Gold, (long)uint.MaxValue);
    rec.Gold = -1; CheckEq("gold cannot go negative", rec.Gold, 0L);
    rec.Gold = 12345;
    rec.Rations = 60000; CheckEq("rations above 9999 are kept", rec.Rations, 60000);
    rec.Rations = 250;
    rec.Experience = 4000000000; CheckEq("experience is a full uint32", rec.Experience, 4000000000L);
    rec.Rations = 250; CheckEq("rations", rec.Rations, 250);
    rec.MaxWeight = 530;
    CheckEq("max weight in pounds", rec.MaxWeight, 530);
    CheckEq("max weight stored in tenths",
        rec.Bytes[CharacterFormat.OffMaxWeight] | (rec.Bytes[CharacterFormat.OffMaxWeight + 1] << 8), 5300);
    rec.Bytes[CharacterFormat.OffCurWeight] = 0x21;
    rec.Bytes[CharacterFormat.OffCurWeight + 1] = 0x01;
    CheckEq("carried weight truncates like the game (289 tenths prints 28)", rec.CurWeight, 28);

    for (int i = 0; i < 5; i++) rec.SetAttribute(i, 14 + i);
    for (int i = 0; i < 5; i++) CheckEq($"attribute {i}", rec.GetAttribute(i), 14 + i);
    rec.SetAttribute(0, 0); CheckEq("attribute clamps low", rec.GetAttribute(0), 1);
    rec.SetAttribute(0, 99); CheckEq("attribute clamps high", rec.GetAttribute(0), CharacterFormat.MaxAttribute);
    rec.Constitution = 19; CheckEq("constitution alias", rec.GetAttribute(0), 19);
    rec.Strength = 18; CheckEq("strength alias", rec.GetAttribute(1), 18);
    rec.Dexterity = 17; CheckEq("dexterity alias", rec.GetAttribute(2), 17);
    rec.Intelligence = 16; CheckEq("intelligence alias", rec.GetAttribute(3), 16);
    rec.Wisdom = 15; CheckEq("wisdom alias", rec.GetAttribute(4), 15);

    for (int i = 0; i < 9; i++) rec.SetAbility(i, 10 + i * 5);
    for (int i = 0; i < 9; i++) CheckEq($"ability {i}", rec.GetAbility(i), 10 + i * 5);
    rec.SetAbility(0, 250); CheckEq("ability clamps at 100", rec.GetAbility(0), 100);
    rec.SetAbility(0, -5); CheckEq("ability clamps at 0", rec.GetAbility(0), 0);
    Check("abilities land on distinct bytes",
        CharacterFormat.AbilityOffsets.Distinct().Count() == CharacterFormat.AbilityOffsets.Length);

    for (int i = 0; i < 8; i++) rec.SetLanguage(i, i % 2 == 0);
    for (int i = 0; i < 8; i++) CheckEq($"language {i}", rec.GetLanguage(i), i % 2 == 0);
    rec.SetLanguage(3, true);
    CheckEq("a spoken language stores 2, as the shipped records do", rec.Bytes[CharacterFormat.OffLanguages + 3], (byte)2);

    rec.SetItem(0, 27, true, 96);
    var slot0 = rec.GetItem(0);
    CheckEq("item id", slot0.ItemId, 27);
    CheckEq("item equipped", slot0.Equipped, true);
    CheckEq("item condition", slot0.Condition, 96);
    rec.SetItem(1, 34, false, 500);
    CheckEq("item condition clamps at 100", rec.GetItem(1).Condition, 100);
    rec.SetItem(2, 13, true, 100);
    CheckEq("used item slots", rec.UsedItemSlots, 3);
    rec.SetItem(1, 0, true, 100);
    CheckEq("clearing an item empties the whole entry", rec.GetItem(1).Equipped, false);
    CheckEq("clearing an item zeroes its condition", rec.GetItem(1).Condition, 0);
    CheckEq("used item slots after clearing", rec.UsedItemSlots, 2);
    rec.SetItem(CharacterFormat.InventorySlots - 1, 1, false, 100);
    CheckEq("last inventory slot is inside the record", rec.GetItem(CharacterFormat.InventorySlots - 1).ItemId, 1);

    rec.SetSpell(0, 1, 8);
    rec.SetSpell(1, 2, 4);
    CheckEq("spell 0 id", rec.GetSpell(0).SpellId, 1);
    CheckEq("spell 0 uses", rec.GetSpell(0).Uses, 8);
    CheckEq("used spell slots", rec.UsedSpellSlots, 2);
    rec.SetSpell(1, 0, 9);
    CheckEq("clearing a spell zeroes its uses", rec.GetSpell(1).Uses, 0);
    rec.SetSpell(CharacterFormat.SpellSlots - 1, 141, 1);
    CheckEq("last spell slot is inside the record", rec.GetSpell(CharacterFormat.SpellSlots - 1).SpellId, 141);

    var copy = new CharacterRecord(rec.Bytes);
    Check("record copy is byte-identical", copy.Bytes.SequenceEqual(rec.Bytes));
    CheckEq("record buffer is exactly one record", rec.Bytes.Length, CharacterFormat.RecordSize);
}

// ---------------------------------------------------------------- ids and slot bounds
Section("Unknown ids and slot bounds");
{
    var rec = new CharacterRecord();

    // An id the tables do not cover must survive a read-modify-write untouched. Clamping it into
    // the table would silently swap an uncatalogued item or spell for the last table entry.
    rec.SetItem(0, 200, true, 100);
    CheckEq("an item id past the table is stored unchanged", rec.GetItem(0).ItemId, 200);
    var it = rec.GetItem(0);
    rec.SetItem(0, it.ItemId, false, it.Condition);       // the "tick In use" round trip
    CheckEq("and survives a read-modify-write", rec.GetItem(0).ItemId, 200);
    Check("and is displayed as an unknown id rather than a wrong name",
        ItemBook.NameOf(200).Contains("200"));

    rec.SetSpell(0, 210, 5);
    CheckEq("a spell id past the table is stored unchanged", rec.GetSpell(0).SpellId, 210);
    var sp = rec.GetSpell(0);
    rec.SetSpell(0, sp.SpellId, 99);
    CheckEq("and survives a refill", rec.GetSpell(0).SpellId, 210);

    // Slot 48 lands on 0x142, inside the record - it would overwrite spell slots instead of throwing.
    void Throws(string what, Action a)
    {
        bool threw = false;
        try { a(); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(what, threw);
    }
    Throws("inventory slot 48 is rejected, not folded into the spell list",
        () => rec.SetItem(CharacterFormat.InventorySlots, 1, false, 100));
    Throws("inventory slot -1 is rejected", () => rec.SetItem(-1, 1, false, 100));
    Throws("reading inventory slot 48 is rejected", () => rec.GetItem(CharacterFormat.InventorySlots));
    Throws("spell slot 44 is rejected", () => rec.SetSpell(CharacterFormat.SpellSlots, 1, 1));
    Throws("spell slot -1 is rejected", () => rec.SetSpell(-1, 1, 1));

    // A negative id clamps to 0 - i.e. to "empty" - so the rest of the entry must be cleared too.
    // Testing the argument rather than the stored value leaves an empty slot carrying live data.
    var neg = new CharacterRecord();
    neg.SetSpell(2, 5, 7);
    neg.SetSpell(2, -1, 7);
    CheckEq("a negative spell id stores as empty", neg.GetSpell(2).SpellId, 0);
    CheckEq("and its use count is cleared with it", neg.GetSpell(2).Uses, 0);
    neg.SetItem(2, 5, true, 80);
    neg.SetItem(2, -1, true, 80);
    CheckEq("a negative item id stores as empty", neg.GetItem(2).ItemId, 0);
    CheckEq("and its equipped flag is cleared with it", neg.GetItem(2).Equipped, false);
    CheckEq("and its condition is cleared with it", neg.GetItem(2).Condition, 0);
    CheckEq("clearing an empty slot leaves the high-water mark at 0", neg.InventoryHighWater, 0);

    // The byte is the record's range, so an uncatalogued id must survive both layers.
    neg.SetItem(0, 255, true, 90);
    CheckEq("the top of the byte range is storable", neg.GetItem(0).ItemId, 255);
    CheckEq("and keeps its condition", neg.GetItem(0).Condition, 90);
    neg.SetSpell(0, 255, 12);
    CheckEq("the top spell id is storable", neg.GetSpell(0).SpellId, 255);
    CheckEq("and keeps its uses", neg.GetSpell(0).Uses, 12);

    // The count bytes are high-water marks, not populations - the game itself put a quest item in
    // slot 9 of a three-item character and wrote 10 here.
    var hw = new CharacterRecord();
    CheckEq("empty inventory high-water", hw.InventoryHighWater, 0);
    hw.SetItem(0, 27, true, 100);
    hw.SetItem(1, 34, false, 100);
    hw.SetItem(2, 13, true, 100);
    CheckEq("packed inventory high-water equals the count", hw.InventoryHighWater, 3);
    CheckEq("and so does the population", hw.UsedItemSlots, 3);
    hw.SetItem(9, 120, false, 100);
    CheckEq("a gap raises the high-water mark to one past the far slot", hw.InventoryHighWater, 10);
    CheckEq("while the population counts only the occupied slots", hw.UsedItemSlots, 4);
    hw.SetItem(9, 0, false, 0);
    CheckEq("clearing the far slot drops the mark back", hw.InventoryHighWater, 3);
    CheckEq("empty spell high-water", hw.SpellHighWater, 0);
    hw.SetSpell(5, 1, 8);
    CheckEq("spell high-water", hw.SpellHighWater, 6);
    // Fill the final slot of each array: the mark must reach the array length exactly and still
    // fit the byte the game reads it from.
    hw.SetItem(CharacterFormat.InventorySlots - 1, 1, false, 100);
    CheckEq("a full inventory marks every slot", hw.InventoryHighWater, CharacterFormat.InventorySlots);
    hw.SetSpell(CharacterFormat.SpellSlots - 1, 1, 1);
    CheckEq("a full spell list marks every slot", hw.SpellHighWater, CharacterFormat.SpellSlots);
    Check("both marks still fit the byte they are stored in",
        hw.InventoryHighWater <= byte.MaxValue && hw.SpellHighWater <= byte.MaxValue);
    hw.ItemCount = hw.InventoryHighWater;
    hw.SpellCount = hw.SpellHighWater;
    CheckEq("and the count byte round-trips the mark", hw.ItemCount, CharacterFormat.InventorySlots);
    CheckEq("and the spell count byte round-trips it", hw.SpellCount, CharacterFormat.SpellSlots);
}

// ---------------------------------------------------------------- validation
Section("Record validation");
{
    var good = FakeRecord.Make("Sarian");
    Check("a plausible record validates", CharacterRecord.IsValidRecord(good, 0));

    var zeros = new byte[CharacterFormat.RecordSize];
    Check("all zeros is not a record", !CharacterRecord.IsValidRecord(zeros, 0));
    Check("all zeros is an empty slot", CharacterRecord.IsEmptySlot(zeros, 0));

    var emptyRoster = new byte[CharacterFormat.RecordSize];
    Encoding.ASCII.GetBytes("__________").CopyTo(emptyRoster, CharacterFormat.OffName);
    Check("an unused roster slot (name '__________', occupied 0) is an empty slot",
        CharacterRecord.IsEmptySlot(emptyRoster, 0));
    Check("an unused roster slot is not a valid record", !CharacterRecord.IsValidRecord(emptyRoster, 0));

    void Reject(string what, Action<byte[]> mutate)
    {
        var b = FakeRecord.Make("Sarian");
        mutate(b);
        Check($"rejects {what}", !CharacterRecord.IsValidRecord(b, 0));
    }

    Reject("occupied flag 0", b => b[CharacterFormat.OffOccupied] = 0);
    Reject("occupied flag 2", b => b[CharacterFormat.OffOccupied] = 2);
    Reject("an empty name", b => b[CharacterFormat.OffName] = 0);
    Reject("a name starting with a digit", b => b[CharacterFormat.OffName] = (byte)'7');
    Reject("a non-printable byte inside the name", b => b[CharacterFormat.OffName + 2] = 0x01);
    Reject("a name with no terminator in the field",
        b => { for (int i = 0; i < CharacterFormat.NameFieldLength; i++) b[CharacterFormat.OffName + i] = (byte)'A'; });
    Reject("an 11-character name", b =>
    {
        for (int i = 0; i < 11; i++) b[CharacterFormat.OffName + i] = (byte)'A';
        b[CharacterFormat.OffName + 11] = 0;
    });
    Reject("race 6", b => b[CharacterFormat.OffRace] = 6);
    Reject("class 13", b => b[CharacterFormat.OffClass] = 13);
    Reject("status 8", b => b[CharacterFormat.OffStatus] = 8);
    Reject("level 100", b => b[CharacterFormat.OffLevel] = 100);

    var npc = FakeRecord.Make("Siegurd", level: 0, cls: 12);
    Check("a non-player character (Rnk 0, trade '??') validates", CharacterRecord.IsValidRecord(npc, 0));
    Reject("sex 2", b => b[CharacterFormat.OffSex] = 2);
    Reject("alignment 2", b => b[CharacterFormat.OffAlignment] = 2);
    Reject("max HP 0", b => { b[CharacterFormat.OffMaxHp] = 0; b[CharacterFormat.OffMaxHp + 1] = 0; });
    Reject("current HP above maximum", b => { b[CharacterFormat.OffCurHp] = 0xFF; b[CharacterFormat.OffCurHp + 1] = 0x00; });
    Reject("max weight 0", b => { b[CharacterFormat.OffMaxWeight] = 0; b[CharacterFormat.OffMaxWeight + 1] = 0; });
    Reject("carried weight above maximum",
        b => { b[CharacterFormat.OffCurWeight] = 0xFF; b[CharacterFormat.OffCurWeight + 1] = 0xFF; });

    var dead = FakeRecord.Make("Connar", maxHp: 11, curHp: 0);
    dead[CharacterFormat.OffStatus] = 7;
    Check("a dead character (0 HP, state Dead) is still a valid record", CharacterRecord.IsValidRecord(dead, 0));

    var maxName = FakeRecord.Make("Lord Krynn");
    Check("a 10-character name with a space validates", CharacterRecord.IsValidRecord(maxName, 0));

    Check("validation rejects a short buffer", !CharacterRecord.IsValidRecord(new byte[10], 0));
    Check("validation rejects a negative offset", !CharacterRecord.IsValidRecord(good, -1));
    Check("validation rejects an offset past the end", !CharacterRecord.IsValidRecord(good, 1));
    Check("validation rejects a null buffer", !CharacterRecord.IsValidRecord(null!, 0));
    Check("empty-slot test rejects a null buffer", !CharacterRecord.IsEmptySlot(null!, 0));
}

// ---------------------------------------------------------------- locator
Section("Locator over a synthetic guest");

const long DgroupGuest = 0x38200;
const long RosterGuest = 0x4E6A2;
const long PartyGuest = RosterGuest + 32 * 410 + 2;

FakeGuest BuildGuest(int validators = 4, int partyMembers = 4, bool withBios = true,
    bool withPartyPointer = true, bool withRosterPointer = true, long dgroup = DgroupGuest)
{
    var g = new FakeGuest();
    if (withBios) g.WriteBios();
    g.WriteDgroup(dgroup, validators);

    for (int i = 0; i < 32; i++)
    {
        if (i < 3) g.Write(RosterGuest + i * 410, FakeRecord.Make($"Roster{i}", level: i + 1));
        else
        {
            var slot = new byte[410];
            Encoding.ASCII.GetBytes("__________").CopyTo(slot, CharacterFormat.OffName);
            g.Write(RosterGuest + i * 410, slot);
        }
    }
    for (int i = 0; i < partyMembers; i++)
        g.Write(PartyGuest + i * 410, FakeRecord.Make($"Hero{i}", level: i + 1, race: i % 6, cls: i % 12));

    if (withPartyPointer) g.WriteFarPointer(dgroup + GameFacts.PartyPointerOffset, PartyGuest);
    if (withRosterPointer) g.WriteFarPointer(dgroup + GameFacts.RosterPointerOffset, RosterGuest);
    return g;
}

{
    var g = BuildGuest();
    var found = GameLocator.Find(g, out string status);
    Check("locates the game", found != null);
    if (found != null)
    {
        CheckEq("party address", found.PartyAddress, g.HostOf(PartyGuest));
        CheckEq("roster address", found.RosterAddress, g.HostOf(RosterGuest));
        CheckEq("data group address", found.DgroupAddress, g.HostOf(DgroupGuest));
        CheckEq("guest zero", found.GuestZero, g.HostOf(0));
        CheckEq("validators matched", found.ValidatorsMatched, 4);
        CheckEq("adjacency cross-check holds", found.Adjacency, AdjacencyResult.Holds);
        CheckEq("party members found", found.Party.Count, 4);
        CheckEq("roster entries found", found.Roster.Count, 3);
        CheckEq("first party member name", found.Party[0].Record.Name, "Hero0");
        CheckEq("first party member slot", found.Party[0].Slot, 0);
        CheckEq("fourth party member level", found.Party[3].Record.Level, 4);
        CheckEq("first roster entry name", found.Roster[0].Record.Name, "Roster0");
        Check("status mentions the validator count", status.Contains("4/4"));
    }
}

{
    var g = BuildGuest(validators: 2);
    var found = GameLocator.Find(g, out _);
    Check("two corroborating literals are enough", found != null && found.ValidatorsMatched == 2);
}

{
    var g = BuildGuest(validators: 1);
    var found = GameLocator.Find(g, out string status);
    Check("one corroborating literal is not enough", found == null);
    Check("and it says why", status.Contains("corroborating"));
}

{
    var g = new FakeGuest();
    g.WriteBios();
    var found = GameLocator.Find(g, out string status);
    Check("an empty guest is not located", found == null);
    Check("and it tells the user to start the game", status.Contains("START.BAT"));
}

{
    var g = BuildGuest(withBios: false);
    Check("no BIOS data area means no locate", GameLocator.Find(g, out _) == null);
}

{
    var g = BuildGuest(withPartyPointer: false);
    var found = GameLocator.Find(g, out string status);
    Check("a null party pointer is refused", found == null);
    Check("and it names the pointer", status.Contains("DS:0x0030"));
}

{
    // The junk has to be somewhere a real-mode far pointer can reach, or the fixture never gets
    // past the null-pointer guard and IsPlausibleArray — the check that actually stops a write into
    // unrelated memory — is never reached.
    const long Junk = 0x60000;
    var g = BuildGuest();
    for (int i = 0; i < 6 * 410; i++) g.Write(Junk + i, 0xCC);
    g.WriteFarPointer(DgroupGuest + GameFacts.PartyPointerOffset, Junk);
    var found = GameLocator.Find(g, out string status);
    Check("a party pointer into junk is refused", found == null);
    Check("and it is refused by the record shape, not the null-pointer guard",
        status.Contains("do not look like characters"));
}

{
    // Guard the fixture itself: silent truncation here is what made the case above vacuous.
    bool threw = false;
    try { new FakeGuest().WriteFarPointer(0x1000, 0x200000); }
    catch (ArgumentOutOfRangeException) { threw = true; }
    Check("the fixture refuses to encode a far pointer it cannot represent", threw);
}

{
    var g = BuildGuest(withRosterPointer: false);
    var found = GameLocator.Find(g, out _);
    Check("the party still resolves without the roster pointer", found != null && found.Party.Count == 4);
    Check("and adjacency is reported as unchecked", found != null && found.Adjacency == AdjacencyResult.NotChecked);
    Check("and the roster list is empty", found != null && found.Roster.Count == 0);
}

{
    var g = BuildGuest();
    // Move the roster somewhere non-adjacent but still reachable by a real-mode far pointer.
    long far = 0x60000;
    for (int i = 0; i < 32; i++)
    {
        if (i < 3) g.Write(far + i * 410, FakeRecord.Make($"Far{i}"));
        else
        {
            var slot = new byte[410];
            Encoding.ASCII.GetBytes("__________").CopyTo(slot, CharacterFormat.OffName);
            g.Write(far + i * 410, slot);
        }
    }
    g.WriteFarPointer(DgroupGuest + GameFacts.RosterPointerOffset, far);
    var found = GameLocator.Find(g, out string status);
    Check("a non-adjacent roster fails the cross-check", found != null && found.Adjacency == AdjacencyResult.Failed);
    Check("and is not opened as a writable tab", found != null && found.RosterAddress == 0 && found.Roster.Count == 0);
    Check("and the failure is called out, not reported as 'not checked'", status.Contains("NOT adjacent"));
    Check("while the party still resolves", found != null && found.Party.Count == 4);
}

{
    var g = BuildGuest(partyMembers: 0);
    var found = GameLocator.Find(g, out _);
    Check("an all-empty party still locates (fresh game, nobody recruited)",
        found != null && found.Party.Count == 0);
}

{
    var g = BuildGuest(partyMembers: 6);
    var found = GameLocator.Find(g, out _);
    Check("a full party of six locates", found != null && found.Party.Count == 6);
}

{
    // An occupied slot after an empty one is not how either array packs.
    var g = BuildGuest(partyMembers: 2);
    g.Write(PartyGuest + 4 * 410, FakeRecord.Make("Straggler"));
    Check("a gap in the party array is refused", GameLocator.Find(g, out _) == null);
}

{
    // The anchor must still be found when it straddles the 1 MiB sweep seam. The seam is a *host*
    // offset from the region base, and the guest is padded, so the pad has to come off — otherwise
    // the anchor lands wholly inside the second chunk and the overlap logic is never exercised.
    var g = new FakeGuest();
    long seam = (1 << 20) - g.GuestPad - GameFacts.PrimaryAnchorText.Length / 2;
    long dgroup = seam - GameFacts.PrimaryAnchorOffset;
    g.WriteBios();
    g.WriteDgroup(dgroup);
    for (int i = 0; i < 4; i++) g.Write(PartyGuest + i * 410, FakeRecord.Make($"Seam{i}"));
    for (int i = 0; i < 32; i++)
    {
        if (i >= 1) { var s = new byte[410]; Encoding.ASCII.GetBytes("__________").CopyTo(s, CharacterFormat.OffName); g.Write(RosterGuest + i * 410, s); }
        else g.Write(RosterGuest + i * 410, FakeRecord.Make("Seam"));
    }
    g.WriteFarPointer(dgroup + GameFacts.PartyPointerOffset, PartyGuest);
    g.WriteFarPointer(dgroup + GameFacts.RosterPointerOffset, RosterGuest);
    var found = GameLocator.Find(g, out _);
    Check("an anchor straddling the 1 MiB chunk seam is still found", found != null && found.Party.Count == 4);
}

{
    var g = BuildGuest();
    g.AddDecoy(0x1000, 64 << 10);            // too small to be guest RAM
    g.AddDecoy(0x8000000, 4 << 20);          // large but empty
    var found = GameLocator.Find(g, out _);
    Check("decoy regions are skipped", found != null && found.PartyAddress == g.HostOf(PartyGuest));
}

{
    // The page has to sit *before* the anchor, or the sweep returns before ever reaching it and the
    // short-read recovery is never exercised. The anchor is at DgroupGuest + 0xF371 = 0x47571.
    var g = BuildGuest();
    g.PoisonedPages.Add(0x10000);
    var found = GameLocator.Find(g, out _);
    Check("an unreadable page before the anchor does not abort the sweep",
        found != null && found.Party.Count == 4);
}

{
    var g = BuildGuest();
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    bool threw = false;
    try { GameLocator.Find(g, out _, cts.Token); } catch (OperationCanceledException) { threw = true; }
    Check("cancellation is honoured", threw);
}

{
    var g = BuildGuest();
    var found = GameLocator.Find(g, out _)!;
    var buf = new byte[CharacterFormat.RecordSize];
    Check("re-reading a located record works", GameLocator.Reread(g, found.Party[0].Address, buf));
    Check("and returns the same bytes", new CharacterRecord(buf).Name == "Hero0");
    Check("re-reading an address outside the guest fails", !GameLocator.Reread(g, 0x1, buf));
}

// ---------------------------------------------------------------- view-model write paths
Section("View-model write paths");
{
    // Everything below is about *what gets written to the game*, which is the part of this trainer
    // that can do damage. A recording host makes that assertable with no emulator present.
    var host = new RecordingHost();
    var rec = new CharacterRecord(FakeRecord.Make("Sarian", level: 3));
    var vm = new CharacterViewModel(host, new LocatedCharacter(0x1000, 0, rec));

    host.Clear();
    vm.Gold = 4242;
    Check("editing gold writes exactly the 4 bytes at +0x72",
        host.WroteOnly(CharacterFormat.OffGold, 4));
    CheckEq("and the address is the record's", host.Writes[0].Address, (nuint)0x1000);
    CheckEq("and the bytes are little-endian", (int)(host.Writes[0].Bytes[0] | (host.Writes[0].Bytes[1] << 8)), 4242 & 0xFFFF);

    host.Clear();
    vm.Gold = 4242;
    CheckEq("re-setting the same gold writes nothing", host.Writes.Count, 0);

    host.Clear();
    vm.Level = 3;
    CheckEq("re-setting the same level writes nothing", host.Writes.Count, 0);
    vm.Attributes[1].Value = vm.Attributes[1].Value;
    CheckEq("re-setting the same attribute writes nothing", host.Writes.Count, 0);
    vm.Abilities[0].Value = vm.Abilities[0].Value;
    CheckEq("re-setting the same ability writes nothing", host.Writes.Count, 0);
    vm.Languages[0].Value = vm.Languages[0].Value;
    CheckEq("re-setting the same language writes nothing", host.Writes.Count, 0);
    vm.Name = vm.Name;
    CheckEq("re-setting the same name writes nothing", host.Writes.Count, 0);
    vm.Items[0].Equipped = vm.Items[0].Equipped;
    CheckEq("re-setting the same equipped flag writes nothing", host.Writes.Count, 0);

    host.Clear();
    vm.Attributes[1].Value = 21;
    Check("editing an attribute writes its single byte",
        host.WroteOnly(CharacterFormat.OffStrength, 1));
    host.Clear();
    vm.Abilities[8].Value = 55;
    Check("editing an ability writes its single byte",
        host.WroteOnly(CharacterFormat.OffLockPicking, 1));
    host.Clear();
    vm.Languages[3].Value = true;
    Check("editing a language writes its single byte",
        host.WroteOnly(CharacterFormat.OffLanguages + 3, 1));

    // The high-water byte must go with a slot edit, or an item in a far slot is never scanned.
    host.Clear();
    vm.Items[20].ItemId = 27;
    Check("a far item slot writes the slot itself",
        host.Wrote(CharacterFormat.OffInventory + 20 * CharacterFormat.InventoryEntrySize,
                   CharacterFormat.InventoryEntrySize));
    Check("and the high-water byte with it", host.Wrote(CharacterFormat.OffItemCount, 1));
    CheckEq("high-water byte now reads one past the far slot", vm.Record.ItemCount, 21);
    host.Clear();
    vm.Items[20].Condition = 50;
    Check("editing that slot again does not rewrite an unchanged high-water byte",
        !host.Wrote(CharacterFormat.OffItemCount, 1));
    host.Clear();
    vm.Spells[10].SpellId = 5;
    Check("a spell slot writes the slot",
        host.Wrote(CharacterFormat.OffSpells + 10 * CharacterFormat.SpellEntrySize,
                   CharacterFormat.SpellEntrySize));
    Check("and its high-water byte", host.Wrote(CharacterFormat.OffSpellCount, 1));

    // Coercions that keep the record acceptable to the locator.
    vm.MaxHp = 200; vm.CurHp = 200;
    vm.MaxHp = 50;
    CheckEq("lowering max HP pulls current HP down with it", vm.Record.CurHp, 50);
    vm.CurHp = 9999;
    CheckEq("current HP cannot exceed the maximum", vm.Record.CurHp, 50);
    vm.MaxWeight = 0;
    Check("max load cannot drop below the carried load", vm.Record.MaxWeight > vm.Record.CurWeight);

    // Every editor ceiling has to produce a record the locator still accepts, or an ordinary edit
    // would lock the user out of their own party.
    var edge = new CharacterRecord(FakeRecord.Make("Edge"));
    var edgeVm = new CharacterViewModel(new RecordingHost(), new LocatedCharacter(0x2000, 0, edge));
    edgeVm.MaxHp = CharacterFormat.MaxHitPoints;
    edgeVm.CurHp = CharacterFormat.MaxHitPoints;
    edgeVm.MaxWeight = CharacterFormat.MaxLoadPounds;
    edgeVm.Gold = CharacterFormat.MaxGold;
    edgeVm.Rations = CharacterFormat.MaxRations;
    edgeVm.Level = 99;
    Check("a record at every editor ceiling still passes IsValidRecord",
        CharacterRecord.IsValidRecord(edge.Bytes, 0));
    CheckEq("max load ceiling stays inside the validator's limit",
        CharacterFormat.MaxLoadPounds * 10 <= CharacterFormat.MaxPlausibleLoadTenths, true);
    CheckEq("gold ceiling is not below what the record can hold", CharacterFormat.MaxGold, (long)uint.MaxValue);
    CheckEq("rations ceiling is not below what the record can hold", CharacterFormat.MaxRations, ushort.MaxValue);

    // Freezes must converge: capture, then re-apply, must leave nothing to do on the second pass.
    var frozen = new CharacterRecord(FakeRecord.Make("Frost", maxHp: 40, curHp: 40, gold: 250000));
    var fh = new RecordingHost();
    var fvm = new CharacterViewModel(fh, new LocatedCharacter(0x3000, 0, frozen));
    fvm.FreezeGold = true;
    CheckEq("freezing gold keeps the value it found, not a clamped one", fvm.Record.Gold, 250000L);
    fh.Clear();
    fvm.ApplyFreezes();
    CheckEq("a settled gold freeze writes nothing", fh.Writes.Count, 0);
    frozen.Gold = 10;
    fh.Clear();
    fvm.ApplyFreezes();
    Check("but it does restore a value the game changed", fh.Wrote(CharacterFormat.OffGold, 4));
    CheckEq("to the captured figure", fvm.Record.Gold, 250000L);
    fh.Clear();
    fvm.ApplyFreezes();
    CheckEq("and then settles again", fh.Writes.Count, 0);

    fvm.FreezeHp = true;
    frozen.CurHp = 1;
    fh.Clear();
    fvm.ApplyFreezes();
    Check("an HP freeze restores current to maximum", fh.Wrote(CharacterFormat.OffCurHp, 2));
    CheckEq("current HP now equals maximum", fvm.Record.CurHp, fvm.Record.MaxHp);
    fh.Clear();
    fvm.ApplyFreezes();
    CheckEq("and the HP freeze settles", fh.Writes.Count, 0);

    // The poll path must stay silent when nothing moved.
    var poll = new CharacterRecord(FakeRecord.Make("Still"));
    var ph = new RecordingHost();
    var pvm = new CharacterViewModel(ph, new LocatedCharacter(0x4000, 0, poll));
    int raised = 0;
    pvm.PropertyChanged += (_, _) => raised++;
    pvm.UpdateFrom((byte[])poll.Bytes.Clone());
    CheckEq("an unchanged poll raises no notifications", raised, 0);
    CheckEq("and writes nothing", ph.Writes.Count, 0);
    var moved = (byte[])poll.Bytes.Clone();
    moved[CharacterFormat.OffCurHp] = 3;
    pvm.UpdateFrom(moved);
    Check("a changed poll does raise notifications", raised > 0);
}

// ---------------------------------------------------------------- shipped files (optional)
Section("Shipped game files");
if (gameDir == null || !Directory.Exists(gameDir))
{
    Console.WriteLine("  (skipped — pass --game <LOF directory> to parse ROST\\ROST and GAMES\\GAMEn)");
    skipped++;
}
else
{
    string rost = Path.Combine(gameDir, "ROST", "ROST");
    if (File.Exists(rost))
    {
        var bytes = File.ReadAllBytes(rost);
        int count = bytes[0];
        const int fileStride = 414;
        CheckEq("ROST size is 1 + count x 414", bytes.Length, 1 + count * fileStride);
        Check("ROST header count is plausible", count is > 0 and <= CharacterFormat.RosterSlots);
        int valid = 0;
        for (int i = 0; i < count; i++)
        {
            var rec = new byte[CharacterFormat.RecordSize];
            Array.Copy(bytes, 1 + i * fileStride, rec, 0, CharacterFormat.RecordSize);
            if (CharacterRecord.IsValidRecord(rec, 0)) valid++;
        }
        CheckEq("every ROST entry parses as a character", valid, count);
    }
    else
    {
        Console.WriteLine("  (no ROST\\ROST in that directory)");
        skipped++;
    }

    string games = Path.Combine(gameDir, "GAMES");
    if (Directory.Exists(games))
    {
        foreach (var f in Directory.GetFiles(games, "GAME?"))
        {
            var bytes = File.ReadAllBytes(f);
            string name = Path.GetFileName(f);
            CheckEq($"{name} size", bytes.Length, 13134);
            Check($"{name} carries the FOL signature",
                bytes.Length > 4 && bytes[1] == 'F' && bytes[2] == 'O' && bytes[3] == 'L');
            int occupied = 0;
            for (int i = 0; i < CharacterFormat.PartySlots; i++)
            {
                int off = 4 + i * CharacterFormat.RecordSize;
                if (CharacterRecord.IsValidRecord(bytes, off)) occupied++;
                else if (!CharacterRecord.IsEmptySlot(bytes, off))
                {
                    Check($"{name} slot {i} is either a character or empty", false);
                    occupied = -1;
                    break;
                }
            }
            Check($"{name} party slots all parse", occupied >= 0);
        }
    }
    else
    {
        Console.WriteLine("  (no GAMES directory)");
        skipped++;
    }
}

// ---------------------------------------------------------------- live (optional)
Section("Live process");
if (livePid == 0)
{
    Console.WriteLine("  (skipped — pass --live <dosbox pid> to locate against a running game)");
    skipped++;
}
else
{
    try
    {
        using var pm = ProcessMemory.Open(livePid);
        var src = new ProcessMemorySource(pm);
        var found = GameLocator.Find(src, out string status);
        Console.WriteLine("  " + status);
        Check("located the live game", found != null);
        if (found != null)
        {
            Console.WriteLine($"  DGROUP 0x{(ulong)found.DgroupAddress:X}  guest0 0x{(ulong)found.GuestZero:X}");
            Console.WriteLine($"  party  0x{(ulong)found.PartyAddress:X} ({found.Party.Count} members)");
            Console.WriteLine($"  roster 0x{(ulong)found.RosterAddress:X} ({found.Roster.Count} entries)");
            foreach (var c in found.Party)
                Console.WriteLine($"    slot {c.Slot}: {c.Record.Name,-12} Rnk {c.Record.Level,-3} {c.Record.ClassName,-12} " +
                                  $"HP {c.Record.CurHp}/{c.Record.MaxHp}  gold {c.Record.Gold}  xp {c.Record.Experience}");
            Check("the live party is packed from slot 0",
                found.Party.Select((c, i) => c.Slot == i).All(x => x));
            Check("the live roster is packed from slot 0",
                found.Roster.Select((c, i) => c.Slot == i).All(x => x));
            CheckEq("the live adjacency cross-check holds", found.Adjacency, AdjacencyResult.Holds);
        }
    }
    catch (Exception ex)
    {
        Check("live locate threw: " + ex.Message, false);
    }
}

// ---------------------------------------------------------------- summary
Console.WriteLine();
Console.WriteLine(new string('-', 60));
Console.WriteLine($"passed {passed}, failed {failed}, skipped groups {skipped}");
if (failed > 0)
{
    Console.WriteLine("\nFailures:");
    foreach (var f in failures) Console.WriteLine("  - " + f);
}
return failed == 0 ? 0 : 1;
