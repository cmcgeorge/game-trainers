namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>A keyed spot on an area map: its (x, y) grid square and what's there.</summary>
public sealed record MapLocation(string Name, int X, int Y, string Notes = "")
{
    public string Coord => $"({X}, {Y})";
}

/// <summary>
/// One explorable level of Curse of the Azure Bonds, with its grid size, notes, and the per-cell
/// terrain (walls/doors/floors) decoded from the game's own level geometry.
/// <paramref name="Geo"/> records the archive block the geometry came from, which is the level's
/// real identity — the display name is a label placed on it (see <see cref="MapTerrainData"/>).
/// </summary>
public sealed record MapArea(string Name, int Width, int Height, string Notes,
    IReadOnlyList<MapLocation> Locations, BoardSquare[,]? Terrain = null, string Geo = "",
    bool IsWilderness = false)
{
    public string Size => $"{Width}×{Height}";
    public string Header => Geo.Length == 0 ? $"{Name}   ({Size} grid)" : $"{Name}   ({Size} grid · {Geo})";

    public int GridWidth  => Locations.Count == 0 ? Width  : Math.Max(Width,  Locations.Max(l => l.X) + 1);
    public int GridHeight => Locations.Count == 0 ? Height : Math.Max(Height, Locations.Max(l => l.Y) + 1);
}

/// <summary>
/// Area reference for Curse of the Azure Bonds. Every level here is 16×16 and its walls, doors and
/// unreachable squares are decoded from the game's <c>GEO*.DAX</c> geometry — not transcribed — so
/// the schematic matches what you walk into. Coordinates: (x, y) = (column, row), origin (0,0)
/// top-left / north-west, x east, y south.
///
/// <para>The <b>module</b> each level belongs to is established from that module's monster roster
/// (see <see cref="MapTerrainData"/> and <c>docs/reverse-engineering.md</c> §7a). The level names
/// within a module are descriptive labels rather than decoded facts: Curse's Adventurer's Journal,
/// which is where the printed maps live, is not part of this install, so there is nothing to match
/// them against the way the sister trainer matched Phlan's districts against its clue book. Each
/// entry therefore carries the archive block it came from, and the Maps tab can tell you which level
/// you are actually standing on by matching the geometry the game has resident — so a label being
/// wrong costs you a name, never a location or a teleport.</para>
/// </summary>
public static class MapBook
{
    private static readonly MapLocation[] None = Array.Empty<MapLocation>();

