using System.Diagnostics;
using System.Text;
using Space1889Trainer.Game;

namespace Space1889Trainer.Memory;

/// <summary>Which Space 1889 executable vouched for the located block, if any.</summary>
public enum PointerWitness
{
    /// <summary>No running Space 1889 program was found holding a pointer to the block.</summary>
    None,

    /// <summary><c>M.EXE</c> (the ground game) holds a far pointer to the block.</summary>
    GroundGame,

    /// <summary><c>S.EXE</c> (ether-flyer travel and ship combat) holds a far pointer to the block.</summary>
    SpaceGame,

    /// <summary><c>CG.EXE</c> (the character generator) holds a far pointer to the block.</summary>
    CharacterGenerator,
}

/// <summary>Everything one successful locate resolved.</summary>
public sealed record LocatedState(
    nuint BlockHost,
    nuint GuestZero,
    long GuestLinear,
    PointerWitness Witness,
    int AnchorHits,
    int ValidatedCandidates,
    long ElapsedMilliseconds)
{
    /// <summary>The block's guest address as a DOS <c>segment:offset</c> (offset 0 when paragraph-aligned).</summary>
    public string GuestAddress => GuestLinear < 0 ? "unknown" : $"{GuestLinear >> 4:X4}:{GuestLinear & 0xF:X4}";
}

