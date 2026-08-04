namespace DarkDesigns1Trainer.Game;

/// <summary>How a wall byte is drawn on the game's own automap.</summary>
public enum WallKind
{
    /// <summary>Nothing between the two squares.</summary>
    Open = 0,
    /// <summary>A solid wall — this is also how an undiscovered secret door draws.</summary>
    Wall = 1,
    /// <summary>A door: open, locked, or unlocked with a key.</summary>
    Door = 2,
}

/// <summary>What a square does when the party steps on it.</summary>
public enum SquareKind
{
    /// <summary>Nothing, or a room-description message.</summary>
    Plain,
    StairsUp,
    StairsDown,
    TreasureChest,
    Item,
    /// <summary>Walking here drops the party to the level below, with damage.</summary>
    Edge,
    /// <summary>A chest or item square the party has already emptied; it never fires again.</summary>
    Emptied,
}

/// <summary>
/// Byte-level layout of a Dark Designs I dungeon level — the 12,648-byte <c>DDMAP1–5.DAT</c>
/// files, which the game reads verbatim into one buffer at <c>DGROUP:0x50F4</c> and writes back
/// unchanged, so this describes the live map and the file at once.
///
/// Everything here was recovered by disassembling the unpacked <c>DARKDES.EXE</c>. The loader
/// (<c>0841:0084</c>) builds the name <c>DDMAP&lt;n&gt;.DAT</c> for <c>n</c> in 1..5 and reads
/// <c>0x3168</c> (12,648) bytes into that one buffer; every routine that touches a square indexes
/// it as <c>x * 128 + y * 4 + facing</c> for walls and <c>x * 32 + y</c> for square contents. See
/// <c>docs/ReverseEngineering.md</c> §6.
/// </summary>
public static class MapFormat
{
    /// <summary>Levels are a fixed 32 × 32 grid; the game bounds-checks both axes against 0..0x1F.</summary>
    public const int GridSize = 32;

    /// <summary>Compass directions, and the number of wall bytes per square.</summary>
    public const int Directions = 4;

    /// <summary>Total size of one map file / of the live map buffer.</summary>
    public const int FileSize = 12648;      // 0x3168 — the size the loader passes to read()

    // --- sections ------------------------------------------------------------
    /// <summary>Wall bytes: one per square per direction, X-major.</summary>
    public const int OffWalls = 0x0000;
    public const int WallsLength = GridSize * GridSize * Directions;   // 4096

    /// <summary>Square contents: bit 7 = mapped/visited, bits 0–5 = event code.</summary>
    public const int OffContents = 0x1000;
    public const int ContentsLength = GridSize * GridSize;             // 1024

    /// <summary>
    /// Per-event-code index of the code's first description line. The game reads lines
    /// <c>first .. first + count - 1</c> (its loop runs <c>i = 1..count</c> over
    /// <c>(first + i - 1) * 0x28</c>), so this indexes the text block directly and line 0 is unused.
    /// </summary>
    public const int OffTextFirst = 0x1400;

    /// <summary>Per-event-code count of description lines.</summary>
    public const int OffTextCount = 0x1440;

    /// <summary>Number of distinct event codes a level can carry.</summary>
    public const int EventCodeCount = 64;

    /// <summary>
    /// Description text: 127 fixed 40-byte lines, each a length-prefixed string (a 39 byte followed
    /// by 39 characters, the last of which is the game's newline marker <c>]</c>).
    /// </summary>
    public const int OffTextLines = 0x1D90;
    public const int TextLineSize = 40;
    public const int TextLineCount = 127;                              // (12648 - 0x1D90) / 40

    /// <summary>
    /// Bytes between the two text-index tables and the text block. Four further 64-entry tables sit
    /// here (the game reads per-code X and Y coordinates out of the first two); the whole span is
    /// round-tripped and never interpreted.
    /// </summary>
    public const int OffUndecoded = 0x1480;
    public const int UndecodedLength = OffTextLines - OffUndecoded;

