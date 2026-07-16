using WastelandTrainer.Game;

namespace WastelandTrainer.Memory;

/// <summary>
/// An address in the target's memory where a create-screen roll's seven attribute bytes were found,
/// plus whether the bytes around it also match the full record shape (a confirmed MAXCON and SKP at
/// their record offsets). A <see cref="Structural"/> match lets the roller also read and target
/// MAXCON; a plain match tracks the seven attributes only.
/// </summary>
public readonly record struct CreationMatch(nuint Address, bool Structural);

/// <summary>
/// Locates the <em>temporary</em> attribute buffer the game fills on the Ranger Center
/// "Create a character" screen. That roll is not a roster record yet (the character is not saved
/// until you accept it, name it, and pick skills), so the structural <see cref="PartyLocator"/>
/// cannot see it. Instead the caller reads the numbers off the create screen once; this
/// signature-scans memory for the seven contiguous attribute bytes (STR, IQ, LCK, SPD, AGL, DEX,
/// CHR) to pin the buffer's address, after which each spacebar re-roll can be read straight from
/// memory (<see cref="CharacterRollerViewModel"/>).
///
/// The seven-value signature (each byte in roughly <see cref="MinAttr"/>..<see cref="MaxAttr"/>) is
/// usually specific enough to resolve to a single address in a DOS game's small image. When the
/// caller also supplies the on-screen MAXCON and SKP, a hit is additionally checked for the full
/// character-record shape — MAXCON (u16) at the record's <see cref="CharacterFormat.OffMaxCon"/> and
/// SKP at <see cref="CharacterFormat.OffSkillPoints"/>, measured relative to the attribute base — so
/// the create buffer (which the game builds as a scratch character record) is confirmed rather than
/// guessed. Any remaining ambiguity is narrowed by re-rolling and keeping the candidate that keeps
/// changing within a plausible range. Nothing here writes to the game; it only reads.
/// </summary>
public static class CreationScanner
{
    /// <summary>The seven attribute bytes the signature is built from (STR..CHR).</summary>
    public const int AttributeCount = CharacterFormat.AttributeCount;

    /// <summary>Plausible inclusive range for one freshly-rolled attribute byte on the create screen.
    /// Kept generous (a base roll before any nationality choice sits well under this) but tight enough
    /// to reject coincidental byte runs while narrowing.</summary>
    public const int MinAttr = 1;
    public const int MaxAttr = 40;

    // Offsets of MAXCON, CON and SKP relative to the seven-attribute base, derived from the record
    // layout so they can never drift from CharacterFormat. The create buffer is a scratch character
    // record, so these hold there too when the record shape is confirmed.
    public const int AttrToMaxCon = CharacterFormat.OffMaxCon - CharacterFormat.OffAttributes; // 0x0D
    public const int AttrToCon = CharacterFormat.OffCon - CharacterFormat.OffAttributes;       // 0x0F
    public const int AttrToSkp = CharacterFormat.OffSkillPoints - CharacterFormat.OffAttributes; // 0x12

    /// <summary>Upper bound on returned matches, so a too-loose signature can't blow up memory.</summary>
    public const int MaxMatches = 4096;

    /// <summary>Per-region read cap (mirrors <see cref="PartyLocator"/>): one huge mapping can't
    /// trigger a multi-GB allocation.</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Pure pattern search within one buffer: offsets where the seven attribute bytes appear
    /// contiguously, i.e. <c>data[off + k] == attrs[k]</c> for all k. Factored out so it is
    /// unit-testable without a live process.
    /// </summary>
    public static IEnumerable<int> FindInBuffer(byte[] data, byte[] attrs)
    {
        if (attrs.Length == 0) yield break;
        for (int i = 0; i + attrs.Length <= data.Length; i++)
        {
            bool ok = true;
            for (int k = 0; k < attrs.Length; k++)
            {
                if (data[i + k] != attrs[k]) { ok = false; break; }
            }
            if (ok) yield return i;
        }
    }