/// <summary>
/// Finds Space 1889's 20,198-byte game-state block inside a running DOSBox, with no value searching.
/// <para>
/// <b>What the block is.</b> <c>1889.COM</c> is a tiny launcher ("SUPER") that allocates one DOS memory
/// block, then chains <c>SETUP.EXE</c>, <c>CG.EXE</c>, <c>M.EXE</c> and <c>S.EXE</c> in and out of the
/// rest of conventional memory. Every one of them receives that block's segment and keeps a far
/// pointer to it in its data group; a saved game (<c>*.SAV</c>, and the shipped new-game <c>DEF.S</c>)
/// is a verbatim image of it. (<c>START.OBJ</c> is not: only its first 0x3462 bytes are the ground-object table.) Because the launcher owns the allocation, the block
/// stays at the same guest address while the executables replace each other around it.
/// </para>
/// <para>
/// <b>How it is found.</b> The sixth of the block's 255-byte character records is the party bank
/// account, and the game names it <c>"PARTY ACCT."</c> at <see cref="StateFormat.PartyAccountNameOffset"/>.
/// Every hit on that text is a candidate block (hit − 0x5ED) that must then pass
/// <see cref="StateFormat.LooksLikeState"/>: every character slot either empty or carrying a printable,
/// terminated name and an <c>m</c>/<c>f</c> sex byte, at least one of them occupied, and the location
/// bytes in range. <c>M.EXE</c>'s own data group carries a second copy of the same text, which fails
/// that check because the bytes in front of it are strings, not records.
/// </para>
/// <para>
/// <b>Corroboration.</b> The locator then pins guest linear 0 through the emulated BIOS data area and
/// looks for a Turbo C data group — the copyright banner sits at <c>DS:0004</c> in all four programs —
/// whose far pointer to the state block (<c>M.EXE</c> <c>DS:0x5737</c>, <c>S.EXE</c> <c>DS:0x2DD5</c>,
/// <c>CG.EXE</c> <c>DS:0x1DF5</c>) resolves to the candidate. A witnessed candidate outranks an
/// unwitnessed one. An unwitnessed candidate is still accepted — the launcher's block legitimately has
/// no pointer to it for the moment one program has exited and the next has not yet started — and the
/// result says so. Among unwitnessed candidates, one whose guest address could be pinned through the
/// BIOS data area (that is, one inside emulated RAM) outranks one that could not.
/// </para>
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;
    private const int PageSize = 0x1000;

    /// <summary>Guest RAM is one large allocation; the housekeeping regions around it are much smaller.</summary>
    private const int MinGuestRegionBytes = 1 << 20;

    /// <summary>Process names worth offering, in the order they are listed.</summary>
    public static readonly IReadOnlyList<string> EmulatorProcessNames = new[]
    {
        "DOSBox", "dosbox", "DOSBox-X", "dosbox-x", "DOSBox-notX", "DOSBox-X-SDL2", "dosbox-staging",
    };

    private static readonly byte[] Anchor = Encoding.ASCII.GetBytes(StateFormat.PartyAccountName + "\0");
    private static readonly byte[] TurboCBanner = Encoding.ASCII.GetBytes(GameFacts.TurboCBanner);

    /// <summary>Every running process that looks like a DOS emulator.</summary>
    public static IReadOnlyList<Process> FindEmulators()
    {
        var found = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            bool match = p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase);
            if (!match)
                foreach (var n in EmulatorProcessNames)
                    if (p.ProcessName.Equals(n, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
            if (match) found.Add(p); else p.Dispose();
        }
        return found;
    }

    /// <summary>Locates the block, or returns null with a reason in <paramref name="status"/>.</summary>
    public static LocatedState? Find(IMemorySource mem, out string status, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mem);
        var sw = Stopwatch.StartNew();
        int anchorHits = 0;
        var candidates = new List<(nuint Host, MemoryRegion Region)>();
        var guestRegions = new List<MemoryRegion>();

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < MinGuestRegionBytes) continue;
            guestRegions.Add(region);

            foreach (nuint hit in Sweep(mem, region, Anchor, ct))
            {
                anchorHits++;
                nuint offset = (nuint)StateFormat.PartyAccountNameOffset;
                if (hit < offset || hit - offset < region.Base) continue;
                nuint block = hit - offset;
                if (block + (nuint)StateFormat.BlockLength > region.Base + region.Size) continue;
                var bytes = mem.Read(block, StateFormat.BlockLength);
                if (bytes.Length != StateFormat.BlockLength || !StateFormat.LooksLikeState(bytes)) continue;
                candidates.Add((block, region));
            }
        }

        if (candidates.Count == 0)
        {
            sw.Stop();
            status = anchorHits == 0
                ? "Space 1889 was not found in that process. Start the game (1889.COM) in DOSBox, begin or load a game, then Attach."
                : $"Found {anchorHits} copy/copies of the party-account name, but none sits in a valid game-state block. "
                  + "Begin or load a game (the character generator's pool is not the live party), then Attach.";
            return null;
        }

        // Rank: a block some running Space 1889 program points at beats one nobody does; then one inside
        // emulated RAM (guest address pinned) beats a stray copy in some other host allocation; ties go to
        // the lowest address, where the launcher's allocation sits below every program image.
        LocatedState? best = null;
        foreach (var (host, region) in candidates.OrderBy(c => c.Host))
        {
            ct.ThrowIfCancellationRequested();
            bool pinned = TryFindGuestZero(mem, region, out nuint zero);
            long linear = pinned ? (long)(host - zero) : -1;
            var witness = pinned ? FindWitness(mem, region, zero, linear, ct) : PointerWitness.None;
            var located = new LocatedState(host, pinned ? zero : 0, linear, witness, anchorHits, candidates.Count, 0);
            if (best is null || Rank(located) > Rank(best)) best = located;
            if (witness != PointerWitness.None) break;
        }

        sw.Stop();
        best = best! with { ElapsedMilliseconds = sw.ElapsedMilliseconds };
        string who = best.Witness switch
        {
            PointerWitness.GroundGame => "confirmed by M.EXE's state pointer",
            PointerWitness.SpaceGame => "confirmed by S.EXE's state pointer",
            PointerWitness.CharacterGenerator => "confirmed by CG.EXE's state pointer",
            _ => "no running Space 1889 program holds a pointer to it right now",
        };
        status = $"Located the game state at guest {best.GuestAddress} in {best.ElapsedMilliseconds} ms ({who}; "
               + $"{best.ValidatedCandidates} valid block(s) from {anchorHits} anchor hit(s)).";
        return best;
    }

    private static int Rank(LocatedState s) => s.Witness != PointerWitness.None ? 2 : s.GuestLinear >= 0 ? 1 : 0;

    /// <summary>
    /// Cheap re-check used before every write: the block must still pass the structural test.
    /// DOSBox can be closed, or the game quit to DOS and something else loaded, between one edit
    /// and the next.
    /// </summary>
    public static bool StillValid(IMemorySource mem, nuint blockHost)
    {
        var bytes = mem.Read(blockHost, StateFormat.BlockLength);
        return bytes.Length == StateFormat.BlockLength && StateFormat.LooksLikeState(bytes);
    }

    // --- sweep -------------------------------------------------------------------

    private static IEnumerable<nuint> Sweep(IMemorySource mem, MemoryRegion region, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] buf = new byte[ChunkSize + overlap];
        nuint regionEnd = region.Base + region.Size;

        for (nuint start = region.Base; start < regionEnd;)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - start;
            int want = (int)Math.Min((nuint)ChunkSize, remaining);
            int readLen = (int)Math.Min((nuint)(want + overlap), remaining);
            int read = mem.Read(start, buf, readLen);

            if (read == readLen)
            {
                foreach (int i in Matches(buf, read, needle))
                    yield return start + (nuint)i;   // a match needs needle.Length bytes, so i < want
            }
            else
            {
                // ReadProcessMemory fails the whole call when any page in the range is bad, so a short
                // chunk is retried a page at a time: one hole must not hide the rest of the megabyte.
                foreach (nuint hit in SweepPages(mem, start, readLen, want, needle, ct))
                    yield return hit;
            }
            start += (nuint)want;
        }
    }

    /// <summary>
    /// Page-by-page sweep of <c>[start, start+length)</c> that skips unreadable pages, still matches across
    /// the seam between two readable pages, and reports only matches that begin before <c>start+limit</c>.
    /// </summary>
    private static IEnumerable<nuint> SweepPages(IMemorySource mem, nuint start, int length, int limit, byte[] needle, CancellationToken ct)
    {
        int overlap = needle.Length - 1;
        byte[] page = new byte[PageSize];
        byte[] window = new byte[PageSize + overlap];
        int carried = 0;               // tail of the previous readable page kept at the front of window
        nuint windowStart = start;
        for (int at = 0; at < length; at += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            int want = Math.Min(PageSize, length - at);
            if (mem.Read(start + (nuint)at, page, want) != want) { carried = 0; continue; }

            if (carried == 0) windowStart = start + (nuint)at;
            Array.Copy(page, 0, window, carried, want);
            int filled = carried + want;
            foreach (int i in Matches(window, filled, needle))
                if (windowStart + (nuint)i < start + (nuint)limit)
                    yield return windowStart + (nuint)i;

            // carried < needle.Length, so a match found wholly inside it cannot be reported twice.
            carried = Math.Min(overlap, filled);
            Array.Copy(window, filled - carried, window, 0, carried);
            windowStart += (nuint)(filled - carried);
        }
    }

    /// <summary>Every offset in <c>buf[0..length)</c> where <paramref name="needle"/> begins.</summary>
    private static List<int> Matches(byte[] buf, int length, byte[] needle)
    {
        var hits = new List<int>();
        var span = buf.AsSpan(0, length);
        int from = 0;
        while (from + needle.Length <= span.Length)
        {
            int i = span[from..].IndexOf(needle);
            if (i < 0) break;
            hits.Add(from + i);
            from += i + 1;
        }
        return hits;
    }

    // --- guest linear 0 ----------------------------------------------------------

    /// <summary>
    /// Finds where guest linear 0 lands in the host by the emulated BIOS data area: 40:0000 holds the
    /// COM1 port (0x03F8) and 40:0013 the conventional-memory size in KB (640). DOSBox pads its guest
    /// allocation by a few bytes, so this is not simply the region base.
    /// </summary>
    internal static bool TryFindGuestZero(IMemorySource mem, MemoryRegion region, out nuint guestZero)
    {
        guestZero = 0;
        const int Window = 0x2000;
        var buf = mem.Read(region.Base, (int)Math.Min((nuint)Window, region.Size));
        for (int i = 0x400; i + 0x14 < buf.Length; i++)
        {
            if (buf[i] != 0xF8 || buf[i + 1] != 0x03) continue;
            if ((buf[i + 0x13] | (buf[i + 0x14] << 8)) != 640) continue;
            guestZero = region.Base + (nuint)(i - 0x400);
            return true;
        }
        return false;
    }

    // --- corroboration -----------------------------------------------------------

    private static PointerWitness FindWitness(IMemorySource mem, MemoryRegion region, nuint zero, long blockLinear, CancellationToken ct)
    {
        foreach (nuint banner in Sweep(mem, region, TurboCBanner, ct))
        {
            if (banner < (nuint)GameFacts.TurboCBannerOffset) continue;
            nuint ds = banner - (nuint)GameFacts.TurboCBannerOffset;
            foreach (var module in GameFacts.Modules)
            {
                var id = Encoding.ASCII.GetBytes(module.IdentityText);
                var got = mem.Read(ds + (nuint)module.IdentityOffset, id.Length);
                if (got.Length != id.Length || !got.AsSpan().SequenceEqual(id)) continue;

                var ptr = mem.Read(ds + (nuint)module.StatePointerOffset, 4);
                if (ptr.Length != 4) continue;
                long linear = (long)(ptr[2] | (ptr[3] << 8)) * 16 + (ptr[0] | (ptr[1] << 8));
                if (linear == blockLinear) return module.Witness;
            }
        }
        return PointerWitness.None;
    }
}
