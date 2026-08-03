namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// The read/write channel a <see cref="CharacterViewModel"/> uses to reach the live game.
/// Implemented by <see cref="MainViewModel"/> over the attached process.
/// </summary>
public interface ICharacterHost
{
    bool IsAttached { get; }

    /// <summary>Writes <paramref name="length"/> bytes of <paramref name="source"/> at <paramref name="offset"/>.</summary>
    bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length);

    /// <summary>Reads <paramref name="length"/> bytes into <paramref name="destination"/>.</summary>
    bool ReadBytes(nuint address, byte[] destination, int length);
}
