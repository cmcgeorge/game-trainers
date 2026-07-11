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

const string Board01B64 =
    "1hYAKfX1HpZpdYxeUBer+l15NXr2W0ZOquls5D3fd5fTOxx+W3e/Ov23U6xc7wO75m94sZDJadg8ScbotzwQdzzuT2j+f56/yMrn1Zs5tt02e0J7cUbkYZrSMhk4kIXKISXfDJTMyQDhLRnGC0xGqITKfHtlUXCw4o/T7fiRIPWgdDpuZzjwfV2OyNTrl1vpxOU2GxKDUkWkIDffBjs7ExHI8Um3JtQzCYVTq4LA/84SbEQaAgWS6KpYjReFgMCJ5UX0ez5IVZiBIRFQY3HrthzQahl88R+Vk3taTzUmlCHV7zb7g9HzePyAareL5oTn+Jw+QK7Y1GrZcr2tVvHN77M5tGHvkkRQAVAQWilNrykUwhrIuB8UwWm4AmNTJfSkllgtmyVWysZQCCQ4zHSigjtYQOGQ+IGZmiGBK0TG5BoTgrkMeYrGzSlSZVqbTY3EPrkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuR+F+F+F+FXkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5LkuS5H4X4X4X4VZO5Fwuv7iy4X4X4X4X4F+BfhfhfhfhfhfhfhfhfhfhfhfhfhfhfhfhfhfhfgWwr/d5+uFWTuRcKvv577+/n+/v7+/n4L78Qvu/bH/sZ32M77Gd9jO+xnfYzvsIX6v9/f3ffwuJfXlwqydyLhfhV8r68vl9evXoWP/f16ft7X2L7F9i4PfYvsX2Nr6t54tevXievPwthVk7k/IuFXll7+Xy+vXry+/D/9e/29r7F9i+xfYvsX2L7G19V/r169efi+JcK+Tre5LkXCvff39eX169eXz/d93y/b2vsX2L7Fxi+xfYvsbX1V8/DW07vvXmmBmxLhVk63uS5Fwqvr39Pl9ef7+nuy+vT5fZ3zM75md8wnzNA/MzvmZ3zLy+/r0+vcW75cKsnW9yXIuFfi+Jp69/fy80/39evXhcT/PxfEvn4mnnvv69evXp9Pr2phVk63uS5Fwq4rEvrz/f17+/n4l9e/rxPfy+e+vf39eha9Prmr15cKsnW9yXIuFf7+ha8voWvXNXj/16fXj/Eos/1zX8voWvXp9euWXp9Cy4VZOt7kuRcK/B809evXr0h1717+/v7+/v65q9c1evXr169Pr168uFWTre5LkXCvFr168vr19GvXnvv7+vf38vrmry+P8XPZfT5/r0LXn+XCrJ1vclyLhV9Pr0+V9evXry+vPxH+J+Jp7+Xzxa8uKxNPXl9evXl8uFWTre5LkXCr69evf39e/v69eJ7+/r0LL5fXv7+vXr0+vXl8uBVk63uS5Fwq+n0+fi+DTT168/F8Svrz/f39/L4XETEvrwuL4l9evL68uFWTre5LkXCq+f7+aYrEvoWvLisS+vL693y+Xy4pkz8TTy4rEvv7+X0LLhVk63uS5Fwrxa5ZC38Ty+vXie/r15fL68vrmv7+/v7+/v5X169d64VZOt7kuRcKvryvr15fT6fXr16fL7+/r167br16fXr15fXoWu9cKsnW9yXIuFX39/d9/L69evL69e/uLL69euavXr169c1en133euFWTre5LkXCq+ha6V0I1/rz/f39evXr15fXr19S5qfXr168r33fd4uJcKsnW9yXIuFX0+ulXRHFZl9ee+vXoWha9euavXr169evT68or4V89/e9cL8KsnclyLhX+/uLf3fXv69ehaFr168vr16Fr16Fr168r3z3z3z3971wqydyXIuFX169euavT69Pr168Li+JfT69evXr7U+nz8NPhp8NPhor3z+uFWTuS5Fwq+vT69c1Pl9evXp9eXFYl9Pr16fXl4efhfhpYaPk/Jrhp8NPhpYaLJ3Jci4VfXr35peauaV9c1c1evXv7+X169evXl8uGljIfJ1vW/k/J+T8nW9yXIuFP8XxLzVzV0P5peauavXr169eX169evXj/DSw0fJ1vW9b1vW9b1vW9yXIuFXE/1zSimrmrmrmr15+Jp5fd9e/l8/F2TPxfBpcNHydb1vW9b1vW9b1vW9yXIuFeLT68/EL65p77+vGeJfL69ehZfLisS4rEuGiydb1vW9b1vW9b1vW9yPwthV9evf165pfXr3fXv7+/v68T39/E8uGnw0WTret63ret63ret7kXCvfXrx/i+JRa9eha9en0+hZ+ImJRaFj/Bmr69/LhosnW9b1vW9b1vW9b3IuFX15+F+F+F+F+F+F+F+F+F+F+F+F+F+F+F+F+F+FX0KPLhosnW9b1vW9b1vW9b3JqYX4X4Wwr5Pyfk/J+T8n5Pyfk/J+T8n5Pyfk/JrhfhfhbDRZOt63ret63ret63pPyfk/J+Tret63ret63ret63ret63ret63rfyfk/J+Tret63ret63ret63Rh8UHySzAItVyNVs2rpOqdu+e1WQ/Su37gWeBGKViukX/aQjCHCFnQgQoWxoWA1sRrDI1l14Lq6bV1X6UKdW7bEO322oUV1Tr1SrVbHqgmqAmqX2vQnw47jpMjnJTUcQaEm4Yhlz4Jo4a1gaOlDk5zaIlK0GVOBwDwTKHKLrlHMiueHQAGg2iYENjjuOkyJ5QfdgcRC+aRrGMmeYQSD4rnOaVN9HShyWgY9FMA84uhLdEz7OJ5BmlulEQIbZtoutMlkP2UO1GxqSPUFqjdQ9QWqNyD1Bao1M9QWqNNHqC1RrUeoLVGjT1Bao1dlWk3QJtVyzaDB3Vksh7rB3VsoU42w3erdZD9lqdrVjbByCB8SBWVrSk6z1qKmPpy9z7OJ6hig3GMNBrN0EMoxnckvDuBhlbemSHQlZiNHy3REmzieQZpbp7o0B18SOcvTNFxw4EpguhDrj+iiIp2FA8YtaKDubadB6gWyQ7u69QLArLiKE7Ao2oY0INl5u4eWbJoXCRUPF0fyxafBxxxulaHIRzrcZgGJg5wk6kYz7YQStAZWYjR8AjAQy5yJejNxshSMWfbAUrHc8AzfEYO57OPRlsGbpKS2CzZZE58FzfKh5AMcC+mzbSUY5CSzaSzqBDaDNJRayHjjK1qRmzJQZQ5Q8BOJM+sQCXlmcTTKHJaNu19G0rKEOg4QVnE9RsJgi5hlwqJZXPuhK29No8j29b6Q/TvDgBm3I29C5p8eA3uNZtlq3hqgCjzeW0+SyHo/LccZWtSDtc8OhIw0AQGDjbRu5rTlZlhgLmsKeDE85sDFklbBcZtg6imuDJS1jhsNwM9qI4ZAIvgKcY1hmCLbMbBD3W75bT5IBdH5bjjuKxNNyvpRM0ywyWuKO6IxELGDOBhpZQRw3cM6XpbbytLPR7UPDMtcV20QRKBMtOaNuQ2rqyWFxq7LVtxtBqFsnS+61C2y1O1tSWwRhJ7GfBc3yoeQDH0kREnQxUZk590MVl2Id+gKklsN8tNkuZuwWZ2RLZRw5QggTny6QwDPJAJk7b0mXrqdYZQNOC2cjebrmb7O3TdczcNbBG6dtCkOMuxDv0BUzCPnIF0Lmnx4FeChDWBrACPQ7uj288FCW2POze49Du42AFHm9lqThjO2M+ENTM1MzUzNTMzfKh5AMfSRESdDFHyspDueznCGdFY53FZSceNgjDd6t1EP2Wp2srGxxAhAw19HayhlwHRbXdAOkRvZQ0eMh6IJtZyIcFpsZc2NabO1QDMFmdkS2VuhkeCnK6dOdrDDrdP0AV7etSAHKBlhs4TRwllc+DDKSwBag4FGC395iAehEmtJjLkt+z5ieCGXFyTKGctmgKOZmOnLI0ozwCMaSxxxDpMmQZnQVI1hHHmO96eNAlGbJKktyh50DrUU8GJ4IZc+ZguPBNCWOXmOnTmKUMBpSM1oQKuMPiJfKZdRpmx8pm4HZcQuEZ52i6QEm5rBlDlEox0LcDreWMN19kYp1sN1JaIJtERDgtBh+0WRYdRsFZWtSGIVBNgA4TZ8x05ZG7mDdHtQ8Y5TSkcxQNcU+LuiuZtNzWO9zXKHjkktH5cUccQdzxxBUYGclGhQy9DtgfHCCH4mA+SIFzb1qQxCoJ6Wxo9ZZ8NrK6VlQKmxx3FYnqZo6zHXyTKEtIK9dFmFrQcRC4BnIkOCxW2uKkN4LRB16PGxx3HSZMhK10Xbms0cRCOfDFogi38vlMzGMjXdneAzLYzReaESQg2Xm3rUjWHrZ4ENjhyunTiJQls4nqaQ5oHLccAuIMobMc4QGDjfkPzisXJMoZAAvhvD5cYTijueztISs4fBWcTyHE0xwsaBDY47kZrlpRrMs2YKBAYOMsBotbhUdGijolnps+iFJnE0woA6BgQ3stTtZECGxx3Ix52BQy2caZtqXDzQlQKDThiGX1zEPuE2J+sZOhEnp6KZnPnztZw6hY0CG2xbLU12TI9vW+kP05ZNCxcx7R4cAM2N2tnapuuWhcs2WLrllmP2nhw3soRsbQejOrJOtCNZtlWk3WQ916M6nLN7LU7Q2BDeyNpkPDShUN2umF0LLIXbXSevpeuA2u4gC5rLu9CEgMtQCUa6eGBy9sIvAum2Ey3HYDauOPooVaDJZ25OxiDgIji0qP0AJ7etSDLwGjaDMzM+mO5tC9MCFM+dsnb1qQFQP9dFQdwR4fdoQyiFzjzsBhlpws2SXHeizC1oRTz2cwDJlEWbhWDoAKJeyYNaO07OgWzpWgypwX0tJg044wWnIJI7bAUrHXP20YCI5iiDDMh+fiGX1zEPuFpwOm+A+jMowRdUe2w6zN2EMaBtxHQxWXYh36AqZkEHkAxwlqHI6GKPlZSRqUlsMw0DZBDKIXOPPpDajrns2ZnHQcIKziaZQ5LQFBpwxDLl8uj+UbAPDrnnexIQyiHIPWoqCp+W4KbWkx9FAklMxWN1DmTDEC6UQ6DuCYCB/XD7ezbvGWNN4ZZlwM22672rV32o/2h/abVUkxs0knEbChRQ/SaXntRk5xa7GKVt60IkHm5lDuDYxc+IJDgHhyg7XohghMT4eZ8x06cM54QphhOIuNwGXG1HclsALsIKoDaBksMBEJIFEhHUdwTzNHYFsvpkP1JvpaGYdTcJ2SDFKZsoh1G9aESL5TbfrsIyZbRmFph4sPEJi9EMsyUoIht2d0Y7OCzjJ6YZwVagiPpPSdtvIOIhcO6YNWQGFq1hxSD2gtksFrde0FtlqdpmkvZGNuZD0kytakHOAO2yRk9HshiHUUAcHMBYXX07j5pzQfR5rj6L87QPFkktlmoNjXtN/a28NbuawPWsymt0tZtAlYOIhdAnMZ6OXSkxA3AAYHeVZphBfScnOBhlDGmYbfnaOXTDIc1sgXTnpAsQDw7JmlabWpkJmmoamzDG3mau+R3ioWMg8gLZO2fOvIC2y1O0i8lsFmzkTnwXN8qHkAx9JERJ0MXNazNgYZWm0x2iB5ruM3NYXEhwEgN7JDUEQygacFpslzN2xbLU1wFJLOosmosU0WNELw4623uoENjjuOkyagMj+0PHYjjx1Z91BQxa0CcWVkeip8CGxpowy+SBAfTkInKog2KeDdfKO8OHjCc6j8/LXAPtlqThjO2EiyN2mY0zNTbb7tl7rC6jTNj5TN29xtzzxmbqTzZ1FobT4sd7APXBPnu9md+vtRsryESBJkIlBdtGy9tEkyh7XZnPbBmw";

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

