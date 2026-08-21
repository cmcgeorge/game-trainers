namespace DarksypreTrainer.Game;

/// <summary>One monster type in DarkSpyre. [Confirmed from walkthrough]</summary>
public sealed record MonsterEntry(
    string Name,
    string Category,
    string Movement,
    string Attack,
    string Tactics);

/// <summary>
/// Curated monster roster for DarkSpyre, organized by combat category.
/// Monsters become exponentially harder on higher levels and work in larger groups.
/// [Confirmed from walkthrough]
/// </summary>
internal static class MonsterBook
{
    public static IReadOnlyList<MonsterEntry> Monsters { get; } = new[]
    {
        new MonsterEntry("Wraith",        "Ground Melee",      "Walk",      "Hand-to-hand",
            "Use hurled/projectile weapons and fireballs at a distance. Most common monster type."),
        new MonsterEntry("Crustacean",    "Ground Melee",      "Walk",      "Hand-to-hand",
            "Use hurled/projectile weapons and fireballs at a distance."),
        new MonsterEntry("Samurai",       "Ground Melee",      "Walk",      "Hand-to-hand",
            "Use hurled/projectile weapons and fireballs at a distance."),
        new MonsterEntry("Gargoyle",      "Ground Melee",      "Walk",      "Hand-to-hand",
            "Use hurled/projectile weapons and fireballs at a distance."),
        new MonsterEntry("Crystal Ninja", "Ground Melee",      "Walk",      "Hand-to-hand",
            "Use hurled/projectile weapons and fireballs at a distance."),
        new MonsterEntry("Jester",        "Ground Projectile", "Walk",      "Fireballs + gas",
            "Only monster that walks and uses projectiles. Fireballs hit in the 40s. " +
            "Smoke clouds make you miss. Cast Hold/Freeze or fireball spam them quickly."),
        new MonsterEntry("Slime",         "Slither Poison",    "Slither",   "Poison on contact",
            "Do not activate weight plates. Immune to all projectile attacks. " +
            "Slither under gates and blocks. Avoiding is best."),
        new MonsterEntry("Creeper",       "Slither Poison",    "Slither",   "Poison on contact",
            "Do not activate weight plates. Immune to projectile attacks. " +
            "Can melee safely: attack once, run away, repeat."),
        new MonsterEntry("Vulture",       "Flying Melee",      "Fly",       "Hand-to-hand",
            "Do not trigger weight plates. Use hurled/projectile weapons and fireballs."),
        new MonsterEntry("Manta Ray",     "Flying Melee",      "Fly",       "Hand-to-hand",
            "Do not trigger weight plates. Use hurled/projectile weapons and fireballs."),
        new MonsterEntry("Beholder",      "Flying Projectile", "Fly",       "Fireballs",
            "Get into h2h range to suppress projectiles, or cast Hold/Freeze. " +
            "Weak melee attack."),
        new MonsterEntry("Electric Storm","Flying Projectile", "Fly",       "Magic gas (smoke)",
            "Get into h2h range to suppress projectiles, or cast Hold/Freeze. " +
            "Weak melee attack."),
        new MonsterEntry("Banshee",       "Flying Projectile", "Fly",       "Bolts",
            "Get into h2h range to suppress projectiles, or cast Hold/Freeze. " +
            "Weak melee attack."),
        new MonsterEntry("Djinn",         "Flying Projectile", "Fly",       "Magic gas (poison)",
            "Get into h2h range to suppress projectiles, or cast Hold/Freefreeze. " +
            "Weak melee attack."),
    };

    public static IReadOnlyList<string> Categories { get; } =
        Monsters.Select(m => m.Category).Distinct().ToList();
}
