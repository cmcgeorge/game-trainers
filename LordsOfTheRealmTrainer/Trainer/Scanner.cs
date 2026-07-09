namespace LordsTrainer;

public enum ValueType { Int32, Int16 }

public enum ScanCompare { Exact, Increased, Decreased, Unchanged, Changed }

/// <summary>One candidate address plus its value at the last scan.</summary>
public sealed class ScanResult(uint address, long value)
{
    public uint Address { get; } = address;
    public long Value { get; set; } = value;
    public string AddressHex => "0x" + Address.ToString("X5");
}

/// <summary>
/// A Cheat-Engine-style value scanner over the emulator's conventional memory.
/// First scan snapshots every address whose value matches; subsequent scans
/// narrow the set by exact value or by how the value changed.
///
/// Candidates are held as parallel <c>offset</c>/<c>value</c> arrays (compacted in
/// place on each narrowing pass) rather than a hashmap of absolute addresses, so a
/// wide "unknown" snapshot over 640 KB doesn't allocate hundreds of thousands of
/// hash nodes or a fresh dictionary per pass.
/// </summary>
public sealed class Scanner(IGuestMemory memory)
{
    private readonly IGuestMemory _memory = memory;

    /// <summary>Above this many candidates the UI does not materialise the result list.</summary>
    public const int MaxDisplayResults = 2000;

    // Parallel candidate store: _offsets[k] is a byte offset into [RangeStart, RangeEnd)
    // and _prevValues[k] is the value seen there at the previous scan. Kept in ascending
    // offset order (built ascending, compaction preserves order).
    private int[] _offsets = [];
    private long[] _prevValues = [];
    private int _count;

    // Reused range buffer + per-chunk read-success flags, so a chunk that fails to read
    // can be excluded from comparisons instead of poisoning them with zeros.
    private byte[] _scanBuf = [];
    private bool[] _chunkOk = [];
    private const int ChunkSize = 0x8000;

    public int Count => _count;
    public bool HasScan { get; private set; }

    public ValueType Type { get; private set; } = ValueType.Int32;
    public uint RangeStart { get; private set; }
    public uint RangeEnd { get; private set; }

    private static int Width(ValueType t) => t == ValueType.Int32 ? 4 : 2;

    private static long ReadValue(byte[] buf, int i, ValueType t) =>
        t == ValueType.Int32
            ? BitConverter.ToInt32(buf, i)
            : BitConverter.ToInt16(buf, i);

    /// <summary>Snapshots the whole range; keeps addresses equal to <paramref name="value"/>.</summary>
    public void FirstScan(long value, ValueType type, uint rangeStart, uint rangeEnd)
    {
        Type = type;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;

        int len = ReadRange(rangeStart, rangeEnd);
        int w = Width(type);
        var offs = new List<int>();
        var vals = new List<long>();
        // Exact-match steps one byte at a time (i++): the value can sit at ANY offset,
        // not just an aligned one, so we must probe every position.
        for (int i = 0; i + w <= len; i++)
        {
            if (!Readable(i, w)) continue;
            long v = ReadValue(_scanBuf, i, type);
            if (v == value) { offs.Add(i); vals.Add(v); }
        }
        _offsets = offs.ToArray();
        _prevValues = vals.ToArray();
        _count = _offsets.Length;
        HasScan = true;
    }

    /// <summary>Snapshots the range and keeps every aligned address (for change-based scans).</summary>
    public void FirstScanUnknown(ValueType type, uint rangeStart, uint rangeEnd)
    {
        Type = type;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;

        int len = ReadRange(rangeStart, rangeEnd);
        int w = Width(type);
        int cap = len / w + 1;
        var offs = new int[cap];
        var vals = new long[cap];
        int n = 0;
        // Change-based scanning tracks aligned slots only (i += w): we compare a slot's
        // value against itself over time, so its stride is the value width.
        for (int i = 0; i + w <= len; i += w)
        {
            if (!Readable(i, w)) continue;
            offs[n] = i;
            vals[n] = ReadValue(_scanBuf, i, type);
            n++;
        }
        _offsets = offs;
        _prevValues = vals;
        _count = n;
        HasScan = true;
    }

    /// <summary>Re-reads current candidates and keeps those equal to <paramref name="value"/>.</summary>
    public void NextScanExact(long value) => NextScan(ScanCompare.Exact, value);

    /// <summary>Re-reads current candidates and keeps those matching a change comparison.</summary>
    public void NextScan(ScanCompare compare) => NextScan(compare, 0);

