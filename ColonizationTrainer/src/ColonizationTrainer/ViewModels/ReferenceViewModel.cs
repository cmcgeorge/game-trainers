using ColonizationTrainer.Game;

namespace ColonizationTrainer.ViewModels;

/// <summary>
/// The References tab: read-only lookup tables recovered from the game's own data (goods, units,
/// terrain yields, professions, buildings, Founding Fathers, nations, difficulty) plus a condensed
/// strategy digest. All static — no process or save needed.
/// </summary>
public sealed class ReferenceViewModel
{
    public IReadOnlyList<Good> Goods { get; } = CargoBook.Goods;
    public IReadOnlyList<UnitType> Units { get; } = UnitBook.Units;
    public IReadOnlyList<Terrain> Terrains { get; } = TerrainBook.Terrains;
    public IReadOnlyList<Profession> Professions { get; } = ProfessionBook.Professions;
    public IReadOnlyList<Building> Buildings { get; } = BuildingBook.Buildings;
    public IReadOnlyList<FoundingFather> Fathers { get; } = FoundingFatherBook.Fathers;
    public IReadOnlyList<Nation> Nations { get; } = NationBook.Nations;
    public IReadOnlyList<Difficulty> Difficulties { get; } = NationBook.Difficulties;
    public IReadOnlyList<StrategySection> Strategy { get; } = Walkthrough.Sections;
}
