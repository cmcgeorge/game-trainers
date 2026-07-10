namespace DragonWarsTrainer.Game;

/// <summary>
/// Byte-level layout of a Dragon Wars character record as it lives in the emulated DOS
/// memory of a running game (DOSBox / DOSBox-X), plus the constants used to find the roster.
///
/// The party roster is an array of <see cref="MaxSlots"/> fixed-size records
/// (<see cref="RecordSize"/> bytes each). It is a separate allocation from the game's
/// static data, but sits at a fixed guest-memory delta (<see cref="RosterAnchorDelta"/>)
/// from the start of DATA1's chunk-0 header, which loads verbatim into memory and serves as
/// a unique locator anchor (see <see cref="AnchorBytes"/>).
///
/// The record layout was reverse-engineered from two live DOSBox-X memory dumps and
/// cross-checked against the open-source <c>fraterrisus/dragonjars</c> reimplementation
/// (its <c>Lists.REQUIREMENTS</c> / <c>Lists.CHAR_FIELDS</c> describe the same field order).
///
/// Text (names) uses Dragon Wars' string encoding: every character has its high bit set
/// EXCEPT the final one, which is plain ASCII; a 0 byte is padding.
/// </summary>
public static class RosterFormat
{
    /// <summary>Size of one character record in bytes.</summary>
    public const int RecordSize = 0x200;    // 512

    /// <summary>Number of roster slots (occupied slots hold a party member; the rest are 0xFF-filled).</summary>
    public const int MaxSlots = 7;

    /// <summary>
    /// Locator anchor: the first 48 bytes of DATA1 (its chunk-table header) load verbatim
    /// into guest RAM and appear exactly once, so they pin the game's data image regardless
    /// of where DOSBox mapped it. Roster slot 0 = (anchor address) + <see cref="RosterAnchorDelta"/>.
    /// </summary>
    public static readonly byte[] AnchorBytes =
    {
        0x7c, 0x04, 0xd0, 0x00, 0x50, 0x01, 0x0e, 0x15, 0xf8, 0x00, 0xfa, 0x00,
        0x8a, 0x0d, 0x00, 0x16, 0x01, 0x03, 0xbf, 0x02, 0x2d, 0x06, 0x6d, 0x03,
        0xd1, 0x05, 0xe9, 0x06, 0x95, 0x02, 0xb0, 0x03, 0x00, 0x20, 0x66, 0x01,
        0x5f, 0x05, 0x72, 0x05, 0x87, 0x03, 0x01, 0x01, 0x4e, 0x00, 0x00, 0x00
    };

    /// <summary>Guest-memory delta from the anchor's start to roster slot 0 (confirmed in two dumps).</summary>
    public const int RosterAnchorDelta = 0x0D0E;

    // --- record field offsets ------------------------------------------------
    public const int OffName = 0x00;
    public const int NameLength = 12;

    // Four attributes, each a current byte followed by a base byte.
    public const int OffStrCur = 0x0C;
    public const int OffStrBase = 0x0D;
    public const int OffDexCur = 0x0E;
    public const int OffDexBase = 0x0F;
    public const int OffIntCur = 0x10;
    public const int OffIntBase = 0x11;
    public const int OffSprCur = 0x12;
    public const int OffSprBase = 0x13;
    public const int AttributeCount = 4;

    // Three vitals, each a current UInt16 followed by a max UInt16.
    public const int OffHealthCur = 0x14;
    public const int OffHealthMax = 0x16;
    public const int OffStunCur = 0x18;
    public const int OffStunMax = 0x1A;
    public const int OffPowerCur = 0x1C;
    public const int OffPowerMax = 0x1E;

    public const int OffSkills = 0x20;      // 27 skill ranks, one byte each (0x20..0x3A)
    public const int SkillCount = 27;

    public const int OffUnspentPoints = 0x3B;   // unspent advancement points ("Unspent AP") available to allocate after levelling
    public const int OffSpells = 0x3C;      // 8 bytes, each a bitfield of 8 spells (0x3C..0x43)
    public const int SpellByteCount = 8;

