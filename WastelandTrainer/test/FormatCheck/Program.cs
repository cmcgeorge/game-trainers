using GameTrainers.Common.Memory;
using WastelandTrainer.Game;
using WastelandTrainer.Memory;
using WastelandTrainer.ViewModels;

// Headless verification harness for the Wasteland roster parser. It decodes a captured 7-record
// slice of the live party roster — the default party (Hell Razor, Angela Deth, Thrasher,
// Snake Vargas) followed by three empty slots — and asserts every parsed field against the
// ground-truth stats recorded in .data/memdump.md, then checks the name round-trip, the packed
// skill/inventory decoding, and IsOccupied. Exits 0 on success, 1 on any failure, so it can gate
// the build (Run.ps1 -Test).
//
// The slice was cut from dosbox-x-16124-20260715-112525-175.bin (party inside the Ranger Center),
// starting at roster slot 0 (Hell Razor's name); see .data/extract.ps1.

// Optional live smoke test for the create-screen roller:
//   FormatCheck --createscan <pid> <STR IQ LCK SPD AGL DEX CHR> [MAXCON] [SKP]
// runs CreationScanner.Find against a running emulator sitting on the Ranger Center create screen and
// reports where the temporary roll buffer was located (and whether the record shape was confirmed).
if (args.Length >= 9 && args[0] == "--createscan")
{
    if (!int.TryParse(args[1], out int scanPid))
    {
        Console.WriteLine("Usage: FormatCheck --createscan <pid> <STR IQ LCK SPD AGL DEX CHR> [MAXCON] [SKP]");
        return 2;
    }
    var attrs = new int[CharacterFormat.AttributeCount];
    for (int k = 0; k < attrs.Length; k++) attrs[k] = int.Parse(args[2 + k]);
    int maxCon = args.Length >= 10 ? int.Parse(args[9]) : 0;
    int skp = args.Length >= 11 ? int.Parse(args[10]) : -1;

    using var scanMem = ProcessMemory.Open(scanPid);
    Console.WriteLine($"Attached to pid {scanPid} (IsOpen={scanMem.IsOpen}). Scanning for the create-screen roll "
        + $"[{string.Join(" ", attrs)}] MAXCON={maxCon} SKP={skp}…");
    var found = CreationScanner.Find(scanMem, attrs, maxCon, skp);
    Console.WriteLine($"CreationScanner.Find returned {found.Count} match(es):");
    foreach (var m in found)
    {
        var v = new int[CharacterFormat.AttributeCount];
        CreationScanner.TryReadAttributes(scanMem, m.Address, v);
        CreationScanner.TryReadMaxCon(scanMem, m.Address, out int mc);
        Console.WriteLine($"  0x{(ulong)m.Address:X}  structural={m.Structural,-5}  attrs=[{string.Join(" ", v)}]  MAXCON@+{CreationScanner.AttrToMaxCon}={mc}");
    }
    return found.Count == 0 ? 2 : 0;
}

// Diagnostic: FormatCheck --find <pid> <text> [text2 …] searches all committed memory for each ASCII
// string and prints every hit's address plus its offset from the located roster.
if (args.Length >= 3 && args[0] == "--find")
{
    if (!int.TryParse(args[1], out int fpid))
    {
        Console.WriteLine("Usage: FormatCheck --find <pid> <text> [text2 …]");
        return 2;
    }
    using var fm = ProcessMemory.Open(fpid);
    var fp = PartyLocator.Find(fm);
    nuint rb = fp?.RosterBase ?? 0;
    Console.WriteLine(fp == null ? "(no roster located; offsets are absolute)" : $"located roster @ 0x{(ulong)rb:X}");
    for (int ai = 2; ai < args.Length; ai++)
    {
        byte[] needle = System.Text.Encoding.ASCII.GetBytes(args[ai]);
        var found = new List<nuint>();
        const int Chunk = 1 << 16;
        int overlap = needle.Length - 1;
        byte[] sbuf = new byte[Chunk + overlap];
        foreach (var region in fm.EnumerateRegions())
        {
            nuint rend = region.Base + region.Size;
            for (nuint s = region.Base; s < rend && found.Count < 40;)
            {
                nuint rem = rend - s;
                int want = (int)Math.Min((nuint)Chunk, rem);
                int rl = (int)Math.Min((nuint)(want + overlap), rem);
                int rd = fm.Read(s, sbuf, rl);
                for (int i = 0; i + needle.Length <= rd; i++)
                {
                    bool ok = true;
                    for (int j = 0; j < needle.Length; j++) if (sbuf[i + j] != needle[j]) { ok = false; break; }
                    if (ok) { found.Add(s + (nuint)i); if (found.Count >= 40) break; }
                }
                s += (nuint)want;
            }
        }
        Console.WriteLine($"\n\"{args[ai]}\": {found.Count} hit(s){(found.Count >= 40 ? " (capped)" : "")}");
        foreach (var a in found)
        {
            long rel = (long)(ulong)a - (long)(ulong)rb;
            Console.WriteLine($"  0x{(ulong)a:X}   roster{(rel >= 0 ? "+" : "")}{rel}");
        }
    }
    return 0;
}

// Diagnostic: FormatCheck --peek <pid> <hexAddr> <len> hex-dumps an arbitrary memory range.
if (args.Length >= 4 && args[0] == "--peek")
{
    if (!int.TryParse(args[1], out int ppid) || !int.TryParse(args[3], out int plen))
    {
        Console.WriteLine("Usage: FormatCheck --peek <pid> <hexAddr> <len>");
        return 2;
    }
    ulong paddr = Convert.ToUInt64(args[2].Replace("0x", ""), 16);
    using var pm = ProcessMemory.Open(ppid);
    var pbuf = new byte[plen];
    int pr = pm.Read((nuint)paddr, pbuf, plen);
    Console.WriteLine($"Read {pr}/{plen} bytes at 0x{paddr:X}:");
    for (int row = 0; row < pr; row += 16)
    {
        int n = Math.Min(16, pr - row);
        string hex = string.Join(" ", Enumerable.Range(row, n).Select(k => pbuf[k].ToString("X2")));
        string asc = new string(Enumerable.Range(row, n).Select(k => pbuf[k] >= 0x20 && pbuf[k] < 0x7F ? (char)pbuf[k] : '.').ToArray());
        Console.WriteLine($"  0x{paddr + (ulong)row:X}: {hex,-47}  {asc}");
    }
    return 0;
}

