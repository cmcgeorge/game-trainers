namespace BardsTale1Trainer.Memory;

/// <summary>Width of the value a <see cref="MemorySearcher"/> looks for.</summary>
public enum ScanWidth { Byte = 1, Int16 = 2, Int32 = 4 }

/// <summary>How a follow-up scan filters the existing candidates.</summary>
public enum ScanCompare { Exact, Increased, Decreased, Changed, Unchanged }

/// <summary>A surviving candidate: an address and the value last read there.</summary>
public readonly record struct MemMatch(nuint Address, long Value);

/// <summary>
/// A minimal "Cheat Engine"-style value scanner over a target process. Supports the
/// classic unknown-value workflow: snapshot memory, then repeatedly narrow by
/// increased / decreased / changed / unchanged — exactly what you need to pin down a
/// value the game never shows you numerically (e.g. the party's map X/Y or facing:
/// take a step north, scan "decreased", repeat until one address remains).
///
/// Not thread-safe; drive it from a single (background) thread. A scan reads every
/// committed region once into a transient snapshot, so per-candidate lookups are
/// just an in-memory region search rather than a syscall each.
/// </summary>
public sealed class MemorySearcher
{
    /// <summary>Upper bound on stored candidates (keeps a broad first scan from exhausting RAM).</summary>
    public const int MaxMatches = 4_000_000;

    /// <summary>Per-region read cap, so one absurdly large mapping can't trigger a multi-GB allocation.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    private readonly ProcessMemory _mem;
    private readonly ScanWidth _width;
    private List<MemMatch>? _matches;       // explicit candidate list (exact scan / after first narrowing)
    private List<(nuint Base, byte[] Data)>? _baseline;  // raw snapshot for an unknown-value first scan
    private bool _truncated;

    public MemorySearcher(ProcessMemory mem, ScanWidth width)
    {
        _mem = mem;
        _width = width;
    }

    public ScanWidth Width => _width;

    /// <summary>True once a first scan has run (exact candidates or an unknown baseline).</summary>
    public bool HasMatches => _matches != null || _baseline != null;

    /// <summary>Candidate count, or -1 while only an unknown baseline exists (count not yet materialised).</summary>
    public int MatchCount => _matches?.Count ?? (_baseline != null ? -1 : 0);
    public bool Truncated => _truncated;

    /// <summary>Discards all candidates so the next scan starts fresh.</summary>
    public void Reset() { _matches = null; _baseline = null; _truncated = false; }

