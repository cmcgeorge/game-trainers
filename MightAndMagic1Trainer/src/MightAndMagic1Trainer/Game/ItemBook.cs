namespace MightAndMagic1Trainer.Game;

/// <summary>
/// One entry of the game's internal item table: a piece of equipment, an accessory, or a
/// consumable. The fields are extracted verbatim from <c>MM.EXE</c>'s 255-entry item table
/// (255 × 24-byte records; the name is each record's first 14 bytes, then cost/damage/bonus/
/// charges). <see cref="Id"/> is the 1-based table index — exactly the byte the game stores
/// in a character's equipped/backpack slot (0 = empty slot, so it has no entry here).
/// </summary>
public sealed record GameItem(byte Id, string Name, int Cost, int Damage, int Bonus, int Charges)
{
    public string Category => ItemBook.ItemCategory(Id);

    private bool IsWeapon => Id is >= 1 and <= 120;       // 1-handed, missile, 2-handed
    private bool IsArmor => Id is >= 121 and <= 170;      // body armor + shields

    /// <summary>Short stat tag shown beside the name (damage for weapons, AC for armor).</summary>
    public string StatText =>
        IsWeapon ? $"Dmg {Damage}" + (Bonus > 0 ? $" (+{Bonus})" : "")
        : IsArmor ? $"AC +{Bonus}"
        : "";

    public string CostText => Cost > 0 ? $"{Cost:N0} gp" : "—";

    /// <summary>One-line reference detail: id, category, stat tag and charge count.</summary>
    public string DetailText
    {
        get
        {
            var parts = new List<string> { $"#{Id}", Category };
            if (StatText.Length > 0) parts.Add(StatText);
            if (Charges > 0) parts.Add($"{Charges} charges");
            return string.Join("  ·  ", parts);
        }
    }
}

/// <summary>
/// The complete Might &amp; Magic 1 item table (ids 1..255), extracted from <c>MM.EXE</c> and
/// cross-checked against the bundled <c>docs/Roster.dta</c> (every character's equipped bytes
/// resolve to sensible, class-appropriate gear). Also derives the inventory picker list and the
/// enhancement (+1/+2/+3) families used by the Inventory tab. Two entries (57, 151) carry the
/// game's own placeholder name "X!XX!X'S …" and are preserved as-is.
/// </summary>
public static class ItemBook
{
    private static GameItem I(byte id, string name, int cost, int dmg, int bonus, int charges) =>
        new(id, name, cost, dmg, bonus, charges);

