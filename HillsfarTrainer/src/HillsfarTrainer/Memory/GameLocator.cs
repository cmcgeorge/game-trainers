using HillsfarTrainer.Game;

namespace HillsfarTrainer.Memory;

/// <summary>The outcome of an auto-locate.</summary>
/// <param name="DgroupAddress">Live address of <c>DGROUP:0000</c>, or 0 when nothing was found.</param>
/// <param name="Record">A copy of the 188-byte character record, or an empty array.</param>
/// <param name="Method">How it was found — shown to the user so a weak hit is obvious.</param>
/// <param name="ValidatorsMatched">How many corroborating literals lined up.</param>
/// <param name="RejectedAddress">
/// Set when the anchors matched at this address but the record behind them was not plausible. That is
/// a very different failure from "the game is not here", and the two need different advice: this one
/// means the game <i>was</i> found and simply has no character loaded yet.
/// </param>
/// <param name="TextTableMatchesShipped">
/// False when the game's digraph table could be read but differs from the one this build was written
/// against — a cheap signal that the attached game is a different release, and therefore that the
/// hard-coded record offsets may not hold. Null when the table could not be read at all.
/// </param>
public readonly record struct LocateResult(
    nuint DgroupAddress,
    byte[] Record,
    string Method,
    int ValidatorsMatched,
    nuint RejectedAddress = 0,
    bool? TextTableMatchesShipped = null)
{
    /// <summary>True when a usable data segment and character record were found.</summary>
    public bool Found =>
        DgroupAddress != 0 && Record is { Length: >= CharacterFormat.RecordLength };

    /// <summary>True when the anchors matched somewhere but the record behind them was rejected.</summary>
    public bool AnchorsMatchedButRecordDidNot => !Found && RejectedAddress != 0;

    /// <summary>Live address of the character record.</summary>
    public nuint RecordAddress => DgroupAddress + CharacterFormat.DgroupRecordOffset;

    /// <summary>The "nothing found" result.</summary>
    public static LocateResult None => new(0, Array.Empty<byte>(), "not found", 0);
}

