namespace DragonWarsTrainer.Game;

/// <summary>A keyed spot on an area map: its (x, y) grid square and what's there.</summary>
public sealed record MapLocation(string Name, int X, int Y, string Notes = "")
{
    public string Coord => $"({X}, {Y})";
}

/// <summary>One explorable area: its board id, grid size, notes, and notable keyed locations.</summary>
public sealed record MapArea(int Id, string Name, int Width, int Height, string Notes,
    IReadOnlyList<MapLocation> Locations)
{
    public string Size => $"{Width}×{Height}";
    public string Header => $"0x{Id:X2}  {Name}   ({Size})";

    public int GridWidth => Locations.Count == 0 ? Width : Math.Max(Width, Locations.Max(l => l.X) + 1);
    public int GridHeight => Locations.Count == 0 ? Height : Math.Max(Height, Locations.Max(l => l.Y) + 1);
}

/// <summary>
/// Dragon Wars map/coordinate reference and the live-position ("Heap") layout, sourced from the
/// <c>fraterrisus/dragonjars</c> engine (<c>Heap.java</c>, <c>Lists.MAP_NAMES</c>) and the
/// hitchhikerprod maps. The party's live position lives in a 256-byte global "Heap" whose address
/// changes each session, so the Maps tab locates it at runtime (see <c>HeapLocator</c>) and uses
/// these coordinates to drive the teleport helper. Coordinates are (X = column, Y = row), origin
/// north-west.
/// </summary>
public static class MapBook
{
    // --- Heap (live-position) byte layout ------------------------------------
    public const int HeapSize = 256;
    public const int OffPartyY = 0x00;
    public const int OffPartyX = 0x01;
    public const int OffBoardId = 0x02;
    public const int OffFacing = 0x03;
    public const int OffBoardMaxX = 0x21;
    public const int OffBoardMaxY = 0x22;

    public const int MaxBoardId = 0x27;

    public static readonly string[] Facings = { "North", "East", "South", "West" };

    public static string FacingName(int f) => f >= 0 && f < Facings.Length ? Facings[f] : $"?({f})";

    /// <summary>Board names indexed by board id (0x00..0x27), from <c>Lists.MAP_NAMES</c>.</summary>
    public static readonly string[] MapNames =
    {
        "Dilmun (Overworld)", "Purgatory", "Slave Camp", "Guard Bridge #1", "Salvation",
        "Tars Ruins", "Phoebus", "Guard Bridge #2", "Mud Toad", "Byzanople",
        "Smugglers Cove", "War Bridge", "Scorpion Bridge", "Bridge of Exiles", "Necropolis",
        "Dwarf Ruins", "Dwarf Clan Hall", "Freeport", "Magan Underworld", "Slave Mines",
        "Lansk", "Sunken Ruins Above", "Sunken Ruins Below", "Mystic Wood", "Snake Pit",
        "Kingshome", "Pilgrim Dock", "Depths of Nisir", "Old Dock", "Siege Camp",
        "Game Preserve", "Magic College", "Dragon Valley", "Phoebus Dungeon", "Lanac'toor's Lab",
        "Byzanople Dungeon", "Kingshome Dungeon", "Slave Estate", "Lansk Undercity", "Tars Dungeon",
    };

    public static string MapName(int id) => id >= 0 && id < MapNames.Length ? MapNames[id] : $"Map 0x{id:X2}";

