namespace QuestForGlory1Trainer.Game;

/// <summary>One room entry for display in the Teleport tab's dropdown.</summary>
public sealed class RoomEntry
{
    /// <summary>SCI0 room number (written to global[1] to trigger a room change).</summary>
    public int Number { get; }

    /// <summary>Human-readable room name.</summary>
    public string Name { get; }

    /// <summary>
    /// True when the room number was confirmed in-game via ALT-T debug teleport or scan
    /// cross-reference; false when estimated from resource-map inspection.
    /// </summary>
    public bool Confirmed { get; }

    public string Display => $"{Number,4}  {Name}{(Confirmed ? "" : " *")}";

    public RoomEntry(int number, string name, bool confirmed = false)
    {
        Number = number;
        Name = name;
        Confirmed = confirmed;
    }
}

/// <summary>
/// Known room numbers for Quest for Glory I. Entries marked with an asterisk (*) in the
/// dropdown are estimates that have not been confirmed in-game; teleporting to an unconfirmed
/// room may crash the interpreter — save first.
/// </summary>
public static class RoomBook
{
    public static readonly IReadOnlyList<RoomEntry> Rooms = new[]
    {
        new RoomEntry(  1, "Spielburg Valley — south road"),
        new RoomEntry(  2, "Spielburg Valley — south-west"),
        new RoomEntry(  3, "Spielburg Valley — west"),
        new RoomEntry(  4, "Spielburg Valley — north-west"),
        new RoomEntry(  5, "Forest — far west"),
        new RoomEntry( 10, "Spielburg — town gate",          confirmed: true),
        new RoomEntry( 11, "Spielburg — town square",        confirmed: true),
        new RoomEntry( 12, "Spielburg — north street"),
        new RoomEntry( 13, "Spielburg — alley"),
        new RoomEntry( 14, "Adventurers' Guild — exterior"),
        new RoomEntry( 15, "Adventurers' Guild — interior",  confirmed: true),
        new RoomEntry( 20, "Dry Grape Inn — exterior",       confirmed: true),
        new RoomEntry( 21, "Dry Grape Inn — common room",    confirmed: true),
        new RoomEntry( 22, "Dry Grape Inn — hero's room"),
        new RoomEntry( 30, "Meeps' Curiosity Shoppe — exterior"),
        new RoomEntry( 31, "Meeps' Curiosity Shoppe — interior", confirmed: true),
        new RoomEntry( 40, "Weapon shop — exterior"),
        new RoomEntry( 41, "Weapon shop — interior",         confirmed: true),
        new RoomEntry( 50, "Healer's hut — exterior",        confirmed: true),
        new RoomEntry( 51, "Healer's hut — interior",        confirmed: true),
        new RoomEntry( 60, "Magic shop (Zara's) — exterior"),
        new RoomEntry( 61, "Magic shop (Zara's) — interior", confirmed: true),
        new RoomEntry( 70, "Sheriff's office — exterior"),
        new RoomEntry( 71, "Sheriff's office — interior",    confirmed: true),
        new RoomEntry( 80, "Erasmus's mountain — forest path"),
        new RoomEntry( 81, "Erasmus's house — exterior"),
        new RoomEntry( 82, "Erasmus's house — interior"),
        new RoomEntry( 90, "Castle Spielburg — drawbridge"),
        new RoomEntry( 91, "Castle Spielburg — courtyard"),
        new RoomEntry( 92, "Castle Spielburg — great hall"),
        new RoomEntry(100, "Erana's Peace",                  confirmed: true),
        new RoomEntry(110, "Baba Yaga's hut — exterior"),
        new RoomEntry(111, "Baba Yaga's hut — interior"),
        new RoomEntry(120, "Brigand fortress — approach"),
        new RoomEntry(121, "Brigand fortress — exterior"),
        new RoomEntry(122, "Brigand fortress — barracks"),
        new RoomEntry(123, "Brigand fortress — dungeon"),
        new RoomEntry(124, "Brigand fortress — leader's chamber"),
        new RoomEntry(130, "Ogre's territory"),
        new RoomEntry(140, "Troll's bridge — approach"),
        new RoomEntry(141, "Troll's bridge"),
        new RoomEntry(150, "Antwerp meadow"),
        new RoomEntry(160, "Flying Falls"),
        new RoomEntry(170, "Kobold cave — entrance"),
        new RoomEntry(171, "Kobold cave — interior"),
        new RoomEntry(180, "Bear cave — exterior"),
        new RoomEntry(181, "Bear cave — interior"),
        new RoomEntry(190, "Cheetaur territory"),
    };
}
