using System.IO;
using System.Globalization;
using System.Text;
using SwordOfAragonTrainer.Game;
using SwordOfAragonTrainer.Memory;
using SwordOfAragonTrainer.ViewModels;

namespace FormatCheck;

/// <summary>
/// Headless verification harness for the Sword of Aragon trainer's game layer. Exits 0 when every
/// check passes and 1 otherwise, so <c>Run.ps1 -Test</c> can gate on it.
///
/// The checks fall into three groups:
/// <list type="bullet">
/// <item><b>Format arithmetic</b> — MBF round-trips, the cost model against the twelve worked examples
/// in <c>docs/RE.md</c> §6.4 (eight from a Knight campaign, four from a Warrior one), the documented
/// hard limits pinned to literals, and the reference tables' invariants.</item>
/// <item><b>Synthetic fixtures</b> — a hand-built roster and kingdom save that prove the parsers write
/// to the offsets/fields they claim and leave everything else byte-identical.</item>
/// <item><b>Real saves</b> — when a game directory is supplied (argument, or the default scratch path),
/// every shipped <c>ARAGON.HS?</c>/<c>ARAGON.HR?</c> pair is parsed and the cost model is checked
/// against every occupied record. Skipped, not failed, when the copyrighted files are absent.</item>
/// </list>
/// </summary>
internal static class Program
{
    private const string DefaultGameDirectory = @"C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\SARAGON";

    private static int _checks;
    private static int _failures;
    private static readonly List<string> Notes = new();

    private static int Main(string[] args)
    {
        Console.WriteLine("Sword of Aragon trainer — format checks");
        Console.WriteLine(new string('-', 64));

        CheckMbf();
        CheckCostModel();
        CheckHardLimits();
        CheckReferenceTables();
        CheckRosterFixture();
        CheckKingdomFixture();
        CheckSegmentSearch();

        CheckViewModelRules();

        string directory = args.Length > 0 ? args[0] : DefaultGameDirectory;
        CheckRealSaves(directory);

        Console.WriteLine(new string('-', 64));
        foreach (string note in Notes) Console.WriteLine("note: " + note);
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed.");
        if (_failures > 0) Console.WriteLine($"FAILED ({_failures} failure{(_failures == 1 ? "" : "s")}).");
        else Console.WriteLine("OK.");
        return _failures > 0 ? 1 : 0;
    }

    // ================================================================== MBF singles
    private static void CheckMbf()
    {
        Section("Microsoft Binary Format singles");

        // Decoded from ARAGON.HRA: the player character's experience points.
        Approx("decode 5,268.97 from cb a7 24 8d",
            Mbf.ToDouble(new byte[] { 0xCB, 0xA7, 0x24, 0x8D }), 5268.97, 0.01);
        // ARAGON.HRA slot 4 holds exactly 3000 XP, which exercises the zero-mantissa-ish path.
        Approx("decode 3,000 from 00 80 3b 8c",
            Mbf.ToDouble(new byte[] { 0x00, 0x80, 0x3B, 0x8C }), 3000.0, 0.01);
        Equal("exponent byte 0 decodes as zero", Mbf.ToDouble(new byte[] { 0x11, 0x22, 0x33, 0x00 }), 0.0);

        foreach (double value in new[] { 1.0, 2.0, 0.5, 100.0, 702.95, 6500.0, 26874.26, 213821.51,
                                         9_999_999.0, -1.0, -4682.4 })
        {
            double back = Mbf.ToDouble(Mbf.GetBytes(value));
            // MBF singles carry a 23-bit mantissa, so allow a relative epsilon rather than exactness.
            Approx($"round-trip {value}", back, value, Math.Max(1e-6, Math.Abs(value) * 1e-6));
        }

        Equal("zero encodes to four clear bytes", BitConverter.ToUInt32(Mbf.GetBytes(0.0)), 0u);
        Equal("NaN encodes to zero", BitConverter.ToUInt32(Mbf.GetBytes(double.NaN)), 0u);
        Equal("tiny values underflow to zero", Mbf.ToDouble(Mbf.GetBytes(1e-45)), 0.0);
        True("huge values saturate rather than wrap",
            Mbf.ToDouble(Mbf.GetBytes(1e40)) >= Mbf.MaxMagnitude * 0.999);
        Equal("negative round-trips keep their sign", Math.Sign(Mbf.ToDouble(Mbf.GetBytes(-12.5))), -1);

        uint raw = BitConverter.ToUInt32(Mbf.GetBytes(702.95));
        Approx("FromRaw matches ToDouble", Mbf.FromRaw(raw), 702.95, 0.001);

        // The property the live scanner depends on: positive MBF singles order the same way as the
        // unsigned little-endian words they occupy, so Increased/Decreased narrowing works on gold.
        bool monotonic = true;
        double previous = 0;
        uint previousRaw = 0;
        for (double value = 1; value < 5_000_000; value *= 1.37)
        {
            uint current = BitConverter.ToUInt32(Mbf.GetBytes(value));
            if (value > previous && current <= previousRaw) monotonic = false;
            previous = value;
            previousRaw = current;
        }
        True("positive MBF singles are monotonic as unsigned Int32", monotonic);
    }

