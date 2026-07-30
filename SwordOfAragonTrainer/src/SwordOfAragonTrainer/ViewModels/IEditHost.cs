namespace SwordOfAragonTrainer.ViewModels;

/// <summary>
/// What a row view-model tells the window when the user changes something: the in-memory save now
/// differs from the file on disk, so the Save button should light up and the status line should say so.
/// </summary>
public interface IEditHost
{
    /// <summary>Marks the loaded save as modified.</summary>
    void MarkDirty(string what);

    /// <summary>
    /// Signals that an edit changed figures in roster slots other than the one edited — which happens
    /// when the player character's class changes, because that is what the purchase discounts key off.
    /// The host re-reads every roster row so the grid cannot show costs computed for the old class.
    /// </summary>
    void NotifyRosterRecomputed();
}
