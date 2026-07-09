namespace MightAndMagic1Trainer.Game;

/// <summary>
/// Per-item "special" data — class/alignment usability and the equip effect (elemental
/// resistance, attribute bonus, armour-class bonus, thievery, or curse) — surfaced as the
/// descriptive second line on the Items reference tab. MM1 has no prose item descriptions; this
/// is the game's own structured effect data, which lives in the same MM.EXE item records as
/// <see cref="ItemBook"/>'s base table but was not part of the verbatim cost/damage extraction.
/// It is transcribed from Andrew Schultz's <i>Might &amp; Magic I</i> item FAQ (Apple IIe data)
/// and joined to <see cref="ItemBook"/> by name: 254 of the 255 ids matched exactly. The single
/// gap, OBSIDIAN BOW (id 85), is absent from that list and so has no effect entry here — callers
/// get an empty description for it.
/// </summary>
public static class ItemEffectBook
{
    /// <summary>One item's usability mask and equip effect, as transcribed from the FAQ.</summary>
    /// <param name="UsedBy">8-char class/alignment mask: "RSCAPK" then "EG" (a space = not allowed).</param>
    /// <param name="Special">Effect tag: EQUIP/NONE/CURSE, a resistance, an attribute, AC+, or THIEF.</param>
    /// <param name="Amount">Effect magnitude: resistance %, attribute / AC / thievery bonus, etc.</param>
    public sealed record ItemEffect(string UsedBy, string Special, int Amount);

    private static (byte id, string used, string special, int amt) E(byte id, string used, string special, int amt)
        => (id, used, special, amt);

