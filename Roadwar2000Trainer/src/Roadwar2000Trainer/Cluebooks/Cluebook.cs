using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeVehicles { get; init; } = true;
    public bool IncludeCities { get; init; } = true;
    public bool IncludeMaps { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<VehicleType> Vehicles { get; init; }
    public required IReadOnlyList<CityInfo> Cities { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Vehicles = VehicleBook.All,
            Cities = CityBook.All,
        };
}