    // =================================================================== cost model
    private static void CheckCostModel()
    {
        Section("Make / train / upkeep cost model");

        // The first eight worked examples in docs/RE.md §6.4, from ARAGON.HRA (a Knight campaign).
        const int knight = 7;
        CheckCosts("player Knight (plate, kite, mace, lance, heavy horse, mail barding)",
            7, new[] { 5, 3, 2, 3, 0, 0, 3, 3 }, knight, 320, 28, 79);
        CheckCosts("Keth, Knight henchman", 7, new[] { 4, 3, 2, 3, 0, 0, 3, 2 }, knight, 260, 26, 72);
        CheckCosts("Roush, Ranger henchman", 8, new[] { 3, 0, 3, 0, 0, 2, 2, 2 }, knight, 224, 29, 66);
        CheckCosts("Palaro, Priest henchman", 9, new[] { 2, 0, 2, 0, 0, 0, 1, 1 }, knight, 190, 27, 54);
        CheckCosts("1st Cavalry (Knight's 25 % discount)", 3, new[] { 4, 3, 2, 3, 0, 0, 1, 1 }, knight, 102, 8, 38);
        CheckCosts("1st Mounted (Knight's discount reaches mounted infantry)",
            2, new[] { 3, 2, 3, 1, 0, 0, 1, 1 }, knight, 75, 7, 28);
        CheckCosts("2nd Javelins (no discount)", 1, new[] { 2, 2, 3, 0, 2, 0, 0, 0 }, knight, 27, 6, 12);
        CheckCosts("1st Bowmen (no discount)", 4, new[] { 2, 0, 2, 0, 0, 2, 0, 0 }, knight, 27, 7, 15);

        // From ARAGON.HRE (a Warrior campaign): the Warrior's 50 % infantry discount, and the fact that
        // it does not reach cavalry.
        const int warrior = 6;
        CheckCosts("Red Dragons, plate infantry (Warrior's 50 % discount)",
            1, new[] { 5, 2, 3, 1, 0, 0, 0, 0 }, warrior, 48, 4, 12);
        CheckCosts("1st Defenders, chain infantry (Warrior's discount)",
            1, new[] { 3, 2, 3, 1, 0, 0, 0, 0 }, warrior, 18, 3, 7);
        CheckCosts("Lightning Riders, cavalry under a Warrior (no discount)",
            3, new[] { 4, 3, 2, 3, 0, 0, 1, 1 }, warrior, 136, 11, 50);
        CheckCosts("Groo, the Warrior himself (characters never discounted)",
            6, new[] { 4, 1, 3, 1, 1, 0, 2, 1 }, warrior, 176, 20, 54);

        Equal("Warrior discounts infantry", UnitBook.Discount(6, 1), 0.50);
        Equal("Warrior does not discount mounted infantry", UnitBook.Discount(6, 2), 1.00);
        Equal("Knight discounts mounted infantry", UnitBook.Discount(7, 2), 0.75);
        Equal("Knight discounts cavalry", UnitBook.Discount(7, 3), 0.75);
        Equal("Ranger discounts bowmen", UnitBook.Discount(8, 4), 0.75);
        Equal("Ranger discounts horse bowmen", UnitBook.Discount(8, 5), 0.75);
        Equal("Priest discounts nothing", UnitBook.Discount(9, 1), 1.00);
        Equal("no class discounts a character", UnitBook.Discount(7, 7), 1.00);

        Equal("foot troops stack at 2 points", UnitBook.SizePoints(0), 2);
        Equal("light horse stacks at 4", UnitBook.SizePoints(1), 4);
        Equal("medium horse stacks at 5", UnitBook.SizePoints(2), 5);
        Equal("heavy horse stacks at 6", UnitBook.SizePoints(3), 6);
    }

    private static void CheckCosts(string what, int type, int[] equipment, int playerClass,
                                   int make, int train, int maint)
    {
        var costs = UnitBook.ComputeCosts(type, equipment, playerClass);
        Equal($"{what}: make", costs.Make, make);
        Equal($"{what}: train", costs.Train, train);
        Equal($"{what}: upkeep", costs.MaintTenths, maint);
    }

    // ================================================================== hard limits
    // Every clamp in the game layer uses one of these constants, so asserting a clamped value against
    // the same constant proves nothing. These pin the constants themselves to the literals the game
    // and the docs commit to, so widening one shows up here rather than silently in a written save.
    private static void CheckHardLimits()
    {
        Section("Documented hard limits");

        Equal("tax rate ceiling is 80 %", GameFacts.MaxTaxRate, 80);
        Equal("score ceiling is 500", GameFacts.MaxScore, 500);
        Equal("the map is 24 hexes square", GameFacts.MapSize, 24);
        Equal("stacking allowance is 200 points", GameFacts.StackingLimit, 200);
        Equal("a battle lasts at most 23 turns", GameFacts.MaxBattleTurns, 23);
        Equal("Quit is offered from turn 7", GameFacts.EarliestQuitTurn, 7);
        Equal("the campaign opens in 871 QJ", GameFacts.BaseYear, 871);
        Equal("the opening month index is 3 (April)", GameFacts.StartMonth, 3);
        Equal("save letters are A-Y", GameFacts.SaveLetters, "ABCDEFGHIJKLMNOPQRSTUVWXY");
        Equal("wealth ceiling stays inside single-precision integer range",
            GameFacts.MaxWealth, 9_999_999d);
        True("the wealth ceiling round-trips exactly through MBF",
            Math.Abs(Mbf.ToDouble(Mbf.GetBytes(GameFacts.MaxWealth)) - GameFacts.MaxWealth) <= 1.0);

        Equal("a roster is 8,000 bytes", RosterFormat.FileSize, 8_000);
        Equal("a record is 100 bytes", RosterFormat.RecordSize, 100);
        Equal("eighty slots", RosterFormat.SlotCount, 80);
        Equal("twenty character slots", RosterFormat.CharacterSlots, 20);
        Equal("sixty unit slots", RosterFormat.UnitSlots, 60);
        Equal("names are sixteen bytes", RosterFormat.NameLength, 16);
        Equal("the player is slot 0", RosterFormat.PlayerSlot, 0);
        Equal("level ceiling is 99", RosterFormat.MaxLevel, 99);
        Equal("figure ceiling is 999", RosterFormat.MaxMen, 999);

        Equal("a kingdom save has at least 283 lines", KingdomFile.MinLineCount, 283);
        Equal("twenty cities", KingdomFile.CityCount, 20);
        Equal("fourteen lines per city block", CityRecord.BlockLines, 14);
        Equal("seven investment categories", CityRecord.CategoryCount, 7);
        Equal("seven categories in the enum", Enum.GetValues<DevelopmentCategory>().Length, 7);
        Equal("the natural top of the mood scale is 100", CityRecord.FullMood, 100);
        Equal("city treasuries stay int16-shaped", CityRecord.MaxCityGold, short.MaxValue);
        Equal("the DOS end-of-file byte is 0x1A", KingdomFile.EofMarker, (byte)0x1A);

        Equal("a real-mode segment is 64 KiB", GameSignatures.SegmentSize, 0x1_0000);
        Equal("the primary anchor is 38 bytes", GameSignatures.WorldMapPrimary.Bytes.Length,
            GameSignatures.WorldMapPrimaryLength);
        Equal("a majority of validators is required", DgroupLocator.MinValidators, 2);
        True("the required threshold is a real majority of the validators",
            DgroupLocator.MinValidators > GameSignatures.WorldMapValidators.Count / 2 &&
            DgroupLocator.MinValidators <= GameSignatures.WorldMapValidators.Count);
    }

