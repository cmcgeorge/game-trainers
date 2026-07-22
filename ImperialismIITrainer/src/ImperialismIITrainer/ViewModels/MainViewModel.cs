namespace ImperialismIITrainer.ViewModels;

/// <summary>
/// Root view-model: two tabs. <see cref="Live"/> attaches to the native <c>Imperialism II.exe</c> and
/// edits/freezes treasury and warehouse resources — one-click auto-locate (a static-global pointer
/// chain, no ASLR) with a Cheat-Engine-style value scanner as the fallback; <see cref="Reference"/> is
/// the static game-knowledge (commodities, the game's own cheat surface, and how-to / RE notes). There
/// is no offline save editor: the .imp save is a later-build MFC serialization with no matching map, so
/// live memory is the safe, verifiable path.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    public LiveScannerViewModel Live { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    public void Dispose() => Live.Dispose();
}
