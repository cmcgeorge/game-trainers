namespace KnightsOfLegendTrainer.Game;

/// <summary>One playable race in Knights of Legend. [Manual]</summary>
public sealed record RaceEntry(
    int Id,
    string Name,
    string Description,
    string Notes);

/// <summary>
/// The four playable races in Knights of Legend. [Manual]
/// </summary>
internal static class RaceBook
{
    public static IReadOnlyList<RaceEntry> Races { get; } = new[]
    {
        new RaceEntry(0, "Human",
            "Versatile and adaptable; the widest class selection (12 male, 4 female).",
            "Can join all six magic orders. Best for beginners due to class flexibility."),
        new RaceEntry(1, "Elven",
            "Forest dwellers with natural archer ability; 6 classes available.",
            "Can join White Pearl order. High Quickness and Intellect typical."),
        new RaceEntry(2, "Dwarven",
            "Sturdy mountain folk; 8 male classes. No female dwarves are playable.",
            "Can join Blue Gem order. High Strength and Health typical. Can ride horses " +
            "(despite Brettle stable's claim to the contrary)."),
        new RaceEntry(3, "Kelden",
            "Winged humanoids native to Ashtalarea; 3 male classes. Can fly in combat.",
            "Can join Blue Gem order. The strongest fighters; fly/fly faster/zoom movement " +
            "in combat. Not welcomed at Htron Training Grounds by Mornag the Merciless."),
    };

    public static RaceEntry? ById(int id) =>
        id >= 0 && id < Races.Count ? Races[id] : null;
}
