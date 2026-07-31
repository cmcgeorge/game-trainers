using AlternateRealityTrainer.Game;

namespace AlternateRealityTrainer.Memory;

/// <summary>The outcome of an auto-locate.</summary>
/// <param name="RecordAddress">Live address of the character record, or 0 when nothing was found.</param>
/// <param name="DgroupAddress">Live address of DGROUP:0000, or 0 when the structural fallback was used.</param>
/// <param name="Buffer">A full copy of the record, or an empty array.</param>
/// <param name="Method">How it was found — shown to the user so an unanchored hit is obvious.</param>
/// <param name="ValidatorsMatched">How many corroborating literals lined up (0 for the fallback).</param>
public readonly record struct LocateResult(
    nuint RecordAddress,
    nuint DgroupAddress,
    byte[] Buffer,
    string Method,
    int ValidatorsMatched)
{
    public bool Found => RecordAddress != 0 && Buffer is { Length: >= CharacterFormat.LiveFieldsLength };

    public static LocateResult None => new(0, 0, Array.Empty<byte>(), "not found", 0);
}

/// <summary>
/// Finds the live character record inside the attached emulator's memory. Nothing is hard-coded to
/// an address: the game is relocated by DOS and its data segment lands somewhere different every
/// session, so the record is found from scratch each time.
///
/// Two strategies, tried in order:
///
/// 1. <b>Anchored</b> (normal path, no value scanning). Sweep for the status-bar header literal
///    <c>Stats STA   CHR   STR   INT   WIS   SKL</c>, which sits at a known offset in the program's
///    data segment. Subtracting that offset gives DGROUP:0000, and the character record is a fixed
///    distance past it. A candidate is only accepted when at least
///    <see cref="CharacterFormat.MinValidators"/> further literals also line up at their own
///    expected offsets <i>and</i> the record behind them passes
///    <see cref="CharacterFormat.LooksLikeRecord"/> — so a stale copy of the header sitting in a
///    disk buffer cannot be mistaken for the running game.
///
/// 2. <b>Structural</b> (<b>opt-in</b>, for a different build whose literals have moved). Sweep for
///    any window matching <see cref="CharacterFormat.LooksLikeRecord"/>. This never runs on its own:
///    over a couple of hundred megabytes of a process that is not the game, a predicate like that
///    will eventually find some byte run that fits — it was seen offering a character called
///    <c>wwwwwwwwww</c> — and silently attaching the editor to that would let one "Max Everything"
///    click scribble into an unrelated program.
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails

    /// <summary>
    /// Finds the character record.
    ///
    /// The anchored scan is the only one run by default. The structural scan is <b>opt-in</b>
    /// (<paramref name="allowStructuralScan"/>) because its predicate, applied to a couple of
    /// hundred megabytes of a process that is not the game, will eventually find a byte run that
    /// fits — observed accepting a heap window as a character called <c>wwwwwwwwww</c> when pointed
    /// at the wrong process. Silently attaching to that would let a "Max Everything" click scribble
    /// into an unrelated program, so the shell asks first and says what it is doing.
    /// </summary>
    public static LocateResult Locate(IMemorySource mem, CancellationToken ct = default,
                                      bool allowStructuralScan = false)
    {
        ArgumentNullException.ThrowIfNull(mem);

        var anchored = LocateByAnchor(mem, ct);
        if (anchored.Found) return anchored;

        return allowStructuralScan ? LocateByStructure(mem, ct) : LocateResult.None;
    }

    // --- strategy 1: anchored on the program's own display literals -----------

    private static LocateResult LocateByAnchor(IMemorySource mem, CancellationToken ct)
    {
        var anchor = CharacterFormat.PrimaryAnchor;
        LocateResult best = LocateResult.None;

        foreach (nuint hit in Scan(mem, anchor.Bytes, ct))
        {
            ct.ThrowIfCancellationRequested();

            // hit is the literal's address; DGROUP:0000 sits that many bytes earlier.
            if (hit < (nuint)anchor.DgroupOffset) continue;
            nuint dgroup = hit - (nuint)anchor.DgroupOffset;

            int matched = CountValidators(mem, dgroup);
            if (matched < CharacterFormat.MinValidators) continue;

            nuint recordAddress = dgroup + (nuint)CharacterFormat.DgroupRecordOffset;
            var buf = ReadRecord(mem, recordAddress);
            if (buf == null) continue;

            var result = new LocateResult(recordAddress, dgroup, buf,
                $"anchored on the status-bar header at DGROUP:0x{anchor.DgroupOffset:X4}", matched);

            // Every validator matching is conclusive; otherwise keep the strongest candidate seen.
            if (matched == CharacterFormat.Validators.Length) return result;
            if (!best.Found || matched > best.ValidatorsMatched) best = result;
        }

        return best;
    }

    /// <summary>
    /// Reads a candidate record and validates its shape, returning null if it is neither readable
    /// nor plausible. <see cref="ProcessMemory.Read(nuint,int)"/> is all-or-nothing, so a full
    /// 12 KB read fails if the record sits near the end of a mapped region — in that case fall back
    /// to the live-fields prefix, which is all the trainer actually needs.
    /// </summary>
    private static byte[]? ReadRecord(IMemorySource mem, nuint address)
    {
        var buf = mem.Read(address, CharacterFormat.RecordSize);
        if (buf.Length < CharacterFormat.LiveFieldsLength)
            buf = mem.Read(address, CharacterFormat.LiveFieldsLength);
        if (buf.Length < CharacterFormat.LiveFieldsLength) return null;
        return CharacterFormat.LooksLikeRecord(buf, 0) ? buf : null;
    }

    private static int CountValidators(IMemorySource mem, nuint dgroup)
    {
        int matched = 0;
        foreach (var v in CharacterFormat.Validators)
        {
            var got = mem.Read(dgroup + (nuint)v.DgroupOffset, v.Bytes.Length);
            if (got.Length == v.Bytes.Length && got.AsSpan().SequenceEqual(v.Bytes))
                matched++;
        }
        return matched;
    }

    // --- strategy 2: structural scan for the record's shape -------------------

    private static LocateResult LocateByStructure(IMemorySource mem, CancellationToken ct)
    {
        int window = CharacterFormat.LiveFieldsLength;
        int overlap = window - 1;
        byte[] buf = new byte[ChunkSize + overlap];

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                ct.ThrowIfCancellationRequested();   // per chunk: a DOSBox region can be ~200 MiB
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
                int read = mem.Read(start, buf, readLen);

                var hit = FindRecord(mem, buf, read, start, window);
                if (hit != null) return hit.Value;

                if (read == 0 && want > PageSize)
                {
                    // The read is all-or-nothing, so one unreadable page fails the whole chunk.
                    // Salvage just this chunk a page at a time and then carry on chunked: this is
                    // the only path left when no anchor matched, so skipping a megabyte here would
                    // mean reporting "no character found" for a character that is right there —
                    // but dropping to page reads for the rest of a ~200 MiB region would cost
                    // orders of magnitude in time for one bad page.
                    nuint salvageEnd = Min(start + (nuint)readLen, regionEnd);
                    var salvaged = SalvageByPage(mem, start, salvageEnd, window, ct);
                    if (salvaged != null) return salvaged.Value;
                }

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }

        return LocateResult.None;
    }

    // Checks every offset in a freshly read window for the record's shape, confirming a candidate
    // with a direct read before accepting it.
    private static LocateResult? FindRecord(IMemorySource mem, byte[] buf, int read, nuint windowBase, int window)
    {
        for (int i = 0; i + window <= read; i++)
        {
            if (!CharacterFormat.LooksLikeRecord(buf, i)) continue;

            nuint address = windowBase + (nuint)i;
            var full = ReadRecord(mem, address);
            if (full == null) continue;

            return new LocateResult(address, 0, full,
                "structural scan (no anchor matched — a different build?)", 0);
        }
        return null;
    }

    // Walks [start, regionEnd) one page at a time, skipping only the pages that will not read.
    private static LocateResult? SalvageByPage(
        IMemorySource mem, nuint start, nuint regionEnd, int window, CancellationToken ct)
    {
        int overlap = window - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < window && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < window) continue;   // unreadable page — skip it, keep scanning

            var hit = FindRecord(mem, page, read, p, window);
            if (hit != null) return hit;
        }
        return null;
    }

    // --- byte-pattern sweep ---------------------------------------------------

    /// <summary>
    /// Yields every address in the target whose bytes match <paramref name="needle"/>. Windows are
    /// read with a needle-sized overlap so a match straddling a window edge is still seen.
    /// </summary>
    private static IEnumerable<nuint> Scan(IMemorySource mem, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] buf = new byte[ChunkSize + overlap];

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                ct.ThrowIfCancellationRequested();   // per chunk: a DOSBox region can be ~200 MiB
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
                int read = mem.Read(start, buf, readLen);

                if (read >= needle.Length)
                {
                    for (int i = 0; i + needle.Length <= read; i++)
                        if (Matches(buf, i, needle))
                            yield return start + (nuint)i;
                }
                else if (want > PageSize)
                {
                    // ProcessMemory.Read is all-or-nothing, so one unreadable page fails the whole
                    // chunk. Salvage this chunk page by page, then carry on chunked. The salvage
                    // window overlaps the next chunk by a needle length, so a match on the seam can
                    // be yielded twice — harmless, because every candidate is evaluated the same way.
                    nuint salvageEnd = Min(start + (nuint)readLen, regionEnd);
                    foreach (var hit in ScanByPage(mem, start, salvageEnd, needle, ct))
                        yield return hit;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
    }

    private static IEnumerable<nuint> ScanByPage(
        IMemorySource mem, nuint start, nuint regionEnd, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < needle.Length && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < needle.Length) continue;   // unreadable page — skip it, keep scanning

            for (int i = 0; i + needle.Length <= read; i++)
                if (Matches(page, i, needle))
                    yield return p + (nuint)i;
        }
    }

    private static nuint Min(nuint a, nuint b) => a < b ? a : b;

    private static bool Matches(byte[] buf, int i, byte[] needle)
    {
        for (int k = 0; k < needle.Length; k++)
            if (buf[i + k] != needle[k]) return false;
        return true;
    }

    /// <summary>
    /// Reads the city street map out of the attached game. Needs an anchored locate, because the map
    /// sits at a fixed offset from DGROUP and the structural fallback never learns where DGROUP is.
    /// Returns null when the map is unreadable or does not explain the known building squares.
    /// </summary>
    public static CityTerrain? ReadTerrain(IMemorySource mem, nuint dgroupAddress)
    {
        if (mem == null || dgroupAddress == 0) return null;
        long addr = (long)dgroupAddress + CharacterFormat.DgroupTerrainOffset;
        if (addr < 0) return null;
        var raw = mem.Read((nuint)addr, CityTerrain.ByteCount);
        return CityTerrain.TryParse(raw);
    }

    /// <summary>
    /// Re-reads the live fields of a located record into a caller-supplied buffer. Returns false
    /// rather than throwing when the buffer is too small — this runs on a UI timer tick, where an
    /// exception would become a message box every 600 ms.
    /// </summary>
    public static bool Reread(IMemorySource mem, nuint address, byte[] buffer)
    {
        if (mem == null || buffer == null || buffer.Length < CharacterFormat.LiveFieldsLength) return false;
        return mem.Read(address, buffer, CharacterFormat.LiveFieldsLength) == CharacterFormat.LiveFieldsLength;
    }
}
