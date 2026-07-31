using System.Text;

namespace AirborneRangerTrainer.Game;

/// <summary>One award the game can grant.</summary>
/// <param name="Bit">Its bit in the roster record's decoration mask.</param>
/// <param name="Mnemonic">The mnemonic printed on the roster's ribbon line.</param>
/// <param name="Name">The full award name, from the game's award messages.</param>
public readonly record struct DecorationInfo(int Bit, string Mnemonic, string Name);

/// <summary>
/// The six decorations, read out of the ribbon-line literal at <c>DGROUP:0xBBA6</c> —
/// <c>"COM1 COM2 BSTR SSTR DSC CMH       (CMPN)"</c> — with the full names taken from the game's
/// own award messages at <c>DGROUP:0xD1E1</c> (<c>Commendation 1.</c> …
/// <c>Congressional Medal of Honor.</c>).
///
/// <para>The bit order was confirmed against the shipped roster: three rangers carry the full line
/// and a mask of <c>0x3F</c> (all six bits), one carries only <c>COM1</c> and a mask of <c>0x01</c>,
/// and two carry a blank line and a mask of <c>0x00</c>.</para>
/// </summary>
public static class DecorationBook
{
    /// <summary>Every award, lowest bit first.</summary>
    public static readonly IReadOnlyList<DecorationInfo> All = new[]
    {
        new DecorationInfo(0x01, "COM1", "Army Commendation Medal"),
        new DecorationInfo(0x02, "COM2", "Army Commendation Medal, second award"),
        new DecorationInfo(0x04, "BSTR", "Bronze Star"),
        new DecorationInfo(0x08, "SSTR", "Silver Star"),
        new DecorationInfo(0x10, "DSC",  "Distinguished Service Cross"),
        new DecorationInfo(0x20, "CMH",  "Congressional Medal of Honor"),
    };

    /// <summary>Every decoration bit set.</summary>
    public const int AllMask = 0x3F;

    /// <summary>
    /// The campaign marker. It has no bit in the mask — the game records it only as this text on the
    /// ribbon line — so it is carried separately everywhere it appears.
    /// </summary>
    public const string CampaignMarker = "(CMPN)";

    /// <summary>
    /// Renders the ribbon line exactly as the game stores it: each earned mnemonic in its own fixed
    /// column, blanked where not earned, and the campaign marker right-aligned in the field.
    /// </summary>
    public static string RenderLine(int mask, bool campaign)
    {
        var sb = new StringBuilder(RosterFormat.DecorationLineLength);
        foreach (var d in All)
        {
            sb.Append((mask & d.Bit) != 0 ? d.Mnemonic : new string(' ', d.Mnemonic.Length));
            sb.Append(' ');
        }
        // The six mnemonics plus their separators occupy 28 columns; the marker fills the last six.
        while (sb.Length < RosterFormat.DecorationLineLength - CampaignMarker.Length) sb.Append(' ');
        sb.Length = RosterFormat.DecorationLineLength - CampaignMarker.Length;
        sb.Append(campaign ? CampaignMarker : new string(' ', CampaignMarker.Length));
        return sb.ToString();
    }
}
