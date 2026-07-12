namespace WarOfTheLanceTrainer.ViewModels;

/// <summary>The write channel a <see cref="UnitStrengthViewModel"/> uses to push a byte to RAM.</summary>
public interface IStrengthHost
{
    /// <summary>Writes a single strength byte at the given absolute address. Returns true on success.</summary>
    bool WriteStrength(nuint address, byte value);

    /// <summary>Reports that a write for the named unit failed, so the host can surface it.</summary>
    void ReportWriteFailure(string unitLabel);
}
