using System.Text;
using LegacyOfTheAncientsTrainer.Game;

namespace LegacyOfTheAncientsTrainer.Memory;

/// <summary>A located character record: its live process address and a decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public CharacterRecord Record { get; }

    public LocatedCharacter(nuint address, CharacterRecord record)
    {
        Address = address;
        Record = record;
    }

    public override string ToString() => $"{Record.Name} @ 0x{(ulong)Address:X}";
}

/// <summary>
/// Locates the Legacy of the Ancients character record inside the attached emulator's memory.
///
/// Two strategies, tried in order:
///
/// 1. <b>Structural</b> — the primary strategy. Scans all readable memory for a
///    <see cref="CharacterFormat.RecordSize"/>-byte window that passes
///    <see cref="CharacterRecord.IsValidRecord"/> — a valid header (bytes 4-5 = 382),
///    a printable ASCII name starting with a letter, and plausible characteristic/HP/Level
///    values. The header record-size field (0x017E) is a strong discriminator that rejects
///    most stray byte runs.
///
/// 2. <b>Anchor</b> — a secondary strategy. The game title string may appear in the
///    data segment of a running module. If found, the locator searches a 256 KB window
///    forward for a valid character record. This is less reliable than the structural
///    scan because the EXEPACK-compressed modules may not carry the title string in
///    a predictable location.
///
/// Legacy of the Ancients is a single-character RPG, so the locator returns at most
/// one character.
/// </summary>
public static class CharacterLocator
{
    private const int ChunkSize = 1 << 20;     // 1 MiB scan window
    private const int PageSize = 0x1000;       // salvage granularity when a chunk read fails
    private const int AnchorWindow = 0x40000;  // 256 KB forward search from the anchor

    private static readonly byte[] AnchorBytes = Encoding.ASCII.GetBytes("LEGACY OF THE ANCIENTS");

    /// <summary>
    /// Finds the live character, or null if none can be located (not attached to the game,
    /// or the game isn't loaded past the title screen yet). Tries the structural scan first,
    /// then the anchor scan.
    /// </summary>
    public static LocatedCharacter? Find(IMemorySource mem, CancellationToken ct = default)
    {
        var byStructure = FindByStructure(mem, ct);
        if (byStructure != null) return byStructure;
        return FindByAnchor(mem, ct);
    }

    // --- strategy 1: structural scan (primary) ------------------------------
    private static LocatedCharacter? FindByStructure(IMemorySource mem, CancellationToken ct)
    {
        int overlap = CharacterFormat.RecordSize - 1;
        byte[] buf = new byte[ChunkSize + overlap];

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
                int read = mem.Read(start, buf, readLen);

                for (int i = 0; i + CharacterFormat.RecordSize <= read; i++)
                {
                    if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                    var rec = new CharacterRecord(buf, i);
                    return new LocatedCharacter(start + (nuint)i, rec);
                }

                if (read < readLen && want > PageSize)
                {
                    var hit = ScanByPage(mem, start, regionEnd, ct);
                    if (hit != null) return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
        return null;
    }

    /// <summary>Page-granular fallback for a region whose bulk read failed on an unreadable page.</summary>
    private static LocatedCharacter? ScanByPage(IMemorySource mem, nuint regionStart, nuint regionEnd, CancellationToken ct)
    {
        int overlap = CharacterFormat.RecordSize - 1;
        byte[] buf = new byte[PageSize + overlap];
        for (nuint start = regionStart; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)PageSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            if (read == 0 && readLen > want)
                read = mem.Read(start, buf, want);

            for (int i = 0; i + CharacterFormat.RecordSize <= read; i++)
            {
                if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                var rec = new CharacterRecord(buf, i);
                return new LocatedCharacter(start + (nuint)i, rec);
            }

            start += (nuint)want;
        }
        return null;
    }

    // --- strategy 2: anchor + forward window scan (secondary) ---------------
    private static LocatedCharacter? FindByAnchor(IMemorySource mem, CancellationToken ct)
    {
        foreach (var anchor in FindAnchors(mem, AnchorBytes, ct))
        {
            byte[] buf = new byte[AnchorWindow];
            int read = mem.Read(anchor, buf, AnchorWindow);
            for (int i = 0; i + CharacterFormat.RecordSize <= read; i++)
            {
                if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                var rec = new CharacterRecord(buf, i);
                return new LocatedCharacter(anchor + (nuint)i, rec);
            }
        }
        return null;
    }

    /// <summary>Re-reads the record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(IMemorySource mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;

    // --- anchor scan helpers -------------------------------------------------
    private static IEnumerable<nuint> FindAnchors(IMemorySource mem, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] buf = new byte[ChunkSize + overlap];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;
            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
                int read = mem.Read(start, buf, readLen);

                if (read >= needle.Length)
                {
                    for (int i = 0; i + needle.Length <= read; i++)
                        if (Matches(buf, i, needle))
                            yield return start + (nuint)i;
                }
                if (read < readLen && want > PageSize)
                {
                    foreach (var hit in ScanByPage(mem, start, regionEnd, needle, ct))
                        yield return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
    }

    private static IEnumerable<nuint> ScanByPage(IMemorySource mem, nuint start, nuint regionEnd, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < needle.Length && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < needle.Length) continue;

            for (int i = 0; i + needle.Length <= read; i++)
                if (Matches(page, i, needle))
                    yield return p + (nuint)i;
        }
    }

    private static bool Matches(byte[] buf, int i, byte[] needle)
    {
        for (int k = 0; k < needle.Length; k++)
            if (buf[i + k] != needle[k]) return false;
        return true;
    }
}
