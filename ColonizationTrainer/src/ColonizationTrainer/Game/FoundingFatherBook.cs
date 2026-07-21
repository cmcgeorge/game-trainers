namespace ColonizationTrainer.Game;

/// <summary>A Founding Father: the bit index in the nation's acquired bitfield, name, category, and effect.</summary>
public sealed record FoundingFather(int Bit, string Name, string Category, string Effect);

/// <summary>
/// The 25 Founding Fathers in bit order (bit N of the nation record's <c>founding_fathers</c> u32 at
/// offset 0x07). The order matches <c>.games/PEDIA.TXT</c> (<c>@FATHER0…24</c>) and the viceroy
/// <c>founding_father_list</c>. Five categories of five. Bit 18 is a dead slot in the game's own
/// list, kept here as a placeholder so bit indices stay aligned. Effects are the verified 1994
/// behaviours — see <c>docs/Colonization-Strategy-Guide.md §7</c>.
/// </summary>
public static class FoundingFatherBook
{
    public static readonly IReadOnlyList<FoundingFather> Fathers = new[]
    {
        // Trade
        new FoundingFather(0,  "Adam Smith",           "Trade",       "Unlocks factory-level buildings (+50% output per input)"),
        new FoundingFather(1,  "Jakob Fugger",         "Trade",       "Cancels all boycotts, once"),
        new FoundingFather(2,  "Peter Minuit",         "Trade",       "Native land is free; no payment demanded"),
        new FoundingFather(3,  "Peter Stuyvesant",     "Trade",       "Allows building Custom Houses"),
        new FoundingFather(4,  "Jan de Witt",          "Trade",       "Trade with foreign colonies; richer foreign reports"),
        // Exploration
        new FoundingFather(5,  "Ferdinand Magellan",   "Exploration", "+1 movement for all ships; faster Europe trips"),
        new FoundingFather(6,  "Francisco de Coronado","Exploration", "Reveals all colonies on the map"),
        new FoundingFather(7,  "Hernando de Soto",     "Exploration", "Lost City Rumors always positive; +sighting radius"),
        new FoundingFather(8,  "Henry Hudson",         "Exploration", "Doubles fur trapper output"),
        new FoundingFather(9,  "Sieur de La Salle",    "Exploration", "Free stockade for colonies of population 3+"),
        // Military
        new FoundingFather(10, "Hernán Cortés",        "Military",    "Conquered villages always yield treasure, shipped free"),
        new FoundingFather(11, "George Washington",    "Military",    "Winning units auto-promote to Veteran"),
        new FoundingFather(12, "Paul Revere",          "Military",    "A colonist grabs stored muskets to defend an undefended colony"),
        new FoundingFather(13, "Francis Drake",        "Military",    "Privateer combat strength +50%"),
        new FoundingFather(14, "John Paul Jones",      "Military",    "Adds a free Frigate"),
        // Political
        new FoundingFather(15, "Thomas Jefferson",     "Political",   "Liberty Bell production +50%"),
        new FoundingFather(16, "Pocahontas",           "Political",   "Native tension reset to content; alarm generated half as fast"),
        new FoundingFather(17, "Thomas Paine",         "Political",   "Liberty Bell production increased by the tax rate"),
        new FoundingFather(18, "(unused)",             "—",           "Dead slot in the game's list (kept for bit alignment)"),
        new FoundingFather(19, "Benjamin Franklin",    "Political",   "Europeans always offer peace; King's wars don't drag you in"),
        // Religious
        new FoundingFather(20, "William Brewster",     "Religious",   "Choose immigrants; no more criminals/servants on the docks"),
        new FoundingFather(21, "William Penn",         "Religious",   "Cross production +50%"),
        new FoundingFather(22, "Jean de Brébeuf",      "Religious",   "All missionaries function as experts"),
        new FoundingFather(23, "Juan de Sepúlveda",    "Religious",   "Higher chance subjugated natives convert"),
        new FoundingFather(24, "Bartolomé de las Casas","Religious",  "All Indian converts become Free Colonists, once"),
    };

    /// <summary>Number of real (grantable) Fathers — the 25 bits minus the one dead slot.</summary>
    public static int GrantableCount => Fathers.Count(f => f.Category != "—");
}
