namespace SwordOfAragonTrainer.Game;

/// <summary>One spell: the level each casting class needs, and what it does.</summary>
/// <param name="Name">Full name.</param>
/// <param name="MenuLabel">The five-character label the battle menu shows.</param>
/// <param name="RangerLevel">Level a Ranger needs, or 0 if Rangers never learn it.</param>
/// <param name="PriestLevel">Level a Priest needs, or 0.</param>
/// <param name="MageLevel">Level a Mage needs, or 0.</param>
public sealed record Spell(
    string Name, string MenuLabel, int RangerLevel, int PriestLevel, int MageLevel, string Effect)
{
    /// <summary>Renders a class's requirement as text for a table.</summary>
    public static string Requirement(int level) => level > 0 ? level.ToString() : "—";

    public string RangerAt => Requirement(RangerLevel);
    public string PriestAt => Requirement(PriestLevel);
    public string MageAt => Requirement(MageLevel);
}

/// <summary>
/// The 23 tactical spells. The per-class level ladder is the one <c>HEXWAR.EXE</c> carries as two rows
/// of six labels per class (<c>Grow Dry Light Withr Mud Vigor</c> / <c>Rally Xhaus Heal Fear Brdge
/// Tower</c> for Rangers, and so on), which matches rule-book Appendix II exactly; the descriptions
/// are the rule book's.
/// </summary>
public static class SpellBook
{
    public static readonly IReadOnlyList<Spell> Spells = new[]
    {
        new Spell("Bless", "Bless", 0, 5, 0,
            "Defensive bonus to the caster's whole army for one turn; value varies by level."),
        new Spell("Bridge", "Brdge", 11, 0, 6,
            "Creates a pathway across a river hex."),
        new Spell("Confuse", "Confu", 0, 0, 3,
            "Tries to dislodge enemy units from an entrenched position in a hex."),
        new Spell("Cure", "Cure", 0, 11, 0,
            "Restores a percentage of lost hits to all units in the caster's hex."),
        new Spell("Disintegrate", "Disnt", 0, 12, 11,
            "Damages structures, walls and every unit in a hex — including your own."),
        new Spell("Dry", "Dry", 2, 0, 0,
            "Reduces the muddiness of a hex."),
        new Spell("Fear", "Fear", 10, 7, 4,
            "Drops enemy morale in a hex; can disperse units, which missile fire cannot."),
        new Spell("Gate", "Gate", 0, 0, 12,
            "Summons a Troll or Demon to fight for you; it arrives with zero movement."),
        new Spell("Grow", "Grow", 1, 0, 0,
            "Increases vegetation in a hex; fails if the hex has none to begin with."),
        new Spell("Haste", "Haste", 0, 0, 7,
            "Adds movement to units in the caster's hex. Drains stamina and can cause damage if " +
            "stamina falls below zero. Cast at the start of movement — the bonus is a percentage of " +
            "the current allowance."),
        new Spell("Heal", "Heal", 9, 6, 0,
            "Restores lost hits to one unit in the caster's hex."),
        new Spell("Light", "Light", 3, 2, 1,
            "Illuminates a radius and reveals all units in lit hexes; blocked hexes stay dark."),
        new Spell("Mud", "Mud", 5, 0, 5,
            "Adds or deepens mud in a hex (drawn as dashed horizontal lines)."),
        new Spell("Prayer", "Prayr", 0, 8, 0,
            "Army-wide defensive bonus that persists between turns, decaying 75 % per turn."),
        new Spell("Pyrotechnics", "Pyro", 0, 0, 8,
            "Multi-hex attack centred on the target hex; never harms your own or allied units."),
        new Spell("Quake", "Quake", 0, 10, 9,
            "Reduces structures and walls in a hex; does no damage to units."),
        new Spell("Rally", "Rally", 7, 3, 0,
            "Restores lost morale to all units in the caster's hex."),
        new Spell("Slow", "Slow", 0, 0, 2,
            "Cuts the movement available to all enemy units in a hex during their next turn."),
        new Spell("Teleport", "Telpt", 0, 0, 10,
            "Moves every unit in the caster's hex, caster included, to a new destination."),
        new Spell("Tower", "Tower", 12, 9, 0,
            "Builds a fortification-like structure in a clear, non-town hex."),
        new Spell("Vigor", "Vigor", 6, 1, 0,
            "Restores lost stamina to all units in the caster's hex."),
        new Spell("Wither", "Withr", 4, 0, 0,
            "Reduces the vegetation in a hex."),
        new Spell("Exhaust", "Xhaus", 8, 4, 0,
            "Drains an enemy unit's stamina."),
    };

    /// <summary>Highest caster level the ladder uses.</summary>
    public const int MaxCasterLevel = 12;

    /// <summary>The spells a class can cast at a given level, in ladder order.</summary>
    public static IEnumerable<Spell> Available(int classCode, int level) => classCode switch
    {
        8 => Spells.Where(s => s.RangerLevel > 0 && s.RangerLevel <= level).OrderBy(s => s.RangerLevel),
        9 => Spells.Where(s => s.PriestLevel > 0 && s.PriestLevel <= level).OrderBy(s => s.PriestLevel),
        10 => Spells.Where(s => s.MageLevel > 0 && s.MageLevel <= level).OrderBy(s => s.MageLevel),
        _ => Enumerable.Empty<Spell>(),
    };
}
