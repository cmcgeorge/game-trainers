namespace KnightsOfLegendTrainer.Game;

/// <summary>One weapon type and its training master. [Manual]</summary>
public sealed record WeaponEntry(
    int Id,
    string Name,
    string Master,
    string Location,
    int MaxProficiency,
    string Notes);

/// <summary>
/// Weapon types and their training masters in Knights of Legend. Each master trains
/// four weapon types up to a maximum proficiency level. Weapons break randomly in
/// combat, so training in several types is essential. [Manual]
/// </summary>
internal static class WeaponBook
{
    public static readonly string[] ProficiencyNames =
    {
        "None", "Beginner", "Neophyte", "Novice", "Average",
        "Skilled", "Stalwart", "Adept", "Savant", "Expert"
    };

    public static IReadOnlyList<WeaponEntry> Weapons { get; } = new[]
    {
        new WeaponEntry(0, "Longsword", "Hvrad Myth", "Fortress of Brettle", 30,
            "Versatile one-handed blade; Long Edge proficiency."),
        new WeaponEntry(1, "Broadsword", "Hvrad Myth", "Fortress of Brettle", 30,
            "Heavy one-handed sword; Long Edge proficiency."),
        new WeaponEntry(2, "Short Spear", "Hvrad Myth", "Fortress of Brettle", 30,
            "One-handed pole-arm; Thrusting proficiency."),
        new WeaponEntry(3, "Battle Axe", "Hvrad Myth", "Fortress of Brettle", 30,
            "Heavy one-handed axe; Clubbing proficiency."),

        new WeaponEntry(4, "Broad Axe", "Fistan Stockhard", "Tower north of Brettle", 45,
            "Two-handed axe; Clubbing proficiency."),
        new WeaponEntry(5, "Hand Axe", "Fistan Stockhard", "Tower north of Brettle", 45,
            "Light axe; Clubbing proficiency. Can be thrown."),
        new WeaponEntry(6, "Heavy Crossbow", "Fistan Stockhard", "Tower north of Brettle", 45,
            "Powerful ranged; Projectile proficiency. Requires bolts."),
        new WeaponEntry(7, "Great Axe", "Fistan Stockhard", "Tower north of Brettle", 45,
            "Largest axe; Clubbing proficiency. Two-handed."),

        new WeaponEntry(8, "Scimitar", "Zachary Bladeshure", "Htron", 30,
            "Curved blade; Long Edge proficiency."),
        new WeaponEntry(9, "Greatsword", "Zachary Bladeshure", "Htron", 30,
            "Two-handed sword; Large proficiency."),
        new WeaponEntry(10, "Shortsword", "Zachary Bladeshure", "Htron", 30,
            "Light blade; Short Edge proficiency."),
        new WeaponEntry(11, "Bastard Sword", "Zachary Bladeshure", "Htron", 30,
            "Hand-and-a-half sword; Long Edge proficiency."),

        new WeaponEntry(12, "Scimitar", "Mornag the Merciless", "Htron Training Grounds", 30,
            "Kelden not welcomed here. Long Edge proficiency."),
        new WeaponEntry(13, "Mace", "Mornag the Merciless", "Htron Training Grounds", 30,
            "Blunt weapon; Clubbing proficiency."),
        new WeaponEntry(14, "Light Crossbow", "Mornag the Merciless", "Htron Training Grounds", 30,
            "Ranged; Projectile proficiency. Requires bolts."),
        new WeaponEntry(15, "War Hammer", "Mornag the Merciless", "Htron Training Grounds", 30,
            "Blunt weapon; Clubbing proficiency."),

        new WeaponEntry(16, "Halberd", "Monvin the Elder", "Tegal Forest", 30,
            "Pole-arm; Thrusting proficiency. Two-handed."),
        new WeaponEntry(17, "Morningstar", "Monvin the Elder", "Tegal Forest", 30,
            "Spiked blunt weapon; Clubbing proficiency."),
        new WeaponEntry(18, "Flail", "Monvin the Elder", "Tegal Forest", 30,
            "Chain weapon; Clubbing proficiency."),
        new WeaponEntry(19, "Broadsword", "Monvin the Elder", "Tegal Forest", 30,
            "Long Edge proficiency."),

        new WeaponEntry(20, "Club", "Nigel Gulliam", "Krell Way", 30,
            "Basic blunt weapon; Clubbing proficiency."),
        new WeaponEntry(21, "Halberd", "Nigel Gulliam", "Krell Way", 30,
            "Pole-arm; Thrusting proficiency."),
        new WeaponEntry(22, "Great Hammer", "Nigel Gulliam", "Krell Way", 30,
            "Large blunt weapon; Clubbing proficiency. Two-handed."),
        new WeaponEntry(23, "Quarterstaff", "Nigel Gulliam", "Krell Way", 30,
            "Staff; Thrusting proficiency. Two-handed."),

        new WeaponEntry(24, "Long Spear", "Kelmore Stratsmoth", "Shellernoon", 30,
            "Long pole-arm; Thrusting proficiency. Two-handed."),
        new WeaponEntry(25, "Morningstar", "Kelmore Stratsmoth", "Shellernoon", 30,
            "Clubbing proficiency."),
        new WeaponEntry(26, "War Maul", "Kelmore Stratsmoth", "Shellernoon", 30,
            "Large blunt weapon; Clubbing proficiency."),
        new WeaponEntry(27, "Heavy Maul", "Kelmore Stratsmoth", "Shellernoon", 30,
            "Largest blunt weapon; Clubbing proficiency. Two-handed."),

        new WeaponEntry(28, "Longsword", "Rhunholland", "Olanthen", 30,
            "Long Edge proficiency."),
        new WeaponEntry(29, "Broadsword", "Rhunholland", "Olanthen", 30,
            "Long Edge proficiency."),
        new WeaponEntry(30, "Bastard Sword", "Rhunholland", "Olanthen", 30,
            "Long Edge proficiency."),
        new WeaponEntry(31, "Greatsword", "Rhunholland", "Olanthen", 30,
            "Large proficiency. Two-handed."),

        new WeaponEntry(32, "Self Bow", "Tyrolliar Cellana", "Klvar Wood", 30,
            "Basic bow; Projectile proficiency. Requires arrows."),
        new WeaponEntry(33, "Elf Bow", "Tyrolliar Cellana", "Klvar Wood", 30,
            "Elven bow; Projectile proficiency. Requires arrows."),
        new WeaponEntry(34, "Long Bow", "Tyrolliar Cellana", "Klvar Wood", 30,
            "Powerful bow; Projectile proficiency. Requires arrows."),
        new WeaponEntry(35, "Dagger", "Tyrolliar Cellana", "Klvar Wood", 30,
            "Short blade; Short Edge proficiency. Can be thrown."),
    };

    public static IReadOnlyList<WeaponEntry> ByMaster(string master) =>
        Weapons.Where(w => w.Master == master).ToList();

    public static IReadOnlyList<string> Masters =>
        Weapons.Select(w => w.Master).Distinct().ToList();
}
