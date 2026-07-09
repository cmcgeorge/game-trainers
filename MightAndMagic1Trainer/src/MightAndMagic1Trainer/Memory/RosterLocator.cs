using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Memory;

/// <summary>A located roster: the address of slot 0 and how many consecutive records follow.</summary>
public readonly record struct RosterLocation(nuint BaseAddress, int RecordCount)
{
    public override string ToString() =>
        $"0x{(ulong)BaseAddress:X} ({RecordCount} record{(RecordCount == 1 ? "" : "s")})";
}

/// <summary>
/// Scans a target process for the MM1 roster by looking for runs of consecutive
/// 128-byte-strided records that satisfy <see cref="RosterFormat.LooksLikeRecord"/>.
/// The run with the most consecutive valid records wins (the real roster is a
/// contiguous block of slots; stray false positives don't chain).
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int Stride = RosterFormat.MemoryStride;

    /// <summary>
    /// Returns candidate roster locations, best (longest run) first.
    /// <paramref name="progress"/> receives 0..1 scan progress if supplied.
    /// </summary>
    public static List<RosterLocation> FindAll(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var hits = new List<RosterLocation>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var r in regions) totalBytes += r.Size;
        nuint scanned = 0;

        // Track addresses already consumed by an accepted run so we don't re-report
        // overlapping sub-runs.
        var claimedUpTo = (nuint)0;

        // Reuse a single chunk buffer across the whole address-space walk. mem.Read's
        // allocating overload would otherwise hand back a fresh 1 MiB array per chunk —
        // thousands of Large Object Heap allocations on a full scan.
        byte[] buf = new byte[ChunkSize];

        foreach (var region in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - offset);
                // Overlap by one stride so a record straddling a chunk edge is still seen.
                int read = mem.Read(region.Base + offset, buf, want);
                if (read < RosterFormat.RecordSize)
                {
                    scanned += (nuint)want;
                    break;
                }

                for (int i = 0; i + RosterFormat.RecordSize <= read; i++)
                {
                    nuint absolute = region.Base + offset + (nuint)i;
                    if (absolute < claimedUpTo) continue;

                    if (!RosterFormat.LooksLikeRecord(buf, i)) continue;

                    // Count how many consecutive records follow at the 128-byte stride,
                    // reading more memory as needed.
                    int count = CountRun(mem, absolute);
                    if (count >= 1)
                    {
                        hits.Add(new RosterLocation(absolute, count));
                        claimedUpTo = absolute + (nuint)((long)count * Stride);
                        // Skip past the part of the run that lies inside this buffer.
                        // The rest is covered by claimedUpTo on later (overlapping) chunks.
                        int runBytesInBuf = (int)Math.Min((long)count * Stride, read - i);
                        i += Math.Max(0, runBytesInBuf - 1);   // for-loop's i++ resumes after the run
                    }
                }

                // Advance, overlapping by a stride to catch boundary-straddling records.
                nuint advance = (nuint)Math.Max(1, want - Stride);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        // Best first: longest run, then lowest address.
        hits.Sort((a, b) =>
        {
            int c = b.RecordCount.CompareTo(a.RecordCount);
            return c != 0 ? c : a.BaseAddress.CompareTo(b.BaseAddress);
        });
        return hits;
    }

    /// <summary>Best single match, or null if none found.</summary>
    public static RosterLocation? Find(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var all = FindAll(mem, progress, ct);
        return all.Count > 0 ? all[0] : null;
    }

    private static int CountRun(ProcessMemory mem, nuint baseAddr)
    {
        int count = 0;
        var rec = new byte[RosterFormat.RecordSize];
        for (int slot = 0; slot < RosterFormat.MaxSlots; slot++)
        {
            nuint addr = baseAddr + (nuint)(slot * Stride);
            if (mem.Read(addr, rec, rec.Length) != rec.Length) break;
            if (!RosterFormat.LooksLikeRecord(rec, 0)) break;
            count++;
        }
        return count;
    }
}
