using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.Memory;

/// <summary>A decoded snapshot of the live-position Heap at a given address.</summary>
public readonly struct HeapReading
{
    public int X { get; }
    public int Y { get; }
    public int BoardId { get; }
    public int Facing { get; }
    public int MaxX { get; }
    public int MaxY { get; }

    public HeapReading(int x, int y, int boardId, int facing, int maxX, int maxY)
    {
        X = x; Y = y; BoardId = boardId; Facing = facing; MaxX = maxX; MaxY = maxY;
    }

    /// <summary>A comparison key that changes whenever the party moves or turns.</summary>
    public int PositionKey => (Y << 16) | (X << 8) | Facing;
}

/// <summary>
/// Locates the Dragon Wars live-position "Heap" in the attached emulator's memory. Unlike the
/// character roster, the Heap sits at no stable delta from any static anchor (it could not be
/// pinned from the captured dumps), so it is found the same way MM1/BT1 find their position: an
/// initial structural scan collects every address that could plausibly be the Heap, then the user
/// takes a step in-game and <see cref="Narrow"/> discards every candidate whose position did not
/// change like the party's — repeating until a single address remains.
///
/// A candidate is "plausible" when it looks like the Heap's first bytes: a valid board id, board
/// dimensions drawn from the game's grid sizes (16/32/48), the party X/Y inside those bounds, and a
/// facing of 0..3.
/// </summary>
public static class HeapLocator
{
    private const int ChunkSize = 1 << 20;                 // 1 MiB scan window
    private const int WindowSize = 0x40;                   // bytes read to validate a candidate
    private const int PageSize = 0x1000;                   // salvage granularity when a chunk read fails
    private const int MaxCandidates = 500_000;             // safety cap on the initial sweep

    // Board dimensions are stored as the true width/height (the wrap modulus in the engine). Real
    // maps are 16/32/48, but accept the whole plausible range so an unexpected size never hides the
    // real Heap — the strong filter is that the party's X/Y must fall inside these bounds.
    private static bool IsValidDim(int d) => d >= 2 && d <= 64;

    private static bool Plausible(byte[] buf, int i)
    {
        int board = buf[i + MapBook.OffBoardId];
        if (board > MapBook.MaxBoardId) return false;
        int mx = buf[i + MapBook.OffBoardMaxX];
        int my = buf[i + MapBook.OffBoardMaxY];
        if (!IsValidDim(mx) || !IsValidDim(my)) return false;
        if (buf[i + MapBook.OffPartyX] >= mx) return false;
        if (buf[i + MapBook.OffPartyY] >= my) return false;
        if (buf[i + MapBook.OffFacing] > 3) return false;
        return true;
    }

