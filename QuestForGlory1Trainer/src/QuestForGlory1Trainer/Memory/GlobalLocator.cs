namespace QuestForGlory1Trainer.Memory;

/// <summary>
/// Addresses and snapshot values for the five confirmed SCI0 global variables in QFG1.
/// </summary>
public sealed class LocatedGlobals
{
    /// <summary>Address of g[1] — current room number (16-bit word).</summary>
    public nuint RoomAddress { get; }

    /// <summary>Address of g[3] — game day, 1-based (16-bit word).</summary>
    public nuint DayAddress { get; }

    /// <summary>Address of g[4] — game clock ticks within the current day, 0–3599 (16-bit word).</summary>
    public nuint ClockAddress { get; }

    /// <summary>Room number read at locate time.</summary>
    public int Room { get; }

    /// <summary>Game day read at locate time.</summary>
    public int Day { get; }

    /// <summary>Clock tick read at locate time.</summary>
    public int Clock { get; }

    internal LocatedGlobals(nuint g0Addr, int room, int day, int clock)
    {
        RoomAddress  = g0Addr + 2;   // g[1]
        DayAddress   = g0Addr + 6;   // g[3]
        ClockAddress = g0Addr + 8;   // g[4]
        Room  = room;
        Day   = day;
        Clock = clock;
    }
}

/// <summary>
/// Locates the SCI0 global-variable array for Quest for Glory I by scanning for the confirmed
/// four-word layout starting at g[1]:
/// <code>
///   g[0]: Ego object selector  (session-variable; not used as a filter)
///   g[1]: Current room number  (1–200)
///   g[2]: Previous room number (0–200)
///   g[3]: Game day             (1–9999, no hard cap in QFG1)
///   g[4]: Game clock ticks     (0–3599)
/// </code>
/// g[0] holds the SCI0 script-segment selector for the Ego object.  Dump analysis confirmed
/// it is NOT a compile-time constant — it varied between sessions (88 in one session, 208 in
/// another) and is therefore not a reliable anchor.  The scan relies instead on the tight
/// combined range constraints on g[1]–g[4] plus a 200 ms stability window that confirms g[4]
/// (clock) has advanced by at least two ticks; the real SCI0 game clock advances at the
/// interpreter-cycle rate (~15–60 Hz) whereas observed false-positive counters advance at
/// &lt;2 ticks/200 ms and therefore fail the gate.
///
/// When a <paramref name="hintAddress"/> is provided (the address of the stat block found by
/// <see cref="StatLocator"/>), only the memory region that contains that address is scanned —
/// the same 16 MiB DOSBox guest-RAM page that holds both the SCI heap and the stat block.
/// </summary>
public static class GlobalLocator
{
    private const int  BlockWords       = 5;
    private const int  BlockBytes       = BlockWords * 2;
    private const int  ChunkSize        = 1 << 20;
    private const int  PageSize         = 0x1000;
    private const long MinRegionBytes   = 2 * 1024 * 1024;  // 2 MiB — DOSBox guest RAM is ≥16 MiB; false-positive regions are ≤1.8 MiB
    private const int  StabilityDelayMs   = 200;
    private const int  MinTickAdvance     = 2;    // real SCI0 clock: ≥15 ticks/s; false positives: <2 ticks per 200 ms

    /// <summary>
    /// Scans for the SCI0 global-variable block. When <paramref name="hintAddress"/> is
    /// non-zero the scan is restricted to the single memory region that contains that address
    /// (typically the stat block address from <see cref="StatLocator.Find"/>).
    /// </summary>
    public static LocatedGlobals? Find(ProcessMemory mem, nuint hintAddress = default,
                                       CancellationToken ct = default)
    {
        byte[] buf = new byte[ChunkSize + BlockBytes];
        var candidates = new List<LocatedGlobals>();

        foreach (var region in mem.EnumerateRegions())
        {
            if ((long)region.Size < MinRegionBytes) continue;

            nuint regionEnd = region.Base + region.Size;

            if (hintAddress != default &&
                (hintAddress < region.Base || hintAddress >= regionEnd))
                continue;

            ct.ThrowIfCancellationRequested();

            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want    = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + BlockBytes), remaining);
                int read    = mem.Read(start, buf, readLen);

                if (read >= BlockBytes)
                {
                    int limit = read - BlockBytes + 1;
                    for (int i = 0; i < limit; i += 2)
                    {
                        var hit = TryValidate(buf, i, read, start);
                        if (hit != null) candidates.Add(hit);
                    }
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }

        if (candidates.Count == 0) return null;

        Thread.Sleep(StabilityDelayMs);
        ct.ThrowIfCancellationRequested();

        byte[] confirm = new byte[2];
        LocatedGlobals? best      = null;
        int             bestDelta = 0;

        foreach (var c in candidates)
        {
            if (mem.Read(c.ClockAddress, confirm, 2) < 2) continue;
            short clock2 = (short)(confirm[0] | (confirm[1] << 8));
            if (clock2 < 0 || clock2 > 3599) continue;

            if (mem.Read(c.DayAddress, confirm, 2) < 2) continue;
            short day2 = (short)(confirm[0] | (confirm[1] << 8));
            if (day2 < 1) continue;

            int delta = clock2 >= c.Clock
                ? clock2 - c.Clock
                : (3600 - c.Clock) + clock2;   // day-boundary wrap

            if (delta < MinTickAdvance) continue;

            if (best == null || delta > bestDelta)
            {
                best      = c;
                bestDelta = delta;
            }
        }

        return best;
    }

    // ---- helpers ------------------------------------------------------------

    private static short Read16(byte[] buf, int off)
        => (short)(buf[off] | (buf[off + 1] << 8));

    private static LocatedGlobals? TryValidate(byte[] buf, int offset, int read, nuint windowBase)
    {
        if (offset + BlockBytes > read) return null;

        short g1 = Read16(buf, offset + 2);   // room
        short g2 = Read16(buf, offset + 4);   // previous room
        short g3 = Read16(buf, offset + 6);   // day
        short g4 = Read16(buf, offset + 8);   // clock ticks

        if (g1 < 1   || g1 > 200)  return null;
        if (g2 < 0   || g2 > 200)  return null;
        if (g3 < 1   || g3 > 9999) return null;
        if (g4 < 0   || g4 > 3599) return null;

        nuint g0Addr = windowBase + (nuint)offset;
        return new LocatedGlobals(g0Addr, g1, g3, g4);
    }
}
