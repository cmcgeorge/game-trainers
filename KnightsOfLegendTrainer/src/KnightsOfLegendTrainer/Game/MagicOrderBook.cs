namespace KnightsOfLegendTrainer.Game;

/// <summary>One magic order in Knights of Legend. [Manual]</summary>
public sealed record MagicOrder(
    int Id,
    string Name,
    string Location,
    string ComponentRace,
    string Notes);

/// <summary>
/// The six magic orders in Knights of Legend. A character can only join one order,
/// but can learn basic spells from all orders before joining. After joining, the
/// character's spell race component is fixed. [Manual]
/// </summary>
internal static class MagicOrderBook
{
    public static IReadOnlyList<MagicOrder> Orders { get; } = new[]
    {
        new MagicOrder(0, "White Pearl", "Brettle", "Human/Elf",
            "The first order most players encounter. Basic spells available in Brettle's wizard tower."),
        new MagicOrder(1, "Blue Gem", "Tegal Forest", "Kelden/Dwarf",
            "Located in the Kelden homeland. Kelden and Dwarven characters can join."),
        new MagicOrder(2, "Black Onyx", "Shellernoon", "Elemental",
            "Elemental-based spells. The component race is fixed after joining."),
        new MagicOrder(3, "Secret Storm", "Poitle Lock", "Giant",
            "Giant-themed spells. Located in the town of Poitle Lock."),
        new MagicOrder(4, "Red Mist", "Thimblewald", "Legendary",
            "Legendary creature-themed spells. Located in Thimblewald/Thimberwald."),
        new MagicOrder(5, "Dark Stone", "Olanthen", "Undead",
            "Undead-themed spells. Located in the eastern town of Olanthen."),
    };

    public static MagicOrder? ById(int id) =>
        id >= 0 && id < Orders.Count ? Orders[id] : null;
}
