using System.Diagnostics;
using System.IO;

namespace GameTrainers.Common.Memory;

/// <summary>What a completed dump wrote: region/byte totals, unreadable (zero-filled) bytes, and elapsed time.</summary>
public sealed record DumpResult(int RegionCount, long BytesWritten, long BytesUnreadable, TimeSpan Elapsed);

/// <summary>
/// Streams every committed, readable region of a process into a single .bin file, plus a .csv
/// index alongside it mapping each region's file offset back to its live process address (the
/// regions aren't contiguous in the process, so the index is what turns a byte pattern found in
/// the file back into an address to poke). Reads in 1 MiB chunks; a chunk that fails as a whole
/// is salvaged page by page with unreadable pages zero-filled and counted, per region, in the
/// index — so a diff or pattern hunt can discount matches that land in fabricated zeros.
/// </summary>
public static class MemoryDumper
{
    private const int ChunkSize = 1 << 20;   // 1 MiB per ReadProcessMemory call
    private const int PageSize = 0x1000;     // retry granularity when a chunk read fails

    /// <summary>The index lives next to the dump as "&lt;dump&gt;.csv" — appended, not swapped, so it
    /// can never collide with the dump path itself or silently claim an unrelated sibling file.</summary>
    public static string IndexPathFor(string dumpPath) => dumpPath + ".csv";

    /// <summary>
    /// Dumps <paramref name="mem"/> to <paramref name="path"/> (index beside it). Writes to
    /// ".part" siblings and renames into place only on success, so a cancelled, failed, or
    /// killed-mid-write dump never destroys an existing dump at the same path and never leaves
    /// a partial that looks complete — at worst a ".part" leftover, which announces itself.
    /// </summary>
    public static DumpResult Dump(ProcessMemory mem, string path,
        IProgress<double> progress, IProgress<string> phase, CancellationToken ct)
    {
        string indexPath = IndexPathFor(path);
        string partBin = path + ".part";
        string partIndex = indexPath + ".part";
        try
        {
            var result = WriteDump(mem, partBin, partIndex, progress, phase, ct);
            File.Move(partBin, path, overwrite: true);
            File.Move(partIndex, indexPath, overwrite: true);
            return result;
        }
        catch
        {
            // Cleanup runs here, on the dumping thread, so it still happens when the UI is
            // gone (app closing) — and it only ever deletes the .part files this run created.
            TryDelete(partBin);
            TryDelete(partIndex);
            throw;
        }
    }

    // Enumerate the regions up front (for a real progress total), then stream each one to the
    // file while the index records where in the file it landed.
    private static DumpResult WriteDump(ProcessMemory mem, string binPath, string indexPath,
        IProgress<double> progress, IProgress<string> phase, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var regions = mem.EnumerateRegions().ToList();
        if (regions.Count == 0)
            throw new InvalidOperationException("No readable memory regions found — is the process still running?");
        long total = 0;
        foreach (var r in regions) total += (long)r.Size;

        // Advisory fail-fast: better a clear message now than gigabytes of I/O ending in a raw
        // IOException on a full drive. (Space can still change under us; the write path copes.)
        var root = Path.GetPathRoot(Path.GetFullPath(binPath));
        if (root != null && TryGetFreeSpace(root) is long free && free < total)
            throw new InvalidOperationException(
                $"Not enough disk space: the dump needs {FormatBytes(total)} and the drive has "
                + $"{FormatBytes(free)} free.");

        phase.Report($"Dumping {regions.Count} regions ({FormatBytes(total)})…");

        long bytesDone = 0, unreadable = 0;
        int lastPercent = -1;
        var buffer = new byte[ChunkSize];
        var page = new byte[PageSize];

        using var bin = new FileStream(binPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
        using var index = new StreamWriter(indexPath);
        index.WriteLine("FileOffset,ProcessAddress,Size,UnreadableBytes");

        foreach (var region in regions)
        {
            long regionOffset = bin.Position;
            long regionUnreadable = 0;
            nuint addr = region.Base;
            nuint remaining = region.Size;
            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int count = (int)Math.Min(remaining, (nuint)ChunkSize);
                // ProcessMemory.Read is all-or-nothing (it returns 0 if any page in the range is
                // unreadable), so on a short read clear the buffer and salvage what's readable page
                // by page. Every byte is then either freshly read or explicitly zeroed — never left
                // stale from the previous chunk.
                if (mem.Read(addr, buffer, count) != count)
                {
                    Array.Clear(buffer, 0, count);
                    long zeroed = SalvageByPage(mem, buffer, page, addr, count);
                    regionUnreadable += zeroed;
                    // A whole chunk with not one readable page usually means the target died, not
                    // a protection hole (regions have uniform protection). Abort with a clear error
                    // instead of grinding out gigabytes of zeros that "succeed".
                    if (zeroed == count && ProcessExited(mem.ProcessId))
                        throw new InvalidOperationException("The target process exited during the dump.");
                }
                bin.Write(buffer, 0, count);
                addr += (nuint)count;
                remaining -= (nuint)count;
                bytesDone += count;

                // Report only when the whole-number percent changes: a multi-GB dump is thousands
                // of chunks, and posting each one would flood the dispatcher for no visible gain.
                int percent = (int)(bytesDone * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(percent / 100.0);
                    phase.Report($"Dumping… {FormatBytes(bytesDone)} of {FormatBytes(total)} ({percent}%).");
                }
            }
            unreadable += regionUnreadable;
            // The region's row is written after its data so it can carry the zero-filled count.
            index.WriteLine($"0x{regionOffset:X},0x{(ulong)region.Base:X},0x{(ulong)region.Size:X},0x{regionUnreadable:X}");
        }
        sw.Stop();
        return new DumpResult(regions.Count, bytesDone, unreadable, sw.Elapsed);
    }

    // Re-read a chunk that failed as a whole, one page at a time, so a single unreadable page
    // doesn't cost the whole chunk. Readable pages are copied in; unreadable ones stay zeroed
    // (the caller cleared the buffer first). Returns the number of bytes left zeroed.
    private static long SalvageByPage(ProcessMemory mem, byte[] buffer, byte[] page, nuint chunkBase, int count)
    {
        long zeroed = 0;
        for (int off = 0; off < count; off += PageSize)
        {
            int n = Math.Min(PageSize, count - off);
            if (mem.Read(chunkBase + (nuint)off, page, n) == n)
                Array.Copy(page, 0, buffer, off, n);
            else
                zeroed += n;        // page unreadable; it stays zeroed in the buffer
        }
        return zeroed;
    }

    // Free space can't be queried for some roots (UNC/network shares, oddly-formed paths):
    // DriveInfo throws there. Treat "unknown" as "proceed" rather than abort a dump that
    // would otherwise succeed — the write path still copes with an actually-full drive.
    private static long? TryGetFreeSpace(string root)
    {
        try { return new DriveInfo(root).AvailableFreeSpace; }
        catch (ArgumentException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Only consulted on the already-slow all-pages-failed path, so the cost is irrelevant.
    private static bool ProcessExited(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.HasExited;
        }
        catch
        {
            return true;   // no such pid, access denied, or racing exit — treat as gone
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* locked or never created; a stray .part is self-evidently incomplete */ }
    }

    public static string FormatBytes(long n) => n switch
    {
        >= 1L << 30 => $"{n / (double)(1L << 30):0.00} GiB",
        >= 1L << 20 => $"{n / (double)(1L << 20):0.0} MiB",
        >= 1L << 10 => $"{n / (double)(1L << 10):0.0} KiB",
        _ => $"{n} bytes",
    };
}
