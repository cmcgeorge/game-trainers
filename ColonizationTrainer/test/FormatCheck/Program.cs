// Headless verification for the Colonization trainer. Exercises the reverse-engineered save format
// (offset arithmetic, the header/nation/colony record views, gold/tax/Founding-Father edits, and a
// byte-for-byte round-trip), the game-knowledge reference books, and the pure value-scanner helpers.
// Exits 0 on success, 1 on any failure so it can gate the build (Run.ps1 -Test). No live process or
// emulator is touched; the copyrighted .games\COLONY00.SAV is checked only if it happens to be present.

using System.IO;
using System.Text;
using ColonizationTrainer.Game;
using ColonizationTrainer.ViewModels;
using GameTrainers.Common.Memory;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual, expected);
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {Fmt(actual)}, expected {Fmt(expected)}");
    if (!ok) failures++;
}

void CheckThrows<TException>(string name, Action act) where TException : Exception
{
    bool threw = false;
    try { act(); }
    catch (TException) { threw = true; }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {name}: threw {ex.GetType().Name}, expected {typeof(TException).Name}"); failures++; return; }
    Console.WriteLine($"  [{(threw ? "PASS" : "FAIL")}] {name}: {(threw ? "threw" : "did not throw")} {typeof(TException).Name}");
    if (!threw) failures++;
}

static string Fmt(object? v) => v switch
{
    null => "null",
    byte[] b => "[" + string.Join(",", b) + "]",
    _ => v.ToString() ?? "null",
};

// Builds a valid, minimal COLONIZE save with the given record counts, so the format code runs against
// a synthetic fixture (no copyrighted file needed). Fields mirror a plausible mid-game English save.
static byte[] BuildSyntheticSave(int colonies, int units)
{
    int size = SaveFormat.NationSectionEnd(colonies, units);
    var buf = new byte[size];
    Encoding.ASCII.GetBytes(SaveFormat.Signature).CopyTo(buf, 0);   // "COLONIZE"
    Bytes.WriteU16(buf, SaveFormat.Off_MapSizeX, 58);
    Bytes.WriteU16(buf, SaveFormat.Off_MapSizeY, 72);
    Bytes.WriteU16(buf, SaveFormat.Off_Year, 1600);
    Bytes.WriteU16(buf, SaveFormat.Off_Season, 0);
    Bytes.WriteU16(buf, SaveFormat.Off_Turn, 42);
    Bytes.WriteU8(buf, SaveFormat.Off_Difficulty, 3);
    Bytes.WriteU16(buf, SaveFormat.Off_HumanPlayer, 0);
    Bytes.WriteU16(buf, SaveFormat.Off_TribeCount, 40);
    Bytes.WriteU16(buf, SaveFormat.Off_UnitCount, units);
    Bytes.WriteU16(buf, SaveFormat.Off_ColonyCount, colonies);
    Bytes.WriteU8(buf, SaveFormat.Off_ManualSaveFlag, 1);

    string[] leaders = { "Christopher", "Jacques Cartier", "Columbus", "de Ruyter" };
    string[] countries = { "New England", "New France", "New Spain", "New Netherlands" };
    for (int i = 0; i < SaveFormat.NationCount; i++)
    {
        int b = SaveFormat.PlayerBlockStart + i * SaveFormat.PlayerRecordSize;
        ColonyText.WriteName(buf, b + SaveFormat.Player_Name, SaveFormat.PlayerNameMax, leaders[i]);
        ColonyText.WriteName(buf, b + SaveFormat.Player_Country, SaveFormat.PlayerNameMax, countries[i]);
    }

    for (int c = 0; c < colonies; c++)
    {
        int cb = SaveFormat.ColonyBase(c);
        Bytes.WriteU8(buf, cb + SaveFormat.Col_X, 10 + c);
        Bytes.WriteU8(buf, cb + SaveFormat.Col_Y, 20 + c);
        ColonyText.WriteName(buf, cb + SaveFormat.Col_Name, SaveFormat.Col_NameMax, c == 0 ? "Jamestown" : $"Colony{c}");
        Bytes.WriteU8(buf, cb + SaveFormat.Col_Nation, 0);
        Bytes.WriteU8(buf, cb + SaveFormat.Col_Population, 4);
        Bytes.WriteU16(buf, cb + SaveFormat.Col_Hammers, 25);
        Bytes.WriteS16(buf, cb + SaveFormat.Col_Stock + 0 * 2, 100);   // food
        Bytes.WriteS16(buf, cb + SaveFormat.Col_Stock + 15 * 2, 7);    // muskets
    }

    // Give each nation a distinct gold/tax so the offset formula is exercised per nation.
    for (int n = 0; n < SaveFormat.NationCount; n++)
    {
        int nb = SaveFormat.NationBase(colonies, units, n);
        Bytes.WriteU32(buf, nb + SaveFormat.Nat_Gold, 1000 + n);
        Bytes.WriteU8(buf, nb + SaveFormat.Nat_TaxRate, 10 + n);
    }
    return buf;
}

