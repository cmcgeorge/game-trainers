using System.Text;

namespace LordsTrainer;

/// <summary>
/// Game-specific knowledge for Lords of the Realm, derived by reverse-engineering
/// (see ../.docs/ReverseEngineering.md). Turns the raw <see cref="IGuestMemory"/>
/// access into named, game-aware operations — this is what makes the tool a trainer
/// rather than a generic memory editor.
///
/// Treasury layout, recovered against the live game and confirmed by a differential
/// scan across a treasury change (40 → 89) plus a verified poke that changed the
/// on-screen crowns:
///   * The game's static data segment (DGROUP) is anchored at runtime by searching
///     for the DIVERGANCE label pool "Counties\0Units\0Players\0Figures\0Armies"
///     (DGROUP_linear = poolAddress - 0x3CC2).
///   * The AUTHORITATIVE per-lord treasury is a 6-entry Int32 array (stride 4) at
///     DGROUP - 0xC3BD. This is what the game spends from.
///   * A single Int32 at DGROUP + 0x8C5F caches the active (human) player's treasury
///     and is what the treasury/market screen displays. Writing the array slot alone
///     is enough to spend; we also write this cache so the displayed number matches.
///   * The human's slot is identified as the array index whose value equals the cache.
///
/// NB: an earlier candidate (a 6-record array at DGROUP + 0x2013, stride 0xE0) turned
/// out to be a display/report snapshot ("Figures"), not the spendable treasury — do
/// not use it. All offsets are in the statically-linked image, so they are fixed for
/// a given LORDS.EXE while DGROUP is re-anchored by signature each attach.
/// </summary>
public sealed class LordsGame(IGuestMemory memory)
{
    private readonly IGuestMemory _memory = memory;

    private static readonly byte[] LabelSignature =
        Encoding.ASCII.GetBytes("Counties\0Units\0Players\0Figures\0Armies");
    private const uint LabelPoolOffset = 0x3CC2;

    /// <summary>Size of the DOS conventional-memory window (640 KB) where game state lives.</summary>
    public const uint ConventionalMemoryBytes = 0xA0000;

    // Treasury structures, relative to DGROUP.
    private const uint TreasuryArrayBackOffset = 0xC3BD;   // array base = DGROUP - this
    private const uint TreasuryCacheOffset     = 0x8C5F;   // active-player cache
    private const uint LordStride = 4;

    // Province goods live in a per-county record (stride 0x168). The trainer anchors on
    // one county's record (grain field at DGROUP+0xB1F0) and reads the four goods at
    // offsets confirmed by differential scans and pokes, cross-checked across several
    // counties: grain +0x00, cattle +0x12, sheep +0x1e, wool +0x46 (all Int16).
    private const uint GrainFieldOffset = 0xB1F0;
    private const int GrainMax = 30_000;
    private const int GoodMax = 9_999;

    // Amount each "Max" cheat writes. Grain is deliberately below GrainMax (9,999 is
    // already a lifetime supply); livestock/wool are kept to sane herd sizes.
    private const int GrainCheatTo = 9_999;
    private const int LivestockCheatTo = 999;

    public readonly record struct ProvinceGood(string Name, uint Address, int Value, int Max, int CheatTo);

    private static readonly (string Name, uint Rel, int Max, int CheatTo)[] GoodDefs =
    {
        ("Grain",  0x00, GrainMax, GrainCheatTo),
        ("Cattle", 0x12, GoodMax,  LivestockCheatTo),
        ("Sheep",  0x1e, GoodMax,  LivestockCheatTo),
        ("Wool",   0x46, GoodMax,  LivestockCheatTo),
    };

    // County records are indexed 0..N; the anchor county (grain field at
    // GrainFieldOffset) has index AnchorCountyIndex, and consecutive counties are
    // CountyStride apart. The game keeps the currently-viewed county's index at
    // CurrentCountyOffset (found by diffing two county views). Together these let the
    // trainer cheat whichever province you have open, not just the anchor.
    private const uint CurrentCountyOffset = 0xC238;
    private const int  AnchorCountyIndex = 24;      // the county whose grain field == GrainFieldOffset
    private const uint CountyStride = 0x168;
    private const int  MaxCountyIndex = 63;

    /// <summary>Maximum lord slots the game reserves.</summary>
    public const int MaxLords = 6;

    /// <summary>Upper bound of a believable treasury, used only for layout sanity checks.</summary>
    public const int PlausibleTreasuryMax = 5_000_000;

