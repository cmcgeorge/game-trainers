namespace AirborneRangerTrainer.Game;

/// <summary>One item the supply-pod screen prices, with the weight the game charges for it.</summary>
/// <param name="Name">The item's name on the supply-pod screen.</param>
/// <param name="Weight">Capacity points it consumes.</param>
/// <param name="StandardLoad">How many the STANDARD pod loadout carries.</param>
public readonly record struct EquipmentInfo(string Name, int Weight, int StandardLoad);

/// <summary>A keyboard command, with how firmly it is established.</summary>
/// <param name="Key">The key or keys.</param>
/// <param name="Action">What it does.</param>
/// <param name="Confirmed">
/// True when the key is pinned by the game's own scan-code table or command dispatcher; false when
/// only the effect is confirmed and the physical key is inferred.
/// </param>
public readonly record struct ControlInfo(string Key, string Action, bool Confirmed);

/// <summary>
/// Facts about <c>Airborne Ranger</c> that the trainer displays but never edits. Everything here was
/// read out of <c>AR.EXE</c> or confirmed against the running game, not taken from a platform manual
/// — the C64 release's controls in particular do <b>not</b> match this build.
/// </summary>
public static class GameFacts
{
    /// <summary>Title as the game prints it.</summary>
    public const string GameTitle = "Airborne Ranger";

    /// <summary>Publisher and year.</summary>
    public const string Publisher = "MicroProse Software, 1988";

    /// <summary>The build's own version string, a literal at <c>DGROUP:0xB7F7</c>.</summary>
    public const string Version = "441.01";

    /// <summary>Capacity of one supply pod, in weight points.</summary>
    public const int SupplyPodCapacity = 21;

    /// <summary>Supply pods you may drop during the airdrop.</summary>
    public const int SupplyPodsPerMission = 3;

    /// <summary>Process-name fragments that mark a likely emulator, floated to the top of the list.</summary>
    public static readonly IReadOnlyList<string> EmulatorHints = new[]
    {
        "dosbox", "dosbox-x", "dosbox-staging", "pcem", "86box", "qemu", "boxer",
    };

    /// <summary>
    /// The equipment table from the supply-pod screen. The weights are not a guess: the mission
    /// status panel's WEIGHT readout is exactly their sum, and a starting loadout of 3 spare
    /// magazines, 3 grenades, 1 LAW rocket, 1 time bomb and 1 first-aid kit gives
    /// 3 + 6 + 6 + 3 + 3 = 21, plus 1 for the loaded magazine = the 22 the game displays.
    /// </summary>
    public static readonly IReadOnlyList<EquipmentInfo> Equipment = new[]
    {
        new EquipmentInfo("Carbine magazine", 1, 3),
        new EquipmentInfo("Hand grenade", 2, 3),
        new EquipmentInfo("First-aid kit", 3, 1),
        new EquipmentInfo("Time bomb", 3, 1),
        new EquipmentInfo("LAW rocket", 6, 1),
    };

    /// <summary>Total weight of the STANDARD supply-pod loadout — equal to <see cref="SupplyPodCapacity"/>.</summary>
    public static int StandardLoadWeight
    {
        get
        {
            int total = 0;
            foreach (var e in Equipment) total += e.Weight * e.StandardLoad;
            return total;
        }
    }

    /// <summary>
    /// The keyboard controls. The movement and fire keys come from the scan-code table the game's
    /// own interrupt-9 handler indexes (<c>DGROUP:0x0950</c>), read out of a live session; the
    /// commands come from the ASCII comparisons in the dispatcher at <c>0x5F60</c>. The function-key
    /// row is marked unconfirmed: the dispatcher's weapon and map codes are certain, but which
    /// physical key produces each was not verified on screen.
    /// </summary>
    public static readonly IReadOnlyList<ControlInfo> Controls = new[]
    {
        new ControlInfo("↑ ↓ ← →, keypad 8 2 4 6", "Move north / south / west / east — hold, do not tap", true),
        new ControlInfo("keypad 7 9 1 3", "Move diagonally", true),
        new ControlInfo("Enter, keypad 5, keypad 0", "Fire / select / drop a pod / jump", true),
        new ControlInfo("Space", "Toggle upright and crawling", true),
        new ControlInfo("5 / 6 / 7", "Plant a time bomb with a 5- / 10- / 15-second fuse", true),
        new ControlInfo("Backspace", "Use a first-aid kit — removes one wound", true),
        new ControlInfo("1", "Recall the aircraft (does nothing in Create a Diversion)", true),
        new ControlInfo("F1 / F2 / F3 / F4", "Select carbine / grenade / LAW rocket / knife", false),
        new ControlInfo("F9", "Show the map and status panel — the countdown stops while it is up", false),
    };

    /// <summary>
    /// The 23 campaign ribbons the manual-lookup copy protection asks about, from the table at
    /// <c>DGROUP:0xB4D7</c>. The answer is the ribbon artwork in the manual, so this is the question
    /// list rather than an answer key.
    /// </summary>
    public static readonly IReadOnlyList<string> ProtectionRibbons = new[]
    {
        "Army Achievement Medal", "Army Commendation Medal", "Army of Occupation Medal",
        "Asiatic-Pacific Campaign", "Bronze Star", "Distinguished Service Cross",
        "Distinguished Service Medal", "European-African Campaign", "Good Conduct Medal",
        "Joint Meritorious Unit Award", "Korean Service Medal", "Legion of Merit",
        "NCO Professional Development", "Oversea Service", "Presidential Unit Citation",
        "Purple Heart", "Silver Star", "Soldier's Medal", "United Nations Service Medal",
        "Valorous Unit Award", "Vietnam Pres. Unit Citation", "Vietnam Service Medal",
        "World War II Victory Medal",
    };

    /// <summary>Short survival notes drawn from the strategy guide.</summary>
    public static readonly IReadOnlyList<string> Tips = new[]
    {
        "There is no pause. The map screen is the closest thing to one — the countdown does not run while it is up, so plan your route there.",
        "Three wounds kill you, and a first-aid kit is refused at three. Heal at one or two, never later.",
        "Crawl by default. An enemy who is not in the same trench cannot see you in it.",
        "Half the missions penalise or fail outright on premature contact. The knife is silent; that is what it is for.",
        "Start with missions 2, 3 or 4 — the game's own challenge table rates them easiest.",
        "The Mission Difficulty slider is the one thing you control; leave it at Easy until you know a map.",
        "If you never press fire after the jump light comes on, the mission is aborted with nothing scored.",
        "Supply pods land where you drop them. Drop them on a landmark you can navigate back to.",
        "Recall the aircraft the moment the objective is met — extra time on the ground is risk without reward.",
        "Practice Rangers are never written to the roster. Learn a mission on one before risking a veteran.",
    };
}
