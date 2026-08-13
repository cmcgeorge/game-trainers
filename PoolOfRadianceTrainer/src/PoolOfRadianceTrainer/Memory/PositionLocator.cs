namespace PoolOfRadianceTrainer.Memory;

/// <summary>How the game holds the party's position for the map it is currently running.</summary>
public enum PositionEncoding
{
    /// <summary>Indoors: three adjacent bytes, [X][Y][Facing] (Gold Box facing 0=N 1=E 2=S 3=W).</summary>
    AdjacentBytes,

    /// <summary>
    /// Wilderness: two adjacent little-endian 16-bit words, [X][Y]. Y is the number the game prints;
    /// X is printed with a constant bias added (13 in the session this was recovered from), so the
    /// bias is measured per lock rather than hard-coded — see <see cref="PositionCandidate.XBias"/>.
    /// </summary>
    WildernessWords,
}

/// <summary>
/// One address that still explains every coordinate the user has entered, together with how to read
/// it. <paramref name="XBias"/> is what must be *added* to the stored X to get the number the game
/// prints (always 0 for <see cref="PositionEncoding.AdjacentBytes"/>).
/// </summary>
public readonly record struct PositionCandidate(nuint Address, PositionEncoding Encoding, int XBias);

/// <summary>
/// Locates the party's map position in the attached emulator's memory by scanning for the current
/// coordinates and then narrowing after the party moves.
///
/// <para>The position has no stable anchor (it is not in the character record and the address
/// changes every DOSBox session), so it is found the way Dragon Wars finds its Heap: an initial scan
/// collects every address that could hold the current coordinates, the user walks to a different
/// square, and <see cref="Narrow"/> drops every candidate that no longer predicts them — repeating
/// until a single address remains.</para>
///
/// <para><b>Two encodings, because the game uses two.</b> Indoors the coordinates are adjacent bytes
/// followed by the facing. In the wilderness they are a pair of 16-bit words inside the block the
/// game writes to <c>SAVGAM?.DAT</c>, and the X word is <i>not</i> the number on screen — it is
/// short by a constant. That is why scanning for the byte pair alone never locked out there: with
/// the party standing on (26, 27), the byte pair 26,27 does not occur anywhere in the emulated
/// guest's 16 MB. Both patterns are collected in one pass and the wrong ones die at the first
/// <see cref="Narrow"/>, so the caller never has to know which map it is on. Full derivation:
/// <c>docs/reverse-engineering.md</c> §7b.</para>
/// </summary>
public static class PositionLocator
{
    private const int ChunkSize = 1 << 20;     // 1 MiB scan window
    private const int MaxHits   = 2_000_000;   // cap so a very common pattern never stalls the UI

    /// <summary>
    /// How far the stored wilderness X may sit from the printed one. The bias observed live is 13;
    /// the window is generous because a candidate whose bias is wrong is dropped by the first
    /// <see cref="Narrow"/> anyway, and a too-tight window would miss the real address outright.
    /// </summary>
    private const int MaxXBias = 64;

    /// <summary>The bytes each encoding needs to read past its address.</summary>
    private static int Footprint(PositionEncoding e) => e == PositionEncoding.WildernessWords ? 4 : 3;

    /// <summary>The signed little-endian 16-bit word at <paramref name="i"/>.</summary>
    private static int Word(byte[] buf, int i) => (short)(buf[i] | (buf[i + 1] << 8));

