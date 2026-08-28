namespace MightAndMagic1Trainer.Cluebooks;

/// <summary>What sort of place a maze record is, which is also how the gazetteer is ordered.</summary>
public enum PlaceKind
{
    Town,
    Overworld,
    Cave,
    Castle,
    Stronghold,
    Beyond,
}

/// <summary>
/// One of the game's 55 places: what it is, how sure we are it is that, and what a reader should
/// expect to find there.
/// </summary>
/// <param name="RawName">The engine's own name for it, e.g. <c>qvl1</c> — the maze record's name and the overlay's.</param>
/// <param name="Kind">Which chapter of the gazetteer it belongs in.</param>
/// <param name="Confidence">How firmly the record is tied to the place: "Confirmed", "Inferred" or "Uncertain".</param>
/// <param name="Blurb">A sentence on what happens there, or the empty string when nothing is established.</param>
public sealed record Place(string RawName, PlaceKind Kind, string Confidence, string Blurb);

/// <summary>
/// Which of the game's 55 maze records is which place, and how firmly.
///
/// <para><b>Every entry carries its own confidence, and that is the point of the table.</b> The
/// record order is the location-name order baked into <c>Mm.exe</c>, so the names are exact; what
/// varies is how firmly a name is tied to a place a player would recognise. The five towns, the
/// twenty overworld cells, the Soul Maze and the Astral Plane are confirmed — the towns by their own
/// text, the overworld by the tiling test in <c>docs/maze-atlas.md</c> §1.6, the last two by what
/// their overlays say. The castles and lairs are inferred from their names and their content, and
/// the four <c>pp</c> levels are a guess nobody has confirmed. A cluebook that flattened all of that
/// into one confident list would be wrong in exactly the places a reader could not check.</para>
///
/// <para>The blurbs come from this project's own reading of the overlays
/// (<c>docs/ovr-events.md</c>), so they say what a location contains rather than repeating a
/// community map's label — the two disagree about which forest lair is which, and this is the half
/// that can be re-derived from the bytes.</para>
/// </summary>
public static class PlaceBook
{
    private static Place P(string raw, PlaceKind kind, string confidence, string blurb) =>
        new(raw, kind, confidence, blurb);

