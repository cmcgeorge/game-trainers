namespace MightAndMagic1Trainer.Game;

/// <summary>One titled chunk of the walkthrough: a heading plus an ordered list of short steps.</summary>
public sealed record WalkthroughSection(string Title, IReadOnlyList<string> Steps);

/// <summary>
/// A spoiler-light solution walkthrough for Might &amp; Magic 1, as read-only reference data
/// (independent of any attached game, like <see cref="MapBook"/> and <see cref="Spellbook"/>).
/// Summarised from community guides (the addictedgamewise newbie walkthrough, the Might &amp;
/// Magic Fandom wiki, and GameFAQs), cross-checked across sources. A few coordinates vary
/// between guides; where they conflict the version corroborated by two or more was used.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<WalkthroughSection> Sections = new[]
    {
        new WalkthroughSection("Getting Started: Party Creation", new[]
        {
            "Build a balanced six-member party with one of each class: Knight, Paladin, Archer, Robber, Cleric, and Sorcerer.",
            "Favor Might, Endurance, Accuracy, and Speed on your fighters, and high Intellect (Sorcerer/Archer) or Personality (Cleric/Paladin) on your casters.",
            "Maximize Endurance early on everyone, since it drives the hit points that keep weak low-level characters alive.",
            "Consider rolling all-female characters to avoid a gender-gated obstacle encountered later in the game.",
            "If creation feels daunting, the game's six pre-made starter characters are perfectly viable for finishing the game.",
            "You begin in the town of Sorpigal (map B-4), the safe hub where you will outfit and train the party.",
        }),
        new WalkthroughSection("Early Survival and Basics", new[]
        {
            "Equip every character's starting gear immediately, then visit the Blacksmith to buy the best weapons and armor you can afford.",
            "Stock up on food at Sorpigal's Food Store, which has the cheapest provisions in the game, since running out of food blocks resting.",
            "Rest to recover hit points and spell points, but only when food remains and no monsters are adjacent.",
            "Spend gold at the Training Center to advance levels; experience alone does not level you up until you train.",
            "Press Search after every battle and in suspicious spots to find hidden gold, gems, and items.",
            "Save your game at the Inn often, which also resets fixed encounters so you can grind them again.",
        }),
        new WalkthroughSection("Sorpigal Town and Dungeon", new[]
        {
            "Learn the town layout: Inn (save), Food Store, Blacksmith, General Store, Tavern, Temple (healing), and Training Center.",
            "Grind the fixed encounters in and around town until the party reaches roughly level 2-3 before venturing out.",
            "Enter the Sorpigal dungeon and retrieve the letter from the old man near coordinates 1-2.",
            "Find the Leprechaun around 11-3 and give him a gem to be teleported to the town of Erliquin.",
            "Explore the lower Sorpigal caverns puzzle, setting the polyhedrons to the right numbers and pulling the lever for a stat and treasure reward.",
        }),
        new WalkthroughSection("Main Early Quests and Signpost Goals", new[]
        {
            "Your overarching goal is to assemble the five prerequisites that validate your Key Card for the Inner Sanctum.",
            "Track down the wizard Agar in Erliquin and the astral brothers (Zam, Zom, and Telgoran) for the directional clues they hand out.",
            "Acquire a Merchant Pass early, as it is required to gain an audience with the castle lords.",
            "Note the numeric clues from NPCs (for example Zam's C-15 and Zom's 1-15) and combine them into overworld map destinations.",
            "Complete the castle lords' quest chains for large experience rewards that fund your training.",
        }),
        new WalkthroughSection("Key Towns: Erliquin and Portsmith", new[]
        {
            "In Erliquin (town 5) meet the wizard Agar hidden behind the Inn by passing through the back wall instead of signing in.",
            "Grind the surrounding wilderness until your Sorcerer learns Fly, the spell that makes overworld travel practical.",
            "Defeat the ogres around C-1 coordinates 5-7 to claim the Merchant Pass needed to see the lords.",
            "In Portsmith (town 2, area B-3) reach the secret room near 12-2 and speak with astral brother Zam for the clue C-15.",
        }),
        new WalkthroughSection("Key Towns: Algary and Dusk", new[]
        {
            "In Algary (town 3, area D-4) visit astral brother Zom near 1-1 to receive the clue 1-15.",
            "Combine Zam's and Zom's clues into the map cell C-1 at 15-15, then Fly there and Search to find the Ruby Whistle.",
            "Buy wolfsbane in Algary, one of the herbs a castle lord will request.",
            "In Dusk (town 4, area E-1) meet Telgoran near 8-0, who tasks you with finding the two astral brothers.",
            "Buy belladonna from the Blacksmith in Dusk for the herb-gathering quest.",
        }),
        new WalkthroughSection("Castles Blackridge and White Wolf", new[]
        {
            "Present your Merchant Pass at the castles to gain an audience with their lords.",
            "Lord Inspectron of Castle Blackridge North sends you to find the Ancient Ruins in Quivering Forest.",
            "Lord Hacker of Castle Blackridge South wants garlic (Sorpigal Blacksmith), wolfsbane (Algary), belladonna (Dusk), and the head of Medusa.",
            "Lord Ironfist of Castle White Wolf runs a quest chain culminating in defeating the Master Archer in Raven's Wood.",
            "Each completed lord quest grants substantial experience, so pursue them even when optional.",
        }),
        new WalkthroughSection("Castles Doom, Alamar, and Dragadune", new[]
        {
            "In Castle Doom (area A-1) navigate the spiral, free the real King Alamar, and obtain the Eye of Goros.",
            "Find the Interleaf clue in Castle Doom near 15-15, which explains the five-portal Inner Sanctum requirement.",
            "At Castle Alamar (area E-3) speak the daily password to the lion statue, then enter the throne room carrying the Eye of Goros to expose the impostor.",
            "Solve the Soul Maze beyond Alamar by mapping its walls to spell the impostor's name, Sheltem.",
            "Castle Dragadune has no lord, but at 13-15 you can convert all your gold into experience and at 1-1 the worthy gain +2 Luck.",
        }),
        new WalkthroughSection("Important Overworld Destinations", new[]
        {
            "Raid the Warrior's/Secret Stronghold (Enchanted Forest, B-2/B-3) to claim the Crystal Key and the Gold Key from the dog statue.",
            "Obtain the King's Pass from Percella the Druid around area A-2, near 0-15, to reach the most restricted areas.",
            "Visit Lord Kilburn at C-3 near 6-14 for the Desert Map, which prevents getting lost while crossing the desert.",
            "Cross the gypsy bridge at A-4: record each character's color and zodiac sign from the gypsy at C-2 (9-11), then answer the sign questions to win the Coral Key.",
            "Reach the Volcanic Island (area C-4) using Walk on Water, then open the volcano with the Coral Key.",
        }),
        new WalkthroughSection("The Five Inner Sanctum Prerequisites", new[]
        {
            "You must hold the Eye of Goros, rescued from the prisoner in Castle Doom.",
            "You must hold the King's Pass, given by the druid in area A-2.",
            "You must hold the Crystal Key, won at the Warrior's Stronghold in area B-2.",
            "You must obtain the Key Card from the Volcano God by answering his riddle with Gala.",
            "Finally, you must activate all five Astral Plane projectors, which validates the Key Card for the Inner Sanctum door.",
        }),
        new WalkthroughSection("Reaching the Volcano God and Key Card", new[]
        {
            "Inside the volcano, set the western dial to B and the eastern dial to J, recalling the dog statue's hint to Remember B.J.",
            "Use the teleporter beyond the dials to reach the chamber of the Volcano God.",
            "Answer the Volcano God's riddle with Gala to be granted the Key Card.",
            "Keep a Sorcerer of at least level 11 available, since only the Astral Spell can reach the Astral Plane.",
        }),
        new WalkthroughSection("The Astral Plane and Endgame", new[]
        {
            "Cast the Astral Spell to enter the Astral Plane, where all walls are invisible and must be mapped carefully.",
            "Find and trigger all five projectors/portals; each one returns you to Sorpigal, so re-cast Astral to return after each.",
            "With all five seeded and the Key Card in hand, navigate to the final door and use the Key Card to enter the Inner Sanctum.",
            "Complete the final confrontation, unmasking Sheltem who had posed as the King, to finish the game.",
            "Meeting the data keeper after the victory opens the Gates to Another World, leading onward to Might and Magic II.",
        }),
        new WalkthroughSection("General Tips", new[]
        {
            "Map every dungeon by hand on grid paper, since hidden wall patterns often spell out puzzle answers.",
            "The Location spell helps you navigate everywhere except the Soul Maze and the Astral Plane, where you must map manually.",
            "Keep movement spells ready: Fly for the overworld, Walk on Water/Etherealize for barriers, and Teleport for shortcuts.",
            "Always carry surplus food so you can rest between fights to restore hit points and spell points.",
            "Train whenever you have the gold, and save at an Inn before any dangerous area so you can recover from a wipe.",
            "Re-visit fixed encounters after saving to grind experience and gold when you need to power up.",
        }),
    };
}
