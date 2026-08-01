namespace JumpmanLivesTrainer.Game;

/// <summary>
/// Fixed memory-layout facts for locating the player's live game state inside Jumpman Lives!
/// (Apogee Software, 1991 — our copy is <c>JMAN2.EXE</c>, 136,431 bytes, EXEPACK-packed Turbo Pascal 6.0),
/// recovered from the game's complete source code and Borland linker map (<c>JMLIVES!.MAP</c>).
///
/// <para>The game is a real-mode DOS program the trainer reaches through the emulator. Turbo Pascal 6.0
/// uses a single data segment (DGROUP) whose segment value changes between sessions, but every global
/// sits at a constant DGROUP-relative offset for this build. Because those offsets are constant, the
/// player's lives, score, bonus, and level are reachable without any per-value scan: find DGROUP once
/// (by anchoring on a static byte pattern whose DGROUP offset is known) and read at
/// <c>DGROUP + offset</c>.</para>
///
/// <para>All offsets are from <c>JMLIVES!.MAP</c> — the Borland linker map shipped with the source code.
/// The record layout is from <c>TYPES.INC</c>. The record size (92 bytes) is confirmed by the MAP file:
/// the <c>p</c> array at <c>0xCFE6</c> and the next symbol <c>dots</c> at <c>0xD436</c> differ by
/// <c>0x450 = 1104 = 12 × 92</c>.</para>
/// </summary>
public static class GameLayout
{
    // --- anchor pattern (jp1 — vertical jump trajectory table) -----------------------------
    /// <summary>
    /// DGROUP offset of <c>jp1</c>, the 22-byte vertical jump trajectory table. [Confirmed] from MAP
    /// file and GLOBALS.PAS initialiser. Used as the primary anchor because 22 bytes is long enough to
    /// be unique in 16 MB of guest RAM.
    /// </summary>
    public const int AnchorOffset = 0x7D46;

    /// <summary>The anchor bytes — the <c>jp1</c> initialised array from GLOBALS.PAS.</summary>
    public static readonly byte[] AnchorBytes =
    {
        0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x00, 0x00, 0x00,
        0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE,
    };

    // --- validator patterns ------------------------------------------------------------------
    /// <summary>DGROUP offset of <c>PLAYSPEED</c> — the 8-byte speed table. [Confirmed] from MAP file.</summary>
    public const int PlayspeedOffset = 0x7D26;

    /// <summary>The <c>PLAYSPEED</c> initialised array: speed tick intervals for speeds 1–8.</summary>
    public static readonly byte[] PlayspeedBytes = { 0x03, 0x07, 0x0B, 0x0F, 0x11, 0x14, 0x1B, 0x26 };

    /// <summary>DGROUP offset of <c>ftwo</c> — 6-byte VGA palette data. [Confirmed] from MAP file.</summary>
    public const int FtwoOffset = 0x7D90;

    /// <summary>The <c>ftwo</c> initialised array.</summary>
    public static readonly byte[] FtwoBytes = { 0x01, 0x2B, 0x03, 0x2A, 0x17, 0x01 };

    /// <summary>How many validators must match before a candidate is accepted.</summary>
    public const int MinValidators = 2;

    // --- global game state -------------------------------------------------------------------
    /// <summary>DGROUP offset of <c>trainer</c> (BOOLEAN). [Confirmed] from MAP file.</summary>
    public const int OffTrainer = 0x7D2E;

    /// <summary>DGROUP offset of <c>current_level</c> (BYTE, 1–45). [Confirmed] from MAP file.</summary>
    public const int OffCurrentLevel = 0x7D3A;

    /// <summary>DGROUP offset of <c>bonus</c> (LONGINT, signed 32-bit). [Confirmed] from MAP file.</summary>
    public const int OffBonus = 0x7D3C;

    /// <summary>DGROUP offset of <c>maxpl</c> (BYTE, 1–4). [Confirmed] from MAP file.</summary>
    public const int OffMaxpl = 0x7D40;

