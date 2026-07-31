using AirborneRangerTrainer.Game;

namespace AirborneRangerTrainer.ViewModels;

/// <summary>
/// The read-only reference tabs: missions, equipment, controls, ranks and awards, tips.
/// Everything here comes from <see cref="MissionBook"/>, <see cref="GameFacts"/>,
/// <see cref="RankBook"/> and <see cref="DecorationBook"/>, which in turn come out of the game's
/// own data segment.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    /// <summary>The twelve missions with their briefings, terrain and challenge levels.</summary>
    public IReadOnlyList<MissionInfo> Missions => MissionBook.All;

    /// <summary>Every rank slot, including the two casualty markers.</summary>
    public IReadOnlyList<RankInfo> Ranks => RankBook.All;

    /// <summary>The six decorations.</summary>
    public IReadOnlyList<DecorationInfo> Decorations => DecorationBook.All;

    /// <summary>The supply-pod item table with its weights.</summary>
    public IReadOnlyList<EquipmentInfo> Equipment => GameFacts.Equipment;

    /// <summary>The keyboard controls.</summary>
    public IReadOnlyList<ControlInfo> Controls => GameFacts.Controls;

    /// <summary>The five weapon codes.</summary>
    public IReadOnlyList<WeaponInfo> Weapons => WeaponBook.All;

    /// <summary>The 23 ribbons the manual-lookup copy protection asks about.</summary>
    public IReadOnlyList<string> ProtectionRibbons => GameFacts.ProtectionRibbons;

    /// <summary>Short survival notes.</summary>
    public IReadOnlyList<string> Tips => GameFacts.Tips;

    /// <summary>A one-line note about the supply pod's capacity arithmetic.</summary>
    public string SupplyPodSummary =>
        $"A supply pod holds {GameFacts.SupplyPodCapacity} weight points, and the STANDARD loadout " +
        $"fills it exactly ({GameFacts.StandardLoadWeight}). You may drop " +
        $"{GameFacts.SupplyPodsPerMission} pods during the airdrop — they land where you release them.";

    /// <summary>The mission-area schematic shown on the map tab.</summary>
    public string MapSchematic { get; } = string.Join("\n", new[]
    {
        "  ┌───────────────────────────────────────┐",
        "  │            OBJECTIVE AREA             │   The map is a tall north–south",
        "  │   ▣ ▣ ▣   tents / bunkers / depot     │   corridor. The aircraft crosses it,",
        "  │   ═══════ wire, mines, trenches       │   you jump, and you walk back to the",
        "  │                                       │   Pickup Point marked X.",
        "  │            X  ← Pickup Point          │",
        "  │                                       │   Objects are generated per mission,",
        "  │            NO-MAN'S LAND              │   so there is no fixed map to learn —",
        "  │   ~ ~ ~   scattered cover             │   only the structure and the",
        "  │                                       │   vocabulary of objects.",
        "  │            DROP ZONE                  │",
        "  │   ⊕ pod   ⊕ pod   ⊕ pod               │   Press F9 for the map screen; the",
        "  └───────────────────────────────────────┘   countdown stops while it is up.",
    });
}
