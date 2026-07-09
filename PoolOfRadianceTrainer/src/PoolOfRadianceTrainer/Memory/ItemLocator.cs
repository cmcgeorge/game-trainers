using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer.Memory;

/// <summary>A located carried-item instance: its live process address and a decoded view.</summary>
public sealed class LocatedItem
{
    public nuint Address { get; }
    public ItemEntry Item { get; }

    public LocatedItem(nuint address, ItemEntry item)
    {
        Address = address;
        Item = item;
    }

    public override string ToString() => $"{Item.DisplayName} @ 0x{(ulong)Address:X}";
}

/// <summary>
/// Finds a character's carried-item instances in the running game. Item records aren't at a fixed
/// stride (a variable-length combat-icon bitmap and per-item link pointers sit between them), so
/// this signature-scans the address range that immediately follows a character record — the space
/// the game keeps that character's item linked list in — for <see cref="ItemSignature"/> matches.
/// </summary>
public static class ItemLocator
{
    /// <summary>Upper bound on how far past a character record to scan for its items, so the last
    /// party member (with no following record to cap the range) can't trigger a huge read.</summary>
    public const int MaxSpan = 0x8000;   // 32 KiB

    /// <summary>Scans <c>[start, limit)</c> (capped at <see cref="MaxSpan"/>) for item records.</summary>
    public static List<LocatedItem> FindInRange(ProcessMemory mem, nuint start, nuint limit)
    {
        var items = new List<LocatedItem>();
        if (limit <= start) return items;

        nuint spanN = limit - start;
        int span = spanN > (nuint)MaxSpan ? MaxSpan : (int)spanN;
        if (span < ItemEntry.RecordSize) return items;

        var buf = new byte[span];
        int read = ReadReadable(mem, start, buf, span);

        for (int i = 0; i + ItemEntry.RecordSize <= read;)
        {
            if (ItemSignature.Looks(buf, i))
            {
                items.Add(new LocatedItem(start + (nuint)i, new ItemEntry(buf, i)));
                i += ItemEntry.RecordSize;   // skip past a matched record to avoid overlapping hits
            }
            else i++;
        }
        return items;
    }

    private const int PageSize = 0x1000;   // 4 KiB — the granularity Windows maps/protects at

    /// <summary>
    /// Reads up to <paramref name="span"/> readable bytes at <paramref name="start"/> into
    /// <paramref name="buf"/>, returning the count of contiguous readable bytes. Item scans run to
    /// the next located record — or, for the last party member, to a generous cap — so the range
    /// routinely overruns the end of the committed region into unmapped memory. A single
    /// <see cref="ProcessMemory.Read"/> spanning a mapped→unmapped boundary fails wholesale (returns
    /// 0), which would drop every item; reading in page-aligned chunks (each wholly inside one page)
    /// captures the readable head and simply stops at the first unreadable page.
    /// </summary>
    private static int ReadReadable(ProcessMemory mem, nuint start, byte[] buf, int span)
    {
        var chunk = new byte[PageSize];
        int total = 0;
        while (total < span)
        {
            nuint addr = start + (nuint)total;
            int toPageEnd = PageSize - (int)((ulong)addr & (PageSize - 1));
            int want = Math.Min(toPageEnd, span - total);
            int got = mem.Read(addr, chunk, want);
            if (got <= 0) break;
            Array.Copy(chunk, 0, buf, total, got);
            total += got;
            if (got < want) break;   // short read — the rest of this page is unreadable
        }
        return total;
    }
}
