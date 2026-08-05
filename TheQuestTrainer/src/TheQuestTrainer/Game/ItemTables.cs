namespace TheQuestTrainer.Game;

/// <summary>What the one mutable word on an item means for a given item type.</summary>
public enum ItemMeter
{
    /// <summary>Nothing — the word is unused for this kind of item and the trainer leaves it alone.</summary>
    None = 0,

    /// <summary>Wear, against the type's maximum condition. "Repair" fills it.</summary>
    Condition,

    /// <summary>Remaining wand charges, against the enchantment's own count. "Recharge" fills it.</summary>
    Charges,

    /// <summary>How many arrows, bolts or throwing weapons the stack holds.</summary>
    Units,
}

/// <summary>
/// The game's own item taxonomy, transcribed from the two tables <c>TheQuest.exe</c> indexes by
/// category and sub-type (image RVAs <c>0x2DDAF0</c> and <c>0x2DDAB0</c> in the v1.9.10 build).
///
/// They are copied here rather than read live for the same reason <see cref="GameTables"/> holds the
/// skill names: the trainer already gets everything it needs from the item type itself, and a table
/// of names is not worth making the tool depend on two more RVAs. The names are the game's, spelling
/// and capitalisation included — the weapon sub-types really are lower-case in the executable.
/// </summary>
public static class ItemTables
{
    /// <summary>Category names, indexed by the item type's category byte. Index 0 is the game's own placeholder.</summary>
    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "?", "Weapon", "Heavy armor", "Light armor", "Accessory", "Book", "Alchemy equipment",
        "Ingredient", "Potion", "Magic", "Money", "Key", "Repair", "Miscellaneous", "Comestible", "Gem",
    };

    /// <summary>
    /// Sub-type names per category. Every list is indexed by the sub-type byte, so entry 0 is the
    /// game's placeholder everywhere except category 1, whose table starts at a real entry ("hand",
    /// the unarmed slot).
    /// </summary>
    private static readonly IReadOnlyList<string>[] Subtypes =
    {
        /*  0 */ new[] { "?" },
        /*  1 */ new[] { "hand", "short sword", "long sword", "mace", "axe", "hammer", "club",
                         "magicstaff", "throwing", "short bow", "long bow", "quiver", "crossbow", "bolt quiver" },
        /*  2 */ new[] { "?", "Shield", "Armored pants", "Armor", "Helm", "Gauntlets", "Boots", "Cloak", "Belt" },
        /*  3 */ new[] { "?", "Shield", "Armored pants", "Armor", "Helm", "Gauntlets", "Boots", "Cloak", "Belt" },
        /*  4 */ new[] { "?", "Amulet", "Ring" },
        /*  5 */ new[] { "?", "Book", "Letter", "Map" },
        /*  6 */ new[] { "?", "Mortar/pestle" },
        /*  7 */ new[] { "?", "Ingredient" },
        /*  8 */ new[] { "?", "Potion" },
        /*  9 */ new[] { "?", "Scroll", "Spellbook", "Blank scroll", "Wand", "Empty wand" },
        /* 10 */ new[] { "?", "Money" },
        /* 11 */ new[] { "?", "Key", "Lockpick" },
        /* 12 */ new[] { "?", "Hammer" },
        /* 13 */ new[] { "?", "Miscellaneous" },
        /* 14 */ new[] { "?", "Food", "Water" },
        /* 15 */ new[] { "?", "Gem" },
    };

    /// <summary>Highest category the game defines. Anything above it is not an item type.</summary>
    public const int MaxCategory = 15;

    /// <summary>Category 1 sub-types that hold a count of ammunition rather than wearing out.</summary>
    private static bool IsAmmunition(int subtype) => subtype is 8 or 11 or 13;

    /// <summary>Category name for <paramref name="category"/>, or a placeholder when it is unknown.</summary>
    public static string CategoryName(int category) =>
        category >= 0 && category < Categories.Count ? Categories[category] : $"Category {category}";

    /// <summary>Sub-type name, or an empty string when the pair names nothing useful.</summary>
    public static string SubtypeName(int category, int subtype)
    {
        if (category < 0 || category >= Subtypes.Length) return "";
        var list = Subtypes[category];
        if (subtype < 0 || subtype >= list.Count) return "";
        return list[subtype] == "?" ? "" : list[subtype];
    }

    /// <summary>
    /// A one-line "Heavy armor · Helm" style label. Category 1 says light or heavy the way the
    /// game's own item panel does, because that is what decides which weapon skill applies.
    ///
    /// Several categories have exactly one sub-type, named after the category — a Potion of
    /// sub-type "Potion", a Book of sub-type "Book" — so the tail is dropped when it would only
    /// repeat the head.
    /// </summary>
    public static string Describe(int category, int subtype, bool lightWeapon)
    {
        string head = category == 1
            ? (lightWeapon ? "Light weapon" : "Heavy weapon")
            : CategoryName(category);
        string tail = SubtypeName(category, subtype);
        return tail.Length == 0 || string.Equals(tail, head, StringComparison.OrdinalIgnoreCase)
            ? head
            : $"{head} · {tail}";
    }

    /// <summary>
    /// What an item of this category and sub-type keeps in its one mutable word, reproducing the
    /// game's own item panel: it prints "Condition" for armour, for weapons that are not ammunition,
    /// for repair hammers, for alchemy equipment and for lockpicks; "Contains %u units" for quivers
    /// and throwing weapons; and "(%u/%u charges)" for wands.
    /// </summary>
    public static ItemMeter MeterFor(int category, int subtype) => category switch
    {
        1 => IsAmmunition(subtype) ? ItemMeter.Units : ItemMeter.Condition,
        2 or 3 or 6 or 12 => ItemMeter.Condition,
        9 => subtype is 4 or 5 ? ItemMeter.Charges : ItemMeter.None,
        11 => subtype == 2 ? ItemMeter.Condition : ItemMeter.None,
        _ => ItemMeter.None,
    };

    /// <summary>
    /// The wear word the game's item panel shows for a condition, from its own ladder. Below a tenth
    /// is "broken", and only a full hundred per cent is "perfect".
    /// </summary>
    public static string ConditionBand(int condition, int maxCondition)
    {
        if (maxCondition <= 0) return "";
        int percent = (int)((long)condition * 100 / maxCondition);
        if (percent < 10) return "broken";
        if (percent < 30) return "poor";
        if (percent < 70) return "average";
        return percent < 100 ? "good" : "perfect";
    }
}
