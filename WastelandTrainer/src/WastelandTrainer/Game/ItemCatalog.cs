using System.Text.RegularExpressions;

namespace WastelandTrainer.Game;

/// <summary>
/// A Wasteland inventory item: its in-record id, display name, category, a short blurb, and — for
/// weapons — a compact damage/range/fire-mode line (<see cref="Damage"/>, empty for non-weapons).
/// </summary>
public sealed record ItemInfo(int Id, string Name, string Category, string Description = "", string Damage = "")
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
///
/// The <see cref="ItemInfo.Description"/> blurbs and the weapon <see cref="ItemInfo.Damage"/> lines are
/// reference only (they drive no memory writes). Damage is quoted the way Wasteland rolls it — a pool of
/// six-sided dice (e.g. "4d6") from which each point of the target's Armor Class removes one die — with
/// numbers taken from the community weapon tables (wasteland.fandom.com and its archive mirror, which
/// agree on every value), while the range/fire-mode notes follow the game's own manual. Damage is set
/// only on weapons and thrown explosives; ammo, armor, and gear leave it blank.
/// </summary>
public static class ItemCatalog
{
    /// <summary>The empty-slot sentinel: id 0 clears an inventory slot.</summary>
    public static readonly ItemInfo None = new(0, "(empty)", "");

