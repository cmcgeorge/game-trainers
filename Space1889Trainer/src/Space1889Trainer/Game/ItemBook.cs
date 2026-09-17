namespace Space1889Trainer.Game;

/// <summary>The 23 item types ITEMS.DAT uses (record +0x2A), in the game's own order and spelling.</summary>
public enum ItemType
{
    Equipment, Tool, TravelEquipment, Explosive, Pistol, Rifle, Shotgun, MeleeWeapon, MissileWeapon,
    MachineGun, MartianArtillery, EuropeanArtillery, Armor, GeologicalInvention, BiologicalInvention,
    Ammunition, Jewelry, Manuscript, Talisman, Plant, AnimalHide, Identification, AnimalProduct,
}

/// <summary>One ITEMS.DAT record, reduced to what the trainer shows and needs.</summary>
/// <param name="Id">The value an inventory entry stores: record index + 1 (0 = empty slot).</param>
/// <param name="Name">Upper-case name; empty for the six unused records.</param>
/// <param name="Type">Record +0x2A.</param>
/// <param name="Price">Record +0x2B, pennies.</param>
/// <param name="Weight16">Record +0x2F, sixteenths of a pound.</param>
/// <param name="ShopMask">Record +0x29: 0x01 Earth, 0x02 Mercury, 0x04 Venus, 0x08 Mars (0 = never sold).</param>
/// <param name="Shots">Record +0x34: IN GUN after a reload.</param>
/// <param name="Damage">Record +0x36: hit points taken from an NPC per hit.</param>
/// <param name="Range">Record +0x39: range in squares.</param>
public sealed record ItemInfo(int Id, string Name, int Type, int Price, int Weight16, int ShopMask, int Shots, int Damage, int Range)
{
    /// <summary>The 0-based record index — what loaded ammunition, flyer guns and rewards store.</summary>
    public int Index => Id - 1;

    public ItemType Kind => (ItemType)Type;

    /// <summary>The game's name for the type.</summary>
    public string TypeName => ItemBook.TypeNames[Type];

    /// <summary>Carried weight in pounds.</summary>
    public double WeightPounds => Weight16 / 16.0;

    /// <summary>Is this one of the six empty records (ids 91, 142 and 167-170)?</summary>
    public bool IsEmptyRecord => Name.Length == 0;

    /// <summary>Firearms the game shows ROUNDS / IN GUN for (pistol, rifle, shotgun, machine gun).</summary>
    public bool UsesAmmunition => Kind is ItemType.Pistol or ItemType.Rifle or ItemType.Shotgun or ItemType.MachineGun;

    /// <summary>Hand weapons the USE command puts in hand (types 4-9).</summary>
    public bool IsHandWeapon => Type is >= 4 and <= 9;

    /// <summary>Ship guns an ether flyer can mount (types 10 and 11).</summary>
    public bool IsShipGun => Kind is ItemType.MartianArtillery or ItemType.EuropeanArtillery;

    /// <summary>The planets whose shops stock it, e.g. "Earth, Mars".</summary>
    public string Shops
    {
        get
        {
            var list = new List<string>();
            if ((ShopMask & 0x01) != 0) list.Add("Earth");
            if ((ShopMask & 0x02) != 0) list.Add("Mercury");
            if ((ShopMask & 0x04) != 0) list.Add("Venus");
            if ((ShopMask & 0x08) != 0) list.Add("Mars");
            return list.Count == 0 ? "—" : string.Join(", ", list);
        }
    }

    public string PriceText => GameFacts.FormatMoney(Price);

    public override string ToString() => IsEmptyRecord ? $"({Id}: unused)" : $"{Name} ({Id})";
}

/// <summary>
/// ITEMS.DAT (12,157 bytes = u16 count 187 + 187 × 65-byte records), baked from the shipped file so the
/// trainer can name inventory entries with no game folder present. The harness re-reads the real file
/// when one is found and compares every row.
/// </summary>
public static class ItemBook
{
    /// <summary>Records in ITEMS.DAT.</summary>
    public const int Count = 187;

    /// <summary>Bytes per ITEMS.DAT record.</summary>
    public const int RecordSize = 65;

