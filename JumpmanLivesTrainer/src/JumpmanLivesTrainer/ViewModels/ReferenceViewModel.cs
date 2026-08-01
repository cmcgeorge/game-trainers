using System.Collections.ObjectModel;
using JumpmanLivesTrainer.Game;

namespace JumpmanLivesTrainer.ViewModels;

/// <summary>
/// The reference tab: displays controls, level list, and tips read-only.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    /// <summary>The keyboard controls from <see cref="GameFacts.Controls"/>.</summary>
    public ObservableCollection<ControlInfo> Controls { get; } = new();

    /// <summary>The 45 levels from <see cref="GameFacts.Levels"/>.</summary>
    public ObservableCollection<LevelInfo> Levels { get; } = new();

    /// <summary>The tips from <see cref="GameFacts.Tips"/>.</summary>
    public ObservableCollection<string> Tips { get; } = new();

    /// <summary>The game title for the header.</summary>
    public string Title => GameFacts.GameTitle;

    /// <summary>The publisher line.</summary>
    public string Publisher => GameFacts.Publisher;

    public ReferenceViewModel()
    {
        foreach (var c in GameFacts.Controls) Controls.Add(c);
        foreach (var l in GameFacts.Levels) Levels.Add(l);
        foreach (var t in GameFacts.Tips) Tips.Add(t);
    }
}