    public static readonly IReadOnlyList<ItemInfo> Items = new ItemInfo[]
    {
        None,

        // --- Melee & thrown-blade weapons ---
        new( 1, "Ax",                 "Melee", "Heavy two-handed melee blade; solid early damage for a strong ranger.", "3d6 · melee"),
        new( 2, "Club",               "Melee", "Crude blunt melee weapon — a small step up from bare fists.", "3d6 · melee"),
        new( 3, "Chainsaw",           "Melee", "Brutal close-range melee weapon with high damage.", "6d6 · melee"),
        new( 4, "Knife",              "Melee", "Basic starting blade for knife-fight range.", "3d6 · melee"),
        new( 5, "Proton ax",          "Melee", "High-tech energy axe; the strongest melee weapon in the game.", "14d6 · melee"),
        new(14, "Spear",              "Melee", "Reach melee weapon that can also be thrown.", "5d6 · thrown, short range"),
        new(15, "Throwing knife",     "Melee", "Light blade meant to be hurled at short range.", "2d6 · thrown, short range"),

        // --- Thrown / explosive ---
        new( 6, "Grenade",            "Thrown / Explosive", "Thrown area-effect explosive; auto-reloads from stock while any remain.", "7d6 · thrown, wide area"),
        new( 7, "Plastic explosive",  "Thrown / Explosive", "Powerful placed/thrown charge for tough targets.", "10d6 · thrown, wide area"),
        new( 8, "TNT",                "Thrown / Explosive", "Bundle of high explosive with a wide blast.", "5d6 · thrown, wide area"),
        new( 9, "Mangler",            "Thrown / Explosive", "Advanced thrown explosive with a broad, heavy blast.", "4d6 · medium range"),
        new(10, "Sabot rocket",       "Thrown / Explosive", "Armour-piercing rocket round for anti-tank launchers.", "7d6 · long range"),
        new(11, "LAW rocket",         "Thrown / Explosive", "Disposable anti-tank rocket; re-equip after firing.", "10d6 · long range · single use"),
        new(12, "RPG-7",              "Thrown / Explosive", "Reusable rocket launcher for armoured and robotic threats.", "13d6 · long range"),

        // --- Firearms ---
        new(13, "M1911A1 45 pistol",  "Firearm", "Reliable eight-shot .45 sidearm; the classic early gun.", "4d6 · short range · single shot"),
        new(16, "VP91Z 9mm pistol",   "Firearm", "Eighteen-round 9mm pistol — fewer reloads than the .45.", "3d6 · short range · single shot"),
        new(17, "Flamethrower",       "Firearm", "Short-range weapon that hoses a cone of fire.", "11d6 · medium range · stream"),
        new(18, "M17 carbine",        "Firearm", "Ten-shot 7.62mm carbine; long reach in a shorter frame.", "5d6 · long range · single shot"),
        new(19, "M19 rifle",          "Firearm", "Eight-shot 7.62mm single-fire rifle for long range.", "5d6 · long range · single shot"),
        new(20, "Red Ryder",          "Firearm", "A hidden joke item — a BB gun that absurdly hits harder than anything else in the game.", "200d6 · long range — easter-egg super-weapon"),
        new(21, "Mac 17 SMG",         "Firearm", "Thirty-round .45 submachine gun for medium-range firefights.", "4d6 · medium range · selective fire"),
        new(22, "Uzi SMG Mark 27",    "Firearm", "Forty-round 9mm SMG with selective fire.", "4d6 · medium range · selective fire"),
        new(23, "AK 97 assault rifle","Firearm", "Thirty-round 7.62mm selective-fire rifle; a workhorse long gun.", "6d6 · long range · selective fire"),
        new(24, "M1989A1 Nato assault rifle", "Firearm", "Top-tier 7.62mm assault rifle — powerful but ammo-hungry.", "6d6 · long range · selective fire"),

        // --- Energy weapons ---
        new(25, "Laser pistol",       "Energy Weapon", "Entry energy sidearm; runs on power packs, not clips.", "6d6 · short range · single shot"),
        new(26, "Ion beamer",         "Energy Weapon", "Energy weapon that fires a damaging ion burst.", "14d6 · medium range · single shot"),
        new(27, "Laser carbine",      "Energy Weapon", "Mid-tier energy long gun.", "8d6 · medium range · single shot"),
        new(28, "Laser rifle",        "Energy Weapon", "High-damage energy rifle.", "12d6 · long range · single shot"),
        new(29, "Meson cannon",       "Energy Weapon", "The strongest standard weapon — devastating end-game energy gun.", "19d6 · long range · single shot"),

        // --- Ammunition ---
        new(30, "45 clip",            "Ammo", "Ammunition for the .45 pistol and the MAC-17 SMG."),
        new(31, "7.62mm clip",        "Ammo", "Ammunition for the rifles and assault rifles."),
        new(32, "9mm clip",           "Ammo", "Ammunition for the 9mm pistol and the Uzi."),
        new(33, "Howitzer shell",     "Ammo", "Heavy shell for artillery-class weapons."),
        new(34, "Power pack",         "Ammo", "Charge cell that powers the energy weapons."),

        // --- Armor ---
        new(35, "Power armor",        "Armor", "The best protection in the game; reserve it for front-line rangers."),
        new(36, "Bullet proof shirt", "Armor", "Light, concealable body armour."),
        new(37, "Kevlar vest",        "Armor", "Mid-weight ballistic vest."),
        new(38, "Leather jacket",     "Armor", "Minimal armour — better than nothing early on."),
        new(39, "Kevlar suit",        "Armor", "Full kevlar covering; strong mid-game protection."),
        new(40, "Pseudo-chitin armor","Armor", "Tough exotic armour a rung above kevlar."),
        new(41, "Rad suit",           "Armor", "Protective suit that lets you cross radiation zones safely."),
        new(42, "Robe",               "Armor", "Cultist robe — disguise value, little real protection."),

        // --- Gear, consumables & quest items ---
        new(43, "Book",               "Gear & Quest", "Readable book; usually carries a clue or flavour."),
        new(44, "Canteen",            "Gear & Quest", "Holds water for desert travel — refill before long treks."),
        new(45, "Crowbar",            "Gear & Quest", "Pries open some doors and crates."),
        new(46, "Engine",             "Gear & Quest", "Vehicle engine; a quest component."),
        new(47, "Gas mask",           "Gear & Quest", "Filters toxic air in certain areas."),
        new(48, "Geiger counter",     "Gear & Quest", "Warns you of radiation levels out in the wasteland."),
        new(49, "Hand mirror",        "Gear & Quest", "Used to peek around a corner for a specific puzzle."),
        new(50, "Jug",                "Gear & Quest", "Container used in a quest step."),
        new(51, "Map",                "Gear & Quest", "In-world map that reveals a location or route."),
        new(52, "Match",              "Gear & Quest", "Lights fires or fuses where a puzzle calls for it."),
        new(53, "Pick ax",            "Gear & Quest", "Digs or breaks through certain obstacles."),
        new(54, "Rope",               "Gear & Quest", "Helps climb or descend where terrain blocks you."),
        new(55, "Shovel",             "Gear & Quest", "Digs at marked spots to unearth buried loot."),
        new(56, "Sledge hammer",      "Gear & Quest", "Smashes through some walls and objects."),
        new(57, "Snake squeezin",     "Gear & Quest", "Wasteland moonshine — a heal/quest consumable."),
        new(58, "Android head",       "Gear & Quest", "Trophy from a defeated android; a quest item."),
        new(59, "Antitoxin",          "Gear & Quest", "Cures the poison inflicted by certain enemies."),
        new(60, "Finster's head",     "Gear & Quest", "Proof of dealing with Finster; a major quest item."),
        new(61, "Blackstar key",      "Gear & Quest", "One of the four Guardian Citadel keys — keep the set together."),
        new(62, "Bloodstaff",         "Gear & Quest", "The Needles cult relic — a pivotal story and bargaining item."),
        new(63, "Bloodstaff",         "Gear & Quest", "A second Bloodstaff entry (the game has a real one and a decoy) — keep the one your clue points to."),
        new(64, "Broken toaster",     "Gear & Quest", "Fix it with Toaster Repair for a reward."),
        new(65, "Chemical",           "Gear & Quest", "Reagent used in a crafting/quest step."),
        new(66, "Clone fluid",        "Gear & Quest", "Fluid for the Darwin cloning equipment."),
        new(67, "Visa card",          "Gear & Quest", "Faction-favour token; present it only to the named contact."),
        new(68, "Fusion cell",        "Gear & Quest", "High-tech power source for late-game gear."),
        new(69, "Grazer bat fetish",  "Gear & Quest", "Tribal charm used as a quest/trade item."),
        new(73, "Nova key",           "Gear & Quest", "Guardian Citadel key — part of the four-key set."),
        new(74, "Onyx ring",          "Gear & Quest", "Valuable ring; a quest or trade item."),
        new(75, "Passkey",            "Gear & Quest", "Opens a restricted door inside a facility."),
        new(76, "Plasma coupler",     "Gear & Quest", "Specialised part for a late-game high-radiation objective — never sell it."),
        new(77, "Power converter",    "Gear & Quest", "Late-game power-restoration component; keep it with your tech parts."),
        new(78, "Pulsar key",         "Gear & Quest", "Guardian Citadel key — part of the four-key set."),
        new(79, "Quasar key",         "Gear & Quest", "Guardian Citadel key — part of the four-key set."),
        new(80, "Rom board",          "Gear & Quest", "Circuit board used to repair or program a machine."),
        new(81, "Room key #18",       "Gear & Quest", "Opens a specific locked room."),
        new(82, "Ruby ring",          "Gear & Quest", "Valuable ring; a quest or trade item."),
        new(83, "Secpass 1",          "Gear & Quest", "Security clearance pass for a guarded checkpoint."),
        new(84, "Secpass 3",          "Gear & Quest", "A higher-level security clearance pass."),
        new(85, "Secpass 7",          "Gear & Quest", "A higher-level security clearance pass."),
        new(86, "Secpass A",          "Gear & Quest", "A lettered security clearance pass."),
        new(87, "Secpass B",          "Gear & Quest", "A lettered security clearance pass."),
        new(88, "Servo motor",        "Gear & Quest", "Mechanical part for a repair/quest step."),
        new(89, "Sonic key",          "Gear & Quest", "Distinct plot key for the Vegas sewer route — don't confuse it with the Citadel keys."),
        new(90, "Toaster",            "Gear & Quest", "A working toaster, tied to the Toaster Repair running gag."),
        new(91, "Clay pot",           "Gear & Quest", "Container; a minor quest item."),
        new(92, "Fruit",              "Gear & Quest", "Edible found item."),
        new(93, "Jewelry",            "Gear & Quest", "Valuables you can sell for cash."),
        new(94, "Cash",               "Gear & Quest", "A pickup of money added to your funds."),
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
