namespace RailroadTycoonTrainer.ViewModels;

/// <summary>
/// Root view-model: two tabs. <see cref="Live"/> attaches to the DOSBox process running Railroad Tycoon
/// and edits/freezes the player's cash — one-click auto-locate (find the data segment by its label
/// strings, read the cash word) with a Cheat-Engine-style value scanner as the fallback;
/// <see cref="Reference"/> is the static game-knowledge (locomotives, stations, scenarios, difficulty,
/// the copy-protection answer key, and how-to / RE notes). There is no offline save editor: the
/// <c>.SVE</c> save is a multi-region serialization whose cash offset was not independently confirmed,
/// so live memory is the safe, verifiable path.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    public LiveScannerViewModel Live { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    public void Dispose() => Live.Dispose();
}
