namespace DarkDesigns1Trainer.Memory;

using System.Text;
using DarkDesigns1Trainer.Game;

/// <summary>
/// Finds the game's 64-entry item table in the running process, and patches the per-item
/// <em>potency</em> word (<see cref="ItemBook.EntryOffPotency"/>).
///
/// Dark Designs has no charge counters. On <c>(U)se</c> it applies the item's effect, rolls
/// <c>random(256)</c>, and destroys the item unless <c>potency &gt; roll</c>; a magic weapon's
/// special effect fires on the same test in combat. Raising potency to
/// <see cref="ItemBook.PotencyAlways"/> therefore makes usable items survive every use and magic
/// weapons trigger every hit — the nearest thing to "infinite charges" the game can express.
///
/// The table is located by <b>content</b>, never by a fixed offset: three known item names must
/// appear at the right 40-byte stride, and entry 0 must be the game's own "NO ITEM" placeholder.
/// This is table-wide game data, not per-character state, so a patch affects every character and
/// is not written to <c>DDCHARS.DAT</c> — it lasts only as long as the process.
/// </summary>
public static class ItemTableLocator
{
    private const int ChunkSize = 1 << 20;
    private const int PageSize = 0x1000;

    // Three names at a known stride make a signature specific enough that a false hit is not a
    // realistic concern; "NO ITEM" at entry 0 then confirms the base.
    private static readonly (int Id, string Name)[] Signature =
    {
        (4, "SHORT SWORD"),
        (5, "LONG SWORD"),
        (6, "BATTLE AXE"),
    };

    private const string Entry0Name = "NO ITEM";

    /// <summary>
    /// Address of item entry 0, or null when the table cannot be found. The entry for id
    /// <c>n</c> is at <c>base + n * <see cref="ItemBook.EntrySize"/></c>.
    /// </summary>
    public static nuint? Find(ProcessMemory mem, CancellationToken ct = default)
    {
        byte[] first = Encoding.ASCII.GetBytes(Signature[0].Name);
        int span = Signature[^1].Id * ItemBook.EntrySize + 64;

        foreach (var hit in Scan(mem, first, ct))
        {
            // hit is the name of entry Signature[0]; step back to entry 0.
            nuint entry0;
            unchecked
            {
                nuint back = (nuint)(Signature[0].Id * ItemBook.EntrySize + ItemBook.EntryOffName);
                if (hit < back) continue;
                entry0 = hit - back;
            }

            byte[] buf = new byte[span];
            if (mem.Read(entry0, buf, buf.Length) < span) continue;
            if (!Verify(buf)) continue;
            return entry0;
        }
        return null;
    }

    /// <summary>Checks the stride signature plus the "NO ITEM" placeholder at entry 0.</summary>
    private static bool Verify(byte[] buf)
    {
        if (!NameAt(buf, 0, Entry0Name)) return false;
        foreach (var (id, name) in Signature)
            if (!NameAt(buf, id, name)) return false;
        return true;
    }

    private static bool NameAt(byte[] buf, int id, string name)
    {
        int at = id * ItemBook.EntrySize + ItemBook.EntryOffName;
        if (at + name.Length > buf.Length) return false;
        for (int i = 0; i < name.Length; i++)
            if (buf[at + i] != (byte)name[i]) return false;
        return true;
    }

    /// <summary>Reads the live potency word for one item id.</summary>
    public static int ReadPotency(ProcessMemory mem, nuint tableBase, int itemId)
    {
        byte[] w = new byte[2];
        nuint at = tableBase + (nuint)(itemId * ItemBook.EntrySize + ItemBook.EntryOffPotency);
        if (mem.Read(at, w, 2) != 2) return -1;
        return w[0] | (w[1] << 8);
    }

    /// <summary>Writes the potency word for one item id. Returns false if the write fails.</summary>
    public static bool WritePotency(ProcessMemory mem, nuint tableBase, int itemId, int value)
    {
        byte[] w = { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) };
        nuint at = tableBase + (nuint)(itemId * ItemBook.EntrySize + ItemBook.EntryOffPotency);
        return mem.WriteRange(at, w, 0, 2);
    }

    // --- plumbing -------------------------------------------------------------
    private static IEnumerable<nuint> Scan(ProcessMemory mem, byte[] needle, CancellationToken ct)
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
                else if (want > PageSize)
                {
                    // Read reports a partial copy as 0, so a single unreadable page would
                    // otherwise skip the whole 1 MB chunk. Fall back to page-sized reads,
                    // matching what RosterLocator.FindAnchors already does.
                    foreach (var hit in ScanByPage(mem, start, regionEnd, needle, ct))
                        yield return hit;
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }
    }

    private static IEnumerable<nuint> ScanByPage(ProcessMemory mem, nuint start, nuint regionEnd,
                                                 byte[] needle, CancellationToken ct)
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
