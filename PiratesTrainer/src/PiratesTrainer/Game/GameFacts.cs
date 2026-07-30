namespace PiratesTrainer.Game;

/// <summary>A hull the player can command or capture.</summary>
/// <param name="Name">Name exactly as the game's ship-type table spells it.</param>
/// <param name="Notes">What it is good for, from the manual's classes and the game's own handling.</param>
public sealed record ShipType(string Name, string Notes);

/// <summary>A trade good the cargo screen moves between holds.</summary>
public sealed record TradeGood(string Name, string Notes);

/// <summary>A rank the Governor can grant, in promotion order.</summary>
public sealed record Rank(int Index, string Name, string Notes);

/// <summary>One of the five character-creation specialities.</summary>
public sealed record Speciality(string Name, string Effect);

/// <summary>A difficulty level offered at character creation.</summary>
public sealed record DifficultyLevel(string Name, string Effect);

/// <summary>A pre-set historical scenario from the "Command a Famous Expedition" menu.</summary>
/// <param name="Name">Menu title.</param>
/// <param name="Captain">Historical commander, as the menu's second line names them.</param>
/// <param name="Year">Year the expedition is set in.</param>
public sealed record Expedition(string Name, string Captain, int Year);

/// <summary>A control the player uses, and what it does.</summary>
/// <param name="Input">Key or stick action.</param>
/// <param name="Context">Which screen or mode it applies to.</param>
/// <param name="Effect">What it does.</param>
public sealed record ControlBinding(string Input, string Context, string Effect);

/// <summary>
/// Static, non-address game knowledge for Sid Meier's Pirates! (MicroProse, 1987 — IBM version 432.02).
/// Every table here was read out of the shipped binaries: the name lists come from <c>DISKP</c>'s
/// display-string table, the configuration options from its setup screens, and the emulator/loader facts
/// from <c>PIR.EXE</c>. Nothing here touches the live process.
/// </summary>
public static class GameFacts
{
    /// <summary>Emulator process names that host a DOS guest — the trainer attaches to one of these.</summary>
    public static readonly IReadOnlyList<string> EmulatorProcessHints =
        new[] { "dosbox-x", "dosbox", "dosbox-staging" };

    /// <summary>The build string the game prints under its title credits.</summary>
    public const string Version = "IBM version 432.02 (1987)";

    /// <summary>The loader that turns the original self-booting disks into a DOS-hosted game.</summary>
    public const string LoaderExe = "PIR.EXE";

    /// <summary>The game program the loader EXECs.</summary>
    public const string GameImage = "DISKP";

    /// <summary>Hulls, smallest to largest, in the order the ship-type table lists them.</summary>
    public static readonly IReadOnlyList<ShipType> Ships = new[]
    {
        new ShipType("Pinnace",     "Tiny, fast, barely armed. Superb for sneaking into a harbour and for outrunning trouble."),
        new ShipType("Sloop",       "Quick and nimble with a small hold. The classic early buccaneer's raider."),
        new ShipType("Barque",      "Modest cargo hauler; handles better than a fluyt but carries less."),
        new ShipType("Cargo Fluyt", "Dutch merchantman — big hold, poor guns. A prize worth taking, a poor flagship."),
        new ShipType("Merchantman", "Balanced trader: decent hold, enough guns to bully a sloop."),
        new ShipType("Frigate",     "The best all-round fighting ship — fast enough to choose the fight, strong enough to win it."),
        new ShipType("War Galleon", "Heaviest guns and crew capacity; slow and clumsy in light winds."),
        new ShipType("Galleon",     "The Treasure Fleet's workhorse. Huge hold, sluggish, well armed."),
        new ShipType("Fast Galleon","A galleon with the sailing qualities to match its guns; the finest prize on the Main."),
    };

