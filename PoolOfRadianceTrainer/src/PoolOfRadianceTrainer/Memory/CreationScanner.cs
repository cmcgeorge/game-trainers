using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer.Memory;

/// <summary>
/// An address in the target's memory where a create-screen roll's signature bytes were found,
/// plus whether the bytes around it also match the full record shape (a confirmed HP at its
/// record offset). A <see cref="Structural"/> match lets the roller also read and target HP;
/// a plain match tracks the six abilities and exceptional strength only.
/// </summary>
public readonly record struct CreationMatch(nuint Address, bool Structural);

/// <summary>
/// Locates the <em>temporary</em> ability buffer the game fills on the "Create a character"
/// screen. That roll is not a saved character record yet (the character is not added to the
/// party until you accept it, name it, and pick a class), so the <see cref="CharacterLocator"/>
/// that finds the party by record shape cannot see it. Instead the caller reads the numbers off
/// the create screen once; this signature-scans memory for the seven contiguous bytes
/// [STR, INT, WIS, DEX, CON, CHA, STR%] (record offsets 0x10–0x16) to pin the buffer's address,
/// after which each <c>n</c>-key re-roll can be read straight from memory
/// (<see cref="CharacterRollerViewModel"/>).
///
/// The seven-value signature (six abilities in roughly <see cref="MinStat"/>..<see cref="MaxStat"/>
/// plus an exceptional-strength percentile in 0..<see cref="MaxStrPercent"/>) is usually specific
/// enough to resolve to a single address in a DOS game's small image. When the caller also
/// supplies the on-screen HP, a hit is additionally checked for the full character-record shape
/// — HP (byte) at the record's <see cref="PorFormat.OffHpMax"/>, measured relative to the
/// signature base — so the create buffer (which the game builds as a scratch character record)
/// is confirmed rather than guessed. Any remaining ambiguity is narrowed by re-rolling and
/// keeping the candidate that keeps changing within a plausible range. Nothing here writes to
/// the game; it only reads.
/// </summary>
public static class CreationScanner
{
    /// <summary>The six primary abilities modelled as 3d6 (STR, INT, WIS, DEX, CON, CHA).</summary>
    public const int StatCount = PorFormat.StatCount;

    /// <summary>Number of contiguous bytes the signature is built from: six abilities + STR%.</summary>
    public const int SignatureCount = StatCount + 1;

    /// <summary>Plausible inclusive range for one freshly-rolled ability byte on the create screen.
    /// Kept to the 3d6 range (3–18) since the six primaries are 3d6, with a little headroom.</summary>
    public const int MinStat = 3;
    public const int MaxStat = 18;

    /// <summary>Plausible inclusive range for the exceptional-strength percentile byte (0 = none,
    /// 1–100 for a fighter with Strength 18).</summary>
    public const int MaxStrPercent = 100;

    // Offset of HP (HpMax) relative to the signature base (record offset 0x10), derived from the
    // record layout so it can never drift from PorFormat. The create buffer is a scratch character
    // record, so this holds there too when the record shape is confirmed.
    public const int SigToHp = PorFormat.OffHpMax - PorFormat.OffStr;   // 0x22

    /// <summary>Upper bound on returned matches, so a too-loose signature can't blow up memory.</summary>
    public const int MaxMatches = 4096;

