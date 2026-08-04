namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// The channel a located-entity row (player, city, unit) uses to reach the game.
///
/// Separate from <see cref="IScanHost"/> because the two have different jobs: the scanner deals in
/// anonymous addresses and widths, whereas these rows know exactly which field they are touching and
/// need the host to be able to refuse a write outright — in multiplayer, or when the trainer has
/// detached mid-edit.
/// </summary>
public interface IGameHost
{
    /// <summary>False when the trainer is detached, or the game reports a PBEM/offline-MP session.</summary>
    bool WritesAllowed { get; }

    /// <summary>Reads <paramref name="count"/> bytes; returns a shorter array on failure.</summary>
    byte[] Read(nuint address, int count);

    /// <summary>Reads a little-endian signed 32-bit value.</summary>
    bool ReadInt32(nuint address, out int value);

    /// <summary>Writes a little-endian signed 32-bit value, honouring <see cref="WritesAllowed"/>.</summary>
    bool WriteInt32(nuint address, int value);

    /// <summary>Surfaces a message in the shell's status bar.</summary>
    void Report(string message);
}
