namespace JumpmanLivesTrainer.ViewModels;

/// <summary>
/// What <see cref="PlayerViewModel"/> needs from its shell. Keeping it to two calls is what lets
/// the verification harness drive the view-model with no game and no process handle.
/// </summary>
public interface IGameHost
{
    /// <summary>Writes <paramref name="bytes"/> at <paramref name="dgroupOffset"/> in the located data segment.</summary>
    bool WriteBytes(int dgroupOffset, byte[] bytes);

    /// <summary>Reports a message for the status bar.</summary>
    void ReportStatus(string message);
}
