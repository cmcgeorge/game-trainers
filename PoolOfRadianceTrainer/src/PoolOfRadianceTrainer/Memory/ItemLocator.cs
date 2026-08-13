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
/// Finds a character's carried items in the running game by walking the game's own list: the
/// character record holds a far pointer to its first item (<see cref="PorFormat.OffItemsPtr"/>) and
/// each item record holds one to the next (<see cref="ItemEntry.OffNextLink"/>), ending at null.
///
/// <para>This has to follow the links rather than sweep an address range, because the game allocates
/// item records wherever there is room in its heap: they are not adjacent, not in list order, and not
/// necessarily anywhere near the character that owns them — a real party had six items packed within
/// 1 KB of the record and the seventh over 8 KB away. Sweeping a range around the record therefore
/// both misses items and picks up free heap slots that still hold a plausible-looking dead record,
/// and nothing about a swept record says which character it belongs to. Following the list gives
/// exactly the items the game shows, in the order it shows them.</para>
/// </summary>
public static class ItemLocator
{
    /// <summary>Stop following a chain after this many hops. Well past a full pack, so a corrupted
    /// or circular link can't spin forever.</summary>
    private const int MaxChain = 64;

    /// <summary>How far either side of a character record to look for its first item while working
    /// out the guest→host offset. The guest is a DOS program, so its records and its heap all live
    /// inside one megabyte of emulated RAM; this window spans that whichever way the heap grew.</summary>
    private const int BaseSearchWindow = 0x100000;   // 1 MiB

    private const int PageSize = 0x1000;             // 4 KiB — Windows' map/protect granularity

    /// <summary>Reads a character's item-list head pointer out of its record.</summary>
    public static FarPointer HeadOf(CharacterRecord record) =>
        FarPointer.Read(record.Bytes, PorFormat.OffItemsPtr);

    /// <summary>
    /// What <see cref="ResolveGuestBaseDetailed"/> found: the guest→host offset, how well it was
    /// corroborated, and whether anything else could have been chosen. Reported rather than
    /// swallowed because this one number underpins every later live item read and write — if it is
    /// wrong, the trainer edits the wrong bytes of a running game.
    /// </summary>
    /// <param name="Base">The resolved guest→host offset.</param>
    /// <param name="ChainLength">How many items the winning candidate's chain walked.</param>
    /// <param name="ExpectedCount">The owner record's own item count, which the chain should match.</param>
    /// <param name="Ambiguous">True when a rival candidate walked an equally good chain from a
    /// different offset, so the choice between them rested on nothing stronger than order.</param>
    public readonly record struct GuestBase(nuint Base, int ChainLength, int ExpectedCount, bool Ambiguous)
    {
        /// <summary>The chain length agrees with the record's own item count — a coincidence a wrong
        /// offset is very unlikely to produce.</summary>
        public bool CountMatched => ChainLength == ExpectedCount;
    }

    /// <summary>
    /// Works out where the guest's RAM sits in the host process, by finding the host address the
    /// character's first item must be at. DOSBox maps the emulated RAM as one flat block, so a single
    /// guest address paired with its host address fixes the offset for the whole session — and every
    /// candidate is checked by walking the chain with it, which a wrong offset cannot survive.
    /// Returns null when no candidate produces a well-formed list.
    /// </summary>
    public static nuint? ResolveGuestBase(ProcessMemory mem, LocatedCharacter owner) =>
        ResolveGuestBaseDetailed(mem, owner)?.Base;

