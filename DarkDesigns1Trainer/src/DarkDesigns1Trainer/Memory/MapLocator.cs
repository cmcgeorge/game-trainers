using DarkDesigns1Trainer.Game;

namespace DarkDesigns1Trainer.Memory;

/// <summary>How the map was located, for the status line.</summary>
public enum MapLocateMethod
{
    None,
    /// <summary>Derived from the roster the character scan already found, then validated.</summary>
    Roster,
    /// <summary>Found by sweeping memory for the map buffer's own structure.</summary>
    Structural,
}

/// <summary>A located live map: the addresses to poll and write, and a snapshot of the map bytes.</summary>
public sealed class LocatedMap
{
    /// <summary>Address of the four-<c>uint16</c> level / X / Y / facing block.</summary>
    public nuint PositionAddress { get; }

    /// <summary>Address of the 12,648-byte map buffer for the level the party is on.</summary>
    public nuint MapAddress { get; }

    /// <summary>The position as it read at locate time.</summary>
    public PartyPosition Position { get; }

    public MapLocateMethod Method { get; }

    public LocatedMap(nuint positionAddress, nuint mapAddress, PartyPosition position,
                      MapLocateMethod method)
    {
        PositionAddress = positionAddress;
        MapAddress = mapAddress;
        Position = position;
        Method = method;
    }
}

/// <summary>
/// Finds the party's live position and the map buffer of the level it is standing on.
///
/// Both live in the game's single data segment, which moves every DOSBox session, so neither
/// address is hard-coded. Two strategies:
///
/// 1. <b>From the roster</b> — the character scan has already found the roster by content, and the
///    position block and map buffer sit at constant offsets from it inside that same data segment
///    (<c>0x424</c>/<c>0x46C</c> → <c>0x1320</c> → <c>0x50F4</c>, all pinned by the disassembly of
///    the <c>DDCHARS.DAT</c> loader and the map loader). The roster scan can anchor on either the
///    array's scratch slot or the first record the file holds, so both offsets are tried and the
///    result is accepted only if the bytes behind it validate.
///
/// 2. <b>Structural</b> — a sweep for the map buffer itself. A level's 4,096 wall bytes are
///    reciprocal: a square's east wall byte and its eastern neighbour's west wall byte hold the
///    same value, for all 3,968 interior pairs. That, plus wall bytes inside the range the movement
///    code accepts and bit 6 clear on every content byte, is a signature unrelated memory does not
///    imitate. The position block then sits <c>0x3DD4</c> before it and must itself validate.
///
/// Either way nothing is trusted that does not decode, so a build that laid its data segment out
/// differently reports "not found" rather than a confident wrong address.
/// </summary>
public static class MapLocator
{
    private const int ChunkSize = 1 << 20;    // 1 MiB scan window
    private const int PageSize = 0x1000;

    /// <summary>
    /// The two data-segment deltas from a located roster to the position block. Which one applies
    /// depends on whether the roster scan anchored on the in-memory array's scratch slot 0 or on
    /// the first record <c>DDCHARS.DAT</c> actually holds — the bytes decide, not the caller.
    /// </summary>
    private static readonly int[] RosterDeltas =
        { MapFormat.PositionFromRosterArray, MapFormat.PositionFromRosterFirstFileSlot };

    /// <summary>Locates the map from the roster, falling back to a structural sweep.</summary>
    public static LocatedMap? Find(IMemorySource mem, nuint? rosterBase, CancellationToken ct = default)
    {
        if (rosterBase is { } rb)
        {
            var fromRoster = FindFromRoster(mem, rb);
            if (fromRoster != null) return fromRoster;
        }
        return FindByStructure(mem, ct);
    }

    // --- strategy 1: from the located roster ---------------------------------
    /// <summary>
    /// Derives the position block and map buffer from a roster address and validates both. Returns
    /// null unless exactly one of the two deltas validates: guessing between two addresses is how a
    /// teleport ends up writing into someone else's data.
    /// </summary>
    public static LocatedMap? FindFromRoster(IMemorySource mem, nuint rosterBase)
    {
        LocatedMap? found = null;

        foreach (int delta in RosterDeltas)
        {
            var candidate = Evaluate(mem, rosterBase + (nuint)delta, MapLocateMethod.Roster);
            if (candidate == null) continue;
            if (found != null) return null;   // both deltas validated — ambiguous, so neither is used
            found = candidate;
        }

        return found;
    }