    /// <summary>Per-region read cap (mirrors <see cref="CharacterLocator"/>): one huge mapping can't
    /// trigger a multi-GB allocation.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Pure pattern search within one buffer: offsets where the seven signature bytes appear
    /// contiguously, i.e. <c>data[off + k] == sig[k]</c> for all k. Factored out so it is
    /// unit-testable without a live process.
    /// </summary>
    public static IEnumerable<int> FindInBuffer(byte[] data, byte[] sig)
    {
        if (sig.Length == 0) yield break;
        for (int i = 0; i + sig.Length <= data.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < sig.Length; k++)
            {
                if (data[i + k] != sig[k]) { ok = false; break; }
            }
            if (ok) yield return i;
        }
    }

    /// <summary>
    /// True when the bytes at <paramref name="sigOffset"/> in <paramref name="data"/> match the full
    /// character-record shape for a fresh roll: the HP byte at the record's <see cref="SigToHp"/>
    /// offset relative to the signature base equals <paramref name="hp"/>. Bounds-checked; returns
    /// false if the HP byte would run past the buffer. Pure, so it is unit-testable.
    /// </summary>
    public static bool IsStructural(byte[] data, int sigOffset, int hp)
    {
        int hpOff = sigOffset + SigToHp;
        if (sigOffset < 0 || hpOff >= data.Length) return false;
        return data[hpOff] == hp;
    }

    /// <summary>
    /// Scans all committed memory for the seven signature bytes contiguously. When
    /// <paramref name="hp"/> is supplied (&gt; 0), each hit is also tested for the full record shape
    /// and flagged <see cref="CreationMatch.Structural"/>. Returns every match (capped at
    /// <see cref="MaxMatches"/>); the caller prefers structural ones.
    /// </summary>
    public static List<CreationMatch> Find(ProcessMemory mem, IReadOnlyList<int> sig, int hp,
                                           CancellationToken ct = default)
    {
        var sbytes = new byte[sig.Count];
        for (int k = 0; k < sig.Count; k++) sbytes[k] = (byte)sig[k];
        bool confirm = hp > 0;

        var matches = new List<CreationMatch>();
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            int want = (int)Math.Min((long)region.Size, MaxRegionBytes);

            // Read a small tail overlap past the region so a match — or its structural window — that
            // straddles into the next region is still seen whole. Anchors are only taken from the
            // owned [0, want) part, so adjacent regions never double-report; the overlap is just
            // enough for the furthest byte IsStructural reads (HP at SigToHp). If the extended read
            // fails because its tail is unreadable, fall back to reading just the region.
            int overlap = want < MaxRegionBytes ? SigToHp : 0;
            var data = mem.Read(region.Base, want + overlap);
            if (data.Length < want) data = mem.Read(region.Base, want);
            if (data.Length == 0) continue;

            foreach (int off in FindInBuffer(data, sbytes))
            {
                if (off >= want) break;   // FindInBuffer yields ascending offsets; the rest are overlap the next region owns
                bool structural = confirm && IsStructural(data, off, hp);
                matches.Add(new CreationMatch(region.Base + (nuint)off, structural));
                if (matches.Count >= MaxMatches) return matches;
            }
        }
        return matches;
    }

    /// <summary>Reads the seven signature bytes (six abilities + STR%) at <paramref name="addr"/>
    /// into <paramref name="dest"/> (length &gt;= <see cref="SignatureCount"/>). Returns false if the
    /// read came up short.</summary>
    public static bool TryReadSignature(ProcessMemory mem, nuint addr, int[] dest)
    {
        var buf = mem.Read(addr, SignatureCount);
        if (buf.Length < SignatureCount) return false;
        for (int k = 0; k < SignatureCount; k++) dest[k] = buf[k];
        return true;
    }

    /// <summary>Reads the HP byte that sits at the record offset relative to the signature base
    /// <paramref name="addr"/>. Returns false if the read came up short.</summary>
    public static bool TryReadHp(ProcessMemory mem, nuint addr, out int hp)
    {
        hp = 0;
        var buf = mem.Read(addr + (nuint)SigToHp, 1);
        if (buf.Length < 1) return false;
        hp = buf[0];
        return true;
    }

    /// <summary>True when the six abilities are within the plausible 3d6 create-screen range AND the
    /// exceptional-strength percentile is in 0..<see cref="MaxStrPercent"/>. Exceptional strength
    /// only applies to fighters with Strength 18, so a nonzero percentile paired with a non-18
    /// Strength is rejected as a false positive.</summary>
    public static bool InRange(IReadOnlyList<int> sig)
    {
        if (sig.Count < SignatureCount) return false;
        for (int k = 0; k < StatCount; k++)
        {
            int v = sig[k];
            if (v < MinStat || v > MaxStat) return false;
        }
        int sp = sig[StatCount];
        if (sp < 0 || sp > MaxStrPercent) return false;
        return sp == 0 || sig[0] == MaxStat;
    }
}
