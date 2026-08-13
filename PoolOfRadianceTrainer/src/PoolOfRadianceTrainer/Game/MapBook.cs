namespace PoolOfRadianceTrainer.Game;

/// <summary>A keyed spot on an area map: its (x, y) grid square and what's there.</summary>
public sealed record MapLocation(string Name, int X, int Y, string Notes = "")
{
    public string Coord => $"({X}, {Y})";
}

/// <summary>
/// One explorable area of Phlan, with its grid size, notable keyed locations, and optional
/// per-cell terrain (walls/floors) decoded from the strategy-guide ASCII maps.
/// <paramref name="IsWilderness"/> marks the one overland area: it is far larger than the 16×16
/// districts, has terrain instead of walls, and the game holds the party's position there in a
/// different shape — see <see cref="Memory.PositionLocator"/>.
/// </summary>
public sealed record MapArea(string Name, int Width, int Height, string Notes,
    IReadOnlyList<MapLocation> Locations, BoardSquare[,]? Terrain = null, bool IsWilderness = false)
{
    public string Size => $"{Width}×{Height}";
    public string Header => $"{Name}   ({Size} grid)";

    public int GridWidth  => Locations.Count == 0 ? Width  : Math.Max(Width,  Locations.Max(l => l.X) + 1);
    public int GridHeight => Locations.Count == 0 ? Height : Math.Max(Height, Locations.Max(l => l.Y) + 1);
}