    public bool Located { get; private set; }
    public uint DGroupLinear { get; private set; }
    public uint DGroupSegment => DGroupLinear / 16;

    private uint ArrayBase => DGroupLinear - TreasuryArrayBackOffset;
    private uint CacheAddr => DGroupLinear + TreasuryCacheOffset;

    /// <summary>
    /// Locates DGROUP by signature and verifies the treasury layout looks sane
    /// (all six slots are plausible and the cache matches one of them). Returns false
    /// if the game data isn't present or the layout doesn't validate.
    /// </summary>
    public bool Locate()
    {
        Located = false;
        var mem = _memory.ReadGuest(0, (int)ConventionalMemoryBytes);
        int idx = IndexOf(mem, LabelSignature);
        if (idx < 0 || idx < (int)LabelPoolOffset)
            return false;
        DGroupLinear = (uint)idx - LabelPoolOffset;
        Located = true;                       // DGROUP found

        // Validate the treasury layout; if it fails we stay "located" (scanner works)
        // but callers can check IsTreasuryValid before offering the gold cheats.
        IsTreasuryValid = ValidateTreasury();
        return true;
    }

    /// <summary>True when the treasury array/cache offsets resolve to a believable layout.</summary>
    public bool IsTreasuryValid { get; private set; }

    private bool ValidateTreasury()
    {
        // Reject a DGROUP so low that the treasury array base would wrap past 0
        // (unsigned underflow), which would make every treasury read/write bogus.
        if (DGroupLinear < TreasuryArrayBackOffset) return false;

        // The layout is valid when every lord slot and the cache read as believable
        // treasuries. Reads go through the failure-aware Try* path so a failed/short
        // read fails validation rather than masquerading as a genuine 0. We deliberately
        // do NOT require the cache to equal a slot exactly: the array grows with each
        // season's income while the display cache only refreshes when you view the
        // treasury screen, so it legitimately lags behind.
        if (!TryReadCache(out int cache) || cache < 0 || cache > PlausibleTreasuryMax) return false;
        for (int i = 0; i < MaxLords; i++)
        {
            if (!TryReadTreasury(i, out int v) || v < 0 || v > PlausibleTreasuryMax) return false;
        }
        return true;
    }

    // ---- Treasury access ----------------------------------------------------

    public uint TreasuryAddress(int lord) => ArrayBase + (uint)lord * LordStride;
    public uint CacheAddress => CacheAddr;

    /// <summary>Best-effort treasury read for display (0 on a failed read).</summary>
    public int ReadTreasury(int lord) => _memory.ReadInt32(TreasuryAddress(lord));

    private bool TryReadTreasury(int lord, out int crowns) => _memory.TryReadInt32(TreasuryAddress(lord), out crowns);
    private bool TryReadCache(out int crowns) => _memory.TryReadInt32(CacheAddr, out crowns);

    // ---- Province goods -----------------------------------------------------

    private uint GrainAddress => DGroupLinear + GrainFieldOffset;

    /// <summary>Grain-field address of the county with the given index.</summary>
    private uint CountyGrainAddress(int index) =>
        (uint)((long)DGroupLinear + GrainFieldOffset + (long)(index - AnchorCountyIndex) * CountyStride);

    /// <summary>Index of the county whose management screen you currently have open.</summary>
    public int CurrentCountyIndex =>
        Located && _memory.TryReadInt16(DGroupLinear + CurrentCountyOffset, out short v) ? v : -1;

    /// <summary>
    /// A county's four goods (grain, cattle, sheep, wool) together with its slot index and
    /// whether it's the county whose management screen you currently have open. Ownership is
    /// not a clean per-record field in this build, so counties are identified by slot index,
    /// not name (see ../.docs/ReverseEngineering.md §4a).
    /// </summary>
    public readonly record struct County(int Index, bool IsViewed, IReadOnlyList<ProvinceGood> Goods);