    /// <summary>
    /// Accepts a candidate position-block address only when the four values decode and the buffer
    /// behind them is a real level.
    ///
    /// The map has to validate in full — a range check alone is not enough. A block of zeros reads
    /// as a perfectly plausible "party is in town" position, and a window of a real map shifted by a
    /// record's width still holds nothing but in-range bytes, so the two together will happily
    /// accept an address one record away from the right one. Only reciprocity tells them apart. The
    /// cost is that the party has to be inside the castle for the locate to work at all, which is
    /// where teleporting is the point anyway.
    /// </summary>
    private static LocatedMap? Evaluate(IMemorySource mem, nuint positionAddress, MapLocateMethod method)
    {
        var posBytes = mem.Read(positionAddress, MapFormat.PositionBlockSize);
        if (posBytes.Length < MapFormat.PositionBlockSize) return null;

        var position = PartyPosition.FromBytes(posBytes);
        if (!position.IsPlausible) return null;

        nuint mapAddress = positionAddress + (nuint)MapFormat.MapFromPosition;
        var mapBytes = mem.Read(mapAddress, MapFormat.FileSize);
        if (mapBytes.Length < MapFormat.FileSize) return null;
        if (!MapFormat.LooksLikeMap(mapBytes, 0)) return null;

        return new LocatedMap(positionAddress, mapAddress, position, method);
    }

