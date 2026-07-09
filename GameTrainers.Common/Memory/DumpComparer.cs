using System.Globalization;
using System.IO;

namespace GameTrainers.Common.Memory;

/// <summary>One region row parsed from a dump's .csv index.</summary>
public sealed record DumpRegion(long FileOffset, ulong Address, long Size, long UnreadableBytes);

/// <summary>One run of consecutive differing bytes, located by live process address.
/// The byte previews are capped (see <see cref="DumpComparer.PreviewBytes"/>).</summary>
public sealed record DumpDiffRun(ulong Address, int Length, byte[] OldBytes, byte[] NewBytes);

/// <summary>Outcome of a dump comparison: the runs found plus coverage bookkeeping.
/// <see cref="BytesUnreadable"/> totals the bytes either dump zero-filled because the live
/// page was unreadable when dumped — changes reported in such a span may be phantoms (a
/// readable page diffed against fabricated zeros), so a non-zero value is worth disclosing.</summary>
public sealed record DumpDiffResult(
    IReadOnlyList<DumpDiffRun> Runs,
    long BytesCompared,
    long BytesChanged,
    long BytesOnlyInOne,
    long BytesUnreadable,
    bool Truncated);

/// <summary>
/// Compares two memory dumps produced by <see cref="MemoryDumper"/> and reports which bytes
/// changed, as runs addressed by live process address. The comparison is driven by the two
/// .csv indexes, not by file offset: each dump's regions are mapped back to the address
/// space and only the address ranges present in <em>both</em> dumps are compared, so the
/// result stays correct even when the process mapped/unmapped regions between the dumps
/// (file offsets shift, addresses don't). Bytes covered by only one dump are counted, not
/// diffed. The classic workflow: dump, change one thing in-game, dump again, compare —
/// the changed addresses are pokeable in the Memory tab.
/// </summary>
public static class DumpComparer
{
    /// <summary>Differing runs separated by up to this many equal bytes are merged into one
    /// (a 16-bit or 32-bit value changing in one byte shouldn't read as several runs).</summary>
    public const int MergeGap = 4;

    /// <summary>At most this many old/new bytes are kept per run for display.</summary>
    public const int PreviewBytes = 16;

    private const int DefaultChunkSize = 1 << 20;   // streamed compare buffer per file

    /// <summary>Parses a dump's .csv region index (header line + "0xOFF,0xADDR,0xSIZE,0xUNREAD" rows).</summary>
    public static List<DumpRegion> ReadIndex(string csvPath)
    {
        var regions = new List<DumpRegion>();
        foreach (var line in File.ReadLines(csvPath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 4) throw new InvalidDataException($"Malformed index row: \"{line}\"");
            regions.Add(new DumpRegion(ParseHex(parts[0]), (ulong)ParseHex(parts[1]),
                ParseHex(parts[2]), ParseHex(parts[3])));
        }
        return regions;
    }

    private static long ParseHex(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return long.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compares dump <paramref name="oldBin"/> against <paramref name="newBin"/> using their
    /// region indexes. Collects at most <paramref name="maxRuns"/> runs; when the cap is hit
    /// the comparison stops and the result is flagged truncated (counts cover only the part
    /// compared so far, so a truncated result understates the change).
    /// <paramref name="chunkSize"/> exists for the tests, which shrink it to exercise runs
    /// straddling a chunk boundary; production callers take the default.
    /// </summary>
    public static DumpDiffResult Compare(
        string oldBin, IReadOnlyList<DumpRegion> oldIndex,
        string newBin, IReadOnlyList<DumpRegion> newIndex,
        int maxRuns, IProgress<double>? progress, CancellationToken ct,
        int chunkSize = DefaultChunkSize)
    {
        var overlaps = IntersectByAddress(oldIndex, newIndex);
        long total = overlaps.Sum(o => o.Length);
        long onlyInOne = oldIndex.Sum(r => r.Size) + newIndex.Sum(r => r.Size) - 2 * total;
        long unreadable = oldIndex.Sum(r => r.UnreadableBytes) + newIndex.Sum(r => r.UnreadableBytes);

        var runs = new List<DumpDiffRun>();
        long compared = 0, changed = 0;
        bool truncated = false;
        int lastPercent = -1;

        using var oldFile = File.OpenRead(oldBin);
        using var newFile = File.OpenRead(newBin);
        var oldBuf = new byte[chunkSize];
        var newBuf = new byte[chunkSize];
        var builder = new RunBuilder(runs);

        foreach (var o in overlaps)
        {
            if (truncated) break;
            oldFile.Position = o.OldFileOffset;
            newFile.Position = o.NewFileOffset;
            long remaining = o.Length;
            ulong addr = o.Address;
            builder.StartRegion();   // never merge runs across discontiguous regions
            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int count = (int)Math.Min(remaining, chunkSize);
                ReadExactly(oldFile, oldBuf, count, oldBin);
                ReadExactly(newFile, newBuf, count, newBin);
                for (int i = 0; i < count; i++)
                {
                    if (oldBuf[i] == newBuf[i]) continue;
                    changed++;
                    if (!builder.AddDiff(addr + (ulong)i, oldBuf[i], newBuf[i]))
                    {
                        // Truncation bookkeeping is deliberately loose at the boundary: the
                        // diff byte at index i is already in `changed` though `compared`
                        // stops before it, and the final Flush below emits the run that
                        // byte just opened (so Runs.Count can reach maxRuns + 1). Both are
                        // fine for a result that is flagged Truncated — don't "fix" them.
                        truncated = runs.Count >= maxRuns;
                        if (truncated) { compared += i; break; }
                    }
                }
                if (truncated) break;
                addr += (ulong)count;
                remaining -= count;
                compared += count;

                int percent = total == 0 ? 100 : (int)(compared * 100 / total);
                if (percent != lastPercent) { lastPercent = percent; progress?.Report(percent / 100.0); }
            }
        }
        builder.Flush();
        return new DumpDiffResult(runs, compared, changed, onlyInOne, unreadable, truncated);
    }

