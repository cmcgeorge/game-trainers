namespace ColonizationTrainer.Game;

/// <summary>One of the 16 tradeable goods: its index, name, and (for a raw good) what it refines into.</summary>
public sealed record Good(int Id, string Name, string Kind, string Refining);

/// <summary>
/// The 16 goods in their canonical index order (Food … Muskets). This exact order is used by the
/// colony <c>stock</c> array, the <c>boycott</c> and Custom-House bitmaps, and the Europe price
/// tables — see <c>docs/Colonization-Reverse-Engineering.md §6</c>. Verified against
/// <c>.games/PEDIA.TXT</c> (<c>@CARGO0…15</c>).
/// </summary>
public static class CargoBook
{
    public static readonly IReadOnlyList<Good> Goods = new[]
    {
        new Good(0,  "Food",        "Raw",          "Feeds colonists (2/turn); surplus breeds Horses"),
        new Good(1,  "Sugar",       "Raw crop",     "→ Rum (Distiller)"),
        new Good(2,  "Tobacco",     "Raw crop",     "→ Cigars (Tobacconist)"),
        new Good(3,  "Cotton",      "Raw crop",     "→ Cloth (Weaver)"),
        new Good(4,  "Furs",        "Raw",          "→ Coats (Fur Trader)"),
        new Good(5,  "Lumber",      "Raw",          "→ Hammers (Carpenter) — construction"),
        new Good(6,  "Ore",         "Raw",          "→ Tools (Blacksmith)"),
        new Good(7,  "Silver",      "Raw",          "Sold raw for cash (mined in mountains)"),
        new Good(8,  "Horses",      "Livestock",    "Make Scouts/Dragoons; bred from food surplus"),
        new Good(9,  "Rum",         "Manufactured", "From Sugar"),
        new Good(10, "Cigars",      "Manufactured", "From Tobacco"),
        new Good(11, "Cloth",       "Manufactured", "From Cotton"),
        new Good(12, "Coats",       "Manufactured", "From Furs"),
        new Good(13, "Trade Goods", "Manufactured", "Bought in Europe; traded to natives"),
        new Good(14, "Tools",       "Manufactured", "From Ore; make Pioneers, build, → Muskets"),
        new Good(15, "Muskets",     "Manufactured", "From Tools (Gunsmith); make Soldiers"),
    };

    /// <summary>The 16 good names in index order, for a colony stockpile editor.</summary>
    public static IReadOnlyList<string> Names { get; } = Goods.Select(g => g.Name).ToArray();
}
