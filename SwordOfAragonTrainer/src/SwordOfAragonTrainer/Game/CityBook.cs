namespace SwordOfAragonTrainer.Game;

/// <summary>A city or wilderness region as the new-game database defines it.</summary>
/// <param name="Index">Position in the save's city order, 0..19.</param>
/// <param name="Name">Name as the save file spells it (no spaces — "SurNova").</param>
/// <param name="DisplayName">Name as the rule book spells it.</param>
/// <param name="X">Map column, or -1 for a region with no city hex.</param>
/// <param name="Y">Map row, or -1.</param>
/// <param name="Ruler">Ruler at the start of the game.</param>
/// <param name="Note">One-line intelligence summary, from the Duke's Notebook.</param>
public sealed record CityInfo(
    int Index, string Name, string DisplayName, int X, int Y, int Population,
    int Morale, int Loyalty, int Health, int CityGold, string Ruler, string Note)
{
    /// <summary>Map position as the save encodes it (<c>x*100 + y</c>), or 0 if there is no city hex.</summary>
    public int PositionCode => X >= 0 ? X * 100 + Y : 0;

    /// <summary>Position for display.</summary>
    public string Position => X >= 0 ? $"({X},{Y})" : "—";

    public override string ToString() => DisplayName;
}

/// <summary>
/// The 20 cities and wilderness regions of Aragon, in the order the save file stores them, with the
/// figures the new-game database in <c>SWORD.EXE</c> supplies. Map positions were confirmed twice
/// over: the executable's own position field and the coordinates written into every shipped save.
/// The notes condense the Notebook of the Duke of Aladda.
/// </summary>
public static class CityBook
{
    public static readonly IReadOnlyList<CityInfo> Cities = new[]
    {
        new CityInfo(0, "Aladda", "Aladda", 6, 7, 1_500, 75, 52, 85, 150, "You",
            "Your capital. Lumber, minerals and rich soil on the Garrish River — the cheapest development in the game."),
        new CityInfo(1, "Marinia", "Marinia", 1, 4, 1_200, 50, 30, 30, 315, "Gardwell, Duke",
            "Poor swampland; a sickly ruler and an army that plunders its own people. The easiest first conquest."),
        new CityInfo(2, "Brocada", "Brocada", 6, 1, 2_600, 50, 25, 80, 7_150, "Petrov, General",
            "North-coast fishing town. Volunteer militia that drills weekly and fights badly — and a 7,150 GP purse."),
        new CityInfo(3, "SurNova", "Sur Nova", 4, 12, 3_400, 35, 25, 60, 350, "unknown",
            "Hilltop town commanding the north-south road. Good resources, monster-plagued, and no army at all."),
        new CityInfo(4, "Paritan", "Paritan", 10, 2, 4_450, 55, 50, 45, 5_250, "Pitlag, Lord Redux",
            "Pirate harbour. The most professional army in the west and an expansionist eye on Nuralia or Brocada."),
        new CityInfo(5, "Nuralia", "Nuralia", 15, 2, 3_250, 40, 5, 60, 1_200, "Wilfreed, Duke",
            "Rich plain, professional but badly-led army, loyalty of just 5 to its own duke — ripe for vassalage."),
        new CityInfo(6, "Tranavan", "Tranavan Forest", 10, 7, 150, 10, 25, 100, 500, "Trinangel, Queen",
            "Evergreen elven forest east of Aladda. Few scouts return; heavily fortified for its size."),
        new CityInfo(7, "Gernok", "Gernok", 15, 8, 750, 125, 110, 20, 150, "Grimlock",
            "Goblin homeland in the north-central Luftgar. The source of the raids on the northern coast."),
        new CityInfo(8, "Xafanta", "Xafanta Mountains", 10, 15, 850, 20, 10, 80, 7_500, "Heben Stenthumble, Grand Trow",
            "Dwarves of the Lastrul Plateau. Vast mineral wealth, and Zarnix orcs trying to take it."),
        new CityInfo(9, "Khalikha", "Khalikha Plains", -1, -1, 1_200, 50, 10, 70, 100, "unknown",
            "Southern steppe of nomadic horsemen — fearsome warriors and excellent bowmen. No city hex."),
        new CityInfo(10, "Tentula", "Tentula", 6, 21, 5_700, 10, 25, 20, 1_240, "Tantala, Baron",
            "Rich bottom land on the Great Blue Lake. Idle and unhappy: morale 10, health 20."),
        new CityInfo(11, "Char", "Char Hills", 11, 22, 1_250, 100, 60, 40, 315, "unknown",
            "Barren hills east of the Khalikha. Home to Giants, Titans and Trolls."),
        new CityInfo(12, "Zarnix", "Zarnix", 13, 18, 1_850, 125, 125, 25, 250, "Gnardix, the Great Hatred",
            "Orc fortress holding the Justinid Pass — the only real route through the Luftgar. The hinge of the campaign."),
        new CityInfo(13, "Medeval", "Medeval Forest", 15, 13, 750, 70, 25, 80, 750, "unknown",
            "Thick forest of the eastern elves, hostile to all men. Structure 12, fortification 10."),
        new CityInfo(14, "Dersh", "Dersh Mountains", 15, 21, 500, 100, 70, 30, 755, "unknown",
            "Legendary home of the Titans, in the south-eastern Luftgar. Source of the Baudom River."),
        new CityInfo(15, "Lucedia", "Lucedia", 20, 20, 7_500, 25, 10, 50, 7_500, "Council of the Wise and Strong",
            "South-east coastal theocracy of priests and Frahali knights — two factions that detest each other."),
        new CityInfo(16, "Pudawala", "Pudawala", 21, 16, 9_800, 25, 10, 50, 12_500, "El-Ikhom, Pasha",
            "Free state on the Dalation. Resource-rich, independent, and the second-richest treasury in Aragon."),
        new CityInfo(17, "Sothold", "Sothold", 20, 11, 16_500, 100, 30, 40, 10_500, "Strumberg, Baron",
            "The breadbasket of the Eastrealm. A strong, disciplined army — your father once served here."),
        new CityInfo(18, "Estallah", "Estallah", 21, 8, 12_500, 50, 15, 70, 7_500, "Landratoz, Earl",
            "Superb harbour, corrupt earl, and a well-led mercenary army hired from all over the east."),
        new CityInfo(19, "Tetrada", "Tetrada", 21, 4, 31_500, 25, 25, 100, 15_420, "Lucinian III, Emperor",
            "The imperial throne, and the object of the game. Commerce 40/75, manufacture 25/40, fortification 10."),
    };

    /// <summary>The row for a save-file city name, or null.</summary>
    public static CityInfo? ByName(string name) => Cities.FirstOrDefault(c =>
        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(c.DisplayName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The row at a save-file index, or null.</summary>
    public static CityInfo? ByIndex(int index) =>
        index >= 0 && index < Cities.Count ? Cities[index] : null;

    /// <summary>Cities that occupy a hex, for a teleport-destination list.</summary>
    public static IEnumerable<CityInfo> WithHexes => Cities.Where(c => c.X >= 0);
}
