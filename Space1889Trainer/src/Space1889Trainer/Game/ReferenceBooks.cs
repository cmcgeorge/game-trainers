namespace Space1889Trainer.Game;

/// <summary>Attributes, skills and social classes, in the order the game stores and draws them.</summary>
public static class SkillBook
{
    /// <summary>Attribute names (record +0x13..+0x18). The manual abbreviates AGI as AGL and CHA as CHR.</summary>
    public static readonly IReadOnlyList<string> Attributes = new[]
    {
        "Strength", "Agility", "Endurance", "Intellect", "Charisma", "Social Level",
    };

    /// <summary>Short attribute names as the status panel prints them.</summary>
    public static readonly IReadOnlyList<string> AttributeAbbreviations = new[] { "STR", "AGI", "END", "INT", "CHA", "SOC" };

    /// <summary>Skill names (record +0x19..+0x30), four per attribute in attribute order. [Confirmed on the PARTY screen]</summary>
    public static readonly IReadOnlyList<string> Skills = new[]
    {
        "Fisticuffs", "Throwing", "Close Combat", "Trimsman",
        "Stealth", "Crime", "Marksmanship", "Mechanics",
        "Wilderness Travel", "Fieldcraft", "Tracking", "Swimming",
        "Observation", "Engineering", "Science", "Gunnery",
        "Eloquence", "Theatrics", "Bargaining", "Linguistics",
        "Riding", "Piloting", "Leadership", "Medicine",
    };

    /// <summary>What each skill does in this game (the cluebook's list, confirmed where the code reads it).</summary>
    public static readonly IReadOnlyList<string> SkillUses = new[]
    {
        "Unarmed fighting", "Thrown weapons", "Melee and close-range guns", "Raising and lowering an ether flyer in combat",
        "Needed to ROB", "ROB and lockpicks", "Long-range shooting", "Mechanical tinkering (little effect)",
        "Less travel fatigue", "Cover in bushes during combat", "HUNT: tracks nearby NPCs", "Crossing water (with a water breather)",
        "STUDY and VIEW: examining things", "Explosives and their fuse time", "Plotting a course between planets", "Ship gun accuracy",
        "Persuasion (little effect)", "Disguise (little effect)", "Shop prices", "Understanding what NPCs say",
        "Horses (little effect)", "Boats and zeppelins; flying in space combat", "Leading the party (little effect)", "CURE: healing party members",
    };

    /// <summary>The attribute (0..5) a skill belongs to.</summary>
    public static int AttributeOf(int skill) => skill / 4;

    /// <summary>Social classes by SOC − 1 (DS:0x2ACB). [Static; matches the five default characters]</summary>
    public static readonly IReadOnlyList<string> SocialClasses = new[]
    {
        "Working Class", "Tradesman", "Middle Class", "Gentry", "Wealthy Gentry", "Aristocracy",
    };

    /// <summary>The class a SOC value gives, or "—" out of range.</summary>
    public static string ClassFor(int soc) => soc >= 1 && soc <= SocialClasses.Count ? SocialClasses[soc - 1] : "—";

    /// <summary>The largest attribute the character generator rolls.</summary>
    public const int MaxRolledAttribute = 6;
}

/// <summary>Career ids stored at record +0x49/+0x4A (89CAREER.DAT, 40 × 49-byte records, id at +0x20).</summary>
public static class CareerBook
{
    /// <summary>Built-in careers by id. Ids 43 and up are user careers from 89CAREER.MOD.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Army (1)", "Army (2)", "Army (3)", "Army (4)", "Army (5)", "Army (6)",
        "Navy (1)", "Navy (2)", "Navy (3)", "Navy (4)", "Navy (5)", "Navy (6)",
        "Foreign Agent", "Foreign Diplomat", "Colonial Office", "Big Game Hunter", "Explorer",
        "Dilettante Traveller", "Adventuress", "Reporter", "Actor/Actress", "Personal Servant",
        "Tutor/Governess (2)", "Tutor/Governess (3)", "Tutor/Governess (4)", "Grounds Keeper", "Inventor",
        "Merchant", "Mechanic", "Engineer", "Seasman", "Detective", "Doctor (1)", "Doctor (2)", "Scientist",
        "Master Criminal", "Poacher", "Smuggler", "Thief", "Anarchist",
    };

    /// <summary>A built-in career name, or "custom #id".</summary>
    public static string NameOf(int id) => id >= 0 && id < Names.Count ? Names[id] : $"custom #{id}";
}

/// <summary>A place the state block can name: planet, area within it, map within the area.</summary>
public static class PlaceBook
{
    /// <summary>Planet names by the state's planet byte (DS:0x2EF7).</summary>
    public static readonly IReadOnlyList<string> Planets = new[] { "Space", "Earth", "Luna", "Mercury", "Mars", "Venus", "Ship" };

