using BattleTech1Trainer.Game;
using GameTrainers.Common.Memory;

namespace BattleTech1Trainer.Memory;

/// <summary>The outcome of a signature scan: whether a signature was found, which one, and where.</summary>
public readonly record struct DetectResult(bool Found, string Signature, nuint Address);

/// <summary>
/// Confirms BattleTech is present in an attached process by scanning its memory for the verbatim
/// <see cref="GameSignatures"/> byte runs. This is a <b>presence check</b>, not a state locator — it
/// tells the user they attached to the right process before they start a value scan; it does not
/// resolve the address of any editable value (the mutable state is not adjacent to these static
/// strings; see <c>.docs/ReverseEngineering.md</c> §5.2).
/// </summary>
public static class GameDetector
{
    /// <summary>
    /// Scans <paramref name="mem"/> for each signature in order and returns the first hit, or a
    /// not-found result. Detection stops at the first matching signature so a broad scan stays cheap.
    /// </summary>
    public static DetectResult Detect(ProcessMemory mem, CancellationToken ct = default)
    {
        foreach (var sig in GameSignatures.All)
        {
            ct.ThrowIfCancellationRequested();
            var hit = BytePatternScanner.Find(mem, sig.Pattern, ct);
            if (hit.Addresses.Count > 0)
                return new DetectResult(true, sig.Name, hit.Addresses[0]);
        }
        return new DetectResult(false, "", 0);
    }
}