    // ============================================================== reference tables
    private static void CheckReferenceTables()
    {
        Section("Reference tables");

        Equal("ten unit/character types", UnitBook.Types.Count, 10);
        Equal("five of them are characters", UnitBook.Types.Count(t => t.IsCharacter), 5);
        True("type codes run 1..10",
            UnitBook.Types.Select(t => t.Code).SequenceEqual(Enumerable.Range(1, 10)));
        Equal("eight equipment slots", UnitBook.Slots.Length, UnitBook.SlotCount);
        foreach (var slot in UnitBook.Slots)
        {
            True($"{UnitBook.SlotName(slot)} slot index 0 is \"none\"", UnitBook.Items(slot)[0].Index == 0);
            True($"{UnitBook.SlotName(slot)} indices are contiguous",
                UnitBook.Items(slot).Select(i => i.Index).SequenceEqual(
                    Enumerable.Range(0, UnitBook.Items(slot).Count)));
        }
        Equal("armor has five items plus none", UnitBook.Items(EquipmentSlot.Armor).Count, 6);
        Equal("plate needs level 3", UnitBook.Item(EquipmentSlot.Armor, 5).MinLevel, 3);
        Equal("pike needs level 4", UnitBook.Item(EquipmentSlot.Pole, 2).MinLevel, 4);
        Equal("compound bow needs level 5", UnitBook.Item(EquipmentSlot.Bow, 4).MinLevel, 5);
        Equal("an out-of-range index falls back to none",
            UnitBook.Item(EquipmentSlot.Bow, 99).Index, 0);

        Equal("twenty cities and regions", CityBook.Cities.Count, KingdomFile.CityCount);
        Equal("nineteen of them occupy a hex", CityBook.WithHexes.Count(), 19);
        Equal("Aladda is at (6,7)", CityBook.ByName("Aladda")!.PositionCode, 607);
        Equal("Tetrada is at (21,4)", CityBook.ByName("Tetrada")!.PositionCode, 2104);
        Equal("Khalikha has no city hex", CityBook.ByName("Khalikha")!.PositionCode, 0);
        True("every city hex is inside the 24x24 map",
            CityBook.WithHexes.All(c => c.X is >= 0 and < GameFacts.MapSize &&
                                        c.Y is >= 0 and < GameFacts.MapSize));
        True("save-order indices match the list order",
            CityBook.Cities.Select(c => c.Index).SequenceEqual(Enumerable.Range(0, 20)));

        Equal("thirteen protected cities", ProtectionBook.Answers.Count, 13);
        Equal("four protection fields", ProtectionBook.Fields.Length, 4);
        True("every row answers all four fields",
            ProtectionBook.Answers.All(a => ProtectionBook.Fields.All(f => a.ForField(f).Length > 0)));
        True("answers are upper case",
            ProtectionBook.Answers.All(a => ProtectionBook.Fields.All(
                f => a.ForField(f) == a.ForField(f).ToUpperInvariant())));
        Equal("Aladda's ruler answer is YOU", ProtectionBook.ForCity("Aladda")!.Ruler, "YOU");
        Equal("Sur Nova is found despite the space", ProtectionBook.ForCity("SurNova")!.Location, "FOOTHILLS");
        Equal("eight distinct LOCATION answers", ProtectionBook.CandidatesFor("LOCATION").Count, 8);
        Equal("thirteen distinct RULER answers", ProtectionBook.CandidatesFor("RULER").Count, 13);
        Equal("an unknown field has no candidates", ProtectionBook.CandidatesFor("COLOUR").Count, 0);

        Equal("twenty-three spells", SpellBook.Spells.Count, 23);
        foreach (int classCode in new[] { 8, 9, 10 })
        {
            var ladder = SpellBook.Available(classCode, SpellBook.MaxCasterLevel).ToArray();
            Equal($"class {classCode} learns twelve spells", ladder.Length, 12);
            True($"class {classCode}'s ladder has one spell per level 1..12",
                ladder.Select(s => classCode switch
                {
                    8 => s.RangerLevel,
                    9 => s.PriestLevel,
                    _ => s.MageLevel,
                }).OrderBy(l => l).SequenceEqual(Enumerable.Range(1, 12)));
        }
        Equal("a level-4 mage knows four spells", SpellBook.Available(10, 4).Count(), 4);
        Equal("a knight knows none", SpellBook.Available(7, 20).Count(), 0);

        Equal("thirty-two world terrain codes", TerrainBook.WorldTerrain.Count, 32);
        Equal("twenty-two distinct terrain names among them",
            TerrainBook.WorldTerrain.Distinct(StringComparer.Ordinal).Count(), 22);
        Equal("twenty-three named battlefields", TerrainBook.NamedBattlefields.Count, 23);
        Equal("forty-five terrain files in total",
            TerrainBook.WorldTerrain.Distinct(StringComparer.Ordinal).Count() +
            TerrainBook.NamedBattlefields.Count, 45);
        Equal("code 31 is water", TerrainBook.World(31), "Water");
        Equal("an out-of-range terrain code is reported, not thrown", TerrainBook.World(99), "code 99");
        Equal("twenty-one hex feature words", TerrainBook.HexFeatures.Count, 21);

        Equal("twelve month names", GameFacts.Months.Length, 12);
        Equal("month 3 of year 0 is April 871 QJ", GameFacts.FormatDate(0, 3), "April 871 QJ");
        Equal("a nonsense month does not throw", GameFacts.FormatDate(1, 42), "month 42 872 QJ");
        Equal("save file names follow the letter", GameFacts.KingdomFileName('a'), "ARAGON.HSA");
        Equal("roster file names follow the letter", GameFacts.RosterFileName('y'), "ARAGON.HRY");
    }

