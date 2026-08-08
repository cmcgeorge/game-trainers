using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.Memory;

/// <summary>A located character record: its live process address, roster slot, and decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public LocatedCharacter(nuint address, int slot, CharacterRecord record)
    {
        Address = address;
        Slot = slot;
        Record = record;
    }

    public override string ToString() => $"{Record.Name} @ 0x{(ulong)Address:X}";
}

/// <summary>The located party: the base address of roster slot 0 plus every occupied member.</summary>
public sealed class LocatedParty
{
    public nuint RosterBase { get; }
    public IReadOnlyList<LocatedCharacter> Members { get; }

    public LocatedParty(nuint rosterBase, IReadOnlyList<LocatedCharacter> members)
    {
        RosterBase = rosterBase;
        Members = members;
    }
}

/// <summary>
/// Locates the Fountain of Dreams party roster inside the attached emulator's memory.
///
/// Fountain of Dreams' record allocation moves every session and has no stable static byte-run
/// adjacent to it to anchor to, so the roster is found by <b>structure</b>: it is an array of
/// <see cref="CharacterFormat.MaxSlots"/> contiguous <see cref="CharacterFormat.RecordSize"/>-byte
/// records where the occupied members pack from slot 0 (an occupied slot never follows an empty
/// one), at least one slot is occupied, and every occupied slot passes
/// <see cref="CharacterRecord.IsValidRecord"/> — a 1..18-char NUL-terminated printable-ASCII name
/// starting with a letter, seven attribute bytes each in 1..20, a plausible MaxCON (1..999),
/// a plausible level (1..99), and a profession in 0..6.
///
/// Unlike Wasteland, there is no party-state header before the roster, so the locator accepts the
/// first valid packed roster it finds (the party has at most 3 members, which is a tight enough
/// constraint to avoid most false positives). The whole address space is swept.
/// </summary>
public static class PartyLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;      // salvage granularity
    private static readonly int RosterBytes = CharacterFormat.MaxSlots * CharacterFormat.RecordSize;

    /// <summary>Finds the live roster, or null if none can be located.</summary>
    public static LocatedParty? Find(IMemorySource mem, CancellationToken ct = default)
    {
        LocatedParty? best = null;
        int bestCount = 0;
        int overlap = RosterBytes - 1;
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

                for (int i = 0; i + RosterBytes <= read; i++)
                {
                    if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                    var party = TryReadRoster(buf, i, start);
                    if (party == null) continue;
                    if (best == null || party.Members.Count > bestCount)
                    {
                        best = party;
                        bestCount = party.Members.Count;
                    }
                    if (bestCount >= CharacterFormat.MaxSlots) return best;
                }

                if (read < readLen && want > PageSize)
                {
                    ScanByPage(mem, start, regionEnd, ct, ref best, ref bestCount);
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }

        return best;
    }

    /// <summary>Page-granular fallback for a region whose bulk read failed on an unreadable page.</summary>
    private static void ScanByPage(IMemorySource mem, nuint regionStart, nuint regionEnd,
        CancellationToken ct, ref LocatedParty? best, ref int bestCount)
    {
        int overlap = RosterBytes - 1;
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

            for (int i = 0; i + RosterBytes <= read; i++)
            {
                if (!CharacterRecord.IsValidRecord(buf, i)) continue;
                var party = TryReadRoster(buf, i, start);
                if (party == null) continue;
                if (best == null || party.Members.Count > bestCount)
                {
                    best = party;
                    bestCount = party.Members.Count;
                }
                if (bestCount >= CharacterFormat.MaxSlots) return;
            }

            start += (nuint)want;
        }
    }

    /// <summary>
    /// Validates the MaxSlots-slot window and, if it holds, returns its occupied members with live
    /// addresses; otherwise null.
    /// </summary>
    private static LocatedParty? TryReadRoster(byte[] buf, int offset, nuint windowBase)
    {
        var slots = new List<LocatedCharacter>();
        bool seenEmpty = false;
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            int off = offset + i * CharacterFormat.RecordSize;
            if (CharacterRecord.IsValidRecord(buf, off))
            {
                if (seenEmpty) return null;
                var rec = new CharacterRecord(buf, off);
                slots.Add(new LocatedCharacter(windowBase + (nuint)off, i, rec));
            }
            else if (IsEmptySlot(buf, off))
            {
                seenEmpty = true;
            }
            else
            {
                return null;
            }
        }
        return slots.Count > 0 ? new LocatedParty(windowBase + (nuint)offset, slots) : null;
    }

    /// <summary>A roster slot is empty when its name field begins with a 0x00 pad byte.</summary>
    private static bool IsEmptySlot(byte[] buf, int off) =>
        buf[off + CharacterFormat.OffName] == 0x00;

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(IMemorySource mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, CharacterFormat.RecordSize) == CharacterFormat.RecordSize;
}