    /// <summary>
    /// Every county slot that holds a live goods record, in index order. The game reserves
    /// <see cref="MaxCountyIndex"/>+1 slots but only some are real counties, so a slot is
    /// included only when all four goods read successfully and sit within their plausible
    /// ranges (skipping garbage/free slots) and at least one good is non-zero (skipping
    /// zero-filled reserve slots). This lets the UI list and max the goods of every province,
    /// not just the anchor or the one currently open.
    /// </summary>
    public IReadOnlyList<County> AllCounties()
    {
        var list = new List<County>();
        if (!Located) return list;
        int viewed = CurrentCountyIndex;
        for (int i = 0; i <= MaxCountyIndex; i++)
        {
            var goods = GoodsAt(CountyGrainAddress(i), alwaysReturn: false);
            if (goods is null) continue;                   // slot didn't validate as a record
            if (goods.All(g => g.Value == 0)) continue;    // empty reserve slot — nothing to list
            list.Add(new County(i, i == viewed, goods));
        }
        return list;
    }

    private List<ProvinceGood>? GoodsAt(uint grainAddr, bool alwaysReturn)
    {
        var list = new List<ProvinceGood>();
        foreach (var (name, rel, max, cheatTo) in GoodDefs)
        {
            uint addr = grainAddr + rel;
            if (!_memory.TryReadInt16(addr, out short sv))
            {
                if (!alwaysReturn) return null;     // couldn't read the record
                sv = 0;
            }
            int v = sv;
            if (!alwaysReturn && (v < 0 || v > max)) return null;   // record didn't validate
            list.Add(new ProvinceGood(name, addr, v, max, cheatTo));
        }
        return list;
    }

    // ---- Global (kingdom-wide) resources: materials + armoury ----------------
    //
    // Iron, Stone, Wood AND the seven armoury weapon stocks are NOT per-county — they
    // all live in the player's economy struct, which the game malloc's in the near heap
    // (the same allocation that carries the treasury). That allocation is re-created
    // every match at a fresh address, so — exactly like the treasury — these cannot be a
    // fixed DGROUP offset and are instead located by their live values.
    //
    // The struct layout was recovered against the running game (relative to the Iron
    // pair, which is the block base):
    //   +0x00 Iron, +0x04 Stone, +0x08 Wood  — each a DOUBLED Int16 pair (authoritative
    //         + a shadow copy), so the six-Int16 signature of the player's current
    //         amounts pins the block uniquely (a single match across the 640 KB window).
    //   +0x0c Weapons total (sum of the batch counts below).
    //   +0x12 Swords, +0x14 Axes, +0x16 Crossbows, +0x18 Spears, +0x1a Maces,
    //   +0x1c Long Bows, +0x1e Armor — each a single Int16 stored in BATCHES OF 50
    //         (the in-game count is the stored value × 50; e.g. 3 → 150). This scaling
    //         is why the raw weapon counts never appear in memory as plain values.

    /// <summary>A located global resource: its slot, an optional mirror copy to keep in
    /// sync, the live raw value, the stored→displayed scale, and the amount to cheat to
    /// (all in raw memory units).</summary>
    public readonly record struct GlobalResource(
        string Name, uint Address, uint? MirrorAddress, int Value, int Scale, int CheatTo);

    /// <summary>The armoury: the seven weapon stocks plus the address/target of the total.</summary>
    public readonly record struct Armoury(uint TotalAddress, int TotalCheatTo, IReadOnlyList<GlobalResource> Weapons);

    /// <summary>Amount each material "Max" writes — a lifetime supply that stays inside an Int16.</summary>
    public const int MaterialCheatTo = 9_999;

    /// <summary>Weapons are stored in batches of 50 (in-game count = stored × 50).</summary>
    public const int WeaponScale = 50;

    /// <summary>Batches each weapon "Max" writes: 200 × 50 = 10,000 of every weapon shown
    /// in-game — a generous, gameplay-safe maximum that leaves plenty of Int16 headroom.</summary>
    public const int WeaponCheatTo = 200;

    private static readonly string[] MaterialNames = { "Iron", "Stone", "Wood" };
    private static readonly string[] WeaponNames =
        { "Swords", "Axes", "Crossbows", "Spears", "Maces", "Long Bows", "Armor" };

    private const uint WeaponsTotalRel = 0x0C;   // sum-of-batches field, relative to block base
    private const uint WeaponArrayRel  = 0x12;   // first weapon (Swords)