// Diagnostic: FormatCheck --diffhdr <pid> <hexRosterA> <hexRosterB> reads the party-state header in
// front of two rosters and prints them side by side, marking bytes that differ — to find a static
// field that separates the live party from a frozen stale snapshot.
if (args.Length >= 4 && args[0] == "--diffhdr")
{
    if (!int.TryParse(args[1], out int dpid))
    {
        Console.WriteLine("Usage: FormatCheck --diffhdr <pid> <hexRosterA> <hexRosterB>");
        return 2;
    }
    nuint HdrOf(string hexRoster) =>
        (nuint)(Convert.ToUInt64(hexRoster.Replace("0x", ""), 16) - (ulong)CharacterFormat.PartyHeaderSize);
    using var dm = ProcessMemory.Open(dpid);
    nuint a = HdrOf(args[2]), b = HdrOf(args[3]);
    var ba = new byte[CharacterFormat.PartyHeaderSize];
    var bb = new byte[CharacterFormat.PartyHeaderSize];
    if (dm.Read(a, ba, ba.Length) != ba.Length || dm.Read(b, bb, bb.Length) != bb.Length)
    { Console.WriteLine("A header was unreadable."); return 2; }
    Console.WriteLine($"A header @ 0x{(ulong)a:X}   B header @ 0x{(ulong)b:X}   (* = differs)");
    for (int row = 0; row < CharacterFormat.PartyHeaderSize; row += 16)
    {
        string HexAsc(byte[] buf)
        {
            string hex = string.Join(" ", Enumerable.Range(row, 16).Select(k => buf[k].ToString("X2")));
            string asc = new string(Enumerable.Range(row, 16).Select(k => buf[k] >= 0x20 && buf[k] < 0x7F ? (char)buf[k] : '.').ToArray());
            return $"{hex}  {asc}";
        }
        bool diff = Enumerable.Range(row, 16).Any(k => ba[k] != bb[k]);
        Console.WriteLine($" {(diff ? "*" : " ")}{row:X2}: {HexAsc(ba)}   |   {HexAsc(bb)}");
    }
    return 0;
}

// Diagnostic: FormatCheck --mapscan <pid> enumerates EVERY party-shaped structure in memory and, for
// each, the party-state header that precedes it (plausible?, map name, X/Y). This surfaces the live
// roster next to any stale snapshot (e.g. a booted-defaults copy that lingers after an in-process game
// restart) so we can see which header fields tell the live party apart from a stale-but-valid one.
// Pass a second arg to also re-scan after that many milliseconds and diff the headers (movement/CON
// regen changes only the live copy): FormatCheck --mapscan <pid> [delayMs]
if (args.Length >= 2 && args[0] == "--mapscan")
{
    if (!int.TryParse(args[1], out int mpid))
    {
        Console.WriteLine("Usage: FormatCheck --mapscan <pid> [delayMs]   (pid must be a number)");
        return 2;
    }
    using var mm = ProcessMemory.Open(mpid);
    Console.WriteLine($"Attached to pid {mpid} (IsOpen={mm.IsOpen}). Enumerating party-shaped structures…");

    List<(nuint rosterBase, int count, string names, string header)> Enumerate()
    {
        int rosterBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;
        const int Chunk = 1 << 20;
        int overlap = rosterBytes - 1;
        byte[] buf = new byte[Chunk + overlap];
        byte[] hb2 = new byte[CharacterFormat.PartyHeaderSize];
        var seen = new HashSet<ulong>();
        var rows = new List<(nuint, int, string, string)>();
        foreach (var region in mm.EnumerateRegions())
        {
            nuint rend = region.Base + region.Size;
            for (nuint s = region.Base; s < rend;)
            {
                nuint rem = rend - s;
                int want = (int)Math.Min((nuint)Chunk, rem);
                int readLen = (int)Math.Min((nuint)(want + overlap), rem);
                int rd = mm.Read(s, buf, readLen);
                for (int i = 0; i + rosterBytes <= rd; i++)
                {
                    if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                    // Only report maximal rosters: skip a window whose preceding record is itself a
                    // member (that is a sub-roster of a larger one starting earlier).
                    if (i >= CharacterFormat.RecordSize && CharacterRecord.IsValidRecord(buf, i - CharacterFormat.RecordSize)) continue;
                    var names = new List<string>();
                    bool emptySeen = false, ok = true;
                    for (int slot = 0; slot < CharacterFormat.MaxSlots; slot++)
                    {
                        int off = i + slot * CharacterFormat.RecordSize;
                        if (CharacterRecord.IsValidRecord(buf, off))
                        {
                            if (emptySeen) { ok = false; break; }
                            names.Add(new CharacterRecord(buf, off).Name);
                        }
                        else if (buf[off + CharacterFormat.OffName] == 0x00) emptySeen = true;
                        else { ok = false; break; }
                    }
                    if (!ok || names.Count == 0) continue;
                    nuint rosterBase = s + (nuint)i;
                    if (!seen.Add((ulong)rosterBase)) continue;
                    string header;
                    if (rosterBase >= (nuint)CharacterFormat.PartyHeaderSize
                        && mm.Read(rosterBase - (nuint)CharacterFormat.PartyHeaderSize, hb2, hb2.Length) == hb2.Length)
                        header = $"header plausible={PartyHeader.IsPlausible(hb2),-5} map=\"{MapBook.MapName(hb2)}\" X={hb2[CharacterFormat.HeaderPartyX]} Y={hb2[CharacterFormat.HeaderPartyY]}";
                    else
                        header = "header <unreadable>";
                    rows.Add((rosterBase, names.Count, string.Join(", ", names), header));
                }
                s += (nuint)Math.Max(CharacterFormat.RecordSize, want);
            }
        }
        return rows;
    }

    var first = Enumerate();
    Console.WriteLine($"{first.Count} candidate roster(s):");
    foreach (var r in first.OrderBy(r => (ulong)r.rosterBase))
        Console.WriteLine($"  0x{(ulong)r.rosterBase:X}  {r.count} member(s)  [{r.names}]  {r.header}");

    var picked = PartyLocator.Find(mm);
    Console.WriteLine(picked == null
        ? "\nPartyLocator.Find: (none)"
        : $"\nPartyLocator.Find picks: 0x{(ulong)picked.RosterBase:X}  [{string.Join(", ", picked.Members.Select(m => m.Record.Name))}]");

    // Optional second pass after a delay: take a step (or let CON regen) in-game during the delay and
    // the LIVE copy's header/roster changes while stale snapshots stay frozen — the cleanest live/stale tell.
    if (args.Length >= 3 && int.TryParse(args[2], out int delayMs) && delayMs > 0)
    {
        Console.WriteLine($"\nRe-scanning after {delayMs} ms — move the party or wait for CON regen now…");
        System.Threading.Thread.Sleep(delayMs);
        var second = Enumerate();
        var before = first.ToDictionary(r => (ulong)r.rosterBase, r => r.header);
        int changed = 0;
        foreach (var r in second.OrderBy(r => (ulong)r.rosterBase))
            if (before.TryGetValue((ulong)r.rosterBase, out var h0) && h0 != r.header)
            {
                Console.WriteLine($"  CHANGED 0x{(ulong)r.rosterBase:X} [{r.names}]\n    was {h0}\n    now {r.header}");
                changed++;
            }
        Console.WriteLine(changed == 0 ? "  (no header changed — nothing moved during the delay)" : $"  {changed} roster header(s) changed.");
    }
    return 0;
}

