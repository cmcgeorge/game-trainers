namespace DarksypreTrainer.Game;

/// <summary>One weapon proficiency type in DarkSpyre. [Confirmed from manual and walkthrough]</summary>
public sealed record WeaponType(
    int Id,
    string Name,
    string Speed,
    string Damage,
    string Hands,
    string Notes);

/// <summary>
/// The seven weapon proficiency classes in DarkSpyre. Using a weapon increases proficiency
/// for all weapons in that class. Higher proficiency unlocks more attack options and damage.
/// [Confirmed from manual and walkthrough]
/// </summary>
internal static class WeaponBook
{
    public static readonly string[] ProficiencyNames =
    {
        "None", "Beginner", "Neophyte", "Novice", "Average",
        "Skilled", "Stalwart", "Adept", "Savant", "Expert"
    };

    public static IReadOnlyList<WeaponType> Types { get; } = new[]
    {
        new WeaponType(0, "Clubbing",  "Average",      "Average",     "1-handed",
            "Swung or clubbed at the enemy. Examples: War Axe, Mace. " +
            "Attacking with a shield increases clubbing proficiency."),
        new WeaponType(1, "Hurled",    "Fast",         "Low",         "1-handed",
            "Thrown weapons. Examples: Throwing Knife, Throwing Axe. " +
            "Most common weapon type in the game. Hand-to-hand with hurled items " +
            "also increases hurling proficiency."),
        new WeaponType(2, "Large",     "Slowest",      "Highest",     "2-handed",
            "Large, heavy weapons. Examples: Claymore, Great Scythe."),
        new WeaponType(3, "Long Edge", "Average",      "Average",     "1-handed",
            "One-handed swords. Examples: Longsword, Scimitar."),
        new WeaponType(4, "Projectile","Fast",         "Average",     "2-handed",
            "Ranged weapons requiring bolts. Example: Light Crossbow. " +
            "Weakest weapon type — each bolt takes an inventory slot."),
        new WeaponType(5, "Short Edge","Fastest",      "Least",       "1-handed",
            "Short, light swords. Examples: Short Sword, Dagger."),
        new WeaponType(6, "Thrusting", "Slow",         "High",        "2-handed",
            "Pole arms. Examples: Spear, Trident. Some can be thrown " +
            "(throwing a thrust weapon increases hurling proficiency)."),
    };

    public static WeaponType? ById(int id) =>
        id >= 0 && id < Types.Count ? Types[id] : null;
}
