// Headless verification harness for the War of the Lance data-file parsers. It decodes byte-exact
// slices captured straight from the shipped game files in .game/ (NAT.DAT, MENU.DAT, WL.UNT, and
// the head of WL.DAT) and asserts the container header, the high-bit text codec, the nation/label
// tables, the .UNT unit table, and the strength-block locator signature. Exits 0 on success, 1 on
// any failure, so it can gate the build (Run.ps1 -Test).

using WarOfTheLanceTrainer.Game;
using WarOfTheLanceTrainer.Memory;
using WarOfTheLanceTrainer.ViewModels;

// NAT.DAT (232 bytes): the 27+placeholder nation/place name table.
const string NatDatB64 =
    "/WeYAADhAMLMz8TF/8PBxdLHz9TI/8fPz8TM1c7E/8fVztTIwdL/yNnMz//Lwc/M2c7/y8XSzv/LyNXS/8vP1MjB0//Mxc3J08j/zcHFzNPU0s/N/83J1MjB0//OxdLBy8H/zs/SxM3BwdL/zq6gxdLHz9TI/9DBzMHO1MjV0//R1cHMyc7F09TJ/9PBzsPUyc/O/9PJzNbBzsXT1Mn/08/Mwc7UyNXT/9TB0tPJ0//UyM/SwsHSxMnO/9TI0s/U2cz/1snOx8HB0sT/2sjBy8HS/8PMxdLJ09Sg1M/XxdL/08/UyP+t/w==";

// MENU.DAT (302 bytes): menu strings ending in the five season labels.
const string MenuDatB64 =
    "/WWYAAAnAcPV0tPP0v/HxdT/0sXDz87/zMHT1P/R1cHE/83B0P/Nxc7V/8PPzcLB1P/TwdbF/83FztX/xMXMwdn/2P/Nz9bF/8XYydT/wdTUwcPL/87F2NT/ydTFzf+o1c6pzM/BxP/QwdTSz8z/zs//2cXT/9TB0sfF1P/UwdLHxdSgwczM/87PoMHU1MHDy//F2MnU/87F2NSg1c7J1P/U0sHO08bF0v/F2MnU/8HCz9LU/8HCz9LUoLGg09H/wdXUz6DNz9bF/8TJ09DMwdmg1c7J1NP/zM/BxP/VzszPwcT/xdjJ1P/VzszPwcSg1c7J1P/F2MnU/87F2NSg09HVwdLF/83B0q/B0NL/zcHZr8rVzv/K1cyvwdXH/9PF0K/Pw9T/18nO1MXS/wA=";

// WL.UNT (1607 bytes): campaign unit placement table.
const string WlUntB64 =
    "/WeYAABABikVRQUpFUUFKBdFBSgXRQUnF0UFKRZFBSkWRQUpF0UFJxdGBSkXRgUnF0EFJxdBBSkWQQUpFkEFKBdBBSgXQQUpF0EFKRdBBScXQwUnF0MFKBdDBSgXQwUpFUMFKRdDBSkWQwUpFkMFKRVDBSkXQwX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//UAX//1AF//9QBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAX//2AF//9gBf//YAU=";

// WL.DAT head: 7-byte container header + first 60 payload bytes (29 strength cells + 24-byte
// locator signature + margin).
const string WlDatHeadB64 =
    "/WeYAABTDsjIyMjIyMjIyJaWlpaWlpaWlpbIyMjIyMjIyJaWAwMDAwQEBAVubm5uFBQUFG5ubm4UFBQUgoKCgoKCgg==";

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

byte[] natDat = Convert.FromBase64String(NatDatB64);
byte[] menuDat = Convert.FromBase64String(MenuDatB64);
byte[] wlUnt = Convert.FromBase64String(WlUntB64);
byte[] wlDatHead = Convert.FromBase64String(WlDatHeadB64);

Console.WriteLine("Container header (NAT.DAT):");
var nat = SaveContainer.ParseValidated(natDat);
Check("magic", nat.MagicByte, SaveContainer.Magic);
Check("payload length", nat.PayloadLength, 225);
Check("length invariant holds", nat.IsValid, true);
Check("file length", natDat.Length, 232);
Console.WriteLine();

Console.WriteLine("Nation-name table (NAT.DAT high-bit text):");
var natWords = GameText.DecodeAll(nat.Payload());
Check("word count", natWords.Count, GameFacts.NationNames.Length);
for (int i = 0; i < GameFacts.NationNames.Length; i++)
    Check($"nation[{i}]", natWords[i], GameFacts.NationNames[i]);
Console.WriteLine();

Console.WriteLine("Text codec round-trip:");
foreach (var w in new[] { "NERAKA", "CLERIST TOWER", "N. ERGOTH" })
{
    var enc = GameText.Encode(w);
    var dec = GameText.DecodeWord(enc, 0, out _);
    Check($"round-trip \"{w}\"", dec, w);
}
Console.WriteLine();

Console.WriteLine("Season labels (MENU.DAT tail):");
var menuWords = GameText.DecodeAll(SaveContainer.ParseValidated(menuDat).Payload());
foreach (var season in GameFacts.SeasonNames)
    Check($"contains \"{season}\"", menuWords.Contains(season), true);
Console.WriteLine();

