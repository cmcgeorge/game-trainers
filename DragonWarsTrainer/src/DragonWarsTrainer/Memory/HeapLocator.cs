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
    private const int MaxCandidates = 500_000;             // safety cap on the initial sweep

    private static bool IsValidDim(int d) => d == 16 || d == 32 || d == 48;

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
            for (nuint offset = 0; offset < region.Size;)
            {
                int readWant = (int)Math.Min((nuint)(ChunkSize + WindowSize), region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, readWant);
                if (read < WindowSize) break;

                for (int i = 0; i + WindowSize <= read; i++)
                {
                    if (Plausible(buf, i))
                    {
                        hits.Add(region.Base + offset + (nuint)i);
                        if (hits.Count >= MaxCandidates) return hits;
                    }
                }

                nuint advance = (nuint)Math.Max(1, read - WindowSize + 1);
                offset += advance;
            }
        }
        return hits;
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
}
