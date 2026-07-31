namespace AlternateRealityTrainer.ViewModels;

/// <summary>
/// What a <see cref="CharacterViewModel"/> needs from the shell: a way to push bytes into the
/// attached game, and a way to say something in the status bar.
/// </summary>
public interface ICharacterHost
{
    /// <summary>
    /// Writes <paramref name="bytes"/> into the game at <paramref name="recordAddress"/> +
    /// <paramref name="offset"/>. Returns false when not attached or the write failed.
    /// </summary>
    bool WriteBytes(nuint recordAddress, int offset, byte[] bytes);

    /// <summary>Shows a one-line message in the shell's status bar.</summary>
    void ReportStatus(string message);
}
