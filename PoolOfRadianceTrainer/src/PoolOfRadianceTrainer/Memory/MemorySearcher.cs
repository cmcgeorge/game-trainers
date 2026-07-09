namespace PoolOfRadianceTrainer.Memory;

/// <summary>The scalar width a memory search operates on.</summary>
public enum ScanWidth { Byte = 1, Int16 = 2, Int32 = 4 }

/// <summary>How a next-scan filters the previous candidates.</summary>
public enum ScanCompare { Equal, Changed, Unchanged, Increased, Decreased, GreaterThan, LessThan }

/// <summary>
/// A surviving search candidate: its address and the value captured at the last scan.
/// <see cref="Value"/> is the differential baseline — it only advances when the user runs an
/// explicit first/next scan, never from the live-display refresh, so Increased/Decreased/
/// Changed always compare against the previous scan.
/// </summary>
public readonly record struct ScanResult(nuint Address, long Value);

/// <summary>
/// A small Cheat-Engine-style scanner for values the character record doesn't hold —
/// the party's map position and facing, the in-combat clock, encounter counters, etc.
/// First-scan by exact value or unknown; then narrow with increased/decreased/changed.
/// Int16/Int32 values are interpreted as signed (matching the width names).
/// </summary>
public sealed class MemorySearcher
{
    private const int ChunkSize = 1 << 20;         // 1 MiB scan window
    private const int MaxResults = 20_000_000;     // runaway-scan backstop
    private const long Unreadable = long.MinValue; // sentinel for a candidate that no longer reads

    private readonly ProcessMemory _mem;
    private List<ScanResult> _results = new();

    public ScanWidth Width { get; private set; } = ScanWidth.Int16;
    public int Count => _results.Count;
    public IReadOnlyList<ScanResult> Results => _results;

    public MemorySearcher(ProcessMemory mem) => _mem = mem;

    public void Reset() => _results = new List<ScanResult>();

    /// <summary>First scan for an exact value across all committed regions.</summary>
    public void FirstScanValue(ScanWidth width, long value, CancellationToken ct = default)
    {
        Width = width;
        _results = ScanAll(width, v => v == value, ct);
    }

    /// <summary>First scan capturing every readable address's current value (unknown-value scan).</summary>
    public void FirstScanUnknown(ScanWidth width, CancellationToken ct = default)
    {
        Width = width;
        _results = ScanAll(width, _ => true, ct);
    }

    /// <summary>Narrow the current candidate set against fresh reads, advancing the baseline to now.</summary>
    public void NextScan(ScanCompare compare, long? value = null, CancellationToken ct = default)
    {
        var current = ReadCurrent(ct);
        var next = new List<ScanResult>(_results.Count);
        for (int i = 0; i < _results.Count; i++)
        {
            long cur = current[i];
            if (cur == Unreadable) continue;
            long baseline = _results[i].Value;
            bool keep = compare switch
            {
                ScanCompare.Equal => value.HasValue && cur == value.Value,
                ScanCompare.Changed => cur != baseline,
                ScanCompare.Unchanged => cur == baseline,
                ScanCompare.Increased => cur > baseline,
                ScanCompare.Decreased => cur < baseline,
                ScanCompare.GreaterThan => value.HasValue && cur > value.Value,
                ScanCompare.LessThan => value.HasValue && cur < value.Value,
                _ => false
            };
            // Survivors take the current value as their new baseline for the next differential scan.
            if (keep) next.Add(new ScanResult(_results[i].Address, cur));
        }
        _results = next;
    }

    /// <summary>Reads the current value at a single address (for live-display refresh); null if unreadable.</summary>
    public long? ReadLive(nuint address)
    {
        int stride = (int)Width;
        var buf = new byte[stride];
        return _mem.Read(address, buf, stride) == stride ? ReadValue(buf, 0, Width) : null;
    }

    public bool Write(nuint address, long value)
    {
        int stride = (int)Width;
        var buf = new byte[stride];
        for (int b = 0; b < stride; b++) buf[b] = (byte)((value >> (8 * b)) & 0xFF);
        return _mem.Write(address, buf);
    }

    /// <summary>
    /// Bulk-reads the current value of every candidate. Because the candidate list is in
    /// ascending address order, a single reused chunk buffer serves many candidates, so this
    /// costs on the order of (committed bytes / 1 MiB) syscalls rather than one per candidate.
    /// </summary>
    private long[] ReadCurrent(CancellationToken ct)
    {
        int stride = (int)Width;
        var values = new long[_results.Count];
        byte[] buf = new byte[ChunkSize];
        nuint bufBase = 0;
        int bufLen = 0;
        for (int i = 0; i < _results.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            nuint a = _results[i].Address;
            bool inBuffer = bufLen >= stride && a >= bufBase && a + (nuint)stride <= bufBase + (nuint)bufLen;
            if (!inBuffer)
            {
                bufBase = a;
                bufLen = _mem.Read(a, buf, ChunkSize);
                if (bufLen < stride) { values[i] = Unreadable; continue; }
            }
            values[i] = ReadValue(buf, (int)(a - bufBase), Width);
        }
        return values;
    }

    private List<ScanResult> ScanAll(ScanWidth width, Func<long, bool> keep, CancellationToken ct)
    {
        int stride = (int)width;
        var results = new List<ScanResult>();
        var buf = new byte[ChunkSize];
        foreach (var region in _mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            for (nuint off = 0; off < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - off);
                int read = _mem.Read(region.Base + off, buf, want);
                if (read < stride) break;
                for (int i = 0; i + stride <= read; i += stride)
                {
                    long v = ReadValue(buf, i, width);
                    if (!keep(v)) continue;
                    if (results.Count >= MaxResults) return results;   // cap before adding (no overshoot)
                    results.Add(new ScanResult(region.Base + off + (nuint)i, v));
                }
                // Advance by what was actually read so a short (partial) read doesn't skip bytes.
                off += (nuint)(read >= want ? want : Math.Max(stride, read));
            }
        }
        return results;
    }

    private static long ReadValue(byte[] buf, int i, ScanWidth width) => width switch
    {
        ScanWidth.Byte => buf[i],
        ScanWidth.Int16 => (short)(buf[i] | (buf[i + 1] << 8)),
        ScanWidth.Int32 => buf[i] | (buf[i + 1] << 8) | (buf[i + 2] << 16) | (buf[i + 3] << 24),
        _ => 0
    };
}