Console.WriteLine("Unit table (WL.UNT):");
var slots = UnitTable.ParseFile(wlUnt);
Check("occupied slot count", slots.Count, 28);
Check("all slots carry live flag 0x05", slots.All(s => s.Flag == UnitSlot.LiveFlag), true);
Check("no occupied slot is empty", slots.Any(s => s.IsEmpty), false);
Console.WriteLine();

Console.WriteLine("Strength block + locator signature (WL.DAT head):");
// Only the head of WL.DAT is embedded, so the slice is treated as a raw container header +
// partial payload rather than a whole (length-validated) file.
Check("magic byte", wlDatHead[0], SaveContainer.Magic);
byte[] payload = wlDatHead.Skip(SaveContainer.HeaderSize).ToArray();
Check("leading base-number run matches appendix",
    payload.Take(StrengthTable.Count).SequenceEqual(ExpectedCampaignStart()), true);
byte[] sigInFile = payload.Skip(StrengthTable.Count).Take(StrengthTable.Signature.Length).ToArray();
Check("signature follows the 29 strength cells",
    sigInFile.SequenceEqual(StrengthTable.Signature), true);
Check("signature-to-block delta", StrengthTable.SignatureToBlockDelta, -StrengthTable.Count);
Console.WriteLine();

Console.WriteLine("Strength entry table:");
Check("entry count", StrengthTable.Entries.Length, StrengthTable.Count);
Check("first entry is a Baaz draconian @200",
    $"{StrengthTable.Entries[0].UnitType}:{StrengthTable.Entries[0].BaseNumber}", "BAAZ DRACONIAN:200");
Check("last entry is Neraka merc cavalry @150",
    $"{StrengthTable.Entries[^1].UnitType}:{StrengthTable.Entries[^1].BaseNumber}", "MERC CAVALRY:150");
Console.WriteLine();

Console.WriteLine("Nation locator signature:");
byte[] natSig = GameLocator.BuildNationSignature();
Check("signature is a prefix of the NAT.DAT payload",
    nat.Payload().Take(natSig.Length).SequenceEqual(natSig), true);
Console.WriteLine();

Console.WriteLine("Text codec rejects non-ASCII:");
CheckThrows<ArgumentException>("Encode throws on a non-ASCII char", () => GameText.Encode("caf\u00E9"));
Console.WriteLine();

Console.WriteLine("Unit table enforces the fixed 400-slot size:");
Check("exactly 1600 bytes parses (400 zero slots)", UnitTable.ParsePayload(new byte[UnitTable.TableBytes]).Count, UnitTable.SlotCount);
CheckThrows<ArgumentException>("1599-byte payload is rejected", () => UnitTable.ParsePayload(new byte[UnitTable.TableBytes - 1]));
CheckThrows<ArgumentException>("1601-byte payload is rejected", () => UnitTable.ParsePayload(new byte[UnitTable.TableBytes + 1]));
Console.WriteLine();

Console.WriteLine("Strength cell clamp / destroyed-unit handling:");
var host = new CaptureHost();
var entry = new StrengthEntry(0, "HIGHLORD", "HIGHLORD", "BAAZ DRACONIAN", 200);
var destroyed = new UnitStrengthViewModel(host, entry, (nuint)0x1000, 0);
Check("a live 0 (destroyed unit) is preserved, not bumped to 1", destroyed.Strength, 0);
destroyed.Freeze = true;
destroyed.ApplyFreeze();
Check("freezing a destroyed unit re-writes 0, not 1", host.LastWrite, (byte)0);

var edited = new UnitStrengthViewModel(host, entry, (nuint)0x2000, 100);
edited.Strength = 300;
Check("an over-range edit clamps to 240", edited.Strength, 240);
Check("the clamped 240 is what reaches RAM", host.LastWrite, (byte)240);
edited.Strength = -5;
Check("an under-range edit clamps to 1", edited.Strength, 1);
Console.WriteLine();

Console.WriteLine("Write-failure path surfaces to the host:");
var failing = new CaptureHost { Succeed = false };
var unit = new UnitStrengthViewModel(failing, entry, (nuint)0x3000, 100);
unit.Strength = 200;
Check("a failed write is reported", failing.Failures, 1);
Check("a failed write does not advance the displayed value", unit.Strength, 100);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

// The 29 campaign-start strength cells: 9×200, 10×150, 8×200, 2×150.
static byte[] ExpectedCampaignStart()
{
    var b = new List<byte>();
    b.AddRange(Enumerable.Repeat((byte)200, 9));
    b.AddRange(Enumerable.Repeat((byte)150, 10));
    b.AddRange(Enumerable.Repeat((byte)200, 8));
    b.AddRange(Enumerable.Repeat((byte)150, 2));
    return b.ToArray();
}

// A fake write channel that records the last byte written and can simulate a failing write, so the
// view-model's clamp / destroyed-unit / write-failure logic can be exercised headlessly.
sealed class CaptureHost : IStrengthHost
{
    public bool Succeed { get; init; } = true;
    public byte? LastWrite { get; private set; }
    public int Failures { get; private set; }

    public bool WriteStrength(nuint address, byte value)
    {
        if (!Succeed) return false;
        LastWrite = value;
        return true;
    }

    public void ReportWriteFailure(string unitLabel) => Failures++;
}
