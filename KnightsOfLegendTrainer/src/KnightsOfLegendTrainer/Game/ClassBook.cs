namespace KnightsOfLegendTrainer.Game;

/// <summary>One character class in Knights of Legend. [Manual]</summary>
public sealed record ClassEntry(
    int Id,
    string Name,
    string Race,
    string Gender,
    int Level,
    string Notes);

/// <summary>
/// The 33 character classes in Knights of Legend, grouped by race and gender.
/// The level column is the starting class level (Peasant = 1, Knight = 25).
/// [Manual]
/// </summary>
internal static class ClassBook
{
    public static IReadOnlyList<ClassEntry> Classes { get; } = new[]
    {
        new ClassEntry(0, "Peasant", "Human", "Male", 1, "Starting class; minimal equipment and stats."),
        new ClassEntry(1, "Brettle Regular", "Human", "Male", 3, "Basic soldier; starts with 3000 GC and leather armor."),
        new ClassEntry(2, "Squire", "Human", "Male", 5, "Apprentice knight; can use most weapons."),
        new ClassEntry(3, "Knight", "Human", "Male", 25, "Highest human male class; best stats and equipment."),
        new ClassEntry(4, "Bowman", "Human", "Male", 4, "Archery-focused; starts with bow and arrows."),
        new ClassEntry(5, "Crossbowman", "Human", "Male", 6, "Heavy ranged; crossbow specialist."),
        new ClassEntry(6, "Levman", "Human", "Male", 5, "Light infantry."),
        new ClassEntry(7, "Pikeman", "Human", "Male", 7, "Pole-arm specialist; Thrusting proficiency."),
        new ClassEntry(8, "Swordsman", "Human", "Male", 8, "Sword-focused melee fighter."),
        new ClassEntry(9, "Axeman", "Human", "Male", 6, "Axe specialist; Clubbing proficiency."),
        new ClassEntry(10, "Maceman", "Human", "Male", 6, "Mace specialist; Clubbing proficiency."),
        new ClassEntry(11, "Horseman", "Human", "Male", 10, "Mounted fighter; requires a horse."),

        new ClassEntry(12, "Peasant", "Human", "Female", 1, "Starting class; minimal equipment."),
        new ClassEntry(13, "Brettle Regular", "Human", "Female", 3, "Basic soldier; starts with 3000 GC."),
        new ClassEntry(14, "Squire", "Human", "Female", 5, "Apprentice knight."),
        new ClassEntry(15, "Knight", "Human", "Female", 25, "Highest human female class."),

        new ClassEntry(16, "Elven Peasant", "Elven", "Either", 1, "Starting elven class."),
        new ClassEntry(17, "Elven Bowman", "Elven", "Either", 5, "Elven archer; natural bow proficiency."),
        new ClassEntry(18, "Elven Squire", "Elven", "Either", 5, "Elven apprentice knight."),
        new ClassEntry(19, "Elven Knight", "Elven", "Either", 20, "High elven class; not as high as human Knight."),
        new ClassEntry(20, "Elven Mage", "Elven", "Either", 8, "Magic-focused; can join White Pearl order."),
        new ClassEntry(21, "Elven Horseman", "Elven", "Either", 10, "Mounted elven fighter."),

        new ClassEntry(22, "Dwarven Peasant", "Dwarven", "Male", 1, "Starting dwarven class."),
        new ClassEntry(23, "Dwarven Regular", "Dwarven", "Male", 3, "Basic dwarven soldier."),
        new ClassEntry(24, "Dwarven Squire", "Dwarven", "Male", 5, "Dwarven apprentice knight."),
        new ClassEntry(25, "Dwarven Knight", "Dwarven", "Male", 20, "High dwarven class."),
        new ClassEntry(26, "Dwarven Axeman", "Dwarven", "Male", 7, "Axe specialist; high Strength."),
        new ClassEntry(27, "Dwarven Maceman", "Dwarven", "Male", 7, "Mace specialist."),
        new ClassEntry(28, "Dwarven Pikeman", "Dwarven", "Male", 7, "Pole-arm specialist."),
        new ClassEntry(29, "Dwarven Horseman", "Dwarven", "Male", 10, "Mounted dwarven fighter; can ride despite stable's claims."),

        new ClassEntry(30, "Kelden Peasant", "Kelden", "Male", 1, "Starting Kelden class."),
        new ClassEntry(31, "Kelden Knight", "Kelden", "Male", 20, "High Kelden class; strongest fighter in the game."),
        new ClassEntry(32, "Kelden Horseman", "Kelden", "Male", 10, "Mounted Kelden; can also fly in combat."),
    };

    public static IReadOnlyList<ClassEntry> ByRace(string race) =>
        Classes.Where(c => c.Race == race).ToList();

    public static ClassEntry? ById(int id) =>
        id >= 0 && id < Classes.Count ? Classes[id] : null;
}