// --- reference books ----------------------------------------------------------------
Console.WriteLine("Reference books (recovered from the game's own data):");
Check("16 goods", CargoBook.Goods.Count, 16);
Check("good 0 is Food", CargoBook.Goods[0].Name, "Food");
Check("good 15 is Muskets", CargoBook.Goods[15].Name, "Muskets");
Check("good names count matches", CargoBook.Names.Count, 16);
Check("goods are in index order", CargoBook.Goods.Select((g, i) => g.Id == i).All(b => b), true);
Check("24 unit types", UnitBook.Units.Count, 24);
Check("unit 0 is Colonist", UnitBook.Units[0].Name, "Colonist");
Check("unit 18 is Man-O-War", UnitBook.Units[18].Name, "Man-O-War");
Check("28 professions", ProfessionBook.Professions.Count, 28);
Check("profession 17 is Elder Statesman", ProfessionBook.Professions[17].Name, "Elder Statesman");
Check("42 buildings", BuildingBook.Buildings.Count, 42);
Check("21 terrain rows", TerrainBook.Terrains.Count, 21);
Check("25 founding fathers", FoundingFatherBook.Fathers.Count, 25);
Check("father 0 is Adam Smith", FoundingFatherBook.Fathers[0].Name, "Adam Smith");
Check("father 11 is George Washington", FoundingFatherBook.Fathers[11].Name, "George Washington");
Check("father bit 18 is the dead slot", FoundingFatherBook.Fathers[18].Category, "—");
Check("24 grantable fathers", FoundingFatherBook.GrantableCount, 24);
Check("fathers are in bit order", FoundingFatherBook.Fathers.Select((f, i) => f.Bit == i).All(b => b), true);
Check("4 nations", NationBook.Nations.Count, 4);
Check("nation 0 is England", NationBook.NameOf(0), "England");
Check("nation 3 is Netherlands", NationBook.NameOf(3), "Netherlands");
Check("5 difficulties", NationBook.Difficulties.Count, 5);
Check("difficulty 4 is Viceroy", NationBook.DifficultyName(4), "Viceroy");
Console.WriteLine();

// --- offset arithmetic --------------------------------------------------------------
Console.WriteLine("Save-format offset arithmetic:");
Check("colonies start at 0x186", SaveFormat.ColonyStart, 0x186);
Check("nation record is 316 bytes", SaveFormat.NationRecordSize, 0x13C);
Check("colony record is 202 bytes", SaveFormat.ColonyRecordSize, 0xCA);
Check("gold is at nation +0x2A", SaveFormat.Nat_Gold, 0x2A);
Check("England base with 0 colonies, 77 units", SaveFormat.NationBase(0, 77, 0), 0x186 + 0x1C * 77);
Check("nation base steps by 316", SaveFormat.NationBase(1, 2, 1) - SaveFormat.NationBase(1, 2, 0), 0x13C);
Check("colony base steps by 202", SaveFormat.ColonyBase(1) - SaveFormat.ColonyBase(0), 0xCA);
Console.WriteLine();

// --- byte + name codecs -------------------------------------------------------------
Console.WriteLine("Byte / name codecs:");
var probe = new byte[8];
Bytes.WriteU32(probe, 0, 0x11223344);
Check("u32 little-endian write/read", Bytes.U32(probe, 0), 0x11223344L);
Check("low byte first", probe[0], (byte)0x44);
Bytes.WriteS16(probe, 4, -2);
Check("s16 round-trips negatives", (int)Bytes.S16(probe, 4), -2);
CheckThrows<ArgumentOutOfRangeException>("u32 read past the buffer throws", () => Bytes.U32(probe, 6));
var namebuf = new byte[24];
ColonyText.WriteName(namebuf, 0, 24, "Jamestown");
Check("name round-trips", ColonyText.ReadName(namebuf, 0, 24), "Jamestown");
ColonyText.WriteName(namebuf, 0, 24, new string('X', 40));
Check("an over-long name is truncated with room for the NUL", ColonyText.ReadName(namebuf, 0, 24).Length, 23);
Console.WriteLine();