    /// <summary>What a hold can carry, as the plunder/cargo screen labels it.</summary>
    public static readonly IReadOnlyList<TradeGood> Goods = new[]
    {
        new TradeGood("Gold",    "Counted in pieces, not tons — it never costs hold space."),
        new TradeGood("Food",    "Consumed by the crew; running out starts desertions and mutiny."),
        new TradeGood("Goods",   "General manufactures. Sell in the colonies, they are worth most far from Europe."),
        new TradeGood("Sugar",   "Caribbean staple; sells best in the north and in Europe-facing ports."),
        new TradeGood("Tobacco", "Best prices in the larger, wealthier towns."),
        new TradeGood("Hides",   "Bulk cargo of the cattle coasts; cheap to buy, modest to sell."),
        new TradeGood("Cannon",  "Costs hold space and crew to work, but decides sea battles."),
    };

    /// <summary>Titles a Governor grants for service, in promotion order.</summary>
    public static readonly IReadOnlyList<Rank> Ranks = new[]
    {
        new Rank(0, "Ensign",   "The starting commission that comes with a Letter of Marque."),
        new Rank(1, "Captain",  "Granted after early successes against your sponsor's enemies."),
        new Rank(2, "Major",    "Land grants begin to be worth something at this point."),
        new Rank(3, "Colonel",  "A serious reputation; Governors' daughters start to notice."),
        new Rank(4, "Admiral",  "Top military rank — large land grants accompany it."),
        new Rank(5, "Baron",    "The first noble title; counts heavily at retirement."),
        new Rank(6, "Count",    "Substantial estates come with it."),
        new Rank(7, "Marquis",  "The highest title the game grants in play (Duke and Prince appear only in the epilogue)."),
    };

    /// <summary>The five specialities offered at character creation.</summary>
    public static readonly IReadOnlyList<Speciality> Specialities = new[]
    {
        new Speciality("Skill at Fencing",    "Duels are much easier — you win boardings against bigger crews."),
        new Speciality("Skill at Navigation", "Better sailing speed and manoeuvre, especially against the wind."),
        new Speciality("Skill at Gunnery",    "Broadsides reload faster and hit harder."),
        new Speciality("Wit and Charm",       "Governors, their daughters and tavern informants all treat you better."),
        new Speciality("Skill at Medicine",   "Crew losses to wounds and disease drop; your career lasts longer."),
    };

    /// <summary>Difficulty levels, easiest first.</summary>
    public static readonly IReadOnlyList<DifficultyLevel> Difficulties = new[]
    {
        new DifficultyLevel("Apprentice",   "Gentlest opposition and the smallest share of plunder at retirement."),
        new DifficultyLevel("Journeyman",   "Moderate opposition; a reasonable first serious game."),
        new DifficultyLevel("Adventurer",   "Tougher garrisons and escorts; noticeably better retirement scoring."),
        new DifficultyLevel("Swashbuckler", "Hardest. Best scoring — the only level that reliably reaches the top titles."),
    };

    /// <summary>The six pre-set historical scenarios.</summary>
    public static readonly IReadOnlyList<Expedition> Expeditions = new[]
    {
        new Expedition("Battle of San Juan De Ulua", "John Hawkins",     1569),
        new Expedition("The Silver Train Ambush",    "Francis Drake",    1573),
        new Expedition("The Treasure Fleet",         "Piet Heyn",        1628),
        new Expedition("The Sack of Maracaibo",      "L'Ollonais",       1666),
        new Expedition("The King's Pirate",          "Henry Morgan",     1671),
        new Expedition("The Last Expedition",        "Baron De Pointis", 1697),
    };

    /// <summary>Duelling weapons, as the fencing screen offers them.</summary>
    public static readonly IReadOnlyList<string> Weapons = new[] { "Rapier", "Longsword", "Cutlass" };

    /// <summary>The named rival captains the game can put on the horizon.</summary>
    public static readonly IReadOnlyList<string> RivalPirates = new[]
    {
        "Pegleg", "One-Eye", "El Dragon", "Rivero", "Mansfield", "Vasseur", "Robert Baal", "Le Grand",
    };

