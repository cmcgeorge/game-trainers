namespace MightAndMagic1Trainer.Memory;

/// <summary>The outcome of a pattern scan: the matching addresses (ascending) plus
/// whether coverage was incomplete (a match or per-region cap was hit).</summary>
public readonly record struct PatternScanResult(List<nuint> Addresses, bool Truncated);

/// <summary>
/// A standalone byte-pattern scan over a target process: find every address whose bytes
/// match a given sequence. Used by the X/Y search to locate a coordinate pair the roster
/// format never exposes — e.g. North=10, East=5 stored as two adjacent bytes (0A 05).
/// Walks the same committed regions the value searcher does, capping each
/// region so one huge mapping can't blow up the allocation. A pattern straddling a region
/// boundary (the per-region cap, or two adjacent regions read independently) is not matched;
/// that's acceptable here because the target struct fits within a single allocation. Not
/// thread-safe; drive it from a single (background) thread.
/// </summary>
public static class BytePatternScanner
{
    /// <summary>Upper bound on returned addresses (even a 4-byte pattern can hit a lot).</summary>
    public const int MaxMatches = 1_000_000;

    /// <summary>Per-region read cap, mirroring <see cref="MemorySearcher"/>.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    /// <summary>Scans all committed memory for <paramref name="pattern"/>.</summary>
    public static PatternScanResult Find(ProcessMemory mem, byte[] pattern, CancellationToken ct = default)
    {
        var hits = new List<nuint>();
        bool truncated = false;
        if (pattern.Length == 0) return new PatternScanResult(hits, false);

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            long want = (long)region.Size;
            if (want > MaxRegionBytes) { want = MaxRegionBytes; truncated = true; }
            var data = mem.Read(region.Base, (int)want);
            for (int i = 0; i + pattern.Length <= data.Length; i++)
            {
                if (!Matches(data, i, pattern)) continue;
                if (hits.Count >= MaxMatches) { truncated = true; return new PatternScanResult(hits, truncated); }
                hits.Add(region.Base + (nuint)i);
            }
        }
        return new PatternScanResult(hits, truncated);
    }

    private static bool Matches(byte[] data, int off, byte[] pattern)
    {
        for (int k = 0; k < pattern.Length; k++)
            if (data[off + k] != pattern[k]) return false;
        return true;
    }
}