Console.WriteLine("Item type table (Lists.ITEM_TYPES):");
Check("item type count", InventoryFormat.ItemTypeNames.Length, 32);
Check("type 0", InventoryFormat.ItemTypeName(0), "General Item");
Check("type 5 (Sword)", InventoryFormat.ItemTypeName(5), "Sword");
Check("type 24 (Helmet)", InventoryFormat.ItemTypeName(24), "Helmet");
Console.WriteLine();

Console.WriteLine("Item templates / apply / infinite charges / duplicate:");
Check("catalog non-empty", ItemTemplates.All.Count > 0, true);
Check("all headers 11 bytes", ItemTemplates.All.All(t => t.Header.Length == InventoryFormat.OffItemName), true);
Check("template names unique", ItemTemplates.All.Select(t => t.Name).Distinct().Count(), ItemTemplates.All.Count);

var invRec = new CharacterRecord(new byte[RosterFormat.RecordSize]);
var wand = ItemTemplates.All.First(t => t.Name == "Wand");
var slot0 = invRec.GetItem(0);
slot0.Apply(wand);
Check("applied name", slot0.Name, "Wand");
Check("applied type is General Item", slot0.TypeName, "General Item");
Check("applied slot occupied", slot0.IsEmpty, false);
Check("Wand is chargeable", slot0.IsChargeable, true);

