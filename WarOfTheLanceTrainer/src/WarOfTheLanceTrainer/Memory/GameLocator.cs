using WarOfTheLanceTrainer.Game;

namespace WarOfTheLanceTrainer.Memory;

/// <summary>The result of locating War of the Lance state in a running emulator.</summary>
public sealed class LocatedState
{
    /// <summary>Address of the NAT.DAT nation-name table image in guest RAM, or 0 if not found.</summary>
    public nuint NationTableAddress { get; init; }

    /// <summary>Address of the first current-strength cell of the WL.DAT working buffer, or 0.</summary>
    public nuint StrengthBlockAddress { get; init; }

    /// <summary>The strength cells read at location time (<see cref="StrengthTable.Count"/> bytes).</summary>
    public byte[] StrengthCells { get; init; } = Array.Empty<byte>();

    public bool NationTableFound => NationTableAddress != 0;
    public bool StrengthBlockFound => StrengthBlockAddress != 0;
    public bool AnythingFound => NationTableFound || StrengthBlockFound;
}

/// <summary>
/// Finds War of the Lance's live data in the emulator's guest RAM by scanning for byte signatures
/// the engine loads verbatim — addresses are discovered every session, never hard-coded.
///
/// <para>Two independent anchors are used. The <b>nation-name table</b> (NAT.DAT, high-bit ASCII)
/// is byte-identical to the shipped file and confirms the game is loaded. The <b>unit
/// current-strength block</b> is located by anchoring on the constant qualities/base-number run
/// that follows it (<see cref="StrengthTable.Signature"/>) and stepping back by a fixed delta —
/// the same anchor+delta approach the Dragon Wars trainer uses for its roster.</para>
/// </summary>
public static class GameLocator
{
    /// <summary>How many leading nation names to weld into the NAT.DAT anchor signature.</summary>
    private const int NationAnchorWords = 6;

    /// <summary>Builds the high-bit-ASCII signature for the head of the NAT.DAT table.</summary>
    public static byte[] BuildNationSignature()
    {
        var bytes = new List<byte>();
        for (int i = 0; i < NationAnchorWords && i < GameFacts.NationNames.Length; i++)
        {
            bytes.AddRange(GameText.Encode(GameFacts.NationNames[i]));
            bytes.Add(GameText.Separator);
        }
        return bytes.ToArray();
    }

    /// <summary>
    /// Locates whatever WOTL state can be found in the attached process. The nation-name table is
    /// the "the game is really loaded" gate: unless it is present, no strength block is returned —
    /// this stops a coincidental signature match in an unrelated process from producing editable
    /// rows that would write into arbitrary memory.
    /// </summary>
    public static LocatedState Locate(ProcessMemory mem, CancellationToken ct = default)
    {
        nuint nationAddr = FindNationTable(mem, ct);
        if (nationAddr == 0)
            return new LocatedState();

        var (strengthAddr, cells) = FindStrengthBlock(mem, ct);
        return new LocatedState
        {
            NationTableAddress = nationAddr,
            StrengthBlockAddress = strengthAddr,
            StrengthCells = cells,
        };
    }

    private static nuint FindNationTable(ProcessMemory mem, CancellationToken ct)
    {
        var sig = BuildNationSignature();
        var hits = BytePatternScanner.Find(mem, sig, ct).Addresses;
        return hits.Count > 0 ? hits[0] : 0;
    }

    private static (nuint address, byte[] cells) FindStrengthBlock(ProcessMemory mem, CancellationToken ct)
    {
        var hits = BytePatternScanner.Find(mem, StrengthTable.Signature, ct).Addresses;
        foreach (var sigAddr in hits)
        {
            ct.ThrowIfCancellationRequested();
            nuint blockAddr = unchecked(sigAddr + (nuint)(nint)StrengthTable.SignatureToBlockDelta);
            var cells = mem.Read(blockAddr, StrengthTable.Count);
            if (cells.Length == StrengthTable.Count && LooksLikeStrengthBlock(cells))
                return (blockAddr, cells);
        }
        return (0, Array.Empty<byte>());
    }

    /// <summary>
    /// A live strength block is 29 bytes, each no greater than the engine ceiling (240), with at
    /// least one non-zero cell. A single destroyed unit can legitimately read 0 mid-campaign, so a
    /// zero cell is allowed; an all-zero run (never a live army) and any &gt;240 byte are rejected,
    /// which — together with the constant signature anchor and the required nation table — screens
    /// out coincidental matches.
    /// </summary>
    private static bool LooksLikeStrengthBlock(byte[] cells)
    {
        bool anyNonZero = false;
        foreach (byte b in cells)
        {
            if (b > GameFacts.MaxStrength) return false;
            if (b != 0) anyNonZero = true;
        }
        return anyNonZero;
    }
}
