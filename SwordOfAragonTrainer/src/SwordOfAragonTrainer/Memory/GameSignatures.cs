using System.Text;

namespace SwordOfAragonTrainer.Memory;

/// <summary>
/// A string constant at a known offset inside the game's data segment.
/// </summary>
/// <param name="Text">The literal's characters, exactly as the executable stores them.</param>
/// <param name="DsOffset">
/// The literal's run-time <c>DS:offset</c>, taken from the 4-byte descriptor
/// (<c>length</c>, <c>dsOffset</c>) that QuickBASIC 3.0 places immediately before every literal.
/// </param>
public sealed record DgroupAnchor(string Text, int DsOffset)
{
    /// <summary>The literal as bytes to scan for.</summary>
    public byte[] Bytes => Encoding.ASCII.GetBytes(Text);
}

/// <summary>
/// Signatures for locating <c>ARAGON.EXE</c>'s data segment (DGROUP) inside a running DOSBox process.
///
/// Compiled QuickBASIC keeps its scalar variables in the same segment as its string literals, and the
/// whole literal pool sits at a single constant offset from the executable's file image — 189 literals
/// in <c>ARAGON.EXE</c> agree on a file→<c>DS</c> delta of −0x3FE0. Finding one literal in guest RAM
/// therefore fixes <c>DS:0000</c> for the whole world-map module:
///
/// <code>dgroupBase = hostAddressOfLiteral - anchor.DsOffset</code>
///
/// The primary anchor is the City Status banner — 38 bytes of distinctive spacing that will not occur
/// by chance. The validators are three further literals whose own offsets must line up relative to it.
/// <see cref="DgroupLocator.MinValidators"/> of them are required, so an accepted hit is at least a
/// three-of-four match; the locator reports how many actually matched.
///
/// The offsets are Confirmed from the executable image. Whether DOSBox lays the guest's segment out
/// contiguously in host memory for the whole 64 KiB (it does for its normal flat guest-RAM buffer) has
/// not been verified against a running game — see <c>docs/RE.md</c> §3.
/// </summary>
public static class GameSignatures
{
    /// <summary>Bytes in a real-mode segment — the size of the window a located DGROUP spans.</summary>
    public const int SegmentSize = 0x1_0000;

    /// <summary>Process-name fragments that suggest a DOSBox build worth attaching to.</summary>
    public static readonly string[] EmulatorHints =
    {
        "dosbox", "dosbox-x", "dosbox-staging", "dosboxx", "dosbox_x",
    };

    /// <summary>
    /// Byte length of <see cref="WorldMapPrimary"/>. Pinned as a constant because the literal's
    /// distinctiveness is its runs of three and four spaces: "tidying" that whitespace would leave the
    /// scanner unable to match anything in guest RAM, and nothing else would notice.
    /// </summary>
    public const int WorldMapPrimaryLength = 38;

    /// <summary>The literal the locator scans for.</summary>
    public static readonly DgroupAnchor WorldMapPrimary =
        new("*****   C I T Y   S T A T U S    *****", 0x90F8);

    /// <summary>Further literals that must appear at their own offsets for a hit to be accepted.</summary>
    public static readonly IReadOnlyList<DgroupAnchor> WorldMapValidators = new DgroupAnchor[]
    {
        new("Population:   ", 0x9146),
        new("Recruit:", 0x91AE),
        new("Wealth:", 0x9298),
    };
}
