namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>
/// Byte-level layout of a Legacy of the Ancients character record as it lives in the
/// emulated DOS memory of a running game (DOSBox / DOSBox-X) and in the CHAR.DAT save file.
///
/// Legacy of the Ancients is a single-character RPG by Electronic Arts / Quest Software
/// (1987), compiled with Microsoft BASIC Compiler v6.00. The save file CHAR.DAT is
/// 3,444 bytes = 9 records of 382 bytes each (up to 8 characters + 1 active/header slot).
///
/// The layout was reverse-engineered from static analysis of a shipped CHAR.DAT save
/// (the first character "CHRISTOPHER": HP 200, all five characteristics 15, Level 1)
/// and cross-checked against the game manual. No live memory dump was available, so every
/// offset carries a confidence marker:
/// - [Static] — confirmed against the CHAR.DAT save file and/or the game manual
/// - [Inferred] — plausible from the CHAR.DAT but not independently confirmed
///
/// The game is compiled BASIC, so multi-byte numbers use Microsoft BASIC's binary
/// format: INTEGER (2-byte signed LE) and LONG (4-byte signed LE). The name is plain
/// ASCII, space-padded.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes. [Static] — 3444 / 9 = 382.</summary>
    public const int RecordSize = 0x17E;   // 382

    /// <summary>Number of record slots in CHAR.DAT. [Static] — 9 (1 active + 8 stored).</summary>
    public const int RecordCount = 9;

    // --- header (6 bytes) --------------------------------------------------
    /// <summary>Record header. [Static] — bytes 4-5 hold the record size (0x017E) for occupied slots, 0x0000 for empty.</summary>
    public const int OffHeader = 0x00;
    public const int HeaderSize = 6;

    /// <summary>Offset within the header of the 2-byte record-size field. [Static]</summary>
    public const int OffRecordSize = 0x04;

    // --- name (15 bytes) ---------------------------------------------------
    /// <summary>Character name, space-padded ASCII. [Static] — "CHRISTOPHER" at +0x06 in CHAR.DAT.</summary>
    public const int OffName = 0x06;
    public const int NameLength = 15;      // +0x06..+0x14

    // --- characteristics ----------------------------------------------------
    /// <summary>Strength — LONG (4-byte signed LE). [Static] — value 15 in CHAR.DAT.</summary>
    public const int OffStrength = 0x15;
    public const int StrengthSize = 4;

    /// <summary>Endurance — LONG (4-byte signed LE). [Static] — value 15 in CHAR.DAT.</summary>
    public const int OffEndurance = 0x21;
    public const int EnduranceSize = 4;

    /// <summary>Hit Points — INTEGER (2-byte signed LE). [Static] — value 200 in CHAR.DAT.</summary>
    public const int OffHP = 0x2F;
    public const int HPSize = 2;

    /// <summary>Level — INTEGER (2-byte signed LE). [Static] — value 1 in CHAR.DAT.</summary>
    public const int OffLevel = 0x31;
    public const int LevelSize = 2;

    /// <summary>Dexterity — INTEGER (2-byte signed LE). [Static] — value 15 in CHAR.DAT.</summary>
    public const int OffDexterity = 0x33;
    public const int DexteritySize = 2;

    /// <summary>Intelligence — LONG (4-byte signed LE). [Static] — value 15 in CHAR.DAT.</summary>
    public const int OffIntelligence = 0x45;
    public const int IntelligenceSize = 4;

    /// <summary>Charm — LONG (4-byte signed LE). [Static] — value 15 in CHAR.DAT.</summary>
    public const int OffCharm = 0x5D;
    public const int CharmSize = 4;

    /// <summary>Number of characteristics.</summary>
    public const int CharacteristicCount = 5;

    // --- possible max values / spell data ----------------------------------
    /// <summary>Five consecutive 0x7FFF (32767) values. [Inferred] — possibly max characteristics or spell data.</summary>
    public const int OffMaxValues = 0x110;
    public const int MaxValueCount = 5;
    public const int MaxValueSize = 2;

    // --- "max" targets used by the trainer's quick actions ------------------
    public const int MaxCharacteristic = 100;
    public const int MaxHP = 9999;
    public const int MaxLevelValue = 10;
    public const int MaxGold = 65535;

    /// <summary>Upper bound for HP when validating a scan candidate.</summary>
    public const int PlausibleHP = 99999;

    /// <summary>Upper bound for characteristics when validating.</summary>
    public const int PlausibleCharacteristic = 999;

    // --- lookup tables -------------------------------------------------------
    /// <summary>Characteristic names in manual order (Strength, Endurance, Dexterity, Intelligence, Charm).</summary>
    public static readonly string[] CharacteristicNames =
        { "Strength", "Endurance", "Dexterity", "Intelligence", "Charm" };

    /// <summary>Characteristic abbreviations.</summary>
    public static readonly string[] CharacteristicShort =
        { "STR", "END", "DEX", "INT", "CHA" };

    /// <summary>Record offsets for each characteristic, in the same order as <see cref="CharacteristicNames"/>.</summary>
    public static readonly int[] CharacteristicOffsets =
        { OffStrength, OffEndurance, OffDexterity, OffIntelligence, OffCharm };

    /// <summary>Sizes (in bytes) for each characteristic, in the same order.</summary>
    public static readonly int[] CharacteristicSizes =
        { StrengthSize, EnduranceSize, DexteritySize, IntelligenceSize, CharmSize };
}