    /// <summary>Every item, id 1..255, in id order (which is also category order).</summary>
    public static readonly IReadOnlyList<GameItem> Catalog = new[]
    {
        I(  1, "CLUB", 1, 3, 0, 0),
        I(  2, "DAGGER", 5, 4, 0, 0),
        I(  3, "HAND AXE", 10, 5, 0, 0),
        I(  4, "SPEAR", 15, 6, 0, 0),
        I(  5, "SHORT SWORD", 20, 6, 0, 0),
        I(  6, "MACE", 40, 6, 0, 0),
        I(  7, "FLAIL", 40, 7, 0, 0),
        I(  8, "SCIMITAR", 40, 7, 0, 0),
        I(  9, "BROAD SWORD", 50, 7, 0, 0),
        I( 10, "BATTLE AXE", 60, 0, 0, 0),   // damage byte is genuinely 0 in MM.EXE (a quirk of the original data; its +1/+2 are 8)
        I( 11, "LONG SWORD", 60, 8, 0, 0),
        I( 12, "CLUB +1", 30, 3, 1, 0),
        I( 13, "CLUB +2", 100, 3, 2, 0),
        I( 14, "DAGGER +1", 50, 4, 1, 0),
        I( 15, "HAND AXE +1", 75, 5, 1, 0),
        I( 16, "SPEAR +1", 100, 6, 1, 0),
        I( 17, "SHORT SWORD +1", 100, 6, 1, 0),
        I( 18, "MACE +1", 125, 6, 1, 0),
        I( 19, "FLAIL +1", 200, 7, 1, 0),
        I( 20, "SCIMITAR +1", 250, 7, 1, 0),
        I( 21, "BROAD SWORD +1", 300, 7, 1, 0),
        I( 22, "BATTLE AXE +1", 300, 8, 1, 0),
        I( 23, "LONG SWORD +1", 300, 8, 1, 0),
        I( 24, "FLAMING CLUB", 500, 3, 3, 30),
        I( 25, "CLUB OF NOISE", 100, 3, 0, 0),
        I( 26, "DAGGER +2", 200, 4, 2, 25),
        I( 27, "HAND AXE +2", 225, 5, 2, 0),
        I( 28, "SPEAR +2", 250, 6, 2, 0),
        I( 29, "SHORT SWORD +2", 300, 6, 2, 15),
        I( 30, "MACE +2", 325, 6, 2, 10),
        I( 31, "FLAIL +2", 350, 7, 2, 15),
        I( 32, "SCIMITAR +2", 400, 7, 2, 0),
        I( 33, "BROAD SWORD +2", 400, 7, 2, 0),
        I( 34, "BATTLE AXE +2", 500, 8, 2, 10),
        I( 35, "LONG SWORD +2", 550, 8, 2, 10),
        I( 36, "ROYAL DAGGER", 2500, 4, 0, 0),
        I( 37, "DAGGER OF MIND", 750, 4, 3, 20),
        I( 38, "DIAMOND DAGGER", 800, 10, 4, 0),
        I( 39, "ELECTRIC SPEAR", 1200, 6, 3, 16),
        I( 40, "HOLY MACE", 2000, 6, 4, 5),
        I( 41, "UN-HOLY MACE", 2000, 6, 4, 5),
        I( 42, "DARK FLAIL", 600, 3, 0, 10),
        I( 43, "FLAIL OF FEAR", 1600, 7, 3, 8),
        I( 44, "LUCKY SCIMITAR", 2200, 7, 4, 0),
        I( 45, "MACE OF UNDEAD", 500, 6, 0, 5),
        I( 46, "COLD AXE", 2500, 8, 3, 10),
        I( 47, "ELECTRIC SWORD", 2200, 8, 3, 10),
        I( 48, "FLAMING SWORD", 2200, 8, 3, 10),
        I( 49, "SWORD OF MIGHT", 8000, 8, 5, 30),
        I( 50, "SWORD OF SPEED", 7000, 8, 5, 20),
        I( 51, "SHARP SWORD", 6500, 10, 4, 5),
        I( 52, "ACCURATE SWORD", 6500, 8, 6, 10),
        I( 53, "SWORD OF MAGIC", 10000, 8, 5, 15),
        I( 54, "IMMORTAL SWORD", 7000, 8, 4, 25),
        I( 55, "AXE PROTECTOR", 8000, 8, 5, 15),
        I( 56, "AXE DESTROYER", 8000, 8, 5, 6),
        I( 57, "X!XX!X'S SWORD", 6000, 8, 4, 10),
        I( 58, "ADAMANTINE AXE", 12000, 8, 5, 5),
        I( 59, "ULTIMATE SWORD", 15000, 20, 6, 20),
        I( 60, "ELEMENT SWORD", 12000, 8, 5, 10),
        I( 61, "SLING", 10, 4, 0, 0),
        I( 62, "CROSSBOW", 50, 6, 0, 0),
        I( 63, "SHORT BOW", 75, 8, 0, 0),
        I( 64, "LONG BOW", 100, 10, 0, 0),
        I( 65, "GREAT BOW", 250, 12, 0, 0),
        I( 66, "SLING +1", 50, 4, 1, 0),
        I( 67, "CROSSBOW +1", 250, 6, 1, 0),
        I( 68, "SHORT BOW +1", 375, 8, 1, 0),
        I( 69, "LONG BOW +1", 500, 10, 1, 0),
        I( 70, "GREAT BOW +1", 1250, 12, 1, 0),
        I( 71, "MAGIC SLING", 800, 4, 3, 10),
        I( 72, "CROSSBOW +2", 1000, 6, 2, 0),
        I( 73, "SHORT BOW +2", 1000, 8, 2, 0),
        I( 74, "LONG BOW +2", 1200, 10, 2, 0),
        I( 75, "GREAT BOW +2", 2000, 12, 2, 0),
        I( 76, "CROSSBOW LUCK", 2000, 6, 3, 20),
        I( 77, "CROSSBOW SPEED", 2000, 6, 3, 10),
        I( 78, "LIGHTNING BOW", 3000, 10, 3, 10),
        I( 79, "FLAMING BOW", 3000, 10, 3, 10),
        I( 80, "GIANT'S BOW", 2000, 20, 3, 0),
        I( 81, "THE MAGIC BOW", 6000, 16, 4, 5),
        I( 82, "BOW OF POWER", 6000, 16, 4, 15),
        I( 83, "ROBBER'S X-BOW", 8000, 10, 5, 10),
        I( 84, "ARCHER'S BOW", 12000, 20, 5, 10),
        I( 85, "OBSIDIAN BOW", 2000, 3, 0, 3),
        I( 86, "STAFF", 30, 8, 0, 0),
        I( 87, "GLAIVE", 80, 10, 0, 0),
        I( 88, "BARDICHE", 80, 10, 0, 0),
        I( 89, "HALBERD", 100, 12, 0, 0),
        I( 90, "GREAT HAMMER", 150, 12, 0, 0),
        I( 91, "GREAT AXE", 150, 12, 0, 0),
        I( 92, "FLAMBERGE", 250, 14, 0, 0),
        I( 93, "STAFF +1", 200, 8, 1, 0),
        I( 94, "GLAIVE +1", 350, 10, 1, 0),
        I( 95, "BARDICHE +1", 350, 10, 1, 0),
        I( 96, "HALBERD +1", 500, 12, 1, 0),
        I( 97, "GREAT HAMMER+1", 550, 12, 1, 0),
        I( 98, "GREAT AXE +1", 500, 12, 1, 0),
        I( 99, "FLAMBERGE +1", 600, 14, 1, 0),
        I(100, "STAFF +2", 600, 8, 2, 10),
        I(101, "GLAIVE +2", 900, 10, 2, 0),
        I(102, "BARDICHE +2", 900, 10, 2, 0),
        I(103, "HALBERD +2", 1200, 12, 2, 20),
        I(104, "GREAT HAMMER+2", 1200, 12, 2, 20),
        I(105, "GREAT AXE +2", 1200, 12, 2, 10),
        I(106, "FLAMBERGE +2", 2000, 14, 2, 10),
        I(107, "STAFF OF LIGHT", 1500, 8, 3, 20),
        I(108, "COLD GLAIVE", 2500, 10, 3, 20),
        I(109, "CURING STAFF", 2500, 8, 3, 12),
        I(110, "MINOTAUR'S AXE", 2000, 3, 0, 0),
        I(111, "THUNDER HAMMER", 3500, 12, 4, 15),
        I(112, "GREAT AXE +3", 3500, 12, 3, 10),
        I(113, "FLAMBERGE +3", 5000, 14, 3, 10),
        I(114, "SORCERER STAFF", 8000, 8, 5, 10),
        I(115, "STAFF OF MAGIC", 5000, 8, 4, 10),
        I(116, "DEMON'S GLAIVE", 10000, 10, 5, 40),
        I(117, "DEVIL'S GLAIVE", 10000, 10, 5, 40),
        I(118, "THE FLAMBERGE", 15000, 30, 6, 10),
        I(119, "HOLY FLAMBERGE", 20000, 20, 6, 15),
        I(120, "EVIL FLAMBERGE", 20000, 20, 6, 15),
        I(121, "PADDED ARMOR", 10, 0, 1, 0),
        I(122, "LEATHER ARMOR", 20, 0, 2, 0),
        I(123, "SCALE ARMOR", 50, 0, 3, 0),
        I(124, "RING MAIL", 100, 0, 4, 0),
        I(125, "CHAIN MAIL", 200, 0, 5, 0),
        I(126, "SPLINT MAIL", 400, 0, 6, 0),
        I(127, "PLATE MAIL", 1000, 0, 7, 0),
        I(128, "PADDED +1", 25, 0, 2, 0),
        I(129, "LEATHER +1", 60, 0, 3, 0),
        I(130, "SCALE +1", 120, 0, 4, 0),
        I(131, "RING MAIL +1", 250, 0, 5, 0),
        I(132, "CHAIN MAIL +1", 500, 0, 6, 0),
        I(133, "SPLINT MAIL +1", 1000, 0, 7, 0),
        I(134, "PLATE MAIL +1", 2500, 0, 8, 0),
        I(135, "LEATHER +2", 150, 0, 4, 0),
        I(136, "SCALE +2", 300, 0, 5, 0),
        I(137, "RING MAIL +2", 750, 0, 6, 0),
        I(138, "CHAIN MAIL +2", 1500, 0, 7, 0),
        I(139, "SPLINT MAIL +2", 2500, 0, 8, 0),
        I(140, "PLATE MAIL +2", 7500, 0, 9, 0),
        I(141, "BRACERS AC 4", 1000, 0, 4, 0),
        I(142, "RING MAIL +3", 2000, 0, 7, 0),
        I(143, "CHAIN MAIL +3", 4500, 0, 8, 0),
        I(144, "SPLINT MAIL +3", 7500, 0, 9, 0),
        I(145, "PLATE MAIL +3", 15000, 0, 10, 0),
        I(146, "BRACERS AC 6", 2500, 0, 6, 20),
        I(147, "CHAIN MAIL +3", 4500, 0, 0, 0),
        I(148, "BRACERS AC 8", 7500, 0, 0, 0),
        I(149, "BLUE RING MAIL", 10000, 0, 9, 30),
        I(150, "RED CHAIN MAIL", 15000, 0, 10, 30),
        I(151, "X!XX!X'S PLATE", 18000, 0, 11, 10),
        I(152, "HOLY PLATE", 25000, 0, 12, 30),
        I(153, "UN-HOLY PLATE", 25000, 0, 12, 30),
        I(154, "ULTIMATE PLATE", 30000, 0, 13, 30),
        I(155, "BRACERS AC 8", 7500, 0, 8, 40),
        I(156, "SMALL SHIELD", 10, 0, 1, 0),
        I(157, "LARGE SHIELD", 50, 0, 2, 0),
        I(158, "SILVER SHIELD", 100, 0, 2, 0),
        I(159, "SMALL SHIELD+1", 100, 0, 2, 0),
        I(160, "LARGE SHIELD+1", 200, 0, 3, 0),
        I(161, "LARGE SHIELD+1", 200, 0, 0, 0),
        I(162, "SMALL SHIELD+2", 400, 0, 3, 0),
        I(163, "LARGE SHIELD+2", 800, 0, 4, 0),
        I(164, "LARGE SHIELD+2", 800, 0, 0, 0),
        I(165, "FIRE SHIELD", 2500, 0, 5, 0),
        I(166, "COLD SHIELD", 2500, 0, 5, 0),
        I(167, "ELEC SHIELD", 2500, 0, 5, 0),
        I(168, "ACID SHIELD", 2500, 0, 5, 0),
        I(169, "MAGIC SHIELD", 5000, 0, 6, 20),
        I(170, "DRAGON SHIELD", 8000, 0, 7, 20),
        I(171, "ROPE & HOOKS", 10, 0, 0, 30),
        I(172, "TORCH", 2, 0, 0, 1),
        I(173, "LANTERN", 20, 0, 0, 10),
        I(174, "10 FOOT POLE", 10, 0, 0, 0),
        I(175, "GARLIC", 5, 0, 0, 0),
        I(176, "WOLFSBANE", 10, 0, 0, 0),
        I(177, "BELLADONNA", 25, 0, 0, 0),
        I(178, "MAGIC HERBS", 50, 0, 0, 3),
        I(179, "DRIED BEEF", 40, 0, 0, 3),
        I(180, "ROBBER'S TOOLS", 150, 0, 0, 0),
        I(181, "BAG OF SILVER", 300, 0, 0, 0),
        I(182, "AMBER GEM", 500, 0, 0, 0),
        I(183, "SMELLING SALT", 50, 0, 0, 3),
        I(184, "BAG OF SAND", 100, 0, 0, 5),
        I(185, "MIGHT POTION", 200, 0, 0, 3),
        I(186, "SPEED POTION", 200, 0, 0, 3),
        I(187, "SUNDIAL", 500, 0, 0, 50),
        I(188, "CURING POTION", 350, 0, 0, 4),
        I(189, "MAGIC POTION", 500, 0, 0, 2),
        I(190, "DEFENSE RING", 500, 0, 0, 30),
        I(191, "BAG OF GARBAGE", 100, 0, 0, 0),
        I(192, "SCROLL OF FIRE", 300, 0, 0, 1),
        I(193, "FLYING CARPET", 500, 0, 0, 10),
        I(194, "JADE AMULET", 600, 0, 0, 0),
        I(195, "ANTIDOTE BREW", 500, 0, 0, 2),
        I(196, "SKILL POTION", 600, 0, 0, 5),
        I(197, "BOOTS OF SPEED", 800, 0, 0, 10),
        I(198, "LUCKY CHARM", 800, 0, 0, 20),
        I(199, "WAND OF FIRE", 1000, 0, 0, 10),
        I(200, "UNDEAD AMULET", 800, 0, 0, 20),
        I(201, "SILENT CHIME", 400, 0, 0, 20),
        I(202, "BELT OF POWER", 600, 0, 0, 0),
        I(203, "MODEL BOAT", 400, 0, 0, 15),
        I(204, "DEFENSE CLOAK", 700, 0, 0, 0),
        I(205, "KNOWLEDGE BOOK", 1000, 0, 0, 4),
        I(206, "RUBY IDOL", 3000, 0, 0, 0),
        I(207, "SORCERER ROBE", 2500, 0, 0, 20),
        I(208, "POWER GAUNTLET", 3000, 0, 0, 0),
        I(209, "CLERIC'S BEADS", 3000, 0, 0, 50),
        I(210, "HORN OF DEATH", 2500, 0, 0, 10),
        I(211, "POTION OF LIFE", 1500, 0, 0, 2),
        I(212, "SHINY PENDANT", 2000, 0, 0, 10),
        I(213, "LIGHTNING WAND", 1500, 0, 0, 10),
        I(214, "PRECISION RING", 3000, 0, 0, 0),
        I(215, "RETURN SCROLL", 2000, 0, 0, 1),
        I(216, "TELEPORT HELM", 5000, 0, 0, 20),
        I(217, "YOUTH POTION", 4000, 0, 0, 2),
        I(218, "BELLS OF TIME", 1000, 0, 0, 50),
        I(219, "MAGIC OIL", 3000, 0, 0, 1),
        I(220, "MAGIC VEST", 6000, 0, 0, 10),
        I(221, "DESTROYER WAND", 7000, 0, 0, 10),
        I(222, "ELEMENT SCARAB", 6000, 0, 0, 20),
        I(223, "SUN SCROLL", 3000, 0, 0, 1),
        I(224, "STAR RUBY", 6000, 0, 0, 30),
        I(225, "STAR SAPPHIRE", 6000, 0, 0, 10),
        I(226, "WEALTH CHEST", 6000, 0, 0, 5),
        I(227, "GEM SACK", 10000, 0, 0, 10),
        I(228, "DIAMOND COLLAR", 10000, 0, 0, 10),
        I(229, "FIRE OPAL", 10000, 0, 0, 10),
        I(230, "UNOBTAINIUM", 50000, 0, 0, 0),
        I(231, "VELLUM SCROLL", 10, 0, 0, 0),
        I(232, "RUBY WHISTLE", 500, 0, 0, 200),
        I(233, "KINGS PASS", 0, 0, 0, 0),
        I(234, "MERCHANTS PASS", 0, 0, 0, 0),
        I(235, "CRYSTAL KEY", 1000, 0, 0, 10),
        I(236, "CORAL KEY", 300, 0, 0, 10),
        I(237, "BRONZE KEY", 500, 0, 0, 20),
        I(238, "SILVER KEY", 600, 0, 0, 30),
        I(239, "GOLD KEY", 800, 0, 0, 15),
        I(240, "DIAMOND KEY", 2000, 0, 0, 20),
        I(241, "CACTUS NECTAR", 400, 0, 0, 10),
        I(242, "MAP OF DESERT", 400, 0, 0, 20),
        I(243, "LASER BLASTER", 2000, 0, 0, 10),
        I(244, "DRAGONS TOOTH", 1500, 0, 0, 10),
        I(245, "WYVERN EYE", 1000, 0, 0, 20),
        I(246, "MEDUSA HEAD", 0, 0, 0, 0),
        I(247, "RING OF OKRIM", 3000, 0, 0, 20),
        I(248, "B QUEEN IDOL", 0, 0, 0, 0),
        I(249, "W QUEEN IDOL", 0, 0, 0, 0),
        I(250, "PIRATES MAP A", 1000, 0, 0, 0),
        I(251, "PIRATES MAP B", 2000, 0, 0, 0),
        I(252, "THUNDRANIUM", 10000, 0, 0, 250),
        I(253, "KEY CARD", 0, 0, 0, 0),
        I(254, "EYE OF GOROS", 10000, 0, 0, 20),
        I(255, "(USELESS ITEM)", 0, 0, 0, 0),
    };

