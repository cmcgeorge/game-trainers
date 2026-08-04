using Wizardry1Trainer.Game;

namespace FormatCheck;

internal static class Program
{
    private static int _checks;
    private static int _passed;

    private static void Check(string label, bool condition)
    {
        _checks++;
        if (condition) { _passed++; }
        else { Console.WriteLine($"  FAIL: {label}"); }
    }

    private static void Section(string name)
    {
        Console.WriteLine($"--- {name} ---");
    }

    private static int Main()
    {
        Console.WriteLine("Wizardry 1 Trainer FormatCheck");
        Console.WriteLine();

        // --- Record size and constants ----------------------------------------
        Section("Constants");
        Check("RecordSize = 207", CharacterFormat.RecordSize == 207);
        Check("MaxSlots = 6", CharacterFormat.MaxSlots == 6);
        Check("MaxAttribute = 18", CharacterFormat.MaxAttribute == 18);
        Check("SpellCount = 50", CharacterFormat.SpellCount == 50);
        Check("SpellKnowledgeBytes = 8", CharacterFormat.SpellKnowledgeBytes == 8);

        // --- Attribute packing ------------------------------------------------
        Section("Attribute Packing");
        byte[] attr = new byte[4];
        CharacterFormat.WriteAttributes(attr, 0, 18, 18, 18, 18, 18, 18);
        Check("All-18s packs to 52 4A 52 4A",
            attr[0] == 0x52 && attr[1] == 0x4A && attr[2] == 0x52 && attr[3] == 0x4A);
        var (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(attr, 0);
        Check("All-18s reads back as 18/18/18/18/18/18",
            s == 18 && i == 18 && p == 18 && v == 18 && a == 18 && l == 18);

        CharacterFormat.WriteAttributes(attr, 0, 3, 3, 3, 3, 3, 3);
        (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(attr, 0);
        Check("All-3s round-trips", s == 3 && i == 3 && p == 3 && v == 3 && a == 3 && l == 3);

        CharacterFormat.WriteAttributes(attr, 0, 10, 11, 12, 13, 14, 15);
        (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(attr, 0);
        Check("Mixed attrs round-trip (10,11,12,13,14,15)",
            s == 10 && i == 11 && p == 12 && v == 13 && a == 14 && l == 15);

        CharacterFormat.WriteAttributes(attr, 0, 0, 0, 0, 0, 0, 0);
        (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(attr, 0);
        Check("All-0s round-trips", s == 0 && i == 0 && p == 0 && v == 0 && a == 0 && l == 0);

        CharacterFormat.WriteAttributes(attr, 0, 25, 25, 25, 25, 25, 25);
        (s, i, p, v, a, l) = CharacterFormat.ReadAttributes(attr, 0);
        Check("Clamped to 18", s == 18 && i == 18 && p == 18 && v == 18 && a == 18 && l == 18);

        // --- TWIZLONG (gold/experience) ---------------------------------------
        Section("TWIZLONG Encoding");
        byte[] wiz = new byte[6];
        CharacterFormat.WriteWizLong(wiz, 0, 0);
        Check("Zero writes all-zero", wiz.All(b => b == 0));
        Check("Zero reads back 0", CharacterFormat.ReadWizLong(wiz, 0) == 0);

        CharacterFormat.WriteWizLong(wiz, 0, 10000);
        Check("10000 -> MID=1, LOW=0",
            wiz[0] == 0 && wiz[1] == 0 && wiz[2] == 1 && wiz[3] == 0 && wiz[4] == 0 && wiz[5] == 0);
        Check("10000 reads back", CharacterFormat.ReadWizLong(wiz, 0) == 10000);

        CharacterFormat.WriteWizLong(wiz, 0, 100000000);
        Check("100M -> HIGH=1",
            wiz[4] == 1 && wiz[5] == 0);
        Check("100M reads back", CharacterFormat.ReadWizLong(wiz, 0) == 100000000);

        CharacterFormat.WriteWizLong(wiz, 0, 123456789);
        Check("123456789 reads back", CharacterFormat.ReadWizLong(wiz, 0) == 123456789);

        CharacterFormat.WriteWizLong(wiz, 0, 9999999999);
        Check("9999999999 reads back", CharacterFormat.ReadWizLong(wiz, 0) == 9999999999);

        // --- Character record round-trip --------------------------------------
        Section("Character Record");
        var rec = new byte[CharacterFormat.RecordSize];
        var cr = new CharacterRecord(rec);

        cr.Name = "TESTCHAR";
        Check("Name writes length byte", cr.Bytes[CharacterFormat.OffName] == 8);
        Check("Name reads back", cr.Name == "TESTCHAR");

        cr.Name = "A";
        Check("Single-char name", cr.Name == "A");
        cr.Name = "ABCDEFGHIJKLMNOP"; // 16 chars, should truncate to 15
        Check("Name truncates to 15", cr.Name == "ABCDEFGHIJKLMNO");

        cr.Race = 3;
        Check("Race = Dwarf (3)", cr.Race == 3);
        Check("RaceName = Dwarf", cr.RaceName == "Dwarf");
        Check("Race clamps 1..5", new CharacterRecord(new byte[CharacterFormat.RecordSize]) { Race = 0 }.Race == 1);

        cr.Class = 5;
        Check("Class = Samurai (5)", cr.Class == 5);
        Check("ClassName = Samurai", cr.ClassName == "Samurai");

        cr.Alignment = 2;
        Check("Alignment = Neutral (2)", cr.Alignment == 2);
        Check("AlignmentName = Neutral", cr.AlignmentName == "Neutral");

        cr.Strength = 18;
        Check("Strength set to 18", cr.Strength == 18);
        cr.Intelligence = 15;
        Check("Intelligence set to 15", cr.Intelligence == 15);
        cr.Luck = 7;
        Check("Luck set to 7", cr.Luck == 7);
        Check("Strength still 18 after other attrs set", cr.Strength == 18);
        Check("Intelligence still 15 after Luck set", cr.Intelligence == 15);

        cr.SetAllAttributes(18);
        Check("All attrs = 18",
            cr.Strength == 18 && cr.Intelligence == 18 && cr.Piety == 18 &&
            cr.Vitality == 18 && cr.Agility == 18 && cr.Luck == 18);

        cr.HpCurrent = 50;
        cr.HpMax = 99;
        Check("HP set/get", cr.HpCurrent == 50 && cr.HpMax == 99);

        cr.Level = 12;
        Check("Level set/get", cr.Level == 12);

        cr.Gold = 5000000;
        Check("Gold set/get", cr.Gold == 5000000);

        cr.Experience = 250000;
        Check("Experience set/get", cr.Experience == 250000);

        cr.Status = CharacterFormat.StatusDead;
        Check("Status = Dead", cr.Status == CharacterFormat.StatusDead);
        Check("StatusName = Dead", cr.StatusName == "Dead");

        cr.ArmorClass = 3;
        Check("ArmorClass set/get", cr.ArmorClass == 3);

        // --- Spells -----------------------------------------------------------
        Section("Spells");
        Array.Clear(rec, 0, rec.Length);
        cr = new CharacterRecord(rec);

        cr.SetSpellKnown(0, true);
        Check("Spell 0 known", cr.GetSpellKnown(0));
        Check("Spell 1 not known", !cr.GetSpellKnown(1));
        Check("Spell 0 bit in byte 0", (cr.Bytes[CharacterFormat.OffSpellKnowledge] & 1) != 0);

        cr.SetSpellKnown(49, true);
        Check("Spell 49 known", cr.GetSpellKnown(49));
        Check("Spell 49 in byte 6 bit 1", (cr.Bytes[CharacterFormat.OffSpellKnowledge + 6] & 2) != 0);

        cr.LearnAllSpells();
        bool allKnown = true;
        for (int sp = 0; sp < 50; sp++)
            if (!cr.GetSpellKnown(sp)) { allKnown = false; break; }
        Check("LearnAllSpells sets all 50 bits", allKnown);

        cr.SetMageSpellCharges(1, 7);
        Check("Mage L1 charges = 7", cr.GetMageSpellCharges(1) == 7);
        cr.SetPriestSpellCharges(7, 9);
        Check("Priest L7 charges = 9", cr.GetPriestSpellCharges(7) == 9);

        cr.SetAllSpellCharges(9);
        bool allMax = true;
        for (int lvl = 1; lvl <= 7; lvl++)
        {
            if (cr.GetMageSpellCharges(lvl) != 9) { allMax = false; break; }
            if (cr.GetPriestSpellCharges(lvl) != 9) { allMax = false; break; }
        }
        Check("SetAllSpellCharges(9) maxes all levels", allMax);

        // --- SpellBook --------------------------------------------------------
        Section("SpellBook");
        Check("SpellBook has 50 spells", SpellBook.Spells.Count == 50);
        Check("21 mage spells", SpellBook.MageSpells.Count == 21);
        Check("29 priest spells", SpellBook.PriestSpells.Count == 29);
        Check("First spell = Dumapic (index 0)", SpellBook.Spells[0].Name == "Dumapic");
        Check("Last spell = Malikto (index 49)", SpellBook.Spells[49].Name == "Malikto");
        Check("Mage L1 has 4 spells",
            SpellBook.MageSpells.Count(s => s.Level == 1) == 4);
        Check("Priest L1 has 5 spells",
            SpellBook.PriestSpells.Count(s => s.Level == 1) == 5);
        Check("Tiltowait is index 20", SpellBook.Spells[20].Name == "Tiltowait");
        Check("Kadorto is index 48", SpellBook.Spells[48].Name == "Kadorto");

        // --- IsOccupied / validation -----------------------------------------
        Section("IsOccupied");
        var empty = new byte[CharacterFormat.RecordSize];
        Check("Empty record not occupied", !new CharacterRecord(empty).IsOccupied);

        var good = new byte[CharacterFormat.RecordSize];
        good[CharacterFormat.OffName] = 5;
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("ALICE"), 0, good, CharacterFormat.OffName + 1, 5);
        good[CharacterFormat.OffRace] = 1; good[CharacterFormat.OffRace + 1] = 0;
        good[CharacterFormat.OffClass] = 0; good[CharacterFormat.OffClass + 1] = 0;
        good[CharacterFormat.OffAlignment] = 1; good[CharacterFormat.OffAlignment + 1] = 0;
        CharacterFormat.WriteAttributes(good, CharacterFormat.OffAttributes, 12, 12, 12, 12, 12, 12);
        good[CharacterFormat.OffHpMax] = 30; good[CharacterFormat.OffHpMax + 1] = 0;
        good[CharacterFormat.OffLevel] = 1; good[CharacterFormat.OffLevel + 1] = 0;
        Check("Well-formed record is occupied", new CharacterRecord(good).IsOccupied);

        good[CharacterFormat.OffName] = 0;
        Check("Zero-length name not occupied", !new CharacterRecord(good).IsOccupied);

        good[CharacterFormat.OffName] = 5;
        good[CharacterFormat.OffHpMax] = 0; good[CharacterFormat.OffHpMax + 1] = 0;
        Check("HP max 0 not occupied", !new CharacterRecord(good).IsOccupied);
        good[CharacterFormat.OffHpMax] = 30; good[CharacterFormat.OffHpMax + 1] = 0;
        good[CharacterFormat.OffHpMax] = 0xE8; good[CharacterFormat.OffHpMax + 1] = 0x03;
        Check("HP max 1000 not occupied (> 999)", !new CharacterRecord(good).IsOccupied);
        good[CharacterFormat.OffHpMax] = 30; good[CharacterFormat.OffHpMax + 1] = 0;

        good[CharacterFormat.OffName] = 5;
        good[CharacterFormat.OffRace] = 0; good[CharacterFormat.OffRace + 1] = 0;
        Check("Race 0 not occupied", !new CharacterRecord(good).IsOccupied);

        // --- RosterLocator.IsValidCharacter ----------------------------------
        Section("RosterLocator.IsValidCharacter");
        var valid = new byte[CharacterFormat.RecordSize];
        valid[CharacterFormat.OffName] = 5;
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("ALICE"), 0, valid, CharacterFormat.OffName + 1, 5);
        valid[CharacterFormat.OffRace] = 1; valid[CharacterFormat.OffRace + 1] = 0;
        valid[CharacterFormat.OffClass] = 0; valid[CharacterFormat.OffClass + 1] = 0;
        valid[CharacterFormat.OffAlignment] = 1; valid[CharacterFormat.OffAlignment + 1] = 0;
        CharacterFormat.WriteAttributes(valid, CharacterFormat.OffAttributes, 12, 12, 12, 12, 12, 12);
        valid[CharacterFormat.OffHpMax] = 30; valid[CharacterFormat.OffHpMax + 1] = 0;
        valid[CharacterFormat.OffLevel] = 1; valid[CharacterFormat.OffLevel + 1] = 0;
        Check("Good record validates",
            Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        Check("Empty record rejects",
            !Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(empty, 0));

        valid[CharacterFormat.OffHpMax] = 0; valid[CharacterFormat.OffHpMax + 1] = 0;
        Check("HP max 0 rejects", !Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));

        // Restore HP max for further checks
        valid[CharacterFormat.OffHpMax] = 30; valid[CharacterFormat.OffHpMax + 1] = 0;

        // Status validation: 0..7 all valid (OK, Afraid, Asleep, Paralyzed, Stoned, Dead, Ashes, Lost)
        valid[CharacterFormat.OffStatus] = CharacterFormat.StatusOK; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status OK validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = CharacterFormat.StatusDead; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status Dead validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = CharacterFormat.StatusLost; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status Lost validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = 3; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status 3 validates (temp status)", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = 7; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status 7 validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = 8; valid[CharacterFormat.OffStatus + 1] = 0;
        Check("Status 8 rejects", !Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = 0; valid[CharacterFormat.OffStatus + 1] = 1;
        Check("Status 256 rejects", !Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffStatus] = 0; valid[CharacterFormat.OffStatus + 1] = 0;

        // EquipCount validation: 0..8 valid, > 8 rejects
        valid[CharacterFormat.OffEquipmentCount] = 0; valid[CharacterFormat.OffEquipmentCount + 1] = 0;
        Check("EquipCount 0 validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffEquipmentCount] = 8; valid[CharacterFormat.OffEquipmentCount + 1] = 0;
        Check("EquipCount 8 validates", Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffEquipmentCount] = 9; valid[CharacterFormat.OffEquipmentCount + 1] = 0;
        Check("EquipCount 9 rejects", !Wizardry1Trainer.Memory.RosterLocator.IsValidCharacter(valid, 0));
        valid[CharacterFormat.OffEquipmentCount] = 0; valid[CharacterFormat.OffEquipmentCount + 1] = 0;

        // --- Status names -----------------------------------------------------
        Section("Status Names");
        Check("Status OK", CharacterFormat.StatusName(CharacterFormat.StatusOK) == "OK");
        Check("Status Dead", CharacterFormat.StatusName(CharacterFormat.StatusDead) == "Dead");
        Check("Status Lost", CharacterFormat.StatusName(CharacterFormat.StatusLost) == "Lost");
        Check("Status Ashes", CharacterFormat.StatusName(CharacterFormat.StatusAshes) == "Ashes");
        Check("Status Stoned", CharacterFormat.StatusName(CharacterFormat.StatusStoned) == "Stoned");

        // --- Race/Class/Alignment names --------------------------------------
        Section("Race/Class/Alignment Names");
        Check("Race 1 = Human", CharacterFormat.RaceName(1) == "Human");
        Check("Race 5 = Hobbit", CharacterFormat.RaceName(5) == "Hobbit");
        Check("Class 0 = Fighter", CharacterFormat.ClassName(0) == "Fighter");
        Check("Class 7 = Ninja", CharacterFormat.ClassName(7) == "Ninja");
        Check("Align 1 = Good", CharacterFormat.AlignmentName(1) == "Good");
        Check("Align 3 = Evil", CharacterFormat.AlignmentName(3) == "Evil");

        // --- Summary ----------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine($"Passed {_passed} of {_checks} checks.");
        return _passed == _checks ? 0 : 1;
    }
}
