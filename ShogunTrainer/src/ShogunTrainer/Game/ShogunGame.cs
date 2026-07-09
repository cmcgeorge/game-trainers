namespace ShogunTrainer.Game;

/// <summary>
/// Static knowledge of James Clavell's Shogun (Shogunt.exe / Shoguni.exe, 1987),
/// reconstructed by reverse engineering and confirmed against a live DOSBox-X dump.
///
/// The world is 40 fixed "entities" of 32 bytes each, laid out contiguously in the
/// game's data segment (entity table). Entity 0 is always BLACKTHORNE, the player.
/// The whole table lives inside DOSBox-X's emulated guest RAM; we locate it at
/// runtime by a structural signature rather than a fixed address (guest RAM and the
/// DOS load segment can both move between launches).
/// </summary>
public static class ShogunGame
{
    public const int EntityCount = 40;
    public const int EntityStride = 0x20;          // 32 bytes per record
    public const int TableSize = EntityCount * EntityStride;

    // Byte offsets inside a 32-byte entity record (verified against the decompile
    // and the live table).
    public const int OffLocation = 0x00;           // area/location marker (0xFF for the player)
    public const int OffX = 0x01;
    public const int OffY = 0x02;
    public const int OffDisposition = 0x04;        // attention/disposition toward player: bit 0x80 = flag,
                                                   // low 7 bits = attention value (raised by accepting gifts)
    public const int OffFacing = 0x07;             // low nibble 0xF = idle; player high nibble is 0
    public const int OffTrait1 = 0x08;             // packed personality nibbles (low nibble = fighting power)
    public const int OffTrait2 = 0x09;             // low nibble = gift/befriend FRIENDLINESS (gifts write it)
    public const int OffTrait3 = 0x0A;             // low nibble = persuasion / ambition
    public const int OffInv0 = 0x0B;               // 4 inventory slots (0x0B..0x0E)
    public const int OffInvCount = 4;
    public const int OffCash = 0x0F;               // YEN (single byte 0..255)
    public const int OffHp = 0x10;                 // hit points (0 = dead; spawn ~127..254)
    public const int OffOwner = 0x11;              // master/recruiter index; == self when independent
    public const int OffState = 0x14;              // primary state
    public const int OffMaster2 = 0x17;            // secondary master link (read by some AI passes)

    public const int PlayerIndex = 0;              // BLACKTHORNE

    // The world-object table (placed items) sits at DS:0x14a0, i.e. 0xBA0 after the
    // entity table (DS:0x900). It is 64 entries x 4 bytes [location, caste|id, 0, 0],
    // SORTED by location. This is the key discriminator between the LIVE guest RAM and
    // DOSBox-X savestate/rewind copies of the entity table (which have unrelated bytes
    // — usually host pointers — at this offset).
    public const int WorldObjOffset = 0x14A0 - 0x900;  // = 0xBA0 from the entity table base
    public const int WorldObjCount = 64;
    public const int WorldObjStride = 4;
    public const int WorldObjLoc = 0;              // entry byte 0 = location/screen id
    public const int WorldObjDesc = 1;             // entry byte 1 = caste(&0xE0) | object id(&0x1F)
    public const byte CasteRelic = 0xC0;           // top caste the victory tally counts

    // The per-entity "following tally" the game re-derives; the become-Shogun contest
    // opens once the player's tally exceeds 0x13 (19). Entity-segment table at DS:0x15A0.
    public const int FollowerTallyOffset = 0x15A0 - 0x900;  // = 0xCA0 from the entity table base
    public const int FollowingThreshold = 0x13;    // > this (i.e. >= 20) opens the contest

    // Globals live in a SECOND segment, 0x300 paragraphs (0x3000 bytes) below the entity
    // segment. Verified live: globals_base = entityTable - 0x900 - 0x3000 = entityTable - 0x3900.
    // Add this to the entity table address, then a DS:0xD0xx offset, to address a global.
    public const int GlobalsSegDelta = -0x3900;
    public const int GbArea = 0xD0C2;              // current area/screen (== player entity b0)
    public const int GbPlayerIndex = 0xD0B8;       // player entity index (== 0); validation anchor
    public const int GbContestLoc = 0xD0D0;        // contest palace location (0 until contest opens)
    public const int GbVictoryIndex = 0xD100;      // entity index the victory check tests (== 0)
    public const int GbTimer = 0xD0A4;             // "DON'T TAKE TOO LONG" fail countdown (word)

    // The world is a 17-wide grid of screens; the area id is row*17 + col. Moving one
    // screen east is +1, west −1, south +17, north −17 (verified: walking west dropped
    // the area by 1). Used by the travel helper.
    public const int MapWidth = 17;
    public const int MapHeight = 15;

