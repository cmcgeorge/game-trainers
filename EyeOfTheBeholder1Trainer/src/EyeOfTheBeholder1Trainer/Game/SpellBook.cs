namespace EyeOfTheBeholder1Trainer.Game;

/// <summary>
/// Reference tables for the Eye of the Beholder I spell system — cleric spells (levels 1–5)
/// and mage spells (levels 1–5), as documented by GameBanshee and the game manual.
/// Paladins can cast a restricted set of cleric spells starting at 9th level:
/// Bless, Cure Light Wounds, Detect Magic, Protection from Evil, Slow Poison.
/// </summary>
public static class SpellBook
{
    public enum SpellSchool { Cleric, Mage }

    public sealed record SpellInfo(string Name, SpellSchool School, int Level, string Description);

    public static readonly IReadOnlyList<SpellInfo> Spells = new SpellInfo[]
    {
        // Cleric Level 1
        new("Bless", SpellSchool.Cleric, 1, "Raises party morale; +1 to attack rolls. Castable by paladins."),
        new("Cause Light Wounds", SpellSchool.Cleric, 1, "Inflicts 1–8 HP damage on one target."),
        new("Cure Light Wounds", SpellSchool.Cleric, 1, "Heals 1–8 HP on one character. Castable by paladins."),
        new("Detect Magic", SpellSchool.Cleric, 1, "Reveals magic items carried by the party. Castable by paladins."),
        new("Protection from Evil", SpellSchool.Cleric, 1, "Magical shell penalises evil attackers. Castable by paladins."),

        // Cleric Level 2
        new("Aid", SpellSchool.Cleric, 2, "Bless + 1–8 temporary HP to one character."),
        new("Flame Blade", SpellSchool.Cleric, 2, "Fiery blade in primary hand; 7–10 damage per hit."),
        new("Hold Person", SpellSchool.Cleric, 2, "Paralyses humanoids in one square."),
        new("Slow Poison", SpellSchool.Cleric, 2, "Delays poison damage. Castable by paladins."),

        // Cleric Level 3
        new("Create Food & Water", SpellSchool.Cleric, 3, "Conjures food for the entire party."),
        new("Dispel Magic", SpellSchool.Cleric, 3, "Negates hostile spells affecting the party."),
        new("Magical Vestment", SpellSchool.Cleric, 3, "Enchants robes to AC 5 (+1 per 3 levels above 5th)."),
        new("Prayer", SpellSchool.Cleric, 3, "Enhanced Bless: +party combat, −enemy combat."),
        new("Remove Paralysis", SpellSchool.Cleric, 3, "Counters Hold and Slow effects on 1–4 characters."),

        // Cleric Level 4
        new("Cause Serious Wounds", SpellSchool.Cleric, 4, "Inflicts 3–17 HP damage on one target."),
        new("Cure Serious Wounds", SpellSchool.Cleric, 4, "Heals 3–17 HP on one character."),
        new("Neutralize Poison", SpellSchool.Cleric, 4, "Detoxifies poison; cannot raise the dead."),
        new("Protection from Evil 10'", SpellSchool.Cleric, 4, "Party-wide Protection from Evil."),
        new("Protection from Lightning", SpellSchool.Cleric, 4, "Grants resistance to electrical attacks."),

        // Cleric Level 5
        new("Cause Critical Wounds", SpellSchool.Cleric, 5, "Inflicts 6–27 HP damage on one target."),
        new("Cure Critical Wounds", SpellSchool.Cleric, 5, "Heals 6–27 HP on one character."),
        new("Flame Strike", SpellSchool.Cleric, 5, "Column of flame; 6–48 damage to target square."),
        new("Raise Dead", SpellSchool.Cleric, 5, "Restores life to a non-elven character (−1 Con)."),

        // Mage Level 1
        new("Armor", SpellSchool.Mage, 1, "Magical field protecting as chain mail (AC 6)."),
        new("Burning Hands", SpellSchool.Mage, 1, "Flame jet; 1–3 + 2/level damage."),
        new("Detect Magic", SpellSchool.Mage, 1, "Reveals magic items carried by the party."),
        new("Magic Missile", SpellSchool.Mage, 1, "Unerring force bolt; 2–5 damage, +2–5 per 2 levels."),
        new("Shield", SpellSchool.Mage, 1, "Blocks Magic Missile; AC 2 vs hurled, AC 3 vs missiles."),
        new("Shocking Grasp", SpellSchool.Mage, 1, "Electrified hand touch; 1–8 + 1/level damage."),

        // Mage Level 2
        new("Invisibility", SpellSchool.Mage, 2, "Target vanishes until attacking or hit."),
        new("Melf's Acid Arrow", SpellSchool.Mage, 2, "Magic arrow; 2–8 damage, +1 attack per 3 levels."),
        new("Stinking Cloud", SpellSchool.Mage, 2, "Noxious vapor; chance of incapacitation."),

        // Mage Level 3
        new("Dispel Magic", SpellSchool.Mage, 3, "Negates hostile spells affecting the party."),
        new("Fireball", SpellSchool.Mage, 3, "Explosive flame; 1–6/level (max 10) damage."),
        new("Flame Arrow", SpellSchool.Mage, 3, "Flaming energy arrow; 3–30 damage (doubles at 10th)."),
        new("Haste", SpellSchool.Mage, 3, "Doubles move/attack rate for 1 target/level."),
        new("Hold Person", SpellSchool.Mage, 3, "Paralyses 1–4 humanoids."),
        new("Invisibility 10' Radius", SpellSchool.Mage, 3, "Party-wide invisibility; broken on attack."),
        new("Lightning Bolt", SpellSchool.Mage, 3, "Electric bolt; 1–6/level (max 10) damage, 2 squares."),
        new("Vampiric Touch", SpellSchool.Mage, 3, "Drains 1–6/2 levels HP; transfers to caster."),

        // Mage Level 4
        new("Fear", SpellSchool.Mage, 4, "Cone of terror; creatures flee the party."),
        new("Ice Storm", SpellSchool.Mage, 4, "Hailstorm; 3–30 damage in 3×3 area."),
        new("Stoneskin", SpellSchool.Mage, 4, "Immunity to non-magical attacks; 1–4 + 1/2 levels hits."),

        // Mage Level 5
        new("Cloudkill", SpellSchool.Mage, 5, "Poison cloud; kills lesser monsters."),
        new("Cone of Cold", SpellSchool.Mage, 5, "Sub-zero cone; 2–5/level damage."),
        new("Hold Monster", SpellSchool.Mage, 5, "Paralyses non-undead monsters in one square."),
    };

    public static IReadOnlyList<SpellInfo> ClericSpells =>
        Spells.Where(s => s.School == SpellSchool.Cleric).ToList();

    public static IReadOnlyList<SpellInfo> MageSpells =>
        Spells.Where(s => s.School == SpellSchool.Mage).ToList();
}
