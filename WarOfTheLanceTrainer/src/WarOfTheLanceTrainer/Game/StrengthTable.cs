namespace WarOfTheLanceTrainer.Game;

/// <summary>
/// One named entry in the leading current-strength block of the WL.DAT / SCEN.DAT working buffer.
/// </summary>
public readonly record struct StrengthEntry(int Index, string Side, string Nation, string UnitType, byte BaseNumber);

/// <summary>
/// Models the leading <b>current-strength block</b> of the WL.DAT/SCEN.DAT working buffer and the
/// byte signature used to find that buffer in the emulator's guest RAM.
///
/// <para>Verified from the shipped files: WL.DAT's payload opens with one byte of <i>current
/// strength</i> per unit for the Highlord assault force and Neraka's core (draconians then Neraka
/// mercenaries). In the CAMPAIGN start every unit is at its base number, so the block reads
/// 9×200, 10×150, 8×200, 2×150; in SCEN.DAT the same 29 cells are already battle-worn (varied),
/// which is how the block was identified as <i>current</i> strength rather than a constant table.</para>
///
/// <para>Immediately after the 29 current-strength cells comes a short, constant qualities/base-number
/// run (<c>3,3,3,3,4,4,4,5,110,110,110,110,20,20,20,20,110,110,110,110,20,20,20,20</c>) that is
/// identical in WL.DAT and SCEN.DAT. That run is used as the locator signature because it does not
/// change as the game is played; the editable current-strength block sits at a fixed negative delta
/// in front of it. This anchor+delta scheme mirrors the sibling Dragon Wars trainer.</para>
/// </summary>
public static class StrengthTable
{
    /// <summary>Number of contiguous current-strength cells at the head of the buffer.</summary>
    public const int Count = 29;

    /// <summary>
    /// Constant qualities/base-number run that follows the current-strength block. Distinctive enough
    /// to anchor a scan and, unlike the strength cells, invariant across play.
    /// </summary>
    public static readonly byte[] Signature =
    {
        3, 3, 3, 3, 4, 4, 4, 5,
        110, 110, 110, 110, 20, 20, 20, 20,
        110, 110, 110, 110, 20, 20, 20, 20,
    };

    /// <summary>Bytes from the located signature back to the first current-strength cell.</summary>
    public const int SignatureToBlockDelta = -Count;

    /// <summary>
    /// The 29 leading units, in buffer order, labelled from the manual's unit appendix. The base
    /// number is the campaign-start (full) strength; live current strength is read from RAM.
    /// </summary>
    public static readonly StrengthEntry[] Entries = BuildEntries();

    private static StrengthEntry[] BuildEntries()
    {
        var list = new List<StrengthEntry>(Count);
        void Add(int n, string side, string nation, string type, byte baseNo)
        {
            for (int i = 0; i < n; i++)
                list.Add(new StrengthEntry(list.Count, side, nation, type, baseNo));
        }

        // Highlord assault force: 9 Baaz draconians @200, 10 Kapak draconians @150.
        Add(9, "HIGHLORD", "HIGHLORD", "BAAZ DRACONIAN", 200);
        Add(10, "HIGHLORD", "HIGHLORD", "KAPAK DRACONIAN", 150);
        // Neraka core: 8 mercenary infantry @200, 2 mercenary cavalry @150.
        Add(8, "HIGHLORD", "NERAKA", "MERC INFANTRY", 200);
        Add(2, "HIGHLORD", "NERAKA", "MERC CAVALRY", 150);

        return list.ToArray();
    }
}
