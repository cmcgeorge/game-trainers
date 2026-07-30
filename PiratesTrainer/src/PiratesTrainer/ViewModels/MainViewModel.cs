namespace PiratesTrainer.ViewModels;

/// <summary>
/// Root view-model: two tabs. <see cref="Live"/> attaches to the DOSBox process running Pirates! and
/// edits/freezes the player's state — one-click auto-locate (find the data segment by three static
/// literals, pin gold, crew, estate and the game clock, and list the live settlement table) with a
/// Cheat-Engine-style value scanner as the fallback. <see cref="Reference"/> is the static game
/// knowledge: settlement tables and convoy itineraries decoded from <c>DISK1</c> (the itineraries are
/// the manual's copy-protection answer key), the ship/goods/rank/speciality tables, the controls, and
/// how-to notes.
///
/// There is no offline save editor. The save disk (<c>DISKS</c>) is a raw floppy image the game reaches
/// through <c>PIR.EXE</c>'s INT 80h shim, and the shipped copy in the target folder is unformatted, so
/// the on-disk slot directory could not be validated against a real save — live memory is the verifiable
/// path.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    public LiveScannerViewModel Live { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    public void Dispose() => Live.Dispose();
}
