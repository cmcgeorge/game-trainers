namespace CurseOfTheAzureBondsTrainer.Game;

public sealed record SpellInfo(string School, int Level, string Name, string Description);

/// <summary>
/// The Curse of the Azure Bonds spell list, transcribed from the game's own Rule Book
/// (<c>curseazure.pdf</c>, "THE SPELLS", pages 18–22), so the effects described are the ones this
/// game implements rather than the tabletop versions they are based on. Reference only.
///
/// <para>School is "Cleric", "Druid" (the four first-level druid spells rangers cast) or "Mage".
/// Curse reaches <b>fifth-level</b> spells in both priest and mage lists, which is the headline
/// difference from the sister game's three — and the reason the character record's spell blocks are
/// four times the size (see <see cref="CoabFormat.OffMemorizedSpells"/>). The list below holds 84
/// spells, which is exactly the length of that memorized-spell block.</para>
/// </summary>
public static class SpellBook
{
    public static readonly IReadOnlyList<SpellInfo> All = new List<SpellInfo>
    {
        // --- Cleric level 1 ---
        new("Cleric", 1, "Bless", "Improves the THAC0 of friendly characters by 1. Does not affect characters already adjacent to a monster when it is cast — so cast it before contact."),
        new("Cleric", 1, "Curse", "Reduces the THAC0 of monsters by 1, no saving throw. Does not affect monsters already adjacent to a friendly character."),
        new("Cleric", 1, "Cure Light Wounds", "Heals 1–8 HP. The workhorse — memorize as many as the slots allow."),
        new("Cleric", 1, "Cause Light Wounds", "Causes 1–8 HP. No saving throw."),
        new("Cleric", 1, "Detect Magic", "Marks magical equipment and treasure with an asterisk when you View items or Take treasure."),
        new("Cleric", 1, "Protection from Evil", "Improves the target's AC and saving throws by 2 against evil attackers."),
        new("Cleric", 1, "Protection from Good", "Improves the target's AC and saving throws by 2 against good attackers."),
        new("Cleric", 1, "Resist Cold", "Halves cold damage and improves saving throws versus cold by 3."),

        // --- Cleric level 2 ---
        new("Cleric", 2, "Find Traps", "Indicates traps in the character's path."),
        new("Cleric", 2, "Hold Person", "Paralyzes targets of roughly human size and shape; you may aim it at up to 3 targets. A held target is easy to finish."),
        new("Cleric", 2, "Resist Fire", "Halves fire damage and improves saving throws versus fire by 3."),
        new("Cleric", 2, "Silence 15' Radius", "The target and everything adjacent to it cannot cast spells for the duration — the answer to enemy casters."),
        new("Cleric", 2, "Slow Poison", "Revives a poisoned character for the duration. He dies when it wears off, so cure the poison before then."),
        new("Cleric", 2, "Snake Charm", "Paralyzes as many hit points of snakes as the cleric has hit points."),
        new("Cleric", 2, "Spiritual Hammer", "Creates a temporary magic hammer, automatically readied, that strikes at range for normal hammer damage."),

        // --- Cleric level 3 ---
        new("Cleric", 3, "Cure Blindness", "Removes the effect of a cause blindness spell."),
        new("Cleric", 3, "Cause Blindness", "Reduces the target's THAC0, Armor Class and saving throws."),
        new("Cleric", 3, "Cure Disease", "Removes disease caused by monsters or by a cause disease spell."),
        new("Cleric", 3, "Cause Disease", "Gives the target a disease that saps Strength and hit points."),
        new("Cleric", 3, "Dispel Magic", "Removes the effects of spells that have no specific counter spell."),
        new("Cleric", 3, "Prayer", "Improves friendly THAC0 and saving throws by 1 and reduces the monsters' by 1 — a two-point swing, and the best round-one buff a cleric has."),
        new("Cleric", 3, "Remove Curse", "Removes a bestow curse, and lets the target unready cursed magic items."),
        new("Cleric", 3, "Bestow Curse", "Reduces the target's THAC0 and saving throw by 4."),

        // --- Cleric level 4 ---
        new("Cleric", 4, "Cure Serious Wounds", "Heals 3–17 HP."),
        new("Cleric", 4, "Cause Serious Wounds", "Causes 3–17 HP. No saving throw."),
        new("Cleric", 4, "Neutralize Poison", "Revives a poisoned character properly, unlike slow poison."),
        new("Cleric", 4, "Poison", "The target saves versus poison or dies."),
        new("Cleric", 4, "Protection from Evil 10' Radius", "Improves the AC and saving throws of the target and all adjacent friendly characters by 2 against evil attackers."),
        new("Cleric", 4, "Sticks to Snakes", "Snakes harass the target: it cannot attack, move or cast spells for the duration."),

        // --- Cleric level 5 ---
        new("Cleric", 5, "Cure Critical Wounds", "Heals 6–27 HP."),
        new("Cleric", 5, "Cause Critical Wounds", "Causes 6–27 HP. No saving throw."),
        new("Cleric", 5, "Dispel Evil", "Improves the target's AC by 7 versus summoned evil creatures until it hits one; the creature must then save versus spells or be dispelled."),
        new("Cleric", 5, "Flame Strike", "Does 6–48 HP to the target; half on a successful save versus magic."),
        new("Cleric", 5, "Raise Dead", "Returns any non-elf player character to life."),
        new("Cleric", 5, "Slay Living", "The target saves versus death or dies; on a save it still takes 3–17 HP."),

        // --- Druid level 1 (rangers) ---
        new("Druid", 1, "Detect Magic", "Marks magical equipment and treasure with an asterisk."),
        new("Druid", 1, "Entangle", "Reduces the target's movement to 0. Outdoors only."),
        new("Druid", 1, "Faerie Fire", "Illuminates the enemy and reduces their AC by 2."),
        new("Druid", 1, "Invisibility to Animals", "Reduces every attacking animal's THAC0 by 4. No effect on intelligent targets or enchanted beasts."),

        // --- Mage level 1 ---
        new("Mage", 1, "Burning Hands", "1 HP of fire damage per level of the caster. No saving throw."),
        new("Mage", 1, "Charm Person", "Changes the target's allegiance in combat. Human-sized targets only."),
        new("Mage", 1, "Detect Magic", "Marks magical equipment and treasure with an asterisk."),
        new("Mage", 1, "Enlarge", "Makes the target larger and stronger, more so the higher the caster's level — at 6th level the target is as strong as an ogre. Unwilling targets get a save."),
        new("Mage", 1, "Reduce", "Negates an enlarge spell."),
        new("Mage", 1, "Friends", "Raises the caster's Charisma by 2–8 points. Cast it just before an encounter you intend to parley."),
        new("Mage", 1, "Magic Missile", "2–5 HP per missile, no saving throw: 1 missile at level 1–2, 2 at 3–4, 3 at 5–6, 4 at 7–8, 5 at 9–10, 6 at 11."),
        new("Mage", 1, "Protection from Evil", "Improves the target's AC and saving throws by 2 against evil attackers."),
        new("Mage", 1, "Protection from Good", "Improves the target's AC and saving throws by 2 against good attackers."),
        new("Mage", 1, "Read Magic", "Lets a magic-user ready and identify a scroll; the scroll's spells are usable afterwards."),
        new("Mage", 1, "Shield", "Negates magic missile, improves the caster's saving throw and improves his AC."),
        new("Mage", 1, "Shocking Grasp", "1–8 HP of electrical damage, +1 HP per level of the caster."),
        new("Mage", 1, "Sleep", "Puts 1–16 targets to sleep with no saving throw. Up to sixteen 1-hit-die targets, or one 4-hit-die target; 5 hit dice and above are immune. The early game's decider."),

        // --- Mage level 2 ---
        new("Mage", 2, "Detect Invisibility", "Lets the target spot invisible targets."),
        new("Mage", 2, "Invisibility", "Melee THAC0 against the target is reduced by 4 and it cannot be targeted by ranged attacks. Dispelled when the target attacks."),
        new("Mage", 2, "Knock", "Opens locks. Castable straight from the door-opening menu if the active character has it memorized."),
        new("Mage", 2, "Mirror Image", "Creates 1–4 illusory duplicates of the magic-user; each disappears when attacked."),
        new("Mage", 2, "Ray of Enfeeblement", "Reduces the target's Strength by 25% + 2% per level of the caster."),
        new("Mage", 2, "Stinking Cloud", "Paralyzes those in its area for 2–5 rounds. On a save the target is merely nauseous, with reduced AC for 2 rounds."),
        new("Mage", 2, "Strength", "Raises the target's Strength by 1–8 points, depending on the target's class."),

        // --- Mage level 3 ---
        new("Mage", 3, "Blink", "The magic-user blinks out after acting each round: he can be attacked before he acts, but not after."),
        new("Mage", 3, "Dispel Magic", "Removes the effects of spells that have no specific counter spell."),
        new("Mage", 3, "Fireball", "1d6 HP per caster level to everything in the area, halved on a save. Radius 2 outdoors, 3 indoors — mind your own party."),
        new("Mage", 3, "Haste", "Doubles the target's movement and melee attacks per round. The single best combat buff in the game."),
        new("Mage", 3, "Hold Person", "Paralyzes human-shaped targets; the mage version may be aimed at up to 4."),
        new("Mage", 3, "Invisibility, 10' Radius", "Makes everything adjacent to the caster invisible. Dispelled for a target when it attacks."),
        new("Mage", 3, "Lightning Bolt", "1d6 HP per caster level in a line 4 or 8 squares long, halved on a save. The bolt rebounds off walls to reach its full length — which can bring it back through your own party."),
        new("Mage", 3, "Protection from Evil, 10' Radius", "Improves the AC and saving throws of the target and everything adjacent by 2 against evil attackers."),
        new("Mage", 3, "Protection from Good, 10' Radius", "Improves the AC and saving throws of the target and everything adjacent by 2 against good attackers."),
        new("Mage", 3, "Protection from Normal Missiles", "Makes the target immune to non-magical missiles."),
        new("Mage", 3, "Slow", "Affects 1 target per caster level, halving movement and melee attacks. Negates haste."),

        // --- Mage level 4 ---
        new("Mage", 4, "Charm Monster", "Changes the target's allegiance in combat; works on any living creature. Affects 2–8 first-level targets, 1–4 second, 1–2 third, or 1 of fourth level and above."),
        new("Mage", 4, "Confusion", "Affects 2–16 targets. Each must save every round or stand confused, become enraged, flee in terror, or go berserk."),
        new("Mage", 4, "Dimension Door", "Teleports the magic-user to another point on the battlefield."),
        new("Mage", 4, "Fear", "Everything within the area flees."),
        new("Mage", 4, "Fire Shield", "Anything that hits the magic-user in melee takes double the damage it deals. Attuned to heat or to cold: he takes half (none on a save) from the opposite form and double from the attuned one."),
        new("Mage", 4, "Fumble", "The target cannot move or attack; on a save it is merely slowed."),
        new("Mage", 4, "Ice Storm", "3–30 HP to everything in the area. No saving throw."),
        new("Mage", 4, "Minor Globe of Invulnerability", "Protects the caster from incoming first-, second- and third-level spells."),
        new("Mage", 4, "Remove Curse", "Removes a bestow curse, and lets the target unready cursed magic items."),
        new("Mage", 4, "Bestow Curse", "Reduces the target's THAC0 and saving throw by 4."),

        // --- Mage level 5 ---
        new("Mage", 5, "Cloudkill", "Instantly kills creatures of 4 or fewer hit dice. 4+1 to 5+1 hit dice save versus poison at -4 or die; up to 6 hit dice save versus poison or die."),
        new("Mage", 5, "Cone of Cold", "1d4+1 HP per caster level to everything in a cone, halved on a save."),
        new("Mage", 5, "Feeblemind", "Drops the target's Intelligence and Wisdom to 3 so it cannot cast. A human magic-user saves at -4, a human cleric at +1, a non-human at -2. Only a temple's heal spell removes it."),
        new("Mage", 5, "Hold Monster", "Paralyzes up to 4 targets; works on any living creature."),
    };

    public static IEnumerable<SpellInfo> ForSchool(string school) =>
        All.Where(s => string.Equals(s.School, school, StringComparison.OrdinalIgnoreCase));

    /// <summary>Filter the spell list by a case-insensitive substring of the name, school or effect.</summary>
    public static IEnumerable<SpellInfo> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return All;
        term = term.Trim();
        return All.Where(s =>
            s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            s.School.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