    public const int OffStatus = 0x4C;      // status flag byte
    public const int OffNpcId = 0x4D;
    public const int OffGender = 0x4E;
    public const int OffLevel = 0x4F;       // UInt16
    public const int OffExperience = 0x51;  // UInt32
    public const int OffGold = 0x55;        // UInt32
    public const int OffArmorValue = 0x59;  // AV (to-hit)
    public const int OffDefenseValue = 0x5A;// DV
    public const int OffArmorClass = 0x5B;  // AC
    public const int OffFlags = 0x5C;

    // --- "max" targets used by the trainer's quick actions --------------------
    public const int MaxAttribute = 99;
    public const int MaxSkillRank = 60;
    public const int MaxVital = 999;        // health / stun / power maximum
    public const long MaxGold = 999_999;

    // --- status flags --------------------------------------------------------
    public const byte StatusDead = 0x01;
    public const byte StatusChained = 0x02;
    public const byte StatusPoisoned = 0x04;
    public const byte StatusPoisonedDisplay = 0x20;

    // --- lookup tables -------------------------------------------------------
    public static readonly string[] AttributeNames = { "Strength", "Dexterity", "Intelligence", "Spirit" };
    public static readonly string[] AttributeShort = { "STR", "DEX", "INT", "SPR" };

    /// <summary>Current-value offsets for the four attributes (each base is +1).</summary>
    public static readonly int[] AttributeCurOffsets = { OffStrCur, OffDexCur, OffIntCur, OffSprCur };

    public static readonly string[] SkillNames =
    {
        "Arcane Lore", "Cave Lore", "Forest Lore", "Mountain Lore",
        "Town Lore", "Bandage", "Climb", "Fistfighting",
        "Hiding", "Lockpick", "Pickpocket", "Swim",
        "Tracker", "Bureaucracy", "Druid Magic", "High Magic",
        "Low Magic", "Merchant", "Sun Magic", "Axes",
        "Flails", "Maces", "Swords", "Two-handers",
        "Bows", "Crossbows", "Thrown Weapons"
    };

    /// <summary>Spell names, one row per spell byte (0x3C..0x43); bit N of the byte = spell N in the row.</summary>
    public static readonly string[][] SpellNames =
    {
        new[] { "Mage Fire", "Disarm", "Charm", "Luck", "Lesser Heal", "Mage Light", "Fire Light", "Elvar's Fire" },
        new[] { "Poog's Vortex", "Ice Chill", "Big Chill", "Dazzle", "Mystic Might", "Reveal Glamour", "Sala's Swift", "Vorn's Guard" },
        new[] { "Cowardice", "Healing", "Group Heal", "Cloak Arcane", "Sense Traps", "Air Summon", "Earth Summon", "Water Summon" },
        new[] { "Fire Summon", "Death Curse", "Fire Blast", "Insect Plague", "Whirl Wind", "Scare", "Brambles", "Greater Healing" },
        new[] { "Cure All", "Create Wall", "Soften Stone", "Invoke Spirit", "Beast Call", "Wood Spirit", "Sun Stroke", "Exorcism" },
        new[] { "Rage of Mithras", "Wrath of Mithras", "Fire Storm", "Inferno", "Holy Aim", "Battle Power", "Column of Fire", "Mithras' Bless" },
        new[] { "Light Flash", "Armor of Light", "Sun Light", "Heal", "Major Healing", "Disarm Trap", "Guidance", "Radiance" },
        new[] { "Summon Salamander", "Charger", "Zak's Speed", "Kill Ray", "Prison", "(unused)", "(unused)", "(unused)" }
    };

    public static readonly string[] Genders = { "Male", "Female", "Sometimes", "Never" };

    public static string GenderName(int v) => v >= 0 && v < Genders.Length ? Genders[v] : $"?({v})";

    /// <summary>Human-readable status: "Okay" when clear, else a list of the set flags.</summary>
    public static string StatusName(int status)
    {
        if (status == 0) return "Okay";
        var parts = new List<string>();
        if ((status & StatusDead) != 0) parts.Add("Dead");
        if ((status & StatusChained) != 0) parts.Add("Chained");
        if (((status & StatusPoisoned) != 0) || ((status & StatusPoisonedDisplay) != 0)) parts.Add("Poisoned");
        int known = StatusDead | StatusChained | StatusPoisoned | StatusPoisonedDisplay;
        int other = status & ~known;
        if (other != 0) parts.Add($"0x{other:X2}");
        return parts.Count == 0 ? $"0x{status:X2}" : string.Join(", ", parts);
    }
}
