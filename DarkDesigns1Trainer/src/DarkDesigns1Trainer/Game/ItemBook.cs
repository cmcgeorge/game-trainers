namespace DarkDesigns1Trainer.Game;

/// <summary>
/// The 64 item ids of Dark Designs I, transcribed from the item table in the unpacked
/// <c>DARKDES.EXE</c> (40-byte entries; entry 0 is the game's own "NO ITEM" placeholder).
/// The table index <em>is</em> the byte the game stores in a character's pack and readied
/// equipment slots, so these ids are what the trainer writes.
///
/// Ids 41–59 (minus 54) are monster natural weapons and hides — real, addressable ids the
/// game will happily let a character carry, but not things the shop sells. See
/// <c>docs/ReverseEngineering.md</c> §4.3.
/// </summary>
public static class ItemBook
{
    /// <summary>
    /// The entry's type byte, which is what the game's "ready equipment" screen tests when it
    /// decides whether an item may go in a given slot.
    /// </summary>
    public enum ItemType
    {
        /// <summary>One-handed and off-hand gear: daggers, short swords, shields.</summary>
        Light = 0,
        /// <summary>Standard one-handed weapons.</summary>
        Medium = 1,
        /// <summary>Two-handed weapons.</summary>
        TwoHanded = 2,
        /// <summary>Wands, potions, scrolls, keys — used, not readied.</summary>
        Usable = 3,
        Ring = 4,
        Armor = 5,
        /// <summary>A blank table slot the game never issues.</summary>
        Unused = 6,
    }

    /// <summary>The four readied-equipment slots the game prompts for.</summary>
    public enum ReadySlot { RightHand, LeftHand, Armor, Ring }

    // --- item table geometry, for patching the live table ---------------------
    /// <summary>Size of one entry in the game's item table.</summary>
    public const int EntrySize = 40;

    /// <summary>Entry offset of the item's name (the entry opens with two other bytes).</summary>
    public const int EntryOffName = 0x02;

    /// <summary>Entry offset of the type byte the ready screen tests.</summary>
    public const int EntryOffType = 0x00;

    /// <summary>
    /// Entry offset of the uint16 <em>potency</em>: the item's chance out of 256 of the good
    /// outcome. On <c>(U)se</c> the game applies the effect, rolls <c>random(256)</c> and destroys
    /// the item unless <c>potency &gt; roll</c>; in combat a magic weapon's special effect fires on
    /// the same test. This is the closest thing Dark Designs has to charges — see
    /// <c>docs/ReverseEngineering.md</c> §4.4.
    /// </summary>
    public const int EntryOffPotency = 0x14;

    /// <summary>A potency of 256 always beats <c>random(256)</c>, so the good outcome is certain.</summary>
    public const int PotencyAlways = 256;

    /// <summary>Grelminar's Staff — the quest item, carried but never sold.</summary>
    public const int QuestStaffId = 63;

