using LegendOfGrimrock1Trainer.Game;

namespace LegendOfGrimrock1Trainer.ViewModels;

/// <summary>
/// What a row view-model needs from the session it belongs to.
///
/// The two resolve methods exist because every edit is a read-validate-write: a row never writes
/// through the snapshot it was last drawn from, it asks for a fresh one first. LuaJIT never moves an
/// object, but adding a key to a table rehashes its node array and relocates every value in it, so a
/// slot address is only trustworthy for as long as the read that produced it.
/// </summary>
public interface IGameHost
{
    /// <summary>Whether edits should be applied at all.</summary>
    bool WritesAllowed { get; }

    /// <summary>
    /// Whether a refresh should leave editable values alone because the user is mid-edit.
    ///
    /// <c>UpdateSourceTrigger=LostFocus</c> only defers the write <i>out</i> of a control; a source
    /// <c>PropertyChanged</c> still replaces the text in a box that is being typed into. Grimrock
    /// drains food and counts condition timers down continuously, so without this the four-times-a-
    /// second refresh would clear a half-typed number every time.
    /// </summary>
    bool EditorHasFocus { get; }

    /// <summary>The edit surface, or null when not attached.</summary>
    TrainerActions? Actions { get; }

    /// <summary>Shows a message in the status bar.</summary>
    void Report(string message);

    /// <summary>Reads the party afresh, or null when no game is loaded.</summary>
    PartySnapshot? ResolveParty();

    /// <summary>Reads one champion afresh by party slot, or null.</summary>
    ChampionSnapshot? ResolveChampion(int index);

    /// <summary>Asks the session to rebuild every bound value now, rather than at the next tick.</summary>
    void RequestRefresh();
}
