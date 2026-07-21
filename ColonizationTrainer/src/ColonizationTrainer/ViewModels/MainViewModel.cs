namespace ColonizationTrainer.ViewModels;

/// <summary>
/// Root view-model: three independent tabs. <see cref="SaveEditor"/> is the verified offline path
/// (edit a COLONYxx.SAV — gold, tax, Founding Fathers, colonies); <see cref="Live"/> is the
/// Cheat-Engine-style value scanner for live edits under DOSBox; <see cref="Reference"/> is the
/// static game-knowledge tables and strategy digest.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    public SaveEditorViewModel SaveEditor { get; } = new();
    public LiveScannerViewModel Live { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    public void Dispose() => Live.Dispose();
}