    /// <summary>
    /// Sweeps every readable region and returns the address of every plausible Heap candidate.
    /// </summary>
    public static List<nuint> ScanCandidates(ProcessMemory mem, CancellationToken ct = default)
    {
        var hits = new List<nuint>();
        byte[] buf = new byte[ChunkSize + WindowSize];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int readWant = (int)Math.Min((nuint)(ChunkSize + WindowSize), remaining);
                int read = mem.Read(start, buf, readWant);

                if (read >= WindowSize)
                {
                    for (int i = 0; i + WindowSize <= read; i++)
                    {
                        if (Plausible(buf, i))
                        {
                            hits.Add(start + (nuint)i);
                            if (hits.Count >= MaxCandidates) return hits;
                        }
                    }
                    start += (nuint)Math.Max(PageSize, read - WindowSize + 1);
                }
                else if (readWant > PageSize)
                {
                    foreach (var hit in ScanRegionByPage(mem, start, regionEnd, ct))
                    {
                        hits.Add(hit);
                        if (hits.Count >= MaxCandidates) return hits;
                    }
                    break;
                }
                else
                {
                    break;
                }
            }
        }
        return hits;
    }

    private static IEnumerable<nuint> ScanRegionByPage(ProcessMemory mem, nuint start, nuint regionEnd, CancellationToken ct)
    {
        int overlap = WindowSize - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < WindowSize && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < WindowSize) continue;

            for (int i = 0; i + WindowSize <= read; i++)
                if (Plausible(page, i))
                    yield return p + (nuint)i;
        }
    }

    /// <summary>Reads and validates the Heap at a single address, or null if it no longer looks valid.</summary>
    public static HeapReading? Read(ProcessMemory mem, nuint address)
    {
        var buf = mem.Read(address, WindowSize);
        if (buf.Length < WindowSize) return null;
        if (!Plausible(buf, 0)) return null;
        return new HeapReading(
            buf[MapBook.OffPartyX], buf[MapBook.OffPartyY], buf[MapBook.OffBoardId],
            buf[MapBook.OffFacing], buf[MapBook.OffBoardMaxX], buf[MapBook.OffBoardMaxY]);
    }

    /// <summary>
    /// Narrows a candidate set after the party has moved/turned: keeps only addresses that are
    /// still a plausible Heap AND whose position key changed since <paramref name="previous"/>
    /// recorded it (i.e. they moved like the party). Returns the surviving candidates and their
    /// fresh readings.
    /// </summary>
    public static List<(nuint Address, HeapReading Reading)> Narrow(
        ProcessMemory mem, IEnumerable<(nuint Address, HeapReading Reading)> previous,
        CancellationToken ct = default)
    {
        var survivors = new List<(nuint, HeapReading)>();
        foreach (var (addr, before) in previous)
        {
            ct.ThrowIfCancellationRequested();
            var now = Read(mem, addr);
            if (now is null) continue;
            if (now.Value.PositionKey == before.PositionKey) continue;   // didn't move — not the party
            survivors.Add((addr, now.Value));
        }
        return survivors;
    }

    /// <summary>
    /// Narrows a candidate set after the party walked a known number of squares in a straight line:
    /// keeps only addresses whose position shifted by exactly <paramref name="steps"/> on a single
    /// axis (X or Y, not both) since <paramref name="baseline"/> was captured. Dragon Wars moves the
    /// party forward relative to its facing, so a straight-line walk always changes exactly one axis;
    /// requiring single-axis movement rejects false candidates whose unrelated fields happened to
    /// shift diagonally by the same total distance. Distances are measured on the wrapping grid
    /// (walking off one edge reappears on the other), and any candidate whose board id changed is
    /// dropped, since leaving the map through a door randomises the coordinates and would otherwise
    /// lock a false address. Returns the survivors with fresh readings, ready to serve as the next
    /// baseline.
    /// </summary>
    public static List<(nuint Address, HeapReading Reading)> NarrowBySteps(
        ProcessMemory mem, IEnumerable<(nuint Address, HeapReading Reading)> baseline,
        int steps, CancellationToken ct = default)
    {
        var survivors = new List<(nuint, HeapReading)>();
        foreach (var (addr, before) in baseline)
        {
            ct.ThrowIfCancellationRequested();
            var now = Read(mem, addr);
            if (now is null) continue;
            if (now.Value.BoardId != before.BoardId) continue;   // walked off the map — not a straight-line step
            int dx = WrappedDelta(now.Value.X, before.X, before.MaxX);
            int dy = WrappedDelta(now.Value.Y, before.Y, before.MaxY);
            bool straightMove = (dx == steps && dy == 0) || (dx == 0 && dy == steps);
            if (!straightMove) continue;
            survivors.Add((addr, now.Value));
        }
        return survivors;
    }

    /// <summary>Shortest distance between two coordinates on an axis that wraps at <paramref name="size"/>.</summary>
    private static int WrappedDelta(int now, int before, int size)
    {
        int d = Math.Abs(now - before);
        if (size > 0 && d > size - d) d = size - d;
        return d;
    }
}