    // =============================================================== roster fixture
    private static void CheckRosterFixture()
    {
        Section("Roster records (synthetic fixture)");

        var original = BuildRoster();
        var roster = RosterFile.FromBytes(original);

        Equal("eighty slots", roster.Records.Count, RosterFormat.SlotCount);
        Equal("player class read from slot 0", roster.PlayerClassCode, 7);
        Equal("player name", roster.Player.Name, "NetDanzr");
        Equal("occupied characters", roster.Characters.Count(), 2);
        Equal("occupied units", roster.Units.Count(), 1);
        True("slot 0 is a character slot", roster.Records[0].IsCharacterSlot);
        True("slot 20 is not", !roster.Records[20].IsCharacterSlot);
        True("an untouched slot reads as empty", !roster.Records[40].IsOccupied);

        var player = roster.Player;
        Equal("level", player.Level, 5);
        Equal("men", player.Men, 1);
        Equal("map X", player.X, 6);
        Equal("map Y", player.Y, 7);
        Approx("experience", player.Experience, 5268.97, 0.05);
        Equal("armor slot", player.GetEquipment(EquipmentSlot.Armor), 5);
        Equal("barding slot", player.GetEquipment(EquipmentSlot.Barding), 3);

        // Writes must land on the documented offsets and keep the byte mirrors in step.
        player.Level = 25;
        var image = roster.ToArray();
        Equal("level write", ReadInt16(image, 0, RosterFormat.OffLevel), 25);
        Equal("level byte mirror follows", image[RosterFormat.OffPackedLevel], (byte)25);
        player.TypeCode = 6;
        image = roster.ToArray();
        Equal("type write", ReadInt16(image, 0, RosterFormat.OffType), 6);
        Equal("type byte mirror follows", image[RosterFormat.OffPackedType], (byte)6);
        player.TypeCode = 7;

        True("the caller's buffer is not mutated by FromBytes",
            original.AsSpan().SequenceEqual(BuildRoster()));

        player.Name = "A rather long name that overflows";
        Equal("a long name is truncated to sixteen", player.Name.Length, 16);
        player.Name = "Tab\there";
        True("control characters become spaces", !player.Name.Contains('\t'));
        player.Name = "NetDanzr";
        Equal("name round-trips", player.Name, "NetDanzr");

        player.Level = 5;
        player.X = 99;
        Equal("map X is clamped to the map", player.X, GameFacts.MapSize - 1);
        player.X = -3;
        Equal("map X cannot go negative", player.X, 0);
        player.X = 6;
        player.Men = 100_000;
        Equal("men are clamped", player.Men, RosterFormat.MaxMen);
        player.Men = 1;
        player.Level = 1000;
        Equal("level is clamped", player.Level, RosterFormat.MaxLevel);
        player.Level = 5;

        // Derived fields are recomputed from the price tables, matching the game's own values.
        var unit = roster.Records[20];
        Equal("unit is cavalry", unit.TypeCode, 3);
        unit.RecomputeDerived(roster.PlayerClassCode);
        Equal("recomputed make cost", unit.MakeCost, 102);
        Equal("recomputed train cost", unit.TrainCost, 8);
        Equal("recomputed upkeep", unit.MaintTenths, 38);
        Equal("recomputed stacking size", unit.SizePoints, 4);
        unit.SetEquipment(EquipmentSlot.Horse, 3);
        unit.RecomputeDerived(roster.PlayerClassCode);
        Equal("heavy horse raises the size", unit.SizePoints, 6);
        Equal("heavy horse raises the make cost", unit.MakeCost, 140);
        unit.Men = 30;
        Equal("stacking cost is men x size", unit.StackingCost, 180);

        Equal("highest armour at level 1 is mail", roster.Records[20].HighestAllowedEquipment(EquipmentSlot.Armor), 4);
        roster.Records[20].Level = 5;
        Equal("highest armour at level 5 is plate", roster.Records[20].HighestAllowedEquipment(EquipmentSlot.Armor), 5);
        roster.Records[20].Level = 1;

        // Every record the fixture did not edit must be byte-identical — including slot 1, which is
        // populated (a scan that started past it would only ever be comparing zeros to zeros and could
        // not catch a write that overran slot 0's name field into its neighbour).
        var edited = roster.ToArray();
        var untouched = Enumerable.Range(0, RosterFormat.SlotCount)
            .Where(slot => slot != 0 && slot != RosterFormat.FirstUnitSlot)
            .ToArray();
        int firstDrift = -1;
        foreach (int slot in untouched)
        {
            int start = RosterFormat.RecordOffset(slot);
            if (!edited.AsSpan(start, RosterFormat.RecordSize)
                       .SequenceEqual(original.AsSpan(start, RosterFormat.RecordSize)))
            {
                firstDrift = slot;
                break;
            }
        }
        Equal("no unedited record changed (slot index, -1 = none)", firstDrift, -1);
        True("the untouched set includes the populated slot 1", untouched.Contains(1));

        Throws<InvalidDataException>("a short file is rejected", () => RosterFile.FromBytes(new byte[10]));
        Throws<InvalidDataException>("a file whose slot 0 is not a character is rejected", () =>
        {
            var bad = new byte[RosterFormat.FileSize];
            WriteInt16(bad, 0, RosterFormat.OffType, 3);      // cavalry in the player's slot
            RosterFile.FromBytes(bad);
        });
    }