    /// <summary>The places, in the game's own record order.</summary>
    public static readonly IReadOnlyList<Place> Places = new[]
    {
        P("sorpigal", PlaceKind.Town, "Confirmed",
            "Where the party starts. Inn, food store, blacksmith, tavern, temple and training hall, a " +
            "leprechaun who will move you between towns for a gem, and stairs down into the caves."),
        P("portsmit", PlaceKind.Town, "Confirmed",
            "The port town. A secret room holds one of the astral brothers and the clue he carries."),
        P("algary", PlaceKind.Town, "Confirmed",
            "Town, and the second astral brother. One of the herbs a castle lord wants is sold here."),
        P("dusk", PlaceKind.Town, "Confirmed",
            "Town. Telgoran sets the task of finding the other two brothers; the blacksmith sells another herb."),
        P("erliquin", PlaceKind.Town, "Confirmed",
            "Town. The wizard behind the inn is reached by walking through the back wall rather than signing in."),

        P("cave1", PlaceKind.Cave, "Inferred", "An arena and a jail, and the courier's letter."),
        P("cave2", PlaceKind.Cave, "Inferred", "The corridor of endless encounters — a grinding ground, not a puzzle."),
        P("cave3", PlaceKind.Cave, "Inferred", "Demons in conference, and a permanent Might reward for interrupting them."),
        P("cave4", PlaceKind.Cave, "Inferred", "The access-code dungeon: doors that want a number you were told elsewhere."),
        P("cave5", PlaceKind.Cave, "Inferred", "The Shrine of Okzar, and permanent Accuracy and Speed."),
        P("cave6", PlaceKind.Cave, "Inferred", "The wizard Ranalou, and portals to the six castles."),
        P("cave7", PlaceKind.Cave, "Inferred", "The Volcano God, his riddle, and the Key Card."),
        P("cave8", PlaceKind.Cave, "Inferred", "The magic square: set the polyhedrons, pull the lever."),
        P("cave9", PlaceKind.Cave, "Inferred", "Spike pits and snake pits."),

        P("areaa1", PlaceKind.Overworld, "Confirmed", "The approach to Castle Doom."),
        P("areaa2", PlaceKind.Overworld, "Confirmed", "The druid who hands over the King's Pass is somewhere in here."),
        P("areaa3", PlaceKind.Overworld, "Confirmed", ""),
        P("areaa4", PlaceKind.Overworld, "Confirmed", "The gypsy bridge, which asks each character their sign."),
        P("areab1", PlaceKind.Overworld, "Confirmed", "The Quivering Forest, and the way into its two lairs."),
        P("areab2", PlaceKind.Overworld, "Confirmed", "Raven's Wood, and the stronghold that holds the Crystal Key."),
        P("areab3", PlaceKind.Overworld, "Confirmed", "The Enchanted Forest, the Korin Bluffs cave, and Portsmith."),
        P("areab4", PlaceKind.Overworld, "Confirmed", "Open country and the roads between the first town and the forests."),
        P("areac1", PlaceKind.Overworld, "Confirmed", "Ogres with a Merchant Pass, and the square the two brothers' clues point at."),
        P("areac2", PlaceKind.Overworld, "Confirmed", "The Crazed Wizard's Cave, and the gypsy who reads your signs."),
        P("areac3", PlaceKind.Overworld, "Confirmed", "Lord Kilburn, and the Desert Map that stops you getting lost."),
        P("areac4", PlaceKind.Overworld, "Confirmed", "The volcanic island, reached over water and opened with the Coral Key."),
        P("aread1", PlaceKind.Overworld, "Confirmed", ""),
        P("aread2", PlaceKind.Overworld, "Confirmed", ""),
        P("aread3", PlaceKind.Overworld, "Confirmed", "The magical square."),
        P("aread4", PlaceKind.Overworld, "Confirmed", "Algary's countryside."),
        P("areae1", PlaceKind.Overworld, "Confirmed", "Dusk's countryside."),
        P("areae2", PlaceKind.Overworld, "Confirmed", ""),
        P("areae3", PlaceKind.Overworld, "Confirmed", "Castle Alamar stands here."),
        P("areae4", PlaceKind.Overworld, "Confirmed", ""),

        P("doom", PlaceKind.Castle, "Inferred",
            "The spiral, the imprisoned king, the Eye of Goros, and the interleave clue that explains " +
            "what the Inner Sanctum wants."),
        P("blackrn", PlaceKind.Castle, "Inferred", "Lord Inspectron, who sends you to the ancient ruins. Holds silver message A."),
        P("blackrs", PlaceKind.Castle, "Inferred", "Lord Hacker, who wants three herbs and a head. Holds silver message C."),
        P("qvl1", PlaceKind.Stronghold, "Inferred", "The lair of the wizard Okrim, who trades a ring for a life. Holds gold message 1."),
        P("qvl2", PlaceKind.Stronghold, "Inferred", "The Labyrinth of Lazzeruth. Holds gold message 4."),
        P("rwl1", PlaceKind.Stronghold, "Inferred", "Raven's Wood, lower level. Holds gold message 6."),
        P("rwl2", PlaceKind.Stronghold, "Inferred", "Raven's Wood, and the Master Archer at the end of Lord Ironfist's chain. Holds gold message 2."),
        P("enf1", PlaceKind.Stronghold, "Inferred", "The Enchanted Forest stronghold. Holds gold message 3."),
        P("enf2", PlaceKind.Stronghold, "Inferred", "The Enchanted Forest stronghold, lower level. Holds gold message 9."),
        P("whitew", PlaceKind.Castle, "Inferred", "Lord Ironfist. Holds silver message B."),
        P("dragad", PlaceKind.Castle, "Inferred",
            "The ruined castle: no lord, but gold converts to experience at one corner and the worthy " +
            "gain Luck at another. Holds silver message F."),
        P("udrag1", PlaceKind.Stronghold, "Inferred", "Under Dragadune. Holds gold message 8."),
        P("udrag2", PlaceKind.Stronghold, "Inferred", "Under Dragadune, second level."),
        P("udrag3", PlaceKind.Stronghold, "Inferred", "Under Dragadune, third level. Holds gold message 5."),
        P("demon", PlaceKind.Beyond, "Confirmed",
            "The Soul Maze. Its walls spell the answer, and neither the Location spell nor any map " +
            "will help you — this one is drawn by hand or not at all."),
        P("alamar", PlaceKind.Castle, "Inferred",
            "The lion statue wants the day's password, and the throne room wants the Eye of Goros to " +
            "show who is really sitting in it. Holds silver message E."),
        P("pp1", PlaceKind.Stronghold, "Uncertain", "The Temple of the Old Order, so its own text says. Holds gold message 7."),
        P("pp2", PlaceKind.Stronghold, "Uncertain", "Checkered rooms."),
        P("pp3", PlaceKind.Stronghold, "Uncertain", "The smallest overlay in the game."),
        P("pp4", PlaceKind.Stronghold, "Uncertain", "A level whose own text calls it under construction."),
        P("astral", PlaceKind.Beyond, "Confirmed",
            "The Astral Plane, reached only by the Astral spell. Every wall is invisible, the five " +
            "projectors are here, and so is the door the Key Card opens."),
    };