    // --- strategy 2: structural sweep for the map buffer ---------------------
    /// <summary>
    /// Sweeps every readable region for the map buffer's structure, then checks the position block
    /// that must precede it. Only finds anything once the party has entered the castle and a level
    /// has actually been loaded — in town there is no map to recognise, which is reported as
    /// "not found" rather than guessed at.
    /// </summary>
    public static LocatedMap? FindByStructure(IMemorySource mem, CancellationToken ct = default)
    {
        int overlap = MapFormat.FileSize - 1;
        byte[] buf = new byte[ChunkSize + overlap];

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                ct.ThrowIfCancellationRequested();

                nuint remaining = regionEnd - start;
                if (remaining < (nuint)MapFormat.FileSize) break;

                int readLen = (int)Math.Min((nuint)(ChunkSize + overlap), remaining);
                int read = ReadLargestPrefix(mem, start, buf, readLen);
                if (read < MapFormat.FileSize)
                {
                    start += PageSize;
                    continue;
                }

                foreach (int hit in FindCandidates(buf, read, ct))
                {
                    nuint mapAddress = start + (nuint)hit;
                    // The position block precedes the map buffer, so a hit that close to address
                    // zero cannot be the real thing — and computing it would wrap.
                    if (mapAddress < (nuint)MapFormat.MapFromPosition) continue;

                    var found = Evaluate(mem, mapAddress - (nuint)MapFormat.MapFromPosition,
                                         MapLocateMethod.Structural);
                    if (found != null) return found;
                }

                // Step to just past the last offset this window could have started a map at, so no
                // candidate falls between two windows.
                start += (nuint)Math.Max(1, read - overlap);
            }
        }
        return null;
    }

    /// <summary>
    /// Reads as much of <paramref name="want"/> as the target will give up, halving on failure.
    /// <c>ProcessMemory.Read</c> is all-or-nothing, so a window whose tail is not committed would
    /// otherwise return nothing at all and lose the whole chunk.
    ///
    /// The halving ends with an explicit try at exactly one map's worth. Without it a readable
    /// prefix that falls between two powers of two — say 13,000 bytes, more than a map but less
    /// than the 16,192-byte trial above it — fails every attempt and the sweep walks past a window
    /// it could have read, missing a map sitting at the head of that region.
    /// </summary>
    private static int ReadLargestPrefix(IMemorySource mem, nuint start, byte[] buffer, int want)
    {
        for (int trial = want; trial > MapFormat.FileSize; trial /= 2)
        {
            int read = mem.Read(start, buffer, trial);
            if (read > 0) return read;
        }
        return mem.Read(start, buffer, MapFormat.FileSize);
    }

    /// <summary>
    /// Offsets within <paramref name="buf"/> that pass the map signature.
    ///
    /// The wall-range filter is a single forward sweep whose watermark never moves backwards, so it
    /// costs one pass over the buffer no matter how many offsets it rejects. What survives that is
    /// then met by a fixed-size reciprocity probe — a couple of dozen neighbour pairs, which random
    /// data essentially never satisfies — before anything pays for a full-length check. The probe
    /// is what keeps the sweep fast over a long run of small-valued bytes, which is exactly what an
    /// emulator's guest RAM is full of and which the range filter alone waves through.
    /// </summary>
    public static IEnumerable<int> FindCandidates(byte[] buf, int length, CancellationToken ct = default)
    {
        if (buf == null) yield break;
        length = Math.Min(length, buf.Length);
        if (length < MapFormat.FileSize) yield break;

        int scanned = 0;        // wall bytes verified in range, up to but excluding this index
        int lastBad = -1;       // most recent index holding a byte the movement code would reject
        int nonZero = 0;        // nonzero wall bytes currently inside [i, i + WallsLength)

        for (int i = 0; i + MapFormat.FileSize <= length; i++)
        {
            if ((i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();

            while (scanned < i + MapFormat.WallsLength)
            {
                byte v = buf[scanned];
                if (v > MapFormat.MaxWallValue) lastBad = scanned;
                else if (v != 0) nonZero++;
                scanned++;
            }

            if (lastBad < i && nonZero >= MapFormat.MinWallBytes &&
                MapFormat.PassesReciprocityProbe(buf, i) && MapFormat.LooksLikeMap(buf, i))
            {
                yield return i;
            }

            byte leaving = buf[i];
            if (leaving != 0 && leaving <= MapFormat.MaxWallValue) nonZero--;
        }
    }

    // --- live reads ----------------------------------------------------------
    /// <summary>Why a re-read of the position block did not produce a usable position.</summary>
    public enum ReadOutcome
    {
        /// <summary>The four values decoded and are in range.</summary>
        Ok,
        /// <summary>The address could not be read at all — the process is gone or has moved on.</summary>
        Unreadable,
        /// <summary>
        /// The bytes read but do not decode. Not the same thing as unreadable: the game can put the
        /// level out of range itself (its edge-square handler increments the level unconditionally,
        /// so stepping off the bottom level leaves it at 6), and that is a game state rather than a
        /// lost address.
        /// </summary>
        Implausible,
    }

    /// <summary>Re-reads the position block, distinguishing "gone" from "temporarily nonsense".</summary>
    public static ReadOutcome TryReadPosition(IMemorySource mem, nuint address, out PartyPosition position)
    {
        position = default;
        var bytes = mem.Read(address, MapFormat.PositionBlockSize);
        if (bytes.Length < MapFormat.PositionBlockSize) return ReadOutcome.Unreadable;

        var decoded = PartyPosition.FromBytes(bytes);
        if (!decoded.IsPlausible) return ReadOutcome.Implausible;

        position = decoded;
        return ReadOutcome.Ok;
    }

    /// <summary>
    /// Re-runs the full validation against an address located earlier, and returns the map bytes
    /// that passed so the caller can act on the very bytes it validated.
    ///
    /// This is what makes a cached address safe to write through. Re-reading the position alone is
    /// not enough: its four values are only range-checked, and a stale address left over from a
    /// game that quit and restarted inside the same DOSBox process can pass that check by accident.
    /// Only the map behind it settles the question.
    /// </summary>
    public static bool TryRevalidate(IMemorySource mem, nuint positionAddress,
                                     out PartyPosition position, out byte[] mapBytes)
    {
        position = default;
        mapBytes = Array.Empty<byte>();

        if (TryReadPosition(mem, positionAddress, out var decoded) != ReadOutcome.Ok) return false;

        var bytes = mem.Read(positionAddress + (nuint)MapFormat.MapFromPosition, MapFormat.FileSize);
        if (bytes.Length < MapFormat.FileSize) return false;
        if (!MapFormat.LooksLikeMap(bytes, 0)) return false;

        position = decoded;
        mapBytes = bytes;
        return true;
    }
}
