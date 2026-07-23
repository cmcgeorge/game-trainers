namespace RailroadTycoonTrainer.Game;

/// <summary>Which map's engine roster a locomotive belongs to.</summary>
public enum LocoRegion { UnitedStates, England, Europe }

/// <summary>
/// One locomotive as the game presents it: wheel arrangement + name, display speed (mph), horsepower,
/// price ($), and introduction year. The intro year is jittered ±4 at game start and an engine stays
/// buyable for roughly 50 years after it appears.
/// </summary>
public sealed record Locomotive(
    LocoRegion Region, string Name, int SpeedMph, int Horsepower, int Price, int Year);

/// <summary>
/// The locomotive rosters for the three engine sets (US, England, Continental Europe). This doubles as
/// the answer key to the startup copy-protection quiz ("IDENTIFY THIS LOCOMOTIVE"): the picture is one
/// of these engines and the multiple-choice list is drawn from the same names. Values transcribed from
/// the manual's roster and cross-checked against the community remake's <c>train_engine.ml</c>.
/// </summary>
public static class LocomotiveBook
{
    public static readonly IReadOnlyList<Locomotive> UnitedStates = new[]
    {
        new Locomotive(LocoRegion.UnitedStates, "0-4-0 Grasshopper",   20,  500, 10_000, 1820),
        new Locomotive(LocoRegion.UnitedStates, "4-2-0 Norris",        30, 1000, 20_000, 1833),
        new Locomotive(LocoRegion.UnitedStates, "4-4-0 American",      40, 1500, 30_000, 1848),
        new Locomotive(LocoRegion.UnitedStates, "2-6-0 Mogul",         25, 2000, 30_000, 1851),
        new Locomotive(LocoRegion.UnitedStates, "4-6-0 Ten-Wheeler",   45, 2000, 40_000, 1868),
        new Locomotive(LocoRegion.UnitedStates, "2-8-0 Consolidation", 40, 2500, 40_000, 1877),
        new Locomotive(LocoRegion.UnitedStates, "4-6-2 Pacific",       60, 3500, 60_000, 1892),
        new Locomotive(LocoRegion.UnitedStates, "2-8-2 Mikado",        45, 3500, 50_000, 1903),
        new Locomotive(LocoRegion.UnitedStates, "2-6-6-2 Mallet",      50, 4500, 70_000, 1911),
        new Locomotive(LocoRegion.UnitedStates, "'F' Series Diesel",   70, 3500, 75_000, 1916),
        new Locomotive(LocoRegion.UnitedStates, "'GP' Series Diesel",  60, 4000, 75_000, 1930),
    };

    public static readonly IReadOnlyList<Locomotive> England = new[]
    {
        new Locomotive(LocoRegion.England, "2-2-0 Planet",           20,  500, 10_000, 1820),
        new Locomotive(LocoRegion.England, "2-2-2 Patentee",         30, 1000, 20_000, 1835),
        new Locomotive(LocoRegion.England, "4-2-2 Iron Duke",        40, 1500, 30_000, 1845),
        new Locomotive(LocoRegion.England, "0-6-0 DX Goods",         25, 2500, 30_000, 1855),
        new Locomotive(LocoRegion.England, "4-2-2 Stirling",         45, 2000, 40_000, 1870),
        new Locomotive(LocoRegion.England, "4-2-2 Spinner",          50, 2500, 50_000, 1880),
        new Locomotive(LocoRegion.England, "0-8-0 Webb Compound",    40, 3000, 50_000, 1890),
        new Locomotive(LocoRegion.England, "4-4-0 Hamilton",         60, 2500, 60_000, 1900),
        new Locomotive(LocoRegion.England, "4-6-2 Gresley (A1)",     50, 4000, 60_000, 1920),
        new Locomotive(LocoRegion.England, "4-6-2 Class A4",         70, 3000, 70_000, 1930),
    };

    public static readonly IReadOnlyList<Locomotive> Europe = new[]
    {
        new Locomotive(LocoRegion.Europe, "0-8-0 Compound",         40, 3000,  50_000, 1880),
        new Locomotive(LocoRegion.Europe, "4-4-0 Hamilton",         60, 2500,  60_000, 1890),
        new Locomotive(LocoRegion.Europe, "4-6-2 Gresley",          45, 4000,  60_000, 1905),
        new Locomotive(LocoRegion.Europe, "4-6-2 Class A4",         70, 3500,  70_000, 1915),
        new Locomotive(LocoRegion.Europe, "6/6 Crocodile",          40, 5000,  50_000, 1925),
        new Locomotive(LocoRegion.Europe, "Class E18 1-D-1",        80, 5000,  80_000, 1935),
        new Locomotive(LocoRegion.Europe, "4-8-4 242 A1",           60, 6000,  70_000, 1945),
        new Locomotive(LocoRegion.Europe, "V200 B-B",               90, 6000, 100_000, 1955),
        new Locomotive(LocoRegion.Europe, "Re 6/6 B-B-B",           70, 7000, 100_000, 1968),
        new Locomotive(LocoRegion.Europe, "TGV",                   160, 8000, 150_000, 1978),
    };

    /// <summary>Every locomotive across all three rosters, in region then intro-year order.</summary>
    public static readonly IReadOnlyList<Locomotive> All =
        UnitedStates.Concat(England).Concat(Europe).ToArray();

    public static IReadOnlyList<Locomotive> ForRegion(LocoRegion region) => region switch
    {
        LocoRegion.England => England,
        LocoRegion.Europe => Europe,
        _ => UnitedStates,
    };
}
