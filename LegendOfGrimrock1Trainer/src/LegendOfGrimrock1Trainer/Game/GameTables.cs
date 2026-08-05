namespace LegendOfGrimrock1Trainer.Game;

/// <summary>How a condition affects the champion carrying it.</summary>
public enum ConditionKind
{
    /// <summary>Harmful: the trainer's "cure" clears these.</summary>
    Harmful,

    /// <summary>Beneficial: the trainer's "bless" sets these.</summary>
    Beneficial,

    /// <summary>Neither — bookkeeping the game shows as a condition (unspent skill points).</summary>
    Neutral,
}

/// <summary>One stat, with the label the game itself shows for it.</summary>
public readonly record struct StatInfo(string Name, string UiName, bool IsResource);

/// <summary>One condition, with the label the game shows and whether it helps or hurts.</summary>
public readonly record struct ConditionInfo(string Name, string UiName, ConditionKind Kind);

/// <summary>One spell, as the game's own <c>dungeon.spells</c> table defines it.</summary>
public readonly record struct SpellInfo(string Name, string UiName, string Skill, int SkillLevel, string Runes, int ManaCost);

/// <summary>
/// Reference data transcribed out of the running game's own Lua tables, so the UI can label things
/// the way the game does without having to walk them on every refresh.
///
/// None of it is guessed. <see cref="Stats"/> and <see cref="StatUiNames"/> come from the globals
/// <c>Stats</c> and <c>StatNames</c>; <see cref="Conditions"/> from <c>Condition.conditions</c>;
/// <see cref="Skills"/> from the keys of <c>Skill.skills</c>; <see cref="Spells"/> from
/// <c>dungeon.spells</c>; <see cref="CampaignLevelNames"/> from <c>dungeon.maps[i].name</c>. The
/// live tables stay authoritative: whenever a champion or map actually carries a name, the trainer
/// shows what it read, and these tables only supply ordering and pretty labels.
/// </summary>
public static class GameTables
{
    /// <summary>Stat keys in the game's own <c>Stats</c> order.</summary>
    public static readonly StatInfo[] Stats =
    {
        new("health",        "Health",        IsResource: true),
        new("energy",        "Energy",        IsResource: true),
        new("strength",      "Strength",      IsResource: false),
        new("dexterity",     "Dexterity",     IsResource: false),
        new("vitality",      "Vitality",      IsResource: false),
        new("willpower",     "Willpower",     IsResource: false),
        new("protection",    "Protection",    IsResource: false),
        new("evasion",       "Evasion",       IsResource: false),
        new("resist_fire",   "Resist Fire",   IsResource: false),
        new("resist_cold",   "Resist Cold",   IsResource: false),
        new("resist_poison", "Resist Poison", IsResource: false),
        new("resist_shock",  "Resist Shock",  IsResource: false),
    };

    /// <summary>Lookup from stat key to the label the game shows.</summary>
    public static readonly IReadOnlyDictionary<string, string> StatUiNames =
        Stats.ToDictionary(s => s.Name, s => s.UiName, StringComparer.Ordinal);

    /// <summary>The two stats that are bars rather than scores, and therefore have a meaningful max.</summary>
    public static readonly string[] ResourceStats = { "health", "energy" };

    /// <summary>The four ability scores.</summary>
    public static readonly string[] Attributes = { "strength", "dexterity", "vitality", "willpower" };

    /// <summary>The four elemental resistances.</summary>
    public static readonly string[] Resistances = { "resist_fire", "resist_cold", "resist_poison", "resist_shock" };

    /// <summary>Conditions in the game's own <c>Condition.conditions</c> order.</summary>
    public static readonly ConditionInfo[] Conditions =
    {
        new("unused_skill_points", "Level Up",        ConditionKind.Neutral),
        new("poison",              "Poisoned",        ConditionKind.Harmful),
        new("starving",            "Starving",        ConditionKind.Harmful),
        new("diseased",            "Diseased",        ConditionKind.Harmful),
        new("paralyzed",           "Paralyzed",       ConditionKind.Harmful),
        new("cursed",              "Cursed",          ConditionKind.Harmful),
        new("blind",               "Blind",           ConditionKind.Harmful),
        new("slow",                "Slow",            ConditionKind.Harmful),
        new("haste",               "Hastened",        ConditionKind.Beneficial),
        new("rage",                "Rage",            ConditionKind.Beneficial),
        new("detect_monsters",     "Detect Monsters", ConditionKind.Beneficial),
        new("burdened",            "Burdened",        ConditionKind.Harmful),
        new("overloaded",          "Overloaded",      ConditionKind.Harmful),
        new("fire_shield",         "Fire Shield",     ConditionKind.Beneficial),
        new("shock_shield",        "Shock Shield",    ConditionKind.Beneficial),
        new("poison_shield",       "Poison Shield",   ConditionKind.Beneficial),
        new("frost_shield",        "Frost Shield",    ConditionKind.Beneficial),
        new("invisibility",        "Invisibility",    ConditionKind.Beneficial),
    };

    /// <summary>Lookup from condition key to its metadata.</summary>
    public static readonly IReadOnlyDictionary<string, ConditionInfo> ConditionsByName =
        Conditions.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

