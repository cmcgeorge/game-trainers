using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SotSAgeTrainer.Core;

/// <summary>
/// Live trainer for Sword of the Samurai running under DOSBox. The running game displays character
/// stats from an in-memory character array (word-scaled, 0x60-byte records) that is SEPARATE from the
/// on-disk save format — editing the save buffers does nothing visible, which is why earlier versions
/// appeared to do nothing. We locate the live array by its record tail "01 00 01 00 12 00" (rec+0x5A),
/// walk back to record 0 (the player), and edit the live fields:
///   • age  : byte  rec+0x33  (0 = Youth — verified in-game)
///   • army : word  rec+0x40  (loyal warriors — verified in-game by writing it and seeing it change)
///   • stats: words rec+0x3A … rec+0x58 (0–128 scaled cluster; max → 128, cripple → 1)
///   • family index: byte rec+0x32 (records sharing the player's value are kin)
/// All copies found are edited, so whichever the game reads is covered.
/// </summary>
public sealed class AgeTrainer : IDisposable
{
    // ---- live character-record layout ----
    private const int Stride = 0x60;
    private static readonly byte[] TailSig = { 0x01, 0x00, 0x01, 0x00, 0x12, 0x00 }; // at rec+0x5A
    private const int TailOff = 0x5A;
    private const int AgeOff = 0x33;      // byte
    private const int FamilyOff = 0x32;   // byte
    private const int ArmyOff = 0x40;     // word
    // Editable attribute/army cluster. Starts at 0x3C to preserve the ~31 marker word at 0x3A, which
    // is part of the record signature (Qualifies) and not an attribute — maxing it corrupts discovery.
    private const int StatLo = 0x3C, StatHi = 0x58; // word cluster
    private const int MaxRecords = 16;
    private const int MaxStat = 128, MinStat = 1, BigArmy = 250;

    private const int TickMs = 150;

    // ---- legacy save-format symbols kept for the Verify diagnostic harness ----
    public static readonly byte[] Signature = Encoding.ASCII.GetBytes("MGRAPHIC.EXE\0RSOUND.SAM");
    public const int AgeOffsetFromSignature = 0x7F;
    public const int BlockReadLen = 0x600;

    private readonly object _gate = new();
    private LifeStage _target = LifeStage.Youth;
    private bool _freezing;
    private volatile bool _running;

    private Thread? _thread;
    private ProcessMemory? _mem;
    private int _pid;
    private List<IntPtr> _bases = new();

    public event Action<TrainerStatus>? StatusChanged;
    public event Action<string>? Log;

    public LifeStage Target
    {
        get { lock (_gate) return _target; }
        set { lock (_gate) _target = value; }
    }