    private static byte[] BuildRoster()
    {
        var bytes = new byte[RosterFormat.FileSize];

        WriteRecord(bytes, 0, "NetDanzr", type: 7, level: 5, men: 1, x: 6, y: 7,
            equipment: new[] { 5, 3, 2, 3, 0, 0, 3, 3 }, experience: 5268.97);
        WriteRecord(bytes, 1, "Keth", type: 7, level: 2, men: 1, x: 6, y: 7,
            equipment: new[] { 4, 3, 2, 3, 0, 0, 3, 2 }, experience: 2240.69);
        WriteRecord(bytes, RosterFormat.FirstUnitSlot, "1st Cavalry", type: 3, level: 1, men: 19, x: 6, y: 7,
            equipment: new[] { 4, 3, 2, 3, 0, 0, 1, 1 }, experience: 37012.45);
        return bytes;
    }

    private static void WriteRecord(byte[] bytes, int slot, string name, int type, int level, int men,
                                    int x, int y, int[] equipment, double experience)
    {
        int start = RosterFormat.RecordOffset(slot);
        var padded = name.PadRight(RosterFormat.NameLength).Substring(0, RosterFormat.NameLength);
        Encoding.ASCII.GetBytes(padded, 0, RosterFormat.NameLength, bytes, start + RosterFormat.OffName);
        Mbf.Write(bytes.AsSpan(), experience, start + RosterFormat.OffExperience);
        WriteInt16(bytes, slot, RosterFormat.OffType, type);
        WriteInt16(bytes, slot, RosterFormat.OffLevel, level);
        WriteInt16(bytes, slot, RosterFormat.OffMen, men);
        WriteInt16(bytes, slot, RosterFormat.OffX, x);
        WriteInt16(bytes, slot, RosterFormat.OffY, y);
        for (int i = 0; i < equipment.Length; i++)
            WriteInt16(bytes, slot, RosterFormat.EquipmentOffsets[i], equipment[i]);
        bytes[start + RosterFormat.OffPackedLevel] = (byte)level;
        bytes[start + RosterFormat.OffPackedType] = (byte)type;

        var costs = UnitBook.ComputeCosts(type, equipment, 7);
        WriteInt16(bytes, slot, RosterFormat.OffMakeCost, costs.Make);
        WriteInt16(bytes, slot, RosterFormat.OffTrainCost, costs.Train);
        WriteInt16(bytes, slot, RosterFormat.OffMaintTenths, costs.MaintTenths);
        WriteInt16(bytes, slot, RosterFormat.OffSize, UnitBook.SizePoints(equipment[6]));
    }

    private static int ReadInt16(byte[] bytes, int slot, int offset)
    {
        int at = RosterFormat.RecordOffset(slot) + offset;
        return (short)(bytes[at] | (bytes[at + 1] << 8));
    }

    private static void WriteInt16(byte[] bytes, int slot, int offset, int value)
    {
        int at = RosterFormat.RecordOffset(slot) + offset;
        bytes[at] = (byte)(value & 0xFF);
        bytes[at + 1] = (byte)((value >> 8) & 0xFF);
    }

    // ============================================================== kingdom fixture
    private static void CheckKingdomFixture()
    {
        Section("Kingdom save (synthetic fixture)");

        var raw = BuildKingdom();
        var save = KingdomFile.Parse(raw, "fixture.HSA");

        Equal("twenty city blocks", save.Cities.Count, KingdomFile.CityCount);
        Approx("wealth", save.Wealth, 702.95, 0.001);
        Equal("score", save.Score, 5);
        Approx("income", save.Income, 523.2, 0.001);
        Approx("maintenance", save.Maintenance, 247.25, 0.001);
        Equal("date", save.Date, "May 871 QJ");
        Equal("cursor X", save.CursorX, 6);
        Equal("cursor Y", save.CursorY, 7);

        // A parse/serialise cycle with no edits must be byte-for-byte identical.
        True("untouched round-trip is byte-identical", save.ToBytes().AsSpan().SequenceEqual(raw));

        var aladda = save.Cities[0];
        Equal("first city name", aladda.Name, "Aladda");
        Equal("population", aladda.Population, 1501);
        Equal("morale", aladda.Morale, 102);
        Equal("loyalty", aladda.Loyalty, 85);
        Equal("health", aladda.Health, 82);
        Equal("tax rate", aladda.TaxRate, 30);
        Equal("recruits", aladda.Recruits, 91);
        Equal("position code", aladda.PositionCode, 607);
        Equal("position X", aladda.X, 6);
        Equal("position Y", aladda.Y, 7);
        True("the first city looks player-owned", aladda.LooksPlayerOwned);
        True("the second does not", !save.Cities[1].LooksPlayerOwned);
        Equal("one player city", save.PlayerCities.Count(), 1);
        Equal("agriculture development", aladda.Develop(DevelopmentCategory.Agriculture), 9);
        Equal("agriculture ceiling", aladda.Resource(DevelopmentCategory.Agriculture), 13);
        Equal("agriculture cost", aladda.Cost(DevelopmentCategory.Agriculture), 60);
        Equal("agriculture production", aladda.Production(DevelopmentCategory.Agriculture), 253);

        aladda.Population = 5000;
        aladda.Morale = 120;
        aladda.TaxRate = 200;
        Equal("tax rate is clamped to 80", aladda.TaxRate, GameFacts.MaxTaxRate);
        aladda.TaxRate = 40;
        aladda.SetDevelop(DevelopmentCategory.Mining, 50);
        aladda.DevelopToResourceCeiling();
        Equal("develop-to-ceiling raises agriculture", aladda.Develop(DevelopmentCategory.Agriculture), 13);
        True("develop-to-ceiling never lowers a category",
            aladda.Develop(DevelopmentCategory.Mining) >= 50);
        aladda.RestoreMood();
        Equal("restore sets morale", aladda.Morale, CityRecord.FullMood);
        Equal("restore sets loyalty", aladda.Loyalty, CityRecord.FullMood);
        Equal("restore sets health", aladda.Health, CityRecord.FullMood);

        save.Wealth = 5_000_000;
        save.Score = 9_999;
        Equal("score is clamped to the maximum", save.Score, GameFacts.MaxScore);
        save.Wealth = 20_000_000;
        Equal("wealth is clamped", save.Wealth, GameFacts.MaxWealth);

        // Edits must touch only their own lines.
        var edited = Encoding.Latin1.GetString(save.ToBytes()).Split("\r\n");
        var startLines = Encoding.Latin1.GetString(raw).Split("\r\n");
        Equal("line count is unchanged", edited.Length, startLines.Length);
        int changed = edited.Where((line, i) => line != startLines[i]).Count();
        True($"only the expected lines changed (saw {changed})", changed is > 0 and <= 12);
        True("the second city block is untouched",
            edited.Skip(3 + CityRecord.BlockLines).Take(CityRecord.BlockLines)
                  .SequenceEqual(startLines.Skip(3 + CityRecord.BlockLines).Take(CityRecord.BlockLines)));
        True("the trailer is untouched", edited[^1] == startLines[^1] && edited[^2] == startLines[^2]);

        Throws<InvalidDataException>("a too-short save is rejected",
            () => KingdomFile.Parse(Encoding.Latin1.GetBytes("1,2\r\n3,4\r\n")));
        Throws<InvalidDataException>("a save with a malformed city block is rejected", () =>
        {
            var lines = Encoding.Latin1.GetString(raw).Split("\r\n").ToArray();
            lines[3 + 1] = "0";                       // mood line loses its fields
            KingdomFile.Parse(Encoding.Latin1.GetBytes(string.Join("\r\n", lines)));
        });
    }

