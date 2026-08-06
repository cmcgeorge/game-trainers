using TheQuestTrainer.Game;

namespace TheQuestTrainer.ViewModels;

/// <summary>
/// What a row needs from the session in order to write, ask questions and report.
///
/// It is an interface so the rows can be exercised without a live game — and so
/// <see cref="EditorHasFocus"/> can be a <i>probe</i> rather than a flag. A flag tracked from
/// keyboard-focus events latches on forever when the focused editor is destroyed rather than
/// blurred (rebuilding the skill list does exactly that), and clearing it when focus leaves the
/// application throws away a half-typed value on alt-tab. The window answers this from
/// <c>FocusManager</c>'s logical focus instead, which has neither problem.
/// </summary>
public interface IGameHost
{
    /// <summary>Whether a validated character record is currently held.</summary>
    bool IsAttached { get; }

    /// <summary>Whether writes are disabled by the safety catch.</summary>
    bool IsReadOnly { get; }

    /// <summary>Whether a text editor in the window currently has logical focus.</summary>
    bool EditorHasFocus { get; }

    /// <summary>Writes a base attribute.</summary>
    ActionResult WriteAttribute(int id, int value);

    /// <summary>Writes a base skill.</summary>
    ActionResult WriteSkill(int id, int value);

    /// <summary>
    /// Writes the one mutable word of the carried item at <paramref name="item"/> — its condition,
    /// its wand charges or its ammunition count, depending on what kind of item it is.
    ///
    /// The item is named by <i>address</i> rather than by its position in the pack, because the pack
    /// closes up when the player drops or sells something and a position captured when the row was
    /// drawn can name a different item a tick later.
    /// </summary>
    ActionResult WriteItemMeter(uint item, int value);

    /// <summary>Fills that word to its maximum: repairs, recharges or refills the item.</summary>
    ActionResult RestoreItem(uint item);

    /// <summary>
    /// Moves the player to a tile of the map they are already standing on.
    ///
    /// The coordinates are <i>map-local</i> — what the Map tab shows — not the window indices the
    /// engine actually holds; converting between the two needs the current map's flags, so it is done
    /// against a position read at the moment of the write rather than against the one on screen.
    /// </summary>
    ActionResult Teleport(int localX, int localY);

    /// <summary>Shows a line in the status bar.</summary>
    void Report(string message);
}
