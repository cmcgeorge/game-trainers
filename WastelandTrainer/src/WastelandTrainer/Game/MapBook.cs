namespace WastelandTrainer.Game;

/// <summary>A notable spot within a Wasteland area: its name and a one-line note.</summary>
public sealed record MapLandmark(string Name, string Notes);

/// <summary>One explorable Wasteland area with a description and its notable landmarks.</summary>
public sealed record MapArea(string Name, string Notes, IReadOnlyList<MapLandmark> Landmarks);

/// <summary>
/// Wasteland area / landmark reference for the Maps tab. Wasteland stores the party's live position
/// as X/Y bytes in the 256-byte party-state header that precedes the roster
/// (<see cref="CharacterFormat.HeaderPartyX"/> / <see cref="CharacterFormat.HeaderPartyY"/>) and the
/// current map's 12-byte name at <see cref="CharacterFormat.HeaderMapName"/>. The trainer reads the
/// live name and X/Y and can teleport within the loaded map by writing the two position bytes.
///
/// A numeric map <i>id</i> is not clearly located adjacent to the roster, so this table is a
/// descriptive reference (area + landmarks) rather than a coordinate atlas; exact interior grid
/// coordinates are not reproduced because they were not confirmed against live memory. The one
/// confirmed coordinate is the Ranger Center start (X 55, Y 62). Reference only.
/// </summary>
public static class MapBook
{
    /// <summary>Reads the current 12-byte map name from a party-header buffer.</summary>
    public static string MapName(byte[] header)
    {
        var s = WastelandText.Decode(header, CharacterFormat.HeaderMapName, CharacterFormat.MapNameLength);
        return string.IsNullOrWhiteSpace(s) ? "(unknown)" : s.Trim();
    }

    public static readonly IReadOnlyList<MapArea> Areas = new MapArea[]
    {
        new("Ranger Center",
            "Home base of the Desert Rangers. Create, manage and promote characters here; the party starts at X 55, Y 62.",
            new MapLandmark[]
            {
                new("Roster Room", "Create, delete and equip starting characters."),
                new("Radio Room", "Call in from the field to level up and receive promotions."),
                new("Infirmary", "Heal wounded and unconscious rangers."),
                new("Desert Exit", "Gateway out to the southern desert overworld."),
            }),

        new("Highpool",
            "A small settlement built around a reservoir; the first town most parties visit.",
            new MapLandmark[]
            {
                new("Water Pipe", "Leak that must be repaired (Perception/tools) to win the town's trust."),
                new("Cave Entrance", "Subterranean area holding a boy's dog and gang loot."),
                new("Chubby's Shop", "Merchant selling early-game gear and ammunition."),
            }),

        new("Agricultural Center",
            "A besieged farming complex overrun by mutated plants and creatures.",
            new MapLandmark[]
            {
                new("Root Cellar", "Underground hatch leading to food storage."),
                new("Harry's House", "Talk to Harry to start the mutant-pest quest."),
                new("Agro-Bot", "Malfunctioning harvester robot blocking paths."),
            }),

        new("Rail Nomads Camp",
            "A camp of railroad nomads split into feuding clans.",
            new MapLandmark[]
            {
                new("The Hobo", "Oracle-like figure giving vital passwords and clues."),
                new("Clan Hall", "Base for the Brakeman and Engineer leaders."),
                new("Casino Car", "Card tables for gambling and trading goods."),
            }),

        new("Quartz",
            "A lawless desert town controlled by bandits.",
            new MapLandmark[]
            {
                new("Scott's Bar", "Bandit-controlled tavern with contacts and brawls."),
                new("Courthouse", "Secure building holding Mayor Pedros and hostages."),
                new("Ugly's Hideout", "Fortified lair of the local bandit boss."),
            }),

        new("Needles",
            "A ruined city home to the blood-cult that stole the Bloodstaff.",
            new MapLandmark[]
            {
                new("Temple of Blood", "Cult headquarters hiding the stolen Bloodstaff."),
                new("Sphinx", "Monument concealing high-tier weapons under its paws."),
                new("Waste Dump", "Irradiated landfill holding a Radiation Suit."),
            }),

        new("Las Vegas",
            "A sprawling ruined casino city and gateway to the Sleeper Base sewers.",
            new MapLandmark[]
            {
                new("Brygo's Palace", "Central casino with crucial robot-investigation leads."),
                new("Mushroom Church", "Cult headquarters with secret sewer entries."),
                new("Sewers", "Massive dungeon connecting to the Sleeper Base."),
            }),

        new("Savage Village",
            "A primitive village of desert savages holding a hostage.",
            new MapLandmark[]
            {
                new("Chief Redhawk's Hut", "Chieftain location for local negotiations."),
                new("Prison Cells", "Jail holding the Mayor's daughter hostage."),
                new("Loot Cache", "Tribal supply stash hidden among the huts."),
            }),

        new("Darwin",
            "A hidden mutation facility run by the villain Finster.",
            new MapLandmark[]
            {
                new("Science Lab", "Mutation facility holding the Blackstar Key."),
                new("Mind Maze", "Finster's psychological puzzle gauntlet."),
                new("Cloning Vats", "Area used to replicate party members."),
            }),

        new("Guardian Citadel",
            "A fortified monastery of the Guardians of the Old Order.",
            new MapLandmark[]
            {
                new("Main Gate", "Deadly choke point guarded by high-tech monks."),
                new("Museum", "Exhibition room displaying rare pre-war weapons."),
                new("Inner Sanctum", "Fortress interior holding Power Armor."),
            }),

        new("Sleeper Base",
            "A dormant military base beneath Las Vegas.",
            new MapLandmark[]
            {
                new("Power Core", "Requires a power converter to boot systems."),
                new("Supply Depot", "Holds the plasma coupler and endgame loot."),
                new("Clone Vats", "High-tech healing and cloning facility."),
            }),

        new("Base Cochise",
            "The final dungeon: an AI-controlled war base building an army of robots.",
            new MapLandmark[]
            {
                new("Assembly Line", "Robotic manufacturing facility and combat zone."),
                new("Core Terminal", "Computer terminal to trigger the base self-destruction."),
                new("Escape Pods", "Pod bay used to flee before detonation."),
            }),
    };
}
