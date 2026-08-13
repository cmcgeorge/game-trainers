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
    private const long Unreadable = long.MinValue; // sentinel for a candidate that no longer reads

    /// <summary>
    /// Backstop on how many candidates one scan may keep. A candidate costs 16 bytes, so this is a
    /// ~64 MB ceiling — enough to hold every byte of a DOS game's 640 KiB conventional memory many
    /// times over, while keeping a stray unknown-value scan of a 4 GB emulator process from turning
    /// into a multi-gigabyte list and an out-of-memory kill. Hitting it sets
    /// <see cref="Truncated"/> rather than quietly returning a partial sweep as if it were the
    /// whole thing.
    /// </summary>
    public const int MaxResults = 4_000_000;

    private readonly ProcessMemory _mem;
    private List<ScanResult> _results = new();

    public ScanWidth Width { get; private set; } = ScanWidth.Int16;
    public int Count => _results.Count;
    public IReadOnlyList<ScanResult> Results => _results;

    /// <summary>True when the last first-scan stopped at <see cref="MaxResults"/> with regions still
    /// unscanned, so the candidate set is a prefix of memory rather than all of it.</summary>
    public bool Truncated { get; private set; }

    /// <summary>
    /// Whether a first scan examines every byte offset or only offsets a whole number of values
    /// apart. Game data is not reliably aligned — the party's map position is three adjacent bytes,
    /// and the wilderness pair of 16-bit words sits wherever the compiler put it — so an exact-value
    /// search checks every offset and will find a value the game stored at an odd address. An
    /// unknown-value search keeps one candidate per examined offset, so it steps by the value width
    /// instead: scanning every offset there would multiply an already huge candidate set by 2 or 4
    /// for a kind of search that is narrowed down by later passes anyway.
    /// </summary>
    public static int StepFor(ScanWidth width, bool exactValue) => exactValue ? 1 : (int)width;

    public MemorySearcher(ProcessMemory mem) => _mem = mem;

    public void Reset()
    {
        _results = new List<ScanResult>();
        Truncated = false;
    }

    /// <summary>First scan for an exact value across all committed regions.</summary>
    public void FirstScanValue(ScanWidth width, long value, CancellationToken ct = default)
    {
        Width = width;
        _results = ScanAll(width, v => v == value, StepFor(width, exactValue: true), ct);
    }

    /// <summary>First scan capturing every readable address's current value (unknown-value scan).</summary>
    public void FirstScanUnknown(ScanWidth width, CancellationToken ct = default)
    {
        Width = width;
        _results = ScanAll(width, _ => true, StepFor(width, exactValue: false), ct);
    }

    /// <summary>
    /// Narrow the current candidate set against fresh reads, advancing the baseline to now.
    ///
    /// <para>Survivors are collected into a list that starts empty and grows to the number that
    /// actually survive, and the current values are read as the walk goes rather than into an array
    /// the size of the whole candidate set first. A narrowing pass over a large unknown-value scan
    /// therefore costs memory proportional to its <i>result</i>, not to its input, which is what
    /// keeps the first narrow of a byte scan from needing three copies of the candidate set at once.</para>
    /// </summary>
    public void NextScan(ScanCompare compare, long? value = null, CancellationToken ct = default)
    {
        int stride = (int)Width;
        var next = new List<ScanResult>();
        byte[] buf = new byte[ChunkSize];
        nuint bufBase = 0;
        int bufLen = 0;

        for (int i = 0; i < _results.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            long cur = CurrentValue(_results[i].Address, stride, buf, ref bufBase, ref bufLen);
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
        Truncated = false;   // a narrow re-examines every candidate it was given
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
    /// The current value at one candidate address, reusing <paramref name="buf"/> across candidates.
    /// Because the candidate list is in ascending address order, one 1 MiB read serves many
    /// candidates, so a narrowing pass costs on the order of (committed bytes / 1 MiB) syscalls
    /// rather than one per candidate.
    /// </summary>
    private long CurrentValue(nuint a, int stride, byte[] buf, ref nuint bufBase, ref int bufLen)
    {
        bool inBuffer = bufLen >= stride && a >= bufBase && a + (nuint)stride <= bufBase + (nuint)bufLen;
        if (!inBuffer)
        {
            bufBase = a;
            bufLen = _mem.Read(a, buf, ChunkSize);
            if (bufLen < stride)
            {
                // The 1 MiB bulk read can straddle the end of this candidate's committed region
                // and fail wholesale; the value itself still fits (ScanAll only keeps addresses
                // with a full stride inside a region), so retry just its width before giving up —
                // otherwise valid candidates in a region's last ~1 MiB are silently dropped when
                // narrowing, and the real target can be narrowed away and never seen again.
                bufLen = _mem.Read(a, buf, stride);
                if (bufLen < stride) return Unreadable;
            }
        }
        return ReadValue(buf, (int)(a - bufBase), Width);
    }

    /// <summary>
    /// Walks every committed region, testing <paramref name="keep"/> at every <paramref name="step"/>
    /// bytes.
    ///
    /// <para>Chunks overlap by <c>stride - 1</c> bytes and only offsets inside the chunk proper are
    /// emitted, so a value lying across a chunk boundary is found exactly once instead of being
    /// missed. On a short read — an unreadable page part-way through a region — the walk resumes at
    /// a whole number of steps past what it managed to read, so the offsets it examines stay on the
    /// same footing before and after the gap instead of silently shifting by the size of the gap.</para>
    /// </summary>
    private List<ScanResult> ScanAll(ScanWidth width, Func<long, bool> keep, int step, CancellationToken ct)
    {
        int stride = (int)width;
        var results = new List<ScanResult>();
        var buf = new byte[ChunkSize + stride - 1];
        Truncated = false;

        foreach (var region in _mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            for (nuint off = 0; off < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - off);
                int readWant = (int)Math.Min((nuint)(ChunkSize + stride - 1), region.Size - off);
                int read = _mem.Read(region.Base + off, buf, readWant);
                if (read < stride) break;

                // Emit only offsets that start inside this chunk; the tail belongs to the next one.
                int last = Math.Min(read - stride, want - 1);
                for (int i = 0; i <= last; i += step)
                {
                    long v = ReadValue(buf, i, width);
                    if (!keep(v)) continue;
                    if (results.Count >= MaxResults) { Truncated = true; return results; }
                    results.Add(new ScanResult(region.Base + off + (nuint)i, v));
                }

                nuint advance;
                if (read >= want) advance = (nuint)want;
                else
                {
                    // Past the readable part, rounded down to a whole number of steps so the
                    // examined offsets keep their spacing across the gap.
                    int usable = Math.Max(step, read - stride + 1);
                    advance = (nuint)(usable / step * step);
                }
                off += advance;
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
