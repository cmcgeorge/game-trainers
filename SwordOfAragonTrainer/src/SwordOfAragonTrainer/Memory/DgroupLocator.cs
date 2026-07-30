using SwordOfAragonTrainer.Game;

namespace SwordOfAragonTrainer.Memory;

/// <summary>A located data segment: where <c>DS:0000</c> sits in the host process.</summary>
/// <param name="Base">Host address of guest <c>DS:0000</c>.</param>
/// <param name="AnchorAddress">Host address the primary anchor was found at.</param>
/// <param name="ValidatorsMatched">How many of the secondary anchors lined up.</param>
public readonly record struct DgroupLocation(nuint Base, nuint AnchorAddress, int ValidatorsMatched)
{
    /// <summary>Host address of a guest <c>DS:offset</c>.</summary>
    public nuint AddressOf(int dsOffset) => Base + (nuint)dsOffset;

    /// <summary>End of the 64 KiB segment window.</summary>
    public nuint End => Base + GameSignatures.SegmentSize;
}

/// <summary>
/// Locates <c>ARAGON.EXE</c>'s data segment inside a running DOSBox process by pattern-scanning for
/// its string literals, then narrows the search space for the game's variables from the whole address
/// space to that one 64 KiB window.
///
/// This is why the trainer hard-codes no addresses: the anchor <i>offsets</i> come from the executable
/// image, but the <i>address</i> is always derived at run time.
/// </summary>
public static class DgroupLocator
{
    /// <summary>
    /// How many of the three secondary anchors must line up before a hit is accepted — a majority.
    /// One would be too weak for a base address the trainer then writes through; demanding all three
    /// would throw the location away if a single literal region happened to be unreadable at that
    /// moment. So the guarantee is "at least three of the four anchors", and
    /// <see cref="DgroupLocation.ValidatorsMatched"/> reports whether all four did.
    /// </summary>
    public const int MinValidators = 2;

    /// <summary>
    /// Scans for the world-map data segment. Returns the strongest candidate — the one where the most
    /// secondary anchors line up, and only if at least <see cref="MinValidators"/> of them do — or null
    /// if nothing matched well enough.
    /// </summary>
    public static DgroupLocation? Locate(ProcessMemory mem, CancellationToken ct = default)
    {
        var primary = GameSignatures.WorldMapPrimary;
        var hits = BytePatternScanner.Find(mem, primary.Bytes, ct);

        DgroupLocation? best = null;
        foreach (var hit in hits.Addresses)
        {
            ct.ThrowIfCancellationRequested();
            if (hit < (nuint)primary.DsOffset) continue;          // would place DS:0000 below zero

            nuint baseAddress = hit - (nuint)primary.DsOffset;
            int matched = CountValidators(mem, baseAddress);
            if (matched < MinValidators) continue;

            if (best == null || matched > best.Value.ValidatorsMatched)
                best = new DgroupLocation(baseAddress, hit, matched);

            if (matched == GameSignatures.WorldMapValidators.Count) break;   // perfect match, stop early
        }
        return best;
    }

    /// <summary>How many secondary anchors are present at their expected offsets from a candidate base.</summary>
    private static int CountValidators(ProcessMemory mem, nuint baseAddress)
    {
        int matched = 0;
        foreach (var anchor in GameSignatures.WorldMapValidators)
        {
            var expected = anchor.Bytes;
            var actual = mem.Read(baseAddress + (nuint)anchor.DsOffset, expected.Length);
            if (actual.Length == expected.Length && actual.AsSpan().SequenceEqual(expected)) matched++;
        }
        return matched;
    }

    /// <summary>Page size the chunked segment read aligns to.</summary>
    private const int PageSize = 0x1000;

    /// <summary>
    /// Reads the 64 KiB segment, returning the readable prefix.
    ///
    /// This has to be chunked rather than one big read. <c>ReadProcessMemory</c> fails as a whole when
    /// a request straddles the end of a committed region (<c>ERROR_PARTIAL_COPY</c>), and
    /// <see cref="ProcessMemory.Read(nuint, int)"/> reports that as zero bytes — so a single 64 KiB
    /// read of a window whose tail runs past the end of DOSBox's guest-RAM allocation would come back
    /// empty and every guided find would report "no candidates" even though the value sits in the
    /// readable part. The base address is the anchor minus its DS offset and so is not page-aligned;
    /// each chunk is therefore trimmed to the next page boundary so no single read can straddle one.
    /// </summary>
    public static byte[] ReadSegment(ProcessMemory mem, DgroupLocation location)
    {
        var buffer = new byte[GameSignatures.SegmentSize];
        int filled = 0;
        while (filled < buffer.Length)
        {
            nuint address = location.Base + (nuint)filled;
            int toBoundary = PageSize - (int)((ulong)address % PageSize);
            int want = Math.Min(toBoundary, buffer.Length - filled);
            var chunk = mem.Read(address, want);
            if (chunk.Length == 0) break;                 // unreadable from here on
            Array.Copy(chunk, 0, buffer, filled, chunk.Length);
            filled += chunk.Length;
            if (chunk.Length < want) break;               // short read: nothing more to get
        }

        if (filled == buffer.Length) return buffer;
        Array.Resize(ref buffer, filled);
        return buffer;
    }

    /// <summary>A candidate found inside the located segment.</summary>
    /// <param name="Address">Host address.</param>
    /// <param name="DsOffset">Guest <c>DS:offset</c>, which is what makes a hit worth reporting.</param>
    /// <param name="Value">Decoded value at that address.</param>
    public readonly record struct Candidate(nuint Address, int DsOffset, double Value);

    /// <summary>
    /// Finds every MBF single in the segment within <paramref name="tolerance"/> of
    /// <paramref name="target"/>. The game displays gold rounded, so an exact match is not usable — a
    /// tolerance of about 1.0 turns "the screen says 703" into a handful of candidates.
    /// </summary>
    public static List<Candidate> FindMbfNear(byte[] segment, nuint segmentBase, double target,
                                              double tolerance)
    {
        var found = new List<Candidate>();
        if (tolerance < 0) tolerance = 0;
        for (int offset = 0; offset + 4 <= segment.Length; offset++)
        {
            if (segment[offset + 3] == 0) continue;                     // MBF zero: exponent byte clear
            double value = Mbf.ToDouble(segment, offset);
            if (Math.Abs(value - target) <= tolerance)
                found.Add(new Candidate(segmentBase + (nuint)offset, offset, value));
        }
        return found;
    }

    /// <summary>
    /// Finds every signed 16-bit word in the segment equal to <paramref name="target"/> — the shape
    /// most of the game's counters (population, morale, recruits) take.
    /// </summary>
    public static List<Candidate> FindInt16(byte[] segment, nuint segmentBase, int target)
    {
        var found = new List<Candidate>();
        if (target is < short.MinValue or > short.MaxValue) return found;
        for (int offset = 0; offset + 2 <= segment.Length; offset++)
        {
            short value = (short)(segment[offset] | (segment[offset + 1] << 8));
            if (value == target)
                found.Add(new Candidate(segmentBase + (nuint)offset, offset, value));
        }
        return found;
    }
}
