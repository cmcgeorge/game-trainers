using System.Diagnostics;
using SotSAgeTrainer.Core;

// Live proof that the trainer's Core finds EVERY copy of the state block — including the resident/live
// block the game actually displays from — using the same discovery the WPF app uses. Non-destructive:
// stat/age tests flip a byte and immediately restore it.
//
//   Verify               -> discover copies + roster (read only)
//   Verify --roster      -> roster of every copy
//   Verify --stat-test   -> flip a protagonist stat byte in every copy and restore
//   Verify --age-test    -> flip protagonist age (Youth<->Old) in every copy and restore

bool doRoster = args.Contains("--roster");
bool doStatTest = args.Contains("--stat-test");
bool doAgeTest = args.Contains("--age-test");
bool doHunt = args.Contains("--hunt");
// --find v1,v2,...  : locate on-screen numbers in live memory and report clusters (the player's live record)
string? findArg = args.SkipWhile(a => a != "--find").Skip(1).FirstOrDefault();

static string ProtName(uint p) => (p & 0x100) != 0 ? "guard" : p switch
{ 0x01 => "no", 0x02 => "r", 0x04 => "rw", 0x08 => "wc", 0x20 => "rx", 0x40 => "rwx", 0x80 => "rwxc", _ => "0x" + p.ToString("X") };
static string TypeName(uint t) => t switch { 0x1000000 => "image", 0x40000 => "mapped", 0x20000 => "private", _ => "0x" + t.ToString("X") };
static GameBlock? TryParseAt(ProcessMemory mem, IntPtr baseAddr)
{
    if (baseAddr == IntPtr.Zero || (long)baseAddr < 0) return null;
    var b = new byte[AgeTrainer.BlockReadLen];
    return mem.ReadInto(baseAddr, b, b.Length) ? AgeTrainer.ParseBlock(baseAddr, b) : null;
}

// --diff A B : compare two --snap files and report every changed byte as record#+offset.
int di = Array.IndexOf(args, "--diff");
if (di >= 0 && di + 2 < args.Length)
{
    var a = File.ReadAllBytes(args[di + 1]);
    var b = File.ReadAllBytes(args[di + 2]);
    int n = Math.Min(a.Length, b.Length);
    Console.WriteLine($"diff {args[di + 1]} vs {args[di + 2]} ({n} bytes):");
    int changes = 0;
    for (int o = 0; o < n; o++)
    {
        if (a[o] == b[o]) continue;
        changes++;
        string where = o >= 0x6F && o < 0x2AF
            ? $"rec{(o - 0x6F) / 0x60} +0x{(o - 0x6F) % 0x60:X2}"
            : "header/nametable";
        Console.WriteLine($"  block+0x{o:X3} ({where}): {a[o]} -> {b[o]}");
    }
    Console.WriteLine($"total changed bytes: {changes}");
    return 0;
}

// --engine : drive the real AgeTrainer engine end-to-end (find process, locate live arrays, edit).
if (args.Contains("--engine"))
{
    var t = new AgeTrainer();
    t.Log += l => Console.WriteLine("  [engine] " + l);
    t.Start();
    System.Threading.Thread.Sleep(1600);            // let it attach + locate
    Console.WriteLine("  BoostArmy -> " + t.BoostArmy());
    Console.WriteLine("  MaxProtagonist changed words -> " + t.MaxProtagonist());
    t.Target = LifeStage.Youth;
    Console.WriteLine("  SetAgeOnce(Youth) -> " + t.SetAgeOnce());
    System.Threading.Thread.Sleep(300);
    t.Dispose();
    return 0;
}

var procs = Process.GetProcesses()
    .Where(p => { try { return p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase); } catch { return false; } })
    .ToList();

if (procs.Count == 0)
{
    Console.WriteLine("No DOSBox process found. Launch the game and load a save, then re-run.");
    return 2;
}