    /// <summary>id → name (ItemNames[0] is id 1). Kept for callers that only need the label.</summary>
    public static readonly IReadOnlyList<string> ItemNames =
        Catalog.Select(c => c.Name).ToArray();

    /// <summary>The item with the given id, or null for 0 / out-of-range.</summary>
    public static GameItem? Get(int id) =>
        id >= 1 && id <= Catalog.Count ? Catalog[id - 1] : null;

    /// <summary>Friendly name for an item-id byte (0 = empty slot; unknown ids fall back to the number).</summary>
    public static string ItemName(int id) =>
        id == 0 ? "(empty)" : Get(id)?.Name ?? $"? (id {id})";

    /// <summary>Category bucket for an item id, matching the game's internal id ranges.</summary>
    public static string ItemCategory(int id) => id switch
    {
        0 => "Empty",
        >= 1 and <= 60 => "1-handed weapons",
        >= 61 and <= 85 => "Missile weapons",
        >= 86 and <= 120 => "2-handed weapons",
        >= 121 and <= 155 => "Body armor",
        >= 156 and <= 170 => "Shields",
        _ => "Misc & special",
    };

    // ===== Inventory picker =================================================================

    /// <summary>One selectable entry for the inventory item picker.</summary>
    public sealed record ItemChoice(byte Id, string Name, string Category)
    {
        /// <summary>List label, e.g. "018  MACE +1" (or "(empty)" for id 0).</summary>
        public string Display => Id == 0 ? "(empty)" : $"{Id:D3}  {Name}";
    }

