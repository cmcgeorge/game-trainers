using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer.Memory;

/// <summary>A located character/monster record: its live process address and a decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public CharacterRecord Record { get; }

    public LocatedCharacter(nuint address, CharacterRecord record)
    {
        Address = address;
        Record = record;
    }

    public bool IsMonster => Record.LooksLikeMonster;

    public override string ToString() => $"{Record.Name} @ 0x{(ulong)Address:X}";
}

/// <summary>
/// Scans a target process for Pool of Radiance character/monster records by testing the
/// <see cref="CharacterSignature"/> at every byte offset of every committed region.
/// Party members and any in-combat monsters share the same record format, so both are
/// returned; the caller distinguishes them (monsters have race 0 / class 17).
/// </summary>
public static class CharacterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window

    public static List<LocatedCharacter> FindAll(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var hits = new List<LocatedCharacter>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var r in regions) totalBytes += r.Size;
        nuint scanned = 0;

        // One reusable buffer across the whole walk avoids thousands of LOH allocations.
        byte[] buf = new byte[ChunkSize + PorFormat.RecordSize];
        var seen = new HashSet<ulong>();

        foreach (var region in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - offset);
                // Read an extra record's worth so a record straddling a chunk edge is still seen.
                int readWant = (int)Math.Min((nuint)(ChunkSize + PorFormat.RecordSize), region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, readWant);
                if (read < PorFormat.RecordSize)
                {
                    scanned += (nuint)want;
                    break;
                }

                for (int i = 0; i + PorFormat.RecordSize <= read; i++)
                {
                    if (!CharacterSignature.Looks(buf, i)) continue;
                    nuint absolute = region.Base + offset + (nuint)i;
                    if (!seen.Add((ulong)absolute)) continue;
                    hits.Add(new LocatedCharacter(absolute, new CharacterRecord(buf, i)));
                }

                // On a full read the +RecordSize overlap already caught boundary-straddling
                // records, so advance by `want`. On a short (partial) read, advance only past
                // what we could actually scan so readable records past the gap aren't skipped.
                nuint advance = read >= want
                    ? (nuint)want
                    : (nuint)Math.Max(1, read - PorFormat.RecordSize + 1);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        // Party members cluster together (adjacent-ish addresses); monsters live in the
        // combat arena. Sort by address so the party reads top-to-bottom in game order.
        hits.Sort((a, b) => a.Address.CompareTo(b.Address));
        return hits;
    }

    /// <summary>
    /// Re-reads a single record into a caller-supplied scratch buffer (length >= record size),
    /// for the poll loop — reusing one buffer across all characters avoids per-tick allocation.
    /// Returns true if the full record was read.
    /// </summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, PorFormat.RecordSize) == PorFormat.RecordSize;
}