    // --- indexing ------------------------------------------------------------
    /// <summary>Distance between two squares one step apart on the X axis, in wall bytes.</summary>
    public const int WallStrideX = GridSize * Directions;   // 128 — the game's `shl di, 7`
    /// <summary>Distance between two squares one step apart on the Y axis, in wall bytes.</summary>
    public const int WallStrideY = Directions;              // 4 — the game's two `shl ax, 1`

    public static int WallIndex(int x, int y, int direction) =>
        OffWalls + x * WallStrideX + y * WallStrideY + direction;

    public static int ContentIndex(int x, int y) =>
        OffContents + x * GridSize + y;

    public static bool InBounds(int x, int y) =>
        x >= 0 && x < GridSize && y >= 0 && y < GridSize;

    // --- directions ----------------------------------------------------------
    public const int North = 0;
    public const int East = 1;
    public const int South = 2;
    public const int West = 3;

    public static readonly string[] FacingNames = { "North", "East", "South", "West" };

    /// <summary>
    /// Per-direction X step. Taken from the game's own delta table at <c>DGROUP:0x1DC</c>
    /// (<c>{-1, 0, +1, 0}</c>), which it adds to the X global — reordered here so index 0 is North.
    /// </summary>
    public static readonly int[] DeltaX = { 0, 1, 0, -1 };

    /// <summary>Per-direction Y step, from the game's table at <c>DGROUP:0x1D4</c>. Y grows southward.</summary>
    public static readonly int[] DeltaY = { -1, 0, 1, 0 };

    public static string FacingName(int f) =>
        f >= 0 && f < FacingNames.Length ? FacingNames[f] : $"?({f})";

    /// <summary>The direction that faces back the way <paramref name="direction"/> came from.</summary>
    public static int Opposite(int direction) => (direction + 2) & 3;

    // --- wall bytes ----------------------------------------------------------
    public const int WallOpen = 0;
    public const int WallSolid = 1;
    public const int WallDoor = 2;
    public const int WallLocked1 = 3;      // needs Key 1
    public const int WallLocked2 = 4;      // needs Key 2
    public const int WallLocked3 = 5;      // needs Key 3
    public const int WallSecretDoor = 6;   // draws as solid until (S)earch finds it
    public const int WallUnlocked1 = 11;   // what Key 1 turns a WallLocked1 into
    public const int WallOpenedSecret = 14;// what (S)earch turns a WallSecretDoor into

    /// <summary>Highest wall byte the movement check will walk through (its <c>cmp al, 0x10</c>).</summary>
    public const int MaxWallValue = 16;

    /// <summary>
    /// The game's own automap classification, copied byte-for-byte from the 16-entry table the main
    /// loop builds on its stack at <c>0x3A7F</c> and the cell renderer indexes by wall value. Note
    /// 6 (a secret door) deliberately draws as a plain wall, and 14 (one that has been found) as a
    /// door.
    /// </summary>
    private static readonly byte[] WallClasses = { 0, 1, 2, 2, 2, 2, 1, 2, 0, 1, 2, 2, 2, 2, 2, 2 };

    /// <summary>
    /// How the wall byte <paramref name="value"/> is drawn.
    ///
    /// Note the game's table has 16 entries but its movement check passes 16 as well
    /// (<c>cmp al, 0x10</c> / <c>ja</c>), so value 16 is walkable yet has no class of its own — the
    /// game would index one byte past its own table. None of the shipped maps contains it and
    /// nothing writes it, so it is drawn as a wall here: over-drawing a wall is the harmless
    /// direction to be wrong in.
    /// </summary>
    public static WallKind Classify(int value)
    {
        if (value < 0 || value >= WallClasses.Length) return WallKind.Wall;
        return (WallKind)WallClasses[value];
    }

    /// <summary>
    /// True when the party can walk through this wall byte. Straight from the movement routine at
    /// <c>0x392A</c>: it passes 0 and 2 outright and anything in 8..16, prints "WALL!" for 1 and 6,
    /// "LOCKED!" for 3–5, and silently blocks the rest.
    /// </summary>
    public static bool IsPassable(int value) =>
        value == WallOpen || value == WallDoor || (value >= 8 && value <= MaxWallValue);

