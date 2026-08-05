namespace TheQuestTrainer.Game;

/// <summary>One of the game's five attributes.</summary>
/// <param name="Id">The game's attribute id, 1..5.</param>
/// <param name="Name">Name as the status screen spells it.</param>
/// <param name="Effect">The game's own one-line description of what it does.</param>
public readonly record struct AttributeInfo(int Id, string Name, string Effect);

/// <summary>One of the game's twenty skills.</summary>
/// <param name="Id">The game's skill id, 1..20 — the index into the record's skill arrays.</param>
/// <param name="Name">Name as the skills screen spells it.</param>
/// <param name="GoverningAttribute">Attribute id that caps this skill at twice its base value.</param>
/// <param name="IsMagic">Whether the skill is one of the six magic schools plus Persuasion group.</param>
/// <param name="Effect">The game's own one-line description.</param>
public readonly record struct SkillInfo(int Id, string Name, int GoverningAttribute, bool IsMagic, string Effect);

/// <summary>
/// Reference data lifted out of <c>TheQuest.exe</c>'s own tables: attribute and skill names in the
/// game's id order, the descriptions it shows in tooltips, the race list, and the reputation and
/// wardrobe bands it derives from fame and outfit value.
///
/// The skill ids were not guessed. The character record carries a 20-byte display-order array —
/// primaries first, then the secondary list — and reading it against the skills screen pins every
/// id; the order here is the same one <c>SSkills.cpp</c>'s string table is laid out in.
/// </summary>
public static class GameTables
{
    /// <summary>Attribute ids, in status-screen order.</summary>
    public static readonly IReadOnlyList<AttributeInfo> Attributes = new[]
    {
        new AttributeInfo(1, "Strength",     "Affects melee damage and encumbrance."),
        new AttributeInfo(2, "Dexterity",    "Affects melee and ranged damage, armor and encumbrance."),
        new AttributeInfo(3, "Endurance",    "Affects health, resistances and encumbrance."),
        new AttributeInfo(4, "Intelligence", "Affects mana, magic and paralysis resistance and the maximum number of positions for Mark."),
        new AttributeInfo(5, "Personality",  "Affects item prices, paralysis resistance and how much the character is liked by others."),
    };

    /// <summary>Skills in id order — which is also the order the two skill arrays are laid out in.</summary>
    public static readonly IReadOnlyList<SkillInfo> Skills = new[]
    {
        new SkillInfo( 1, "Block",             2, false, "Provides extra defense against physical attacks while wielding a shield."),
        new SkillInfo( 2, "Light Weapon",      2, false, "Influences damage done by light weapons."),
        new SkillInfo( 3, "Heavy Weapon",      1, false, "Influences damage done by heavy weapons."),
        new SkillInfo( 4, "Dual Wield",        2, false, "Influences damage done while wielding a weapon in both hands."),
        new SkillInfo( 5, "Light Armor",       2, false, "Influences the armor value provided by Light Armor items."),
        new SkillInfo( 6, "Heavy Armor",       3, false, "Influences the armor value provided by Heavy Armor items."),
        new SkillInfo( 7, "Accuracy",          2, false, "Influences damage done by ranged weapons."),
        new SkillInfo( 8, "Healing Magic",     4, true,  "Influences the effectiveness of Healing spells. Cannot be learned by Undead (Rasvim)."),
        new SkillInfo( 9, "Protection Magic",  4, true,  "Influences the effectiveness of Protection spells."),
        new SkillInfo(10, "Attack Magic",      4, true,  "Influences the effectiveness of Attack spells."),
        new SkillInfo(11, "Mind Magic",        5, true,  "Influences the effectiveness of Mind spells."),
        new SkillInfo(12, "Undead Magic",      4, true,  "Influences the effectiveness of Undead spells. Can only be learned by Undead (Rasvim)."),
        new SkillInfo(13, "Environment Magic", 4, true,  "Influences the effectiveness of Environment spells."),
        new SkillInfo(14, "Repair",            2, false, "Influences the effectiveness of repairing items and the wear on repair hammers."),
        new SkillInfo(15, "Appraise",          5, false, "Influences prices for both buying and selling."),
        new SkillInfo(16, "Alchemy",           4, false, "Influences the chance to create potions and to recognize the effects of ingredients."),
        new SkillInfo(17, "Persuasion",        5, false, "Determines the chance to persuade others in dialogs. Also influences prices."),
        new SkillInfo(18, "Lockpick",          2, false, "Influences the chance to open locked doors or items."),
        new SkillInfo(19, "Disarm",            4, false, "Influences the chance to disarm traps on doors or items."),
        new SkillInfo(20, "Stealth",           2, false, "Influences the chance to steal from passersby."),
    };

    /// <summary>Race names by id. Id 0 is the engine's "Creature", used for NPCs and monsters.</summary>
    public static readonly IReadOnlyList<string> Races = new[]
    {
        "Creature", "Rasvim", "Etherim", "Seiry", "Derth", "Nogur",
    };

    /// <summary>
    /// The first entries of the per-level experience table, used as the heap-scan signature.
    ///
    /// Eight ascending, wide-apart u32s is a 32-byte pattern with no plausible accidental match; in
    /// a 257 MB live session it hit exactly twice, once for the live character and once for the
    /// prototype the game keeps beside it, and validation then separated them.
    /// </summary>
    public static readonly IReadOnlyList<uint> ExperienceSignature = new uint[]
    {
        400, 900, 1500, 2500, 4000, 7000, 11000, 17000,
    };

    /// <summary>Looks up a skill by id, or null when the id is outside 1..20.</summary>
    public static SkillInfo? Skill(int id)
    {
        foreach (var s in Skills)
            if (s.Id == id) return s;
        return null;
    }

    /// <summary>Looks up an attribute by id, or null when the id is outside 1..5.</summary>
    public static AttributeInfo? Attribute(int id)
    {
        foreach (var a in Attributes)
            if (a.Id == id) return a;
        return null;
    }

    /// <summary>Race name for <paramref name="id"/>, or a placeholder when the id is unknown.</summary>
    public static string RaceName(uint id) => id < (uint)Races.Count ? Races[(int)id] : $"Unknown ({id})";

    /// <summary>
    /// The reputation word the status screen shows for a fame value, reproducing the game's own
    /// ladder exactly — including that only +100 is "Saint" and only -100 is "Demonic".
    /// </summary>
    public static string FameBand(int fame)
    {
        if (fame == 100) return "Saint";
        if (fame > 79) return "Blessed";
        if (fame > 49) return "Blameless";
        if (fame > 19) return "Virtuous";
        if (fame > 0) return "Good";
        if (fame == 0) return "Neutral";
        if (fame == -100) return "Demonic";
        if (fame < -79) return "Pure evil";
        if (fame < -49) return "Evil";
        return fame > -20 ? "Immoral" : "Corrupt";
    }

    /// <summary>
    /// The wardrobe word the status screen shows for an outfit value. Outfit is summed from what the
    /// character is wearing, so the trainer reports the band but has nothing to write.
    /// </summary>
    public static string OutfitBand(int outfit)
    {
        if (outfit < 11) return "Threadbare";
        if (outfit < 21) return "Shabby";
        if (outfit < 41) return "Plain";
        if (outfit < 61) return "Regular";
        if (outfit < 81) return "Dressy";
        if (outfit < 91) return "Well dressed";
        return outfit > 95 ? "Swell" : "Fashionable";
    }
}