    public bool Freezing
    {
        get { lock (_gate) return _freezing; }
        set
        {
            lock (_gate) _freezing = value;
            Log?.Invoke(value ? $"Freeze ON → holding age at {LifeStages.Label((byte)Target)}." : "Freeze OFF.");
        }
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "SotS-AgeTrainer" };
        _thread.Start();
    }

    // ---- one-shot actions -----------------------------------------------------------------------

    public bool SetAgeOnce() => ForEachArray("Set age", (b, block) =>
    {
        if (block.Protagonist is { } p) _mem!.WriteByte(p.AgeAddress, (byte)_target);
    });

    public int MaxProtagonist() => EditRecords(r => r.Role == CharacterRole.You, MaxStat, "Maxed YOUR stats");
    public int CrippleRivals() => EditRecords(r => r.Role == CharacterRole.Rival, MinStat, "Crippled rivals");
    public int MaxRecord(int index) => EditRecords(r => r.Index == index, MaxStat, $"Maxed record {index}");
    public int MinRecord(int index) => EditRecords(r => r.Index == index, MinStat, $"Crippled record {index}");

    /// <summary>Set the player's loyal-warriors count high across every copy.</summary>
    public bool BoostArmy() => ForEachArray("Boost army", (b, block) =>
    {
        if (block.Protagonist is { } p) WriteWord(p.ArmyAddress, BigArmy);
    });

    private bool ForEachArray(string what, Action<IntPtr, GameBlock> act)
    {
        lock (_gate)
        {
            if (_mem == null || _bases.Count == 0) { Log?.Invoke($"{what}: no game loaded yet."); return false; }
            int n = 0;
            foreach (var b in _bases)
            {
                var block = ParseArray(_mem!, b);
                if (block == null) continue;
                act(b, block); n++;
            }
            Log?.Invoke($"{what}: applied to {n} copy/ies.");
            return n > 0;
        }
    }

    private int EditRecords(Func<CharacterRecord, bool> selector, int target, string what)
    {
        lock (_gate)
        {
            if (_mem == null || _bases.Count == 0) { Log?.Invoke($"{what}: no game loaded yet."); return 0; }
            int changed = 0, recs = 0;
            foreach (var b in _bases)
            {
                var block = ParseArray(_mem!, b);
                if (block == null) continue;
                var buf = new byte[Stride];
                foreach (var r in block.Records.Where(selector))
                {
                    if (!_mem.ReadInto(r.Address, buf, buf.Length)) continue;
                    recs++;
                    for (int o = StatLo; o < StatHi; o += 2)
                    {
                        int val = buf[o] | (buf[o + 1] << 8);
                        if (val >= 2 && val <= 128 && val != target && WriteWord((IntPtr)((long)r.Address + o), target))
                            changed++;
                    }
                }
            }
            Log?.Invoke($"{what}: {changed} word(s) across {recs} record instance(s) → {target}.");
            return changed;
        }
    }

    // ---- worker loop ----------------------------------------------------------------------------

    private void Loop()
    {
        while (_running)
        {
            TrainerStatus status;
            try { status = Step(); }
            catch (Exception ex) { status = new TrainerStatus { Connected = false, Message = "Error: " + ex.Message }; }
            StatusChanged?.Invoke(status);
            Thread.Sleep(TickMs);
        }
    }

    private TrainerStatus Step()
    {
        lock (_gate)
        {
            AcquireProcessIfNeeded();
            if (_mem == null)
                return new TrainerStatus { Connected = false, Message = "Waiting for DOSBox…", Target = _target, Freezing = _freezing };

            if (_bases.Count == 0)
            {
                LocateArrays();
                if (_bases.Count == 0)
                    return new TrainerStatus { Connected = true, ProcessId = _pid, Target = _target, Freezing = _freezing,
                        Message = "DOSBox connected — load your game (Restore/Continue) and open a character screen." };
            }

            var blocks = new List<GameBlock>(_bases.Count);
            byte ageTarget = (byte)_target;
            foreach (var b in _bases)
            {
                var block = ParseArray(_mem!, b);
                if (block == null) continue;
                if (_freezing && block.Protagonist is { } p)
                    _mem.WriteByte(p.AgeAddress, ageTarget);
                blocks.Add(block);
            }

            if (blocks.Count == 0) { _bases = new List<IntPtr>(); return new TrainerStatus { Connected = true, ProcessId = _pid, Target = _target, Freezing = _freezing, Message = "Arrays moved — re-scanning…" }; }

            int rivals = blocks[0].Records.Count(r => r.Role == CharacterRole.Rival);
            var prot = blocks[0].Protagonist;
            string who = prot != null ? $"age {LifeStages.Label(prot.AgeStage)}, army {prot.Army}" : "";
            string msg = _freezing
                ? $"Frozen at {LifeStages.Label(ageTarget)} · {blocks.Count} copy/ies · you: {who}"
                : $"Ready · {blocks.Count} copy/ies · {rivals} rival(s) · you: {who}";
            return new TrainerStatus { Connected = true, ProcessId = _pid, Target = _target, Freezing = _freezing,
                Blocks = blocks, Message = msg };
        }
    }

    // ---- live-array discovery -------------------------------------------------------------------

    private void LocateArrays()
    {
        if (_mem == null) return;
        // Keep real character arrays (>=2 records); drop lone tail-matches (e.g. a kin record in a
        // byte-scaled save buffer that coincidentally passes the check).
        _bases = FindPlayerRecords(_mem)
            .Where(b => (ParseArray(_mem, b)?.Records.Count ?? 0) >= 2)
            .ToList();
        if (_bases.Count > 0)
            Log?.Invoke($"Located {_bases.Count} live character array(s) at {string.Join(", ", _bases.Select(b => "0x" + ((long)b).ToString("X")))}.");
    }

    /// <summary>
    /// Find the record-0 (player) address of every live character array: scan for the record tail,
    /// then walk back to the first qualifying record so mid-array matches collapse to one base.
    /// </summary>
    public static List<IntPtr> FindPlayerRecords(ProcessMemory mem)
    {
        var bases = new List<IntPtr>();
        var seen = new HashSet<long>();
        var rbuf = new byte[Stride];

        foreach (IntPtr hit in mem.ScanAll(TailSig, writableOnly: true, maxMatches: 4000))
        {
            long rec0 = (long)hit - TailOff;
            for (int step = 0; step < MaxRecords; step++)
            {
                long prev = rec0 - Stride;
                if (!mem.ReadInto((IntPtr)prev, rbuf, rbuf.Length) || !Qualifies(rbuf, 0)) break;
                rec0 = prev;
            }
            if (!seen.Add(rec0)) continue;
            if (mem.ReadInto((IntPtr)rec0, rbuf, rbuf.Length) && Qualifies(rbuf, 0))
                bases.Add((IntPtr)rec0);
        }
        return bases;
    }

    private static bool Qualifies(byte[] b, int off) =>
        off + Stride <= b.Length
        && b[off + 0x3A] is >= 20 and <= 45 && b[off + 0x3B] == 0
        && b[off + TailOff] == 1 && b[off + TailOff + 1] == 0
        && b[off + AgeOff] <= 6;

    public static GameBlock? ParseArray(ProcessMemory mem, IntPtr baseAddr)
    {
        var buf = new byte[MaxRecords * Stride];
        if (!mem.ReadInto(baseAddr, buf, buf.Length)) return null;

        var recs = new List<CharacterRecord>();
        for (int i = 0; i < MaxRecords; i++)
        {
            int off = i * Stride;
            if (!Qualifies(buf, off)) break;
            int army = buf[off + ArmyOff] | (buf[off + ArmyOff + 1] << 8);
            var stats = new[] { 0x3C, 0x3E, 0x42, 0x44, 0x46 }
                .Select(o => buf[off + o] | (buf[off + o + 1] << 8)).ToList();
            recs.Add(new CharacterRecord(i, (IntPtr)((long)baseAddr + off), CharacterRole.You,
                buf[off + FamilyOff], buf[off + AgeOff], army, stats));
        }
        if (recs.Count == 0) return null;

        byte fam0 = recs[0].FamilyIndex;
        for (int r = 0; r < recs.Count; r++)
        {
            CharacterRole role = r == 0 ? CharacterRole.You
                : recs[r].FamilyIndex == fam0 ? CharacterRole.Kin
                : CharacterRole.Rival;
            recs[r] = recs[r] with { Role = role };
        }
        return new GameBlock { BaseAddress = baseAddr, Records = recs };
    }

    // One WriteProcessMemory for the whole little-endian word: two separate byte writes could
    // leave the value half-updated if the second failed after the first landed.
    private bool WriteWord(IntPtr addr, int value) =>
        _mem!.WriteBytes(addr, new byte[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) });

    // ---- process management ---------------------------------------------------------------------

    private void AcquireProcessIfNeeded()
    {
        if (_mem != null)
        {
            try { if (!Process.GetProcessById(_pid).HasExited) return; } catch { }
            DropProcess();
        }
        foreach (var proc in FindDosBoxProcesses())
        {
            var mem = ProcessMemory.TryOpen(proc.Id);
            if (mem == null) continue;
            _mem = mem; _pid = proc.Id; _bases = new List<IntPtr>();
            Log?.Invoke($"Attached to {proc.ProcessName} (PID {proc.Id}).");
            return;
        }
    }

    private static IEnumerable<Process> FindDosBoxProcesses()
    {
        Process[] all;
        try { all = Process.GetProcesses(); } catch { yield break; }
        foreach (var p in all)
        {
            string name;
            try { name = p.ProcessName; } catch { continue; }
            if (name.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase)) yield return p;
        }
    }

    private void DropProcess()
    {
        _mem?.Dispose(); _mem = null; _pid = 0; _bases = new List<IntPtr>();
    }

    public void Dispose()
    {
        _running = false;
        try { _thread?.Join(500); } catch { }
        lock (_gate) DropProcess();
    }

    // ---- legacy helpers for the Verify harness (save-format buffers) -----------------------------

    public sealed record Discovery(IReadOnlyList<IntPtr> Bases, byte[]? Fragment, int FragOffset, string PlayerName);

    public static Discovery DiscoverStateBlocks(ProcessMemory mem)
    {
        var refBuf = new byte[BlockReadLen];
        IntPtr refBase = IntPtr.Zero;
        foreach (IntPtr hit in mem.ScanAll(Signature, writableOnly: true))
        {
            IntPtr b = (IntPtr)((long)hit - 0x23);
            if (mem.ReadInto(b, refBuf, refBuf.Length) && ParseBlock(b, refBuf) is { }) { refBase = b; break; }
        }
        if (refBase == IntPtr.Zero) return new Discovery(Array.Empty<IntPtr>(), null, 0, "");

        int nameOff = FirstAsciiRun(refBuf, 0x100, 4);
        if (nameOff < 0 || nameOff - 0x10 < 0) return new Discovery(Array.Empty<IntPtr>(), null, 0, "");
        int fragOffset = nameOff - 0x10;
        byte[] fragment = refBuf[fragOffset..(fragOffset + 0x30)];
        string name = Encoding.ASCII.GetString(TrimName(fragment));

        var bases = new List<IntPtr>();
        var buf = new byte[BlockReadLen];
        foreach (IntPtr hit in mem.ScanAll(fragment, writableOnly: true, maxMatches: 32))
        {
            IntPtr baseAddr = (IntPtr)((long)hit - fragOffset);
            if (mem.ReadInto(baseAddr, buf, buf.Length) && ParseBlock(baseAddr, buf) is { })
                bases.Add(baseAddr);
        }
        return new Discovery(bases, fragment, fragOffset, name);
    }

    /// <summary>Legacy save-format parser (records at 0x6F, byte-scaled) — used only by Verify.</summary>
    public static GameBlock? ParseBlock(IntPtr baseAddr, byte[] buf)
    {
        int nt = FirstAsciiRun(buf, 0x100, 4);
        if (nt < 0) nt = buf.Length;
        var records = new List<CharacterRecord>();
        int i = 0;
        while (0x6F + (i + 1) * Stride <= nt)
        {
            int off = 0x6F + i * Stride;
            var stats = Enumerable.Range(0, 6).Select(k => (int)buf[off + 0x3A + 2 * k]).ToList();
            records.Add(new CharacterRecord(i, (IntPtr)((long)baseAddr + off), CharacterRole.You,
                buf[off + FamilyOff], buf[off + AgeOff], buf[off + ArmyOff] | (buf[off + ArmyOff + 1] << 8), stats));
            i++;
        }
        if (records.Count == 0) return null;
        byte fam0 = records[0].FamilyIndex;
        for (int r = 0; r < records.Count; r++)
        {
            CharacterRole role = r == 0 ? CharacterRole.You
                : records[r].FamilyIndex == fam0 ? CharacterRole.Kin : CharacterRole.Rival;
            records[r] = records[r] with { Role = role };
        }
        return new GameBlock { BaseAddress = baseAddr, Records = records };
    }

    private static int FirstAsciiRun(byte[] buf, int start, int minLen)
    {
        int run = 0;
        for (int i = start; i < buf.Length; i++)
        {
            byte c = buf[i];
            bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            if (letter) { if (++run >= minLen) return i - run + 1; }
            else run = 0;
        }
        return -1;
    }

    private static byte[] TrimName(byte[] frag)
    {
        int s = 0x10, e = s;
        while (e < frag.Length && ((frag[e] >= 'A' && frag[e] <= 'Z') || (frag[e] >= 'a' && frag[e] <= 'z'))) e++;
        return e > s ? frag[s..e] : Array.Empty<byte>();
    }
}
