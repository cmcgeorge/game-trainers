namespace AmberstarTrainer.Game;

/// <summary>
/// Byte-level layout of an Amberstar character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X), plus the constants used to find the party.
///
/// Amberstar was originally developed for the Atari ST (Motorola 68000, big-endian) and
/// ported to PC. The character data retains the original big-endian byte order for all
/// multi-byte values. Each record starts with a two-byte magic header <c>00 FF</c>.
///
/// The party is an array of up to <see cref="MaxSlots"/> contiguous records. Party
/// members (Type = 0) use the core record up to the item data; NPC-specific fields
/// (interactions, portrait, dialogue) are not present for PCs.
///
/// The layout is derived from the open-source Pyrdacor/Amberstar file specification
/// (https://github.com/Pyrdacor/Amberstar/blob/main/FileSpecs/CharData.md), confirmed
/// against the GAME.EXE V1.34 (22.10.1992) IBM AT build by Frank Ussner & Gino Fehr.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Core record size for a party member (up to the NPC-interaction block).</summary>
    public const int RecordSize = 0x047A;  // 1146

    /// <summary>Maximum party slots.</summary>
    public const int MaxSlots = 6;

    // --- magic header --------------------------------------------------------
    public const int OffMagic = 0x0000;    // Word (big-endian): always 00 FF
    public const ushort MagicValue = 0x00FF;

    // --- identity ------------------------------------------------------------
    public const int OffType = 0x0002;     // 0 = Person (NPC/PC), 1 = Monster
    public const int OffGender = 0x0003;   // 0 = Male, 1 = Female
    public const int OffRace = 0x0004;     // see RaceBook
    public const int OffClass = 0x0005;    // see ClassBook

    // --- skills (current + max, each a byte) ---------------------------------
    public const int OffSkillsCur = 0x0006;   // 10 bytes: ATK, PAR, SWI, LIS, F-T, D-T, P-L, SEA, RMS, U-M
    public const int OffSkillsMax = 0x0010;   // 10 bytes (same order)
    public const int SkillCount = 10;

    // --- magic / combat ------------------------------------------------------
    public const int OffMagicSchools = 0x001A; // flags: 2=white, 4=grey, 8=black, 128=special
    public const int OffLevel = 0x001B;        // Byte
    public const int OffUsedHands = 0x001C;
    public const int OffUsedFingers = 0x001D;
    public const int OffBaseDef = 0x001E;      // Base Defense
    public const int OffBaseDam = 0x001F;      // Base Damage
    public const int OffMagicBonusWeapon = 0x0020;
    public const int OffMagicBonusArmour = 0x0021;

    // --- item amounts --------------------------------------------------------
    public const int OffEquippedAmounts = 0x0022;  // 9 bytes
    public const int OffInventoryAmounts = 0x002B;  // 12 bytes

    // --- languages / ailments ------------------------------------------------
    public const int OffLanguages = 0x0037;   // bitfield
    public const int OffPhysicalAilments = 0x003A; // bitfield
    public const int OffMentalAilments = 0x003B;   // bitfield

    // --- attributes (current + max, each a big-endian Word) ------------------
    public const int OffAttrCur = 0x0048;  // 9 Words: STR, INT, DEX, SPE, CON, CHA, LUC, MAG, AGE
    public const int OffAttrMax = 0x005C;  // 9 Words (same order)
    public const int AttributeCount = 9;
    public const int AttributeSize = 2;    // big-endian Word

    // --- progression ---------------------------------------------------------
    public const int OffLvlAtt = 0x0070;   // Word
    public const int OffHpPerLvl = 0x0072; // Word
    public const int OffSpPerLvl = 0x0074; // Word
    public const int OffSlpPerLvl = 0x0076;// Word

    // --- vitals (big-endian Words) -------------------------------------------
    public const int OffHpCur = 0x0086;
    public const int OffHpMax = 0x0088;
    public const int OffSpCur = 0x008A;    // Spell Points
    public const int OffSpMax = 0x008C;
    public const int OffSlp = 0x008E;      // Spell Learning Points

    // --- resources (big-endian Words) ----------------------------------------
    public const int OffGold = 0x0090;
    public const int OffFood = 0x0092;

    // --- bonus from equipment (big-endian) -----------------------------------
    public const int OffBonusDef = 0x0094;
    public const int OffBonusDam = 0x0096;
    public const int OffBonusHp = 0x0098;
    public const int OffBonusSp = 0x009A;

    // --- experience (big-endian Long) ----------------------------------------
    public const int OffExperience = 0x00CC;  // 4 bytes

    // --- known spells (big-endian Longs, bitfields) --------------------------
    public const int OffSpellsWhite = 0x00D0;
    public const int OffSpellsGrey = 0x00D4;
    public const int OffSpellsBlack = 0x00D8;
    public const int OffSpellsSpecial = 0x00E8;
    public const int SpellFieldCount = 4;

    // --- weight / name -------------------------------------------------------
    public const int OffWeight = 0x00EC;   // Long (grams)
    public const int OffName = 0x00F0;     // 16 bytes (15 chars + null)
    public const int NameLength = 16;

    // --- items (each 40 bytes = 0x28) ----------------------------------------
    public const int OffEquippedItems = 0x0132;  // 9 items
    public const int OffInventoryItems = 0x029A;  // 12 items
    public const int EquippedItemCount = 9;
    public const int InventoryItemCount = 12;
    public const int ItemSize = 0x28;     // 40 bytes

    // --- "max" targets used by the trainer's quick actions -------------------
    public const int MaxAttribute = 999;
    public const int MaxSkill = 99;
    public const int MaxVital = 9999;
    public const int MaxGold = 65535;
    public const int MaxFood = 65535;
    public const long MaxExperience = 999_999_999;

    // --- physical ailments flags ---------------------------------------------
    public const byte AilStunned = 0x01;
    public const byte AilPoisoned = 0x02;
    public const byte AilPetrified = 0x04;
    public const byte AilDiseased = 0x08;
    public const byte AilAging = 0x10;
    public const byte AilDead = 0x20;
    public const byte AilAsh = 0x40;
    public const byte AilDust = 0x80;

    // --- mental ailments flags -----------------------------------------------
    public const byte AilIrritated = 0x01;
    public const byte AilMad = 0x02;
    public const byte AilSleeping = 0x04;
    public const byte AilAfraid = 0x08;
    public const byte AilBlind = 0x10;
    public const byte AilOverloaded = 0x20;

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AttributeNames =
        { "Strength", "Intelligence", "Dexterity", "Speed", "Constitution",
          "Charisma", "Luck", "Anti-Magic", "Age" };
    public static readonly string[] AttributeShort =
        { "STR", "INT", "DEX", "SPE", "CON", "CHA", "LUC", "MAG", "AGE" };

    public static readonly string[] SkillNames =
        { "Attack", "Parry", "Swim", "Listen", "Find Traps",
          "Disarm Traps", "Pick Locks", "Search", "Read Magic", "Use Magic" };
    public static readonly string[] SkillShort =
        { "ATK", "PAR", "SWI", "LIS", "F-T", "D-T", "P-L", "SEA", "RMS", "U-M" };

    /// <summary>Human-readable physical ailments: "Okay" when clear, else a list.</summary>
    public static string PhysicalAilmentsName(int v)
    {
        if (v == 0) return "Okay";
        var parts = new List<string>();
        if ((v & AilStunned) != 0) parts.Add("Stunned");
        if ((v & AilPoisoned) != 0) parts.Add("Poisoned");
        if ((v & AilPetrified) != 0) parts.Add("Petrified");
        if ((v & AilDiseased) != 0) parts.Add("Diseased");
        if ((v & AilAging) != 0) parts.Add("Aging");
        if ((v & AilDead) != 0) parts.Add("Dead");
        if ((v & AilAsh) != 0) parts.Add("Ash");
        if ((v & AilDust) != 0) parts.Add("Dust");
        int known = AilStunned | AilPoisoned | AilPetrified | AilDiseased | AilAging | AilDead | AilAsh | AilDust;
        int other = v & ~known;
        if (other != 0) parts.Add($"0x{other:X2}");
        return parts.Count == 0 ? $"0x{v:X2}" : string.Join(", ", parts);
    }

    /// <summary>Human-readable mental ailments.</summary>
    public static string MentalAilmentsName(int v)
    {
        if (v == 0) return "Okay";
        var parts = new List<string>();
        if ((v & AilIrritated) != 0) parts.Add("Irritated");
        if ((v & AilMad) != 0) parts.Add("Mad");
        if ((v & AilSleeping) != 0) parts.Add("Sleeping");
        if ((v & AilAfraid) != 0) parts.Add("Afraid");
        if ((v & AilBlind) != 0) parts.Add("Blind");
        if ((v & AilOverloaded) != 0) parts.Add("Overloaded");
        int known = AilIrritated | AilMad | AilSleeping | AilAfraid | AilBlind | AilOverloaded;
        int other = v & ~known;
        if (other != 0) parts.Add($"0x{other:X2}");
        return parts.Count == 0 ? $"0x{v:X2}" : string.Join(", ", parts);
    }
}
