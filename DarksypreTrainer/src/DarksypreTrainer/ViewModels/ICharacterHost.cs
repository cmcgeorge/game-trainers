namespace DarksypreTrainer.ViewModels;

/// <summary>
/// The write channel a <see cref="CharacterViewModel"/> uses to push edits back to the live
/// game. Implemented by <see cref="MainViewModel"/> over the attached process.
/// </summary>
public interface ICharacterHost
{
    /// <summary>Whether a target process is currently open.</summary>
    bool IsAttached { get; }

    /// <summary>
    /// Writes <paramref name="length"/> bytes taken from <paramref name="source"/> at
    /// <paramref name="offset"/> to <paramref name="structureAddress"/> + <paramref name="offset"/>.
    /// The offset applies to both sides, so callers pass the address of the whole structure and the
    /// offset of the field within it — the same shape as
    /// <see cref="ProcessMemory.WriteRange"/>.
    /// </summary>
    bool WriteBytes(nuint structureAddress, byte[] source, int offset, int length);
}