    /// <summary>
    /// Locates the player economy block by searching conventional memory for the doubled
    /// Int16 signature of the current Iron/Stone/Wood. Returns the block base (the Iron
    /// pair), or null if the values aren't found or more than one location matches
    /// (ambiguous — the caller should ask the user to change a material in-game and try
    /// again, exactly as the treasury finder does).
    /// </summary>
    public uint? FindEconomyBlock(int iron, int stone, int wood)
    {
        if (iron < 0 || stone < 0 || wood < 0) return null;

        // Signature: iron,iron, stone,stone, wood,wood (six little-endian Int16).
        var sig = new byte[12];
        WritePair(sig, 0, iron);
        WritePair(sig, 4, stone);
        WritePair(sig, 8, wood);

        var mem = _memory.ReadGuest(0, (int)ConventionalMemoryBytes);
        int match = -1;
        // The block is Int16-aligned, so stepping by 2 both halves the work and avoids
        // odd-offset false hits.
        for (int i = 0; i + sig.Length <= mem.Length; i += 2)
        {
            if (!MatchAt(mem, i, sig)) continue;
            if (match >= 0) return null;    // ambiguous — more than one match
            match = i;
        }
        return match < 0 ? null : (uint)match;
    }

    /// <summary>The three materials at a located block, each with its shadow copy.</summary>
    public IReadOnlyList<GlobalResource> MaterialsAt(uint b) => new[]
    {
        new GlobalResource(MaterialNames[0], b + 0, b + 2,  ReadI16(b + 0), 1, MaterialCheatTo),
        new GlobalResource(MaterialNames[1], b + 4, b + 6,  ReadI16(b + 4), 1, MaterialCheatTo),
        new GlobalResource(MaterialNames[2], b + 8, b + 10, ReadI16(b + 8), 1, MaterialCheatTo),
    };

    /// <summary>The seven armoury weapons (batch-scaled) and the total, at a located block.</summary>
    public Armoury WeaponsAt(uint b)
    {
        var list = new List<GlobalResource>(WeaponNames.Length);
        for (int i = 0; i < WeaponNames.Length; i++)
        {
            uint a = b + WeaponArrayRel + (uint)(i * 2);
            list.Add(new GlobalResource(WeaponNames[i], a, null, ReadI16(a), WeaponScale, WeaponCheatTo));
        }
        return new Armoury(b + WeaponsTotalRel, WeaponNames.Length * WeaponCheatTo, list);
    }

    private int ReadI16(uint addr) => _memory.TryReadInt16(addr, out short v) ? v : 0;

    private static void WritePair(byte[] buf, int offset, int value)
    {
        short v = (short)value;
        buf[offset] = (byte)v;
        buf[offset + 1] = (byte)(v >> 8);
        buf[offset + 2] = buf[offset];
        buf[offset + 3] = buf[offset + 1];
    }

    private static bool MatchAt(byte[] haystack, int i, byte[] needle)
    {
        for (int j = 0; j < needle.Length; j++)
            if (haystack[i + j] != needle[j]) return false;
        return true;
    }

    /// <summary>True if the anchor grain slot holds a believable value (guards against
    /// a version whose layout differs from the one that was reverse-engineered).</summary>
    public bool IsGoodsValid
    {
        get
        {
            if (!Located) return false;
            if (!_memory.TryReadInt16(GrainAddress, out short v)) return false;
            return v >= 0 && v <= GrainMax;
        }
    }

    /// <summary>
    /// Index of the human's lord slot: the active-lord entry equal to (or nearest) the
    /// active-player cache. An exact match wins outright; otherwise we take the closest,
    /// since the cache only lags the human's own treasury by recent income. Only the
    /// <see cref="DetectLordCount"/> active slots are considered, so an inactive/garbage
    /// slot can never be mistaken for the human (which would let "Bankrupt rivals" zero
    /// the human). The user can always pick a different row.
    /// </summary>
    public int HumanIndex()
    {
        if (!TryReadCache(out int cache)) return 0;
        int count = DetectLordCount();
        int best = 0;
        long bestDiff = long.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!TryReadTreasury(i, out int v)) continue;   // skip an unreadable slot
            if (v == cache) return i;                        // exact match is unambiguous
            long diff = Math.Abs((long)v - cache);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    public bool IsPlausibleLord(int lord)
    {
        if (!Located) return false;
        if (!TryReadTreasury(lord, out int v)) return false;
        return v >= 0 && v <= PlausibleTreasuryMax;
    }

    /// <summary>Count of active lord slots (contiguous plausible treasuries from slot 0).</summary>
    public int DetectLordCount()
    {
        if (!Located) return 0;
        int n = 0;
        for (int i = 0; i < MaxLords; i++)
        {
            if (!IsPlausibleLord(i)) break;
            n++;
        }
        return Math.Max(n, 1);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        int end = haystack.Length - needle.Length;
        for (int i = 0; i <= end; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}
