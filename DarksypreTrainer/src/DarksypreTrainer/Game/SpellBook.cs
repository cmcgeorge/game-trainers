namespace DarksypreTrainer.Game;

/// <summary>One spell in the DarkSpyre magic system. [Confirmed from manual and walkthrough]</summary>
public sealed record Spell(
    string Name,
    string Class,
    int SpCost,
    string Description);

/// <summary>
/// All confirmed spells in DarkSpyre, grouped by the six magic classes.
/// Spells are found on scrolls throughout the game; each can be cast once from a scroll
/// or permanently added to the spell book (found on Level 1).
/// SP cost is split 50/50 between preparation and casting.
/// </summary>
internal static class SpellBook
{
    public enum MagicClass { Healing, Sorcery, Wizardry, Conjury, Diviny, Enchantry }

    public static readonly string[] ClassNames =
    {
        "Healing", "Sorcery", "Wizardry", "Conjury", "Diviny", "Enchantry"
    };

    public static readonly string[] ProficiencyNames =
    {
        "None", "Novice", "Average", "Skilled", "Sage", "Maren", "Master"
    };

    public static IReadOnlyList<Spell> Spells { get; } = new[]
    {
        new Spell("Liquify",   "Healing",   10, "Creates a potion. With empty chalice: Jera Potion (heal HP). " +
            "With emerald: Isa Potion (poison). With ruby: Algit Potion (cure poison). " +
            "With amethyst: Teiwaz Potion (restore attributes). With diamond: Ambrosia (boost HP/SP/ENC)."),
        new Spell("Knock",     "Sorcery",   16, "Opens some gates."),
        new Spell("Zap Away",  "Sorcery",   10, "Teleports large blocks and balls to another part of the level."),
        new Spell("Hold",      "Sorcery",   30, "Prevents targeted monster from moving or attacking."),
        new Spell("Fireball",  "Wizardry",  20, "High damage attack spell. Can bounce off some walls."),
        new Spell("Magic Gas", "Wizardry",  20, "Projectile that explodes into a gas cloud — confusion (below skilled) " +
            "or poison cloud (skilled or above)."),
        new Spell("Abstraka",  "Conjury",   20, "Invisibility. Cast again to become visible."),
        new Spell("Disguise",  "Conjury",   30, "Temporarily look like a monster. Attacking cancels the spell."),
        new Spell("Magic Wall","Conjury",   30, "A temporary, moveable wall. Helpful for puzzles at Sage level."),
        new Spell("Compass",   "Diviny",    30, "Creates a pulsing aura showing the general direction of the exit."),
        new Spell("Magic Map", "Diviny",    30, "Gives you a map of the level."),
        new Spell("Sight",     "Diviny",    10, "Enlarges items on the ground."),
        new Spell("Dispel",    "Enchantry", 36, "Defensive dispel. Fails more than other spell types."),
        new Spell("Freeze",    "Enchantry", 40, "Temporarily stops all monsters from moving or attacking."),
    };

    public static IReadOnlyList<Spell> ByClass(string className) =>
        Spells.Where(s => s.Class == className).ToList();
}
