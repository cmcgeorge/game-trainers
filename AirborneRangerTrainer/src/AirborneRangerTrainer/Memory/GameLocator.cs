using AirborneRangerTrainer.Game;

namespace AirborneRangerTrainer.Memory;

/// <summary>The outcome of an auto-locate.</summary>
/// <param name="DgroupAddress">Live address of <c>DGROUP:0000</c>, or 0 when nothing was found.</param>
/// <param name="Window">A copy of the mission-state window, or an empty array.</param>
/// <param name="Method">How it was found — shown to the user so a weak hit is obvious.</param>
/// <param name="ValidatorsMatched">How many corroborating literals lined up.</param>
/// <param name="RejectedAddress">
/// Set when the anchors matched at this address but the mission state behind them was not
/// plausible. That is a very different failure from "the game is not here", and the two need
/// different advice: this one means the game <i>was</i> found and is simply not in a mission.
/// </param>
public readonly record struct LocateResult(
    nuint DgroupAddress,
    byte[] Window,
    string Method,
    int ValidatorsMatched,
    nuint RejectedAddress = 0)
{
    /// <summary>True when a usable data segment was found.</summary>
    public bool Found => DgroupAddress != 0 && Window is { Length: >= MissionFormat.WindowLength };

    /// <summary>True when the anchors matched somewhere but the state behind them was rejected.</summary>
    public bool AnchorsMatchedButStateDidNot => !Found && RejectedAddress != 0;

    /// <summary>The "nothing found" result.</summary>
    public static LocateResult None => new(0, Array.Empty<byte>(), "not found", 0);
}

/// <summary>
/// Finds the running game's data segment inside the attached emulator's memory. Nothing is
/// hard-coded to an address: DOS relocates the program, so <c>DGROUP</c> lands somewhere different
/// every session and has to be found from scratch each time.
///
/// <para>There is only one strategy, and it needs no value scanning. <c>AR.EXE</c> is a
/// medium-model program with a single data segment, so every global sits at a constant
/// <c>DGROUP</c> offset. Sweep the emulator for the status panel's own caption
/// <c>CARBINE MAGS</c>, which sits at <c>DGROUP:0xB923</c>; subtracting that offset gives
/// <c>DGROUP:0000</c>. A candidate is accepted only when at least
/// <see cref="MissionFormat.MinValidators"/> further literals — the rank table, the decoration line,
/// the mission list and the version string — also line up at their own known offsets, and the
/// mission-state window behind them passes
/// <see cref="MissionFormat.LooksLikeMissionState"/>.</para>
///
/// <para>There is deliberately <b>no structural fallback</b>. Between missions the mission-state
/// block holds whatever the last one left behind, and on a fresh run it is all zeros, so it has no
/// shape distinctive enough to scan for — a structural sweep would confidently return a wrong
/// address. Four independent literals in one 59 KB segment is the stronger evidence, and if a
/// different build moved them the honest answer is "not found".</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails

    /// <summary>Runs the anchored scan and returns the best candidate.</summary>
    public static LocateResult Locate(IMemorySource mem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);

        var anchor = MissionFormat.PrimaryAnchor;
        LocateResult best = LocateResult.None;
        nuint rejected = 0;

        foreach (nuint hit in Scan(mem, anchor.Bytes, ct))
        {
            ct.ThrowIfCancellationRequested();

            // hit is the literal's address; DGROUP:0000 sits that many bytes earlier.
            if (hit < (nuint)anchor.DgroupOffset) continue;
            nuint dgroup = hit - (nuint)anchor.DgroupOffset;

            int matched = CountValidators(mem, dgroup);
            if (matched < MissionFormat.MinValidators) continue;

            var window = ReadWindow(mem, dgroup, out bool readable);
            if (window == null)
            {
                // The anchors are there and the block behind them read, but it is not plausible
                // mission state. Remember it: telling the user "not found" here would send them off
                // to check that the game is running, when in fact it is running and simply between
                // missions. An unreadable window gets no such claim — that address is not the game.
                if (readable) rejected = dgroup;
                continue;
            }

            var result = new LocateResult(dgroup, window,
                $"anchored on the status-panel caption at DGROUP:0x{anchor.DgroupOffset:X4}", matched);

            // Every validator matching is conclusive; otherwise keep the strongest candidate seen,
            // because a stale copy of the caption with a couple of coincidental matches can appear
            // at a lower address than the live segment and would otherwise win on scan order alone.
            if (matched == MissionFormat.Validators.Length) return result;
            if (!best.Found || matched > best.ValidatorsMatched) best = result;
        }

        return best.Found ? best : new LocateResult(0, Array.Empty<byte>(), "not found", 0, rejected);
    }

    /// <summary>
    /// Reads the mission-state window at a candidate <c>DGROUP</c>.
    /// </summary>
    /// <param name="readable">
    /// True when the window could be read at all. The two failures have to stay distinct: a window
    /// that read but did not look like mission state means the game <i>is</i> there and simply is
    /// not in a mission, whereas one that would not read at all means this address is not the game —
    /// and telling a user to go and start a mission is useless advice for the second.
    /// </param>
    private static byte[]? ReadWindow(IMemorySource mem, nuint dgroup, out bool readable)
    {
        var buf = mem.Read(dgroup + (nuint)MissionFormat.WindowStart, MissionFormat.WindowLength);
        readable = buf.Length >= MissionFormat.WindowLength;
        if (!readable) return null;
        return MissionFormat.LooksLikeMissionState(buf) ? buf : null;
    }

    private static int CountValidators(IMemorySource mem, nuint dgroup)
    {
        int matched = 0;
        foreach (var v in MissionFormat.Validators)
        {
            var got = mem.Read(dgroup + (nuint)v.DgroupOffset, v.Bytes.Length);
            if (got.Length == v.Bytes.Length && got.AsSpan().SequenceEqual(v.Bytes))
                matched++;
        }
        return matched;
    }

    /// <summary>
    /// Re-reads the mission-state window into a caller-supplied buffer. Returns false rather than
    /// throwing when the buffer is too small or the read fails — this runs on a UI timer tick, where
    /// an exception would become a message box several times a second.
    /// </summary>
    public static bool Reread(IMemorySource mem, nuint dgroup, byte[] buffer)
    {
        if (mem == null || dgroup == 0 || buffer == null || buffer.Length < MissionFormat.WindowLength)
            return false;
        return mem.Read(dgroup + (nuint)MissionFormat.WindowStart, buffer, MissionFormat.WindowLength)
               == MissionFormat.WindowLength;
    }

    /// <summary>
    /// Reads the status-panel text template — the panel exactly as the game last rendered it.
    /// Returns null when it cannot be read.
    /// </summary>
    public static byte[]? ReadStatusPanel(IMemorySource mem, nuint dgroup)
    {
        if (mem == null || dgroup == 0) return null;
        var buf = mem.Read(dgroup + (nuint)MissionFormat.OffStatusPanel, MissionFormat.StatusPanelLength);
        return buf.Length == MissionFormat.StatusPanelLength ? buf : null;
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