    /// <summary>
    /// Region id of each of the 255 screens (row‑major, 17 wide), from the game's overview
    /// grid in String0.bin. 0 = impassable / off‑map; nonzero = a real screen (128 of them).
    /// Used by the travel router.
    /// </summary>
    public static readonly byte[] ScreenRegion =
    {
          5,   2,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,
         27,  27,  27,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   5,   3,   5,
          0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  14,  16,  14,   0,   0,  27,  27,
         27,   0,  24,   0,   0,   0,   0,  30,  30,  30,   0,   0,   0,   0,   5,   0,  10,
         10,  10,  10,   0,   0,   0,   0,  14,  15,  15,  15,  14,   0,  27,  27,  27,  33,
         24,   0,   0,   0,   0,  30,  30,  30,   0,   0,   0,   0,   4,  10,  10,   9,   9,
         10,  10,   0,   0,   0,  14,   0,   0,   0,  14,  14,   0,  27,  27,  33,  24,   0,
          0,   0,   0,  30,  30,  34,   0,   0,   0,   0,   8,  10,   9,   9,   9,   9,  10,
          0,   0,  14,  14,   0,   0,   0,   0,  14,  14,   0,  27,  33,  24,  28,  28,  28,
         28,  28,  34,  34,   0,   0,   0,   0,   8,   8,   0,   0,   9,   0,  10,   0,  12,
         13,   0,   0,   0,  19,   0,   0,  21,  26,  26,  25,  25,  26,  26,  29,  29,  34,
         34,  34,  32,   0,   0,   0,   8,   8,   0,   0,   9,   0,  12,  12,  12,  17,  17,
         17,  17,  18,  17,  17,  17,  22,  26,  23,   0,   0,   0,  29,  29,  35,  35,  32,
         32,  32,   0,   0,   6,   7,   0,   0,  11,   0,   0,   0,   0,   0,   0,   0,  20,
          0,   0,   0,   0,   0,   0,  23,   0,   0,   0,  29,  29,  35,   0,   0,  31,  31,
    };

    public static bool Passable(int screen) =>
        screen >= 0 && screen < ScreenRegion.Length && ScreenRegion[screen] != 0;

    /// <summary>Shortest walkable path (BFS over adjacent screens) from one screen to another,
    /// or null if unreachable. Returns the sequence of screen ids including both ends.</summary>
    public static int[]? FindPath(int from, int to)
    {
        if (!Passable(from) || !Passable(to)) return null;
        if (from == to) return new[] { from };

        int cells = ScreenRegion.Length, W = MapWidth;
        var prev = new int[cells];
        for (int i = 0; i < cells; i++) prev[i] = -2;   // -2 = unvisited
        prev[from] = -1;

        var queue = new Queue<int>();
        queue.Enqueue(from);
        Span<int> nbrs = stackalloc int[4];
        while (queue.Count > 0)
        {
            int s = queue.Dequeue();
            int col = s % W, row = s / W;
            int n = 0;
            if (col + 1 < W) nbrs[n++] = s + 1;          // E
            if (col - 1 >= 0) nbrs[n++] = s - 1;         // W
            if (row + 1 < MapHeight) nbrs[n++] = s + W;  // S
            if (row - 1 >= 0) nbrs[n++] = s - W;         // N
            for (int k = 0; k < n; k++)
            {
                int t = nbrs[k];
                if (ScreenRegion[t] != 0 && prev[t] == -2)
                {
                    prev[t] = s;
                    if (t == to) { queue.Clear(); break; }
                    queue.Enqueue(t);
                }
            }
        }
        if (prev[to] == -2) return null;

        var rev = new List<int>();
        for (int c = to; c != -1; c = prev[c]) rev.Add(c);
        rev.Reverse();
        return rev.ToArray();
    }

    /// <summary>Turn-by-turn compass directions for a screen path, e.g. "West 5 → South 1".</summary>
    public static string Directions(int[] path)
    {
        if (path.Length < 2) return "you're already there";
        var legs = new List<string>();
        int runDir = path[1] - path[0], runLen = 0;
        static string Name(int d) => d == 1 ? "East" : d == -1 ? "West" : d == MapWidth ? "South" : "North";
        for (int i = 1; i < path.Length; i++)
        {
            int d = path[i] - path[i - 1];
            if (d == runDir) runLen++;
            else { legs.Add($"{Name(runDir)} {runLen}"); runDir = d; runLen = 1; }
        }
        legs.Add($"{Name(runDir)} {runLen}");
        return string.Join(" → ", legs);
    }