    // Transcribed from the FAQ's item table and joined to ItemBook by name (id 85 OBSIDIAN BOW is
    // absent from the FAQ and intentionally omitted). The "used" string is the raw 8-char mask.
    private static readonly IReadOnlyDictionary<byte, ItemEffect> Effects = new[]
    {
        E(  1, "RSCAPKEG", "EQUIP", 0),
        E(  2, "RS APKEG", "EQUIP", 0),
        E(  3, "R  APKEG", "EQUIP", 0),
        E(  4, "   APKEG", "EQUIP", 0),
        E(  5, "R  APKEG", "EQUIP", 0),
        E(  6, "R CAPKEG", "EQUIP", 0),
        E(  7, "R CAPKEG", "EQUIP", 0),
        E(  8, "R  APKEG", "EQUIP", 0),
        E(  9, "R  APKEG", "EQUIP", 0),
        E( 10, "R  APKEG", "EQUIP", 0),
        E( 11, "R  APKEG", "EQUIP", 0),
        E( 12, "RSCAPKEG", "EQUIP", 0),
        E( 13, "RSCAPKEG", "EQUIP", 0),
        E( 14, "RS APKEG", "EQUIP", 0),
        E( 15, "R  APKE ", "LUCK", 1),
        E( 16, "   APK G", "LUCK", 1),
        E( 17, "R  APKEG", "EQUIP", 0),
        E( 18, "R CAPKEG", "EQUIP", 0),
        E( 19, "R CAPKEG", "EQUIP", 0),
        E( 20, "R  APK G", "LUCK", 2),
        E( 21, "R  APKE ", "LUCK", 2),
        E( 22, "R  APKEG", "EQUIP", 0),
        E( 23, "R  APKEG", "EQUIP", 0),
        E( 24, "RSCAPKEG", "FIRE", 20),
        E( 25, "RSCAPKEG", "CURSE", 0),
        E( 26, "RS APKEG", "EQUIP", 0),
        E( 27, "R  APK G", "LUCK", 2),
        E( 28, "   APKE ", "LUCK", 2),
        E( 29, "R  APKEG", "EQUIP", 0),
        E( 30, "R CAPKEG", "PERS", 1),
        E( 31, "R CAPKEG", "PERS", 1),
        E( 32, "R  APKE ", "MIGHT", 1),
        E( 33, "R  APK G", "MIGHT", 1),
        E( 34, "R  APKEG", "FIRE", 20),
        E( 35, "R  APKEG", "ELEM", 20),
        E( 36, "RS APK  ", "EQUIP", 0),
        E( 37, " S    EG", "INT", 3),
        E( 38, " S    EG", "MIGHT", 4),
        E( 39, "   APKEG", "ELEC", 40),
        E( 40, "  C    G", "PERS", 3),
        E( 41, "  C   E ", "PERS", 3),
        E( 42, "  CAPKE ", "CURSE", 0),
        E( 43, "  C   EG", "FEAR", 40),
        E( 44, "R  APKEG", "LUCK", 5),
        E( 45, "  CAPK G", "CURSE", 0),
        E( 46, "    PKEG", "COLD", 40),
        E( 47, "   APKEG", "ELEC", 40),
        E( 48, "   APKEG", "FIRE", 50),
        E( 49, "     KEG", "MIGHT", 6),
        E( 50, "   APKEG", "SPEED", 6),
        E( 51, "    PKE ", "MAGIC", 21),
        E( 52, "   APK G", "ACCY", 6),
        E( 53, "R  APKEG", "MAGIC", 30),
        E( 54, "R  APK G", "LUCK", 5),
        E( 55, "   APKEG", "MAGIC", 25),
        E( 56, "    PKE ", "MIGHT", 4),
        E( 57, "R  APK  ", "LUCK", 15),
        E( 58, "   APKEG", "LUCK", 8),
        E( 59, "R  APKEG", "MIGHT", 10),
        E( 60, "R  APKEG", "MAGIC", 25),
        E( 61, "R  APKEG", "EQUIP", 0),
        E( 62, "R  APKEG", "EQUIP", 0),
        E( 63, "   APKEG", "EQUIP", 0),
        E( 64, "   APKEG", "EQUIP", 0),
        E( 65, "   APKEG", "EQUIP", 0),
        E( 66, "R  APKEG", "EQUIP", 0),
        E( 67, "R  APKEG", "EQUIP", 0),
        E( 68, "   APKEG", "EQUIP", 0),
        E( 69, "   APKEG", "EQUIP", 0),
        E( 70, "   APKEG", "EQUIP", 0),
        E( 71, "R  APKEG", "MAGIC", 10),
        E( 72, "R  A  EG", "ACCY", 2),
        E( 73, "   APKE ", "HOLY", 10),
        E( 74, "   APK G", "HOLY", 10),
        E( 75, "   APKEG", "FEAR", 30),
        E( 76, "R  A  EG", "LUCK", 3),
        E( 77, "R  APKEG", "SPEED", 4),
        E( 78, "   APK G", "ELEC", 20),
        E( 79, "   APKE ", "FIRE", 20),
        E( 80, "   APKEG", "EQUIP", 0),
        E( 81, "   APK G", "MAGIC", 20),
        E( 82, "   APKE ", "FEAR", 40),
        E( 83, "R     EG", "SPEED", 4),
        E( 84, "   A  EG", "ACCY", 5),
        E( 86, " SCAPKEG", "EQUIP", 0),
        E( 87, "   APKEG", "EQUIP", 0),
        E( 88, "   APKEG", "EQUIP", 0),
        E( 89, "   APKEG", "EQUIP", 0),
        E( 90, "  CAPKEG", "EQUIP", 0),
        E( 91, "   APKEG", "EQUIP", 0),
        E( 92, "   APKEG", "EQUIP", 0),
        E( 93, " SCAPKEG", "INT", 1),
        E( 94, "   APKE ", "SPEED", 1),
        E( 95, "   APK G", "SPEED", 1),
        E( 96, "   APKEG", "EQUIP", 0),
        E( 97, "  CAPKEG", "PERS", 1),
        E( 98, "   APKEG", "EQUIP", 0),
        E( 99, "   APKEG", "EQUIP", 0),
        E(100, " SCAPKEG", "LUCK", 2),
        E(101, "   APKE ", "SPEED", 2),
        E(102, "   APK G", "SPEED", 2),
        E(103, "   APKEG", "SPEED", 3),
        E(104, "  CAPKEG", "PERS", 2),
        E(105, "   APKEG", "MIGHT", 2),
        E(106, "   APKEG", "MIGHT", 2),
        E(107, " SCAPKEG", "HOLY", 40),
        E(108, "   APKE ", "COLD", 40),
        E(109, " SC    G", "POISN", 30),
        E(110, "   APKEG", "CURSE", 0),
        E(111, "  C   EG", "ELEC", 40),
        E(112, "   APKEG", "MIGHT", 4),
        E(113, "   APKEG", "MIGHT", 4),
        E(114, " S    EG", "INT", 4),
        E(115, " SCAPKEG", "MAGIC", 25),
        E(116, "   A K  ", "ELEM", 50),
        E(117, "   A K  ", "COLD", 50),
        E(118, "   APKEG", "MIGHT", 10),
        E(119, "    P  G", "MAGIC", 50),
        E(120, "    P E ", "MAGIC", 50),
        E(121, "RSCAPKEG", "EQUIP", 0),
        E(122, "R CAPKEG", "EQUIP", 0),
        E(123, "R CAPKEG", "EQUIP", 0),
        E(124, "R CAPKEG", "EQUIP", 0),
        E(125, "  CAPKEG", "EQUIP", 0),
        E(126, "    PKEG", "EQUIP", 0),
        E(127, "    PKEG", "EQUIP", 0),
        E(128, "RSCAPKEG", "EQUIP", 0),
        E(129, "R CAPKEG", "EQUIP", 0),
        E(130, "R CAPKEG", "EQUIP", 0),
        E(131, "R CAPKEG", "FIRE", 5),
        E(132, "  CAPKEG", "FIRE", 5),
        E(133, "    PKEG", "FIRE", 10),
        E(134, "    PKEG", "FIRE", 10),
        E(135, "R CAPKEG", "ELEC", 10),
        E(136, "R CAPKEG", "COLD", 10),
        E(137, "R CAPKEG", "FIRE", 15),
        E(138, "  CAPKEG", "FIRE", 15),
        E(139, "    PKEG", "FIRE", 20),
        E(140, "    PKEG", "FIRE", 20),
        E(141, "RS A  EG", "EQUIP", 0),
        E(142, "R CAPKEG", "SPEED", 2),
        E(143, "  CAPKEG", "LUCK", 4),
        E(144, "    PKEG", "MIGHT", 2),
        E(145, "    PKEG", "FIRE", 50),
        E(146, "RS A  EG", "FEAR", 20),
        E(147, "  CAPKEG", "CURSE", 0),
        E(148, "RSCAPKEG", "CURSE", 0),
        E(149, "R CAPKEG", "ELEC", 60),
        E(150, "  CAPKEG", "FIRE", 60),
        E(151, "    PK  ", "LUCK", 10),
        E(152, "    P  G", "MAGIC", 40),
        E(153, "    P E ", "MAGIC", 40),
        E(154, " S A KEG", "MAGIC", 40),
        E(155, "RS A  EG", "FEAR", 60),
        E(156, "R C PKEG", "EQUIP", 0),
        E(157, "R C PKEG", "EQUIP", 0),
        E(158, "R C PKEG", "HOLY", 20),
        E(159, "R C PKEG", "EQUIP", 0),
        E(160, "R C PKEG", "EQUIP", 0),
        E(161, "R C PKEG", "CURSE", 0),
        E(162, "R C PKEG", "EQUIP", 0),
        E(163, "R C PKEG", "EQUIP", 0),
        E(164, "R C PKEG", "CURSE", 0),
        E(165, "R C PKEG", "FIRE", 20),
        E(166, "R C PKEG", "COLD", 20),
        E(167, "R C PKEG", "ELEC", 20),
        E(168, "R C PKEG", "ELEM", 20),
        E(169, "R C PKEG", "MAGIC", 20),
        E(170, "R C PKEG", "MAGIC", 10),
        E(171, "RSCAPKEG", "NONE", 0),
        E(172, "RSCAPKEG", "NONE", 0),
        E(173, "RSCAPKEG", "NONE", 0),
        E(174, "RSCAPKEG", "NONE", 0),
        E(175, "RSCAPKEG", "NONE", 0),
        E(176, "RSCAPKEG", "NONE", 0),
        E(177, "RSCAPKEG", "NONE", 0),
        E(178, "RSCAPKEG", "NONE", 0),
        E(179, "RSCAPKEG", "NONE", 0),
        E(180, "R     EG", "THIEF", 20),
        E(181, "RSCAPKEG", "EQUIP", 0),
        E(182, "RSCAPKEG", "EQUIP", 0),
        E(183, "RSCAPKEG", "NONE", 0),
        E(184, "RSCAPKEG", "NONE", 0),
        E(185, "RSCAPKEG", "NONE", 0),
        E(186, "RSCAPKEG", "EQUIP", 0),
        E(187, "RSCAPKEG", "NONE", 0),
        E(188, "RSCAPKEG", "NONE", 0),
        E(189, "RSCAPKEG", "NONE", 0),
        E(190, "RSCAPKEG", "AC+", 1),
        E(191, "RSCAPKEG", "CURSE", 0),
        E(192, "RSCAPKEG", "NONE", 0),
        E(193, " S    EG", "AC+", 2),
        E(194, "        ", "?????", 5),
        E(195, "RSCAPKEG", "NONE", 0),
        E(196, "RSCAPKEG", "NONE", 0),
        E(197, "RSCAPKEG", "SPEED", 5),
        E(198, "RSCAPKEG", "LUCK", 5),
        E(199, " S A  EG", "FIRE", 15),
        E(200, "RSCAPKEG", "FEAR", 50),
        E(201, "RSCAPKEG", "NONE", 0),
        E(202, "R   PKEG", "MIGHT", 5),
        E(203, "RSCAPKEG", "NONE", 0),
        E(204, "RSCAPKEG", "AC+", 2),
        E(205, " SCAP EG", "INT", 2),
        E(206, "RSCAPKEG", "NONE", 0),
        E(207, " S    EG", "INT", 5),
        E(208, "R CAPKEG", "MIGHT", 5),
        E(209, "  C   EG", "PERS", 5),
        E(210, "RSCAPKEG", "NONE", 0),
        E(211, "RSCAPKEG", "NONE", 0),
        E(212, "RSCAPKEG", "HOLY", 30),
        E(213, "R CA  EG", "ELEC", 20),
        E(214, "RSCAPKEG", "ACCY", 5),
        E(215, "RSCAPKEG", "NONE", 0),
        E(216, "RSCAPKEG", "MAGIC", 10),
        E(217, "RSCAPKEG", "NONE", 0),
        E(218, "RSCAPKEG", "NONE", 0),
        E(219, "RSCAPKEG", "NONE", 0),
        E(220, "RSCAPKEG", "MAGIC", 20),
        E(221, " S A  EG", "MAGIC", 10),
        E(222, "RSCAPKEG", "PERS", 5),
        E(223, "RSCAPKEG", "NONE", 0),
        E(224, "RSCAPKEG", "LUCK", 10),
        E(225, "RSCAPKEG", "MAGIC", 30),
        E(226, "RSCAPKEG", "NONE", 0),
        E(227, "RSCAPKEG", "NONE", 0),
        E(228, "RSCAPKEG", "?????", 80),
        E(229, "RSCAPKEG", "?????", 80),
        E(230, "        ", "?????", 5),
        E(231, "RSCAPKEG", "NONE", 0),
        E(232, "RSCAPKEG", "LUCK", 2),
        E(233, "RSCAPKEG", "NONE", 0),
        E(234, "RSCAPKEG", "NONE", 0),
        E(235, "RSCAPKEG", "NONE", 0),
        E(236, "RSCAPKEG", "NONE", 0),
        E(237, "RSCAPKEG", "NONE", 0),
        E(238, "RSCAPKEG", "NONE", 0),
        E(239, "RSCAPKEG", "NONE", 0),
        E(240, "RSCAPKEG", "NONE", 0),
        E(241, "RSCAPKEG", "NONE", 0),
        E(242, "RSCAPKEG", "NONE", 0),
        E(243, "RSCAPKEG", "ACCY", 5),
        E(244, "RSCAPKEG", "NONE", 0),
        E(245, "RSCAPKEG", "NONE", 0),
        E(246, "RSCAPKEG", "CURSE", 0),
        E(247, "RSCAPKEG", "LUCK", 10),
        E(248, "RSCAPKEG", "NONE", 0),
        E(249, "RSCAPKEG", "NONE", 0),
        E(250, "RSCAPKEG", "NONE", 0),
        E(251, "RSCAPKEG", "NONE", 0),
        E(252, "RSCAPKEG", "NONE", 0),
        E(253, "RSCAPKEG", "NONE", 0),
        E(254, "RSCAPKEG", "NONE", 0),
        E(255, "RSCAPKEG", "NONE", 0),
    }.ToDictionary(t => t.id, t => new ItemEffect(t.used, t.special, t.amt));