    private static IReadOnlyList<MapArea>? _areas;
    public static IReadOnlyList<MapArea> Areas =>
        _areas ??= RawAreas.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static readonly MapArea[] RawAreas = new MapArea[]
    {
        new(0x02, "Slave Camp", 32, 32,
            "Gain the residents' trust to explore the camp and meet companions, or face a ghost town.",
            new MapLocation[]
            {
                new("The Tavern", 10, 2, "Recruit Louie."),
                new("The Nature Axe", 4, 2, "Use Forest Lore to find the Nature Axe."),
                new("The Creepy Old Wizard", 7, 4, "Use magic skill to unlock his treasure room."),
                new("The Arms Cache", 7, 7, "Locked chest (difficulty 1) behind wizard's house."),
                new("The Sick Man", 10, 6, "Heal him for exposition and spells."),
                new("The Campfire", 10, 11, "Rest to restore Health, Stun, and Power."),
                new("The Blind Man", 2, 5, "Exposition on Namtar, Drake, and Master Mages."),
                new("The Universal Shrine", 0, 16, "Exposition on the various gods of Dilmun."),
                new("The Dice Game", 4, 13, "Exposition on the political climate."),
            }),

        new(0x03, "Guard Bridge #1", 32, 32,
            "Allows passage between Forlorn and Isle of the Sun, but you won't get there without some hassle.",
            new MapLocation[]
            {
                new("Exit: Isle of the Sun (North)", 12, 8),
                new("Exit: Forlorn (South)", 12, 6),
                new("Lanac'toor's Rock", 3, 1, "Hint on how to restore the statue of Lanac'toor."),
                new("The Arms Cache", 7, 2, "Contains Helm, Shield, Bladed Flail, and 4 Dragon Stones."),
                new("The Southern Approach", 4, 2, "Guards demand Citizenship Papers and a bribe."),
                new("The Northern Approach", 4, 5, "Guards demand Citizenship Papers."),
                new("The Bridge", 4, 3, "Guards attack here if not appeased, or 10% chance of Rats."),
                new("The Bridge", 4, 4, "Guards attack here if not appeased, or 10% chance of Rats."),
            }),

        new(0x04, "Salvation", 32, 32,
            "The destination of many a pilgrim's trek, and your first step towards the endgame.",
            new MapLocation[]
            {
                new("Exit: Isle of Salvation (North)", 19, 20),
                new("Exit: Magan Underworld (Stairs)", 19, 19, "Stairs down."),
                new("Exit: Depths of Nisir", 13, 2, "Plunge into the depths of Nisir."),
                new("Exit: Depths of Nisir", 14, 3, "Plunge into the depths of Nisir."),
                new("Shrine of the Universal God", 13, 11, "Show the Sword of Freedom for 500 XP and blessings (+3 stats)."),
                new("Arms Cache Guard Fight", 1, 8, "Defeat guards to access cache."),
                new("Arms Cache", 1, 9, "Chest (difficulty 5) with heavy gear and 30 Dragon's Eyes."),
                new("The Mountain Pass", 5, 7, "Use Intelligence/Mountain Lore and Climb to bypass fight."),
            }),

        new(0x05, "Tars Ruins", 32, 32,
            "A ruined city, destroyed by dragons. Random and fixed encounters abound.",
            new MapLocation[]
            {
                new("Exit: Dilmun (West)", 20, 4),
                new("Exit: Tars Underground", 15, 15, "Stairs down under moved stone slab."),
                new("Entrance", 0, 8, "Color text indicating Tars is destroyed."),
                new("The Tracks (Start)", 2, 8, "Tracks start here."),
                new("The Tracks", 4, 8, "Notice tracks; use Tracker to follow to stone slab."),
                new("Spells on the Wall", 1, 1, "Find scrolls: H:Air Summon, H:Elvar's Fire, S:Exorcism, S:Guidance."),
                new("Spells Guard Fight", 2, 2, "Fight to reach Spells on the Wall."),
                new("The Dragon Pit", 7, 6, "Use Town/Arcane Lore to read about dragon defense."),
                new("The Dragon Pit", 9, 8, "Use Town/Arcane Lore to read about dragon defense."),
                new("The Guardian Snake", 14, 7, "Defeat snake to get Large Shield and Firesword."),
                new("The Pit Trap", 13, 14, "Deals 8 damage unless tracking, finding traps, or slab is moved."),
            }),

        new(0x06, "Phoebus", 32, 32,
            "Home of Mystalvision, High Priest of the Temple of the Sun and chief Sun Mage.",
            new MapLocation[]
            {
                new("Exit: Isle of the Sun (South)", 5, 10),
                new("Exit: Phoeban Dungeon (Stairs)", 2, 14, "Exit stairs from dungeon."),
                new("Buck Ironhead's Enlistment Office", 14, 2, "Travel to Siege Camp or pay draft-dodging fee."),
                new("The Barracks", 6, 9, "Fight with ten Stosstrupen."),
                new("The Chest of Scrolls", 1, 10, "Chest (difficulty 2) containing powerful scrolls."),
                new("Thieves Fight", 1, 11, "Fight thieves to reach Chest of Scrolls."),
                new("The Armor Cache", 1, 15, "Chest (difficulty 1) containing Plate Mail and Tri-Cross."),
                new("Guards Fight", 2, 15, "Fight guards to reach Armor Cache."),
                new("The Ominous Fellow", 3, 14, "Delivers message from Mystalvision and flees."),
                new("The Other Armor Cache", 11, 15, "Chest (difficulty 2) with Magic Plate and Fire Spear."),
                new("Dirty Rats Fight", 13, 15, "Defeat Rats to access Other Armor Cache."),
                new("Mad Dogs Fight", 14, 15, "Defeat Dogs to access Other Armor Cache."),
                new("The Icarian Triumph Tavern", 15, 15, "Recruit Ulrik; learn of Berengaria in Mud Toad."),
                new("Mystalvision's Temple", 8, 13, "Fight Mystalvision to enter Phoeban Dungeon (12,03)."),
            }),

        new(0x07, "Guard Bridge #2", 32, 32,
            "Allows passage between the Isle of the Sun and Lansk. Also contains several tasty bits of kit.",
            new MapLocation[]
            {
                new("Exit: Lansk (North)", 14, 13),
                new("Exit: Isle of the Sun (South)", 14, 11),
                new("The Customs Inspection", 3, 3, "Charges larger of $100 or 20% gold; fight Pikemen if refused."),
                new("The Oath of Fealty", 3, 6, "Comply with Lansk neutrality laws or fight guards."),
                new("The Barracks", 1, 1, "Loot chest (Lockpick 3 or Key) for scrolls and Runed Flail."),
                new("The Barracks", 2, 3, "Sleeping guards barracks."),
                new("The Armory", 4, 6, "Chest contains Axe of Kalah, Holy Mace, Gem Helm, and Gauntlets."),
                new("The Armory", 5, 8, "Fight Pikemen on second entry."),
                new("Armory Pit Trap", 5, 6, "Pit trap deals 1d8 damage."),
            }),

        new(0x08, "Mud Toad", 32, 32,
            "A sinking, rotting, falling-apart old city. Find the Golden Boots here to open new regions.",
            new MapLocation[]
            {
                new("Exit: Quag (East)", 26, 8),
                new("Exit: Quag (South)", 25, 7),
                new("Exit: Lanac'toor's Laboratory", 7, 10, "Stairs down under repaired statue."),
                new("The Town Healer", 10, 4, "Charges $4 per point of Health."),
                new("The Souvenir Shop", 13, 6, "Sells Dragon Stones, ammunition, and the critical Ankh."),
                new("The Cavern Tavern", 12, 14, "Barkeep shares gossip."),
                new("Berengaria", 13, 14, "Meet Berengaria for scrolls: S:Rage of Mithras, S:Holy Aim, and more."),
                new("The Statue of Lanac'toor", 7, 10, "Repair with stone body parts for 500 XP and stairs."),
                new("The Temple of the Mud Toad", 4, 13, "Quest to stop the Mud Leak; rewards Golden Boots (+2 AC, jump)."),
                new("The Mud Leak", 5, 12, "Cast D:Create Wall to block mud leak."),
                new("The Mud Leak", 6, 12, "Cast D:Create Wall to block mud leak."),
                new("The Crumbling Walls", 14, 6, "Use Climb to pass through rock slide."),
                new("The Crumbling Walls", 14, 12, "Use Climb to pass through rock slide."),
                new("The Militia Fight", 2, 2, "Defeat militia to access their treasure."),
                new("Militia Treasure", 6, 2, "Contains scrolls, Barbed Flail, Mountain Sword, and Lucky Boots."),
            }),

        new(0x09, "Byzanople", 32, 32,
            "A city under siege by Kingshome. Gain entry via secret paths or open the gates to end the war.",
            new MapLocation[]
            {
                new("Exit: Siege Camp", 9, 15),
                new("Exit: Byzanople Dungeon (Long Tunnel)", 9, 1),
                new("Exit: Byzanople Dungeon (Short Tunnel)", 7, 4),
                new("Exit: Siege Camp (Final Fight)", 2, 5),
                new("Exit: Byzanople Dungeon (Stairs)", 6, 9),
                new("Hydra Corner", 9, 1, "Boulder blocks dungeon tunnel; stepping 1N triggers Hydra fight."),
                new("Hydra Corner", 9, 2, "Boulder blocks dungeon tunnel."),
                new("The Sappers", 7, 4, "Sappers' tunnel leads down to dungeon."),
                new("The Siege Run", 2, 3, "Red squares; each step deals 1 HP damage."),
                new("The Siege Run", 5, 11, "Red squares; each step deals 1 HP damage."),
                new("The City Gates", 3, 9, "Open to end war; throws party in Kingshome Dungeon (00,15)."),
                new("The City Gates", 4, 9, "Open to end war."),
                new("The Back Way", 2, 10, "Locked door (level 1) leading to the hills."),
                new("Princess Myrilla", 3, 1, "Sneak up at (02,01) to meet her and get dungeon intro."),
                new("Princess Myrilla Encounter", 2, 1, "Sneak up on Myrilla and guards."),
                new("The Dungeon Stairs", 6, 9, "Stairs down to Byzanople Dungeon."),
                new("The Secret Passage", 9, 7, "Secret passage to hills above Siege Camp."),
                new("The Hidden Shield", 13, 4, "Chest (difficulty 3) containing the Fire Shield."),
                new("Secret Door", 12, 12, "Leads south to hidden Fire Shield chest."),
                new("Marik's Armory", 7, 11, "Sells Plate Mail and Dragon Stones."),
                new("Bart's Weaponsmithing", 8, 11, "Sells Long Mace."),
                new("Town Healer", 9, 11, "Charges $4 per point of Health."),
            }),

        new(0x0A, "Smugglers Cove", 32, 32,
            "Home base for Ugly and his crew of pirates, and the good ship Prairie Madness which you'll eventually steal.",
            new MapLocation[]
            {
                new("Exit: Quag (East)", 25, 13),
                new("Exit: Necropolis (Fake Boat Dock)", 2, 1, "Takes you to Necropolis (07,14)."),
                new("Exit: Boat Dock (Real Boat Dock)", 1, 1, "Takes you to all the places a normal Boat Dock takes you."),
                new("The Statue of Irkalla", 6, 5, "Sacrifice any item and make a Spirit check to receive Irkalla's blessing."),
                new("The Waterfront Beast", 6, 2, "A Serpent Swimmer attacks."),
                new("Ugly's Hideout", 3, 3, "Speak their language (costs $50+). W door leads to fight, S door to Necropolis."),
            }),

        new(0x0B, "War Bridge", 32, 32,
            "Simple enough, if you have the Governor's Pass from Lansk. Just watch out for the Murk Tree.",
            new MapLocation[]
            {
                new("Exit: Isle of Lansk (West)", 17, 12),
                new("Exit: Quag (East)", 19, 12),
                new("The Bridge", 2, 3, "Guards demand Governor's Pass."),
                new("The Bridge (Guards)", 3, 3, "Fight Pikemen and Guards if you don't have Governor's Pass."),
                new("The Bridge", 4, 3, "Guards demand Governor's Pass."),
                new("The Random Murk Tree", 7, 3, "A fight with a Murk Tree."),
                new("The Quag Visitor's Bureau", 6, 6, "1 in 5 chance that Crazed Old Ladies attack instead of helping."),
            }),

        new(0x0C, "Scorpion Bridge", 32, 32,
            "Connects Rustic Isle to the island with the Magic College.",
            new MapLocation[]
            {
                new("Exit: Rustic (West)", 30, 19),
                new("Exit: Magic College isle (East)", 32, 19),
                new("The Scorpion Guards", 2, 3, "Show Enkidu Totem to pass."),
                new("The Scorpion Guards", 2, 4, "Show Enkidu Totem to pass."),
                new("Scorpion Guard Combat", 3, 3, "Fight Scorpion Guards if no Enkidu Totem."),
                new("Scorpion Guard Combat", 3, 4, "Fight Scorpion Guards if no Enkidu Totem."),
                new("The Secret Treasure", 4, 6, "Bones, Barbed Flail, and Magic Shield."),
            }),

        new(0x0D, "Bridge of Exiles", 32, 32,
            "The most straightforward board in the entire game. No monsters, just a one-way door and a bunch of screaming.",
            new MapLocation[]
            {
                new("Exit: Isle of the Damned (West)", 6, 18),
                new("Exit: King's Isle (East)", 8, 18, "Leads to a fight with Goblins."),
                new("The One-way Door", 2, 4, "One-way door that traps you on the Isle of the Damned."),
            }),

        new(0x0E, "Necropolis", 32, 32,
            "Nergal's summer palace, aka where he hangs out when Irkalla's angry at him.",
            new MapLocation[]
            {
                new("Exit: Boat Dock", 7, 14, "Only active once Ugly is dead and boat is stolen."),
                new("Exit: Underworld Stairs", 0, 7, "Stairs down to Underworld and Well of Souls."),
                new("The Grim Guardians", 2, 12, "Guards hit you with 1d6 breath weapons."),
                new("The Grim Guardians", 2, 10, "Guards hit you with 1d6 breath weapons."),
                new("The Random Chest", 1, 11, "Contains Stone Trunk, Black Helm, Magic Chain, and Dead Bolt."),
                new("The Stone Demon", 7, 6, "Charge him quickly to make him run away; uses 1d4 breath weapon."),
                new("Nergal's Throne Room", 5, 7, "Feed Nergal Mushrooms, serve him to get Silver Key, Holy Spear, and scrolls."),
                new("The Well of Souls", 0, 7, "Access to the Underworld linking with the Well of Souls."),
                new("The Web-filled Hallway", 13, 9, "Spider fights. Fire spell burns out spiders and webs."),
                new("The Portal of Power", 15, 11, "Teleports back to boat dock, or random destination before boat is taken."),
            }),

        new(0x0F, "Dwarf Ruins", 32, 32,
            "The ruined surface entrance on King's Isle leading down into the petrified home of the dwarves.",
            new MapLocation[]
            {
                new("Exit: Dwarf Clan Hall", 0, 4, "Stairs down to Dwarf Clan Hall (14,08)."),
                new("The Proud Statue", 4, 4, "Use Jade Eyes to replace them, opening the tunnel at (02,04)."),
                new("The Tunnel", 2, 4, "Tunnel into the mountain opens when Jade Eyes are used on the statue."),
                new("The Dwarf Hammer Chest", 1, 6, "Locked chest (difficulty 4) containing the Dwarf Hammer."),
            }),

        new(0x10, "Dwarf Clan Hall", 32, 32,
            "A grim underground habitat for a clan of dwarves who ran into a Gorgon and have all been petrified.",
            new MapLocation[]
            {
                new("Exit: Dwarf Ruins", 14, 8, "Stairs up to the Ruins (00,04)."),
                new("Exit: Underworld", 14, 0, "Stairs down to the Underworld (10,21)."),
                new("The Crystal Wall", 9, 8, "Impassable but invisible barrier. Use Soften Stone to bypass."),
                new("The Forge", 5, 8, "Bring Skull of Roba here to forge the Sword of Freedom."),
                new("The Gorgon", 9, 4, "Gorgon combat (disappears if dwarves are revived without stealing)."),
                new("The Petrified Dwarves", 8, 12, "Cast D:Soften Stone to revive dwarves and reactivate the Forge."),
                new("The Petrified Dwarves", 10, 14, "Cast D:Soften Stone to revive dwarves and reactivate the Forge."),
                new("The Treasury", 6, 13, "Chest with Dragon Helm, Spiked Flail, scroll, and eight Bombs."),
                new("Automata Combat", 7, 11, "Wakes up when Treasury is opened before reviving dwarves."),
                new("The Other Automata", 8, 15, "Automata combat (disappears if revived without stealing)."),
                new("The Hidden Treasury", 4, 15, "Contains Crush Mace, Spell Staff, and Healing Potion."),
                new("The Even More Hidden Treasury", 0, 15, "Contains $1000 and the Dragon Horn."),
            }),

        new(0x12, "Magan Underworld", 48, 48,
            "The Underworld joins several places Topside and is where you return in the endgame to defeat Namtar.",
            new MapLocation[]
            {
                new("Exit: Purgatory", 13, 4, "Stairs up to Purgatory (07,12)."),
                new("Exit: Tars Underground", 19, 4, "Stairs up to Tars Underground (00,05)."),
                new("Exit: Lansk Undercity", 16, 14, "Stairs up to Lansk Undercity (14,05)."),
                new("Exit: Necropolis", 27, 16, "Stairs up to Necropolis (00,07)."),
                new("Exit: Mystic Wood", 2, 6, "Stairs up to Mystic Wood (04,15)."),
                new("Exit: Dwarf Clan Hall", 10, 21, "Stairs up to Dwarf Clan Hall forge (00,08)."),
                new("Exit: Salvation", 19, 19, "Stairs up to Salvation (03,04)."),
                new("The Refresh Pool", 9, 14, "Refreshes party Power. Starting point of Endgame."),
                new("Exposition Cavern", 12, 29, "Learn about Namtar's invasion and the Sword of Freedom."),
                new("The Slicer Chest", 11, 24, "Locked chest (difficulty 3) with The Slicer and 10 Dragon Stones."),
                new("The Rusty Axe Chest", 31, 2, "Locked chest with Rusty Axe, Speed Wand, 3 Bombs, 10 Dragon Stones."),
                new("Entrance to Irkalla's Realm", 26, 16, "Requires praying at a statue of Irkalla topside to pass."),
                new("The Leap of Faith", 28, 17, "Step over the railing to the North for +5 CP."),
                new("Isle of Woe (Hop From)", 2, 17, "Use Golden Boots to hop over to the Isle of Woe."),
                new("The Isle of Woe", 4, 16, "Free Irkalla with Silver Key; get Sword of Freedom later."),
                new("The Isle of Woe", 6, 18, "Free Irkalla with Silver Key; get Sword of Freedom later."),
                new("The Evil Fairies", 20, 20, "Accept bargain, refuse, or cast D:Scare to pass."),
                new("The Root of Salvation", 19, 17, "Island to defeat Namtar for the final time; use body to win."),
                new("The Root of Salvation", 21, 19, "Island to defeat Namtar for the final time; use body to win."),
                new("The Well of Souls", 3, 11, "Throw dead party member in to resurrect them."),
            }),

        new(0x13, "Slave Mines", 32, 32,
            "An obvious way out of Purgatory but far from the easiest: you're stripped of gear and chained.",
            new MapLocation[]
            {
                new("Entrance: Purgatory (One-way)", 7, 8, "One-way entrance from Purgatory."),
                new("Exit: Slave Estate", 5, 11, "Takes you to Slave Estate (05,12)."),
                new("Guard Patrols", 8, 8, "Sneer at you in chains, fight you once freed."),
                new("Guard Patrols", 9, 4, "Guards beat you up regardless of chains."),
                new("Guard Patrols", 3, 10, "Guards beat you up regardless of chains."),
                new("Guard Patrols", 2, 13, "Guards beat you up regardless of chains."),
                new("Snake Pit", 3, 0, "Fight with up to 19 Snakes."),
                new("Spider Nest", 2, 3, "Fight with up to 4 Spiders."),
                new("A Pile of Stones", 12, 8, "Dragon Stones and Rock for Crude Hammer."),
                new("The Old Pick Handle", 7, 1, "Pick handle needed for Crude Hammer."),
                new("The Battered Cup", 2, 15, "Drinking vessel."),
                new("The Trickling Spring", 12, 12, "Source of water."),
                new("The Dying Old Man", 3, 6, "Give him water; his Laces are the last item for the Crude Hammer."),
                new("The Trash Pile", 13, 5, "Your old equipment. Only retrievable after breaking chains."),
                new("The Secret Cache", 4, 11, "Locked chest (difficulty 1) with War Axe, Gauntlets, Magic Sword, and Bolt."),
                new("The Exit", 5, 12, "If still chained, guards drag you back; otherwise, fight Cruel Slave Boss."),
            }),

        new(0x01, "Purgatory", 32, 32,
            "The starting prison. Level up in the Arena, recharge Power, then escape via the Morgue " +
            "(friendly Slave Camp flag) or swim out through the Hole in the Wall.",
            new MapLocation[]
            {
                new("Game Start", 20, 13),
                new("Phoebus's Tavern", 25, 27, "Recruit Ulrik."),
                new("Low Magic Spell Shoppe", 3, 22),
                new("Black Market Merchant", 12, 30),
                new("Power Recharge Pool", 23, 2, "Restores 100% Power to all."),
                new("Statue of Irkalla", 6, 13, "Sacrifice items for her blessing (flag 0x80)."),
                new("Apsu Waters", 7, 12, "Exit portal to the Magan Underworld."),
                new("The Arena Master", 19, 26, "Acquire Citizenship Papers."),
                new("Humbaba (quest monster)", 31, 31),
                new("The Morgue", 31, 10, "Escape Purgatory; sets friendly flag 0x40."),
                new("Hole in the Wall", 25, 8, "Escape via the bay (needs Swim)."),
            }),

        new(0x00, "Dilmun (Overworld)", 48, 48,
            "The wrapping overworld connecting every region. Entrances to the towns and dungeons, plus " +
            "the free Forlorn heal/recharge pool.",
            new MapLocation[]
            {
                new("Entrance to Purgatory", 13, 4),
                new("Entrance to Slave Camp", 11, 3),
                new("Entrance to Slave Estate", 17, 7),
                new("Entrance to Tars Ruins", 21, 4),
                new("Free Heal & Power Pool", 14, 1, "Forlorn Peninsula."),
                new("Guard Bridge #1", 12, 7, "Forlorn ↔ Isle of the Sun."),
                new("Entrance to Phoebus", 5, 11),
                new("Entrance to Mystic Wood", 2, 6),
                new("Guard Bridge #2", 14, 12, "Sun ↔ Lansk."),
                new("Entrance to Lansk", 16, 14),
                new("War Bridge", 18, 12, "Lansk ↔ Quag."),
                new("City of the Yellow Mud Toad", 25, 8),
                new("Entrance to Necropolis", 27, 15),
                new("Entrance to Kingshome", 18, 27),
                new("Entrance to Byzanople", 7, 27),
                new("Entrance to Dwarf Ruins", 10, 21),
                new("Entrance to Old Dock", 14, 17),
                new("Entrance to Freeport", 43, 23),
                new("Entrance to Sunken Ruins", 38, 15),
                new("Mount Salvation / Nisir", 19, 19),
            }),

        new(0x11, "Freeport", 16, 16,
            "A pirate town reached by boat. Cheap shields, a recruitable companion, and the Order of " +
            "the Sword — plus a deadly fake Sword of Freedom.",
            new MapLocation[]
            {
                new("Boat Dock", 14, 14),
                new("Ryan's Armor Shop", 5, 14, "Large Shields for $100."),
                new("Brews Brothers Tavern", 14, 7, "Recruit Halifax."),
                new("Order of the Sword", 14, 8, "Stone Hands and the Spell Staff."),
                new("Fake Sword of Freedom", 3, 4, "Trap — picking it up incinerates characters."),
            }),

        new(0x14, "Lansk", 32, 32,
            "Dust off your Bureaucracy skill and hunt for paperwork. No random encounters, but tough fixed ones.",
            new MapLocation[]
            {
                new("Exit: Isle of Lansk (South)", 16, 13),
                new("Exit: Lansk Undercity", 5, 8, "Stairs down; requires bribing an official."),
                new("The Dragon Pit", 7, 6, "A well-fortified tower and an extremely overfed dragon."),
                new("The Druid's Mace", 3, 7, "A weapon sitting inside an unattended building."),
                new("The Governor's Office", 4, 4, "Start here to obtain Papers that need to be stamped."),
                new("Department of Lubrication", 6, 14, "Use Papers for stamp or offer a $500 bribe to open the undercity stairs."),
                new("Visitor's Information Bureau", 11, 4, "Trade stamped Papers for a Governor's Pass to cross the War Bridge."),
                new("Visitor's Registration Department", 5, 4, "Ignore warnings; tells you to head to the Visitor's Information Bureau."),
                new("Office of the Bureau of Departments", 3, 11, "An empty office."),
                new("Quarter Master's Office", 12, 13, "Informed that Slaveholder Mog has died and left you his estate."),
            }),

        new(0x15, "Sunken Ruins Above", 32, 32,
            "The upper level of the Sunken Ruins in the Eastern Isles; you'll need a way to survive underwater.",
            new MapLocation[]
            {
                new("Exit: Eastern Isles", 38, 15),
                new("Exit: Sunken Ruins Below (Down)", 5, 3, "Stairs down to the lower level."),
                new("The False Door", 2, 7, "Hard to unlock; use the secret door 1N of here for free instead."),
                new("The Dead Wood", 4, 3, "Contains Driftwood, Flotsam, and a Spiked Flail."),
                new("The Open Well", 5, 4, "Need Water Potion to survive underwater."),
            }),

        new(0x16, "Sunken Ruins Below", 32, 32,
            "Another small, wrapping dungeon map with a spinner. You'll need light and a compass.",
            new MapLocation[]
            {
                new("Exit: Sunken Ruins Above (Up)", 5, 4, "Stairs up to the upper level."),
                new("The Magic Clam", 1, 6, "Say no to taking the skull; take the whole Clam to get Roba's Skull."),
                new("The Secret Closet", 4, 2, "Locked chest (difficulty 4) containing summoning scrolls and Dragon Stones."),
                new("Javy Dones' Locker", 7, 7, "Requires Lockpick 2; contains the Trident, Dragon Plate, and Dragon Sword."),
            }),

        new(0x17, "Mystic Wood", 32, 32,
            "A useful conduit between the two worlds, with a Transportation Nexus and an easy route to the Underworld.",
            new MapLocation[]
            {
                new("Exit: Transportation Nexus", 7, 5, "Travel to matching Nexus on Quag or King's Isle."),
                new("Exit: Underworld (Well)", 4, 15, "Use Climb to drop down into the Underworld."),
                new("Enkidu's Shrine", 2, 13, "Wrestle Enkidu for Druid Magic 2 and spells."),
                new("Beast Horn", 2, 15, "Beast Horn sitting at the foot of Enkidu's statue."),
                new("The Mushroom Log", 13, 14, "Pick up some Mushrooms before you visit the Necropolis."),
                new("The Tracks", 7, 9, "Use Tracker to go on a roundabout walk to the Nexus clearing at (07,03)."),
                new("The Ring", 14, 8, "Stand here, face South, and use Swim to retrieve The Ring."),
                new("The Mysterious Island", 13, 5, "Use Golden Boots at (11,05) to hop over. Shed blood for Enkidu Totem."),
                new("The Lagooners", 1, 5, "Defeat lagooners for chest with Plate Mail, Great Bow, and Dragon Stones."),
                new("Zaton's Grave", 5, 1, "Use Soul Bowl to revive Zaton's spirit for 500 XP and Druid scrolls."),
                new("More Druid Magic Scrolls", 1, 0, "Locked chest containing several Druid Magic scrolls."),
            }),

        new(0x18, "Snake Pit", 32, 32,
            "A vacation home for the criminally insane, or at least those declared criminals by Namtar.",
            new MapLocation[]
            {
                new("Exit: Isle of the Damned (East)", 3, 19),
                new("Exit: King's Isle (Ferry)", 20, 26, "The ferrymaster takes you to the King's Isle ambush point."),
                new("The Boathouse", 12, 10, "Show King Drake's Signet Ring to pass; leads to the ferrymaster at (11,14)."),
                new("Loose Branches", 8, 13, "Pick up Branches that can cast D:Beast Call if Charged first."),
                new("The Lonely Druid", 4, 1, "Show Branches or use Druid Magic to learn D:Beast Call."),
                new("The Stone Head", 1, 9, "Pick up the washed-up Stone Head to repair Lanac'toor's statue in Mud Toad."),
                new("The Mad Artist", 11, 2, "Read paragraph #76."),
                new("Josephina the Dwarf", 6, 8, "Offers a plot hint about using the Jade Eyes at the Dwarf Ruins."),
                new("The Useless Hint", 9, 7, "A mad woman tells you \"the King is near!\""),
                new("Sad Jester", 7, 6, "Meet a sad jester outside King Drake's secret chambers."),
                new("The Sad Remains of King Drake", 7, 8, "King Drake's skeleton, Signet Ring, Jewels, and a locked chest 1S."),
            }),

        new(0x19, "Kingshome", 32, 32,
            "Namtar is holed up in Drake's old chambers. No one to fight here, so feel free to explore.",
            new MapLocation[]
            {
                new("Exit: King's Isle (South/West)", 18, 26),
                new("Exit: King's Isle (East)", 19, 27),
                new("Exit: Kingshome Dungeon", 6, 8, "Stairs down; accessible with D:Soften Stone after meeting Namtar."),
                new("Namtar's Bedroom", 7, 8, "Read paragraph #131. The door South of here is one-way."),
                new("The Guardrooms", 4, 5, "Locked doors with nothing behind them."),
                new("The Guardrooms", 10, 5, "Locked doors with nothing behind them."),
                new("Family Portraits", 3, 12, "Backstory on Drake's kids, who now run Byzanople."),
                new("The Armory", 11, 12, "Chest with weapons, Rare Books, Magic Chain, Lucky Boots, and Royal Robe."),
                new("The King's Wardrobe", 3, 7, "A large stash of Pilgrim Robes."),
                new("The Library", 11, 7, "Nothing interesting."),
                new("The Front Door", 7, 0, "Namtar's Guards prevent entry unless you show the Signet Ring."),
            }),

        new(0x1A, "Pilgrim Dock", 32, 32,
            "Join a throng of pilgrims heading up to Mount Salvation. Bring Pilgrim Robes or fight Stosstrupen.",
            new MapLocation[]
            {
                new("Exit: Isle of Salvation (West)", 17, 21),
                new("Exit: Isle of Salvation (South)", 18, 20),
                new("Exit: Isle of Salvation (East)", 19, 21),
                new("Exit: Salvation (Teleporter)", 7, 15, "Teleporter west of your Jail Cell sends you straight to Salvation."),
                new("The Checkpoint", 2, 4, "Equip Pilgrim Robes to pass, or fight. Losing throws you in Jail Cell."),
                new("The Jail Cell", 2, 1, "Locked door. Secret door to the West leads to an escape tunnel."),
                new("The Empty Cell", 3, 1, "Requires Lockpick 3; empty."),
                new("The Crying Prisoner", 4, 1, "Requires Lockpick 1. Hints about secret tunnel and Nisir swamp."),
                new("Statue of the Universal God", 5, 4, "Read paragraph #84."),
            }),

        new(0x1B, "Depths of Nisir", 32, 32,
            "A large, wrapping cave system requiring light and a compass. Direct path leads to Namtar.",
            new MapLocation[]
            {
                new("Exit: Underworld (Up)", 8, 15, "Stairs up to the Underworld from the starting area."),
                new("The Icy Caves", 11, 15, "Break through with D:Soften Stone. Icy winds snuff light and deal 1 HP damage."),
                new("The Other Icy Caves", 6, 15, "Mystalvision teleports you here. Turn West and cast D:Soften Stone to exit."),
                new("The Guard Barracks", 22, 24, "Pass through here to get to Buck Ironhead at (27,18)."),
                new("The Flaming Corridor", 21, 29, "Flaming corridor leading to Mystalvision. Extinguishes light."),
                new("The Twin Warrens", 14, 7, "Nearly-identical rooms with a central teleporter linking them."),
                new("The Invisible Maze", 8, 6, "Full of invisible walls; cast H:Reveal Glamour to reveal them."),
                new("The Chasm", 23, 11, "Cast H:Air Summon to get ferried across the chasm to (27,12)."),
                new("The Swamp Under the Mountain", 5, 22, "Enter through locked door. A sign you're getting close to Namtar."),
                new("Hell on Earth", 24, 31, "Full of pit traps. Cast D:Soften Stone in the spiral to teleport to Battle Plain."),
                new("The Battle Plain", 21, 0, "Namtar's army waits South. Use Dragon Gem here to summon the Dragon Queen."),
            }),

        new(0x1C, "Old Dock", 32, 32,
            "A transit point for multiple ferries, though you probably won't come here until you're ready for Namtar.",
            new MapLocation[]
            {
                new("Exit: King's Isle (West)", 15, 17),
                new("Exit: Lansk Undercity (Ferry)", 7, 15, "Take the ferry. Requires a ticket."),
                new("Exit: Pilgrim Dock (Ferry)", 2, 7, "Take the ferry. Requires Pilgrim Garb."),
                new("The Lansk Travel Bureau", 5, 1, "Sells you a ticket for the Lansk ferry for $500."),
                new("The Pilgrim Ferry", 3, 6, "Requires Pilgrim Garb to board."),
                new("Statue of Our Lady of Home Computers", 1, 1, "Move with STR 24 for Pilgrim Garb, Ice Wand, and an Apple II or IBM PS/2."),
            }),

        new(0x1D, "Siege Camp", 32, 32,
            "The primary base of the forces besieging Byzanople. Once the War is decided, the camp empties out.",
            new MapLocation[]
            {
                new("Exit: King's Isle (South)", 7, 25, "Enlist or fight guards to leave this way unless war is over."),
                new("Exit: Byzanople", 9, 14, "Exit to Byzanople front after receiving blessing."),
                new("Exit: Byzanople (Backdoor)", 2, 4, "Passage between Byzanople and the final fight."),
                new("The Front Door", 7, 1, "Guards enlist you or kick you out; guards at (07,02) stop you leaving."),
                new("Buck Ironhead's Office", 5, 6, "Offers a pardon if you beat Byzanople, or sends you to the front at (09,13)."),
                new("The Silver Arrows", 8, 2, "Locked chest (difficulty 2) with Silver Arrows."),
                new("The Camp Healer", 10, 4, "Heals the party for free."),
                new("The Black Market", 11, 13, "Sells shields and the Bladed Flail."),
                new("The Weapons Stash", 10, 14, "Contains the Lance Sword and the Silver Gloves."),
                new("The Final Fight", 3, 9, "The battle that decides the siege. Lose and Buck jails you in Kingshome Dungeon."),
            }),

        new(0x1E, "Game Preserve", 32, 32,
            "Plenty of random encounters and even more traps. Worth running a trap-detection spell here.",
            new MapLocation[]
            {
                new("Exit: Rustic (Any)", 25, 27, "Brings you to the expected place on Rustic."),
                new("The Stag", 8, 12, "Use Hiding or Tracker here and a stag will appear."),
                new("Jack's House", 3, 3, "If you haven't talked to the warden, he's here."),
                new("The Bandit Trap", 12, 2, "A tripwire here triggers a fight with bandits unless spotted."),
                new("The Bandit Campsite", 11, 1, "Bandits guard a chest containing scrolls and magic arrows."),
            }),

        new(0x1F, "Magic College", 32, 32,
            "Utnapishtim the Faraway's college of magical trickery, with several illusion-themed trials.",
            new MapLocation[]
            {
                new("Exit: The Eastern Isles", 37, 24, "Leaves to the Eastern Isles (as if you exited East)."),
                new("The Front Door", 1, 0, "Face North and use Lanac'toor's Spectacles to reveal the door."),
                new("Room 1", 1, 1, "Freeze the wall of fire using H:Ice Chill or H:Big Chill."),
                new("Room 2", 2, 4, "Melt the wall of ice with any fire spell."),
                new("Room 3", 3, 5, "Sneak past the gargoyle using H:Cloak Arcane to avoid being ejected."),
                new("Room 4", 3, 2, "Defeat a Philistine in an anti-magic zone."),
                new("Room 5", 5, 1, "Disarm the granite block tripwire with D:Soften Stone or S:Disarm Trap."),
                new("Room 6", 6, 3, "Utnapishtim appears as an illusion; ignore him and walk through."),
                new("Victory", 6, 5, "Utnapishtim asks you to pick a prize (Soul Bowl is recommended)."),
            }),

        new(0x20, "Dragon Valley", 32, 32,
            "Home of the fearsome Dragon Queen, your ticket to defeating Namtar's army.",
            new MapLocation[]
            {
                new("Exit: Eastern Isles (South)", 34, 14, "Exit to the Eastern Isles."),
                new("The Dead Dragon", 8, 3, "Skeleton has an infinite supply of Dragon Teeth."),
                new("The Armor Cache", 3, 3, "Open chest with high-value gear and Dragon's Eyes."),
                new("The Magic Cache", 14, 14, "Difficulty 5 chest containing magic scrolls."),
                new("The Dragon Queen", 6, 12, "Use the Dragon Gem to gain her alliance against Namtar."),
            }),

        new(0x21, "Phoebus Dungeon", 32, 32,
            "After your run-in with Mystalvision you're dropped in a cell — but you'll get revenge soon.",
            new MapLocation[]
            {
                new("Exit: Phoebus (Stairs)", 1, 13, "Stairs back up to Phoebus, behind the Cave-In."),
                new("Exit: Dilmun (Failsafe)", 5, 10, "If the dragon is released, you travel to Dilmun."),
                new("Your Jail Cell", 12, 3, "Where you wake up in captivity."),
                new("The Jail", 13, 7, "A cell with a crying man who says they are torturing the Druid."),
                new("The Cave-In", 5, 1, "The way out. Use Climb or a Shovel to get past."),
                new("The \"Armory\"", 2, 11, "Contains basic equipment and a Shovel."),
                new("The Magic Mouth", 4, 12, "Requires password HALIFAX to access the vault."),
                new("The Treasure Vault", 3, 14, "Contains the Blow Horn, Magic Ring, and Magic Quiver."),
                new("Dinnertime for Dragons", 13, 13, "A hunchback feeds the dragon. Letting it loose destroys Phoebus."),
                new("The Torture Chamber", 6, 11, "Rescue the druid to get the password HALIFAX."),
                new("Mystalvision, Part Two", 7, 15, "Fight Mystalvision and find multiple powerful scrolls."),
            }),

        new(0x22, "Lanac'toor's Lab", 32, 32,
            "A flooded, wrapping, dark lab (needs a compass). Softening the wrong wall floods the party.",
            new MapLocation[]
            {
                new("Exit: Mud Toad (Stairs)", 7, 9, "Stairs up to Mud Toad."),
                new("Exit: Underworld (Portal)", 2, 12, "Portal/stairs down to the Underworld."),
                new("Lanac'toor's Journal", 8, 9, "Read paragraph #107 for several clues."),
                new("The Underworld Portal", 2, 12, "Cast D:Create Wall at it to seal it and eliminate all encounters."),
                new("Inner Sanctum: Wand & Shield", 14, 0, "Healing Potion, Battle Wand, and Dragon Shield (+5 AC)."),
                new("Inner Sanctum: Spectacles", 0, 15, "Lanac'toor's Spectacles — get you into the Magic College."),
                new("Inner Sanctum: Scrolls", 1, 15, "L:Mage Fire, S:Fire Storm, S:Sun Stroke, H:Dazzle, and more."),
            }),

        new(0x23, "Byzanople Dungeon", 32, 32,
            "Aligned oddly to line up with the Byzanople map. Reach Prince Jordan or the crypt below.",
            new MapLocation[]
            {
                new("Exit: Byzanople (Stairs)", 6, 9, "Stairs taking you up to Byzanople."),
                new("The Tunnels", 6, 1, "Walk to the North end and use STR 18+ to teleport across."),
                new("The Guardroom", 9, 7, "Full of City Militia and Royal Guards unless allied with Jordan."),
                new("The Jail", 7, 7, "If thrown here, you are stuck forever; the door cannot be opened."),
                new("Princess Myrolla", 6, 8, "Stops you climbing the stairs. Sends you to jail unless killed."),
                new("Prince Jordan", 7, 11, "Join him to reveal the West door, or kill him to end the war."),
                new("The Treasure Vault", 9, 11, "Contains Magic Chain, Magic Shield, scrolls, and Dragon Stones."),
                new("The Crypt", 14, 1, "Down a hallway past locked doors. Zombies guard the Magic Axe."),
            }),

        new(0x24, "Kingshome Dungeon", 32, 32,
            "Plenty of random encounters and tough fixed ones. You need a light and a compass.",
            new MapLocation[]
            {
                new("Exit: Namtar's Bedroom (Stairs)", 6, 8, "Stairs up to Namtar's Bedroom."),
                new("Your Jail Cell", 0, 15, "Where you are dropped by the ambush on King's Isle."),
                new("The Court Jester", 4, 12, "Re-locks the door and has you back out quickly."),
                new("The Vicious Guards", 2, 7, "A tough fight. Losing throws you back in your cell."),
                new("The Kingshome Armory", 7, 15, "Jackpot armory with the Gatlin Bow, Mage Ring, and Dragon Stones."),
                new("Drake's Old Throne", 11, 10, "Secret door East to King Drake's throne, Crown, and cash."),
                new("The Crossbow Trap", 14, 16, "Deals 1d6 damage unless a trap-detection spell is running."),
            }),

        new(0x25, "Slave Estate", 32, 32,
            "Slaveholder Mog's estate — mostly a gateway between the Slave Mines below and Forlorn.",
            new MapLocation[]
            {
                new("Exit: Slave Mines (Stairs)", 5, 12, "The shack where you're deposited coming up from the mines."),
                new("The Statue of Mog", 7, 5, "An unflattering statue of Mog or a random aristocrat."),
                new("Tracks", 7, 11, "Follow with Tracker to be deposited right outside the Demon's lair."),
                new("The Unguarded Guardroom", 10, 12, "Six Dragon Stones, a Ruby Dagger, and basic weapons."),
                new("The Creaky Floorboard", 5, 8, "Break through with STR to find a chest with the Magic Lamp."),
                new("The Secret Room", 3, 8, "Chest with a Handaxe, gold, and a pile of broken mirrors."),
                new("Mirror Pile", 4, 3, "Pick up a mirror fragment to defeat the Gaze Demon without combat."),
                new("The Gaze Demon", 3, 4, "Use a mirror fragment to petrify the Demon or face him in combat."),
            }),

        new(0x26, "Lansk Undercity", 32, 32,
            "The dirtier, nastier, deadlier version of Lansk, filled with black market shops.",
            new MapLocation[]
            {
                new("Exit: Underworld (Stairs)", 16, 14, "Stairs down to the Underworld."),
                new("Exit: Lansk (Stairs)", 5, 8, "Stairs up to Lansk (one-way unless an official is bribed)."),
                new("Exit: Old Dock (Ferry)", 8, 15, "Take the ferry to the Old Dock using a Kings Ticket."),
                new("Dr. Death's Killing and Maiming Emporium", 13, 11, "Weapons shop."),
                new("Exeter's Fine Shields and Armors", 13, 9, "Armor shop."),
                new("Town Healer", 13, 7, "Heals wounds for $4 per point of Health."),
                new("The Illegal Magic Shoppe", 2, 8, "Sells Druid Magic scrolls and Dragon Stones. Access at (01,06)."),
                new("The Ministry of EZ Paperwork", 2, 10, "Sells Citizenship Papers, Governor's Passes, and Ferry Tickets."),
                new("The Sick Dragon", 7, 9, "Heal the dragon or use an Ankh to receive the Dragon Gem."),
                new("The Statue of Irkalla", 1, 15, "Move with STR for a chest with the Glow Sword and Dragon Stones."),
                new("The Statue of Nergal", 14, 15, "Mentions Nergal's summer palace, the Necropolis."),
                new("The Statue of the Universal God", 1, 2, "Tells you about Roba and Freeport."),
                new("The Statue of Enkidu", 14, 2, "Tells you about Enkidu, whom you wrestled."),
            }),

        new(0x27, "Tars Dungeon", 32, 32,
            "A tiny but confusing board with a spinner and a wrapping map. Get in and get out.",
            new MapLocation[]
            {
                new("Exit: Tars Ruins (Stairs)", 2, 5, "Stairs up to the ruins of Tars."),
                new("Exit: Underworld (Pit)", 0, 5, "A pit behind a secret door you can Climb down to the Underworld."),
                new("The Spinner", 1, 4, "Changes your orientation but not the viewport. Can throw you through walls."),
                new("The Stone Arms", 7, 4, "Chest behind a secret door containing the Stone Arms."),
                new("The Closet", 4, 7, "Chest behind a secret door with a Healing Potion and Dragon Stones."),
                new("The Other Closet", 4, 0, "Locked chest (difficulty 1) containing multiple scrolls."),
                new("The Visual Illusion", 5, 2, "You spot yourself moving at the other end of an infinite corridor."),
                new("The Adventuring Party", 3, 1, "Nasty fight against adventurers and a wizard."),
                new("The Inexplicable Easter Egg", 4, 2, "Use Arcane Lore here to trigger a chat with Captain Kirk and Spock."),
            }),
    };
}