    // Inventory byte = caste/category bits | object id. The three victory relics:
    public const byte RelicBuddha = 0xCD;          // caste 0xC0 | id 0x0D
    public const byte RelicScroll = 0xCE;          // caste 0xC0 | id 0x0E
    public const byte RelicMirror = 0xCF;          // caste 0xC0 | id 0x0F

    /// <summary>The fixed 40-entity roster (names table [17..56]).</summary>
    public static readonly string[] Names =
    {
        "Blackthorne", "Mariko", "Toranaga", "Ishido", "Yotaka", "Kiku", "Rako", "Autumn Moon",
        "Jade", "Moonlight", "Willow", "Persimmon", "Pine", "Koku", "Stone", "Night Rain",
        "Camellia", "Asa", "Yoko", "Novi", "Kogai", "Yamaha", "Suzuki", "Danshichi",
        "Blood", "Muraji", "Ikematsu", "Sky Dragon", "Naga", "Lion", "Hawk", "Tiger",
        "Peregrine", "Omi", "Yoshinaka", "Katana", "Wakizashi", "Tachi", "Kozuka", "Hachiman",
    };

    public static string NameOf(int index) =>
        index >= 0 && index < Names.Length ? Names[index] : $"#{index}";

    /// <summary>
    /// Score a candidate 40*32 byte block as the entity table. The signature: every
    /// entity's owner byte (+0x11) is a valid index 0..39, and independent entities'
    /// owner == their own index (a rising diagonal no graphics data reproduces). We
    /// also count idle-facing records as corroboration.
    /// </summary>
    public static (int diagonal, int ownersValid, int idle) Score(ReadOnlySpan<byte> block, int baseOff)
    {
        int diag = 0, ownValid = 0, idle = 0;
        for (int n = 0; n < EntityCount; n++)
        {
            int rec = baseOff + n * EntityStride;
            byte owner = block[rec + OffOwner];
            if (owner == n) diag++;
            if (owner < EntityCount) ownValid++;
            if ((block[rec + OffFacing] & 0x0F) == 0x0F) idle++;
        }
        return (diag, ownValid, idle);
    }

    /// <summary>
    /// Cheap pre-filter: does this 40*32 block have the entity-table shape? (Owners are
    /// valid indices, facings idle, player self-owned and alive.) This alone is NOT enough
    /// — DOSBox-X savestate buffers contain many matching copies; pair it with
    /// <see cref="HasValidWorldObjectTable"/> to isolate the live table.
    /// </summary>
    public static bool LooksLikeTable(ReadOnlySpan<byte> block, int baseOff)
    {
        var (_, ownValid, idle) = Score(block, baseOff);
        // NOTE: the owner *diagonal* (owner[n]==n) is NOT part of the gate — recruiting
        // sets many owners to 0 and destroys it. Owners staying valid indices and idle
        // facings survive any amount of recruitment.
        return block[baseOff + OffOwner] == 0
            && block[baseOff + OffHp] > 0
            && ownValid >= 38
            && idle >= 20;
    }

    /// <summary>
    /// Confirm the live world-object table follows the entity table at +0xBA0: 64 entries
    /// sorted by location with clean (zero) trailing bytes. Savestate copies fail this —
    /// they have host pointers / unsorted noise there. Needs the block to extend past the
    /// world-object table (i.e. the candidate is inside the large guest-RAM region).
    /// </summary>
    public static bool HasValidWorldObjectTable(ReadOnlySpan<byte> block, int baseOff)
    {
        int wo = baseOff + WorldObjOffset;
        if (wo < 0 || wo + WorldObjCount * WorldObjStride > block.Length)
            return false;

        int nonzero = 0, sortViolations = 0, cleanTail = 0, lastLoc = -1;
        for (int i = 0; i < WorldObjCount; i++)
        {
            int e = wo + i * WorldObjStride;
            byte loc = block[e + WorldObjLoc], desc = block[e + 1], b2 = block[e + 2], b3 = block[e + 3];
            if (loc == 0 && desc == 0 && b2 == 0 && b3 == 0)
                continue;                        // empty slot
            nonzero++;
            if (loc < lastLoc) sortViolations++;  // table is sorted ascending by location
            lastLoc = loc;
            if (b2 == 0 && b3 == 0) cleanTail++;
        }
        return nonzero >= 15
            && sortViolations <= 2
            && cleanTail >= nonzero - 6;          // a few relic/special entries carry extra bytes
    }

    /// <summary>Composite score used to pick the best candidate when several pass.</summary>
    public static int Confidence(ReadOnlySpan<byte> block, int baseOff)
    {
        var (diag, ownValid, idle) = Score(block, baseOff);
        return idle * 2 + ownValid + diag;
    }
}