int totalCopies = 0;
foreach (var p in procs)
{
    Console.WriteLine($"== {p.ProcessName} (PID {p.Id}) ==");
    using var mem = ProcessMemory.TryOpen(p.Id);
    if (mem == null) { Console.WriteLine("  Could not open process (same user / admin?)."); continue; }

    var d = AgeTrainer.DiscoverStateBlocks(mem);

    // --arrays : run the trainer's real player-record discovery and print each array's roster.
    if (args.Contains("--arrays"))
    {
        var found = AgeTrainer.FindPlayerRecords(mem);
        Console.WriteLine($"  FindPlayerRecords -> {found.Count} arrays (rec0 = player):");
        foreach (var b in found)
        {
            var block = AgeTrainer.ParseArray(mem, b);
            var prot = block?.Protagonist;
            mem.TryQuery(b, out uint pr, out uint ty, out _, out _, out _);
            Console.WriteLine($"    rec0=0x{(long)b:X} [{ProtName(pr)}/{TypeName(ty)}] recs={block?.Records.Count ?? 0} " +
                              $"you: age={prot?.AgeText} army={prot?.Army} stats=[{prot?.StatsText}]");
        }
        continue;
    }

    // --sig <hexbytes> : scan writable memory for a byte pattern; group hits at 0x60 stride into arrays.
    int sg = Array.IndexOf(args, "--sig");
    if (sg >= 0 && sg + 1 < args.Length)
    {
        string hex = args[sg + 1];
        var pat = new byte[hex.Length / 2];
        for (int i = 0; i < pat.Length; i++) pat[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        var hits = mem.ScanAll(pat, writableOnly: true, maxMatches: 100000).Select(h => (long)h).OrderBy(x => x).ToList();
        Console.WriteLine($"  pattern {hex}: {hits.Count} hits");
        // group into runs where consecutive hits differ by 0x60 (a record array)
        var arrays = new List<(long start, int n)>();
        for (int i = 0; i < hits.Count;)
        {
            int n = 1;
            while (i + n < hits.Count && hits[i + n] - hits[i + n - 1] == 0x60) n++;
            if (n >= 2) arrays.Add((hits[i], n));
            i += n;
        }
        Console.WriteLine($"  arrays (>=2 records at 0x60 stride): {arrays.Count}");
        foreach (var (start, n) in arrays)
        {
            long rec0 = start - 0x5A; // tail sits at rec+0x5A
            mem.TryQuery((IntPtr)rec0, out uint pr, out uint ty, out _, out _, out _);
            var bb = new byte[0x48];
            mem.ReadInto((IntPtr)rec0, bb, bb.Length);
            int army = bb[0x40] | (bb[0x41] << 8);
            Console.WriteLine($"    rec0=0x{rec0:X} records={n} [{ProtName(pr)}/{TypeName(ty)}] rec0.age={bb[0x33]} rec0.army={army}");
        }
        continue;
    }

    // --poke <hexaddr> <value> <width> : write a value (for validation). width 1 or 2.
    int po = Array.IndexOf(args, "--poke");
    if (po >= 0 && po + 3 < args.Length)
    {
        long addr = Convert.ToInt64(args[po + 1], 16);
        int value = int.Parse(args[po + 2]);
        int width = int.Parse(args[po + 3]);
        bool ok = width == 2
            ? mem.WriteByte((IntPtr)addr, (byte)(value & 0xFF)) & mem.WriteByte((IntPtr)(addr + 1), (byte)((value >> 8) & 0xFF))
            : mem.WriteByte((IntPtr)addr, (byte)value);
        mem.TryReadByte((IntPtr)addr, out byte rb);
        Console.WriteLine($"  poke 0x{addr:X} = {value} (u{width}): {(ok ? "ok" : "FAIL")}, reread byte0={rb}");
        continue;
    }

    // --recs <hexbase> <count> : dump N records of 0x60 bytes as u16le words, aligned for comparison.
    int rc = Array.IndexOf(args, "--recs");
    if (rc >= 0 && rc + 2 < args.Length)
    {
        long baseAddr = Convert.ToInt64(args[rc + 1], 16);
        int count = int.Parse(args[rc + 2]);
        Console.Write("  offset:");
        for (int w = 0; w < 0x30; w++) Console.Write($" {w * 2:X2}");
        Console.WriteLine();
        for (int r = 0; r < count; r++)
        {
            var bb = new byte[0x60];
            if (!mem.ReadInto((IntPtr)(baseAddr + r * 0x60), bb, bb.Length)) { Console.WriteLine($"  rec{r}: read fail"); continue; }
            Console.Write($"  rec{r} 0x{baseAddr + r * 0x60:X}:");
            for (int w = 0; w < 0x30; w++) Console.Write($" {bb[w * 2] | (bb[w * 2 + 1] << 8),3}");
            Console.WriteLine();
        }
        continue;
    }

    // --peek <hexaddr> <len> : dump raw memory at an absolute address (decimal, flags 11/33/13/17).
    int pk = Array.IndexOf(args, "--peek");
    if (pk >= 0 && pk + 2 < args.Length)
    {
        long addr = Convert.ToInt64(args[pk + 1], 16);
        int len = int.Parse(args[pk + 2]);
        var bb = new byte[len];
        if (!mem.ReadInto((IntPtr)addr, bb, len)) { Console.WriteLine("  read failed"); continue; }
        var flag = new HashSet<int> { 11, 33, 13, 17 };
        for (int off = 0; off < len; off += 16)
        {
            var row = new System.Text.StringBuilder($"  0x{addr + off:X}: ");
            for (int k = 0; k < 16 && off + k < len; k++)
            {
                int v = bb[off + k];
                row.Append(flag.Contains(v) ? $"[{v,3}]" : $" {v,3} ");
            }
            Console.WriteLine(row.ToString());
        }
        continue;
    }

    // --track <value> [--reset] : iterative known-value search. First call scans writable memory for
    // every u8/u16 == value; later calls keep only candidates whose current value matches. Converges
    // on the field (army count, etc.) as the user changes it in-game.
    int ti = Array.IndexOf(args, "--track");
    if (ti >= 0 && ti + 1 < args.Length)
    {
        string file = Path.Combine(Path.GetTempPath(), "sots_track.txt");
        if (args.Contains("--reset") && File.Exists(file)) File.Delete(file);
        int val = int.Parse(args[ti + 1]);

        if (!File.Exists(file))
        {
            var cands = new List<string>();
            foreach (var h in mem.ScanAll(new[] { (byte)val }, writableOnly: true, maxMatches: 20_000_000))
                cands.Add($"{(long)h:X} 1");
            if (val <= 65535)
                foreach (var h in mem.ScanAll(BitConverter.GetBytes((ushort)val), writableOnly: true, maxMatches: 20_000_000))
                    cands.Add($"{(long)h:X} 2");
            File.WriteAllLines(file, cands);
            Console.WriteLine($"  track init: {cands.Count} candidates == {val}. Change it in-game, then run --track <new>.");
        }
        else
        {
            var kept = new List<string>();
            foreach (var line in File.ReadAllLines(file))
            {
                var pp = line.Split(' ');
                long addr = Convert.ToInt64(pp[0], 16); int w = int.Parse(pp[1]);
                var bb = new byte[w];
                if (mem.ReadInto((IntPtr)addr, bb, w))
                {
                    int cur = w == 1 ? bb[0] : BitConverter.ToUInt16(bb, 0);
                    if (cur == val) kept.Add(line);
                }
            }
            File.WriteAllLines(file, kept);
            Console.WriteLine($"  track narrow: {kept.Count} candidates now == {val}.");

            // Spatial cross-check: the real player-army cell sits in the character array, so the
            // rivals' armies (33/13/17) should appear near it. Flag candidates with all three nearby.
            if (kept.Count is > 0 and <= 200000)
            {
                var near = new List<long>();
                var win = new byte[0x180];
                foreach (var line in kept)
                {
                    long addr = Convert.ToInt64(line.Split(' ')[0], 16);
                    if (!mem.ReadInto((IntPtr)(addr - 0xC0), win, win.Length)) continue;
                    bool h33 = false, h13 = false, h17 = false;
                    foreach (var b in win) { if (b == 33) h33 = true; else if (b == 13) h13 = true; else if (b == 17) h17 = true; }
                    if (h33 && h13 && h17) near.Add(addr);
                }
                Console.WriteLine($"  …of which {near.Count} have rival armies 33/13/17 within ±0xC0 (likely the army array):");
                foreach (var addr in near.Take(40))
                {
                    mem.TryQuery((IntPtr)addr, out uint pr, out uint ty, out _, out _, out _);
                    Console.WriteLine($"    0x{addr:X} [{ProtName(pr)}/{TypeName(ty)}]");
                }
            }
            else if (kept.Count <= 100)
                foreach (var line in kept)
                {
                    var pp = line.Split(' '); long addr = Convert.ToInt64(pp[0], 16);
                    mem.TryQuery((IntPtr)addr, out uint pr, out uint ty, out _, out _, out _);
                    Console.WriteLine($"    0x{addr:X} u{pp[1]} [{ProtName(pr)}/{TypeName(ty)}]");
                }
        }
        continue;
    }

    // --snap PATH : save the LIVE (dense) copy's 0x600 bytes to a file for change-and-diff.
    string? snapPath = args.SkipWhile(a => a != "--snap").Skip(1).FirstOrDefault();
    if (snapPath != null)
    {
        foreach (IntPtr hit in mem.ScanAll(d.Fragment!, writableOnly: true, maxMatches: 32))
        {
            IntPtr baseAddr = (IntPtr)((long)hit - d.FragOffset);
            var bb = new byte[AgeTrainer.BlockReadLen];
            if (!mem.ReadInto(baseAddr, bb, bb.Length)) continue;
            if (bb[0x6F + 0x3A] == 0x80) continue; // skip the maxed save-image buffers
            File.WriteAllBytes(snapPath, bb);
            Console.WriteLine($"  snapshot of LIVE copy base=0x{(long)baseAddr:X} -> {snapPath} ({bb.Length} bytes)");
            break;
        }
        continue;
    }

    if (args.Contains("--dumpblock"))
    {
        // Dump each copy's first 0x300 bytes as decimal, and flag any byte equal to a known army count.
        var wanted = new HashSet<int> { 4, 33, 13, 17 };
        foreach (IntPtr hit in mem.ScanAll(d.Fragment!, writableOnly: true, maxMatches: 32))
        {
            IntPtr baseAddr = (IntPtr)((long)hit - d.FragOffset);
            var bb = new byte[0x300];
            if (!mem.ReadInto(baseAddr, bb, bb.Length)) continue;
            Console.WriteLine($"\n  === copy base=0x{(long)baseAddr:X} (rec0 stats {(bb[0x6F+0x3A]==0x80?"maxed=save-buf":"real=LIVE?")}) ===");
            for (int off = 0; off < 0x300; off += 16)
            {
                var row = new System.Text.StringBuilder($"  +0x{off:X3}: ");
                for (int k = 0; k < 16; k++)
                {
                    int v = bb[off + k];
                    row.Append(wanted.Contains(v) ? $"[{v,3}]" : $" {v,3} ");
                }
                Console.WriteLine(row.ToString());
            }
        }
        continue;
    }

    if (args.Contains("--solve"))
    {
        // Find leadership/honor offsets in the LIVE record format by matching army = L*H/128 to the
        // known army counts. Scans every copy and every offset pair; reports pairs reproducing the set.
        int[] targetArmies = { 4, 33, 13, 17 }; // Christopher, Ieyasu, Toshiro, Yasuhira
        var frag = d.Fragment;
        if (frag == null) { Console.WriteLine("  no fragment (game loaded?)"); continue; }

        foreach (IntPtr hit in mem.ScanAll(frag, writableOnly: true, maxMatches: 32))
        {
            IntPtr baseAddr = (IntPtr)((long)hit - d.FragOffset);
            var sbuf = new byte[AgeTrainer.BlockReadLen];
            if (!mem.ReadInto(baseAddr, sbuf, sbuf.Length)) continue;

            // records: 0x60 bytes each from 0x6F
            const int rec0 = 0x6F, stride = 0x60, nrec = 6;
            byte B(int r, int o) => sbuf[rec0 + r * stride + o];

            var matches = new List<string>();
            for (int a = 0; a < stride; a++)
            for (int bo = 0; bo < stride; bo++)
            {
                // For each target army, is there a record whose B(r,a)*B(r,bo)/128 ≈ target?
                var used = new HashSet<int>();
                var map = new List<string>();
                bool ok = true;
                foreach (int army in targetArmies)
                {
                    int found = -1;
                    for (int r = 0; r < nrec; r++)
                    {
                        if (used.Contains(r)) continue;
                        int calc = B(r, a) * B(r, bo) / 128;
                        if (Math.Abs(calc - army) <= 1) { found = r; break; }
                    }
                    if (found < 0) { ok = false; break; }
                    used.Add(found); map.Add($"rec{found}(={B(found, a)}*{B(found, bo)}/128={B(found, a) * B(found, bo) / 128})->{army}");
                }
                if (ok) matches.Add($"L=+0x{a:X2} H=+0x{bo:X2}: " + string.Join(" ", map));
            }
            Console.WriteLine($"  base=0x{(long)baseAddr:X}: {matches.Count} offset-pair(s) reproduce armies {string.Join(",", targetArmies)}");
            foreach (var m in matches.Take(12)) Console.WriteLine("    " + m);
        }
        continue;
    }

    if (findArg != null)
    {
        var values = findArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(s => int.Parse(s)).ToList();
        Console.WriteLine($"  searching writable memory for {string.Join(", ", values)} (as u8, u16le, u32le)…");

        var hits = new List<(long addr, int val, int w)>();
        foreach (int v in values)
        {
            if (v is >= 0 and <= 255)
                foreach (var h in mem.ScanAll(new[] { (byte)v }, writableOnly: true, maxMatches: 200000))
                    hits.Add(((long)h, v, 1));
            if (v is >= 0 and <= 65535)
                foreach (var h in mem.ScanAll(BitConverter.GetBytes((ushort)v), writableOnly: true, maxMatches: 200000))
                    hits.Add(((long)h, v, 2));
            foreach (var h in mem.ScanAll(BitConverter.GetBytes((uint)v), writableOnly: true, maxMatches: 200000))
                hits.Add(((long)h, v, 4));
        }
        foreach (int v in values)
            Console.WriteLine($"    value {v}: u8={hits.Count(x => x.val == v && x.w == 1)} u16={hits.Count(x => x.val == v && x.w == 2)} u32={hits.Count(x => x.val == v && x.w == 4)} hits");

        // Cluster: windows holding the most DISTINCT requested values (likely the live record array).
        long window = 0x400;
        int need = values.Count; // require ALL requested values present in the window
        hits.Sort((a, b) => a.addr.CompareTo(b.addr));
        var clusters = new List<(long lo, long hi, int distinct, string detail)>();
        for (int i = 0; i < hits.Count; i++)
        {
            long lo = hits[i].addr;
            var inWin = new List<(long addr, int val, int w)>();
            for (int j = i; j < hits.Count && hits[j].addr < lo + window; j++) inWin.Add(hits[j]);
            int distinct = inWin.Select(x => x.val).Distinct().Count();
            if (distinct >= need)
                clusters.Add((lo, inWin.Max(x => x.addr), distinct,
                    string.Join(" ", inWin.Select(x => $"+{x.addr - lo:X}:{x.val}/u{x.w}"))));
        }
        clusters.Sort((a, b) => b.distinct.CompareTo(a.distinct));
        var shown = new List<(long lo, long hi, int distinct, string detail)>();
        foreach (var c in clusters)
        {
            if (shown.Any(s => c.lo <= s.hi && c.hi >= s.lo)) continue;
            shown.Add(c);
            if (shown.Count > 25) break;
        }
        Console.WriteLine($"  clusters with >= {Math.Min(3, values.Count)} distinct values ({shown.Count}):");
        foreach (var c in shown.OrderBy(s => s.lo))
        {
            mem.TryQuery((IntPtr)c.lo, out uint pr, out uint ty, out _, out _, out _);
            Console.WriteLine($"    @0x{c.lo:X} [{ProtName(pr)}/{TypeName(ty)}] distinct={c.distinct}: {c.detail}");
        }
        continue;
    }

    if (args.Contains("--debug"))
    {
        Console.WriteLine($"  bootstrap name='{d.PlayerName}' fragOffset=0x{d.FragOffset:X} d.Bases={d.Bases.Count}");
        if (d.Fragment != null)
        {
            var hits = mem.ScanAll(d.Fragment, writableOnly: true, maxMatches: 64);
            Console.WriteLine($"  fragment scan hits: {hits.Count}");
            foreach (var hit in hits)
            {
                IntPtr baseAddr = (IntPtr)((long)hit - d.FragOffset);
                var b = new byte[AgeTrainer.BlockReadLen];
                bool read = mem.ReadInto(baseAddr, b, b.Length);
                var blk = read ? AgeTrainer.ParseBlock(baseAddr, b) : null;
                string age = blk?.Protagonist is { } pp ? pp.AgeStage.ToString() : "n/a";
                Console.WriteLine($"    hit=0x{(long)hit:X} base=0x{(long)baseAddr:X} read={read} parsed={(blk != null)} recs={blk?.Records.Count ?? 0} protAge={age}");
            }
        }
        continue;
    }

    if (args.Contains("--cmp") && d.Fragment != null)
    {
        var t = mem.ScanAll(d.Fragment, writableOnly: true, maxMatches: 64);
        var f = mem.ScanAll(d.Fragment, writableOnly: false, maxMatches: 64);
        Console.WriteLine($"  same-process fragment scan: writableOnly=true -> {t.Count} hits, writableOnly=false -> {f.Count} hits");
        Console.WriteLine("   true : " + string.Join(" ", t.Select(x => "0x" + ((long)x).ToString("X"))));
        Console.WriteLine("   false: " + string.Join(" ", f.Select(x => "0x" + ((long)x).ToString("X"))));
        continue;
    }

    if (args.Contains("--regions"))
    {
        long target = 0x1BCAC30;
        int total = 0, writable = 0;
        foreach (var reg in mem.EnumerateRegions())
        {
            total++;
            if (reg.IsWritable) writable++;
            long lo = (long)reg.BaseAddress, hi = lo + reg.Size;
            if (lo <= target && target < hi)
                Console.WriteLine($"  region containing 0x{target:X}: base=0x{lo:X} size=0x{reg.Size:X} protect=0x{reg.Protect:X} ({ProtName(reg.Protect)}) IsWritable={reg.IsWritable}");
        }
        Console.WriteLine($"  regions total={total} writable={writable}");
        continue;
    }

    if (doHunt)
    {
        // Ground truth: scan ALL committed memory (not just writable) for the name and the name-table
        // fragment, reporting each hit's region so we can see every copy and why a scan might skip it.
        Console.WriteLine($"  HUNT — player '{d.PlayerName}', fragment {(d.Fragment == null ? "n/a" : d.Fragment.Length + " bytes")}");
        void Report(string what, byte[] needle, int fragOff)
        {
            foreach (var hit in mem.ScanAll(needle, writableOnly: false, maxMatches: 64))
            {
                IntPtr baseAddr = (IntPtr)((long)hit - fragOff);
                mem.TryQuery(hit, out uint pr, out uint ty, out _, out _, out _);
                var blk = TryParseAt(mem, baseAddr);
                string stats = blk?.Protagonist is { } pp ? $"rec0=[{pp.StatsText}]" : "(no parse)";
                Console.WriteLine($"    {what} hit=0x{(long)hit:X} base=0x{(long)baseAddr:X} [{ProtName(pr)}/{TypeName(ty)}] {stats}");
            }
        }
        if (d.PlayerName.Length >= 4) Report("name", System.Text.Encoding.ASCII.GetBytes(d.PlayerName), d.FragOffset + 0x10);
        if (d.Fragment != null) Report("frag", d.Fragment, d.FragOffset);
        continue;
    }

    if (d.Bases.Count == 0) { Console.WriteLine("  No state block found (is a game loaded via Restore/Continue?)."); continue; }

    long live = d.Bases.Min(b => (long)b); // resident copy sits lowest, in emulated DOS RAM
    Console.WriteLine($"  player '{d.PlayerName}' — {d.Bases.Count} state-block copy/ies:");
    totalCopies += d.Bases.Count;

    var buf = new byte[AgeTrainer.BlockReadLen];
    foreach (var baseAddr in d.Bases.OrderBy(b => (long)b))
    {
        if (!mem.ReadInto(baseAddr, buf, buf.Length)) { Console.WriteLine($"  0x{(long)baseAddr:X}: read failed"); continue; }
        var block = AgeTrainer.ParseBlock(baseAddr, buf);
        if (block?.Protagonist is not { } prot) { Console.WriteLine($"  0x{(long)baseAddr:X}: parse failed"); continue; }
        bool isLive = (long)baseAddr == live;
        string tag = isLive ? "LIVE" : "buf ";

        Console.WriteLine($"  [{tag}] base=0x{(long)baseAddr:X}  age={prot.AgeText}  YOUR stats=[{prot.StatsText}]");

        if (doRoster)
            foreach (var r in block.Records)
                Console.WriteLine($"          #{r.Index} {r.RoleText,-5} fam={r.FamilyIndex,3} age={r.AgeText,-12} stats=[{r.StatsText}]");

        if (doStatTest)
        {
            IntPtr statAddr = (IntPtr)((long)prot.Address + 0x3A); // first byte of the editable span
            mem.TryReadByte(statAddr, out byte orig);
            byte probe = (byte)(orig == 0x80 ? 0x7F : 0x80);
            mem.WriteByte(statAddr, probe);
            mem.TryReadByte(statAddr, out byte after);
            mem.WriteByte(statAddr, orig);
            mem.TryReadByte(statAddr, out byte restored);
            Console.WriteLine($"          stat-test @0x{(long)statAddr:X}: orig={orig} wrote={probe} read={after} restored={restored} " +
                              $"=> {(after == probe && restored == orig ? "PASS" : "FAIL")}");
        }

        if (doAgeTest)
        {
            mem.TryReadByte(prot.AgeAddress, out byte orig);
            byte probe = (byte)(orig == (byte)LifeStage.Old ? (byte)LifeStage.Youth : (byte)LifeStage.Old);
            mem.WriteByte(prot.AgeAddress, probe);
            mem.TryReadByte(prot.AgeAddress, out byte after);
            mem.WriteByte(prot.AgeAddress, orig);
            mem.TryReadByte(prot.AgeAddress, out byte restored);
            Console.WriteLine($"          age-test @0x{(long)prot.AgeAddress:X}: orig={orig} wrote={probe} read={after} restored={restored} " +
                              $"=> {(after == probe && restored == orig ? "PASS" : "FAIL")}");
        }
    }
}

Console.WriteLine($"\nTotal state-block copies located: {totalCopies}");
Console.WriteLine(totalCopies > 0 ? "PASS — the trainer can locate the live block." : "No copies located.");
return totalCopies > 0 ? 0 : 1;