    private void NextScan(ScanCompare compare, long value)
    {
        if (!HasScan) return;
        int w = Width(Type);

        // Read the whole active range once (<= 1 MB, chunked) and index into it, rather
        // than issuing one read per candidate — which would be tens of thousands of
        // syscalls for a broad scan. Compact the survivors in place.
        int len = ReadRange(RangeStart, RangeEnd);
        int keep = 0;
        for (int k = 0; k < _count; k++)
        {
            int idx = _offsets[k];
            if (idx < 0 || idx + w > len || !Readable(idx, w))
                continue;                         // out of range or couldn't be re-read -> drop
            long prev = _prevValues[k];
            long cur = ReadValue(_scanBuf, idx, Type);
            bool match = compare switch
            {
                ScanCompare.Exact => cur == value,
                ScanCompare.Increased => cur > prev,
                ScanCompare.Decreased => cur < prev,
                ScanCompare.Unchanged => cur == prev,
                ScanCompare.Changed => cur != prev,
                _ => false,
            };
            if (match)
            {
                _offsets[keep] = idx;
                _prevValues[keep] = cur;
                keep++;
            }
        }
        _count = keep;
    }

    public void Reset()
    {
        _offsets = [];
        _prevValues = [];
        _count = 0;
        HasScan = false;
    }

    /// <summary>Returns up to <paramref name="max"/> results (values from the last scan), sorted by address.</summary>
    public List<ScanResult> Snapshot(int max = MaxDisplayResults)
    {
        int n = Math.Min(max, _count);
        var list = new List<ScanResult>(n);
        for (int k = 0; k < n; k++)           // _offsets is already in ascending order
            list.Add(new ScanResult(RangeStart + (uint)_offsets[k], _prevValues[k]));
        return list;
    }

    /// <summary>
    /// Cheaply summarises the current candidates for the treasury finder without allocating a
    /// <see cref="ScanResult"/> list, so it is safe to call from UI-state updates. Prefers
    /// addresses aligned to <paramref name="alignBytes"/> (which must be a power of two): the
    /// treasury is an aligned Int32, so this drops the coincidental unaligned matches a
    /// byte-granular scan turns up, and it falls back to the raw set when nothing is aligned.
    /// Unlike <see cref="Snapshot"/> it considers EVERY candidate (no display cap), so it can
    /// never miss the real address the way a capped snapshot can. Returns the effective
    /// candidate count and, when that count is exactly one, that sole guest address.
    /// </summary>
    public (int Count, uint Address) AlignedSummary(uint alignBytes)
    {
        uint mask = alignBytes - 1;
        int aligned = 0;
        uint firstAligned = 0;
        for (int k = 0; k < _count; k++)
        {
            uint addr = RangeStart + (uint)_offsets[k];
            if ((addr & mask) == 0)
            {
                if (aligned == 0) firstAligned = addr;
                aligned++;
            }
        }
        if (aligned > 0)
            return (aligned, aligned == 1 ? firstAligned : 0u);

        // No aligned candidate: fall back to the raw set (a build whose treasury isn't
        // 4-aligned still works, it just needs more narrowing).
        uint only = _count == 1 ? RangeStart + (uint)_offsets[0] : 0u;
        return (_count, only);
    }

    // Reads [start, end) into the reused _scanBuf and records per-chunk success in
    // _chunkOk. Returns the number of bytes covered. Bytes in a failed chunk are zeroed
    // and flagged unreadable so they can't create false candidates.
    private int ReadRange(uint start, uint end)
    {
        if (end <= start) { _chunkOk = []; return 0; }
        int len = (int)(end - start);
        if (_scanBuf.Length < len) _scanBuf = new byte[len];

        int nChunks = (len + ChunkSize - 1) / ChunkSize;
        if (_chunkOk.Length < nChunks) _chunkOk = new bool[nChunks];

        for (int c = 0; c < nChunks; c++)
        {
            int off = c * ChunkSize;
            int take = Math.Min(ChunkSize, len - off);
            int got = _memory.ReadGuestInto(start + (uint)off, _scanBuf.AsSpan(off, take));
            _chunkOk[c] = got == take;
            if (got < take)
                Array.Clear(_scanBuf, off + Math.Max(got, 0), take - Math.Max(got, 0));
        }
        return len;
    }

    // A value of width w at offset idx is trustworthy only if every chunk it touches
    // read successfully. Widths (2/4) are tiny, so it spans at most two chunks.
    private bool Readable(int idx, int w)
    {
        int c0 = idx / ChunkSize;
        int c1 = (idx + w - 1) / ChunkSize;
        return _chunkOk[c0] && _chunkOk[c1];
    }
}
