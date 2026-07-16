using System.Text.RegularExpressions;

namespace WastelandTrainer.Game;

/// <summary>A Wasteland inventory item: its in-record id, display name, and category.</summary>
public sealed record ItemInfo(int Id, string Name, string Category)
{
    public string Label => $"{Id}  {Name}";
}

/// <summary>
/// The full Wasteland item table (ids 1..94), decoded directly from the game's own <c>WL.EXE</c>.
/// The executable is EXEPACK-compressed and stores names as 5-bit dictionary-coded strings; the item
/// names sit in the "inventory" string group at string-index <c>id + 36</c> (the same group whose
/// first 35 entries are the skill names). The decoder and offsets are ported from the open-source
/// <c>kayahr/wastelib</c> project, and the result was cross-checked against the ten item ids decoded
/// from the four live memory dumps (Knife 4, M1911A1 13, VP91Z 16, .45 clip 30, 9mm clip 32, Canteen
/// 44, Crowbar 45, Hand mirror 49, Matches 52, Rope 54) — all match at <c>index = id + 36</c>.
///
/// Names are the game's own singular forms (e.g. it renders "45 clip", not ".45 clip"). Categories
/// are a display grouping added here, not from the game. Ids 70..72 are unused in the table. The
/// second inventory byte is ammunition (weapons), a quantity/charge count (consumables), or a status
/// byte whose high bit marks a jammed weapon.
/// </summary>
public static class ItemCatalog
{
    /// <summary>The empty-slot sentinel: id 0 clears an inventory slot.</summary>
    public static readonly ItemInfo None = new(0, "(empty)", "");

    public static readonly IReadOnlyList<ItemInfo> Items = new ItemInfo[]
    {
        None,

        // --- Melee & thrown-blade weapons ---
        new( 1, "Ax",                 "Melee"),
        new( 2, "Club",               "Melee"),
        new( 3, "Chainsaw",           "Melee"),
        new( 4, "Knife",              "Melee"),
        new( 5, "Proton ax",          "Melee"),
        new(14, "Spear",              "Melee"),
        new(15, "Throwing knife",     "Melee"),

        // --- Thrown / explosive ---
        new( 6, "Grenade",            "Thrown / Explosive"),
        new( 7, "Plastic explosive",  "Thrown / Explosive"),
        new( 8, "TNT",                "Thrown / Explosive"),
        new( 9, "Mangler",            "Thrown / Explosive"),
        new(10, "Sabot rocket",       "Thrown / Explosive"),
        new(11, "LAW rocket",         "Thrown / Explosive"),
        new(12, "RPG-7",              "Thrown / Explosive"),

        // --- Firearms ---
        new(13, "M1911A1 45 pistol",  "Firearm"),
        new(16, "VP91Z 9mm pistol",   "Firearm"),
        new(17, "Flamethrower",       "Firearm"),
        new(18, "M17 carbine",        "Firearm"),
        new(19, "M19 rifle",          "Firearm"),
        new(20, "Red Ryder",          "Firearm"),
        new(21, "Mac 17 SMG",         "Firearm"),
        new(22, "Uzi SMG Mark 27",    "Firearm"),
        new(23, "AK 97 assault rifle","Firearm"),
        new(24, "M1989A1 Nato assault rifle", "Firearm"),

        // --- Energy weapons ---
        new(25, "Laser pistol",       "Energy Weapon"),
        new(26, "Ion beamer",         "Energy Weapon"),
        new(27, "Laser carbine",      "Energy Weapon"),
        new(28, "Laser rifle",        "Energy Weapon"),
        new(29, "Meson cannon",       "Energy Weapon"),

        // --- Ammunition ---
        new(30, "45 clip",            "Ammo"),
        new(31, "7.62mm clip",        "Ammo"),
        new(32, "9mm clip",           "Ammo"),
        new(33, "Howitzer shell",     "Ammo"),
        new(34, "Power pack",         "Ammo"),

        // --- Armor ---
        new(35, "Power armor",        "Armor"),
        new(36, "Bullet proof shirt", "Armor"),
        new(37, "Kevlar vest",        "Armor"),
        new(38, "Leather jacket",     "Armor"),
        new(39, "Kevlar suit",        "Armor"),
        new(40, "Pseudo-chitin armor","Armor"),
        new(41, "Rad suit",           "Armor"),
        new(42, "Robe",               "Armor"),

        // --- Gear, consumables & quest items ---
        new(43, "Book",               "Gear & Quest"),
        new(44, "Canteen",            "Gear & Quest"),
        new(45, "Crowbar",            "Gear & Quest"),
        new(46, "Engine",             "Gear & Quest"),
        new(47, "Gas mask",           "Gear & Quest"),
        new(48, "Geiger counter",     "Gear & Quest"),
        new(49, "Hand mirror",        "Gear & Quest"),
        new(50, "Jug",                "Gear & Quest"),
        new(51, "Map",                "Gear & Quest"),
        new(52, "Match",              "Gear & Quest"),
        new(53, "Pick ax",            "Gear & Quest"),
        new(54, "Rope",               "Gear & Quest"),
        new(55, "Shovel",             "Gear & Quest"),
        new(56, "Sledge hammer",      "Gear & Quest"),
        new(57, "Snake squeezin",     "Gear & Quest"),
        new(58, "Android head",       "Gear & Quest"),
        new(59, "Antitoxin",          "Gear & Quest"),
        new(60, "Finster's head",     "Gear & Quest"),
        new(61, "Blackstar key",      "Gear & Quest"),
        new(62, "Bloodstaff",         "Gear & Quest"),
        new(63, "Bloodstaff",         "Gear & Quest"),
        new(64, "Broken toaster",     "Gear & Quest"),
        new(65, "Chemical",           "Gear & Quest"),
        new(66, "Clone fluid",        "Gear & Quest"),
        new(67, "Visa card",          "Gear & Quest"),
        new(68, "Fusion cell",        "Gear & Quest"),
        new(69, "Grazer bat fetish",  "Gear & Quest"),
        new(73, "Nova key",           "Gear & Quest"),
        new(74, "Onyx ring",          "Gear & Quest"),
        new(75, "Passkey",            "Gear & Quest"),
        new(76, "Plasma coupler",     "Gear & Quest"),
        new(77, "Power converter",    "Gear & Quest"),
        new(78, "Pulsar key",         "Gear & Quest"),
        new(79, "Quasar key",         "Gear & Quest"),
        new(80, "Rom board",          "Gear & Quest"),
        new(81, "Room key #18",       "Gear & Quest"),
        new(82, "Ruby ring",          "Gear & Quest"),
        new(83, "Secpass 1",          "Gear & Quest"),
        new(84, "Secpass 3",          "Gear & Quest"),
        new(85, "Secpass 7",          "Gear & Quest"),
        new(86, "Secpass A",          "Gear & Quest"),
        new(87, "Secpass B",          "Gear & Quest"),
        new(88, "Servo motor",        "Gear & Quest"),
        new(89, "Sonic key",          "Gear & Quest"),
        new(90, "Toaster",            "Gear & Quest"),
        new(91, "Clay pot",           "Gear & Quest"),
        new(92, "Fruit",              "Gear & Quest"),
        new(93, "Jewelry",            "Gear & Quest"),
        new(94, "Cash",               "Gear & Quest"),
    };