// Optional live smoke test: FormatCheck --live <pid> runs the structural PartyLocator against a
// running emulator instead of the embedded fixture.
if (args.Length >= 2 && args[0] == "--live")
{
    if (!int.TryParse(args[1], out int livePid))
    {
        Console.WriteLine("Usage: FormatCheck --live <pid>   (pid must be a number)");
        return 2;
    }
    using var liveMem = ProcessMemory.Open(livePid);
    Console.WriteLine($"Attached to pid {livePid} (IsOpen={liveMem.IsOpen}). Running PartyLocator.Find…");
    int regionCount = 0;
    foreach (var _ in liveMem.EnumerateRegions()) regionCount++;
    Console.WriteLine($"EnumerateRegions yielded {regionCount} region(s).");
    var party = PartyLocator.Find(liveMem);
    if (party == null) { Console.WriteLine("No party located."); return 2; }
    Console.WriteLine($"Found roster at 0x{(ulong)party.RosterBase:X} with {party.Members.Count} character(s).");

    // Report the party-state header that anchors the located roster. The live roster is preceded by a
    // plausible header (map name + in-range X/Y); a stale copy of deleted rangers is not — this is the
    // discriminator the locator now ranks on, so a healthy "plausible=True" here is the fix working.
    var headerBuf = new byte[CharacterFormat.PartyHeaderSize];
    nuint headerBase = party.RosterBase - (nuint)CharacterFormat.PartyHeaderSize;
    if (liveMem.Read(headerBase, headerBuf, CharacterFormat.PartyHeaderSize) == CharacterFormat.PartyHeaderSize)
        Console.WriteLine($"  header @ 0x{(ulong)headerBase:X}: plausible={PartyHeader.IsPlausible(headerBuf)} "
            + $"map=\"{MapBook.MapName(headerBuf)}\" X={headerBuf[CharacterFormat.HeaderPartyX]} Y={headerBuf[CharacterFormat.HeaderPartyY]}");
    else
        Console.WriteLine($"  header @ 0x{(ulong)headerBase:X}: <unreadable>");

    foreach (var lc in party.Members)
    {
        var r = lc.Record;
        Console.WriteLine($"  slot {lc.Slot}: {r.Name} @ 0x{(ulong)lc.Address:X} (CON {r.Con}/{r.MaxCon}, weaponByte 0x{r.Bytes[CharacterFormat.OffWeaponState]:X2})");
        for (int s = 0; s < CharacterFormat.ItemSlots; s++)
        {
            int id = r.GetItemId(s);
            if (id == 0) continue;
            int qty = r.GetItemQty(s);
            Console.WriteLine($"      inv[{s,2}] id {id,3} qty {qty,3} (0x{qty:X2}){(qty >= 0x80 ? " JAMMED?" : "")}  {ItemCatalog.ItemName(id)}");
        }
    }
    return party.Members.Count == 0 ? 2 : 0;
}

const string Roster7B64 =
    "SGVsbCBSYXpvcgAAAAAMDg0JDg8LAAAAAAAAHAAcAAwBAAAAAQAAAAAAAAAAAAAAAABQcml2YXRlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgIBAwEHAQkCDAEQAQ0BCAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADQceAB4AHgAeAB4AHgAeAB4ANgEsAC0ABAAxADQoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEFuZ2VsYSBEZXRoAAAACA8OCwoRDgAAAAEAABsAGwABAQAAAAEAAAAAAAAAAAAAAAAAUHJpdmF0ZQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwIHAQkBFAESAQoBFgEPARoBGQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABASIAAgACAAIAAgACAAIAAgADYBLAAtAAQAMQA0KAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABUaHJhc2hlcgAAAAAAABEKCwwQCQgAAAAAAAAiACIADQEAAAABAAAAAAAAAAAAAAAAAFByaXZhdGUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAECBAEDAQcBCQELAQ4BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAANBx4AHgAeAB4AHgAeAB4AHgA2ASwALQAEADEANCgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAU25ha2UgVmFyZ2FzAAAKEQwNCQ0MAAAAAAIAHwAfAAEBAAAAAQAAAAAAAAAAAAAAAABQcml2YXRlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABARgBAwEHAQkBBgEXARkCHAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEBIgACAAIAAgACAAIAAgACAANgEsAC0ABAAxADQoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

int failures = 0;
byte[] roster = Convert.FromBase64String(Roster7B64);
Console.WriteLine($"Decoded roster buffer: {roster.Length} bytes ({roster.Length / CharacterFormat.RecordSize} records)");
Console.WriteLine();