    /// <param name="Id">Byte stored in the character record.</param>
    /// <param name="Power">Weapon damage, or effect id for a usable item.</param>
    /// <param name="Protection">Shield protection; 0 for everything else.</param>
    /// <param name="Price">Equipment-shop price in gold; 0 when not sold.</param>
    /// <param name="ClassMask">Bit 0 Fighter, bit 1 Priest, bit 2 Wizard.</param>
    /// <param name="Potency">
    /// Chance out of 256 that a usable item survives being used, or that a magic weapon's special
    /// effect triggers. 0 for ordinary gear, where the roll never happens.
    /// </param>
    public sealed record Item(
        int Id, string Name, ItemType Type, int Power, int Protection, int Price, int ClassMask,
        int Potency = 0)
    {
        /// <summary>True when the potency roll actually applies to this item.</summary>
        public bool HasPotency => Potency > 0 ||
            (Type == ItemType.Usable && IsPlayerItem);

        /// <summary>
        /// True for gear a character can actually be given. Excludes blank table slots and monster
        /// parts. A class mask alone is not enough: <c>Gaze</c> (id 32) is a monster attack that
        /// carries mask 0b111 but has no price and is never sold, so the shop price is what
        /// separates real gear from the monster entries. The quest staff is the one priceless
        /// exception.
        /// </summary>
        public bool IsPlayerItem =>
            Type != ItemType.Unused && ClassMask != 0 && (Price > 0 || Id == QuestStaffId);

        /// <summary>True when a character of <paramref name="characterClass"/> (1–3) may use this.</summary>
        public bool UsableBy(int characterClass) =>
            characterClass >= 1 && characterClass <= 3 && (ClassMask & (1 << (characterClass - 1))) != 0;

        /// <summary>Fighter/Priest/Wizard letters this item is legal for, e.g. "F P W".</summary>
        public string ClassLabel
        {
            get
            {
                if (ClassMask == 0) return "—";
                var parts = new List<string>(3);
                if ((ClassMask & 1) != 0) parts.Add("F");
                if ((ClassMask & 2) != 0) parts.Add("P");
                if ((ClassMask & 4) != 0) parts.Add("W");
                return string.Join(" ", parts);
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>Every id 0–63, indexed by id.</summary>
    public static readonly Item[] All =
    {
        new(0,  "(empty)",          ItemType.Unused,    0,  0,     0, 0b000),
        new(1,  "Dagger",           ItemType.Light,     3,  0,     5, 0b101),
        new(2,  "Staff",            ItemType.TwoHanded, 5,  0,    10, 0b111),
        new(3,  "Mace",             ItemType.Medium,    6,  0,    15, 0b011),
        new(4,  "Short Sword",      ItemType.Light,     7,  0,    20, 0b001),
        new(5,  "Long Sword",       ItemType.Medium,    9,  0,    30, 0b001),
        new(6,  "Battle Axe",       ItemType.TwoHanded, 10, 0,    40, 0b001),
        new(7,  "Two Hand Sword",   ItemType.TwoHanded, 11, 0,    50, 0b001),
        new(8,  "Shield",           ItemType.Light,     0,  30,   25, 0b011),
        new(9,  "Spiked Shield",    ItemType.Light,     3,  15,   35, 0b001),
        new(10, "Leather Armor",    ItemType.Armor,     2,  0,    20, 0b111),
        new(11, "Magic Shield",     ItemType.Light,     0,  55, 2000, 0b001),
        new(12, "Chain Mail",       ItemType.Armor,     4,  0,    50, 0b011),
        new(13, "Magic Armor",      ItemType.Armor,     8,  0,  3000, 0b011),
        new(14, "Plate Mail",       ItemType.Armor,     6,  0,   100, 0b011),
        new(15, "Full Plate",       ItemType.Armor,     7,  0,   250, 0b001),
        new(16, "Hell Dagger",      ItemType.Light,     15, 0,  4000, 0b111),
        new(17, "Gravedigger Axe",  ItemType.TwoHanded, 13, 0,  5000, 0b001,  66),
        new(18, "Paralyze Wand",    ItemType.Usable,    4,  0,  2000, 0b100,  10),
        new(19, "Wand of Evil",     ItemType.Usable,    8,  0,  3500, 0b100,  29),
        new(20, "Healing Potion",   ItemType.Usable,    11, 0,   150, 0b111, 128),
        new(21, "Extra Healing",    ItemType.Usable,    14, 0,   500, 0b111, 245),
        new(22, "Cureall Potion",   ItemType.Usable,    18, 0,  1500, 0b111, 255),
        new(23, "Medusa Skull",     ItemType.Usable,    22, 0,  7000, 0b111,  50),
        new(24, "Speed Ring",       ItemType.Ring,      0,  0,  1000, 0b111),
        new(25, "Strength Ring",    ItemType.Ring,      0,  0,  2000, 0b111),
        new(26, "Recall Scroll",    ItemType.Usable,    17, 0,  1500, 0b010, 250),
        new(27, "(unused 27)",      ItemType.Unused,    0,  0,     0, 0b000),
        new(28, "(unused 28)",      ItemType.Unused,    0,  0,     0, 0b000),
        new(29, "Mangling Mace",    ItemType.Medium,    10, 0,  3000, 0b011,  45),
        new(30, "Vampiric Sword",   ItemType.Medium,    10, 0,  2500, 0b001,  50),
        new(31, "Holy Sword",       ItemType.Medium,    12, 0,  7000, 0b001,  77),
        new(32, "Gaze",             ItemType.Medium,    1,  0,     0, 0b111, 250),
        new(33, "Old Dark Sword",   ItemType.Medium,    15, 0, 32768, 0b001,  80),
        new(34, "Trident of Pain",  ItemType.TwoHanded, 15, 0,  2010, 0b001, 200),
        new(35, "Electroblade",     ItemType.Medium,    12, 0,  5000, 0b001,  50),
        new(36, "(unused 36)",      ItemType.Unused,    0,  0,     0, 0b000),
        new(37, "Boom Blade",       ItemType.Medium,    14, 0,  5000, 0b001,  25),
        new(38, "Striking Staff",   ItemType.TwoHanded, 10, 0,  2500, 0b111),
        new(39, "Bone Basher",      ItemType.TwoHanded, 13, 0,  5000, 0b011),
        new(40, "Active Axe",       ItemType.TwoHanded, 10, 0,  3500, 0b001, 200),
        new(41, "Hide",             ItemType.Armor,     2,  0,     0, 0b000),
        new(42, "Thick Hide",       ItemType.Armor,     3,  0,     0, 0b000),
        new(43, "Scales",           ItemType.Armor,     5,  0,     0, 0b000),
        new(44, "Plated Hide",      ItemType.Armor,     6,  0,     0, 0b000),
        new(45, "Shell",            ItemType.Armor,     6,  0,     0, 0b000),
        new(46, "Shell & Scales",   ItemType.Armor,     8,  0,     0, 0b000),
        new(47, "Nip",              ItemType.Medium,    2,  0,     0, 0b000),
        new(48, "Claw",             ItemType.Medium,    3,  0,     0, 0b000),
        new(49, "Big Claw",         ItemType.Medium,    6,  0,     0, 0b000),
        new(50, "Huge Claw",        ItemType.Medium,    8,  0,     0, 0b000),
        new(51, "Bite",             ItemType.Medium,    7,  0,     0, 0b000),
        new(52, "Big Bite",         ItemType.Medium,    10, 0,     0, 0b000),
        new(53, "Huge Bite",        ItemType.Medium,    13, 0,     0, 0b000),
        new(54, "Bad Buckler",      ItemType.Light,     6,  30, 4500, 0b001),
        new(55, "Bash",             ItemType.Medium,    5,  0,     0, 0b000),
        new(56, "Hard Bash",        ItemType.Medium,    8,  0,     0, 0b000),
        new(57, "Tail",             ItemType.Medium,    7,  0,     0, 0b000),
        new(58, "Horn",             ItemType.Medium,    8,  0,     0, 0b000),
        new(59, "Spikes",           ItemType.Medium,    6,  0,     0, 0b000),
        new(60, "Key 1",            ItemType.Usable,    26, 0,    10, 0b111),
        new(61, "Key 2",            ItemType.Usable,    27, 0,    10, 0b111),
        new(62, "Key 3",            ItemType.Usable,    28, 0,    10, 0b111),
        new(63, "The Staff",        ItemType.Usable,    29, 0,     0, 0b111),
    };

    /// <summary>The entry for <paramref name="id"/>, or entry 0 when the id is out of range.</summary>
    public static Item Get(int id) =>
        id >= 0 && id < All.Length ? All[id] : All[0];

    /// <summary>Display name for <paramref name="id"/>; out-of-range ids are shown as-is.</summary>
    public static string Name(int id) =>
        id >= 0 && id < All.Length ? All[id].Name : $"?({id})";

    /// <summary>Gear a character can be given — excludes blanks and monster parts.</summary>
    public static IEnumerable<Item> PlayerItems => All.Where(i => i.IsPlayerItem);

    /// <summary>Record byte offset of a readied-equipment slot.</summary>
    public static int ReadyOffset(ReadySlot slot) => slot switch
    {
        ReadySlot.RightHand => CharacterFormat.OffReadyRightHand,
        ReadySlot.LeftHand => CharacterFormat.OffReadyLeftHand,
        ReadySlot.Armor => CharacterFormat.OffReadyArmor,
        ReadySlot.Ring => CharacterFormat.OffReadyRing,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    /// <summary>The label the game prints when prompting for this slot.</summary>
    public static string ReadyLabel(ReadySlot slot) => slot switch
    {
        ReadySlot.RightHand => "Right hand",
        ReadySlot.LeftHand => "Left hand",
        ReadySlot.Armor => "Armor",
        ReadySlot.Ring => "Ring",
        _ => slot.ToString(),
    };

    /// <summary>
    /// The game's own rule for whether an item may be readied in a slot: the right hand takes
    /// any weapon or shield, the left hand only light gear, and armor/ring slots only their
    /// own type. Emptying a slot (id 0) is always allowed.
    /// </summary>
    public static bool CanReady(ReadySlot slot, int itemId)
    {
        if (itemId == 0) return true;
        var type = Get(itemId).Type;
        return slot switch
        {
            ReadySlot.RightHand => type is ItemType.Light or ItemType.Medium or ItemType.TwoHanded,
            ReadySlot.LeftHand => type is ItemType.Light,
            ReadySlot.Armor => type is ItemType.Armor,
            ReadySlot.Ring => type is ItemType.Ring,
            _ => false,
        };
    }

    /// <summary>Items legal in a readied slot, always including "(empty)".</summary>
    public static IEnumerable<Item> ReadyOptions(ReadySlot slot) =>
        All.Where(i => i.Id == 0 || (i.IsPlayerItem && CanReady(slot, i.Id)));

    /// <summary>
    /// Usable items whose potency decides whether they survive being used — i.e. the ones a
    /// "never break" patch is for.
    /// </summary>
    public static IEnumerable<Item> Consumables =>
        All.Where(i => i.IsPlayerItem && i.Type == ItemType.Usable);

    /// <summary>
    /// Magic weapons whose potency is the chance their special effect fires in combat.
    /// Ordinary weapons have potency 0 and never roll.
    /// </summary>
    public static IEnumerable<Item> MagicWeapons =>
        All.Where(i => i.IsPlayerItem && i.Potency > 0 &&
                       i.Type is ItemType.Light or ItemType.Medium or ItemType.TwoHanded);
}