    private static readonly Dictionary<string, Place> ByName =
        Places.ToDictionary(p => p.RawName, StringComparer.OrdinalIgnoreCase);

    /// <summary>The place a maze record's raw name stands for, or null when the name is not one of the 55.</summary>
    public static Place? For(string rawName) =>
        rawName is not null && ByName.TryGetValue(rawName, out var place) ? place : null;

    /// <summary>The gazetteer's chapter heading for a kind.</summary>
    public static string KindName(PlaceKind kind) => kind switch
    {
        PlaceKind.Town => "The towns",
        PlaceKind.Overworld => "The surface of VARN",
        PlaceKind.Cave => "The caves",
        PlaceKind.Castle => "The castles",
        PlaceKind.Stronghold => "Lairs and strongholds",
        _ => "Beyond the map",
    };

    /// <summary>The order the gazetteer's chapters run in: outward from town, then off the map entirely.</summary>
    public static readonly IReadOnlyList<PlaceKind> KindOrder = new[]
    {
        PlaceKind.Town, PlaceKind.Overworld, PlaceKind.Cave,
        PlaceKind.Castle, PlaceKind.Stronghold, PlaceKind.Beyond,
    };
}

/// <summary>
/// The two ciphers the endgame is built on, and which file each fragment lives in.
///
/// <para>Nine "ETCHED IN GOLD" fragments are scattered one per stronghold and read in order 1–9;
/// six "ETCHED IN SILVER" fragments are hidden in the castles and re-ordered by a rule Castle Doom
/// tells you. <b>This table holds only where each fragment is, never the fragment.</b> The text
/// itself is read out of the player's own overlays when they point the cluebook at their
/// installation — which also makes the collection worth having, because it puts nine messages that
/// are nine dungeons apart onto one page in the right order.</para>
/// </summary>
public static class PuzzleTrail
{
    /// <summary>One fragment: which it is, where it lives, and the phrase that finds it in that file.</summary>
    /// <param name="Label">"Gold 1", "Silver A".</param>
    /// <param name="RawName">The overlay that holds it.</param>
    /// <param name="Marker">The text the message starts with, used to pick it out of that file.</param>
    public sealed record Fragment(string Label, string RawName, string Marker);

    /// <summary>The nine gold fragments, in reading order.</summary>
    public static readonly IReadOnlyList<Fragment> Gold = new[]
    {
        new Fragment("Gold 1", "qvl1", "ETCHED IN GOLD, MESSAGE 1"),
        new Fragment("Gold 2", "rwl2", "ETCHED IN GOLD, MESSAGE 2"),
        new Fragment("Gold 3", "enf1", "ETCHED IN GOLD, MESSAGE 3"),
        new Fragment("Gold 4", "qvl2", "ETCHED IN GOLD, MESSAGE 4"),
        new Fragment("Gold 5", "udrag3", "ETCHED IN GOLD, MESSAGE 5"),
        new Fragment("Gold 6", "rwl1", "ETCHED IN GOLD, MESSAGE 6"),
        new Fragment("Gold 7", "pp1", "ETCHED IN GOLD, MESSAGE 7"),
        new Fragment("Gold 8", "udrag1", "ETCHED IN GOLD, MESSAGE 8"),
        new Fragment("Gold 9", "enf2", "ETCHED IN GOLD, MESSAGE 9"),
    };

    /// <summary>The six silver fragments, in label order — which is <em>not</em> the order they decode in.</summary>
    public static readonly IReadOnlyList<Fragment> Silver = new[]
    {
        new Fragment("Silver A", "blackrn", "ETCHED IN SILVER"),
        new Fragment("Silver B", "whitew", "ETCHED IN SILVER"),
        new Fragment("Silver C", "blackrs", "ETCHED IN SILVER"),
        new Fragment("Silver D", "doom", "ETCHED IN SILVER"),
        new Fragment("Silver E", "alamar", "ETCHED IN SILVER"),
        new Fragment("Silver F", "dragad", "ETCHED IN SILVER"),
    };
}