    /// <summary>True for the three locked doors, which need the matching numbered key.</summary>
    public static bool IsLocked(int value) =>
        value >= WallLocked1 && value <= WallLocked3;

    /// <summary>Which numbered key (1–3) opens this wall byte, or 0 if it is not a locked door.</summary>
    public static int KeyFor(int value) =>
        IsLocked(value) ? value - WallLocked1 + 1 : 0;

    // --- square contents -----------------------------------------------------
    /// <summary>Set on a square's content byte once the party has stood on it.</summary>
    public const int VisitedFlag = 0x80;

    /// <summary>
    /// Mask that isolates the event code from a content byte. Seven bits, not six: the game reads
    /// a square as <c>byte - 0x80</c> and treats anything above <see cref="MaxEventCode"/> as
    /// "nothing here any more", which is how it retires a looted square (below).
    /// </summary>
    public const int EventCodeMask = 0x7F;

    /// <summary>Highest code that means something; above this the game zeroes it (its `cmp 0x3F`).</summary>
    public const int MaxEventCode = 0x3F;

    public const int CodeStairsUp = 0x35;
    public const int CodeStairsDown = 0x36;
    public const int CodeTreasureChest = 0x37;
    public const int CodeItem = 0x38;
    public const int CodeEdge = 0x39;

    /// <summary>
    /// What the game stamps over a treasure-chest square once it has been opened — it writes the
    /// whole byte <c>0xF7</c>, i.e. visited plus this code, which is past
    /// <see cref="MaxEventCode"/> and so reads back as nothing.
    /// </summary>
    public const int CodeChestTaken = 0x77;

    /// <summary>The same for an item square, written as the whole byte <c>0xF8</c>.</summary>
    public const int CodeItemTaken = 0x78;

    /// <summary>Codes below this print the square's description; from here up they are actions.</summary>
    public const int FirstActionCode = CodeStairsUp;

    /// <summary>
    /// The event code of a content byte, decoded exactly as the game does it: strip the mapped bit,
    /// and treat anything above <see cref="MaxEventCode"/> as nothing. That last step is what
    /// retires a chest or item square the party has already emptied — without it, a looted chest
    /// would keep reading back as a chest.
    /// </summary>
    public static int DecodeEventCode(int contentByte)
    {
        int code = contentByte & EventCodeMask;
        return code > MaxEventCode ? 0 : code;
    }

    /// <summary>True when this content byte is a chest or item square the party has emptied.</summary>
    public static bool IsEmptied(int contentByte)
    {
        int code = contentByte & EventCodeMask;
        return code == CodeChestTaken || code == CodeItemTaken;
    }

    public static SquareKind KindOf(int eventCode) => eventCode switch
    {
        CodeStairsUp => SquareKind.StairsUp,
        CodeStairsDown => SquareKind.StairsDown,
        CodeTreasureChest => SquareKind.TreasureChest,
        CodeItem => SquareKind.Item,
        CodeEdge => SquareKind.Edge,
        _ => SquareKind.Plain,
    };

    public static string KindName(SquareKind kind) => kind switch
    {
        SquareKind.StairsUp => "Stairs up",
        SquareKind.StairsDown => "Stairs down",
        SquareKind.TreasureChest => "Treasure chest",
        SquareKind.Item => "Item",
        SquareKind.Edge => "Edge — you fall to the level below",
        SquareKind.Emptied => "Already emptied",
        _ => "",
    };

    // --- live party position (DGROUP globals) --------------------------------
    /// <summary>
    /// Size of the party-position block: four <c>uint16</c> the game reads straight out of the
    /// <c>DDCHARS.DAT</c> header and writes straight back to it.
    /// </summary>
    public const int PositionBlockSize = 8;

    public const int PosOffLevel = 0;    // 0 = in town, 1..5 = a dungeon level
    public const int PosOffX = 2;
    public const int PosOffY = 4;
    public const int PosOffFacing = 6;

    /// <summary>The lowest and highest dungeon level; the loader rejects anything outside it.</summary>
    public const int MinLevel = 1;
    public const int MaxLevel = 5;

