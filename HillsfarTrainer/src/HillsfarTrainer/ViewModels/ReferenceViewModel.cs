using System.Collections.ObjectModel;
using HillsfarTrainer.Game;

namespace HillsfarTrainer.ViewModels;

/// <summary>One row of the opening-hours table, with whether it is open at the chosen hour.</summary>
public sealed class LocationRow : ObservableObject
{
    /// <summary>The location.</summary>
    public LocationInfo Info { get; }

    /// <summary>Name as the game spells it.</summary>
    public string Name => Info.Name;

    /// <summary>Hours as the manual prints them.</summary>
    public string Hours => Info.Hours;

    /// <summary>Why you go there.</summary>
    public string Note => Info.Note;

    private bool _isOpen;

    /// <summary>True when the location is open at the currently-selected hour.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => SetField(ref _isOpen, value);
    }

    /// <summary>Builds a row.</summary>
    public LocationRow(LocationInfo info) => Info = info;
}

/// <summary>
/// The read-only reference tabs: opening hours against a chosen hour of day, the arena roster and
/// its tells, the overland map, the controls, and play notes.
///
/// <para>Everything here is a table recovered from the game or the manual. Nothing on these tabs
/// touches the game, so they work with no process attached.</para>
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    /// <summary>The eighteen city locations and their hours.</summary>
    public ObservableCollection<LocationRow> Locations { get; } = new();

    /// <summary>The arena roster with the tell that beats each opponent.</summary>
    public IReadOnlyList<ArenaOpponent> Opponents => ArenaBook.Opponents;

    /// <summary>Which missions require a named opponent beaten.</summary>
    public IReadOnlyList<MissionGate> MissionGates => ArenaBook.MissionGates;

    /// <summary>Overland destinations and how each is reached.</summary>
    public IReadOnlyList<OverlandInfo> Overland => LocationBook.Overland;

    /// <summary>The keyboard controls.</summary>
    public IReadOnlyList<ControlInfo> Controls => GameFacts.Controls;

    /// <summary>Play notes.</summary>
    public IReadOnlyList<string> Tips => GameFacts.Tips;

    /// <summary>The four named pubs.</summary>
    public IReadOnlyList<string> Pubs => LocationBook.Pubs;

    /// <summary>The class combinations the game allows.</summary>
    public IReadOnlyList<ClassInfo> Classes => ClassBook.Classes;

    /// <summary>A short note on how the game keeps time and heals.</summary>
    public string ClockNote =>
        $"One game hour costs {GameFacts.RealSecondsPerGameHour} real seconds, so a game day is about "
        + $"{GameFacts.RealMinutesPerGameDay} real minutes. "
        + "Natural healing is 1 + clamp(Constitution − 14, 0, 5) hit points per game day. "
        + "Both figures come from the game's own clock-tick routine.";

    /// <summary>Builds the tabs and applies the default hour.</summary>
    public ReferenceViewModel()
    {
        foreach (var l in LocationBook.Locations) Locations.Add(new LocationRow(l));
        ApplyHour();
    }

    private int _hour = 9;

    /// <summary>The hour of day, 1..24, that the opening-hours table is evaluated against.</summary>
    public int Hour
    {
        get => _hour;
        set
        {
            if (!SetField(ref _hour, Math.Clamp(value, 1, CharacterFormat.HoursPerDay))) return;
            OnPropertyChanged(nameof(HourText));
            ApplyHour();
        }
    }

    /// <summary>The chosen hour as the game would print it.</summary>
    public string HourText => GameFacts.FormatHour(Hour);

    private void ApplyHour()
    {
        foreach (var row in Locations) row.IsOpen = row.Info.IsOpenAt(Hour);
    }
}