    /// <summary>All 256 inventory picks (id 0 = empty, then 1..255), in id order.</summary>
    public static readonly IReadOnlyList<ItemChoice> Choices = BuildChoices();

    private static ItemChoice[] BuildChoices()
    {
        var list = new ItemChoice[Catalog.Count + 1];
        list[0] = new ItemChoice(0, "(empty)", "Empty");
        foreach (var it in Catalog)
            list[it.Id] = new ItemChoice(it.Id, it.Name, it.Category);
        return list;
    }

    // ===== Enhancement (+1/+2/+3) families ==================================================
    //
    // MM1 has no separate "enchantment" byte: "MACE", "MACE +1" and "MACE +2" are three
    // distinct item ids. To let the UI change just the enhancement, items are grouped into
    // families that share a base. The base is the name with any trailing "+N" stripped; three
    // armours are abbreviated when enchanted ("PADDED +1" → base "PADDED ARMOR"), so those are
    // aliased back. Named uniques (e.g. "FLAMING CLUB") have no "+N" and form singleton families.

    private static readonly Dictionary<string, string> BaseAlias = new()
    {
        ["PADDED"] = "PADDED ARMOR", ["LEATHER"] = "LEATHER ARMOR", ["SCALE"] = "SCALE ARMOR",
    };