    /// <summary>Type names, table at M.EXE DS:0x2AE3.</summary>
    public static readonly IReadOnlyList<string> TypeNames = new[]
    {
        "EQUIPMENT", "TOOL", "TRAVEL EQUIPMENT", "EXPLOSIVE", "PISTOL", "RIFLE", "SHOTGUN", "MELEE WEAPON",
        "MISSILE WEAPON", "MACHINE GUN", "MARTIAN ARTILLERY", "EUROPEAN ARTILLERY", "ARMOR",
        "GEOLOGICAL INVENTION", "BIOLOGICAL INVENTION", "AMMUNITION", "JEWELRY", "MANUSCRIPT", "TALISMAN",
        "PLANT", "ANIMAL HIDE", "IDENTIFICATION", "ANIMAL PRODUCT",
    };

    // Well-known ids (id = index + 1).
    public const int MinersHat = 11;
    public const int Lantern = 12;
    public const int FoulWeatherClothing = 9;
    public const int RoughLivingClothing = 10;
    public const int WaterBreather = 67;
    public const int Shot = 69;
    public const int Shell = 70;
    public const int Grapeshot = 71;
    public const int Shrapnel = 72;
    public const int Ammonia = 106;
    public const int ElectricLamp = 107;
    public const int GlowCrystal = 162;