slot0.Charges = 5;
Check("charges set to 5", slot0.Charges, 5);
slot0.Charges = InventoryFormat.MaskCharges;
Check("infinite charges = 63", slot0.Charges, 63);
Check("charges never exceed 63", new Func<bool>(() => { slot0.Charges = 99; return slot0.Charges == 63; })(), true);

int dup = invRec.DuplicateItem(0);
Check("duplicate lands in slot 1", dup, 1);
Check("duplicate name matches", invRec.GetItem(1).Name, "Wand");
Check("duplicate charges match source", invRec.GetItem(1).Charges, slot0.Charges);

var full = new CharacterRecord(new byte[RosterFormat.RecordSize]);
for (int i = 0; i < InventoryFormat.SlotCount; i++) full.GetItem(i).Apply(wand);
Check("duplicate into full inventory = -1", full.DuplicateItem(0), -1);

Check("empty slot not chargeable", invRec.GetItem(5).IsChargeable, false);
Console.WriteLine();

Console.WriteLine("Board terrain (map 0x01 Purgatory, primary chunk 0x47 from DATA1):");
byte[] boardChunk = Convert.FromBase64String(Board01B64);
byte[] boardBytes = HuffmanDecoder.Decode(boardChunk);
Check("decompressed length", boardBytes.Length, 5846);
var board = BoardMap.TryParse(boardBytes);
Check("board parsed", board is not null, true);
if (board is not null)
{
    Check("width", board.Width, 34);
    Check("height", board.Height, 34);

    int west = 0, north = 0, doors = 0, water = 0, abyss = 0, stone = 0;
    for (int y = 0; y < board.Height; y++)
    {
        for (int x = 0; x < board.Width; x++)
        {
            var sq = board.Square(x, y);
            if (sq.West != WallKind.None) west++;
            if (sq.North != WallKind.None) north++;
            if (sq.West == WallKind.Door) doors++;
            if (sq.North == WallKind.Door) doors++;
            switch (sq.Floor)
            {
                case FloorKind.Water: water++; break;
                case FloorKind.Abyss: abyss++; break;
                case FloorKind.Stone: stone++; break;
            }
        }
    }
    Check("west walls", west, 305);
    Check("north walls", north, 319);
    Check("doors", doors, 40);
    Check("water squares", water, 165);
    Check("abyss squares", abyss, 0);
    Check("stone squares", stone, 0);
}
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