    /// <summary>
    /// As <see cref="ResolveGuestBase"/>, but reports how well the answer is corroborated.
    ///
    /// <para>Every candidate is scored, rather than the first well-formed chain being taken: a
    /// chain whose length matches the owner's own item count wins outright, and among candidates
    /// that only walk cleanly the longest chain wins, because each additional link is another whole
    /// item record a wrong offset would have had to land on by accident. Ties are broken by the
    /// lowest offset so the same session resolves the same way twice, and reported as ambiguous.</para>
    /// </summary>
    public static GuestBase? ResolveGuestBaseDetailed(ProcessMemory mem, LocatedCharacter owner)
    {
        var head = HeadOf(owner.Record);
        if (head.IsNull) return null;
        int expected = owner.Record.Bytes[PorFormat.OffNumberOfItems];

        nuint from = owner.Address > (nuint)BaseSearchWindow ? owner.Address - (nuint)BaseSearchWindow : 0;
        nuint to = owner.Address + (nuint)BaseSearchWindow;

        nuint bestBase = 0;
        int bestLength = 0;
        bool bestMatched = false, ambiguous = false;

        foreach (nuint candidate in ScanForItemRecords(mem, from, to))
        {
            if (candidate < (nuint)head.Linear) continue;
            nuint guestBase = candidate - (nuint)head.Linear;
            int count = Walk(mem, guestBase, head, null);
            if (count == 0) continue;
            bool matched = count == expected;

            if (bestLength == 0)                                   // first well-formed candidate
            {
                bestBase = guestBase; bestLength = count; bestMatched = matched;
            }
            else if (matched && !bestMatched)                      // count corroboration beats length
            {
                bestBase = guestBase; bestLength = count; bestMatched = true; ambiguous = false;
            }
            else if (matched == bestMatched)
            {
                if (count > bestLength) { bestBase = guestBase; bestLength = count; ambiguous = false; }
                else if (count == bestLength && guestBase != bestBase)
                {
                    ambiguous = true;
                    if (guestBase < bestBase) bestBase = guestBase;
                }
            }

            // An exact count match on a chain the record itself vouches for cannot be improved on.
            if (bestMatched && bestLength == expected && !ambiguous) break;
        }

        return bestLength == 0 ? null : new GuestBase(bestBase, bestLength, expected, ambiguous);
    }

    /// <summary>The items on a character's list, in the order the game shows them.</summary>
    public static List<LocatedItem> FollowChain(ProcessMemory mem, nuint guestBase, CharacterRecord record)
    {
        var items = new List<LocatedItem>();
        Walk(mem, guestBase, HeadOf(record), items);
        return items;
    }

    /// <summary>Walks a chain, optionally collecting it. Returns the number of items walked, or 0 if
    /// the chain is not well-formed (a link that isn't readable, or doesn't point at an item record).</summary>
    private static int Walk(ProcessMemory mem, nuint guestBase, FarPointer head, List<LocatedItem>? into)
    {
        var buf = new byte[ItemEntry.RecordSize];
        var seen = new HashSet<nuint>();
        var link = head;
        int n = 0;

        while (!link.IsNull && n < MaxChain)
        {
            nuint addr = guestBase + (nuint)link.Linear;
            if (!seen.Add(addr)) return 0;                                   // a loop: not a real list
            if (mem.Read(addr, buf, ItemEntry.RecordSize) < ItemEntry.RecordSize) return 0;
            if (!ItemSignature.Looks(buf, 0)) return 0;

            var item = new ItemEntry(buf, 0);
            into?.Add(new LocatedItem(addr, item));
            n++;
            link = item.NextLink;
        }
        return link.IsNull ? n : 0;   // ran past MaxChain without terminating — treat as malformed
    }

    /// <summary>Every address in <c>[from, to)</c> whose bytes look like an item record. Used only to
    /// generate candidates for <see cref="ResolveGuestBase"/>.</summary>
    private static IEnumerable<nuint> ScanForItemRecords(ProcessMemory mem, nuint from, nuint to)
    {
        const int Chunk = 0x10000;                      // 64 KiB, re-read with an overlap for records
        var buf = new byte[Chunk + ItemEntry.RecordSize];   // that straddle a chunk boundary

        for (nuint at = from; at < to;)
        {
            int want = (int)Math.Min((ulong)(to - at), (ulong)buf.Length);
            int read = ReadReadable(mem, at, buf, want);
            if (read >= ItemEntry.RecordSize)
                for (int i = 0; i + ItemEntry.RecordSize <= read; i++)
                    if (ItemSignature.Looks(buf, i))
                        yield return at + (nuint)i;

            // Advance a whole chunk when the read succeeded; otherwise skip the unreadable page.
            at += (nuint)(read >= Chunk ? Chunk : Math.Max(read, PageSize));
        }
    }

    /// <summary>
    /// Reads up to <paramref name="span"/> readable bytes at <paramref name="start"/>, returning the
    /// count of contiguous readable bytes. A single <see cref="ProcessMemory.Read"/> spanning a
    /// mapped→unmapped boundary fails wholesale, so this reads in page-aligned chunks and stops at
    /// the first unreadable page instead of losing the readable head.
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
