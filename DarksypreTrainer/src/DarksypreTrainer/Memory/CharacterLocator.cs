using System.Text;
using DarksypreTrainer.Game;

namespace DarksypreTrainer.Memory;

/// <summary>
/// The three live addresses that together make up the player's state, plus the bytes read from
/// each at the moment they were found.
/// </summary>
public sealed class LocatedCharacter
{
    /// <summary>Address of the six-<see cref="ushort"/> status block the on-screen bars read from.</summary>
    public nuint StatusAddress { get; }

    /// <summary>Address of the character record: attribute bytes then the three maxima.</summary>
    public nuint RecordAddress { get; }

    /// <summary>Address of creature-table entry 0 — the player actor holding current HP and SP.</summary>
    public nuint ActorAddress { get; }

    /// <summary><see cref="CharacterFormat.StatusSize"/> bytes read at <see cref="StatusAddress"/>.</summary>
    public byte[] Status { get; }

    /// <summary><see cref="CharacterFormat.RecordSize"/> bytes read at <see cref="RecordAddress"/>.</summary>
    public byte[] Record { get; }

    /// <summary>
    /// The four bytes at <see cref="ActorAddress"/> + <see cref="CharacterFormat.ActorCurrentHp"/>:
    /// current hit points then current spell points, as the actor holds them.
    /// </summary>
    public byte[] ActorVitals { get; }

    public LocatedCharacter(nuint statusAddress, byte[] status, nuint recordAddress, byte[] record,
                            nuint actorAddress, byte[] actorVitals)
    {
        StatusAddress = statusAddress;
        Status = status;
        RecordAddress = recordAddress;
        Record = record;
        ActorAddress = actorAddress;
        ActorVitals = actorVitals;
    }

    public override string ToString() =>
        $"status 0x{(ulong)StatusAddress:X} record 0x{(ulong)RecordAddress:X} actor 0x{(ulong)ActorAddress:X}";
}

