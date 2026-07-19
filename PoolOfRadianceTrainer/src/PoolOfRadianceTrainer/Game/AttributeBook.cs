namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// One of the six primary AD&amp;D abilities (or the exceptional-strength / hit-point extras):
/// its abbreviation, full name, what the game uses it for (<see cref="Role"/>), and a practical
/// build note (<see cref="InPlay"/>). Reference only — nothing here is written to the game.
/// </summary>
public sealed record AbilityInfo(string Abbr, string Name, string Role, string InPlay)
{
    /// <summary>A one-paragraph blurb (full name + role + practical note) for a tooltip.</summary>
    public string Description => $"{Name} ({Abbr}) — {Role} {InPlay}";
}

/// <summary>
/// Reference descriptions of Pool of Radiance's six primary abilities, exceptional strength, and
/// hit points: what each does in the game and how it matters when building a character. The role
/// text follows AD&amp;D 1st-edition rules (the system Pool of Radiance implements); the in-play
/// notes follow the bundled strategy guide (<c>.docs</c>). Reference only — nothing here is
/// written to the game.
///
/// The list is ordered to match <see cref="PorFormat.Stats"/>/ <see cref="PorFormat.StatsShort"/>
/// exactly (STR, INT, WIS, DEX, CON, CHA), so <see cref="ByIndex"/> lines up with
/// <see cref="CharacterRecord.GetStat"/>.
/// </summary>
public static class AttributeBook
{
    public static readonly IReadOnlyList<AbilityInfo> Abilities = new AbilityInfo[]
    {
        new("STR", "Strength",
            "raw physical power — melee attack and damage rolls, how much the character can carry "
            + "(encumbrance), and the chance to force doors and bend bars. Fighters with Strength 18 "
            + "also roll an exceptional-strength percentile (18/01–18/00) that further boosts hit and "
            + "damage.",
            "Best on a front-line fighter; a high Strength with a good percentile is the single biggest "
            + "melee edge a starting character can roll."),
        new("INT", "Intelligence",
            "the mage's casting stat: it sets the maximum spell level a mage can learn and how many "
            + "spells of each level can be known. It also governs the number of languages the character "
            + "can read.",
            "Essential for any mage; a 9+ lets a ranger or paladin cast mage spells later. Irrelevant to "
            + "pure fighters and clerics."),
        new("WIS", "Wisdom",
            "the cleric's casting stat: it sets the maximum spell level a cleric can use and grants "
            + "bonus spells and a save bonus against mental magic at high values.",
            "Essential for a cleric or druid; a 13+ gives a save bonus and a 16+ starts granting extra "
            + "low-level spells per day."),
        new("DEX", "Dexterity",
            "agility and reflexes — ranged attack rolls, an Armor Class bonus (the higher the Dexterity, "
            + "the lower the AC), reaction adjustments, and most thief skills (pick pockets, move "
            + "silently, hide in shadows).",
            "Useful on everyone for the AC bonus; critical for archers and thieves."),
        new("CON", "Constitution",
            "health and hardiness — it adds a per-level bonus (or penalty) to hit points, improves the "
            + "system-shock survival roll, and grants a bonus against poison. The hit-point bonus applies "
            + "to every level gained, so a high Constitution pays off for the character's whole career.",
            "Strong on every class; a 16+ adds +2 HP per level for most classes, which adds up fast."),
        new("CHA", "Charisma",
            "force of personality — reaction adjustments with NPCs, the number of henchmen a character "
            + "can hire, and (for paladins) the ability to turn undead. It has almost no combat effect.",
            "Usually the dump stat; keep it above 3 to avoid penalties, but only a spokesperson or paladin "
            + "needs it high."),
    };

    /// <summary>The ability at record index <paramref name="index"/> (0..5), or null if out of range.</summary>
    public static AbilityInfo? ByIndex(int index) =>
        index >= 0 && index < Abilities.Count ? Abilities[index] : null;

    /// <summary>The tooltip blurb for the ability at <paramref name="index"/>, or "" if out of range.</summary>
    public static string DescriptionOf(int index) => ByIndex(index)?.Description ?? "";

    /// <summary>Description of the exceptional-strength percentile (STR%), shown as its own row.</summary>
    public static AbilityInfo StrPercent { get; } = new(
        "STR%", "Exceptional Strength",
        "a percentile rolled only for fighters (and paladins/rangers) whose Strength is exactly 18, "
        + "giving a value from 18/01 to 18/00 (100). A higher percentile adds to melee hit and damage "
        + "and to bend-bars/lift-gates rolls — 18/00 is the strongest a starting fighter can be.",
        "Only matters when Strength is 18 and the class is fighter, paladin, or ranger; re-rolling for "
        + "a high percentile is the classic fighter-build goal.");

    /// <summary>Description of hit points (HP), shown as its own row when the record is confirmed.</summary>
    public static AbilityInfo HitPoints { get; } = new(
        "HP", "Hit Points",
        "the character's life total — how much damage they can take before falling. Rolled at creation "
        + "from the class hit die (e.g. d10 for fighters, d8 for clerics, d6 for mages/thieves) plus the "
        + "Constitution per-level bonus, so a fighter with Constitution 16+ starts around 10–12 HP.",
        "Higher is always tougher; combined with Constitution it sets how long a character survives the "
        + "early game before they can afford raise-dead.");
}
