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

        new("Wealthy District & Temple of Bane", 16, 16,
            "Grinding ground (orcs/hobgoblins/ogres). Enter the Temple of Bane with the leather holy " +
            "symbols from Podol Plaza for Dust of Disappearance (save it for the final fight) + a Ring of " +
            "Feather Falling. No keyed locations transcribed — the walls below are the game's own.",
            Array.Empty<MapLocation>(),
            MapAscii.Parse(MapTerrainData.WealthyAreaTerrain)),

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