    /// <summary>
    /// DGROUP offset of <c>which_to_play</c> (BYTE: 1=Jumpman, 2=Jr, 3=Original, 4=All, 5=Random).
    /// [Confirmed] from MAP file.
    /// </summary>
    public const int OffWhichToPlay = 0xD97A;

    /// <summary>DGROUP offset of <c>level_current</c> (BYTE). [Confirmed] from MAP file.</summary>
    public const int OffLevelCurrent = 0xD97F;

    /// <summary>DGROUP offset of <c>pl</c> — the current player index (BYTE, 1–4). [Confirmed] from MAP file.</summary>
    public const int OffPl = 0xD981;

    /// <summary>DGROUP offset of <c>eomission</c> (INTEGER, 2 bytes). [Confirmed] from MAP file.</summary>
    public const int OffEomission = 0xD9A8;

    /// <summary>DGROUP offset of <c>max_screens</c> (INTEGER, 2 bytes). [Confirmed] from MAP file.</summary>
    public const int OffMaxScreens = 0xD9AA;

    // --- globals window (covers PLAYSPEED through ftwo, used for validation + polling) -------
    /// <summary>
    /// Start of the globals window — begins at <see cref="PlayspeedOffset"/> so the PLAYSPEED
    /// validator pattern is inside the window.
    /// </summary>
    public const int GlobalWindowStart = PlayspeedOffset;

    /// <summary>
    /// Length of the globals window. Covers from <see cref="PlayspeedOffset"/> (0x7D26) through
    /// <see cref="FtwoOffset"/> + 6 (0x7D96), so one read validates both anchor patterns and
    /// fetches all the small globals (trainer, current_level, bonus, maxpl).
    /// </summary>
    public const int GlobalWindowLength = 0x70;

    // --- player array -------------------------------------------------------------------------
    /// <summary>DGROUP offset of <c>p</c> — the player array. [Confirmed] from MAP file.</summary>
    public const int PlayerArrayOffset = 0xCFE6;

    /// <summary>Size of each <c>player</c> record in bytes. [Confirmed] by MAP arithmetic.</summary>
    public const int PlayerRecordSize = 92;

    /// <summary>Maximum number of active players (the game uses p[1]–p[4]).</summary>
    public const int MaxActivePlayers = 4;

    // --- player record field offsets (within a 92-byte record) ------------------------------
    /// <summary>Player X position (INTEGER, 2 bytes). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerX = 6;

    /// <summary>Player Y position (INTEGER, 2 bytes, 0=top). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerY = 8;

    /// <summary>Death animation phase (SHORTINT, 0=alive, 2+=dying, 50=fully dead). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerPdeath = 51;

    /// <summary>Remaining lives (SHORTINT). [Confirmed] from TYPES.INC. Starts at 7 (or 21 in trainer mode).</summary>
    public const int PlayerLives = 52;

    /// <summary>Current speed (BYTE, 1–8). [Confirmed] from TYPES.INC. Indexes PLAYSPEED.</summary>
    public const int PlayerSpeed = 80;

    /// <summary>Speed for next level (BYTE, 1–8). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerNextSpeed = 81;

    /// <summary>Input device (BYTE: 0=keyboard, 1=joystick1, 2=joystick2). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerIDevice = 82;

    /// <summary>Player score (LONGINT, signed 32-bit, 4 bytes). [Confirmed] from TYPES.INC.</summary>
    public const int PlayerScore = 88;

    /// <summary>Bytes in the score field.</summary>
    public const int PlayerScoreBytes = 4;

    // --- caps ---------------------------------------------------------------------------------
    /// <summary>Maximum lives the trainer should set (the game's display is 2 digits).</summary>
    public const int MaxLives = 99;

    /// <summary>Starting lives in normal mode.</summary>
    public const int StartingLives = 7;

    /// <summary>Starting lives in trainer mode (TAB ×4).</summary>
    public const int TrainerLives = 21;