    /// <summary>Level value the game uses while the party is in town rather than in the castle.</summary>
    public const int TownLevel = 0;

    // --- DGROUP offsets, for the locator's fixed-delta arithmetic ------------
    // These are offsets inside the game's single data segment, not addresses: the segment moves
    // every session, so the locator derives real addresses from something it found by content.
    /// <summary>In-memory roster array base — slot 0 is scratch; the file's 15 records start at slot 1.</summary>
    public const int DgroupRosterArray = 0x0424;
    /// <summary>Where the loader reads the file's first character record to.</summary>
    public const int DgroupRosterFirstFileSlot = 0x046C;
    /// <summary>Level / X / Y / facing, read from and written to the save header as one run.</summary>
    public const int DgroupPosition = 0x1320;
    /// <summary>The 12,648-byte map buffer the current level is read into.</summary>
    public const int DgroupMapBuffer = 0x50F4;

    /// <summary>Position block relative to a roster located on the scratch slot.</summary>
    public const int PositionFromRosterArray = DgroupPosition - DgroupRosterArray;          // 0xEFC
    /// <summary>Position block relative to a roster located on the file's first record.</summary>
    public const int PositionFromRosterFirstFileSlot = DgroupPosition - DgroupRosterFirstFileSlot; // 0xEB4
    /// <summary>Map buffer relative to the position block.</summary>
    public const int MapFromPosition = DgroupMapBuffer - DgroupPosition;                    // 0x3DD4

    // --- validation ----------------------------------------------------------
    /// <summary>
    /// Nonzero wall bytes a real level carries at minimum. The emptiest shipped map (Mid Castle)
    /// has 508, so this only rules out a blank or near-blank buffer — which is exactly what the map
    /// buffer holds before the party first enters the castle.
    /// </summary>
    public const int MinWallBytes = 256;

    /// <summary>A plausible level number, in town or in the castle.</summary>
    public static bool IsPlausibleLevel(int level) => level >= TownLevel && level <= MaxLevel;

    /// <summary>Validates a decoded party-position block.</summary>
    public static bool IsPlausiblePosition(int level, int x, int y, int facing) =>
        IsPlausibleLevel(level) &&
        x >= 0 && x < GridSize &&
        y >= 0 && y < GridSize &&
        facing >= 0 && facing < Directions;

    /// <summary>
    /// Validates a <see cref="FileSize"/>-byte window as a Dark Designs level.
    ///
    /// Two tests carry this, and both are needed.
    ///
    /// <b>Wall reciprocity</b> — a square's east wall byte and its eastern neighbour's west wall
    /// byte hold the same value, for all 3,968 interior neighbour pairs — rules out unrelated
    /// memory. But it is a relation between squares a fixed distance apart, so it cannot tell the
    /// buffer from a copy of itself shifted by whole squares, and the real buffer has zero bytes in
    /// front of it that a shifted window reads as empty map. <see cref="HasConsistentTextTables"/>
    /// is what pins the alignment.
    ///
    /// The rest — wall bytes within the range the movement code accepts, content bytes obeying
    /// <see cref="IsPlausibleContentByte"/>, and enough content to not be a blank buffer — is cheap
    /// pre-filtering.
    /// </summary>
    public static bool LooksLikeMap(byte[] b, int o)
    {
        if (b == null || o < 0 || o + FileSize > b.Length) return false;

        int nonZeroWalls = 0;
        for (int i = 0; i < WallsLength; i++)
        {
            int v = b[o + OffWalls + i];
            if (v > MaxWallValue) return false;
            if (v != 0) nonZeroWalls++;
        }
        if (nonZeroWalls < MinWallBytes) return false;

        bool anyContent = false;
        for (int i = 0; i < ContentsLength; i++)
        {
            int v = b[o + OffContents + i];
            if (!IsPlausibleContentByte(v)) return false;
            if (DecodeEventCode(v) != 0) anyContent = true;
        }
        if (!anyContent) return false;

        return HasWallReciprocity(b, o) && HasConsistentTextTables(b, o);
    }

