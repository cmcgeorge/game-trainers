namespace Questron2Trainer.Game;

/// <summary>
/// Byte-level layout of a Questron II character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X).
///
/// Questron II is a single-character RPG (unlike the party-based Dragon Wars or Wasteland),
/// so there is exactly one character record in memory at a time.
///
/// The layout was reverse-engineered from static analysis of the shipped DEMOFILE save
/// (the demo character "The Thing": HP 200, Food 188, Gold 162, all attributes 15, Level 1)
/// and cross-checked against the game manual and strings extracted from START.EXE. No live
/// memory dump was available, so every offset carries a confidence marker:
/// - [Static] — confirmed against the DEMOFILE and/or the game manual
/// - [Inferred] — plausible from the DEMOFILE but not independently confirmed
/// See <c>docs\Questron2-Reverse-Engineering.md</c>.
///
/// All multi-byte integers are little-endian. Names are plain ASCII (null-terminated).
/// The game engine is START.EXE, an EXEPACK-compressed Microsoft C 1987 build by
/// Westwood Associates / Quest Software / SSI, version 1.2.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record in bytes. [Inferred] — covers all fields identified in the DEMOFILE.</summary>
    public const int RecordSize = 0x100;   // 256

    // --- vitals (uint16 LE) -------------------------------------------------
    /// <summary>Hit Points — damage the character can take before dying. [Static] — DEMOFILE 200, manual "begins at 200".</summary>
    public const int OffHP = 0x00;

    /// <summary>Food — days of food remaining. [Static] — DEMOFILE 188, manual "buy food in towns".</summary>
    public const int OffFood = 0x02;

    /// <summary>Gold — money carried. [Static] — DEMOFILE 162, manual "begins at 200".</summary>
    public const int OffGold = 0x04;

    /// <summary>Flag or item count. [Inferred] — DEMOFILE 03 (three starting items per manual).</summary>
    public const int OffFlag = 0x06;

    // --- attributes (one byte each) -----------------------------------------
    /// <summary>Five attributes: Charisma, Strength, Agility, Stamina, Intelligence. [Static] — DEMOFILE all 15.</summary>
    public const int OffAttributes = 0x07;
    public const int AttributeCount = 5;

    // --- equipment ----------------------------------------------------------
    /// <summary>Equipped weapon ID. [Inferred] — DEMOFILE 07 (Shortbow, 0-indexed in weapon table).</summary>
    public const int OffWeapon = 0x10;

    /// <summary>Equipped armor ID. [Inferred] — DEMOFILE 05 (Plate Mail, 0-indexed in armor table).</summary>
    public const int OffArmor = 0x11;

    // --- progression --------------------------------------------------------
    /// <summary>Level. [Inferred] — DEMOFILE 01 (Adventurer per the level name table).</summary>
    public const int OffLevel = 0x18;

    // --- inventory flags ----------------------------------------------------
    /// <summary>Item ownership flags. [Inferred] — DEMOFILE has sparse 01s at +0x27, +0x2F, +0x3F (3 items).</summary>
    public const int OffItems = 0x20;
    public const int ItemFlagBytes = 48;   // +0x20..+0x4F

    // --- name ---------------------------------------------------------------
    /// <summary>Character name, null-terminated ASCII. [Static] — DEMOFILE "The Thing" at +0x50.</summary>
    public const int OffName = 0x50;
    public const int NameLength = 16;      // +0x50..+0x5F

    // --- combat / spells ----------------------------------------------------
    /// <summary>Combat and inventory data. [Inferred] — includes experience, max HP, etc.</summary>
    public const int OffCombat = 0x60;

    /// <summary>Spell charges, one byte per spell. [Inferred] — DEMOFILE 01 01 01 01 01 01 01 01 (8 bytes).</summary>
    public const int OffSpellCharges = 0x86;
    public const int SpellSlotCount = 8;

    // --- "max" targets used by the trainer's quick actions ------------------
    public const int MaxAttribute = 25;
    public const int MaxHP = 9999;
    public const int MaxFood = 9999;
    public const int MaxGold = 65535;
    public const int MaxLevel = 20;
    public const int MaxSpellCharges = 99;

    /// <summary>Upper bound for HP when validating a scan candidate (comfortably above MaxHP).</summary>
    public const int PlausibleHP = 99999;

    // --- lookup tables -------------------------------------------------------
    /// <summary>Attribute names in record order (Charisma, Strength, Agility, Stamina, Intelligence).</summary>
    public static readonly string[] AttributeNames =
        { "Charisma", "Strength", "Agility", "Stamina", "Intelligence" };

    /// <summary>Attribute abbreviations.</summary>
    public static readonly string[] AttributeShort =
        { "CHA", "STR", "AGI", "STA", "INT" };

    /// <summary>Level/rank names extracted from START.EXE strings.</summary>
    public static readonly string[] LevelNames =
        { "Nothing", "Adventurer", "Apprentice", "Knight" };

    /// <summary>Returns the level name for a given level value, clamping to the last known name for levels beyond the table.</summary>
    public static string LevelName(int level) =>
        level <= 0 ? LevelNames[0]
        : level >= LevelNames.Length ? LevelNames[^1]
        : LevelNames[level];
}