    /// <summary>The effect record for an item id, or null when none is on file (id 85 / 0 / out of range).</summary>
    public static ItemEffect? For(byte id) => Effects.TryGetValue(id, out var e) ? e : null;

    // SPECIAL tags that grant a percentage resistance when equipped (FAQ: "COLD/50 increases cold
    // resistance by 50 per cent"); the rest are flat attribute bonuses, handled below.
    private static readonly IReadOnlyDictionary<string, string> Resistances = new Dictionary<string, string>
    {
        ["FIRE"] = "Fire", ["COLD"] = "Cold", ["ELEC"] = "Electricity", ["ELEM"] = "Acid",
        ["POISN"] = "Poison", ["FEAR"] = "Fear", ["MAGIC"] = "Magic", ["HOLY"] = "Holy",
    };

    private static readonly IReadOnlyDictionary<string, string> Attributes = new Dictionary<string, string>
    {
        ["MIGHT"] = "Might", ["SPEED"] = "Speed", ["INT"] = "Intellect",
        ["PERS"] = "Personality", ["ACCY"] = "Accuracy", ["LUCK"] = "Luck",
    };

    /// <summary>
    /// The readable equip effect, e.g. "Fire resistance +20%", "+6 Might", "+1 Armour Class",
    /// "Cursed". Empty for a plain equippable (EQUIP), a non-equippable (NONE), or an item with no
    /// data on file.
    /// </summary>
    public static string EffectText(byte id) => For(id) is { } e ? EffectText(e) : "";

