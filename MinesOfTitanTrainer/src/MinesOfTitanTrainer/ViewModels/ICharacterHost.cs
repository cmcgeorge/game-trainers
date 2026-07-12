namespace MinesOfTitanTrainer.ViewModels;

/// <summary>
/// The write channel a <see cref="CharacterViewModel"/> uses to push edits back to the live game.
/// Implemented by <see cref="MainViewModel"/> over the attached process.
/// </summary>
public interface ICharacterHost
{
    bool IsAttached { get; }
    bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length);
}
