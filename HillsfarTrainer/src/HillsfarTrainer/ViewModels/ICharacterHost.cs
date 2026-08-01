namespace HillsfarTrainer.ViewModels;

/// <summary>
/// What <see cref="CharacterViewModel"/> needs from its shell. Keeping it to two calls is what lets
/// the verification harness drive the view-model with no game and no process handle.
/// </summary>
public interface ICharacterHost
{
    /// <summary>
    /// Writes <paramref name="bytes"/> at <paramref name="dgroupOffset"/> inside the data segment at
    /// <paramref name="dgroupBase"/>. Returns false when the write failed.
    /// </summary>
    /// <param name="dgroupBase">
    /// Live address of <c>DGROUP:0000</c>, supplied by the caller. The host must not substitute its
    /// own idea of the current address: after a re-locate the two can differ, and a write aimed at the
    /// old segment must not land in the new one.
    /// </param>
    /// <param name="dgroupOffset">Offset of the first byte within the data segment.</param>
    /// <param name="bytes">The bytes to write.</param>
    bool WriteBytes(nuint dgroupBase, int dgroupOffset, byte[] bytes);

    /// <summary>Reports a message for the status bar.</summary>
    void ReportStatus(string message);
}