// Ground truth from .data/memdump.md (party inside the Ranger Center).
CheckCharacter(0, "Hell Razor", str: 12, iq: 14, lck: 13, spd: 9, agl: 14, dex: 15, chr: 11,
    maxCon: 28, gender: 0, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(1, "Angela Deth", str: 8, iq: 15, lck: 14, spd: 11, agl: 10, dex: 17, chr: 14,
    maxCon: 27, gender: 1, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(2, "Thrasher", str: 17, iq: 10, lck: 11, spd: 12, agl: 16, dex: 9, chr: 8,
    maxCon: 34, gender: 0, nationality: 0, level: 1, skp: 1, rank: "Private");
CheckCharacter(3, "Snake Vargas", str: 10, iq: 17, lck: 12, spd: 13, agl: 9, dex: 13, chr: 12,
    maxCon: 31, gender: 0, nationality: 2, level: 1, skp: 1, rank: "Private");

Console.WriteLine("Hell Razor's skills (packed id/level list):");
var hr = new CharacterRecord(roster, 0);
Check("Brawling (1)", hr.GetSkillLevel(1), 2);
Check("Climb (2)", hr.GetSkillLevel(2), 1);
Check("Clip Pistol (3)", hr.GetSkillLevel(3), 1);
Check("Swim (7)", hr.GetSkillLevel(7), 1);
Check("Perception (9)", hr.GetSkillLevel(9), 2);
Check("SMG (12)", hr.GetSkillLevel(12), 1);
Check("Acrobat (13)", hr.GetSkillLevel(13), 1);
Check("Silent Move (16)", hr.GetSkillLevel(16), 1);
Check("Knife Throw (8)", hr.GetSkillLevel(8), 1);
Check("skill he lacks (Doctor 32) = 0", hr.GetSkillLevel(32), 0);
Console.WriteLine();

Console.WriteLine("Hell Razor's inventory (packed id/qty list):");
Check("slot 0 id (M1911A1 = 13)", hr.GetItemId(0), 13);
Check("slot 0 ammo (7 rounds)", hr.GetItemQty(0), 7);
Check("slot 1 id (.45 clip = 30)", hr.GetItemId(1), 30);
Check("item count", hr.ItemCount, 15);
Console.WriteLine();

Console.WriteLine("Inventory compaction (the game reads the list only up to the first empty slot):");
var inv = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
inv.SetItem(0, 13, 7);    // M1911A1, 7 rounds
inv.SetItem(1, 30, 0);    // .45 clip
inv.SetItem(5, 4, 0);     // Knife written past a gap — invisible in-game until the list is compacted
inv.CompactInventory();
Check("gap closed: slot 2 id (Knife)", inv.GetItemId(2), 4);
Check("gap closed: slot 3 empty", inv.GetItemId(3), 0);
Check("compaction preserves ammo", inv.GetItemQty(0), 7);
Check("item count after compaction", inv.ItemCount, 3);
inv.SetItem(0, 0, 0);     // clear the leading item; later items must not be truncated
inv.CompactInventory();
Check("middle clear keeps later items", inv.ItemCount, 2);
Check("order preserved after clear: slot 0", inv.GetItemId(0), 30);
Check("order preserved after clear: slot 1", inv.GetItemId(1), 4);
Console.WriteLine();

Console.WriteLine("Ammo freeze tops ammo-bearing items to the max, leaving other items alone:");
var af = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
af.SetItem(0, 13, 7);    // M1911A1 (Firearm), 7 rounds  -> topped to MaxAmmo
af.SetItem(1, 30, 0);    // 45 clip (Ammo)               -> topped to MaxAmmo
af.SetItem(2, 54, 1);    // Rope (Gear & Quest)          -> left untouched
af.SetItem(3,  4, 0);    // Knife (Melee)                -> left untouched
af.SetItem(4, 38, 0);    // Leather jacket (Armor)       -> left untouched
var topped = new List<int>();
Check("top-up reports it raised something", AmmoFreeze.TopUp(af, topped), true);
Check("firearm topped to the max", af.GetItemQty(0), CharacterFormat.MaxAmmo);
Check("clip topped to the max", af.GetItemQty(1), CharacterFormat.MaxAmmo);
Check("rope (gear) left alone", af.GetItemQty(2), 1);
Check("knife (melee) left alone", af.GetItemQty(3), 0);
Check("armor left alone", af.GetItemQty(4), 0);
Check("only the two ammo slots were raised", topped.Count == 2 && topped.Contains(0) && topped.Contains(1), true);
Check("second pass is a no-op once full", AmmoFreeze.TopUp(af), false);
Console.WriteLine();

Console.WriteLine("Ammo freeze never lowers ammo and clears the jammed-weapon flag (qty byte bit 7):");
var jr = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
jr.SetItem(0, 29, 120);                                        // Meson cannon (Energy Weapon) already above the max, un-jammed
Check("above-max, un-jammed ammo is a no-op", AmmoFreeze.TopUp(jr), false);
Check("above-max value kept", jr.GetItemQty(0), 120);
jr.SetItem(0, 29, CharacterFormat.InventoryJammedFlag | 10);   // jammed, 10 charges (below the max)
Check("tops up a jammed weapon", AmmoFreeze.TopUp(jr), true);
Check("count raised to the max", jr.GetItemQty(0) & CharacterFormat.InventoryCountMask, CharacterFormat.MaxAmmo);
Check("jam flag cleared", jr.GetItemQty(0) & CharacterFormat.InventoryJammedFlag, 0);
// The stuck case: a weapon that jams while already at max ammo. The old "already full, skip"
// fast-path left bit 7 set forever; the freeze must still clear it.
jr.SetItem(0, 29, CharacterFormat.InventoryJammedFlag | CharacterFormat.MaxAmmo);  // jammed at full (0xE3)
Check("clears a jam even at full ammo", AmmoFreeze.TopUp(jr), true);
Check("full ammo kept after unjam", jr.GetItemQty(0) & CharacterFormat.InventoryCountMask, CharacterFormat.MaxAmmo);
Check("jam flag cleared at full ammo", jr.GetItemQty(0) & CharacterFormat.InventoryJammedFlag, 0);
Check("nothing to do once full and un-jammed", AmmoFreeze.TopUp(jr), false);
// A jammed weapon above the max: never lower the count, but still drop the jam bit.
jr.SetItem(0, 29, CharacterFormat.InventoryJammedFlag | 120);  // jammed, 120 charges (above the max)
Check("clears a jam above the max", AmmoFreeze.TopUp(jr), true);
Check("above-max count still kept after unjam", jr.GetItemQty(0), 120);
// Bit 7 is cleared uniformly for every ammo-bearing category, not just firing weapons: the game's
// display categories are cosmetic (a fired launcher like the RPG-7 sits under "Thrown / Explosive"),
// so scoping the clear by category could miss a real jam. A clip (id 30, "Ammo") normalises the same
// way — its count naturally sits well under 0x80, so clearing bit 7 is a no-op on real game data.
jr.SetItem(0, 30, CharacterFormat.InventoryJammedFlag | 5);    // 45 clip with bit 7 set
Check("clears bit 7 on a non-weapon ammo item too", AmmoFreeze.TopUp(jr), true);
Check("clip count topped, high bit cleared", jr.GetItemQty(0), CharacterFormat.MaxAmmo);
Console.WriteLine();

Console.WriteLine("Drop-down parsing (pick a name or type a raw id; the separate id box is gone):");
Check("blank = empty slot", ItemCatalog.ParseSelection(""), 0);
Check("bare id", ItemCatalog.ParseSelection("27"), 27);
Check("label form", ItemCatalog.ParseSelection("13  M1911A1 45 pistol"), 13);
Check("known name", ItemCatalog.ParseSelection("Knife"), 4);
Check("name is case-insensitive", ItemCatalog.ParseSelection("rope"), 54);
Check("id past a byte rejected", ItemCatalog.ParseSelection("999"), -1);
Check("gibberish rejected", ItemCatalog.ParseSelection("zzz"), -1);
// A catalog name that begins with a digit must resolve to that item, not to the leading number as a
// raw id (regression: "45 clip" once parsed as id 45 = Crowbar, "7.62mm clip" as id 7 = explosive).
Check("digit-leading name '45 clip' -> item 30", ItemCatalog.ParseSelection("45 clip"), 30);
Check("digit-leading name '7.62mm clip' -> item 31", ItemCatalog.ParseSelection("7.62mm clip"), 31);
Check("digit-leading name is case-insensitive", ItemCatalog.ParseSelection("9MM CLIP"), 32);
Console.WriteLine();

Console.WriteLine("Name encode / decode round-trip (a NUL terminator is always reserved):");
int nameMax = CharacterFormat.NameLength - 1;   // 13; one byte is kept for the terminator
foreach (var name in new[] { "A", "Bo", "Hell Razor", "Snake Vargas", "ThirteenChar!", "FourteenChars!" })
{
    var rec = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    rec.Name = name;
    string expected = name.Length > nameMax ? name[..nameMax] : name;
    Check($"round-trip \"{name}\"", rec.Name, expected);
}
Console.WriteLine();

Console.WriteLine("Occupancy discriminators (reject stray byte runs the scanner would misread as a member):");
Check("2-char name occupied", MakeValid("Bo").IsOccupied, true);
Check("13-char name occupied", MakeValid("ThirteenChar!").IsOccupied, true);
Check("1-char name rejected", MakeValid("A").IsOccupied, false);
// Force the out-of-range value at the byte level: the setters now clamp gender/nationality to the
// locatable ranges (0/1 and 0..4), so assigning 4/79 through them would be clamped, not rejected.
var badGender = MakeValid("Test"); badGender.Bytes[CharacterFormat.OffGender] = 4;
Check("gender 4 rejected", badGender.IsOccupied, false);
var badNat = MakeValid("Test"); badNat.Bytes[CharacterFormat.OffNationality] = 79;
Check("nationality 79 rejected", badNat.IsOccupied, false);
var conOverMax = MakeValid("Test");                                  // CON == MAXCON by construction
conOverMax.Bytes[CharacterFormat.OffCon] = 0xFF;                     // force CON above MAXCON at the byte level
conOverMax.Bytes[CharacterFormat.OffCon + 1] = 0xFF;                 // (the setter itself clamps CON <= MAXCON)
Check("CON > MAXCON rejected", conOverMax.IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Edit safety — writes stay within the ranges the locator treats as a real record:");
var edited = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
edited.Name = "Editor";
edited.SetAttribute(0, 150);   // over-range → clamped so the slot never becomes un-locatable
Check("attribute clamped to MaxAttribute", edited.GetAttribute(0), CharacterFormat.MaxAttribute);
edited.SetAttribute(1, 0);     // under-range → clamped up to 1
Check("attribute clamped to >= 1", edited.GetAttribute(1), 1);
for (int a = 2; a < CharacterFormat.AttributeCount; a++) edited.SetAttribute(a, 10);
edited.MaxCon = 20000;         // over-range → clamped to the plausibility ceiling
Check("MaxCon clamped to MaxPlausibleCon", edited.MaxCon, CharacterFormat.MaxPlausibleCon);
edited.Gender = 4;             // over-range → clamped into the locatable 0/1 range
Check("Gender clamped to 0/1", edited.Gender, 1);
edited.Nationality = 79;       // over-range → clamped into the locatable 0..4 range
Check("Nationality clamped to 0..4", edited.Nationality, 4);
Check("edited record stays occupied", edited.IsOccupied, true);
// IsOccupied and the scanner share one validator (CharacterRecord.IsValidRecord); confirm the
// instance property and the static test agree on the same bytes so they can't drift apart.
Check("IsOccupied matches IsValidRecord (occupied)",
    edited.IsOccupied, CharacterRecord.IsValidRecord(edited.Bytes, 0));
Check("IsValidRecord rejects an empty record",
    CharacterRecord.IsValidRecord(new byte[CharacterFormat.RecordSize], 0), false);
Console.WriteLine();

Console.WriteLine("IsOccupied (occupied slots pack from 0; the rest are empty):");
Check("occupied slot 0", new CharacterRecord(roster, 0).IsOccupied, true);
Check("occupied slot 3", new CharacterRecord(roster, 3 * CharacterFormat.RecordSize).IsOccupied, true);
Check("empty slot 4", new CharacterRecord(roster, 4 * CharacterFormat.RecordSize).IsOccupied, false);
Check("empty (0x00) record", new CharacterRecord(new byte[CharacterFormat.RecordSize]).IsOccupied, false);
Check("empty (0xFF) record", new CharacterRecord(FilledWith(0xFF)).IsOccupied, false);
Console.WriteLine();

Console.WriteLine("Record geometry:");
Check("skill block precedes inventory",
    CharacterFormat.OffSkills + CharacterFormat.SkillBlockBytes <= CharacterFormat.OffInventory, true);
Check("inventory block fits the record",
    CharacterFormat.OffInventory + CharacterFormat.ItemBlockBytes <= CharacterFormat.RecordSize, true);
Console.WriteLine();

Console.WriteLine("Roll target text binding (an emptied minimum box must clear the target to null):");
var lck = new RollStatViewModel("LCK", CreationScanner.MaxAttr);
lck.MinimumText = "15";
Check("typed 15 -> Minimum 15", lck.Minimum, 15);
lck.MinimumText = "";
Check("cleared box -> Minimum null", lck.Minimum, null);
lck.MinimumText = "50";                                        // over the cap
Check("over-cap value clamps to MaxAttr", lck.Minimum, CreationScanner.MaxAttr);
Check("MinimumText reflects the clamp", lck.MinimumText, CreationScanner.MaxAttr.ToString());
lck.MinimumText = "abc";
Check("non-numeric text -> Minimum null", lck.Minimum, null);
lck.Minimum = 12;
Check("setting Minimum updates MinimumText", lck.MinimumText, "12");
Console.WriteLine();

Console.WriteLine("Capture text bindings (the on-screen boxes clear to null the same way the minimums do):");
var cap = new RollStatViewModel("STR", CreationScanner.MaxAttr);
cap.CapturedText = "12";
Check("typed captured 12 -> Captured 12", cap.Captured, 12);
cap.CapturedText = "";                         // clearing must null it (not linger like a raw int? binding)
Check("cleared captured box -> Captured null", cap.Captured, null);
cap.CapturedText = "xyz";
Check("non-numeric captured -> Captured null", cap.Captured, null);
cap.Captured = 9;
Check("setting Captured updates CapturedText", cap.CapturedText, "9");
var svm = new CharacterRollerViewModel(() => null, () => null, _ => { });
svm.CapturedSkpText = "4";
Check("typed SKP 4 -> CapturedSkp 4", svm.CapturedSkp, 4);
svm.CapturedSkpText = "";
Check("cleared SKP box -> CapturedSkp null", svm.CapturedSkp, null);
Console.WriteLine();

Console.WriteLine("Attribute total display (sum of the seven attributes; excludes SKP and MAXCON):");
var roller = new CharacterRollerViewModel(() => null, () => null, _ => { });
Check("captured total blank until all seven entered", roller.CapturedTotalText, "—");
int[] roll = { 12, 4, 12, 6, 10, 11, 7 };   // sums to 62
for (int i = 0; i < roller.Stats.Count; i++) roller.Stats[i].Captured = roll[i];
roller.MaxCon.Captured = 29;                 // MAXCON must NOT count toward the attribute total
roller.CapturedSkp = 4;                       // SKP must NOT count either
Check("captured total sums only the seven attributes", roller.CapturedTotalText, "62");
Check("live total blank until locked", roller.LiveTotalText, "—");
for (int i = 0; i < roller.Stats.Count; i++) roller.Stats[i].Live = roll[i];
Check("live total sums the seven live attributes", roller.LiveTotalText, "62");
roller.Stats[0].Captured = null;              // clearing one box drops the captured total back to "—"
Check("captured total blank again if a box is cleared", roller.CapturedTotalText, "—");
Console.WriteLine();

Console.WriteLine("Total-points target (an optional minimum on the attribute total):");
var tvm = new CharacterRollerViewModel(() => null, () => null, _ => { });
Check("total target blank by default", tvm.TotalMinimumText, "");
tvm.TotalMinimumText = "80";
Check("typed 80 -> TotalMinimum 80", tvm.TotalMinimum, 80);
Check("criteria lists the total target", tvm.CriteriaText.Contains("total ≥ 80"), true);
tvm.TotalMinimumText = "";                     // clearing the box must drop the target (the nullable-int trap)
Check("cleared box -> TotalMinimum null", tvm.TotalMinimum, null);
Check("criteria drops the total once cleared", tvm.CriteriaText.Contains("total"), false);
tvm.TotalMinimumText = (7 * CreationScanner.MaxAttr + 50).ToString();
Check("over-cap total clamps to 7 * MaxAttr", tvm.TotalMinimum, 7 * CreationScanner.MaxAttr);
tvm.Stats[1].Minimum = 15;                     // set a per-stat + total target, then Clear all targets
tvm.TotalMinimum = 80;
tvm.ClearMinimumsCommand.Execute(null);
Check("Clear all targets clears the total", tvm.TotalMinimum, null);
Check("Clear all targets clears the per-stat minimum too", tvm.Stats[1].Minimum, null);
// A MAXCON minimum persists like the others and stays clearable even when MAXCON isn't tracked.
tvm.MaxCon.Minimum = 30;
Check("a MAXCON minimum alone enables 'Clear all targets'", tvm.ClearMinimumsCommand.CanExecute(null), true);
tvm.ClearMinimumsCommand.Execute(null);
Check("Clear all targets clears the MAXCON minimum", tvm.MaxCon.Minimum, null);
Console.WriteLine();

Console.WriteLine("Roll odds (each attribute modelled as fair 3d6; per-stat and total combined):");
CheckClose("P(3d6 >= 15) = 20/216", RollOdds.PAtLeast(15), 20.0 / 216, 1e-12);
CheckClose("P(3d6 >= 3) = 1", RollOdds.PAtLeast(3), 1.0, 1e-12);
CheckClose("P(3d6 >= 19) = 0", RollOdds.PAtLeast(19), 0.0, 1e-12);
CheckClose("no constraints -> 1", RollOdds.PMeetsTarget(new int[7], 0), 1.0, 1e-9);
CheckClose("total >= 21 (min possible) always met", RollOdds.PMeetsTarget(new int[7], 21), 1.0, 1e-9);
CheckClose("three stats >= 15 -> P(>=15)^3", RollOdds.PMeetsTarget(new[] { 0, 15, 0, 0, 15, 15, 0 }, 0),
    Math.Pow(20.0 / 216, 3), 1e-9);
Check("per-stat min above 18 is impossible", RollOdds.PMeetsTarget(new[] { 19, 0, 0, 0, 0, 0, 0 }, 0), 0.0);
Check("total above 126 is impossible", RollOdds.PMeetsTarget(new int[7], 127), 0.0);
Check("higher total floor is never more likely",
    RollOdds.PMeetsTarget(new int[7], 90) <= RollOdds.PMeetsTarget(new int[7], 80), true);
Check("adding a total floor never raises the odds",
    RollOdds.PMeetsTarget(new[] { 15, 0, 0, 0, 0, 0, 0 }, 80) <= RollOdds.PMeetsTarget(new[] { 15, 0, 0, 0, 0, 0, 0 }, 0), true);
Console.WriteLine();

Console.WriteLine("Odds readout shown in the target section:");
var ovm = new CharacterRollerViewModel(() => null, () => null, _ => { });
Check("no minimums -> every roll qualifies", ovm.OddsText.Contains("every roll qualifies"), true);
ovm.Stats[1].Minimum = 15; ovm.Stats[4].Minimum = 15; ovm.Stats[5].Minimum = 15;   // IQ, AGL, DEX >= 15
Console.WriteLine($"  e.g. IQ/AGL/DEX >= 15 -> {ovm.OddsText}");
Check("odds cites the 3d6 model", ovm.OddsText.Contains("3d6"), true);
Check("odds gives a 95% roll count", ovm.OddsText.Contains("95% chance"), true);
ovm.Stats[1].Minimum = 19;
Check("impossible per-stat is flagged out of reach", ovm.OddsText.Contains("Out of reach"), true);
Console.WriteLine();

Console.WriteLine("Attribute reference (one entry per attribute, aligned with the record order):");
Check("attribute count", AttributeBook.Attributes.Count, CharacterFormat.AttributeCount);
for (int i = 0; i < AttributeBook.Attributes.Count; i++)
{
    var a = AttributeBook.Attributes[i];
    // The book must line up with CharacterFormat.AttributeNames so tooltips match the row they annotate
    // and ByIndex agrees with CharacterRecord.GetAttribute.
    Check($"attr[{i}] index", a.Index, i);
    Check($"attr[{i}] abbr aligns with record order", a.Abbr, CharacterFormat.AttributeNames[i]);
    Check($"attr[{i}] has a name", a.Name.Length > 0, true);
    Check($"attr[{i}] has role text", a.Role.Length > 0, true);
    Check($"attr[{i}] has in-play text", a.InPlay.Length > 0, true);
    Check($"attr[{i}] description mentions its name", a.Description.Contains(a.Name), true);
}
Check("CHR is Charisma", AttributeBook.ByIndex(6)?.Name, "Charisma");
Check("ByIndex out of range is null", AttributeBook.ByIndex(7), null);
Check("DescriptionOf out of range is empty", AttributeBook.DescriptionOf(-1), "");
Console.WriteLine();

Console.WriteLine("Reference tables:");
Check("skill count", SkillBook.Skills.Count, 35);
Check("skill 1 name", SkillBook.SkillName(1), "Brawling");
Check("skill 35 name", SkillBook.SkillName(35), "Cyborg Tech");
Check("unknown skill name", SkillBook.SkillName(200), "Skill #200");
Check("item 0 name", ItemCatalog.ItemName(0), "(empty)");
Check("item 13 name (decoded from WL.EXE)", ItemCatalog.ItemName(13), "M1911A1 45 pistol");
Check("item 29 name", ItemCatalog.ItemName(29), "Meson cannon");
Check("item 35 name", ItemCatalog.ItemName(35), "Power armor");
Check("item 94 name", ItemCatalog.ItemName(94), "Cash");
Check("catalog covers 91 items + empty", ItemCatalog.Items.Count, 92);
Check("unused id 70 is unknown", ItemCatalog.ItemName(70), "Item #70");
Check("unknown item name", ItemCatalog.ItemName(200), "Item #200");
Check("gender 1 name", CharacterFormat.GenderName(1), "Female");
Check("nationality 2 name", CharacterFormat.NationalityName(2), "Mexican");
Console.WriteLine();

Console.WriteLine("Party-state header offsets (X/Y are adjacent inside the header):");
Check("Party X at 0x08", CharacterFormat.HeaderPartyX, 0x08);
Check("Party Y at 0x09", CharacterFormat.HeaderPartyY, 0x09);
Check("Y immediately follows X", CharacterFormat.HeaderPartyY, CharacterFormat.HeaderPartyX + 1);
Console.WriteLine();

Console.WriteLine("Party-state header plausibility (the live roster has one; a stale copy does not):");
// Two real 256-byte headers cut from dosbox-x-…-112525-175.bin. ValidHeader is the party-state header
// that precedes the live roster (Ranger Center); StaleHeader is the 0x100 bytes before the lingering
// stale copy of [Thrasher, Snake Vargas] ~18 KB earlier — actually the tail of another record, so it
// must be rejected. This is exactly the discriminator PartyLocator ranks on.
const string ValidHeaderB64 =
    "AAECAwQAAAA3PgA3PgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgMEBQYBAAAAAAAAAAAAAgAAAAAAvgAAAAAGAOIZCgAGCl0AKAcAAAEAYQAAggIAAAAAoB6RLjrwAAAEAAAEAAAEAQIGQ7cBm04BOCEA7yYAq98AUBoAAhMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFJhbmdlciBDdHIuIAABAAAA/gAAAAAAAAAAAAAAAAAAAAAAAAAUAAAAAAAAAAAAAA==";
const string StaleHeaderB64 =
    "OzgDMP9RAzOzgAAwLzIzO7OAX4I7sAgAAAMzMzCPMjMwAIC/MjMwAID0AjAjEwMwUxMDMP//oxEwA9IxCIh7MAIyiId8szFCAwh3dMtjAohnAjCSMgh3fMvCMgiH/LPyMoCI+zAiIwgAA1MTAzD//wAAAAAAAAAAAAAAAAAAAAADAgcBCQEUARIBCgEWAQ8BGgEZAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEBIgACAAIAAgACAAIAAgACAANgEsAC0ABAAxADQoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";
byte[] validHeader = Convert.FromBase64String(ValidHeaderB64);
byte[] staleHeader = Convert.FromBase64String(StaleHeaderB64);
Check("live Ranger Center header is plausible", PartyHeader.IsPlausible(validHeader), true);
Check("  live header map name", MapBook.MapName(validHeader), "Ranger Ctr.");
Check("  live header X = 55", validHeader[CharacterFormat.HeaderPartyX], (byte)55);
Check("  live header Y = 62", validHeader[CharacterFormat.HeaderPartyY], (byte)62);
Check("stale copy's would-be header is rejected", PartyHeader.IsPlausible(staleHeader), false);
Check("  stale would-be X is out of range", staleHeader[CharacterFormat.HeaderPartyX] >= PartyHeader.MapCoordinateCeiling, true);
// Synthetic edge cases isolate each rejection reason.
Check("all-zero header rejected (blank map name)", PartyHeader.IsPlausible(new byte[CharacterFormat.PartyHeaderSize]), false);
var oobX = (byte[])validHeader.Clone(); oobX[CharacterFormat.HeaderPartyX] = 200;
Check("out-of-range X rejected", PartyHeader.IsPlausible(oobX), false);
var oobY = (byte[])validHeader.Clone(); oobY[CharacterFormat.HeaderPartyY] = (byte)PartyHeader.MapCoordinateCeiling;
Check("Y at the ceiling rejected", PartyHeader.IsPlausible(oobY), false);
var badMarch = (byte[])validHeader.Clone(); badMarch[0] = (byte)CharacterFormat.MaxSlots;
Check("marching byte >= MaxSlots rejected", PartyHeader.IsPlausible(badMarch), false);
var ctrlName = (byte[])validHeader.Clone(); ctrlName[CharacterFormat.HeaderMapName] = 0x01;
Check("non-printable map name rejected", PartyHeader.IsPlausible(ctrlName), false);
var spaceName = (byte[])validHeader.Clone();
for (int i = 0; i < CharacterFormat.MapNameLength; i++) spaceName[CharacterFormat.HeaderMapName + i] = 0x20;
Check("all-space map name rejected", PartyHeader.IsPlausible(spaceName), false);
Console.WriteLine();

Console.WriteLine("Roster ranking (header beats headerless; among header-backed the higher address wins):");
// Bug 1: a live 1-member party (CHRISTOPHER, header-backed) lost to a lingering headerless 2-member
// stale copy (Thrasher + Snake Vargas) under a member-count-only rule. Header-preference fixes it.
Check("header-backed 1-member beats headerless 2-member", PartyLocator.Outranks(true, 1, 0x1000, false, 2, 0x9000), true);
Check("headerless 2-member never beats header-backed 1-member", PartyLocator.Outranks(false, 2, 0x9000, true, 1, 0x1000), false);
Check("both headerless: more members wins", PartyLocator.Outranks(false, 2, 0x1000, false, 1, 0x2000), true);
// Bug 2: the always-loaded 4-member pre-made template (lower address, valid header) outvoted the live
// CHRISTOPHER party (higher address, valid header) on member count. Higher address must win.
Check("live party (higher addr) beats template (lower addr, more members)",
    PartyLocator.Outranks(true, 1, 0x422C1, true, 4, 0x3D890), true);
Check("template (lower addr, more members) never beats live party (higher addr)",
    PartyLocator.Outranks(true, 4, 0x3D890, true, 1, 0x422C1), false);
Check("among header-backed, member count does not override address",
    PartyLocator.Outranks(true, 7, 0x1000, true, 1, 0x2000), false);
Check("equal address: not better (first found wins)", PartyLocator.Outranks(true, 3, 0x1000, true, 3, 0x1000), false);
Console.WriteLine();

Console.WriteLine("Create-screen roll scanner (locate the temporary create buffer by its attribute bytes):");
// A record-shaped scratch buffer holding the user's create-screen roll: STR 12 IQ 4 LK 12 SP 6 AGL 10
// DEX 11 CHR 7, MAXCON 29, SKP 4 (== IQ on a fresh roll). The create buffer is a character record, so
// the scanner's offsets are exercised against the real record layout.
int[] rollAttrs = { 12, 4, 12, 6, 10, 11, 7 };
var probe = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
probe.Name = "Roller";
for (int a = 0; a < CharacterFormat.AttributeCount; a++) probe.SetAttribute(a, rollAttrs[a]);
probe.MaxCon = 29; probe.Con = 29; probe.SkillPoints = 4;
byte[] probeBytes = probe.Bytes;
byte[] attrBytes = rollAttrs.Select(x => (byte)x).ToArray();

var hits = CreationScanner.FindInBuffer(probeBytes, attrBytes).ToList();
Check("attribute run found once", hits.Count, 1);
Check("attribute run at OffAttributes", hits.Count > 0 ? hits[0] : -1, CharacterFormat.OffAttributes);
Check("MAXCON offset from attr base", CreationScanner.AttrToMaxCon, CharacterFormat.OffMaxCon - CharacterFormat.OffAttributes);
Check("SKP offset from attr base", CreationScanner.AttrToSkp, CharacterFormat.OffSkillPoints - CharacterFormat.OffAttributes);
Check("structural: matching MAXCON + SKP confirms", CreationScanner.IsStructural(probeBytes, CharacterFormat.OffAttributes, 29, 4), true);
Check("structural: wrong MAXCON rejected", CreationScanner.IsStructural(probeBytes, CharacterFormat.OffAttributes, 30, 4), false);
Check("structural: wrong SKP rejected", CreationScanner.IsStructural(probeBytes, CharacterFormat.OffAttributes, 29, 5), false);
Check("structural: out-of-bounds window rejected", CreationScanner.IsStructural(probeBytes, CharacterFormat.RecordSize - 2, 29, 4), false);
Check("InRange accepts a plausible roll", CreationScanner.InRange(rollAttrs), true);
Check("InRange rejects a zero byte", CreationScanner.InRange(new[] { 12, 0, 12, 6, 10, 11, 7 }), false);
Check("InRange rejects an over-range byte", CreationScanner.InRange(new[] { 12, 4, 12, 6, 10, 11, 200 }), false);
Console.WriteLine();

Console.WriteLine("Roll tally (per-stat and total averages; consecutive duplicate reads are dropped):");
var tally = new RollTally(CharacterFormat.AttributeCount);
Check("first roll accepted", tally.Add(new[] { 12, 4, 12, 6, 10, 11, 7 }), true);      // total 62
Check("duplicate read dropped", tally.Add(new[] { 12, 4, 12, 6, 10, 11, 7 }), false);
Check("changed roll accepted", tally.Add(new[] { 14, 6, 12, 6, 10, 11, 7 }), true);    // total 66
var snap = tally.Snapshot();
Check("tally counted two fresh rolls", snap.Count, 2);
Check("STR average of 12 and 14", snap.StatMean[0], 13.0);
Check("STR min", snap.StatMin[0], 12);
Check("STR max", snap.StatMax[0], 14);
Check("total min (62)", snap.TotalMin, 62);
Check("total max (66)", snap.TotalMax, 66);
Check("total mean (64.0)", snap.TotalMean, 64.0);
Console.WriteLine();

Console.WriteLine(failures == 0
    ? "ALL CHECKS PASSED — the 256-byte record layout decodes the sample party correctly."
    : $"{failures} CHECK(S) FAILED.");
return failures == 0 ? 0 : 1;

void CheckCharacter(int slot, string name, int str, int iq, int lck, int spd, int agl, int dex, int chr,
    int maxCon, int gender, int nationality, int level, int skp, string rank)
{
    var rec = new CharacterRecord(roster, slot * CharacterFormat.RecordSize);
    Console.WriteLine($"Slot {slot}: {rec.Name}");
    Check("name", rec.Name, name);
    Check("STR", rec.GetAttribute(0), str);
    Check("IQ", rec.GetAttribute(1), iq);
    Check("LCK", rec.GetAttribute(2), lck);
    Check("SPD", rec.GetAttribute(3), spd);
    Check("AGL", rec.GetAttribute(4), agl);
    Check("DEX", rec.GetAttribute(5), dex);
    Check("CHR", rec.GetAttribute(6), chr);
    Check("MAXCON", rec.MaxCon, maxCon);
    Check("CON = MAXCON at full", rec.Con, maxCon);
    Check("gender", rec.Gender, gender);
    Check("nationality", rec.Nationality, nationality);
    Check("level", rec.Level, level);
    Check("skill points", rec.SkillPoints, skp);
    Check("rank", rec.Rank, rank);
    Check("is occupied", rec.IsOccupied, true);
    Console.WriteLine();
}

byte[] FilledWith(byte b)
{
    var a = new byte[CharacterFormat.RecordSize];
    Array.Fill(a, b);
    return a;
}

// Builds a minimally-valid occupied record (letter-leading name, in-range attributes, CON==MAXCON,
// gender/nationality 0) for the occupancy-discriminator checks.
CharacterRecord MakeValid(string name)
{
    var r = new CharacterRecord(new byte[CharacterFormat.RecordSize]);
    r.Name = name;
    for (int a = 0; a < CharacterFormat.AttributeCount; a++) r.SetAttribute(a, 10);
    r.MaxCon = 30;
    r.Con = 30;
    return r;
}

void Check<T>(string label, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-24} = {actual}" + (ok ? "" : $"   (expected {expected})"));
}

void CheckClose(string label, double actual, double expected, double tol)
{
    bool ok = Math.Abs(actual - expected) <= tol;
    if (!ok) failures++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-24} = {actual:0.#######}" + (ok ? "" : $"   (expected {expected:0.#######})"));
}
