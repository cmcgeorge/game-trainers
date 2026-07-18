// Headless verification for the Darklands trainer. Exercises the game-knowledge layer (the Confirmed
// attribute / skill / currency / Fame tables), the read-only save reader against a synthetic fixture
// built from the DEFAULT template's Confirmed offsets, plus the pure value-scanner helpers and the
// frozen-value write / clamp logic. Exits 0 on success, 1 on any failure so it can gate the build
// (Run.ps1 -Test). No live process, emulator, or copyrighted save file is touched.

using System.Text;
using DarklandsTrainer.Game;
using DarklandsTrainer.ViewModels;
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

// Places an ASCIIZ string into the buffer at an absolute offset.
static void PutZ(byte[] buf, int offset, string text)
{
    var bytes = Encoding.ASCII.GetBytes(text);
    Array.Copy(bytes, 0, buf, offset, bytes.Length);
    buf[offset + bytes.Length] = 0;
}

// Builds a synthetic save that reproduces, byte-for-byte at the Confirmed offsets, the fields the
// shipped SAVES\DEFAULT template carries for its sample character "Gretchen Wilburg" (see
// .docs/ReverseEngineering.md §3). The real copyrighted file is never read.
static byte[] BuildDefaultFixture()
{
    var buf = new byte[SaveFile.DefaultSize];
    PutZ(buf, SaveFile.LocationOffset, "Rottweil");
    PutZ(buf, SaveFile.LabelOffset, "new default");
    buf[SaveFile.PartyCountOffset] = 4;

    string[] portraits = { "F60", "F01", "A00", "C00" };
    for (int i = 0; i < portraits.Length; i++)
        PutZ(buf, SaveFile.PortraitBlockOffset + i * SaveFile.PortraitStride, portraits[i]);

    PutZ(buf, SaveFile.NameOffset, "Gretchen Wilburg");
    PutZ(buf, SaveFile.NicknameOffset, "Gretch");

    byte[] attrs = { 0x1E, 0x1E, 0x1D, 0x12, 0x1F, 0x20, 0x63 };  // End 30, Str 30, Agi 29, Per 18, Int 31, Cha 32, +cap
    Array.Copy(attrs, 0, buf, SaveFile.AttributesCurrentOffset, attrs.Length);
    Array.Copy(attrs, 0, buf, SaveFile.AttributesMaxOffset, attrs.Length);
    return buf;
}

Console.WriteLine("Attribute book (Confirmed six primaries + Divine Favor):");
Check("primary count is 6", AttributeBook.PrimaryCount, 6);
Check("primary list length matches", AttributeBook.Primary.Count, AttributeBook.PrimaryCount);
Check("first attribute is Endurance", AttributeBook.Primary[0].Name, "Endurance");
Check("second attribute is Strength", AttributeBook.Primary[1].Name, "Strength");
Check("last primary is Charisma", AttributeBook.Primary[^1].Name, "Charisma");
Check("Divine Favor index follows the six", AttributeBook.DivineFavor.Index, 6);
Check("practical max is 99", AttributeBook.PracticalMax, 99);
Console.WriteLine();

Console.WriteLine("Skill book (Confirmed nineteen skills):");
Check("skill count is 19", SkillBook.SkillCount, 19);
Check("skill list length matches", SkillBook.Skills.Count, SkillBook.SkillCount);
Check("first skill is Edged Wpns", SkillBook.Skills[0].Name, "Edged Wpns");
Check("last skill is Woodwise", SkillBook.Skills[^1].Name, "Woodwise");
Check("skill indices are contiguous",
    SkillBook.Skills.Select((s, i) => s.Index == i).All(b => b), true);
Console.WriteLine();

Console.WriteLine("Currency + Fame (Confirmed ratios and ladder):");
Check("groschen per florin", GameFacts.GroschenPerFlorin, 20);
Check("pfennig per groschen", GameFacts.PfennigPerGroschen, 12);
Check("pfennig per florin", GameFacts.PfennigPerFlorin, 240);
Check("240 pf splits to 1 florin", GameFacts.SplitPfennigs(240), (1, 0, 0));
Check("253 pf splits to 1fl 1gr 1pf", GameFacts.SplitPfennigs(253), (1, 1, 1));
Check("negative purse clamps to zero", GameFacts.SplitPfennigs(-5), (0, 0, 0));
Check("three coin denominations", GameFacts.Currency.Count, 3);
Check("top coin is the Florin", GameFacts.Currency[0].Name, "Florin");
Check("eleven Fame tiers", GameFacts.FameTiers.Count, 11);
Check("lowest Fame tier", GameFacts.FameTiers[0], "Unknown");
Check("highest Fame tier", GameFacts.FameTiers[^1], "Legendary Heroes");
Console.WriteLine();