    /// <summary>Area names per planet, index 0 = the planet's own surface map (DS:0x00E2).</summary>
    public static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> Areas = new Dictionary<int, IReadOnlyList<string>>
    {
        [1] = new[] { "Earth (overland)", "London", "New York", "Angkor", "San Francisco", "Egypt", "Teotihuacan", "Inner Earth" },
        [2] = new[] { "Luna (surface)", "A Cave" },
        [3] = new[] { "Mercury (surface)", "Princess Christiana" },
        [4] = new[] { "Mars (overland)", "Aubochon", "Moab", "Syrtis Major", "Moerus Lacus", "Gaaryan", "Ausonia", "Boreo Syrtis" },
        [5] = new[] { "Venus (overland)", "Thetis Mountains", "Venusstadt", "Ganis Mountains" },
        [6] = new[] { "Ship", "Whisperdeath", "Bloodrunner", "Aphid", "Hullcutter", "Dauntless", "Hamburg", "Smallbird" },
    };

    /// <summary>The planet's name, or "#n" out of range.</summary>
    public static string PlanetName(int planet) => planet >= 0 && planet < Planets.Count ? Planets[planet] : $"#{planet}";

    /// <summary>The area's name within its planet, or "area n".</summary>
    public static string AreaName(int planet, int area) =>
        Areas.TryGetValue(planet, out var list) && area >= 0 && area < list.Count ? list[area] : $"area {area}";

    /// <summary>
    /// The map id the data files key on: planet × 100 + area × 10 + map. Maps 2 and 9 of a city are the shared
    /// pub (x92) and inn (x99) interiors, stored under pseudo-planet 9.
    /// </summary>
    public static int MapId(int planet, int area, int map) => planet * 100 + area * 10 + map;

    /// <summary>"Earth › London › map 3" style description.</summary>
    public static string Describe(int planet, int area, int map)
    {
        string where = area == 0 ? PlanetName(planet) + " (planet map)" : $"{PlanetName(planet)} › {AreaName(planet, area)}";
        return map == 0 ? where : $"{where} › map {map}";
    }
}

/// <summary>Ether-flyer part names used by the builder at an ether port.</summary>
public static class FlyerBook
{
    /// <summary>Lift types (DS:0x2B87). Liftwood fails on Venus.</summary>
    public static readonly IReadOnlyList<string> LiftTypes = new[] { "Hydrogen", "Liftwood" };

    /// <summary>Propeller types (DS:0x2B77). Zeppelin allows power ≤ 4; Saurian needs the PROPELLER item.</summary>
    public static readonly IReadOnlyList<string> Propellers = new[] { "Edison", "Armstrong", "Zeppelin", "Saurian" };

    /// <summary>Bridge stations (state 0x42D5..0x42D9).</summary>
    public static readonly IReadOnlyList<string> Stations = new[] { "Captain", "Helmsman", "Trimsman", "Top gunner", "Low gunner" };

    /// <summary>The builder's hull-size ceiling.</summary>
    public const int MaxHull = 30;
}

/// <summary>A story-flag bit at state 0x42E0.</summary>
public sealed record StoryFlag(int Bit, string Name, string Effect);

/// <summary>What each story-flag bit does (M.EXE/S.EXE code that sets and tests it). [Static]</summary>
public static class StoryFlagBook
{
    public static readonly IReadOnlyList<StoryFlag> All = new StoryFlag[]
    {
        new(0, "Inner Earth entrance open", "Set with bit 10 when the flyer reaches Jupiter (space rows 2-4, columns 116-118); M.EXE then draws the Inner Earth entrance at the North Pole of the Earth map (row 2, col 52)."),
        new(1, "Tutankhamen's tomb opened", "Searching the sarcophagus (Egypt map 8) has given the STONE TABLET and TUTS TREASURES."),
        new(2, "Freemerchant found", "The Atlantis event (Teotihuacan map 7) has given FREEMERCHANTS ID, DIARY and SCROLLS OF THE ANCIENTS."),
        new(3, "Edison rescued", "Thomas Edison's event aboard the Whisperdeath has played; ether ports accept AMMONIA and GLOW CRYSTALS as fuel."),
        new(4, "Kleuht's emerald", "Kleuht Na Vriss has handed over THE EMERALD; fuelling allowed; the Whisperdeath waits over Mars."),
        new(5, "Teotihuacan tablets placed", "Both tablets lie on their altars (map 3); the Atlantis cave entrance (Teotihuacan map, row 35, col 76) and the inner doors of map 4 are open."),
        new(6, "Egyptian tomb dug out", "The dig 14 paces south of the Eye of the Desert (Egypt, row 37, col 49) has opened the tomb stairs."),
        new(7, "Burial chamber dug out", "The dig 8 paces north and 1 west of the stairs (Egypt map 7, row 9, col 9) has opened the burial chamber."),
        new(8, "Lost city of Moab dug out", "The dig at the tail of the scorpion (Moab, row 16, col 29) has opened the underground city."),
        new(9, "Stonehenge gold shield taken", "The GOLD SHIELD has been dug up at Stonehenge (London, row 30, col 61, on a day divisible by 30); it cannot be dug again."),
        new(10, "Europa sequence shown", "Set with bit 0 by the same S.EXE trigger at Jupiter; S.EXE's own record that the Europa sequence has played."),
        new(11, "Silbury Hill chalice taken", "The RUBY CHALICE has been dug up (London map 5, row 15, col 11); it cannot be dug again."),
    };
}
