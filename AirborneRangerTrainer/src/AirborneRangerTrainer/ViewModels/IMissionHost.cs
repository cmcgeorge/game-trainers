namespace AirborneRangerTrainer.ViewModels;

/// <summary>
/// What <see cref="MissionViewModel"/> needs from its shell. Keeping it to two calls is what lets
/// the verification harness drive the view-model with no game and no process handle.
/// </summary>
public interface IMissionHost
{
    /// <summary>
    /// Writes <paramref name="bytes"/> at <paramref name="dgroupOffset"/> in the located data
    /// segment. Returns false when the write failed.
    /// </summary>
    bool WriteBytes(int dgroupOffset, byte[] bytes);

    /// <summary>Reports a message for the status bar.</summary>
    void ReportStatus(string message);
}
