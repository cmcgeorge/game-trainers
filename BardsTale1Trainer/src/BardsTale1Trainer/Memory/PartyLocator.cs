using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.Memory;

/// <summary>
/// A located game data segment: the absolute address of DGROUP's byte 0 in the
/// target process. The party array and the roster-name rows sit at fixed offsets
/// from here (see <see cref="PartyFormat"/>).
/// </summary>
public readonly record struct PartyLocation(nuint DsBase)
{
    public nuint SlotAddress(int slot) =>
        DsBase + (nuint)(PartyFormat.DsPartySlots + slot * PartyFormat.RecordSize);

    public nuint RowAddress(int slot) =>
        DsBase + (nuint)(PartyFormat.DsPartyRows + slot * PartyFormat.PartyRowStride);

    public override string ToString() => $"DS @ 0x{(ulong)DsBase:X}";
}

/// <summary>
/// Finds the running game's data segment by scanning the target process for the
/// race-name table ("Human\0Elf\0Dwarf\0…", a unique byte string in BARD.EXE's
/// DGROUP) and validating two more string tables at their known segment offsets.
/// BARD.EXE itself is packed on disk, so these strings exist in plain form only in
/// the live, unpacked data segment — false positives are rare and the extra anchors
/// eliminate them.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window

    public static List<PartyLocation> FindAll(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var anchor = PartyFormat.RaceTableBytes;
        var hits = new List<PartyLocation>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var r in regions) totalBytes += r.Size;
        nuint scanned = 0;

        byte[] buf = new byte[ChunkSize];

        foreach (var region in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, want);
                if (read < anchor.Length)
                {
                    scanned += (nuint)want;
                    break;
                }

                for (int i = IndexOf(buf, read, anchor, 0);
                     i >= 0;
                     i = IndexOf(buf, read, anchor, i + 1))
                {
                    nuint raceAddr = region.Base + offset + (nuint)i;
                    if (raceAddr < (nuint)PartyFormat.DsRaceTable) continue;
                    nuint dsBase = raceAddr - (nuint)PartyFormat.DsRaceTable;
                    if (Validate(mem, dsBase) && !hits.Any(h => h.DsBase == dsBase))
                        hits.Add(new PartyLocation(dsBase));
                }

                // Advance, overlapping by the anchor length so a table straddling a
                // chunk edge is still seen.
                nuint advance = (nuint)Math.Max(1, read - anchor.Length);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        hits.Sort((a, b) => a.DsBase.CompareTo(b.DsBase));
        return hits;
    }

    /// <summary>Best single match, or null if none found.</summary>
    public static PartyLocation? Find(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var all = FindAll(mem, progress, ct);
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>
    /// A candidate DS base is accepted when the item table ("Torch\0Lamp\0…") and the
    /// class table ("Misc. Item\0Warrior\0…") are found at their fixed DGROUP offsets,
    /// and the 7 party slots pass the loose per-record sanity check.
    /// </summary>
    private static bool Validate(ProcessMemory mem, nuint dsBase)
    {
        if (!CheckBytes(mem, dsBase + (nuint)PartyFormat.DsItemTable, PartyFormat.ItemTableBytes)) return false;
        if (!CheckBytes(mem, dsBase + (nuint)PartyFormat.DsClassTable, PartyFormat.ClassTableBytes)) return false;

        var slots = new byte[PartyFormat.RecordSize * PartyFormat.PartySlots];
        if (mem.Read(dsBase + (nuint)PartyFormat.DsPartySlots, slots, slots.Length) != slots.Length)
            return false;
        // Check slots 1..6 only. Slot 0 is the special / summoned-monster slot, whose
        // occupant can carry a class id or stats outside mortal bounds (LooksLikeSlot
        // would reject it), so gating on it would make a valid party fail to be found
        // mid-combat. The string-table anchors above already rule out false positives.
        for (int k = 1; k < PartyFormat.PartySlots; k++)
            if (!PartyFormat.LooksLikeSlot(slots, k * PartyFormat.RecordSize))
                return false;
        return true;
    }

    private static bool CheckBytes(ProcessMemory mem, nuint addr, byte[] expected)
    {
        var buf = new byte[expected.Length];
        if (mem.Read(addr, buf, buf.Length) != buf.Length) return false;
        return buf.AsSpan().SequenceEqual(expected);
    }

    /// <summary>First index of <paramref name="pattern"/> within buf[start..length), or -1.</summary>
    private static int IndexOf(byte[] buf, int length, byte[] pattern, int start)
    {
        int limit = length - pattern.Length;
        for (int i = Math.Max(0, start); i <= limit; i++)
        {
            if (buf[i] != pattern[0]) continue;
            bool ok = true;
            for (int k = 1; k < pattern.Length; k++)
                if (buf[i + k] != pattern[k]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