    /// <summary>First scan for an exact value across all committed memory.</summary>
    public void FirstScanExact(long value, CancellationToken ct = default)
    {
        _baseline = null;
        _truncated = false;   // a fresh first scan starts with full coverage
        var snap = Snapshot(ct);
        var found = new List<MemMatch>();
        int w = (int)_width;
        foreach (var (baseAddr, data) in snap)
        {
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i + w <= data.Length; i++)
            {
                if (Decode(data, i) != value) continue;
                if (found.Count >= MaxMatches) { _truncated = true; goto done; }
                found.Add(new MemMatch(baseAddr + (nuint)i, value));
            }
        }
    done:
        _matches = found;
    }

    /// <summary>
    /// First scan with an unknown value: keep a raw byte snapshot of memory. The
    /// candidate list isn't materialised yet (that would cost 16 bytes per position);
    /// the next relative scan compares this baseline against fresh memory and only
    /// then builds the surviving-address list.
    /// </summary>
    public void FirstScanUnknown(CancellationToken ct = default)
    {
        _matches = null;
        _truncated = false;   // a fresh first scan starts with full coverage
        _baseline = Snapshot(ct);
    }

    /// <summary>
    /// Narrows the candidate set. For <see cref="ScanCompare.Exact"/>,
    /// <paramref name="value"/> is the target; the relative comparisons ignore it and
    /// compare each position against the value it previously held.
    /// </summary>
    public void NextScan(ScanCompare compare, long value, CancellationToken ct = default)
    {
        if (_baseline != null) { NarrowFromBaseline(compare, value, ct); return; }
        if (_matches == null) return;

        var snap = Snapshot(ct);
        var kept = new List<MemMatch>(_matches.Count);
        int w = (int)_width;
        foreach (var m in _matches)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryRead(snap, m.Address, w, out long now)) continue;   // region gone/unreadable
            if (Keep(compare, now, m.Value, value)) kept.Add(new MemMatch(m.Address, now));
        }
        _matches = kept;
    }

    // First narrowing after an unknown scan: walk the baseline snapshot position by
    // position, compare against the same address in a fresh snapshot, and emit the
    // survivors as the initial candidate list.
    private void NarrowFromBaseline(ScanCompare compare, long value, CancellationToken ct)
    {
        var cur = Snapshot(ct);
        var found = new List<MemMatch>();
        int w = (int)_width;
        foreach (var (baseAddr, data) in _baseline!)
        {
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i + w <= data.Length; i++)
            {
                nuint addr = baseAddr + (nuint)i;
                if (!TryRead(cur, addr, w, out long now)) continue;
                if (!Keep(compare, now, Decode(data, i), value)) continue;
                if (found.Count >= MaxMatches) { _truncated = true; goto done; }
                found.Add(new MemMatch(addr, now));
            }
        }
    done:
        _baseline = null;
        _matches = found;
    }

    private static bool Keep(ScanCompare compare, long now, long prev, long target) => compare switch
    {
        ScanCompare.Exact     => now == target,
        ScanCompare.Increased => now > prev,
        ScanCompare.Decreased => now < prev,
        ScanCompare.Changed   => now != prev,
        ScanCompare.Unchanged => now == prev,
        _ => false,
    };

    /// <summary>Re-reads the live value at every candidate (without filtering).</summary>
    public void RefreshValues(CancellationToken ct = default)
    {
        if (_matches == null) return;
        var snap = Snapshot(ct);
        int w = (int)_width;
        for (int idx = 0; idx < _matches.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();
            if (TryRead(snap, _matches[idx].Address, w, out long now))
                _matches[idx] = new MemMatch(_matches[idx].Address, now);
        }
    }

    /// <summary>
    /// A snapshot copy of up to <paramref name="max"/> current candidates (order is not
    /// guaranteed to be ascending by address). A copy is returned so callers aren't
    /// handed the live list the searcher keeps mutating on later scans.
    /// </summary>
    public IReadOnlyList<MemMatch> Take(int max) =>
        _matches == null ? Array.Empty<MemMatch>()
        : _matches.GetRange(0, Math.Min(max, _matches.Count));

    /// <summary>Reads the live value at an arbitrary address with this searcher's width.</summary>
    public bool ReadValue(nuint address, out long value)
    {
        value = 0;
        var buf = _mem.Read(address, (int)_width);
        if (buf.Length < (int)_width) return false;
        value = Decode(buf, 0);
        return true;
    }

    /// <summary>Writes <paramref name="value"/> (little-endian, this searcher's width) to memory.</summary>
    public bool WriteValue(nuint address, long value)
    {
        int w = (int)_width;
        var buf = new byte[w];
        ulong v = unchecked((ulong)value);
        for (int k = 0; k < w; k++) { buf[k] = (byte)(v & 0xFF); v >>= 8; }
        return _mem.Write(address, buf);
    }

    // --- internals --------------------------------------------------------------
    // EnumerateRegions walks the address space upward, so the returned list is in
    // ascending Base order — a contract TryRead's binary search depends on.
    private List<(nuint Base, byte[] Data)> Snapshot(CancellationToken ct)
    {
        // _truncated is sticky across a scan chain: once a pass (or a caller's match
        // cap) reports incomplete coverage, every narrowed result derived from it is
        // also incomplete. It is cleared only on Reset / a fresh first scan.
        var snap = new List<(nuint, byte[])>();
        foreach (var region in _mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            // Keep each region contiguous so address->offset math stays simple, but
            // cap the read so a single huge mapping can't blow up the allocation. A
            // capped region marks the scan truncated (its tail bytes aren't covered).
            long want = (long)region.Size;
            if (want > MaxRegionBytes) { want = MaxRegionBytes; _truncated = true; }
            var data = _mem.Read(region.Base, (int)want);
            if (data.Length > 0) snap.Add((region.Base, data));
        }
        return snap;
    }

    private long Decode(byte[] data, int off) => _width switch
    {
        ScanWidth.Byte  => data[off],
        ScanWidth.Int16 => (ushort)(data[off] | (data[off + 1] << 8)),
        _               => (uint)(data[off] | (data[off + 1] << 8)
                                  | (data[off + 2] << 16) | (data[off + 3] << 24)),
    };

    private bool TryRead(List<(nuint Base, byte[] Data)> snap, nuint addr, int w, out long value)
    {
        value = 0;
        // snap is in ascending Base order (EnumerateRegions walks the address space
        // upward), so binary-search for the region whose Base is <= addr.
        int lo = 0, hi = snap.Count - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (snap[mid].Base <= addr) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        if (found < 0) return false;
        var (baseAddr, data) = snap[found];
        nuint offN = addr - baseAddr;
        if (offN + (nuint)w > (nuint)data.Length) return false;
        value = Decode(data, (int)offN);
        return true;
    }
}