    /// <summary>
    /// Conditions the game's own condition timers count down in seconds. Burdened, overloaded and
    /// the unused-skill-points marker are recomputed from load and level each frame instead, so
    /// setting a duration on them accomplishes nothing.
    /// </summary>
    public static readonly string[] TimedConditions =
    {
        "poison", "diseased", "paralyzed", "blind", "slow", "haste", "rage", "detect_monsters",
        "fire_shield", "shock_shield", "poison_shield", "frost_shield", "invisibility",
    };

    /// <summary>Skill keys, in the game's <c>Skill.skills</c> set, grouped the way the character sheet does.</summary>
    public static readonly string[] Skills =
    {
        "athletics", "armors", "dodge",
        "swords", "axes", "maces", "daggers", "unarmed_combat", "assassination", "staves",
        "missile_weapons", "throwing_weapons",
        "spellcraft", "fire_magic", "air_magic", "ice_magic", "earth_magic",
    };

    /// <summary>Pretty labels for <see cref="Skills"/>, from each skill definition's <c>uiName</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> SkillUiNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["athletics"] = "Athletics",
        ["armors"] = "Armors",
        ["dodge"] = "Dodge",
        ["swords"] = "Swords",
        ["axes"] = "Axes",
        ["maces"] = "Maces",
        ["daggers"] = "Daggers",
        ["unarmed_combat"] = "Unarmed Combat",
        ["assassination"] = "Assassination",
        ["staves"] = "Staff Defense",       // the game's own uiName, not "Staves"
        ["missile_weapons"] = "Missile Weapons",
        ["throwing_weapons"] = "Throwing Weapons",
        ["spellcraft"] = "Spellcraft",
        ["fire_magic"] = "Fire Magic",
        ["air_magic"] = "Air Magic",
        ["ice_magic"] = "Ice Magic",
        ["earth_magic"] = "Earth Magic",
    };

    /// <summary>
    /// The twenty spells the shipped dungeon defines, with the rune letters the game stores. The
    /// letters map onto the 3×3 rune board reading left to right, top to bottom: A B C on the top
    /// row, D E F in the middle, G H I on the bottom.
    /// </summary>
    public static readonly SpellInfo[] Spells =
    {
        new("fireburst",             "Fireburst",               "fire_magic",  2,  "A",    15),
        new("enchant_fire_arrow",    "Enchant Fire Arrow",      "fire_magic",  7,  "ABFH", 20),
        new("fireball",              "Fireball",                "fire_magic",  13, "ACF",  33),
        new("fire_shield",           "Fire Shield",             "fire_magic",  16, "AE",   50),
        new("shock",                 "Shock",                   "air_magic",   4,  "C",    21),
        new("enchant_shock_arrow",   "Enchant Lightning Arrow", "air_magic",   9,  "BCFH", 20),
        new("lightning_bolt",        "Lightning Bolt",          "air_magic",   14, "CD",   40),
        new("invisibility",          "Invisibility",            "air_magic",   19, "CEH",  35),
        new("shock_shield",          "Shock Shield",            "air_magic",   22, "CE",   55),
        new("ice_shards",            "Ice Shards",              "ice_magic",   3,  "GI",   24),
        new("enchant_frost_arrow",   "Enchant Frost Arrow",     "ice_magic",   7,  "BFHI", 20),
        new("frostbolt",             "Frostbolt",               "ice_magic",   13, "CI",   29),
        new("frost_shield",          "Frost Shield",            "ice_magic",   19, "EI",   45),
        new("poison_cloud",          "Poison Cloud",            "earth_magic", 3,  "G",    17),
        new("poison_bolt",           "Poison Bolt",             "earth_magic", 7,  "CG",   22),
        new("enchant_poison_arrow",  "Enchant Poison Arrow",    "earth_magic", 11, "BFGH", 20),
        new("poison_shield",         "Poison Shield",           "earth_magic", 13, "EG",   35),
        new("light",                 "Light",                   "spellcraft",  5,  "BE",   25),
        new("darkness",              "Darkness",                "spellcraft",  5,  "EH",   25),
        new("powerbolt",             "Powerbolt",               "air_magic",   0,  "",     0),
    };

    /// <summary>
    /// Level names of the shipped campaign, in order. Read live from <c>dungeon.maps[i].name</c>
    /// whenever a dungeon is loaded — this copy only labels the teleport list before that happens,
    /// and is deliberately not used for a custom dungeon or a mod, which name their own levels.
    /// </summary>
    public static readonly string[] CampaignLevelNames =
    {
        "Into the Dark",
        "Old Tunnels",
        "Pillars of Light",
        "Archives",
        "Hallways",
        "Trapped",
        "Ancient Chambers",
        "The Vault",
        "Goromorg Temple I",
        "Goromorg Temple II",
        "The Tomb",
        "The Prison",
        "The Cemetery",
    };

    /// <summary>Compass labels for the party's <c>facing</c> field.</summary>
    public static readonly string[] FacingNames = { "North", "East", "South", "West" };

    /// <summary>Turns a snake_case game key into something readable when no <c>uiName</c> is available.</summary>
    public static string Humanise(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