    public static readonly IReadOnlyList<MapArea> Areas = new MapArea[]
    {
        new("Tilverton — Streets", 16, 16,
            "Module 2. The frontier city the adventure opens in, on the Cormyr/Dalelands border: the " +
            "party wakes here bearing the five azure bonds. Its roster is townsfolk and trouble — bar " +
            "patrons, royal guards, thieves, mages and the Fire Knives sent to finish the ambush. " +
            "Talk to the high priest, the sage and the bartender before leaving.",
            None, MapAscii.Parse(MapTerrainData.TilvertonStreetsTerrain), "GEO2:1"),

        new("Tilverton — The Pit", 16, 16,
            "Module 2. The arena level: fighting dogs and monkeys are on this module's roster and " +
            "nowhere else in the game, and the rule book's gambling scene ('only need one platinum " +
            "piece to play') belongs to it.",
            None, MapAscii.Parse(MapTerrainData.TilvertonPitTerrain), "GEO2:3"),

        new("Tilverton — Sewers", 16, 16,
            "Module 2. Crocodiles, trolls, otyughs and a neo-otyugh — the classic Gold Box sewer " +
            "bestiary, and the only place in Tilverton those appear.",
            None, MapAscii.Parse(MapTerrainData.TilvertonSewersTerrain), "GEO2:4"),

        new("Yulash — Ruined Town", 16, 16,
            "Module 3. The ruined town fought over by the Red Plumes of Hillsfar and the Zhentilar. " +
            "Both sides are on this module's roster along with looters; Alias and Dragonbait have " +
            "records here too, which is where they join the story.",
            None, MapAscii.Parse(MapTerrainData.YulashRuinsTerrain), "GEO3:16"),

        new("Yulash — Tunnels", 16, 16,
            "Module 3. The warren under Yulash linking the two occupying armies' positions — the " +
            "level with the fewest reachable squares in the module, and the giant slug.",
            None, MapAscii.Parse(MapTerrainData.YulashTunnelsTerrain), "GEO3:17"),

        new("Temple of Moander", 16, 16,
            "Module 3. Moander's temple: cultists, shambling mounds, vegepygmies large and small, " +
            "the priestess Mogion (one of the five bond-holders) and a Bit o' Moander worth 11,500 XP. " +
            "Kill Mogion to break her bond.",
            None, MapAscii.Parse(MapTerrainData.MoanderTempleTerrain), "GEO3:21"),

        new("Zhentil Keep — Streets", 16, 16,
            "Module 4. The Black Network's city. Zhentilar fighters, mages and clerics patrol; a " +
            "beholder (12,900 XP) and a hooded medusa are the module's set pieces, and a rakshasa and " +
            "a dark elf lord are in residence.",
            None, MapAscii.Parse(MapTerrainData.ZhentilKeepStreetsTerrain), "GEO4:32"),

        new("Zhentil Keep — Upper Level", 16, 16,
            "Module 4. Ogres, griffons, manticores and minotaurs — the guard beasts of the keep proper.",
            None, MapAscii.Parse(MapTerrainData.ZhentilKeepUpperTerrain), "GEO4:33"),

        new("Zhentil Keep — Dungeon", 16, 16,
            "Module 4. The prison and the sewer beneath it (otyughs again), and the high priest.",
            None, MapAscii.Parse(MapTerrainData.ZhentilKeepDungeonTerrain), "GEO4:37"),

        new("Dracandros — Grounds", 16, 16,
            "Module 5. The approach to the mage Dracandros's stronghold: wyverns, owl bears, ankhegs " +
            "and a black dragon guard the open ground.",
            None, MapAscii.Parse(MapTerrainData.DracandrosGroundsTerrain), "GEO5:50"),

        new("Dracandros — Tower", 16, 16,
            "Module 5. The tightest level in the game — 87 of 256 squares reachable. Dark elf " +
            "fighters, mages, clerics and a dark elf lord hold it, with efreeti and salamanders bound " +
            "to its defence. Dracandros himself is worth 2,850 XP; Akabar bel Akash is here too.",
            None, MapAscii.Parse(MapTerrainData.DracandrosTowerTerrain), "GEO5:51"),

        new("Dracandros — Vault", 16, 16,
            "Module 5. Where the dracolich (13,200 XP — the richest kill in the game) waits.",
            None, MapAscii.Parse(MapTerrainData.DracandrosVaultTerrain), "GEO5:53"),

        new("Myth Drannor — Outer Ruins", 16, 16,
            "Module 6. The elven ruin the adventure ends in. Thri-kreen, phase spiders and giant " +
            "spiders hold the outskirts.",
            None, MapAscii.Parse(MapTerrainData.MythDrannorOuterTerrain), "GEO6:64"),

        new("Myth Drannor — Inner Ruins", 16, 16,
            "Module 6. Hell hounds, margoyles and rakshasas.",
            None, MapAscii.Parse(MapTerrainData.MythDrannorInnerTerrain), "GEO6:66"),

        new("Myth Drannor — Catacombs", 16, 16,
            "Module 6. Priests of Bane and the high priest.",
            None, MapAscii.Parse(MapTerrainData.MythDrannorCatacombsTerrain), "GEO6:67"),

        new("Myth Drannor — Sanctum", 16, 16,
            "Module 6. The finish: Tyranthraxus the Flamed One, possessing a body again and worth " +
            "5,850 XP. Destroy him and the last bond fades — the game's own ending text says so.",
            None, MapAscii.Parse(MapTerrainData.MythDrannorSanctumTerrain), "GEO6:69"),
    };
}
