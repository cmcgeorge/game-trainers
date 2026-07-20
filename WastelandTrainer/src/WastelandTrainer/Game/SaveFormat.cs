namespace WastelandTrainer.Game;

/// <summary>
/// Byte-level layout of the <b>savegame block</b> that Wasteland embeds inside each of its two save
/// files, GAME1 (party in group 0) and GAME2 (its second image). The block is one of many
/// <c>"msqN"</c>-tagged, rotating-XOR-obfuscated chunks in the file; this one carries the party's
/// world position, the in-game clock, a save serial number, and the seven 256-byte character records
/// (identical in shape to the live records the trainer already edits via
/// <see cref="CharacterFormat"/>).
///
/// <para>Structure of the block, from its start:</para>
/// <list type="bullet">
///   <item><c>+0x00</c> — 4-byte tag, <c>"msq0"</c> in GAME1 / <c>"msq1"</c> in GAME2.</item>
///   <item><c>+0x04</c> — two rotating-XOR seed/checksum bytes (see <see cref="RotatingXor"/>).</item>
///   <item><c>+0x06</c> — <see cref="PayloadSize"/> bytes of encrypted payload (decoded below).</item>
///   <item>then a <see cref="ReservedTailSize"/>-byte zero-filled tail (macro storage), so the whole
///   block occupies <see cref="ReservedBlockSize"/> bytes.</item>
/// </list>
///
/// Offsets and the codec were derived from the open-source <c>kayahr/wastelib</c> file-format project
/// and verified byte-for-byte against the shipped GAME1/GAME2 in <c>test/FormatCheck</c>. All
/// multi-byte integers are little-endian.
/// </summary>
public static class SaveFormat
{
    // --- block framing -------------------------------------------------------
    /// <summary>Length of the <c>"msqN"</c> block tag.</summary>
    public const int TagSize = 4;

    /// <summary>Decoded payload size in bytes (four group headers, party state, seven records).</summary>
    public const int PayloadSize = 0x800;

    /// <summary>Zero-filled reserved tail that follows the payload inside the block.</summary>
    public const int ReservedTailSize = 0x0A00;

    /// <summary>Total on-disk size of the savegame block (tag + seed + payload + reserved tail).</summary>
    public const int ReservedBlockSize = TagSize + RotatingXor.SeedSize + PayloadSize + ReservedTailSize; // 0x1206

    /// <summary>The two possible block tags: index 0 = GAME1, index 1 = GAME2.</summary>
    public static readonly string[] Tags = { "msq0", "msq1" };

    /// <summary>Documented savegame-block start offsets in a freshly shipped save (used as a hint only —
    /// the container locates the block by scanning + a clean decode, not by trusting these).</summary>
    public static readonly int[] ShippedBlockOffset = { 152517, 166855 };

    // --- party-group headers (four contiguous records at the payload start) --
    /// <summary>Number of party-group headers at the start of the payload.</summary>
    public const int GroupCount = 4;

    /// <summary>Size of one party-group header record.</summary>
    public const int GroupSize = 14;

    /// <summary>Byte 0 is unused; bytes 1..7 are the marching order (1-based slot, 0 = empty).</summary>
    public const int GroupOrder = 0x01;
    public const int GroupOrderLength = 7;
    public const int GroupX = 0x08;
    public const int GroupY = 0x09;
    public const int GroupMap = 0x0A;
    public const int GroupPrevX = 0x0B;
    public const int GroupPrevY = 0x0C;
    public const int GroupPrevMap = 0x0D;

    // --- party state (single fields after the four group headers) ------------
    /// <summary>Signed viewport origin X — the map cell drawn at the top-left; the active party's
    /// world X is <c>ViewportX + <see cref="ViewportOffsetX"/></c>.</summary>
    public const int ViewportX = 0x78;
    public const int ViewportY = 0x79;

    /// <summary>The party is drawn offset from the viewport origin by this many cells (X 9, Y 4), so a
    /// world position of (px,py) sets the viewport to (px-9, py-4). This is how a save-edited teleport
    /// centres the party the way the game would after a map load.</summary>
    public const int ViewportOffsetX = 9;
    public const int ViewportOffsetY = 4;

    public const int CurrentMembers = 0x7D;
    public const int CurrentPartyIndex = 0x7E;
    public const int CurrentMap = 0x7F;
    public const int TotalMembers = 0x80;
    public const int ExtraGroupCount = 0x81;
    public const int Minute = 0x83;
    public const int Hour = 0x84;
    public const int CombatScrollSpeed = 0x85;

    /// <summary>Little-endian u32 save serial; the game loads whichever of GAME1/GAME2 has the higher
    /// value and bumps it by 2 on each save. The editor bumps it so the edited file wins the next load.</summary>
    public const int Serial = 0xF5;
    public const int SerialSize = 4;
    public const int SerialBump = 2;

    // --- character records ---------------------------------------------------
    /// <summary>Offset of the first of <see cref="CharacterFormat.MaxSlots"/> character records; each is
    /// <see cref="CharacterFormat.RecordSize"/> bytes with the same layout as a live in-memory record.</summary>
    public const int CharacterArea = 0x100;

    /// <summary>Payload offset of the character record for the given roster slot (0..6).</summary>
    public static int CharacterOffset(int slot) => CharacterArea + slot * CharacterFormat.RecordSize;

    /// <summary>Highest valid map coordinate (Wasteland maps are at most 64×64).</summary>
    public const int MapCoordinateCeiling = 64;
}
