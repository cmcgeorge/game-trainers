namespace ImperialismIITrainer.Game;

/// <summary>A tradeable good and how it is produced/used, for the Reference tab and the resource-scan picker.</summary>
public sealed record Commodity(int Id, string Name, string Kind, string Notes);

/// <summary>
/// The commodity set of <b>Imperialism II: The Age of Exploration</b>, taken from the game manual's
/// "Possible Commodities to Transport" and confirmed against the live warehouse (the ten materials a
/// running game held — Wool/Timber/Tin/Copper/Iron Ore and their products Fabric/Lumber/Paper/Bronze/
/// Cast Iron — were read straight out of memory, packed as consecutive signed 16-bit values, raws then
/// refined).
///
/// NOTE ON PROVENANCE: an earlier version of this book used an internal name table found in
/// <c>Imperialism II.exe</c> (Cotton/Coal/Iron/Steel/Oil…). That table is the <i>industrial</i>
/// commodity set and does <b>not</b> match what Age of Exploration actually plays with — the exploration
/// era uses Cast Iron and Bronze (Tin+Copper), not Steel/Oil, and those display names aren't stored as
/// plain text in the game files at all (they're baked into the trade-screen art). The manual is the
/// authoritative source, so this book follows it.
///
/// A warehouse stockpile is read by <c>TCountry::GetStockpile(short commodity)</c> and stored as a
/// signed 16-bit quantity, which is why the resource guided-scan defaults to Int16. This book only
/// <i>labels</i> a scan; the actual amount is whatever the user reads off the warehouse screen. The
/// <see cref="Commodity.Id"/> is a catalogue number for display, not the in-game commodity index.
/// </summary>
public static class CommodityBook
{
    public const string Raw = "Raw material";
    public const string Refined = "Refined material";
    public const string Food = "Food";
    public const string Luxury = "Luxury";
    public const string Riches = "Riches";

    public static IReadOnlyList<Commodity> Commodities { get; } = new[]
    {
        // Raw industrial materials — gathered on the map, refined in a city.
        new Commodity(0,  "Wool",         Raw,     "Old World; refined into Fabric. Needed early for all military units and to recruit labour."),
        new Commodity(1,  "Cotton",       Raw,     "New World; substitutes for / supplements Wool in making Fabric."),
        new Commodity(2,  "Timber",       Raw,     "From scrub/hardwood forest; refined into Lumber and Paper."),
        new Commodity(3,  "Coal",         Raw,     "Smelts Iron into Steel (late game); fuels some ships late game."),
        new Commodity(4,  "Iron Ore",     Raw,     "Refined into Cast Iron; with Coal, into Steel."),
        new Commodity(5,  "Tin",          Raw,     "Found in swamps; combines with Copper to make Bronze."),
        new Commodity(6,  "Copper",       Raw,     "Found in hills/mountains; combines with Tin to make Bronze."),
        new Commodity(7,  "Horses",       Raw,     "Old World; used to build military units. One source is usually enough."),

        // Refined materials — produced by industry from the raws above.
        new Commodity(8,  "Fabric",       Refined, "From Wool/Cotton; required for all military units and to recruit industrial labour."),
        new Commodity(9,  "Lumber",       Refined, "From Timber; builds infrastructure (ports, roads) and ships."),
        new Commodity(10, "Paper",        Refined, "From Timber; produces civilian units and trains industrial workers."),
        new Commodity(11, "Cast Iron",    Refined, "From Iron Ore; with Lumber, drives infrastructure improvements."),
        new Commodity(12, "Bronze",       Refined, "From Tin + Copper; best military units, warships and fortifications."),
        new Commodity(13, "Steel",        Refined, "From Iron + Coal (late game); replaces Bronze for most uses."),

        // Food — consumed by every worker, ship and regiment each turn.
        new Commodity(14, "Grain",        Food,    "Old World staple; feeds your population, navy and army."),
        new Commodity(15, "Cattle",       Food,    "Old World meat source (the complex food economy wants meat + grain)."),
        new Commodity(16, "Fish",         Food,    "From any river or sea tile; a food source available everywhere."),

        // Luxuries — New World raws and the goods refined from them; keep trained workers productive.
        new Commodity(17, "Sugar Cane",   Luxury,  "New World; refined into Refined Sugar."),
        new Commodity(18, "Tobacco",      Luxury,  "New World; refined into Cigars."),
        new Commodity(19, "Furs",         Luxury,  "New World; refined into Fur Hats."),
        new Commodity(20, "Refined Sugar",Luxury,  "The most common luxury; consumed by apprentice (lowest trained) workers."),
        new Commodity(21, "Cigars",       Luxury,  "A luxury demanded by higher-trained workers."),
        new Commodity(22, "Fur Hats",     Luxury,  "A luxury demanded by higher-trained workers."),

        // Riches — convert directly to cash when shipped home.
        new Commodity(23, "Spices",       Riches,  "Lowest-valued rich ($50/unit); from spice-orchard terrain."),
        new Commodity(24, "Silver",       Riches,  "A rich mined in hills; sold for cash."),
        new Commodity(25, "Gold",         Riches,  "A rich mined in mountains; sold for cash."),
        new Commodity(26, "Gems",         Riches,  "A rich mined in mountains; sold for cash."),
        new Commodity(27, "Diamonds",     Riches,  "A desert rich; must be found first by a prospecting Explorer."),
    };

    /// <summary>Just the names, in catalogue order.</summary>
    public static IReadOnlyList<string> Names { get; } =
        Commodities.Select(c => c.Name).ToArray();
}