/// <summary>
/// Finds DarkSpyre's live character state in the attached emulator's memory without the user
/// hunting for addresses.
///
/// The search is content-based end to end — no hard-coded addresses, and no fixed distance
/// between the three structures, because only the <em>internal</em> layout of each is a
/// property of the build. Each stage confirms the next:
///
/// <list type="number">
/// <item><b>Player actor</b> — the string <c>player</c> is the name field of creature-table
/// entry 0, loaded verbatim from <c>CR.DAT</c>. Every hit is validated as an actor record
/// (<see cref="CharacterFormat.IsPlayerActor"/>) and yields the live HP and SP.</item>
/// <item><b>Status block</b> — scan the same region for six <see cref="ushort"/>s whose first
/// two equal the actor's HP and SP and whose maxima bracket the current values
/// (<see cref="CharacterFormat.IsStatusBlock"/>).</item>
/// <item><b>Character record</b> — scan for six in-range attribute bytes followed by exactly
/// the three maxima the status block reported
/// (<see cref="CharacterFormat.IsCharacterRecord"/>).</item>
/// </list>
///
/// Cross-checking the stages against each other is what makes the result unique: on the dumps
/// this was developed against, each stage resolved to exactly one address in 16 MB of guest RAM.
/// A stage that finds nothing means the game has not reached play yet (the menus have no
/// character), so <see cref="Find"/> returns null rather than guessing.
/// </summary>
public static class CharacterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window
    private const int PageSize = 0x1000;     // salvage granularity when a chunk read fails
    private const int AnyFirstByte = -1;     // no cheap first-byte reject available for this shape

    private static readonly byte[] ActorNameBytes =
        Encoding.ASCII.GetBytes(CharacterFormat.PlayerActorName + "\0");

    /// <summary>
    /// Locates the live character, or returns null when no character is in play. Safe to call
    /// repeatedly; it holds no state between calls.
    /// </summary>
    public static LocatedCharacter? Find(IMemorySource mem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            var hit = FindInRegion(mem, region, ct);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>Re-reads <paramref name="length"/> bytes at an address into <paramref name="buffer"/>.</summary>
    public static bool Reread(IMemorySource mem, nuint address, byte[] buffer, int length) =>
        mem.Read(address, buffer, length) == length;

    // --- per-region search ---------------------------------------------------
    private static LocatedCharacter? FindInRegion(IMemorySource mem, MemoryRegion region, CancellationToken ct)
    {
        foreach (nuint actor in FindActors(mem, region, ct))
        {
            var actorBytes = new byte[CharacterFormat.ActorSize];
            if (!Reread(mem, actor, actorBytes, actorBytes.Length)) continue;

            int hp = CharacterFormat.ReadU16(actorBytes, CharacterFormat.ActorCurrentHp);
            int sp = CharacterFormat.ReadU16(actorBytes, CharacterFormat.ActorCurrentSp);

            foreach (nuint status in Scan(mem, region, CharacterFormat.StatusSize, hp & 0xFF,
                         (buf, i) => CharacterFormat.IsStatusBlock(buf, i, hp, sp), ct))
            {
                var statusBytes = new byte[CharacterFormat.StatusSize];
                if (!Reread(mem, status, statusBytes, statusBytes.Length)) continue;

                int maxHp = CharacterFormat.ReadU16(statusBytes, CharacterFormat.StatusMaxHp);
                int maxSp = CharacterFormat.ReadU16(statusBytes, CharacterFormat.StatusMaxSp);
                int maxEnc = CharacterFormat.ReadU16(statusBytes, CharacterFormat.StatusMaxEnc);

                foreach (nuint record in Scan(mem, region, CharacterFormat.RecordSize, AnyFirstByte,
                             (buf, i) => CharacterFormat.IsCharacterRecord(buf, i, maxHp, maxSp, maxEnc), ct))
                {
                    if (record == status) continue;   // one window cannot be both structures
                    var recordBytes = new byte[CharacterFormat.RecordSize];
                    if (!Reread(mem, record, recordBytes, recordBytes.Length)) continue;

                    var vitals = new byte[4];
                    Array.Copy(actorBytes, CharacterFormat.ActorCurrentHp, vitals, 0, vitals.Length);
                    return new LocatedCharacter(status, statusBytes, record, recordBytes, actor, vitals);
                }
            }
        }
        return null;
    }

    // --- stage 1: the player actor ------------------------------------------
    private static IEnumerable<nuint> FindActors(IMemorySource mem, MemoryRegion region, CancellationToken ct)
    {
        foreach (nuint nameAddress in Scan(mem, region, ActorNameBytes.Length, ActorNameBytes[0],
                     (buf, i) => Matches(buf, i, ActorNameBytes), ct))
        {
            if (nameAddress < region.Base + CharacterFormat.ActorName) continue;
            nuint actor = nameAddress - CharacterFormat.ActorName;

            var buf = new byte[CharacterFormat.ActorSize];
            if (!Reread(mem, actor, buf, buf.Length)) continue;
            if (CharacterFormat.IsPlayerActor(buf, 0)) yield return actor;
        }
    }

    // --- chunked region scan -------------------------------------------------
    /// <summary>
    /// Walks a region in 1 MiB windows, offering every <paramref name="windowSize"/>-byte
    /// position to <paramref name="match"/>. Windows overlap by <c>windowSize - 1</c> bytes so a
    /// structure straddling a chunk boundary is still seen. A chunk whose bulk read fails is
    /// retried page by page, so one unreadable page does not blind the rest of the region.
    /// </summary>
    private static IEnumerable<nuint> Scan(
        IMemorySource mem, MemoryRegion region, int windowSize, int firstByte, Func<byte[], int, bool> match, CancellationToken ct)
    {
        int overlap = windowSize - 1;
        byte[] buf = new byte[ChunkSize + overlap];
        nuint regionEnd = region.Base + region.Size;

        for (nuint start = region.Base; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)ChunkSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            if (read < readLen && want > PageSize)
            {
                foreach (nuint hit in ScanByPage(mem, start, regionEnd, windowSize, firstByte, match, ct))
                    yield return hit;
                yield break;
            }

            for (int i = 0; i + windowSize <= read; i++)
            {
                if (firstByte >= 0 && buf[i] != firstByte) continue;
                if (match(buf, i)) yield return start + (nuint)i;
            }

            start += (nuint)Math.Max(PageSize, want);
        }
    }

    private static IEnumerable<nuint> ScanByPage(
        IMemorySource mem, nuint start, nuint regionEnd, int windowSize, int firstByte, Func<byte[], int, bool> match, CancellationToken ct)
    {
        int overlap = windowSize - 1;
        byte[] page = new byte[PageSize + overlap];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + overlap), remaining);
            int read = mem.Read(p, page, readLen);
            if (read < windowSize && readLen > PageSize)
                read = mem.Read(p, page, (int)Math.Min((nuint)PageSize, remaining));
            if (read < windowSize) continue;

            for (int i = 0; i + windowSize <= read; i++)
            {
                if (firstByte >= 0 && page[i] != firstByte) continue;
                if (match(page, i)) yield return p + (nuint)i;
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