    /// <summary>
    /// The description block agrees with the tables that index it: every text run lands inside the
    /// 127 lines, and every line's length prefix fits its 40-byte slot.
    ///
    /// This is here because reciprocity alone is <b>not</b> enough, which is not obvious and cost a
    /// live debugging session to find out. Reciprocity is a relation between squares a fixed
    /// distance apart, so it survives translating the whole grid by whole squares — and the map
    /// buffer is preceded in the data segment by a few hundred zero bytes, which a translated
    /// window happily reads as the level's empty north-west corner. Measured against the running
    /// game, <b>113</b> different offsets around the real buffer passed the wall test, every one of
    /// them a multiple of four bytes from the truth. These two table checks are sensitive to the
    /// exact byte alignment rather than to relative distances, and they cut those 113 down to the
    /// one correct offset.
    /// </summary>
    public static bool HasConsistentTextTables(byte[] b, int o)
    {
        if (b == null || o < 0 || o + FileSize > b.Length) return false;

        for (int code = 0; code < EventCodeCount; code++)
        {
            int first = b[o + OffTextFirst + code];
            int count = b[o + OffTextCount + code];
            if (count == 0) continue;                       // no text for this code
            // The game reads line `first + i - 1` for i = 1..count, so a run must start at line 1
            // or later and end inside the block — first = 0 would have it read before the block.
            if (first < 1) return false;
            if (first + count - 1 > TextLineCount - 1) return false;
        }

        for (int line = 0; line < TextLineCount; line++)
            if (b[o + OffTextLines + line * TextLineSize] > TextLineSize - 1) return false;

        return true;
    }

    /// <summary>
    /// The only constraint a content byte actually has to obey.
    ///
    /// A square the party has never stood on still holds the code the map file shipped, which is
    /// 0..<see cref="MaxEventCode"/>. Once the mapped bit is set the game is free to write a code
    /// above that — and does, stamping <c>0xF7</c> over an opened chest and <c>0xF8</c> over a
    /// taken item — so a visited square constrains nothing.
    ///
    /// Getting this wrong is not academic: an earlier version required bit 6 clear on every
    /// content byte, which held for the shipped maps only because none of them has been played.
    /// The first chest a player opened would have made their own map fail to validate, and with it
    /// the whole locate.
    /// </summary>
    public static bool IsPlausibleContentByte(int contentByte) =>
        (contentByte & VisitedFlag) != 0 || (contentByte & EventCodeMask) <= MaxEventCode;

    /// <summary>
    /// True when every pair of neighbouring squares agrees about the wall between them. Both sides
    /// are checked, so a one-sided edit shows up from either square.
    /// </summary>
    public static bool HasWallReciprocity(byte[] b, int o)
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                for (int d = 0; d < Directions; d++)
                {
                    int nx = x + DeltaX[d], ny = y + DeltaY[d];
                    if (!InBounds(nx, ny)) continue;
                    if (b[o + WallIndex(x, y, d)] != b[o + WallIndex(nx, ny, Opposite(d))])
                        return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// A handful of reciprocity pairs, spread across the grid, for the structural sweep to reject a
    /// candidate on before it pays for anything longer. Random data fails one of these almost
    /// immediately, which is what keeps the sweep linear in practice; survivors still go through
    /// <see cref="LooksLikeMap"/> in full.
    /// </summary>
    public static bool PassesReciprocityProbe(byte[] b, int o)
    {
        if (b == null || o < 0 || o + FileSize > b.Length) return false;

        // A stride that is coprime with the grid so the samples walk both axes rather than
        // marching down one column.
        for (int n = 0; n < ProbePairs; n++)
        {
            int x = (n * 7) % (GridSize - 1);
            int y = (n * 11) % (GridSize - 1);
            if (b[o + WallIndex(x, y, East)] != b[o + WallIndex(x + 1, y, West)]) return false;
            if (b[o + WallIndex(x, y, South)] != b[o + WallIndex(x, y + 1, North)]) return false;
        }
        return true;
    }

    /// <summary>How many neighbour pairs <see cref="PassesReciprocityProbe"/> samples.</summary>
    public const int ProbePairs = 24;
}