    /// <summary>Crew morale bands, worst to best, as the sailing panel prints them.</summary>
    public static readonly IReadOnlyList<string> MoraleBands = new[]
    {
        "PANIC", "SHAKEN", "ANGRY", "FIRM", "STRONG", "WILD!", "WILD!!",
    };

    /// <summary>Crew mood bands shown on the party panel.</summary>
    public static readonly IReadOnlyList<string> CrewMood = new[] { "ANGRY!", "UNHAPPY", "PLEASED", "HAPPY!" };

    /// <summary>Town prosperity bands, from the settlement record's top two bits.</summary>
    public static readonly IReadOnlyList<string> Prosperity = new[]
    {
        "Struggling", "Surviving", "Prospering", "Wealthy",
    };

    /// <summary>Reputation bands the personal-status screen prints.</summary>
    public static readonly IReadOnlyList<string> Reputation = new[]
    {
        "Cowardly", "Promising", "Well Known", "Famous", "Notorious", "Infamous!",
    };

    /// <summary>
    /// Retirement outcomes, best to worst — the station in life the epilogue assigns from wealth, land,
    /// rank, marriage and Pirate Points.
    /// </summary>
    public static readonly IReadOnlyList<string> RetirementStations = new[]
    {
        "King's Advisor", "Governor", "Lt. Governor", "Fleet Admiral", "Rich Banker", "Plantation Owner",
        "Wealthy Merchant", "General", "Sugar Planter", "Merchant Captain", "Councilmember", "Colonel",
        "Shop Owner", "Major", "Tavernkeeper", "Sailing Master", "Sergeant", "Bartender", "Sailor",
        "Farm Hand", "Rogue", "Scoundrel", "Pauper", "Beggar",
    };

    /// <summary>Controls, as the game's own setup and prompt strings describe them.</summary>
    public static readonly IReadOnlyList<ControlBinding> Controls = new[]
    {
        new ControlBinding("F10", "Anywhere", "Quit to DOS. PIR.EXE's INT 82h hook watches for scancode 0x44, restores the interrupt table, sets text mode and exits."),
        new ControlBinding("Arrow keys / joystick", "Menus", "Move the highlight. The setup screen picks between the two."),
        new ControlBinding("Enter / fire button", "Menus and prompts", "Choose the highlighted option. Prompts read \"Press ENTER to continue\" on keyboard and \"Press TRIGGER\" on joystick."),
        new ControlBinding("Left / right", "Sailing", "Turn the ship. You cannot sail straight into the wind — tack across it."),
        new ControlBinding("Up / down", "Sailing", "Raise and lower sail: full sails to travel, battle sails to fight and turn tightly."),
        new ControlBinding("Fire / Enter", "Sea battle", "Fire the broadside that is loaded and bears. Reloading takes time — the panel shows GUNS LOADED / RELOADING."),
        new ControlBinding("Toward the enemy + fire", "Sea battle", "Close and grapple to board. Board only when your crew and morale beat theirs."),
        new ControlBinding("Up / down", "Fencing", "Aim high or low: high slashes swing, low thrusts."),
        new ControlBinding("Left / right", "Fencing", "Press the attack or give ground. Driving a beaten opponent back wins the duel."),
        new ControlBinding("Arrow keys / joystick", "Cargo transfer", "Move goods between holds; \"Press ENTER when done\"."),
        new ControlBinding("Any direction", "Land battle", "Manoeuvre your buccaneers. Terrain and numbers decide it; keep your force concentrated."),
    };

    /// <summary>Setup options the game asks for on first run.</summary>
    public static readonly IReadOnlyList<string> SetupOptions = new[]
    {
        "Graphics: 1) CGA  2) Tandy-1000  3) EGA",
        "Drives: 1) 1 floppy drive  2) 2 floppy drives",
        "Control: 1) Joystick  2) Keyboard",
    };
}