    /// <summary>One enhancement level available for an item's family.</summary>
    public sealed record EnhancementOption(int Plus, byte Id)
    {
        public string Label => Plus == 0 ? "Base" : $"+{Plus}";
    }

    // baseKey -> (plus -> representative id), built once from the catalog.
    private static readonly Dictionary<string, SortedDictionary<int, byte>> Families = BuildFamilies();

    private static (string baseKey, int plus) Split(string name)
    {
        int plus = 0;
        string stem = name;
        int n = name.Length;
        if (n >= 2 && char.IsDigit(name[n - 1]) && name[n - 2] == '+')
        {
            plus = name[n - 1] - '0';
            stem = name[..(n - 2)].TrimEnd();   // drop "+N" and any space before it
        }
        return (BaseAlias.TryGetValue(stem, out var full) ? full : stem, plus);
    }

    private static Dictionary<string, SortedDictionary<int, byte>> BuildFamilies()
    {
        var fam = new Dictionary<string, SortedDictionary<int, byte>>();
        foreach (var it in Catalog)
        {
            var (key, plus) = Split(it.Name);
            if (!fam.TryGetValue(key, out var levels))
                fam[key] = levels = new SortedDictionary<int, byte>();
            // First id wins for a given plus (handful of duplicate names share a level).
            if (!levels.ContainsKey(plus)) levels[plus] = it.Id;
        }
        return fam;
    }

    private static SortedDictionary<int, byte>? FamilyOf(int id)
    {
        var item = Get(id);
        return item != null && Families.TryGetValue(Split(item.Name).baseKey, out var levels) ? levels : null;
    }

    /// <summary>The enhancement levels selectable for an item's family (empty/single for uniques).</summary>
    public static IReadOnlyList<EnhancementOption> EnhancementsFor(int id)
    {
        var levels = FamilyOf(id);
        if (levels == null) return Array.Empty<EnhancementOption>();
        return levels.Select(kv => new EnhancementOption(kv.Key, kv.Value)).ToArray();
    }

    /// <summary>The enhancement level (+N; 0 = base) of an item id.</summary>
    public static int PlusOf(int id) => Get(id) is { } it ? Split(it.Name).plus : 0;

    /// <summary>The id of the same-family item at a given enhancement level, or null if none.</summary>
    public static byte? VariantId(int id, int plus) =>
        FamilyOf(id) is { } levels && levels.TryGetValue(plus, out var vid) ? vid : null;
}