/// <summary>
/// Area/location reference for Pool of Radiance. Notes and keyed locations are drawn from the
/// bundled strategy guide (<c>docs/strategy-guide.md</c>); the wall/door/floor grids come from the
/// game's own level geometry — see <see cref="MapTerrainData"/>. Coordinates: (x, y) = (column, row),
/// origin (0,0) top-left / north-west, x east, y south — the same system the memory dump records
/// ("Slums 0,4"). Locations marked [approx] have not been confirmed live.
/// </summary>
public static class MapBook
{
    public static readonly IReadOnlyList<MapArea> Areas = new MapArea[]
    {
        new("New Phlan", 16, 15,
            "The civilized hub — City Council Clerk issues commissions and pays rewards; four Training " +
            "Halls (1,000 gp per level); Temples of Tyr, Sune and Tempus (heal/raise/cure); weapon, " +
            "armor, magic-item and scroll shops (daytime only); the Inn; and the Docks (boat to Sokal Keep). " +
            "Return here between commissions to train, heal and resupply. The harbour east of the sea " +
            "wall is water — the squares shaded blue are off the map.",
            // Numbered against the clue book's own key for this map, and placed on the squares its
            // map prints those numbers on — so they line up with the walls, which come from the
            // game's level data. Several of these (inns, taverns, shops) appear more than once on
            // the map; one representative square is listed.
            new MapLocation[]
            {
                new("Exit ↔ Slums (E1)",                  0,  4, "West exit; connects to the Slums at (15, 4). Confirmed live."),
                new("Boat to Sokal Keep (1)",            15,  1, "Also the boat out to the wilderness once the Keep is cleared."),
                new("Passenger Dock (2)",                11,  1, "Tell the boatman a destination and pay for passage."),
                new("Temple of Tyr (3)",                 10,  4, "Healing services."),
                new("Bishop Braccio's Office (4)",       10,  5, "Leader of the temple of Tyr."),
                new("Dueling & Hiring Hall (5)",          8,  1, "Duel for XP; hire NPCs (they take a cut of the treasure)."),
                new("Cleric Training Hall (6)",           5,  0, "1,000 gp per Cleric/Druid level."),
                new("Magic-User Training Hall (7)",       7,  0, "1,000 gp per Mage/Illusionist level."),
                new("Fighter Training Hall (8)",          8,  0, "1,000 gp per Fighter/Paladin/Ranger level."),
                new("Thief Training Hall (9)",            9,  0, "1,000 gp per Thief/Assassin level."),
                new("Temple of Sune (10)",                1,  1, "Healing services."),
                new("City Park (11)",                     1,  7, ""),
                new("City Hall entrance (12)",            4,  3, "Proclamations are posted on the wall here."),
                new("City Council Clerk (13)",            5,  5, "Collect commissions; pay rewards."),
                new("Junior Councilman's Office (14)",    6,  5, ""),
                new("Senior Councilman's Office (15)",    6,  6, ""),
                new("Head Councilman's Office (16)",      6,  8, "Cadorna's office."),
                new("City Council Chambers (17)",         6, 10, ""),
                new("Temple of Tempus (18)",              1, 13, "Healing services; Tempus is lawful good, so his temple repels vampires."),
                new("Inn (19)",                           1, 14, "1 pp for the party; rest and memorize spells. Also at (4,13) and (6,13)."),
                new("Tavern (20)",                        8,  9, "Gamble and brawl (worth XP). Several around the district."),
                new("Arms & Armor Shop (21)",             8, 11, "Weapons and armor; all branches charge the same."),
                new("General Items Shop (22)",            9, 10, "Mirrors, flasks of oil, holy symbols, vials of holy water."),
                new("Silver Shop (23)",                  10, 13, "Silver weapons, silver armor, silver jewelry."),
                new("Jeweler (24)",                       8, 10, "Converts coin to jewelry (75–50,000 gp) so the party can carry its money."),
            },
            MapAscii.Parse(MapTerrainData.NewPhlanTerrain, 16, 15)),

        new("Slums", 16, 16,
            "First commission — start here. Goblins/kobolds/orcs; a hobgoblin mage drops the Wand of " +
            "Magic Missiles; the infamous ogres + trolls fight is in the SW corner.",
            new MapLocation[]
            {
                new("Exit ↔ New Phlan (E1)",              15,  4, "East exit back to the civilized quarter."),
                new("Exit ↔ Kuto's Well (E2)",             0, 11, "West exit (also at row 4)."),
                new("Rope Guild stairs (E3)",               6, 10, "Down to the Rope Guild (automap off inside)."),
                new("Illusory-wall treasure (12)",          0,  0, "Enter from the east through the illusory wall."),
                new("Kobolds → Bracers AC 6 (6)",           7,  0, ""),
                new("Orcs w/ scroll (1)",                  13,  1, ""),
                new("Goblins → Leather Armor +1 (2)",      10,  1, ""),
                new("Hobgoblins → Ring of Protection +1 (10)", 0, 2, ""),
                new("Orc leaders → Chain Mail +1, Flail +1 (9)", 3, 3, ""),
                new("Monster leaders (14)",                 1,  5, ""),
                new("Mage Ohlo (3)",                       13, 10, "Rope Guild errand — say OHLO to the guild merchant."),
                new("Rope-Guild merchant (19)",            15, 12, "Deliver Ohlo's package for a monster-blasting necklace."),
                new("4 Trolls + 2 Ogres (20)",             0, 14, "SW corner — Sleep ogres, fire/oil on trolls, stand on corpses."),
            },
            MapAscii.Parse(MapTerrainData.SlumsTerrain)),

        new("Sokal Keep", 16, 16,
            "Reach it by boat; clearing it opens the wilderness. Passwords LUX / SHESTNI / SAMOSUD. " +
            "Never melee Ferran or the spectres (2-level drain).",
            new MapLocation[]
            {
                new("Boat ↔ New Phlan (E1)",          11, 15, "South side of the keep."),
                new("Dead elf / passwords (1)",         6, 13, "SEARCH for the rune scroll."),
                new("Elven ghosts / barracks",          6,  2, "Parley with LUX before Ferran — diary + 5 gems."),
                new("Ferran Martinez altar (12)",       7,  9, "Give LUX, then tell the truth to finish the mission."),
                new("Armory illusory-wall cache (17)", 12,  0, "Long Sword +1, Chain Mail +1, Mace +2, Shield +1."),
                new("Huge scorpions (8)",               2, 11, "Poison — skip or Sleep."),
            },
            MapAscii.Parse(MapTerrainData.SokalKeepTerrain)),

        new("Kuto's Well", 16, 16,
            "Free the Wide-Eyed Woman (banded mail +1, quarter staff +1, bracers AC 4), then descend " +
            "the well to fight Norris the Gray in the catacombs.",
            new MapLocation[]
            {
                new("Well down to catacombs (E4)", 7, 7, "Descend to Norris the Gray (drops Long Sword +1 + a Boss note)."),
            },
            MapAscii.Parse(MapTerrainData.KutosWellTerrain)),

        new("Kuto's Well — Catacombs", 16, 16,
            "Down the well. Norris the Gray (half-orc Fighter 5) holds the bandit hideout with 5 lizardmen " +
            "and 9 kobold leaders; he drops a Long Sword +1 and a note from the Boss, and his hoard sits " +
            "north-east of the catacombs. You cannot rest down here until Norris is dead. The rooms are cut " +
            "out of rock — the wide stone blocks below are solid, not unexplored.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.KutosWellCatacombsTerrain)),

        new("Podol Plaza", 16, 16,
            "Auction spy commission — on entry choose 'disguise yourself as monsters'. Garwin escapes " +
            "no matter your bid; witnessing it completes the commission.",
            new MapLocation[]
            {
                new("The Pit (2)",              4,  8, "Duel the drunk buccaneer — Long Sword +1 + Chain Mail +1."),
                new("Orc priest of Bane (6)",  14,  8, "Drops 6 leather holy symbols — needed to enter the Temple of Bane."),
                new("Temple of Ilmater",        1, 15, "SW — Knock the doors; a safe rest/heal."),
            },
            MapAscii.Parse(MapTerrainData.PodolPlazaTerrain)),

        new("Mendor's Library", 16, 16,
            "Cast Knock to enter. Search Philosophy (2) and History (4) for Tyranthraxus's origin; the " +
            "Rhetoric section (5) hides a Basilisk — equip mirrors! Leaving with a book triggers a spectre.",
            new MapLocation[]
            {
                new("Library door (E3)",                       12,  1, "Cast Knock — bashing is unreliable."),
                new("Surrendering kobolds → map (18)",         12, 10, ""),
                new("Potions of Extra Healing (8)",             8, 11, "3 potions, under a floor jar."),
                new("Mad Man (11)",                            11, 12, "Raves of 'the castle of flowers on the hill' — points to Valjevo Castle."),
                new("Manual of Bodily Health (13)",            13, 13, "Permanent +CON; sells for ~25,000 gp."),
            },
            MapAscii.Parse(MapTerrainData.MendorTerrain)),

        new("Kovel Mansion", 16, 16,
            "Thieves' guild — traps everywhere. Move in Search Mode, cast Find Traps, bring a thief + " +
            "Knock. Reveals Cadorna's treachery and that the Boss is a dragon in Valjevo Castle.",
            new MapLocation[]
            {
                new("Entrance (double door)",    9, 14, "From the north — the three west doors are fake."),
                new("Weapons cache (deadliest trap)", 3, 11, "Short Sword +1, Hammer +2, etc."),
                new("Four caskets (42 gems)",    6,  8, ""),
            },
            MapAscii.Parse(MapTerrainData.KovelMansionTerrain)),

        new("Cadorna Textile House", 16, 16,
            "Councilman Cadorna's commission — recover the iron treasure box (holds the Gauntlets of " +
            "Ogre Power, STR 18/00). Opening it breaks the seal and earns his enmity; a thief can re-forge it. " +
            "No keyed locations transcribed — the walls below are the game's own.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.CadornaTerrain)),

        new("Wealthy District", 16, 16,
            "Grinding ground (orcs/hobgoblins/ogres). Mace, the half-orc cleric who runs the Temple of " +
            "Bane next door, lives in a mansion here. Clearing this block and the temple are one " +
            "commission. No keyed locations transcribed — the walls below are the game's own.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.WealthyAreaTerrain)),

        new("Temple of Bane", 16, 16,
            "The great temple of Ilmater, converted to the worship of Bane. Enter with the leather holy " +
            "symbols from Podol Plaza. Bishop Braccio lends you Dirtan, a 6th-level gnome cleric, if you " +
            "agree to recover Ilmater's lost artifacts. Mace intends to let you find the hidden treasure " +
            "and then kill you for it — the payoff is Dust of Disappearance (save it for the final fight) " +
            "and a Ring of Feather Falling. The colonnade down the middle of the nave is the temple proper.",
            new MapLocation[]
            {
                new("Mace's cultists (8)",  2,  7, "The half-orc cleric's motley group of cultists and slaves."),
                new("Temple treasure (9)",  5,  5, "Ilmater's hidden artifacts; also marked at (13,9) and (9,12)."),
                new("Altar chamber (10)",  15,  7, "East end of the nave."),
            },
            MapAscii.Parse(MapTerrainData.TempleOfBaneTerrain)),

        new("Valhingen Graveyard", 16, 16,
            "A cauldron of undead, and the hardest of the city commissions — take it late. Everything here " +
            "drains levels; bring Restoration scrolls, and remember Tyr's temple can restore them. The " +
            "vampire must be killed TWICE (once in the crypt, then again over his coffin) before the " +
            "graveyard clears. Clear the spectres at 13 and 16 before the vampire's room will open.",
            new MapLocation[]
            {
                new("Skeletal hands (1)",       6,  4, "Hands erupt from a grave and attack."),
                new("Skeleton mausoleum (2)",   5,  6, ""),
                new("Giant skeleton (3)",       7,  6, "Search after the fight for its treasure."),
                new("Zombie tower (4)",         5,  9, ""),
                new("Spectre — zombies (5)",    4, 10, "A spectre creating zombies."),
                new("Skeleton tower (6)",       1, 12, ""),
                new("Poison-gas room (7)",      1, 13, "Buff saving throws before entering."),
                new("Spectre — skeletons (8)",  0, 15, ""),
                new("Zombies (9)",              9, 15, "Milling outside the mausoleum."),
                new("Ju-ju zombie (10)",        8, 15, "Search after the fight for its treasure."),
                new("Mummy crypt (11)",         9, 11, "The fear aura paralyzes — buff saves first."),
                new("Wight tower (12)",         9,  5, ""),
                new("Spectre — wights (13)",    9,  7, "Must be cleared to open the vampire's room."),
                new("Wight mausoleum (14)",    14,  6, ""),
                new("Wraith (15)",             14,  8, ""),
                new("Spectre crypt (16)",       9,  1, "Must be cleared to open the vampire's room."),
                new("Knight's grave (17)",      8,  2, "A gallant knight and his treasure are buried here."),
                new("Vampire's coffin (18)",   12,  4, "Kill him here the second time to finish him."),
                new("The vampire (19)",        14,  0, "Needs 13 and 16 cleared first. 18,800 XP."),
                new("Evil magic-user (20)",     8,  7, "Offers to help — he turns on you for the vampire."),
            },
            MapAscii.Parse(MapTerrainData.ValhingenGraveyardTerrain)),

        // ---- Wilderness locations. Each is its own 16×16 level entered from the overland map; the
        // square that opens it is marked on the Wilderness map below.
        new("Nomad Camp", 16, 16,
            "Overland (12, 11) — always visible. Parley rather than attack: stay for the feast, hear the " +
            "chief out, then fight the kobold army with the nomads for 5,000 gp, a Two-Handed Sword +2 and " +
            "a Wand of Magic Missiles. The kobolds come in 3 waves; at the third, go with the chief to " +
            "finish them for the bigger reward. Do not backstab the nomads mid-fight — both sides turn on " +
            "you. The shaded ring is the tripwire line and the east edge is the stream.",
            new MapLocation[]
            {
                new("Tripwire ring (1)",  3,  8, "Crossing it sets off alarms and the nomads turn out in force. Also at (10,1) and (9,14)."),
                new("Your hut (2)",       9, 12, "Where they put you up; rest here until the kobolds come."),
                new("The forest (3)",     0,  9, "Outside the tripwire — caught out here you fight the kobolds alone."),
            },
            MapAscii.Parse(MapTerrainData.NomadCampTerrain)),

        new("Kobold Caves", 16, 16,
            "Overland (6, 15) east — kobold patrols thicken as you approach. Two entrances at the south " +
            "edge: the small cave is the kobold cavern, the large cave is a wyvern lair that connects to " +
            "it. Low ceilings cut movement, AC and damage in every fight down here, and the kobolds have " +
            "trapped the place — move in Search Mode and let a thief disarm what you find.",
            new MapLocation[]
            {
                new("Small cave entrance",   6, 15, "Straight into the kobold cavern."),
                new("Large cave entrance",  10, 15, "The wyvern lair; you can rest before the wyvern fight."),
                new("Water trap (1)",        6, 13, "A character falls in; money and items can be lost."),
                new("Discarded map (2)",     6, 11, "Shows how the kobold and wyvern caves connect."),
                new("Kobold guide (3)",      8, 12, "Follow him to the wyvern cave; refuse and a deadfall hits you."),
                new("Net trap (4)",         10,  9, "Kobolds leap on the entangled party."),
                new("Spike trap (5)",       11,  9, ""),
                new("Drunken kobold (6)",   12,  9, "Coming from the wyvern side, he takes you to the king."),
                new("Wyvern roam (7)",      14,  8, ""),
                new("Wyvern nest (8)",      14,  6, "With its treasure."),
                new("Crippled kobold (9)",  15,  3, "Search, then give him water for his story."),
                new("Princess Fatima (10)",  2,  2, "Freed, she fights the kobolds fanatically and can join."),
                new("Throne room (11)",      6,  3, "Three waves plus ballista fire — heal with 'Continue Combat', you get no rest."),
                new("King's guard (12)",     8,  1, ""),
                new("Fate of the king (13)",10,  1, "Confirming his death breaks the kobolds."),
                new("Efreeti bottle (14)",  11,  0, "Search for it. Tell the truth and he helps you later — keep the bottle."),
                new("Treasure trove (15)",  12,  1, "The huge kobold hoard."),
                new("A clue (16)",           4,  3, ""),
            },
            MapAscii.Parse(MapTerrainData.KoboldCavesTerrain)),

        // Yarash's pyramid on Sorcerer's Isle. Level 1 is two separate 16×16 levels — the clue book
        // prints them as the "west half" and "east half" of one drawing, joined by the secret entrance.
        new("Yarash's Pyramid — Level 1 west", 16, 16,
            "Rowboat from the overland shore at (6, 16). The mad sorcerer Yarash is poisoning the Stojanow " +
            "River, turning lizardmen into freshwater sahuagin. Three sets of teleporters (A, B and C) run " +
            "the pyramid; throwing a rock through a portal toggles where it goes. You can rest on either " +
            "half of level 1 once that half's random encounters are done. The sealed cells the schematic " +
            "shows are teleport destinations — you arrive in them, you cannot walk in.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.PyramidWestTerrain)),

        new("Yarash's Pyramid — Level 1 east", 16, 16,
            "The east half of the pyramid's base, reached through the secret entrance between the halves " +
            "or by teleporter. Same rules as the west half.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.PyramidEastTerrain)),

        new("Yarash's Pyramid — Level 2", 16, 16,
            "The middle level; safe to rest anywhere. Free the enslaved lizardmen — be Nice — for the " +
            "friend-word SAVIOR, which buys you the alliance at Lizard Man Keep without a bloodbath.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.PyramidLevel2Terrain)),

        new("Yarash's Pyramid — Level 3", 16, 16,
            "The top level, where Yarash himself is. Password NOKNOK. The colour dial at (5, 0) aims the " +
            "treasure teleporter: Blue = the way out, Copper / Silver / Gold = three treasure rooms with " +
            "3 random magic items each. Kill Yarash and the river starts to clear.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.PyramidLevel3Terrain)),

        new("Lizard Man Keep", 16, 16,
            "Overland (11, 8) — obvious among the trees. An old wizard's field blocks spellcasting over " +
            "the whole keep, so this is a melee fight. The old chief is being usurped by a young warrior: " +
            "give the old lizardman SAVIOR (from Yarash's level 2) and champion him in single combat " +
            "against Drythh to win the alliance without fighting the tribe. Rubbled walls and swamp " +
            "squares are drawn as ordinary floor below — only the game's walls are shown.",
            new MapLocation[]
            {
                new("Hole to catacombs (1)", 10,  4, ""),
                new("Hole to catacombs (2)",  2,  7, ""),
                new("Hole to catacombs (3)",  6, 12, ""),
                new("Hole to catacombs (4)",  4,  6, ""),
                new("Hole to catacombs (5)",  5,  9, ""),
                new("Hole to catacombs (6)",  9,  6, ""),
                new("Stairs down (7)",        9,  8, "The proper way into the catacombs."),
                new("Ambush (8)",             3,  3, "Lizard men and giant lizards waiting."),
                new("Giant lizards (9)",      5,  6, "They inhabit this building."),
            },
            MapAscii.Parse(MapTerrainData.LizardManKeepTerrain)),

        new("Lizard Man Catacombs", 16, 16,
            "Under the keep, through any of the six holes or the stairs at (9, 8). Lizard men ambush from " +
            "the pools until they are all dead; once they are, swim the pools for the treasure the castle's " +
            "original owners left — 3× Shield +2 among it. The first time you come down, every remaining " +
            "giant lizard attacks at once.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.LizardManCatacombsTerrain)),

        new("Buccaneer's Base", 16, 16,
            "Overland (12, 31) — only appears once you take the commission to rescue the Bivant heir. " +
            "The fast way out: scout first, then open the animal pen to start a stampede, free the boy " +
            "while the guards chase animals, and run for the front gate. The longer you stay in the " +
            "compound the more buccaneer groups you fight on the way out.",
            new MapLocation[]
            {
                new("Front gate (1)",         7,  0, "The only exit; guarded, but the guards can be distracted by the stampede."),
                new("Merchant's camp (2)",    8, 10, "Safe to camp here as long as you like."),
                new("Captain's guards (3)",   7, 11, ""),
                new("Captain's quarters (4)", 8, 14, ""),
                new("Barracks (5)",           6,  3, "Forcing your way in starts a fight. Also at (10,3), (2,7), (10,7), (5,10) and (12,10)."),
                new("Guard tower (6)",        6,  0, "Forcing your way in starts a fight. Towers ring the compound."),
                new("Animal pen (7)",        13,  7, "Release the animals to stampede — the diversion that makes the rescue easy."),
                new("Slave pen (8)",          7,  8, "The boy is kept here, under guard."),
                new("Slave-pen guards (9)",   7,  6, "They leave if the animals stampede."),
                new("Huckster (10)",          2, 11, "Sells a pass to see the buccaneer captain."),
            },
            MapAscii.Parse(MapTerrainData.BuccaneerBaseTerrain)),

        new("Outpost of Zhentil Keep", 16, 16,
            "Overland (3, 32) — only enterable once you carry Cadorna's diplomatic pouch. The pouch asks " +
            "the Keepers to return you to Phlan with your heads on a pike; they try that night. Set a " +
            "watch, survive the ambush, and kill the Commandant for a Javelin of Lightning (one of the few " +
            "things that hurts the final dragon), Plate Mail +2 and a Ring of Fire Resistance.",
            new MapLocation[]
            {
                new("Front gate (1)",           7,  0, "Guarded. Also at (8,0)."),
                new("Guard tower (2)",          1,  1, "Towers at the four corners and mid-walls: (14,1), (6,6), (10,6), (1,15), (14,15)."),
                new("Commandant's quarters (3)",7,  9, "Where you first meet him — and where you kill him."),
                new("Party's quarters (4)",     6,  1, "Where the guards put you before and after dinner. Also at (10,1)."),
                new("Barracks (5)",             5,  3, "Forcing your way in starts a fight. Six more around the walls."),
                new("Stables (6)",             12,  8, "They smell bad."),
            },
            MapAscii.Parse(MapTerrainData.ZhentilKeepOutpostTerrain)),

        // ---- The endgame.
        new("Stojanow Gate", 16, 16,
            "The fortified gate on the road to Valjevo Castle. Buy the merchant's wagon (250 gp, daylight " +
            "only) as a disguise: the bugbear patrol takes 15 gp and waves you through, letting you hit " +
            "each guard tower separately by surprise. Undisguised you bash both gates under volleys of " +
            "boulders and fight everything at once. Knock opens the gates; up to 3× Ring of Protection +2 " +
            "is the reward. (The DOS game has no fire trap or flooded passage — those are NES additions.)",
            new MapLocation[]
            {
                new("Merchant in wagon (1)",  4, 14, "Daylight only; 250 gp for the wagon. Also at (11,14)."),
                new("Bugbear patrol (2)",     8, 11, "Disguised and unseen through, you pass free."),
                new("Southern gate (3)",      8,  9, "Barred by massive beams — Knock or massive Strength."),
                new("Northern gate (4)",      8,  7, "The same again; guards throw boulders if you bash it."),
                new("Ettin ambush (5)",       8,  6, "Both towers' ettins meet you here if you opened both gates by force."),
                new("West tower (6)",         4,  8, "Level-6 mage + aides + 3 ettins. Surprise them if you sneaked past."),
                new("East tower (7)",        11,  8, "The same garrison again — if the alarm sounds you meet both."),
            },
            MapAscii.Parse(MapTerrainData.StojanowGateTerrain)),

        // The endgame castle is four separate 16×16 levels, not one big map: each quadrant is its
        // own GEO block, and they connect at the row-4/row-11 edge gaps that ring the hedge maze.
        new("Valjevo Castle — SW (entry)", 16, 16,
            "The endgame, entered here. Four quadrants (SW/NW/NE/SE) ring a poisonous hedge maze " +
            "around the Inner Tower and the Pool. Passwords HARASH / TYRANTHRAXUS / RHODIA. Do NOT " +
            "steal from the Altar of Bane.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.ValjevoSWTerrain)),

        new("Valjevo Castle — NW", 16, 16,
            "North-west quadrant; leads east to the NE quadrant and south back to the SW entry.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.ValjevoNWTerrain)),

        new("Valjevo Castle — NE", 16, 16,
            "North-east quadrant; leads west to the NW quadrant and south to the SE quadrant.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.ValjevoNETerrain)),

        new("Valjevo Castle — SE", 16, 16,
            "South-east quadrant; leads west to the SW entry and north to the NE quadrant.",
            new MapLocation[]
            {
                new("Flame Tongue Long Sword +2 (well)", 15, 10, "Down the well in this quadrant."),
            },
            MapAscii.Parse(MapTerrainData.ValjevoSETerrain)),

        // The two-storey tower in the middle of the hedge maze. Both levels are small — the tower
        // occupies one corner of its 16×16 block and the rest of the block is sealed off.
        new("Valjevo Castle — Inner Tower, lower level", 16, 16,
            "Reached through the hedge maze: the stairs are behind an illusory wall in the NW quadrant, or " +
            "in through the SE entrance. Medusa's chamber is down here — equip mirrors; she has only ~30 HP, " +
            "so hit her hard on the first round. The false 'Tyranthraxus' holds court in the throne room; " +
            "parley and you can walk away with Long Sword +5, Ring of Protection +3 and Gauntlets of Ogre " +
            "Power without a fight.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.InnerTowerLowerTerrain)),

        new("Valjevo Castle — Inner Tower, upper level", 16, 16,
            "The top of the tower, and the end of the game. Safe to rest anywhere up here until you meet " +
            "Tyranthraxus. Genheeris, a level-7 mage, offers to join — take him for the Wand of Lightning " +
            "Bolt. Then two back-to-back fights with no rest between: ~12 eighth-level fighters, then " +
            "Tyranthraxus in the bronze dragon. Spread out against the lightning breath, use Dust of " +
            "Disappearance, and answer 'attack' when he offers to let each character join him.",
            new MapLocation[]
            {
                new("Stairs to lower level (1)", 5,  4, "The tower occupies columns 1–8, rows 4–11 of this block."),
                new("Trap-door room (2)",        7,  5, "The trap door drops to Medusa's chamber — glancing down can petrify."),
                new("Waiting room (3)",          1,  7, "Messengers for Genheeris and Tyranthraxus. Parley → Nice sends one to Genheeris."),
                new("Genheeris' office (4)",     2,  4, "Promise to attack Tyranthraxus at once and he joins."),
                new("Tyranthraxus' lair (5)",    2,  9, "Buff everything before you step in. The Pool of Radiance is at (2, 10)."),
            },
            MapAscii.Parse(MapTerrainData.InnerTowerUpperTerrain)),

        // The overland map. Terrain here is transcribed from the clue book, not decoded from the
        // game's level data (the wilderness has no GEO block) — see WildernessMap for why.
        new("Wilderness — Moonsea overland", WildernessMap.Width, WildernessMap.Height,
            "Opened by clearing Sokal Keep; the boat runs here from the Phlan docks. Travel is square " +
            "by square with a 5% chance of an encounter per step, and the danger set depends only on " +
            "your X column (Western 2–15, Central 16–28, Eastern 29–41). Outdoor fights are large and " +
            "have no choke points — avoid them unless you want the challenge. Terrain below is " +
            "transcribed from the clue-book map, not decoded from the game, so treat it as a travel " +
            "aid; your live position and the teleport target are read from and written to the game " +
            "itself and are exact.",
            WildernessMap.Landmarks,
            WildernessMap.Terrain(),
            IsWilderness: true),
    };
}