    /// <summary>
    /// Scans all committed regions for every address that could hold the coordinates (x, y), in
    /// either encoding. Returns at most <see cref="MaxHits"/>; the caller should narrow the list
    /// before it grows too large.
    /// </summary>
    public static List<PositionCandidate> ScanCandidates(ProcessMemory mem, int x, int y, CancellationToken ct)
    {
        var hits = new List<PositionCandidate>();
        byte bx = (byte)x, by = (byte)y;
        const int Overlap = 4;                 // longest pattern, so none is missed at a chunk seam
        byte[] buf = new byte[ChunkSize + Overlap];

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            for (nuint offset = 0; offset < region.Size;)
            {
                int readWant = (int)Math.Min((nuint)(ChunkSize + Overlap), region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, readWant);
                if (read < 2) break;

                // Chunks overlap by Overlap-1 bytes so a pattern straddling the seam is still seen
                // whole. Each address must be reported once, though — a duplicated hit would survive
                // narrowing as two candidates and the lock would never fall to one — so a chunk only
                // emits the addresses it owns, and leaves the tail to the chunk that re-reads it.
                bool lastChunk = offset + (nuint)read >= region.Size;
                int advance = Math.Max(1, read - Overlap + 1);
                int owned = lastChunk ? read : advance;

                for (int i = 0; i < owned && i < read - 1; i++)
                {
                    // Indoors: [X][Y] as bytes.
                    if (buf[i] == bx && buf[i + 1] == by)
                    {
                        hits.Add(new PositionCandidate(region.Base + offset + (nuint)i,
                                                       PositionEncoding.AdjacentBytes, 0));
                        if (hits.Count >= MaxHits) return hits;
                    }

                    // Wilderness: [X][Y] as little-endian words, X short by an unknown constant.
                    // Y is a row index and always positive, but X carries a bias, so a square west
                    // of it stores a negative word — read it signed or the map's whole western half
                    // becomes unreachable.
                    if (i + 3 < read && buf[i + 3] == 0 && buf[i + 2] == by)
                    {
                        int bias = x - Word(buf, i);
                        if (Math.Abs(bias) <= MaxXBias)
                        {
                            hits.Add(new PositionCandidate(region.Base + offset + (nuint)i,
                                                           PositionEncoding.WildernessWords, bias));
                            if (hits.Count >= MaxHits) return hits;
                        }
                    }
                }

                offset += (nuint)advance;
            }
        }
        return hits;
    }

    /// <summary>
    /// Re-reads each candidate and keeps only those that still predict (newX, newY) — for a
    /// wilderness candidate, using the bias it was found with. Call after the party has moved.
    /// </summary>
    public static List<PositionCandidate> Narrow(ProcessMemory mem, List<PositionCandidate> candidates,
        int newX, int newY, CancellationToken ct)
    {
        var survivors = new List<PositionCandidate>();
        byte[] buf = new byte[4];

        foreach (var c in candidates)
        {
            ct.ThrowIfCancellationRequested();
            int want = Footprint(c.Encoding);
            if (mem.Read(c.Address, buf, want) < want) continue;

            bool ok = c.Encoding == PositionEncoding.WildernessWords
                ? buf[3] == 0 && buf[2] == newY && Word(buf, 0) + c.XBias == newX
                : buf[0] == newX && buf[1] == newY;
            if (ok) survivors.Add(c);
        }
        return survivors;
    }

    /// <summary>
    /// Reads the live position from a locked candidate, or null if the read fails. X is returned as
    /// the game prints it (bias already applied). Facing is only known for the indoor encoding — the
    /// wilderness words have no facing byte beside them — so it is null out on the overland map.
    /// </summary>
    public static (int X, int Y, int? Facing)? Read(ProcessMemory mem, PositionCandidate c)
    {
        var buf = mem.Read(c.Address, Footprint(c.Encoding));

        if (c.Encoding == PositionEncoding.WildernessWords)
        {
            if (buf.Length < 4 || buf[3] != 0) return null;
            return (Word(buf, 0) + c.XBias, buf[2], null);
        }

        if (buf.Length < 2) return null;
        int facing = buf.Length >= 3 && buf[2] < 4 ? buf[2] : 0;
        return (buf[0], buf[1], facing);
    }

    /// <summary>
    /// Writes (x, y) — given as the numbers the game prints — back through a locked candidate,
    /// undoing the X bias for the wilderness encoding. Facing is left alone. The X word is written
    /// as a full signed 16-bit value, not a byte: with a bias of 13, teleporting to x = 10 has to
    /// store −3, and truncating that to one byte lands the party 256 columns east instead.
    /// </summary>
    public static bool Write(ProcessMemory mem, PositionCandidate c, int x, int y)
    {
        if (c.Encoding != PositionEncoding.WildernessWords)
            return mem.Write(c.Address, new[] { (byte)x, (byte)y });

        short sx = (short)(x - c.XBias);
        return mem.Write(c.Address, new[] { (byte)sx, (byte)(sx >> 8), (byte)y, (byte)0 });
    }

    /// <summary>Whether teleporting to this X would store a negative word — see <see cref="Write"/>.</summary>
    public static bool StoresNegativeX(PositionCandidate c, int x)
        => c.Encoding == PositionEncoding.WildernessWords && x - c.XBias < 0;
}