// --- synthetic save: parse, edit, round-trip ----------------------------------------
Console.WriteLine("Synthetic save (parse / edit / round-trip):");
var raw = BuildSyntheticSave(colonies: 2, units: 3);
var save = SaveGame.Parse(raw, "synthetic.SAV");
Check("signature accepted", save.MapWidth, 58);
Check("map height", save.MapHeight, 72);
Check("year", save.Year, 1600);
Check("turn", save.Turn, 42);
Check("difficulty name", save.DifficultyName, "Governor");
Check("human is England", save.HumanNationName, "England");
Check("colony count", save.ColonyCount, 2);
Check("unit count", save.UnitCount, 3);
Check("manual save flag", save.IsManualSave, true);
Check("leader name parsed", save.LeaderNames[0], "Christopher");
Check("country name parsed", save.CountryNames[0], "New England");
Check("England gold read at computed offset", save.Nations[0].Gold, 1000L);
Check("Spain gold read at computed offset", save.Nations[2].Gold, 1002L);
Check("England tax read", save.Nations[0].TaxRate, 10);
Check("first colony name", save.Colonies[0].Name, "Jamestown");
Check("first colony population", save.Colonies[0].Population, 4);
Check("first colony hammers", save.Colonies[0].Hammers, 25);
Check("first colony food stock", (int)save.Colonies[0].GetStock(0), 100);
Check("first colony muskets stock", (int)save.Colonies[0].GetStock(15), 7);

// An untouched parse must reproduce the file byte-for-byte.
Check("unedited round-trip is byte-identical", save.ToArray().SequenceEqual(raw), true);

// Edit through the typed views, then confirm persistence by re-parsing the emitted bytes.
save.HumanNation.Gold = SaveFormat.MaxGoldTarget;
save.HumanNation.TaxRate = 0;
save.HumanNation.LibertyBells = 500;
save.Colonies[0].Name = "Boston";
save.Colonies[0].FillAllStock(SaveFormat.GoodsFill);
var reparsed = SaveGame.Parse(save.ToArray());
Check("edited gold persists", reparsed.Nations[0].Gold, SaveFormat.MaxGoldTarget);
Check("edited tax persists", reparsed.Nations[0].TaxRate, 0);
Check("edited bells persist", reparsed.Nations[0].LibertyBells, 500);
Check("edited colony name persists", reparsed.Colonies[0].Name, "Boston");
Check("filled colony stock persists", (int)reparsed.Colonies[0].GetStock(5), (int)SaveFormat.GoodsFill);
Check("other nations untouched by a human edit", reparsed.Nations[1].Gold, 1001L);

// Gold clamps to a safe positive range; tax clamps to 0..99.
save.HumanNation.Gold = -5;
Check("negative gold clamps to 0", save.HumanNation.Gold, 0L);
save.HumanNation.Gold = long.MaxValue;
Check("huge gold clamps to the cap", save.HumanNation.Gold, SaveFormat.GoldCap);
save.HumanNation.TaxRate = 250;
Check("tax clamps to 99", save.HumanNation.TaxRate, 99);
Console.WriteLine();

// --- decoupled offset check (catches a shifted constant the circular round-trip can't) ----
Console.WriteLine("Decoupled offset check (gold read from a hand-computed absolute byte):");
// The synthetic round-trip above writes AND reads gold via the same SaveFormat.* constants, so a
// shifted constant would still round-trip. Here we write a distinctive gold value at the England gold
// byte computed from LITERALS (0x186 + 28*units + 0x2A = 460, independent of SaveFormat), with 0xFF
// off-by-one sentinels, so a ±1 or section-size shift in the parser's offset math is caught in CI.
var goldProbe = BuildSyntheticSave(colonies: 0, units: 1);
const int englandGoldAbs = 0x186 + 0x1C * 1 + 0x2A;   // = 460; hand-derived, not from SaveFormat
goldProbe[englandGoldAbs + 0] = 0x78;
goldProbe[englandGoldAbs + 1] = 0x56;
goldProbe[englandGoldAbs + 2] = 0x34;
goldProbe[englandGoldAbs + 3] = 0x12;
goldProbe[englandGoldAbs - 1] = 0xFF;   // a -1 offset shift would read this byte
goldProbe[englandGoldAbs + 4] = 0xFF;   // a +1 offset shift would read this byte
Check("gold reads from hand-computed absolute offset 460", SaveGame.Parse(goldProbe).Nations[0].Gold, 0x12345678L);
Console.WriteLine();

// --- founding fathers ---------------------------------------------------------------
Console.WriteLine("Founding-Father bitfield:");
var nat = SaveGame.Parse(BuildSyntheticSave(0, 0)).Nations[0];
nat.ClearAllFathers();
Check("cleared: none held", nat.HasFather(0), false);
Check("cleared: count is 0", nat.FoundingFatherCount, 0);
nat.SetFather(11, true);   // George Washington
Check("Washington granted", nat.HasFather(11), true);
Check("count tracks one father", nat.FoundingFatherCount, 1);
nat.GrantAllFathers();
Check("grant-all sets Adam Smith", nat.HasFather(0), true);
Check("grant-all skips the dead slot 18", nat.HasFather(18), false);
Check("grant-all count is 24 (not the dead slot)", nat.FoundingFatherCount, 24);
Console.WriteLine();