    /// <summary>Default time bonus per level.</summary>
    public const int DefaultBonus = 1500;

    /// <summary>Maximum speed value (slowest).</summary>
    public const int MaxSpeed = 8;

    /// <summary>Minimum speed value (fastest).</summary>
    public const int MinSpeed = 1;

    /// <summary>Maximum level number.</summary>
    public const int MaxLevel = 45;

    /// <summary>Extra life threshold (every 10,000 points).</summary>
    public const int ExtraLifeThreshold = 10_000;

    // --- little-endian accessors (unit-tested) ------------------------------------------------
    /// <summary>Reads the byte at <paramref name="off"/>.</summary>
    public static byte ReadU8(byte[] b, int off) => b[off];

    /// <summary>Reads the byte at <paramref name="off"/> as a signed value.</summary>
    public static sbyte ReadI8(byte[] b, int off) => unchecked((sbyte)b[off]);

    /// <summary>Reads the little-endian 16-bit value at <paramref name="off"/>.</summary>
    public static ushort ReadU16(byte[] b, int off) => (ushort)(b[off] | (b[off + 1] << 8));

    /// <summary>Reads the little-endian signed 16-bit value at <paramref name="off"/>.</summary>
    public static short ReadI16(byte[] b, int off) => (short)(b[off] | (b[off + 1] << 8));

    /// <summary>Reads the little-endian 32-bit value at <paramref name="off"/>.</summary>
    public static int ReadI32(byte[] b, int off) =>
        b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);

    /// <summary>Writes the byte at <paramref name="off"/>.</summary>
    public static void WriteU8(byte[] b, int off, byte v) => b[off] = v;

    /// <summary>Writes the little-endian 16-bit value at <paramref name="off"/>.</summary>
    public static void WriteI16(byte[] b, int off, short v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
    }

    /// <summary>Writes the little-endian 32-bit value at <paramref name="off"/>.</summary>
    public static void WriteI32(byte[] b, int off, int v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
        b[off + 2] = (byte)(v >> 16);
        b[off + 3] = (byte)(v >> 24);
    }

    // --- validation helpers (unit-tested; no process access) ----------------------------------
    /// <summary>
    /// Whether the globals window carries both validator patterns at their known offsets — i.e. this
    /// really is the game's data segment. <paramref name="window"/> must start at the candidate DGROUP
    /// base offset <see cref="GlobalWindowStart"/>.
    /// </summary>
    public static bool ValidateGlobals(ReadOnlySpan<byte> window)
    {
        int playspeedRel = PlayspeedOffset - GlobalWindowStart;
        if (!MatchAt(window, playspeedRel, PlayspeedBytes)) return false;

        int ftwoRel = FtwoOffset - GlobalWindowStart;
        if (!MatchAt(window, ftwoRel, FtwoBytes)) return false;

        return true;
    }

    /// <summary>Bytes needed for a globals validation window.</summary>
    public static int GlobalsWindowBytes => GlobalWindowLength;

    /// <summary>
    /// Whether the globals values are plausible: trainer is 0 or 1, level is 1–45, maxpl is 1–4.
    /// </summary>
    public static bool IsPlausibleGlobals(byte[] window)
    {
        if (window == null || window.Length < GlobalWindowLength) return false;
        int trainerRel = OffTrainer - GlobalWindowStart;
        int levelRel = OffCurrentLevel - GlobalWindowStart;
        int maxplRel = OffMaxpl - GlobalWindowStart;

        byte trainer = window[trainerRel];
        if (trainer > 1) return false;

        byte level = window[levelRel];
        if (level is < 1 or > MaxLevel) return false;

        byte maxpl = window[maxplRel];
        if (maxpl is < 1 or > 4) return false;

        return true;
    }

    private static bool MatchAt(ReadOnlySpan<byte> window, int offset, byte[] needle)
    {
        if (offset < 0 || offset + needle.Length > window.Length) return false;
        return window.Slice(offset, needle.Length).SequenceEqual(needle);
    }
}