    private static string EffectText(ItemEffect e)
    {
        if (Resistances.TryGetValue(e.Special, out var r)) return $"{r} resistance +{e.Amount}%";
        if (Attributes.TryGetValue(e.Special, out var a)) return $"+{e.Amount} {a}";
        return e.Special switch
        {
            "AC+" => $"+{e.Amount} Armour Class",
            "THIEF" => $"+{e.Amount}% Thievery",
            "CURSE" => "Cursed",
            "?????" => "Unknown special effect",
            _ => "",   // EQUIP / NONE carry no special effect
        };
    }

    // Mask positions 0..5 in usability order; positions 6,7 are the E/G alignment slots.
    private static readonly (char Flag, string Name)[] ClassOrder =
    {
        ('R', "Robber"), ('S', "Sorcerer"), ('C', "Cleric"),
        ('A', "Archer"), ('P', "Paladin"), ('K', "Knight"),
    };

    /// <summary>
    /// Readable class/alignment usability, e.g. "All classes · any alignment",
    /// "Knight · any alignment", or "Archer, Paladin, Knight · Good only". Empty when no data, or
    /// when no class can use the item (an all-space mask, e.g. the gem quest items).
    /// </summary>
    public static string UsedByText(byte id) => For(id) is { } e ? UsedByText(e) : "";

    private static string UsedByText(ItemEffect e)
    {
        var mask = e.UsedBy;
        if (mask.Length < 8) return "";
        var classes = ClassOrder.Where((_, i) => mask[i] != ' ').Select(c => c.Name).ToList();
        if (classes.Count == 0) return "";   // all-space mask → no meaningful usability to show
        var who = classes.Count == ClassOrder.Length ? "All classes" : string.Join(", ", classes);
        bool evil = mask[6] != ' ', good = mask[7] != ' ';
        var align = evil && good ? "any alignment" : good ? "Good only" : evil ? "Evil only" : "";
        return align.Length == 0 ? who : $"{who} · {align}";
    }

    /// <summary>
    /// The full descriptive second line for the Items tab: the equip effect, then either the
    /// class/alignment usability or a "Cannot be equipped" note for carried/quest items. Empty
    /// when no data is on file (OBSIDIAN BOW, id 85).
    /// </summary>
    public static string Describe(byte id)
    {
        if (For(id) is not { } e) return "";
        var usedBy = e.Special == "NONE" ? "Cannot be equipped" : UsedByText(e);
        return string.Join("  ·  ", new[] { EffectText(e), usedBy }.Where(p => p.Length > 0));
    }
}
