using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.Memory;

/// <summary>A located character record: its live process address and a decoded view.</summary>
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

/// <summary>
/// Locates the Dragon Wars party roster inside the attached emulator's memory.
///
/// The roster is not found by scanning for character records (empty slots and monsters would
/// confuse that); instead it is anchored to DATA1's chunk-0 header — a unique byte run that
/// loads verbatim into guest RAM (see <see cref="RosterFormat.AnchorBytes"/>). Roster slot 0
/// sits a fixed delta past the anchor, and each slot is a fixed-size record. Occupied slots
/// are returned; empty (0xFF-filled) slots are skipped.
/// </summary>
public static class RosterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window

    /// <summary>
    /// Finds the roster and returns every occupied character slot, or an empty list if the
    /// anchor can't be found (not attached to Dragon Wars, or the game isn't loaded yet).
    /// </summary>
    public static List<LocatedCharacter> FindAll(ProcessMemory mem, CancellationToken ct = default)
    {
        var hits = new List<LocatedCharacter>();
        foreach (var anchor in FindAnchors(mem, RosterFormat.AnchorBytes, ct))
        {
            nuint rosterBase = anchor + (nuint)RosterFormat.RosterAnchorDelta;
            var slots = ReadRoster(mem, rosterBase);
            if (slots.Count == 0) continue;    // anchor matched but no live party behind it
            return slots;                      // first anchor with a real party wins
        }
        return hits;
    }

    private static List<LocatedCharacter> ReadRoster(ProcessMemory mem, nuint rosterBase)
    {
        var slots = new List<LocatedCharacter>();
        for (int i = 0; i < RosterFormat.MaxSlots; i++)
        {
            nuint addr = rosterBase + (nuint)(i * RosterFormat.RecordSize);
            var buf = mem.Read(addr, RosterFormat.RecordSize);
            if (buf.Length != RosterFormat.RecordSize) break;
            var rec = new CharacterRecord(buf);
            if (rec.IsOccupied) slots.Add(new LocatedCharacter(addr, i, rec));
        }
        return slots;
    }

    /// <summary>Re-reads a single record into a caller-supplied scratch buffer for the poll loop.</summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer) =>
        mem.Read(address, buffer, RosterFormat.RecordSize) == RosterFormat.RecordSize;

    // --- anchor scan ---------------------------------------------------------
    private static IEnumerable<nuint> FindAnchors(ProcessMemory mem, byte[] needle, CancellationToken ct)
    {
        byte[] buf = new byte[ChunkSize + needle.Length];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - offset);
                int readWant = (int)Math.Min((nuint)(ChunkSize + needle.Length), region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, readWant);
                if (read < needle.Length) break;

                for (int i = 0; i + needle.Length <= read; i++)
                    if (Matches(buf, i, needle))
                        yield return region.Base + offset + (nuint)i;

                // Advance by what was actually read minus the needle overlap so a match that
                // straddles a chunk boundary is still seen; never advance by zero.
                nuint advance = (nuint)Math.Max(1, read - needle.Length + 1);
                offset += advance;
            }
        }
    }

    private static bool Matches(byte[] buf, int i, byte[] needle)
    {
        for (int k = 0; k < needle.Length; k++)
            if (buf[i + k] != needle[k]) return false;
        return true;
    }
}
