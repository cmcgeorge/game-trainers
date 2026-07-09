namespace MightAndMagic1Trainer.Memory;

/// <summary>
/// An address where a roll's stat values were found in the target's memory, plus the
/// byte stride between consecutive stats: 1 = contiguous single bytes, 2 = the record's
/// <c>[normal, active]</c> pair layout (on a fresh roll active == normal, so the visible
/// numbers sit at every other byte).
/// </summary>
public readonly record struct RollMatch(nuint Address, int Stride);

/// <summary>
/// Locates the *temporary* attribute buffer the game uses on the CREATE NEW CHARACTERS
/// screen. That roll isn't a roster record yet (the character isn't saved), so the
/// normal roster scan can't see it. Instead the caller reads the seven numbers off the
/// screen once; this signature-scans memory for those exact bytes (at stride 1 or 2) to
/// pin the buffer's address, after which each re-roll can be read directly.
///
/// The seven-value signature (each byte in roughly 3..18) is specific enough to usually
/// resolve to a single address in a DOS game's small image; <see cref="CharacterRollerViewModel"/>
/// narrows any remaining ambiguity by re-rolling and keeping the candidate that keeps
/// changing within a plausible stat range.
/// </summary>
public static class RollScanner
{
    /// <summary>Plausible inclusive range for a single rolled/boosted attribute byte.
    /// Used to reject coincidental matches while narrowing.</summary>
    public const int MinStatValue = 1;
    public const int MaxStatValue = 60;

    /// <summary>Strides tried when locating the buffer (contiguous, then paired).</summary>
    public static readonly int[] Strides = { 1, 2 };

    /// <summary>Upper bound on returned matches, so a too-loose signature can't blow up memory.</summary>
    public const int MaxMatches = 4096;

    /// <summary>Per-region read cap (mirrors <see cref="MemorySearcher"/>): one huge mapping
    /// can't trigger a multi-GB allocation.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Pure pattern search within one buffer: offsets where every byte of
    /// <paramref name="values"/> appears at <paramref name="stride"/>, i.e.
    /// <c>data[off + k*stride] == values[k]</c> for all k. Factored out so it's unit-testable
    /// without a live process.
    /// </summary>
    public static IEnumerable<int> FindInBuffer(byte[] data, byte[] values, int stride)
    {
        if (values.Length == 0 || stride < 1) yield break;
        int span = (values.Length - 1) * stride;          // offset of the last matched byte
        for (int i = 0; i + span < data.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < values.Length; k++)
            {
                if (data[i + k * stride] != values[k]) { ok = false; break; }
            }
            if (ok) yield return i;
        }
    }

    /// <summary>
    /// Scans all committed memory for the supplied stat values at each candidate stride.
    /// Returns every matching address (capped at <see cref="MaxMatches"/>).
    /// </summary>
    public static List<RollMatch> Find(ProcessMemory mem, IReadOnlyList<int> values, CancellationToken ct = default)
    {
        var vbytes = new byte[values.Count];
        for (int k = 0; k < values.Count; k++) vbytes[k] = (byte)values[k];

        var matches = new List<RollMatch>();
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            long want = (long)region.Size;
            if (want > MaxRegionBytes) want = MaxRegionBytes;
            var data = mem.Read(region.Base, (int)want);
            if (data.Length == 0) continue;

            foreach (int stride in Strides)
            {
                foreach (int off in FindInBuffer(data, vbytes, stride))
                {
                    matches.Add(new RollMatch(region.Base + (nuint)off, stride));
                    if (matches.Count >= MaxMatches) return matches;
                }
            }
        }
        return matches;
    }

    /// <summary>
    /// Reads <paramref name="count"/> attribute bytes at <paramref name="addr"/>, honoring
    /// <paramref name="stride"/> (writes them into <paramref name="dest"/>). Returns false if
    /// the read came up short.
    /// </summary>
    public static bool TryReadStats(ProcessMemory mem, nuint addr, int stride, int count, int[] dest)
    {
        int need = (count - 1) * stride + 1;
        var buf = mem.Read(addr, need);
        if (buf.Length < need) return false;
        for (int k = 0; k < count; k++) dest[k] = buf[k * stride];
        return true;
    }

    /// <summary>True when every value is within the plausible attribute range.</summary>
    public static bool InRange(IReadOnlyList<int> values)
    {
        foreach (var v in values)
            if (v < MinStatValue || v > MaxStatValue) return false;
        return true;
    }
}