    private static byte[] BuildKingdom()
    {
        var lines = new List<string>
        {
            "0,4,2,5,6,7",
            "0,0,0,0,2,2",
            "702.95,5,523.2,247.25",
        };

        for (int i = 0; i < KingdomFile.CityCount; i++)
        {
            var info = CityBook.Cities[i];
            bool owned = i == 0;
            lines.Add($"\"{info.Name}\",{(owned ? 1501 : info.Population)},{(owned ? "523.2" : "0")}");
            lines.Add(owned ? "0,102,85,82" : $"0,{info.Morale},{info.Loyalty},{info.Health}");
            lines.Add(owned ? "30,150,27,3" : $"25,{info.CityGold},0,1");
            lines.Add($"{(owned ? 91 : 0)},0,{info.PositionCode}");
            lines.Add(owned ? "1,27,33,-3" : "0,0,0,0");
            lines.Add(owned ? "0,0,47,3" : "0,0,0,0");
            lines.Add(owned ? "400,0" : "0,0");
            lines.Add(owned ? "9,60,13,253,80,0,253,76" : "4,100,6,0,350,0,0,0");
            lines.Add(owned ? "4,100,5,225,350,0,225,68" : "0,250,0,0,0,0,0,0");
            lines.Add(owned ? "3,250,4,253,350,0,253,76" : "0,500,0,0,0,0,0,0");
            lines.Add(owned ? "4,250,5,450,300,0,450,135" : "1,250,1,0,500,0,0,0");
            lines.Add(owned ? "4,200,5,563,400,0,563,169" : "1,300,1,0,350,0,0,0");
            lines.Add(owned ? "2,250,3,0,800,0,0,0" : "2,360,3,0,1000,0,0,0");
            lines.Add(owned ? "1,500,2,0,1000,0,0,0" : "1,750,1,0,1600,0,0,0");
        }

        lines.Add("0,0,0");
        lines.Add("0,0");
        lines.Add(string.Empty);                       // the game's final CRLF
        return Encoding.Latin1.GetBytes(string.Join("\r\n", lines) + (char)KingdomFile.EofMarker);
    }

    // ============================================================ segment searching
    private static void CheckSegmentSearch()
    {
        Section("Data-segment searching");

        var segment = new byte[0x400];
        Mbf.Write(segment.AsSpan(), 702.95, 0x100);
        Mbf.Write(segment.AsSpan(), 703.40, 0x200);
        Mbf.Write(segment.AsSpan(), 5000.0, 0x300);
        segment[0x080] = 0xE7; segment[0x081] = 0x05;             // 1511 as Int16

        var gold = DgroupLocator.FindMbfNear(segment, 0x10000, 703, 1.0);
        Equal("two gold candidates within +/-1", gold.Count, 2);
        True("candidates carry their DS offsets", gold.Any(c => c.DsOffset == 0x100));
        True("candidate addresses are base + offset",
            gold.All(c => c.Address == (nuint)0x10000 + (nuint)c.DsOffset));
        Equal("a tight tolerance narrows to one",
            DgroupLocator.FindMbfNear(segment, 0x10000, 702.95, 0.01).Count, 1);
        Equal("a value that is not there yields nothing",
            DgroupLocator.FindMbfNear(segment, 0x10000, 12345, 0.5).Count, 0);

        var counters = DgroupLocator.FindInt16(segment, 0x10000, 1511);
        True("the Int16 counter is found", counters.Any(c => c.DsOffset == 0x080));
        Equal("an out-of-range Int16 target yields nothing",
            DgroupLocator.FindInt16(segment, 0x10000, 70000).Count, 0);

        Equal("primary anchor offset", GameSignatures.WorldMapPrimary.DsOffset, 0x90F8);
        Equal("three validators", GameSignatures.WorldMapValidators.Count, 3);
        True("validators sit above the primary anchor",
            GameSignatures.WorldMapValidators.All(a => a.DsOffset > GameSignatures.WorldMapPrimary.DsOffset));
        True("every anchor fits inside one segment",
            GameSignatures.WorldMapValidators
                .Append(GameSignatures.WorldMapPrimary)
                .All(a => a.DsOffset + a.Bytes.Length <= GameSignatures.SegmentSize));
        True("no two anchors overlap",
            GameSignatures.WorldMapValidators
                .Append(GameSignatures.WorldMapPrimary)
                .OrderBy(a => a.DsOffset)
                .Zip(GameSignatures.WorldMapValidators
                        .Append(GameSignatures.WorldMapPrimary)
                        .OrderBy(a => a.DsOffset)
                        .Skip(1))
                .All(pair => pair.First.DsOffset + pair.First.Bytes.Length <= pair.Second.DsOffset));
        // Each anchor's exact byte length is pinned, so "tidying" the banner's runs of three and four
        // spaces (which is what makes it distinctive) fails here instead of silently breaking the scan.
        Equal("validator lengths",
            string.Join(",", GameSignatures.WorldMapValidators.Select(a => a.Bytes.Length)), "14,8,7");
    }

