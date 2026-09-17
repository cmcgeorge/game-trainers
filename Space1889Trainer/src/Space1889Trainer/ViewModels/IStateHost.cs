using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>
/// What the character and world editors need from whoever owns a state block: the live game (through
/// <see cref="MainViewModel"/>) or a save file (through <see cref="SaveEditorViewModel"/>). One editor
/// implementation serves both.
/// </summary>
public interface IStateHost
{
    /// <summary>The block being edited, or null when there is none.</summary>
    GameState? State { get; }

    /// <summary>False when writes must not be attempted (detached, stale, nothing loaded).</summary>
    bool CanEdit { get; }

    /// <summary>True while the host is repopulating the editors, so their setters do not write back.</summary>
    bool SuppressWriteBack { get; }

    /// <summary>Reports the outcome of an action.</summary>
    void Report(string message);

    /// <summary>Called after every deliberate write (freezes re-seed from it; a save file marks itself dirty).</summary>
    void AfterWrite();
}