    /// <summary>Every record, index 0 = id 1.</summary>
    public static readonly IReadOnlyList<ItemInfo> All = new ItemInfo[]
    {
        new(  1, "CONKLINS ATLAS",  0,      12,      8, 0x01,  0, 0,   0),
        new(  2, "DOCTORS BAG",  0,     720,    160, 0x01,  0, 0,   0),
        new(  3, "EDISONS ENCYCLOPEDIA",  0,      12,     16, 0x01,  0, 0,   0),
        new(  4, "NAVIGATION EQUIPMENT",  0,    2880,    128, 0x01,  0, 0,   0),
        new(  5, "ROBBS MEDICAL COMPANION",  0,      24,     16, 0x01,  0, 0,   0),
        new(  6, "LOCKPICK",  1,      12,      0, 0x01,  0, 0,   0),
        new(  7, "SHOVEL",  1,      24,     80, 0x09,  0, 0,   0),
        new(  8, "CAMPING OUTFIT",  2,     480,   1280, 0x01,  0, 0,   0),
        new(  9, "FOUL WEATHER CLOTHING",  2,      96,     48, 0x06,  0, 0,   0),
        new( 10, "ROUGH LIVING CLOTHING",  2,     240,     48, 0x0E,  0, 0,   0),
        new( 11, "MINERS HAT",  2,       8,      5, 0x09,  0, 0,   0),
        new( 12, "LANTERN",  2,      17,     32, 0x09,  0, 0,   0),
        new( 13, "ROPE",  2,      24,     80, 0x0D,  0, 0,   0),
        new( 14, "GUNPOWDER",  3,     120,     96, 0x09,  0, 0,   0),
        new( 15, "DYNAMITE",  3,      60,      8, 0x09,  0, 0,   0),
        new( 16, "SINGLE BARREL",  4,      96,     16, 0x09,  0, 2,  15),
        new( 17, "LIGHT REVOLVER",  4,     120,     24, 0x09,  6, 1,  10),
        new( 18, "HEAVY REVOLVER",  4,     480,     32, 0x09,  6, 1,  15),
        new( 19, "LT. MULTI BARREL PISTOL",  4,     240,     11, 0x09,  2, 1,   5),
        new( 20, "HVY. MULTI BARREL PISTOL",  4,     240,     32, 0x09,  4, 2,  15),
        new( 21, "BOLT ACTION RIFLE (LM)",  5,     480,    144, 0x09,  8, 2, 120),
        new( 22, "BOLT ACTION CARBINE (LM)",  5,     552,    128, 0x09,  8, 2,  90),
        new( 23, "BOLT ACTION RIFLE",  5,     480,    144, 0x09,  5, 2, 120),
        new( 24, "BOLT ACTION CARBINE",  5,     312,    128, 0x09,  5, 2,  90),
        new( 25, "LEVER ACTION RIFLE",  5,     510,    144, 0x09, 12, 1,  75),
        new( 26, "LEVER ACTION CARBINE",  5,     480,    128, 0x09,  6, 1,  45),
        new( 27, "BREECH LOADING RIFLE",  5,     480,    128, 0x09,  0, 2,  90),
        new( 28, "BREECH LOADING CARBINE",  5,     456,    120, 0x09,  0, 2,  60),
        new( 29, "MUZZLE LOADING RIFLE",  5,      96,    112, 0x09,  0, 2,  75),
        new( 30, "MUZZLE LOADING CARBINE",  5,      72,     96, 0x09,  0, 2,  45),
        new( 31, "SMOOTHBORE MUSKET",  5,      96,    128, 0x09,  0, 2,  45),
        new( 32, "SMOOTHBORE CARBINE",  5,      72,    112, 0x09,  0, 2,  30),
        new( 33, "HVY. DOUBLE RIFLE",  5,    2400,    168, 0x09,  2, 4, 65430),
        new( 34, "20-GAUGE DOUBLE",  6,     480,    112, 0x09,  2, 1,  30),
        new( 35, "12-GAUGE DOUBLE",  6,     720,    144, 0x09,  2, 1,  30),
        new( 36, "12-GAUGE SCATTERGUN",  6,    1200,     96, 0x09,  2, 1,  15),
        new( 37, "12-GAUGE LEVER ACTION",  6,    1200,    144, 0x09,  5, 1,  30),
        new( 38, "PIKE",  7,      32,     96, 0x04,  0, 2,   0),
        new( 39, "SPEAR",  7,      12,     32, 0x04,  0, 2,   0),
        new( 40, "GREAT SWORD",  7,    2400,     96, 0x04,  0, 1,   0),
        new( 41, "SWORD",  7,     480,     32, 0x04,  0, 1,   0),
        new( 42, "KNIFE",  7,      12,     16, 0x04,  0, 1,   0),
        new( 43, "MACHETE",  7,       8,     32, 0x04,  0, 1,   0),
        new( 44, "CLUB",  7,       0,     48, 0x04,  0, 1,   0),
        new( 45, "AXE",  7,      24,     48, 0x04,  0, 1,   0),
        new( 46, "HATCHET",  7,       6,     16, 0x04,  0, 2,   0),
        new( 47, "BOW AND ARROW",  8,     240,     32, 0x04,  0, 1,  30),
        new( 48, "JAVELIN",  8,      10,     32, 0x04,  0, 2,  10),
        new( 49, "THROWING KNIFE",  8,      12,      8, 0x04,  0, 1,   5),
        new( 50, "STONE",  8,       0,      1, 0x00,  0, 1,   5),
        new( 51, "GATLING 0.50",  9,    9600,   3200, 0x09, 36, 3, 300),
        new( 52, "GATLING 1-INCH",  9,   16800,   4000, 0x09, 18, 4, 300),
        new( 53, "MITRAILLEUS",  9,   14400,   4800, 0x09,  8, 3, 300),
        new( 54, "GARDNER",  9,   12000,    640, 0x09, 20, 3, 300),
        new( 55, "NORDENFELT 1-BARREL",  9,    4800,    240, 0x09, 15, 3, 300),
        new( 56, "MAXIM",  9,   36000,    640, 0x09, 50, 3, 300),
        new( 57, "DOUBLET", 12,      12,     32, 0x09,  0, 0,   0),
        new( 58, "SHOULDER SCALES", 12,      30,     32, 0x09,  0, 0,   0),
        new( 59, "MAIL", 12,     216,     64, 0x09,  0, 0,   0),
        new( 60, "BREAST PLATE", 12,     240,     96, 0x09,  0, 0,   0),
        new( 61, "HELMET", 12,     264,     32, 0x09,  0, 0,   0),
        new( 62, "SHIELD", 12,     144,     64, 0x09,  0, 0,   0),
        new( 63, "MINERAL DETECTOR", 13,   43392,  16000, 0x01,  0, 0,   0),
        new( 64, "SLEEP GAS", 14,     240,      1, 0x03,  0, 0,   0),
        new( 65, "ANTIBIOTIC", 14,     720,      1, 0x03,  0, 0,   0),
        new( 66, "STRENGTH ELIXER", 14,     480,      1, 0x02,  0, 0,   0),
        new( 67, "WATER BREATHER", 14,     144,     16, 0x09,  0, 0,   0),
        new( 68, "FOOD PILL", 14,     240,      1, 0x02,  0, 0,   0),
        new( 69, "SHOT", 15,       1,      0, 0x09,  0, 0,   0),
        new( 70, "SHELL", 15,       1,      0, 0x09,  0, 0,   0),
        new( 71, "GRAPESHOT", 15,       1,      0, 0x09,  0, 0,   0),
        new( 72, "SHRAPNEL", 15,       1,      0, 0x09,  0, 0,   0),
        new( 73, "MADNESS POTION", 14,     480,      1, 0x00,  0, 0,   0),
        new( 74, "AMULET OF SELDON", 18,   12000,      3, 0x00,  0, 0,   0),
        new( 75, "DETONITE",  3,     120,     16, 0x09,  0, 0,   0),
        new( 76, "POACHERS ID", 21,     120,      2, 0x00,  0, 0,   0),
        new( 77, "SKRILL PASS", 17,       0,      1, 0x00,  0, 0,   0),
        new( 78, "DAGGER OF WORM CULT",  7,      12,      0, 0x00,  0, 1,   0),
        new( 79, "MYCENEAN GOLD MASK", 16,   24000,     32, 0x00,  0, 0,   0),
        new( 80, "FEVER SERUM", 14,     480,      1, 0x00,  0, 0,   0),
        new( 81, "MOON-MAN WAR MASK", 16,   12000,     16, 0x00,  0, 0,   0),
        new( 82, "KAI BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new( 83, "LOPKAN BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new( 84, "KARKEM BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new( 85, "UCUZ BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new( 86, "PHOTHO BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new( 87, "FREEMERCHANTS DIARY", 17,     480,     32, 0x00,  0, 0,   0),
        new( 88, "SCROLLS OF THE ANCIENTS", 17,     600,     64, 0x00,  0, 0,   0),
        new( 89, "FREEMERCHANTS ID", 21,       0,      2, 0x00,  0, 0,   0),
        new( 90, "STONE TABLET", 17,     480,      1, 0x00,  0, 0,   0),
        new( 91, "",  0,       0,      0, 0x00,  0, 0,   0),
        new( 92, "TABLET OF SANCTUARY", 17,  120000,    160, 0x00,  0, 0,   0),
        new( 93, "TABLET OF MORTALS", 17,  120000,    160, 0x00,  0, 0,   0),
        new( 94, "POISON SERUM", 14,     480,      1, 0x00,  0, 0,   0),
        new( 95, "ATLANTIS INFO PARCHMENT", 17,    2400,      8, 0x00,  0, 0,   0),
        new( 96, "TALISMAN OF MANGLI DESH", 18,   24000,     32, 0x00,  0, 0,   0),
        new( 97, "STEPPE TIGER HIDE", 20,       0,    900, 0x00,  0, 0,   0),
        new( 98, "SPRITE PASSAGE", 17,     480,      8, 0x00,  0, 0,   0),
        new( 99, "WORM CULT KEY",  1,       0,      0, 0x00,  0, 0,   0),
        new(100, "THE EMERALD", 16,       0,      0, 0x00,  0, 0,   0),
        new(101, "WINE",  2,     480,      1, 0x00,  0, 0,   0),
        new(102, "GERMAN UNIFORMS",  2,      90,    480, 0x00,  0, 0,   0),
        new(103, "GERMAN HQ PASS", 17,     144,      3, 0x00,  0, 0,   0),
        new(104, "WORM CULT MAP", 17,    2400,      8, 0x00,  0, 0,   0),
        new(105, "PROPELLER", 13,  480000,   1600, 0x00,  0, 0,   0),
        new(106, "AMMONIA", 14,     480,      1, 0x00,  0, 0,   0),
        new(107, "ELECTRIC LAMP",  2,      12,     16, 0x00,  0, 0,   0),
        new(108, "MOON-MEN MEDALLION", 16,   24000,     32, 0x00,  0, 0,   0),
        new(109, "GLOWING YELLOW FUNGUS", 19,    1400,      6, 0x00,  0, 0,   0),
        new(110, "LONDON REPORT", 17,      12,      8, 0x00,  0, 0,   0),
        new(111, "MAPS OF EGYPT", 17,     480,      2, 0x00,  0, 0,   0),
        new(112, "RAVACHOLS WATCH", 16,    2400,      8, 0x00,  0, 0,   0),
        new(113, "TIN JUGGERNAUT BLUEPRINTS", 17,      12,      4, 0x00,  0, 0,   0),
        new(114, "ORCHID", 19,    1400,      6, 0x00,  0, 0,   0),
        new(115, "STEPPE TIGER BONE CANE", 18,  240000,    640, 0x00,  0, 0,   0),
        new(116, "MERCHANT SHIP BLUEPRINTS", 17,      12,      4, 0x00,  0, 0,   0),
        new(117, "PRIEST ROBES",  2,      90,    480, 0x00,  0, 0,   0),
        new(118, "VOLAACES BIBLE", 17,      12,      8, 0x00,  0, 0,   0),
        new(119, "LIFTWOOD",  0, 2400000,      0, 0x00,  0, 0,   0),
        new(120, "GUMME",  0,     480,      8, 0x00,  0, 0,   0),
        new(121, "OIL",  0,       1,      1, 0x00,  0, 0,   0),
        new(122, "JEWELS", 16,   24000,    160, 0x00,  0, 0,   0),
        new(123, "SPICE",  2,     288,      2, 0x00,  0, 0,   0),
        new(124, "CYTHERIAN ORCHID", 19,    1400,      6, 0x00,  0, 0,   0),
        new(125, "OMA JOLIMA", 19,    1400,      6, 0x00,  0, 0,   0),
        new(126, "HOMERS ILIAD", 17,    1200,      8, 0x00,  0, 0,   0),
        new(127, "ALFRED C. HOBBS MESSAGE", 17,    2400,      1, 0x00,  0, 0,   0),
        new(128, "HOBBS LOCKPICKS",  1,     216,      0, 0x00,  0, 0,   0),
        new(129, "CASTLE KEY",  1,       0,      0, 0x00,  0, 0,   0),
        new(130, "SHELL GLAND", 21,     120,      8, 0x00,  0, 0,   0),
        new(131, "CROWN", 16,   96000,     48, 0x00,  0, 1,   0),
        new(132, "SCALPEL",  7,     480,      2, 0x00,  0, 1,   0),
        new(133, "GOLD SHIELD", 12,   96000,     32, 0x00,  0, 0,   0),
        new(134, "BOGWEED", 19,      12,     16, 0x00,  0, 0,   0),
        new(135, "BHUTAN SPICE", 19,      12,     16, 0x00,  0, 0,   0),
        new(136, "STATUE OF GARUDA", 18,   24000,     32, 0x00,  0, 0,   0),
        new(137, "CIPHER BOOK", 17,       0,     16, 0x00,  0, 0,   0),
        new(138, "ARROW OF KAUNDINYA", 18,       0,     16, 0x00,  0, 0,   0),
        new(139, "STATUE OF QUEEN WILLOW LEAF", 18,       0,     64, 0x00,  0, 0,   0),
        new(140, "GALILEOS TELESCOPE",  0,   48000,    160, 0x00,  0, 0,   0),
        new(141, "SHAKESPEARIAN WORKS", 17,     240,     32, 0x00,  0, 0,   0),
        new(142, "",  0,       0,      0, 0x00,  0, 0,   0),
        new(143, "ALUMINUM FORMULA", 14,     120,      2, 0x00,  0, 0,   0),
        new(144, "LETTERS OF INTRODUCTION", 17,      12,      2, 0x00,  0, 0,   0),
        new(145, "BURNABYS MEDAL OF HONOR", 21,     120,      8, 0x00,  0, 0,   0),
        new(146, "SWORD OF URIAH TU",  7,   24000,    192, 0x00,  0, 3,   0),
        new(147, "GERMAN ID", 21,     120,      2, 0x00,  0, 0,   0),
        new(148, "KEY",  1,       0,      0, 0x00,  0, 0,   0),
        new(149, "GIANT MUSHROOM", 19,       0,      8, 0x00,  0, 0,   0),
        new(150, "RUBY CHALICE", 16,   96000,     48, 0x00,  0, 0,   0),
        new(151, "TORUK THE LOYALS BAJUY",  7,      24,     96, 0x00,  0, 2,   0),
        new(152, "TALISMAN", 18,   24000,     32, 0x00,  0, 0,   0),
        new(153, "TALISMAN", 18,   24000,     32, 0x00,  0, 0,   0),
        new(154, "TALISMAN", 18,   24000,     32, 0x00,  0, 0,   0),
        new(155, "INCENSE", 14,       0,      2, 0x00,  0, 0,   0),
        new(156, "VENOM OF FAUNA", 21,       0,      8, 0x00,  0, 0,   0),
        new(157, "FAZUCK SEAL", 21,   12000,    900, 0x00,  0, 0,   0),
        new(158, "FAZUCK SEAL", 21,   12000,    900, 0x00,  0, 0,   0),
        new(159, "BISON HIDE", 20,       0,    900, 0x00,  0, 0,   0),
        new(160, "RHINO HIDE", 20,       0,    900, 0x00,  0, 0,   1),
        new(161, "PAPER", 17,       0,      2, 0x00,  0, 0,   0),
        new(162, "GLOW CRYSTAL", 18,    2400,    160, 0x00,  0, 0,   0),
        new(163, "TUTS TREASURES", 16, 2400000,   1600, 0x00,  0, 0,   0),
        new(164, "KEY",  1,       0,      0, 0x00,  0, 0,   0),
        new(165, "FIRE JEWELS", 16,    4800,     16, 0x00,  0, 0,   0),
        new(166, "FIRE JEWELS", 16,    4800,     16, 0x00,  0, 0,   0),
        new(167, "",  0,       0,      0, 0x00,  0, 0,   0),
        new(168, "",  0,       0,      0, 0x00,  0, 0,   0),
        new(169, "",  0,       0,      0, 0x00,  0, 0,   0),
        new(170, "",  0,       0,      0, 0x00,  0, 0,   0),
        new(171, "SWEEPER", 10,   48000,  32000, 0x08,  0, 0, 100),
        new(172, "LIGHT GUN", 10,   96000,  64000, 0x08,  0, 0, 300),
        new(173, "HEAVY GUN", 10,  240000, 128000, 0x08,  0, 0, 400),
        new(174, "ROD GUN", 10,  192000,  96000, 0x08,  0, 0, 400),
        new(175, "ROGUE", 10,  480000, 192000, 0x08,  0, 0, 400),
        new(176, "1 POUNDER HRC", 11,   38400,   4800, 0x09,  0, 0, 400),
        new(177, "3 POUNDER HRC", 11,   43200,   6400, 0x09,  0, 0, 400),
        new(178, "6 POUNDER HRC", 11,   52800,  12800, 0x09,  0, 0, 600),
        new(179, "6 POUNDER RBL", 11,   48000,   9600, 0x09,  0, 0, 600),
        new(180, "9 POUNDER RBL", 11,   60000,  12800, 0x09,  0, 0, 600),
        new(181, "12 POUNDER RBL", 11,   72000,  16000, 0x09,  0, 0, 600),
        new(182, "15 POUNDER RBL", 11,   96000,  19200, 0x09,  0, 0, 600),
        new(183, "20 POUNDER RBL", 11,  120000,  25600, 0x09,  0, 0, 600),
        new(184, "40 POUNDER RBL", 11,  240000,  56000, 0x09,  0, 0, 800),
        new(185, "5 INCH HOWITZER", 11,  240000,  48000, 0x09,  0, 0, 800),
        new(186, "7PDR MTN HOWITZER", 11,   48000,   6400, 0x09,  0, 0, 600),
        new(187, "HALE ROCKET", 11,    1200,    320, 0x09,  0, 0, 600),
    };

    /// <summary>The record for an inventory id (1-based), or null for 0 / out of range.</summary>
    public static ItemInfo? ById(int id) => id >= 1 && id <= All.Count ? All[id - 1] : null;

    /// <summary>The record for a 0-based index, or null.</summary>
    public static ItemInfo? ByIndex(int index) => index >= 0 && index < All.Count ? All[index] : null;

    /// <summary>Display name for an inventory id: "(empty)" for 0, "#id" for an unknown one.</summary>
    public static string NameOf(int id) =>
        id == 0 ? "(empty)" : ById(id) is { IsEmptyRecord: false } item ? item.Name : $"#{id}";

    /// <summary>The four AMMUNITION records a firearm can load (loaded-ammo byte stores the 0-based index).</summary>
    public static IEnumerable<ItemInfo> AmmunitionTypes => All.Where(i => i.Kind == ItemType.Ammunition);

    /// <summary>Ship guns (types 10/11) for the flyer's gun mounts.</summary>
    public static IEnumerable<ItemInfo> ShipGuns => All.Where(i => i.IsShipGun);

    /// <summary>Items a player can hold: every non-empty record.</summary>
    public static IEnumerable<ItemInfo> Holdable => All.Where(i => !i.IsEmptyRecord);
}
