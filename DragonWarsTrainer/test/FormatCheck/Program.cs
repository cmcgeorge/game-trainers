using DragonWarsTrainer.Game;

// Headless verification harness for the Dragon Wars roster parser. It decodes a captured
// 4-record slice of the live party roster (the opening party: Muskels, Theb, Elendil,
// Cheetah) and asserts every parsed field against values read straight from the memory dump,
// then checks the Dragon Wars name encode/decode round-trip and IsOccupied. Exits 0 on
// success, 1 on any failure, so it can gate the build (Run.ps1 -Test).

// First 4 512-byte character records from dosbox-x-51092-20260709-114600-030.bin
// (roster slot 0 begins at the "Muskels" record).
using DragonWarsTrainer.Memory;
using GameTrainers.Common.Memory;

if (args.Length >= 2 && args[0] == "--live")
{
    int livePid = int.Parse(args[1]);
    using var liveMem = ProcessMemory.Open(livePid);
    Console.WriteLine($"Attached to pid {livePid} (IsOpen={liveMem.IsOpen}). Running RosterLocator.FindAll…");
    int regionCount = 0;
    foreach (var _ in liveMem.EnumerateRegions()) regionCount++;
    Console.WriteLine($"EnumerateRegions yielded {regionCount} region(s).");
    var live = RosterLocator.FindAll(liveMem);
    Console.WriteLine($"FindAll returned {live.Count} character(s).");
    foreach (var lc in live)
        Console.WriteLine($"  slot {lc.Slot}: {lc.Record.Name} @ 0x{(ulong)lc.Address:X} (L{lc.Record.Level}, HP {lc.Record.HealthCurrent}/{lc.Record.HealthMax})");
    return live.Count == 0 ? 2 : 0;
}

const string Roster4B64 =
    "zfXz6+XscwAAAAAAFRUUFAoKCgoQABAAEAAQAAAAAAAAAQEBAAAAAAAAAAEBAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAFBQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADU6OViAAAAAAAAAAAODhgYCgoKCg4ADgAOAA4AAAAAAAAAAAABAAEAAQEBAQAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAYGAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMXs5e7k6WwAAAAAAAoKEBAMDA4ODAAMAAwADAAcABwAAAAAAAACAAAAAAABAAEAAQEAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAABAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAw+jl5fThaAAAAAAACwsMDBAQDQ0NAA0ADQANABoAGgABAAAAAAAAAAAAAAEAAAEAAQABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQEAAAAAAAAAAAADAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

int failures = 0;
byte[] roster = Convert.FromBase64String(Roster4B64);
Console.WriteLine($"Decoded roster buffer: {roster.Length} bytes ({roster.Length / RosterFormat.RecordSize} records)");
Console.WriteLine();

CheckCharacter(0, "Muskels", str: 21, dex: 20, intel: 10, spr: 10,
    hp: 16, stun: 16, pow: 0, gender: 0, level: 1, av: 5, dv: 5, ac: 0);
CheckCharacter(1, "Theb", str: 14, dex: 24, intel: 10, spr: 10,
    hp: 14, stun: 14, pow: 0, gender: 0, level: 1, av: 6, dv: 6, ac: 0);
CheckCharacter(2, "Elendil", str: 10, dex: 16, intel: 12, spr: 14,
    hp: 12, stun: 12, pow: 28, gender: 0, level: 1, av: 4, dv: 4, ac: 0);
CheckCharacter(3, "Cheetah", str: 11, dex: 12, intel: 16, spr: 13,
    hp: 13, stun: 13, pow: 26, gender: 1, level: 1, av: 3, dv: 3, ac: 0);

Console.WriteLine("Name encode / decode round-trip:");
foreach (var name in new[] { "A", "Bo", "Muskels", "Elendil", "TwelveLetter" })
{
    var rec = new CharacterRecord(new byte[RosterFormat.RecordSize]);
    rec.Name = name;
    Check($"round-trip \"{name}\"", rec.Name, name);
}
Console.WriteLine();

Console.WriteLine("Inventory (the opening party carries no items):");
for (int slot = 0; slot < 4; slot++)
{
    var rec = new CharacterRecord(roster, slot * RosterFormat.RecordSize);
    Check($"slot {slot} item count", rec.ItemCount, 0);
    for (int i = 0; i < InventoryFormat.SlotCount; i++)
        Check($"slot {slot} item {i} empty", rec.GetItem(i).IsEmpty, true);
}
Console.WriteLine();

Console.WriteLine("Item slot geometry:");
Check("inventory block ends at record boundary",
    InventoryFormat.OffInventory + InventoryFormat.SlotCount * InventoryFormat.SlotSize,
    RosterFormat.RecordSize);
Console.WriteLine();

Console.WriteLine("IsOccupied:");
Check("occupied slot 0", new CharacterRecord(roster, 0).IsOccupied, true);
Check("empty (0xFF) slot", new CharacterRecord(FilledWith(0xFF)).IsOccupied, false);
Check("empty (0x00) slot", new CharacterRecord(new byte[RosterFormat.RecordSize]).IsOccupied, false);
Console.WriteLine();

Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the 512-byte record layout decodes the sample party correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

void CheckCharacter(int slot, string name, int str, int dex, int intel, int spr,
    int hp, int stun, int pow, int gender, int level, int av, int dv, int ac)
{
    var rec = new CharacterRecord(roster, slot * RosterFormat.RecordSize);
    Console.WriteLine($"Slot {slot}: {rec.Name}");
    Check("name", rec.Name, name);
    Check("strength", rec.Strength, str);
    Check("dexterity", rec.Dexterity, dex);
    Check("intelligence", rec.Intelligence, intel);
    Check("spirit", rec.Spirit, spr);
    Check("health cur", rec.HealthCurrent, hp);
    Check("health max", rec.HealthMax, hp);
    Check("stun cur", rec.StunCurrent, stun);
    Check("stun max", rec.StunMax, stun);
    Check("power cur", rec.PowerCurrent, pow);
    Check("power max", rec.PowerMax, pow);
    Check("gender", rec.Gender, gender);
    Check("level", rec.Level, level);
    Check("armor value", rec.ArmorValue, av);
    Check("defense value", rec.DefenseValue, dv);
    Check("armor class", rec.ArmorClass, ac);
    Check("is occupied", rec.IsOccupied, true);
    Console.WriteLine();
}

byte[] FilledWith(byte b)
{
    var a = new byte[RosterFormat.RecordSize];
    Array.Fill(a, b);
    return a;
}

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-16} = {actual}" + (ok ? "" : $"   (expected {expected})"));
}
