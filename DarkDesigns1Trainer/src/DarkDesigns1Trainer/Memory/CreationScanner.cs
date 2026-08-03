namespace DarkDesigns1Trainer.Memory;

using DarkDesigns1Trainer.Game;

/// <summary>
/// Locates the <em>temporary</em> rolled-stat pool the game fills on the (C)reate screen.
///
/// That roll is not a roster record yet — the character has no name, class or level until the five
/// values have been arranged — so the structural <see cref="RosterLocator"/> cannot see it.
/// Instead the player reads the five numbers off the create screen once and this signature-scans
/// the emulator's memory for them, pinning the pool's address; from then on every <c>R</c> re-roll
/// can be read straight out of memory (<see cref="ViewModels.CharacterRollerViewModel"/>).
///
/// The signature is matched as a <b>multiset</b>: five contiguous uint16 LE values that, once
/// sorted, equal the five captured numbers sorted. Matching the set rather than the sequence means
/// the player can type the numbers in any order and still get a lock. That is a deliberate trade,
/// not a free one: the signature now accepts every permutation of the captured values — as many as
/// 5! = 120 byte patterns where an exact-sequence signature accepts one — so it is correspondingly
/// likelier to collide by chance. It is still far too specific to collide in practice: measured
/// against the running game, a captured roll resolved to exactly <b>one</b> address in the whole
/// emulator process on every attempt, so the caller's re-roll narrowing is a safety net rather than
/// the usual path.
///
/// Nothing here writes to the game; the write path lives in the view model.
/// </summary>
public static class CreationScanner
{
    /// <summary>Values in one roll (5).</summary>
    public const int RolledCount = CreationFormat.RolledCount;

    /// <summary>Upper bound on returned matches, so a surprisingly loose signature can't blow up memory.</summary>
    public const int MaxMatches = 4096;

    /// <summary>Per-region read cap (mirrors <see cref="RosterLocator"/>'s chunking): one huge
    /// mapping can't trigger a multi-GB allocation.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Pure pattern search within one buffer: offsets where five contiguous uint16 LE values form
    /// the same multiset as <paramref name="sortedWanted"/> (which must already be sorted). Factored
    /// out so it is unit-testable without a live process.
    /// </summary>
    public static IEnumerable<int> FindInBuffer(byte[] data, int[] sortedWanted)
    {
        if (data == null || sortedWanted == null || sortedWanted.Length != RolledCount) yield break;

        var found = new int[RolledCount];
        for (int i = 0; i + CreationFormat.PoolBytes <= data.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < RolledCount; k++)
            {
                int o = i + k * CreationFormat.ValueSize;
                // Every plausible rolled value fits in a byte, so a non-zero high byte rejects the
                // window immediately — this is the cheap gate that keeps the whole scan fast.
                if (data[o + 1] != 0) { ok = false; break; }
                int v = data[o];
                if (v < CreationFormat.MinPlausible || v > CreationFormat.MaxPlausible) { ok = false; break; }
                found[k] = v;
            }
            if (!ok) continue;

            Array.Sort(found);
            for (int k = 0; k < RolledCount; k++)
                if (found[k] != sortedWanted[k]) { ok = false; break; }
            if (ok) yield return i;
        }
    }

    /// <summary>
    /// Scans all committed memory for the captured roll. Returns every matching address, capped at
    /// <see cref="MaxMatches"/>; the caller narrows any ambiguity by re-rolling.
    /// </summary>
    public static List<nuint> Find(ProcessMemory mem, IReadOnlyList<int> captured, CancellationToken ct = default)
    {
        var matches = new List<nuint>();
        // Guard like FindInBuffer does: a short capture list would otherwise throw from inside the
        // caller's Task.Run and surface only as a generic "Lock failed" status.
        if (captured == null || captured.Count < RolledCount) return matches;

        var wanted = new int[RolledCount];
        for (int k = 0; k < RolledCount; k++) wanted[k] = captured[k];
        Array.Sort(wanted);

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            int want = (int)Math.Min((long)region.Size, MaxRegionBytes);

            // Read a short tail past the region so a pool straddling into the next one is still seen
            // whole. Anchors are only taken from the owned [0, want) part, so adjacent regions never
            // double-report. Asked for unconditionally: making it conditional on the region having
            // been read untruncated needs a comparison that is easy to get off by one at exactly
            // MaxRegionBytes, and the fallback below already handles an unreadable tail.
            const int overlap = CreationFormat.PoolBytes - 1;
            var data = mem.Read(region.Base, want + overlap);
            if (data.Length < want) data = mem.Read(region.Base, want);
            if (data.Length == 0) continue;

            foreach (int off in FindInBuffer(data, wanted))
            {
                if (off >= want) break;   // FindInBuffer yields ascending offsets; the rest belong to the next region
                matches.Add(region.Base + (nuint)off);
                if (matches.Count >= MaxMatches) return matches;
            }
        }
        return matches;
    }

    /// <summary>Reads the five rolled values at <paramref name="addr"/> into <paramref name="dest"/>.
    /// Returns false if the read came up short.</summary>
    public static bool TryReadRoll(ProcessMemory mem, nuint addr, int[] dest)
    {
        var buf = mem.Read(addr, CreationFormat.PoolBytes);
        if (buf.Length < CreationFormat.PoolBytes) return false;
        return CreationFormat.Decode(buf, 0, dest);
    }

    /// <summary>Writes five values over the pool, so the create screen offers them instead of what it
    /// rolled. Confirmed against the running game: the arranged character keeps the written values.</summary>
    public static bool WriteRoll(ProcessMemory mem, nuint addr, IReadOnlyList<int> values) =>
        mem.Write(addr, CreationFormat.Encode(values));

    /// <summary>True when every value is within the plausible rolled-stat range.</summary>
    public static bool InRange(IReadOnlyList<int> values) => CreationFormat.LooksLikeRoll(values);
}