Console.WriteLine("Save reader (read-only, against a DEFAULT-shaped fixture):");
Check("default size is 26349", SaveFile.DefaultSize, 26349);
var header = SaveFile.ParseHeader(BuildDefaultFixture());
Check("location", header.Location, "Rottweil");
Check("label", header.Label, "new default");
Check("party count", header.PartyCount, 4);
Check("four portrait codes", header.Portraits.Count, 4);
Check("first portrait", header.Portraits[0], "F60");
Check("last portrait", header.Portraits[^1], "C00");
Check("name", header.FirstCharacter.Name, "Gretchen Wilburg");
Check("nickname", header.FirstCharacter.Nickname, "Gretch");
Check("six current attributes", header.FirstCharacter.Attributes.Count, 6);
Check("Endurance current is 30", header.FirstCharacter.Attributes[0], (byte)30);
Check("Charisma current is 32", header.FirstCharacter.Attributes[5], (byte)32);
Check("max block matches current (fresh character)",
    header.FirstCharacter.MaxAttributes.SequenceEqual(header.FirstCharacter.Attributes), true);
CheckThrows<ArgumentException>("ParseHeader rejects a too-short buffer", () => SaveFile.ParseHeader(new byte[16]));
CheckThrows<ArgumentNullException>("ParseHeader rejects null", () => SaveFile.ParseHeader(null!));

// Portraits must honour the declared party count, not scoop up stale bytes in unused slots.
var reduced = BuildDefaultFixture();
reduced[SaveFile.PartyCountOffset] = 1;                        // one member; slots 1..3 are now stale
var reducedHeader = SaveFile.ParseHeader(reduced);
Check("portraits honour a reduced party count", reducedHeader.Portraits.Count, 1);
Check("the surviving portrait is slot 0", reducedHeader.Portraits[0], "F60");
var overfull = BuildDefaultFixture();
overfull[SaveFile.PartyCountOffset] = 5;                       // more than MaxPartySlots
CheckThrows<ArgumentException>("ParseHeader rejects an over-full party count", () => SaveFile.ParseHeader(overfull));
Check("ReadAsciiZ stops at the terminator", SaveFile.ReadAsciiZ(new byte[] { 65, 66, 0, 67 }, 0, 4), "AB");
Console.WriteLine();

Console.WriteLine("Scan-value parsing:");
Check("decimal parse", TryParse("30"), 30L);
Check("hex 0x parse", TryParse("0x1E"), 30L);
Check("hex suffix parse", TryParse("1Eh"), 30L);
Check("blank is rejected", ScanValue.TryParse("", out _), false);
Check("garbage is rejected", ScanValue.TryParse("zz", out _), false);
Check("30 fits a byte", ScanValue.FitsWidth(30, ScanWidth.Byte), true);
Check("300 does not fit a byte", ScanValue.FitsWidth(300, ScanWidth.Byte), false);
Check("30000 fits a word", ScanValue.FitsWidth(30000, ScanWidth.Int16), true);
Check("70000 does not fit a word", ScanValue.FitsWidth(70000, ScanWidth.Int16), false);
Check("-1 fits a byte (signed)", ScanValue.FitsWidth(-1, ScanWidth.Byte), true);
Check("-1 canonicalizes to a byte's 0xFF", ScanValue.Canonicalize(-1, ScanWidth.Byte), 0xFFL);
Check("-1 canonicalizes to a word's 0xFFFF", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Check("a positive value canonicalizes unchanged", ScanValue.Canonicalize(30000, ScanWidth.Int16), 30000L);
Console.WriteLine();

Console.WriteLine("Frozen-value write / freeze / width guard:");
var host = new CaptureHost();
var pin = new FrozenValueViewModel(host, (nuint)0x1000, ScanWidth.Byte, 30, "Endurance");
Check("label is carried", pin.Label, "Endurance");
Check("target starts at the captured value", pin.Target, 30L);
pin.Target = 99;
Check("editing target pokes RAM", host.LastWrite, 99L);
Check("the poke uses the pin's captured width", host.LastWidth, ScanWidth.Byte);
Check("target updates", pin.Target, 99L);
pin.Target = 300;                       // does not fit a byte pin
Check("an out-of-width target is rejected", pin.Target, 99L);
pin.Frozen = true;
host.LastWrite = null;
pin.ApplyFreeze();
Check("freezing re-writes the target", host.LastWrite, 99L);

// A pin keeps its own width even if the host's active scan width later differs: a Word pin must still
// write two bytes (widths are per-pin, not per-searcher).
var fame = new FrozenValueViewModel(host, (nuint)0x3000, ScanWidth.Int16, 100, "Fame");
fame.Target = 30000;
Check("a word pin writes at word width", host.LastWidth, ScanWidth.Int16);
Check("a large Fame target is accepted", fame.Target, 30000L);

var failing = new CaptureHost { Succeed = false };
var pin2 = new FrozenValueViewModel(failing, (nuint)0x2000, ScanWidth.Byte, 10);
pin2.Target = 20;
Check("a failed write is reported", failing.Failures, 1);

// A freeze re-write that fails (target unmapped / process gone) must also be reported, not swallowed.
var failingFreeze = new CaptureHost { Succeed = false };
var pin3 = new FrozenValueViewModel(failingFreeze, (nuint)0x4000, ScanWidth.Byte, 10) { Frozen = true };
pin3.ApplyFreeze();
Check("a failed freeze re-write is reported", failingFreeze.Failures, 1);
Console.WriteLine();

Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

long TryParse(string s) { ScanValue.TryParse(s, out long v); return v; }

// A fake read/write channel: records the last value written and can simulate a failing write, so the
// frozen-value view-model's poke / freeze / width-guard / failure logic runs headlessly.
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
