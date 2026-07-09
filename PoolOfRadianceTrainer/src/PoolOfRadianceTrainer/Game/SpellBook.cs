namespace PoolOfRadianceTrainer.Game;

public sealed record SpellInfo(string School, int Level, string Name, string Description);

/// <summary>
/// The Pool of Radiance spell list, as enumerated by the character-record's known/memorized
/// spell tables (formats.zip) with descriptions distilled from Stephen S. Lee's FAQ.
/// Reference only. School = "Cleric" or "Mage" (magic-user).
/// </summary>
public static class SpellBook
{
    public static readonly IReadOnlyList<SpellInfo> All = new List<SpellInfo>
    {
        // --- Cleric level 1 ---
        new("Cleric", 1, "Bless", "Allies not yet adjacent to a foe get +1 to hit and +5 morale. Cast before contact."),
        new("Cleric", 1, "Curse", "Enemies get -1 to hit and -5 morale (no save) — helps force surrenders."),
        new("Cleric", 1, "Cure Light Wounds", "Restores 1d8 HP. The ONLY castable healing spell in the game — keep several ready."),
        new("Cleric", 1, "Cause Light Wounds", "Touch attack for 1d8 damage."),
        new("Cleric", 1, "Detect Magic", "Flags magic items."),
        new("Cleric", 1, "Protection from Evil", "-2 AC and saves vs evil creatures."),
        new("Cleric", 1, "Protection from Good", "-2 AC and saves vs good creatures."),
        new("Cleric", 1, "Resist Cold", "Cold resistance (little used — nothing deals cold)."),
        // --- Cleric level 2 ---
        new("Cleric", 2, "Find Traps", "Reveals traps ahead."),
        new("Cleric", 2, "Hold Person", "Paralyzes up to 3 humanoids; a held target is auto-killed by any hit. Single-target = -2 to its save."),
        new("Cleric", 2, "Resist Fire", "Halves fire damage — mitigates the final dragon's fiery aura."),
        new("Cleric", 2, "Silence 15' Radius", "Shuts down enemy spellcasters in the area."),
        new("Cleric", 2, "Slow Poison", "Temporarily staves off a poisoned character's death."),
        new("Cleric", 2, "Snake Charm", "Neutralizes snakes."),
        new("Cleric", 2, "Spiritual Hammer", "Conjures a striking hammer at range."),
        // --- Cleric level 3 ---
        new("Cleric", 3, "Animate Dead", "Raises slain foes as temporary skeleton/zombie allies."),
        new("Cleric", 3, "Cure Blindness", "Cures blindness."),
        new("Cleric", 3, "Cause Blindness", "Blinds a foe."),
        new("Cleric", 3, "Cure Disease", "Cures disease (e.g. from mummies)."),
        new("Cleric", 3, "Cause Disease", "Inflicts disease."),
        new("Cleric", 3, "Dispel Magic", "Counters hostile charm/hold; base 50% ± level difference."),
        new("Cleric", 3, "Prayer", "Party-wide +1 to hit & saves for allies, -1 for enemies (no save)."),
        new("Cleric", 3, "Remove Curse", "Removes a curse / lets a cursed item be unequipped."),
        new("Cleric", 3, "Bestow Curse", "Curses a foe."),

        // --- Mage level 1 ---
        new("Mage", 1, "Burning Hands", "Short-range fire cone."),
        new("Mage", 1, "Charm Person", "Charms a humanoid to your side (no XP for a blue-named kill)."),
        new("Mage", 1, "Detect Magic", "Flags magic items."),
        new("Mage", 1, "Enlarge", "Raises a target's Strength, up to 18/00 at caster level 6 — a top fighter buff."),
        new("Mage", 1, "Reduce", "Shrinks a target."),
        new("Mage", 1, "Friends", "Brief Charisma boost (too short to matter)."),
        new("Mage", 1, "Magic Missile", "1d4+1 per missile, unerring, no save. +1 missile at each odd level."),
        new("Mage", 1, "Protection from Evil", "-2 AC & saves vs evil."),
        new("Mage", 1, "Protection from Good", "-2 AC & saves vs good — works on the Lawful-Good possessed dragon!"),
        new("Mage", 1, "Read Magic", "Identifies a scroll so it can be scribed."),
        new("Mage", 1, "Shield", "Immunity to Magic Missile + good AC vs missiles; +1 saves."),
        new("Mage", 1, "Shocking Grasp", "Electric touch attack."),
        new("Mage", 1, "Sleep", "THE early-game king. Sleeps 4d4 HD of <6-HD creatures, NO save; sleeping foes die to one hit."),
        // --- Mage level 2 ---
        new("Mage", 2, "Detect Invisibility", "Reveals invisible creatures."),
        new("Mage", 2, "Invisibility", "Target is invisible until it attacks/casts. Great pre-combat setup."),
        new("Mage", 2, "Knock", "Opens locked/stuck doors — needed for Mendor's Library."),
        new("Mage", 2, "Mirror Image", "1d4 decoys absorb single-target hits — excellent vs level-drainers."),
        new("Mage", 2, "Ray of Enfeeblement", "Weakens a foe (unreliable at low level)."),
        new("Mage", 2, "Stinking Cloud", "2x2 cloud; failed poison save = helpless 1d4+1 rounds. AI won't enter it — walls off doorways."),
        new("Mage", 2, "Strength", "Raises a target's Strength."),
        // --- Mage level 3 ---
        new("Mage", 3, "Blink", "Caster can only be hit by area effects after acting."),
        new("Mage", 3, "Dispel Magic", "Counters hostile magic."),
        new("Mage", 3, "Fireball", "(level)d6 fire, save for half. Diameter 5 outdoors / 7 indoors. No damage cap — clears hordes."),
        new("Mage", 3, "Haste", "Doubles allies' movement AND attacks (ages each 1 year). Cast just before the boss."),
        new("Mage", 3, "Hold Person", "Mage version: holds up to 4 humanoids."),
        new("Mage", 3, "Invisibility 10' Radius", "Party-wide invisibility."),
        new("Mage", 3, "Lightning Bolt", "(level)d6 electrical line that bounces off walls (can hit twice — or your own party)."),
        new("Mage", 3, "Protection from Evil 10' Radius", "Area protection vs evil."),
        new("Mage", 3, "Protection from Good 10' Radius", "Area protection vs good."),
        new("Mage", 3, "Protection from Normal Missiles", "Immunity to non-magical arrows/bolts."),
        new("Mage", 3, "Slow", "Halves enemy movement and attacks (no save)."),
    };

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