    // Ids are unique, so key straight off the id (matching SkillBook); this fails fast if a duplicate
    // id is ever introduced into the hand-written table rather than silently keeping the first.
    private static readonly Dictionary<int, ItemInfo> ById = Items.ToDictionary(i => i.Id);

    public static string ItemName(int id) =>
        id == 0 ? None.Name : ById.TryGetValue(id, out var i) ? i.Name : $"Item #{id}";

    public static ItemInfo? Find(int id) => ById.TryGetValue(id, out var i) ? i : null;

    // Categories whose second inventory byte is ammunition the Freeze Ammo action tops up: weapons
    // that fire (firearm, energy weapon, thrown/explosive) and clips/shells/power packs. Melee weapons,
    // armor and gear/quest items are excluded — their second byte is unused or a status byte, so forcing
    // it to a max could corrupt the item.
    private static readonly HashSet<string> AmmoCategories =
        new(StringComparer.Ordinal) { "Firearm", "Energy Weapon", "Thrown / Explosive", "Ammo" };

    /// <summary>
    /// True when an item's quantity byte is ammunition Freeze Ammo should top up (a firearm, energy
    /// weapon, thrown/explosive, or a clip/shell/power pack). Unknown ids and non-ammo items are false,
    /// so the freeze never touches a melee weapon, a worn armor, or a quest item's status byte.
    /// </summary>
    public static bool IsAmmoItem(int id) => Find(id) is { } i && AmmoCategories.Contains(i.Category);

    private static readonly Regex LeadingId = new(@"^\s*(\d{1,3})\b", RegexOptions.Compiled);

    /// <summary>
    /// Parses an entry typed into (or picked from) the inventory drop-down into an item id. A blank
    /// string is the empty slot (0); next a case-insensitive item-name match wins (so a catalog name
    /// that begins with a digit — "45 clip" (30), "7.62mm clip" (31) — resolves to that item rather
    /// than being misread as a raw id); failing a name match, a leading number — bare, or the
    /// "13  Name" label form — is that raw id when it fits a byte (0..255). Returns -1 when the text
    /// matches nothing, so the caller can leave the slot untouched. This is what lets the editable
    /// drop-down reach any item id without a separate id box.
    /// </summary>
    public static int ParseSelection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string s = text.Trim();

        // Name match first: a real item name such as "45 clip" (id 30) starts with digits followed by
        // a word boundary, so the leading-id rule would otherwise mis-resolve it to id 45 (Crowbar).
        foreach (var i in Items)
            if (string.Equals(i.Name, s, StringComparison.OrdinalIgnoreCase))
                return i.Id;

        var m = LeadingId.Match(s);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int id) && id is >= 0 and <= 255)
            return id;

        return -1;
    }
}