/// <summary>
/// Finds the running game's data segment inside the attached emulator's memory. Nothing is
/// hard-coded to an address: DOS relocates the program, so <c>DGROUP</c> lands somewhere different
/// every session — measured at <c>0x76181E0</c> and <c>0x6D7F1E0</c> in two consecutive runs of the
/// same build — and has to be found from scratch each time.
///
/// <para>There is one strategy and it needs no value scanning at all. The twice-unpacked
/// <c>MAIN.EXE</c> is a Microsoft C program with a single data group, so every global sits at a
/// constant <c>DGROUP</c> offset. Sweep the emulator for the game's own 69-byte startup banner, which
/// sits at <c>DGROUP:0x0D1A</c>; subtracting that offset gives <c>DGROUP:0000</c>. A candidate is
/// accepted only when at least <see cref="CharacterFormat.MinValidators"/> further literals also line
/// up at their own known offsets <b>and</b> the record at <c>DGROUP:0x094C</c> passes
/// <see cref="CharacterFormat.LooksLikeRecord"/>. All five literals were checked against a live
/// 16 MB guest and each occurs exactly once.</para>
///
/// <para>There is deliberately <b>no structural fallback</b>. The record has a name string and
/// plausible attribute bytes, and run against a process that is not the game that shape will
/// eventually match some unrelated byte run — a confident wrong address here means a "Max
/// everything" click scribbling into another program's memory. Five independent literals inside one
/// 45 KB segment is far stronger evidence, and if a different build moves them the honest answer is
/// "not found".</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails

    /// <summary>Runs the anchored scan and returns the best candidate.</summary>
    public static LocateResult Locate(IMemorySource mem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);

        var anchor = CharacterFormat.PrimaryAnchor;
        LocateResult best = LocateResult.None;
        nuint rejected = 0;

        foreach (nuint hit in Scan(mem, anchor.Bytes, ct))
        {
            ct.ThrowIfCancellationRequested();

            // hit is the banner's address; DGROUP:0000 sits that many bytes earlier.
            if (hit < (nuint)anchor.DgroupOffset) continue;
            nuint dgroup = hit - (nuint)anchor.DgroupOffset;

            int matched = CountValidators(mem, dgroup);
            if (matched < CharacterFormat.MinValidators) continue;

            var record = ReadRecord(mem, dgroup, out bool readable);
            if (record == null)
            {
                // The anchors are there and the window read, but it is not a plausible character
                // record. Remember it: saying "not found" here would send the user off to check that
                // the game is running, when in fact it is running and simply has no character loaded.
                if (readable) rejected = dgroup;
                continue;
            }

            var result = new LocateResult(dgroup, record,
                $"anchored on the {anchor.Description}", matched, 0,
                CompareTextTable(mem, dgroup));

            // Every validator matching is conclusive. Otherwise keep the strongest candidate, because
            // a stale copy of the banner with a couple of coincidental matches can sit at a lower
            // address than the live segment and would otherwise win on scan order alone.
            if (matched == CharacterFormat.Validators.Length) return result;
            if (!best.Found || matched > best.ValidatorsMatched) best = result;
        }

        return best.Found
            ? best
            : new LocateResult(0, Array.Empty<byte>(), "not found", 0, rejected);
    }

    /// <summary>
    /// Reads the character record at a candidate <c>DGROUP</c>.
    /// </summary>
    /// <param name="readable">
    /// True when the window could be read at all. The two failures have to stay distinct: a window
    /// that read but did not look like a character means the game <i>is</i> there and simply has no
    /// character loaded, whereas one that would not read at all means this address is not the game —
    /// and "load a character and try again" is useless advice for the second.
    /// </param>
    private static byte[]? ReadRecord(IMemorySource mem, nuint dgroup, out bool readable)
    {
        var buf = mem.Read(dgroup + CharacterFormat.DgroupRecordOffset, CharacterFormat.RecordLength);
        readable = buf.Length >= CharacterFormat.RecordLength;
        if (!readable) return null;
        return CharacterFormat.LooksLikeRecord(buf) ? buf : null;
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

    /// <summary>
    /// Re-reads the character record into a caller-supplied buffer. Returns false rather than throwing
    /// when the buffer is too small or the read fails — this runs on a UI timer tick, where an
    /// exception would become a message box several times a second.
    /// </summary>
    public static bool Reread(IMemorySource mem, nuint dgroup, byte[] buffer)
    {
        if (mem == null || dgroup == 0 || buffer == null ||
            buffer.Length < CharacterFormat.RecordLength) return false;
        return mem.Read(dgroup + CharacterFormat.DgroupRecordOffset, buffer,
                        CharacterFormat.RecordLength) == CharacterFormat.RecordLength;
    }

    /// <summary>
    /// Reads the game's own digraph table so text decodes correctly even on a build whose table
    /// differs. Returns null when it cannot be read, and callers fall back to the shipped table.
    /// </summary>
    public static byte[]? ReadTextTable(IMemorySource mem, nuint dgroup)
    {
        if (mem == null || dgroup == 0) return null;
        var buf = mem.Read(dgroup + TextCodec.DgroupTableOffset, TextCodec.TableLength);
        return buf.Length == TextCodec.TableLength ? buf : null;
    }

    /// <summary>
    /// Compares the attached game's digraph table against the one this build was written against.
    /// Null when it could not be read.
    ///
    /// <para>This is a build-mismatch canary. The table is 144 bytes of pure data at a fixed
    /// <c>DGROUP</c> offset, so if it differs the attached game is a different release — and the
    /// record offsets, which are hard-coded, may not hold. Cheaper and far more specific than
    /// noticing later that a field reads nonsense.</para>
    /// </summary>
    private static bool? CompareTextTable(IMemorySource mem, nuint dgroup)
    {
        var live = ReadTextTable(mem, dgroup);
        if (live == null) return null;
        return live.AsSpan().SequenceEqual(TextCodec.ShippedTable);
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

        foreach (var region in Coalesce(mem.EnumerateRegions()))
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            // A region ending exactly at the top of the address space wraps to 0, which would make the
            // loop below skip it entirely — a silent false negative in the one file where an
            // arithmetic slip means writing to the wrong address. Saturate instead.
            if (regionEnd < region.Base) regionEnd = nuint.MaxValue;
            for (nuint start = region.Base; start < regionEnd;)
            {
                ct.ThrowIfCancellationRequested();   // per chunk: a DOSBox region can be ~16 MiB
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

                start += (nuint)Math.Max(PageSize, want);   // next window; overlap re-covers the seam
            }
        }
    }

    /// <summary>
    /// Merges regions that touch, so a match straddling the boundary between two adjacent commits is
    /// still found.
    ///
    /// <para>The needle-sized overlap only covers seams <i>within</i> a region, and
    /// <c>VirtualQueryEx</c> reports adjacent committed pages as separate regions whenever their
    /// protection differs. Without this, a banner that happened to span such a boundary would make the
    /// sweep return "not found" — technically the honest answer, but here a false negative.</para>
    ///
    /// <para>Public so the verification harness can drive it directly; it is otherwise an
    /// implementation detail of <see cref="Locate"/>.</para>
    /// </summary>
    public static IEnumerable<MemoryRegion> Coalesce(IEnumerable<MemoryRegion> regions)
    {
        MemoryRegion? pending = null;
        foreach (var r in regions)
        {
            if (r.Size == 0) continue;
            if (pending is null) { pending = r; continue; }

            var p = pending.Value;
            // Only merge when neither the existing end nor the merged size wraps.
            nuint end = p.Base + p.Size;
            if (end >= p.Base && end == r.Base && p.Size + r.Size >= p.Size)
            {
                pending = new MemoryRegion(p.Base, p.Size + r.Size);   // contiguous — extend
            }
            else
            {
                yield return p;
                pending = r;
            }
        }
        if (pending is not null) yield return pending.Value;
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
}