// --- invalid inputs -----------------------------------------------------------------
Console.WriteLine("Validation:");
CheckThrows<InvalidDataException>("a too-small buffer is rejected", () => SaveGame.Parse(new byte[100]));
var badSig = BuildSyntheticSave(0, 0); badSig[0] = (byte)'X';
CheckThrows<InvalidDataException>("a wrong signature is rejected", () => SaveGame.Parse(badSig));
var badCounts = BuildSyntheticSave(0, 0);
Bytes.WriteU16(badCounts, SaveFormat.Off_ColonyCount, 9999);   // points the nation section past EOF
CheckThrows<InvalidDataException>("inconsistent counts are rejected", () => SaveGame.Parse(badCounts));
CheckThrows<ArgumentNullException>("null buffer is rejected", () => SaveGame.Parse(null!));
Console.WriteLine();

// --- scan-value helpers -------------------------------------------------------------
Console.WriteLine("Value-scanner helpers:");
Check("decimal parse", TryParse("500"), 500L);
Check("hex 0x parse", TryParse("0x1F4"), 500L);
Check("hex suffix parse", TryParse("1F4h"), 500L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("500 fits an int16", ScanValue.FitsWidth(500, ScanWidth.Int16), true);
Check("70000 does not fit an int16", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("-1 canonicalizes to 0xFFFFFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int32), 0xFFFFFFFFL);
Console.WriteLine();

Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Int32, 100, "Gold");
Check("target starts at the captured value", pin.Target, 100L);
pin.Target = 999_999;
Check("editing target pokes RAM", host.LastWrite, 999_999L);
Check("the poke uses the pin's width", host.LastWidth, ScanWidth.Int32);
var bytePin = new FrozenValueViewModel(host, (nuint)0x2000, ScanWidth.Byte, 10, "Tax");
bytePin.Target = 300;   // does not fit a byte
Check("an out-of-width target is rejected", bytePin.Target, 10L);
bytePin.Frozen = true;
host.LastWrite = null;
bytePin.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 10L);
var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x3000, ScanWidth.Int32, 10, "Gold");
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);
Console.WriteLine();

// --- optional: the real shipped save (only if present; never committed) -------------
Console.WriteLine("Real save (.games\\COLONY00.SAV, if present):");
var real = FindShippedSave();
if (real == null)
{
    Console.WriteLine("  [SKIP] no .games\\COLONY00.SAV found — synthetic checks cover the format.");
}
else
{
    var rs = SaveGame.Load(real);
    Check("real: map is 58x72", (rs.MapWidth, rs.MapHeight), (58, 72));
    Check("real: year is 1492", rs.Year, 1492);
    Check("real: turn is 0", rs.Turn, 0);
    Check("real: difficulty is Viceroy", rs.DifficultyName, "Viceroy");
    Check("real: human is England", rs.HumanNationName, "England");
    Check("real: 0 colonies", rs.ColonyCount, 0);
    Check("real: 77 units", rs.UnitCount, 77);
    Check("real: 65 native dwellings", rs.TribeCount, 65);
    Check("real: leader is Christopher", rs.LeaderNames[0], "Christopher");
    Check("real: country is New England", rs.CountryNames[0], "New England");
    Check("real: England starts with 0 gold", rs.Nations[0].Gold, 0L);
    Check("real: England starts at 0% tax", rs.Nations[0].TaxRate, 0);
}
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s)
{
    if (!ScanValue.TryParse(s, out long v))
        throw new InvalidOperationException($"TryParse helper: '{s}' failed to parse (test bug).");
    return v;
}

// Walks up from the test binary looking for a shipped .games/.game folder — used only for the
// optional real-file assertions; the copyrighted save itself is never committed.
static string? FindShippedSave()
{
    try
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
            foreach (var name in new[] { ".games", ".game" })
            {
                var path = Path.Combine(dir.FullName, name, "COLONY00.SAV");
                if (File.Exists(path)) return path;
            }
    }
    catch { /* optional */ }
    return null;
}

// A fake read/write channel that records the last write and can simulate failure, so the frozen-value
// view-model's poke / freeze / width-guard / failure logic runs headlessly.
sealed class CaptureHost : IScanHost
{
    public bool Succeed { get; init; } = true;
    public long? LastWrite { get; set; }
    public ScanWidth LastWidth { get; private set; }
    public int Failures { get; private set; }

    public bool Write(nuint address, long value, ScanWidth width)
    {
        if (!Succeed) return false;
        LastWrite = value;
        LastWidth = width;
        return true;
    }

    public bool Read(nuint address, ScanWidth width, out long value) { value = 0; return false; }

    public void ReportWriteFailure(nuint address) => Failures++;
}