    // ============================================================ view-model rules
    /// <summary>A do-nothing edit host so the roster view-models can be exercised headlessly.</summary>
    private sealed class SilentHost : IEditHost
    {
        public int DirtyCount { get; private set; }
        public int RosterRecomputeCount { get; private set; }
        public void MarkDirty(string what) => DirtyCount++;
        public void NotifyRosterRecomputed() => RosterRecomputeCount++;
    }

    private static void CheckViewModelRules()
    {
        Section("View-model rules");

        var roster = RosterFile.FromBytes(BuildRoster());
        var host = new SilentHost();
        var player = new RosterSlotViewModel(roster, roster.Records[0], host);
        var cavalry = new RosterSlotViewModel(roster, roster.Records[RosterFormat.FirstUnitSlot], host);
        var empty = new RosterSlotViewModel(roster, roster.Records[40], host);

        // "Equip best" must not put a foot unit on a horse: that pairing appears nowhere in the corpus
        // the cost model was validated against, and movement is not among the fields it can fix up.
        var foot = roster.Records[RosterFormat.FirstUnitSlot + 1];
        foot.TypeCode = 1;                                    // Infantry
        foot.Level = 5;
        for (int i = 0; i < UnitBook.SlotCount; i++) foot.SetEquipment(UnitBook.Slots[i], 0);
        var footView = new RosterSlotViewModel(roster, foot, host);
        footView.EquipBest();
        Equal("equip-best leaves a foot unit unmounted", foot.GetEquipment(EquipmentSlot.Horse), 0);
        Equal("equip-best leaves an unmounted unit without barding",
            foot.GetEquipment(EquipmentSlot.Barding), 0);
        Equal("equip-best still upgrades a foot unit's armour", foot.GetEquipment(EquipmentSlot.Armor), 5);
        Equal("an unmounted unit keeps foot stacking size", foot.SizePoints, 2);

        cavalry.Level = 5;
        cavalry.EquipBest();
        True("equip-best keeps a mounted unit mounted",
            roster.Records[RosterFormat.FirstUnitSlot].GetEquipment(EquipmentSlot.Horse) > 0);
        True("equip-best gives a mounted unit barding",
            roster.Records[RosterFormat.FirstUnitSlot].GetEquipment(EquipmentSlot.Barding) > 0);

        // An empty slot must stay empty: writing the type is what would mark it occupied.
        int emptyTypeBefore = roster.Records[40].TypeCode;
        empty.Type = UnitBook.Type(3);
        Equal("the type combo cannot occupy an empty slot", roster.Records[40].TypeCode, emptyTypeBefore);
        True("an empty slot is still empty afterwards", !roster.Records[40].IsOccupied);

        // Changing the player's class must recompute every unit, not just slot 0.
        int before = host.RosterRecomputeCount;
        player.Type = UnitBook.Type(6);                        // Knight -> Warrior
        Equal("changing the player's class recomputes the whole roster",
            host.RosterRecomputeCount, before + 1);
        Equal("the Warrior discount now applies to the infantry unit", foot.MakeCost,
            UnitBook.ComputeCosts(1, foot.Equipment(), 6).Make);

        // 16-bit candidates display signed, because the game's own delta counters go negative.
        Equal("an Int16 candidate shows -3, not 65533",
            ScanResultViewModel.Decode(0xFFFD, PinKind.Raw, ScanWidth.Int16), -3d);
        Equal("an Int32 candidate is not sign-extended from 16 bits",
            ScanResultViewModel.Decode(0xFFFD, PinKind.Raw, ScanWidth.Int32), 65533d);
        Approx("an MBF candidate decodes as a float",
            ScanResultViewModel.Decode(BitConverter.ToUInt32(Mbf.GetBytes(702.95)), PinKind.MbfSingle,
                                       ScanWidth.Int32), 702.95, 0.01);
        Equal("a candidate carries the width it was found at",
            new ScanResultViewModel(0x1000, 5, ScanWidth.Int16).Width, ScanWidth.Int16);
        Equal("an MBF candidate is always four bytes wide",
            new ScanResultViewModel(0x1000, 5, ScanWidth.Byte, PinKind.MbfSingle).Width, ScanWidth.Int32);
    }

