using JumpmanLivesTrainer.Game;

namespace JumpmanLivesTrainer.Memory;

/// <summary>The outcome of an auto-locate.</summary>
/// <param name="DgroupAddress">Live address of DGROUP:0000, or 0 when nothing was found.</param>
/// <param name="Globals">A copy of the globals window, or an empty array.</param>
/// <param name="Method">How it was found — shown to the user.</param>
/// <param name="ValidatorsMatched">How many corroborating patterns lined up.</param>
/// <param name="RejectedAddress">Set when the anchors matched but the globals were not plausible.</param>
public readonly record struct LocateResult(
    nuint DgroupAddress,
    byte[] Globals,
    string Method,
    int ValidatorsMatched,
    nuint RejectedAddress = 0)
{
    /// <summary>True when a usable data segment was found.</summary>
    public bool Found => DgroupAddress != 0 && Globals is { Length: >= GameLayout.GlobalWindowLength };

    /// <summary>True when the anchors matched somewhere but the globals were rejected.</summary>
    public bool AnchorsMatchedButGlobalsDidNot => !Found && RejectedAddress != 0;

    /// <summary>The "nothing found" result.</summary>
    public static LocateResult None => new(0, Array.Empty<byte>(), "not found", 0);
}

/// <summary>
/// Finds the running game's data segment inside the attached emulator's memory. Nothing is
/// hard-coded to an address: DOS relocates the program, so DGROUP lands somewhere different every
/// session and has to be found from scratch each time.
///
/// <para>There is only one strategy, and it needs no value scanning. <c>JMAN2.EXE</c> is a Turbo
/// Pascal 6.0 program with a single data segment, so every global sits at a constant DGROUP offset.
/// Sweep the emulator for the 22-byte <c>jp1</c> jump-trajectory table at <c>DGROUP:0x7D46</c>;
/// subtracting that offset gives <c>DGROUP:0000</c>. A candidate is accepted only when at least
/// <see cref="GameLayout.MinValidators"/> further patterns — <c>PLAYSPEED</c> and <c>ftwo</c> — also
/// line up at their own known offsets, and the globals behind them pass
/// <see cref="GameLayout.IsPlausibleGlobals"/>.</para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails

    /// <summary>Runs the anchored scan and returns the best candidate.</summary>
    public static LocateResult Locate(IMemorySource mem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);

        var anchor = GameLayout.AnchorBytes;
        nuint rejected = 0;

        foreach (nuint hit in Scan(mem, anchor, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (hit < (nuint)GameLayout.AnchorOffset) continue;
            nuint dgroup = hit - (nuint)GameLayout.AnchorOffset;

            int matched = CountValidators(mem, dgroup);
            if (matched < GameLayout.MinValidators) continue;

            var globals = ReadGlobals(mem, dgroup, out bool readable);
            if (globals == null)
            {
                if (readable) rejected = dgroup;
                continue;
            }

            return new LocateResult(dgroup, globals,
                $"anchored on the jp1 jump table at DGROUP:0x{GameLayout.AnchorOffset:X4}", matched);
        }

        return new LocateResult(0, Array.Empty<byte>(), "not found", 0, rejected);
    }

    private static byte[]? ReadGlobals(IMemorySource mem, nuint dgroup, out bool readable)
    {
        var buf = mem.Read(dgroup + (nuint)GameLayout.GlobalWindowStart, GameLayout.GlobalWindowLength);
        readable = buf.Length >= GameLayout.GlobalWindowLength;
        if (!readable) return null;

        if (!GameLayout.ValidateGlobals(buf)) return null;
        return GameLayout.IsPlausibleGlobals(buf) ? buf : null;
    }

    private static int CountValidators(IMemorySource mem, nuint dgroup)
    {
        int matched = 0;

        var ps = mem.Read(dgroup + (nuint)GameLayout.PlayspeedOffset, GameLayout.PlayspeedBytes.Length);
        if (ps.Length == GameLayout.PlayspeedBytes.Length && ps.AsSpan().SequenceEqual(GameLayout.PlayspeedBytes))
            matched++;

        var ft = mem.Read(dgroup + (nuint)GameLayout.FtwoOffset, GameLayout.FtwoBytes.Length);
        if (ft.Length == GameLayout.FtwoBytes.Length && ft.AsSpan().SequenceEqual(GameLayout.FtwoBytes))
            matched++;

        return matched;
    }

    /// <summary>Re-reads the globals window into a caller-supplied buffer.</summary>
    public static bool RereadGlobals(IMemorySource mem, nuint dgroup, byte[] buffer)
    {
        if (mem == null || dgroup == 0 || buffer == null || buffer.Length < GameLayout.GlobalWindowLength)
            return false;
        return mem.Read(dgroup + (nuint)GameLayout.GlobalWindowStart, buffer, GameLayout.GlobalWindowLength)
               == GameLayout.GlobalWindowLength;
    }

    /// <summary>Reads the current player's 92-byte record. Returns null if the read fails.</summary>
    public static byte[]? ReadPlayer(IMemorySource mem, nuint dgroup, int playerIndex)
    {
        if (mem == null || dgroup == 0 || playerIndex is < 1 or > GameLayout.MaxActivePlayers) return null;
        nuint addr = dgroup + (nuint)GameLayout.PlayerArrayOffset
                     + (nuint)((playerIndex - 1) * GameLayout.PlayerRecordSize);
        var buf = mem.Read(addr, GameLayout.PlayerRecordSize);
        return buf.Length == GameLayout.PlayerRecordSize ? buf : null;
    }

    /// <summary>Reads the <c>pl</c> byte (current player index). Returns 0 if the read fails.</summary>
    public static int ReadPl(IMemorySource mem, nuint dgroup)
    {
        if (mem == null || dgroup == 0) return 0;
        var buf = mem.Read(dgroup + (nuint)GameLayout.OffPl, 1);
        return buf.Length == 1 ? buf[0] : 0;
    }

    // --- byte-pattern sweep ---------------------------------------------------

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
                ct.ThrowIfCancellationRequested();
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
            if (read < needle.Length) continue;

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
