namespace MightAndMagic1Trainer.Game;

/// <summary>
/// One reference map: a bundled image of a Might &amp; Magic 1 area, plus where it sits in
/// the world. <see cref="Image"/> is the file name under <c>Assets\Maps\</c>; the view model
/// turns it into a pack URI. Maps are grouped in the UI by <see cref="Category"/>.
/// </summary>
public sealed record GameMap(string Category, string Name, string Image, string Description);

/// <summary>
/// The complete set of bundled MM1 reference maps (transcribed/scanned community maps,
/// the same coordinate scheme the game uses). Purely reference data — independent of any
/// attached game — mirroring how <see cref="Spellbook"/> backs the spell reference.
/// </summary>
public static class MapBook
{
    private static GameMap M(string cat, string name, string img, string desc) => new(cat, name, img, desc);

    public static readonly IReadOnlyList<GameMap> Maps = new[]
    {
        // ===== Overworld (the surface of VARN, a 5x5 grid of 16x16 areas A1..E5) =====
        M("Overworld", "The World of VARN (full surface)", "world-of-varn.png",
            "The entire VARN continent stitched into one map, with towns, castles and landmarks labelled."),
        M("Overworld", "Areas A-1 to A-4", "overworld-a.png",
            "The westernmost column of the surface, including the starting town of Sorpigal (A-1)."),
        M("Overworld", "Areas B-1 to B-4", "overworld-b.png",
            "Column B of the surface, home to the Quivering, Raven's and Enchanted forests and their lairs."),
        M("Overworld", "Areas C-1 to C-4", "overworld-c.png",
            "Column C of the surface, including the Crazed Wizard's Cave (C-2) and the Volcano (C-4)."),
        M("Overworld", "Areas D-1 to D-4", "overworld-d.png",
            "Column D of the surface, including the Magical Square (D-3)."),
        M("Overworld", "Areas E-1 to E-4", "overworld-e.png",
            "The easternmost column of the surface."),

        // ===== Towns =====
        M("Towns", "Sorpigal & Erliquin (with dungeons)", "towns-sorpigal-erliquin.png",
            "The starting town of Sorpigal and the town of Erliquin, each with their underground dungeon."),
        M("Towns", "Portsmith & Algary (with dungeon)", "towns-portsmith-algary.png",
            "The towns of Portsmith and Algary, with the Portsmith dungeon."),
        M("Towns", "Dusk (with dungeon)", "town-dusk.png",
            "The town of Dusk and its dungeon."),

        // ===== Castles =====
        M("Castles", "Blackridge & White Wolf", "castles-blackridge-whitewolf.png",
            "Castle Blackridge and Castle White Wolf."),
        M("Castles", "Alamar & Doom", "castles-alamar-doom.png",
            "Castle Alamar and Castle Doom."),
        M("Castles", "Dragadune (ruins)", "castle-dragadune-ruins.png",
            "The ruins of Castle Dragadune."),

        // ===== Dungeons, caves & special areas =====
        M("Dungeons & Special", "Warrior's Stronghold (B-2)", "warriors-stronghold-b2.png",
            "The Warrior's Stronghold hidden in the Raven's Wood of B-2."),
        M("Dungeons & Special", "Ancient Wizard's Lair (B-1)", "ancient-wizards-lair-b1.png",
            "The Ancient Wizard's Lair in the Quivering Forest of B-1."),
        M("Dungeons & Special", "Sealed Minotaur Stronghold (B-3)", "minotaur-stronghold-b3.png",
            "The sealed Minotaur Stronghold in the Enchanted Forest of B-3."),
        M("Dungeons & Special", "Medusa's Lair (B-2)", "medusas-lair-b2.png",
            "The Medusa's Lair in B-2."),
        M("Dungeons & Special", "The Magical Square (D-3)", "magical-square-d3.png",
            "The Magical Square puzzle area of D-3."),
        M("Dungeons & Special", "Cave at Korin Bluffs (B-3)", "korin-bluffs-cave-b3.png",
            "The cave in the Korin Bluffs of B-3."),
        M("Dungeons & Special", "Crazed Wizard's Cave (C-2)", "crazed-wizards-cave-c2.png",
            "The Crazed Wizard's Cave of C-2."),
        M("Dungeons & Special", "Fabled Building of Gold", "fabled-building-of-gold.png",
            "The Fabled Building of Gold."),
        M("Dungeons & Special", "The Volcano (C-4)", "volcano-c4.png",
            "The Volcano of C-4."),
        M("Dungeons & Special", "The Soul Maze", "soul-maze.png",
            "The Soul Maze."),
        M("Dungeons & Special", "The Astral Plane", "astral-plane.png",
            "The Astral Plane, reached by the Astral Spell."),
    };
}
