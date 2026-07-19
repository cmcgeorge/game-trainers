namespace MoriaTrainer.Game;

/// <summary>One level of the Moria dungeon or the town (Confirmed from the manual + <c>faq</c> Q11).</summary>
public sealed record LevelInfo(
    int Depth,           // 0 = town, 1..50 = dungeon
    int Feet,            // depth × 50
    string Name,
    string NotableMonsters,
    string NotableItems,
    string Notes)
{
    public bool IsTown => Depth == 0;
    public bool IsBalrogLevel => Depth >= 49;
}

/// <summary>
/// The 51-level descent reference (town + 50 dungeon levels). Dungeon layouts are **procedurally
/// generated** — there are no fixed maps; this book describes what each depth contains, what items
/// first appear there, and which monsters dominate. Source: <c>moria2.txt</c> §10, <c>faq</c> Q11,
/// and the source's depth-monster and depth-item tables.
/// </summary>
public static class LevelBook
{
    public static readonly IReadOnlyList<LevelInfo> Levels = new[]
    {
        new LevelInfo( 0,    0, "Town",                  "urchins, rogues, drunks, warriors (no XP)",
            "stores 1-6 sell basics; Word-of-Recall scrolls at Temple",
            "Fixed layout, walled, six stores. Day/night cycle. Single down-stair."),
        new LevelInfo( 1,   50, "Level 1",               "mice, kobolds, centipedes, jackals",
            "torch, leather, short sword from town",
            "First descent. Buy supplies first."),
        new LevelInfo( 2,  100, "Level 2",               "blue jelly, giant centipede, skeleton",
            "first potions/scrolls occasionally",
            "Easy. Practice combat and search."),
        new LevelInfo( 3,  150, "Level 3",               "orc, giant ant, yellow mushroom",
            "identify scrolls start appearing",
            "Mushrooms cause hallucination; kill at range."),
        new LevelInfo( 5,  250, "Level 5",               "cave spider, ogre",
            "first ego weapons sometimes",
            "Ogre is the first real bruiser. Carry a real weapon."),
        new LevelInfo( 7,  350, "Level 7",               "wraith",
            "first slay-undead weapons",
            "Wraith drains XP. Slay-undead essential."),
        new LevelInfo( 9,  450, "Level 9",               "vampire",
            "restore-life-levels potions begin",
            "Vampire drains XP and regenerates."),
        new LevelInfo(10,  500, "Level 10",              "young dragon",
            "first dragon-slay weapons",
            "First dragon. Bring a slay-dragon weapon."),
        new LevelInfo(12,  600, "Level 12",              "white dragon (cold)",
            "Healing potions first appear",
            "Stock up on healing potions from here on."),
        new LevelInfo(14,  700, "Level 14",              "white dragon (common)",
            "resist-cold items matter",
            "Resist cold item advised."),
        new LevelInfo(18,  900, "Level 18",              "blue dragon (lightning)",
            "first gain-stat rings occasionally",
            "Lightning breath destroys items. Resist lightning."),
        new LevelInfo(20, 1000, "Level 20",              "blue dragon (common)",
            "amulets of wisdom/charisma first appear",
            "Mid-game. Get free-action and see-invisible before going deeper."),
        new LevelInfo(22, 1100, "Level 22",              "red dragon (fire)",
            "first good ego weapons/crowns",
            "Resist fire essential."),
        new LevelInfo(25, 1250, "Level 25",              "lich, red dragon (common)",
            "Gain-stat potions, Restore-mana potions first appear",
            "Farm gain-stat potions here for permanent stat boosts."),
        new LevelInfo(26, 1300, "Level 26",              "black dragon (acid)",
            "ring of STR/INT/DEX/CON first appear",
            "Acid breath corrodes armor. Body armor soaks some."),
        new LevelInfo(30, 1500, "Level 30",              "green dragon (poison), archlich",
            "ring of speed starts appearing occasionally",
            "No resistance to poison gas. Cure poison ready."),
        new LevelInfo(35, 1750, "Level 35",              "emperor liches begin",
            "Genocide scrolls first appear",
            "Emperor lich: 1520+ HP, drains wands for 40HP/charge. See strategy guide."),
        new LevelInfo(40, 2000, "Level 40",              "ancient dragons, emperor liches",
            "Invulnerability potion, Destruction scroll, Staff of Speed",
            "Deep-game. Speed ≥ 2 needed to outrun liches."),
        new LevelInfo(45, 2250, "Level 45",              "ancient multi-hued dragons begin",
            "Staff of Mass Polymorph",
            "AMHDs: 12000 XP, breath all elements. Pillar-dance at speed 3."),
        new LevelInfo(49, 2450, "Level 49",              "Balrog spawns here",
            "Staff of Dispel Evil",
            "Do not descend past 49 unless Balrog-ready."),
        new LevelInfo(50, 2500, "Level 50",              "Balrog of Moria (the boss)",
            "Ring of Speed, Amulet of the Magi, Gain-Experience potion, Mass Genocide scroll, Rune of Protection scroll, Wand of Drain Life, Staff of Destruction",
            "Farm for endgame gear, then kill the Balrog to WIN."),
    };

    public static LevelInfo? ByDepth(int depth) => Levels.FirstOrDefault(l => l.Depth == depth);
    public static LevelInfo Town => Levels[0];
    public static LevelInfo BalrogLevel => Levels[^1];
}