    /// <summary>
    /// True when the bytes at <paramref name="attrOffset"/> in <paramref name="data"/> match the full
    /// character-record shape for a fresh roll: the little-endian MAXCON u16 equals
    /// <paramref name="maxCon"/> and the SKP byte equals <paramref name="skp"/>, each at its record
    /// offset relative to the attribute base. Bounds-checked; returns false if the record window would
    /// run past the buffer. Pure, so it is unit-testable.
    /// </summary>
    public static bool IsStructural(byte[] data, int attrOffset, int maxCon, int skp)
    {
        int end = attrOffset + AttrToSkp;             // the SKP byte is the furthest field we read
        if (attrOffset < 0 || end >= data.Length) return false;
        int m = data[attrOffset + AttrToMaxCon] | (data[attrOffset + AttrToMaxCon + 1] << 8);
        if (m != maxCon) return false;
        return data[attrOffset + AttrToSkp] == skp;
    }

    /// <summary>
    /// Scans all committed memory for the seven attribute bytes contiguously. When
    /// <paramref name="maxCon"/> and <paramref name="skp"/> are supplied (&gt; 0), each hit is also
    /// tested for the full record shape and flagged <see cref="CreationMatch.Structural"/>. Returns
    /// every match (capped at <see cref="MaxMatches"/>); the caller prefers structural ones.
    /// </summary>
    public static List<CreationMatch> Find(ProcessMemory mem, IReadOnlyList<int> attrs, int maxCon, int skp,
                                           CancellationToken ct = default)
    {
        var abytes = new byte[attrs.Count];
        for (int k = 0; k < attrs.Count; k++) abytes[k] = (byte)attrs[k];
        bool confirm = maxCon > 0 && skp >= 0;

        var matches = new List<CreationMatch>();
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            int want = (int)Math.Min((long)region.Size, MaxRegionBytes);

            // Read a small tail overlap past the region so a match — or its structural window — that
            // straddles into the next region is still seen whole (like PartyLocator's window overlap).
            // Anchors are only taken from the owned [0, want) part, so adjacent regions never double-report;
            // the overlap is just enough for the furthest byte IsStructural reads (SKP at AttrToSkp). If the
            // extended read fails because its tail is unreadable, fall back to reading just the region.
            int overlap = want < MaxRegionBytes ? AttrToSkp : 0;
            var data = mem.Read(region.Base, want + overlap);
            if (data.Length < want) data = mem.Read(region.Base, want);
            if (data.Length == 0) continue;

            foreach (int off in FindInBuffer(data, abytes))
            {
                if (off >= want) break;   // FindInBuffer yields ascending offsets; the rest are overlap the next region owns
                bool structural = confirm && IsStructural(data, off, maxCon, skp);
                matches.Add(new CreationMatch(region.Base + (nuint)off, structural));
                if (matches.Count >= MaxMatches) return matches;
            }
        }
        return matches;
    }

    /// <summary>Reads the seven attribute bytes at <paramref name="addr"/> into
    /// <paramref name="dest"/>. Returns false if the read came up short.</summary>
    public static bool TryReadAttributes(ProcessMemory mem, nuint addr, int[] dest)
    {
        var buf = mem.Read(addr, AttributeCount);
        if (buf.Length < AttributeCount) return false;
        for (int k = 0; k < AttributeCount; k++) dest[k] = buf[k];
        return true;
    }

    /// <summary>Reads the little-endian MAXCON u16 that sits at the record offset relative to the
    /// attribute base <paramref name="addr"/>. Returns false if the read came up short.</summary>
    public static bool TryReadMaxCon(ProcessMemory mem, nuint addr, out int maxCon)
    {
        maxCon = 0;
        var buf = mem.Read(addr + (nuint)AttrToMaxCon, 2);
        if (buf.Length < 2) return false;
        maxCon = buf[0] | (buf[1] << 8);
        return true;
    }

    /// <summary>True when every attribute value is within the plausible create-screen range.</summary>
    public static bool InRange(IReadOnlyList<int> attrs)
    {
        foreach (var v in attrs)
            if (v < MinAttr || v > MaxAttr) return false;
        return true;
    }
}