    // ================================================================== real saves
    private static void CheckRealSaves(string directory)
    {
        Section($"Shipped saves in '{directory}'");

        var sets = SaveSet.Discover(directory);
        if (sets.Count == 0)
        {
            Notes.Add($"no ARAGON.HS? saves under '{directory}' — real-save checks skipped " +
                      "(the game files are copyrighted and are not part of this repository)");
            return;
        }

        int rosterRecords = 0, costMatches = 0, rostersRead = 0, rostersMissing = 0;
        foreach (var set in sets)
        {
            try
            {
                var kingdom = KingdomFile.Load(set.KingdomPath);
                True($"{set.Letter}: kingdom parses with 20 cities", kingdom.Cities.Count == 20);
                True($"{set.Letter}: month is 0..11", kingdom.Month is >= 0 and <= 11);
                True($"{set.Letter}: score is 0..{GameFacts.MaxScore}",
                    kingdom.Score is >= 0 and <= GameFacts.MaxScore);
                True($"{set.Letter}: wealth is not negative", kingdom.Wealth >= 0);
                True($"{set.Letter}: round-trips byte-for-byte",
                    kingdom.ToBytes().AsSpan().SequenceEqual(File.ReadAllBytes(set.KingdomPath)));
                True($"{set.Letter}: first city is Aladda at (6,7)",
                    kingdom.Cities[0].Name == "Aladda" && kingdom.Cities[0].PositionCode == 607);
                True($"{set.Letter}: every city name is in the reference book",
                    kingdom.Cities.All(c => CityBook.ByName(c.Name) != null));
                True($"{set.Letter}: city positions match the reference book",
                    kingdom.Cities.All(c => CityBook.ByName(c.Name)!.PositionCode == c.PositionCode));

                if (!File.Exists(set.RosterPath))
                {
                    rostersMissing++;
                    continue;
                }

                rostersRead++;
                var roster = RosterFile.Load(set.RosterPath);
                True($"{set.Letter}: roster round-trips byte-for-byte",
                    roster.ToArray().AsSpan().SequenceEqual(File.ReadAllBytes(set.RosterPath)));
                True($"{set.Letter}: the player is a character class",
                    roster.PlayerClassCode >= UnitBook.FirstCharacterCode);
                True($"{set.Letter}: characters occupy only character slots",
                    roster.Characters.All(r => UnitBook.Type(r.TypeCode)!.IsCharacter));
                True($"{set.Letter}: units occupy only unit slots",
                    roster.Units.All(r => !UnitBook.Type(r.TypeCode)!.IsCharacter));

                foreach (var record in roster.Records.Where(r => r.IsOccupied))
                {
                    rosterRecords++;
                    var costs = UnitBook.ComputeCosts(record.TypeCode, record.Equipment(),
                                                      roster.PlayerClassCode);
                    if (costs.Make == record.MakeCost && costs.Train == record.TrainCost &&
                        costs.MaintTenths == record.MaintTenths &&
                        UnitBook.SizePoints(record.GetEquipment(EquipmentSlot.Horse)) == record.SizePoints)
                        costMatches++;
                }
            }
            catch (Exception ex)
            {
                Fail($"{set.Letter}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Notes.Add($"parsed {sets.Count} shipped saves ({rostersRead} with a roster, " +
                  $"{rostersMissing} without), {rosterRecords} occupied roster records");

        // Without this the cost-model assertion below would be 0 == 0 and pass vacuously whenever the
        // rosters are absent — exactly the claim the docs lean on hardest.
        True("every discovered save has its roster file", rostersMissing == 0);
        True("at least one roster record was examined", rosterRecords > 0);
        Equal("every occupied record reproduces its make/train/upkeep/size", costMatches, rosterRecords);

        // The corpus the cost model was derived from. Asserted so a regression that stopped recognising
        // occupied slots would fail here instead of quietly shrinking the note above.
        if (sets.Count == 15)
        {
            Equal("the development corpus is still 623 records", rosterRecords, 623);
            Equal("16 distinct (player class, unit type) pairs are covered",
                CountClassTypePairs(sets), 16);
        }
        else
        {
            Notes.Add($"{sets.Count} saves present, not the 15-save development corpus — the 623-record " +
                      "and 16-pair figures quoted in README/AGENTS were not re-checked");
        }
    }

    /// <summary>Counts the distinct (player class, unit type) combinations the corpus exercises.</summary>
    private static int CountClassTypePairs(IReadOnlyList<SaveSet> sets)
    {
        var pairs = new HashSet<(int Player, int Type)>();
        foreach (var set in sets)
        {
            if (!File.Exists(set.RosterPath)) continue;
            var roster = RosterFile.Load(set.RosterPath);
            foreach (var record in roster.Records.Where(r => r.IsOccupied))
                pairs.Add((roster.PlayerClassCode, record.TypeCode));
        }
        return pairs.Count;
    }

    // ===================================================================== plumbing
    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    private static void Record(bool ok, string what, string? detail = null)
    {
        _checks++;
        if (ok) return;
        _failures++;
        Console.WriteLine($"  FAIL  {what}" + (detail == null ? "" : $"  ({detail})"));
    }

    private static void True(string what, bool condition) => Record(condition, what);

    private static void Fail(string what) => Record(false, what);

    private static void Equal<T>(string what, T actual, T expected) =>
        Record(EqualityComparer<T>.Default.Equals(actual, expected), what,
               $"expected {Describe(expected)}, got {Describe(actual)}");

    private static void Approx(string what, double actual, double expected, double tolerance) =>
        Record(Math.Abs(actual - expected) <= tolerance, what,
               $"expected {expected} +/-{tolerance}, got {actual}");

    /// <summary>
    /// Asserts that <paramref name="action"/> is rejected by a deliberate validation failure.
    ///
    /// The exception type matters: accepting any exception would let these checks pass after the guard
    /// they name was deleted, because the malformed input would then throw
    /// <see cref="IndexOutOfRangeException"/> or similar from deeper in the parser instead — the very
    /// thing the guard exists to prevent.
    /// </summary>
    private static void Throws<TExpected>(string what, Action action) where TExpected : Exception
    {
        _checks++;
        try
        {
            action();
            _failures++;
            Console.WriteLine($"  FAIL  {what}  (no exception was thrown)");
        }
        catch (TExpected)
        {
            // expected: a validation failure, not an incidental crash
        }
        catch (Exception ex)
        {
            _failures++;
            Console.WriteLine($"  FAIL  {what}  (expected {typeof(TExpected).Name}, " +
                              $"got {ex.GetType().Name}: {ex.Message})");
        }
    }

    private static string Describe<T>(T value) =>
        value is double d ? d.ToString("0.####", CultureInfo.InvariantCulture) : value?.ToString() ?? "null";
}
