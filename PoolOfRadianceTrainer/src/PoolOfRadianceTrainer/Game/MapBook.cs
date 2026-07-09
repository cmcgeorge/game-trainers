namespace PoolOfRadianceTrainer.Game;

/// <summary>A keyed spot on an area map: its (x, y) grid square and what's there.</summary>
public sealed record MapLocation(string Name, int X, int Y, string Notes = "")
{
    public string Coord => $"({X}, {Y})";
}

/// <summary>
/// One explorable area of Phlan, with its grid size and the notable keyed locations on it.
/// </summary>
public sealed record MapArea(string Name, int Width, int Height, string Notes, IReadOnlyList<MapLocation> Locations)
{
    public string Size => $"{Width}×{Height}";
    public string Header => $"{Name}   ({Size} grid)";
}

/// <summary>
/// Transcribed area/location reference for Pool of Radiance, drawn from the bundled strategy guide
/// (<c>.docs/strategy-guide.md</c>), which in turn sources GameBanshee, oldgames.sk maps and
/// Stephen S. Lee's FAQ. Coordinates follow the game's own convention — <b>(x, y) = (column, row)</b>,
/// origin (0, 0) at the top-left / north-west, x east, y south — the same system the memory dump
/// records ("Slums 0,4"). Reference only; the 🗺 Maps tab uses the coordinates to drive the manual
/// teleport helper.
/// </summary>
public static class MapBook
{
    public static readonly IReadOnlyList<MapArea> Areas = new MapArea[]
    {
        new("Slums", 16, 16,
            "First commission — start here. Goblins/kobolds/orcs; a hobgoblin mage drops the Wand of " +
            "Magic Missiles; the infamous ogres + trolls fight is in the SW corner.",
            new MapLocation[]
            {
                new("Exit ↔ New Phlan (E1)", 15, 4, "East exit back to the civilized quarter."),
                new("Exit ↔ Kuto's Well (E2)", 0, 4, "West exit."),
                new("Rope Guild stairs (E3)", 6, 10, "Down to the Rope Guild (automap off inside)."),
                new("Illusory-wall treasure (12)", 0, 0, "Enter from the east."),
                new("Kobolds → Bracers AC 6 (6)", 7, 0, ""),
                new("Orcs w/ scroll (1)", 13, 1, ""),
                new("Goblins → Leather Armor +1 (2)", 10, 1, ""),
                new("Hobgoblins → Ring of Protection +1 (10)", 0, 2, ""),
                new("Orc leaders → Chain Mail +1, Flail +1 (9)", 3, 3, ""),
                new("Monster leaders (14)", 1, 5, ""),
                new("Mage Ohlo (3)", 13, 10, "Rope Guild errand — say OHLO to the guild merchant."),
                new("Rope-Guild merchant (19)", 15, 12, "Deliver Ohlo's package for a monster-blasting necklace."),
                new("4 Trolls + 2 Ogres (20)", 0, 14, "SW corner — Sleep ogres, fire/oil on trolls, stand on corpses."),
            }),

        new("Sokal Keep", 16, 16,
            "Reach it by boat; clearing it opens the wilderness. Passwords LUX / SHESTNI / SAMOSUD. " +
            "Never melee Ferran or the spectres (2-level drain).",
            new MapLocation[]
            {
                new("Boat ↔ New Phlan (E1)", 11, 15, "Bottom of the map."),
                new("Dead elf / passwords (1)", 6, 13, "SEARCH for the rune scroll."),
                new("Elven ghosts / barracks", 6, 2, "Parley with LUX before Ferran — diary + 5 gems."),
                new("Ferran Martinez altar (12)", 7, 9, "Give LUX, then tell the truth to finish the mission."),
                new("Armory illusory-wall cache (17)", 12, 0, "Long Sword +1, Chain Mail +1, Mace +2, Shield +1."),
                new("Huge scorpions (8)", 2, 11, "Poison — skip or Sleep."),
            }),

        new("Kuto's Well", 16, 16,
            "Free the Wide-Eyed Woman (banded mail +1, quarter staff +1, bracers AC 4), then descend " +
            "the well to fight Norris the Gray in the catacombs.",
            new MapLocation[]
            {
                new("Well down to catacombs (E4)", 7, 7, "Descend to Norris the Gray (drops Long Sword +1 + a Boss note)."),
            }),

        new("Podol Plaza", 16, 16,
            "Auction spy commission — on entry choose 'disguise yourself as monsters'. Garwin escapes " +
            "no matter your bid; witnessing it completes the commission.",
            new MapLocation[]
            {
                new("The Pit (2)", 4, 8, "Duel the drunk buccaneer — Long Sword +1 + Chain Mail +1."),
                new("Orc priest of Bane (6)", 14, 8, "Drops 6 leather holy symbols — needed to enter the Temple of Bane."),
                new("Temple of Ilmater", 1, 15, "SW — Knock the doors; a safe rest/heal."),
            }),

        new("Mendor's Library", 16, 16,
            "Cast Knock to enter. Search Philosophy (2) and History (4) for Tyranthraxus's origin; the " +
            "Rhetoric section (5) hides a Basilisk — equip mirrors! Leaving with a book triggers a spectre.",
            new MapLocation[]
            {
                new("Library door (E3)", 12, 1, "Cast Knock — bashing is unreliable."),
                new("Surrendering kobolds → map (18)", 12, 10, ""),
                new("Potions of Extra Healing (8)", 8, 11, "3 potions, under a floor jar."),
                new("Mad Man (11)", 11, 12, "Raves of 'the castle of flowers on the hill' — points to Valjevo Castle."),
                new("Manual of Bodily Health (13)", 13, 13, "Permanent +CON; sells for ~25,000 gp."),
            }),

        new("Kovel Mansion", 16, 16,
            "Thieves' guild — traps everywhere. Move in Search Mode, cast Find Traps, bring a thief + " +
            "Knock. Reveals Cadorna's treachery and that the Boss is a dragon in Valjevo Castle.",
            new MapLocation[]
            {
                new("Entrance (double door)", 9, 14, "From the north — the three west doors are fake."),
                new("Weapons cache (deadliest trap)", 3, 11, "Short Sword +1, Hammer +2, etc."),
                new("Four caskets (42 gems)", 6, 8, ""),
            }),

        new("Cadorna Textile House", 16, 16,
            "Councilman Cadorna's commission — recover the iron treasure box (holds the Gauntlets of " +
            "Ogre Power, STR 18/00). Opening it breaks the seal and earns his enmity; a thief can re-forge it. " +
            "No verified grid coordinates transcribed.",
            Array.Empty<MapLocation>()),

        new("Wealthy District & Temple of Bane", 16, 16,
            "Grinding ground (orcs/hobgoblins/ogres). Enter the Temple of Bane with the leather holy " +
            "symbols from Podol Plaza for Dust of Disappearance (save it for the final fight) + a Ring of " +
            "Feather Falling. No verified grid coordinates transcribed.",
            Array.Empty<MapLocation>()),

        new("Valjevo Castle", 18, 20,
            "The endgame — four 18×20 quadrants (SW entry, plus NW/NE/SE) ring a poisonous hedge maze " +
            "around the Inner Tower and the Pool. Passwords HARASH / TYRANTHRAXUS / RHODIA. Do NOT steal " +
            "from the Altar of Bane.",
            new MapLocation[]
            {
                new("Flame Tongue Long Sword +2 (SE well)", 15, 10, "SE quadrant."),
            }),
    };
}