    private static void ReadExactly(FileStream f, byte[] buf, int count, string name)
    {
        int got = f.ReadAtLeast(buf.AsSpan(0, count), count, throwOnEndOfStream: false);
        if (got != count)
            throw new InvalidDataException(
                $"{Path.GetFileName(name)} is shorter than its index claims — index and dump don't match.");
    }

    // One address range present in both dumps, with where each dump stored its bytes.
    internal readonly record struct Overlap(ulong Address, long OldFileOffset, long NewFileOffset, long Length);

    // Walk both region lists sorted by address and emit the intersecting spans. Regions
    // within one dump never overlap each other (VirtualQueryEx walks disjoint ranges).
    internal static List<Overlap> IntersectByAddress(
        IReadOnlyList<DumpRegion> oldIndex, IReadOnlyList<DumpRegion> newIndex)
    {
        var a = oldIndex.OrderBy(r => r.Address).ToList();
        var b = newIndex.OrderBy(r => r.Address).ToList();
        var result = new List<Overlap>();
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            ulong aEnd = a[i].Address + (ulong)a[i].Size;
            ulong bEnd = b[j].Address + (ulong)b[j].Size;
            ulong start = Math.Max(a[i].Address, b[j].Address);
            ulong end = Math.Min(aEnd, bEnd);
            if (start < end)
            {
                result.Add(new Overlap(start,
                    a[i].FileOffset + (long)(start - a[i].Address),
                    b[j].FileOffset + (long)(start - b[j].Address),
                    (long)(end - start)));
            }
            if (aEnd <= bEnd) i++; else j++;
        }
        return result;
    }

    /// <summary>
    /// Accumulates differing bytes into runs, merging diffs separated by ≤ MergeGap equal
    /// bytes. The previews record only the first PreviewBytes *changed* bytes of the run
    /// (in address order, merged-over equal bytes excluded), so a multi-megabyte changed
    /// region costs the same memory as a one-byte change.
    /// </summary>
    private sealed class RunBuilder
    {
        private readonly List<DumpDiffRun> _runs;
        private ulong _start;
        private ulong _lastDiff;
        private readonly List<byte> _old = new();
        private readonly List<byte> _new = new();
        private bool _open;

        public RunBuilder(List<DumpDiffRun> runs) => _runs = runs;

        public void StartRegion() => Flush();

        /// <summary>Returns false when this diff started a new run (caller checks the run cap).</summary>
        public bool AddDiff(ulong addr, byte oldB, byte newB)
        {
            bool continuesRun = _open && addr - _lastDiff <= (ulong)(MergeGap + 1);
            if (!continuesRun)
            {
                Flush();
                _start = addr;
                _open = true;
            }
            _lastDiff = addr;
            if (_old.Count < PreviewBytes)
            {
                _old.Add(oldB);
                _new.Add(newB);
            }
            return continuesRun;
        }

        public void Flush()
        {
            if (!_open) return;
            int len = (int)(_lastDiff - _start) + 1;
            _runs.Add(new DumpDiffRun(_start, len, _old.ToArray(), _new.ToArray()));
            _old.Clear();
            _new.Clear();
            _open = false;
        }
    }
}
