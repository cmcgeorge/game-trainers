namespace KeefTrainer.Core;

/// <summary>
/// Memory map of Keef the Thief (KF.EXE, DOS, 1989) inside a DOSBox / DOSBox-X process.
///
/// All game state lives in the EXE's static data segments, so every address is a fixed
/// delta from one anchor: the character stat table (Ghidra segment 36ed, "DSEG").
/// The table is an array of 17-byte entries { int16 value; char label[15]; } holding
/// the AUTHORITATIVE live stats (combat / level-up / character-roll code in KF.EXE
/// reads and writes these directly; the 2000-byte save file is staged from them).
///
/// The anchor is located at runtime by scanning guest RAM for the label signature:
///   DSEG+0x13 "Strength:"  DSEG+0x24 "Speed:"  DSEG+0x35 "Constitution:"
/// (labels are NUL-padded char[15] fields, values in the 2 bytes preceding each label).
/// </summary>
public enum KeefField
{
    HiddenStat,
    Strength,
    Speed,
    Constitution,
    Wisdom,
    Luck,
    Charisma,
    Disarming,
    Stealing,
    Unlocking,
    Nutrition,
    Sobriety,
    Sleep,
    Gold,
    MagicPoints,
    HitPoints,
    Level,
    WeaponStrength,
    WeaponSpeed,
    WeaponRange,
    ArmorStrength,
    ArmorSpeed,
    Flints,
    Experience,
    LockPicks,
}

public sealed record KeefFieldInfo(KeefField Field, int Delta, bool Is32Bit, int Min, int Max);

public static class KeefMap
{
    // Signature: "Strength:" + NUL padding to char[15], then value word, then "Speed:" etc.
    // Byte 0 of the signature sits at DSEG + 0x13.
    public const int SignatureToDseg = 0x13;

    public static readonly byte[][] SignatureParts =
    {
        Pad("Strength:"),      // at DSEG+0x13
        Pad("Speed:"),         // at DSEG+0x24  (= +0x11 from part 0)
        Pad("Constitution:"),  // at DSEG+0x35  (= +0x22 from part 0)
    };

    public static readonly int[] SignaturePartOffsets = { 0x00, 0x11, 0x22 };

    private static byte[] Pad(string s)
    {
        var b = new byte[15];
        System.Text.Encoding.ASCII.GetBytes(s, 0, s.Length, b, 0);
        return b;
    }

    /// <summary>Bytes to read from DSEG to cover every field in one call.</summary>
    public const int SnapshotSize = 0x7EC0;

    // Verified against a live DOSBox-X memory dump (2026-07-01) and Ghidra decompilation
    // of KF.EXE (character roll FUN_1862_068a, status screen FUN_18d8_02fa, abilities
    // screen FUN_18d8_05f2, level-up FUN_25b4_11b6, save FUN_17e6_012b).
    public static readonly KeefFieldInfo[] Fields =
    {
        // Character stat table: DSEG + 0x11 * k. Con doubles as Max HP, Wis as Max MP.
        new(KeefField.HiddenStat,     0x000, false, 0, 100),
        new(KeefField.Strength,       0x011, false, 0, 100),
        new(KeefField.Speed,          0x022, false, 0, 100),
        new(KeefField.Constitution,   0x033, false, 0, 999),
        new(KeefField.Wisdom,         0x044, false, 0, 999),
        new(KeefField.Luck,           0x055, false, 0, 100),
        new(KeefField.Charisma,       0x066, false, 0, 100),
        new(KeefField.Disarming,      0x077, false, 0, 100),
        new(KeefField.Stealing,       0x088, false, 0, 100),
        new(KeefField.Unlocking,      0x099, false, 0, 100),
        new(KeefField.Nutrition,      0x0AA, false, 0, 100),
        new(KeefField.Sobriety,       0x0BB, false, 0, 100),
        new(KeefField.Sleep,          0x0CC, false, 0, 100),
        new(KeefField.Gold,           0x0DD, false, 0, 9999),   // game clamps loot at 9999
        new(KeefField.MagicPoints,    0x0EE, false, 0, 999),
        new(KeefField.HitPoints,      0x0FF, false, 0, 999),    // combat clamps at 999
        new(KeefField.Level,          0x110, false, 1, 24),     // level-up stops at 0x18
        // Equipped weapon / armor stats (copied from item tables when equipping).
        new(KeefField.WeaponStrength, 0x5A6F, false, 0, 100),
        new(KeefField.WeaponSpeed,    0x5A71, false, 0, 100),
        new(KeefField.WeaponRange,    0x5A73, false, 0, 10),    // displayed as value * 6 ft
        new(KeefField.ArmorStrength,  0x5A75, false, 0, 100),
        new(KeefField.ArmorSpeed,     0x5A77, false, 0, 100),
        // Consumables + progression.
        new(KeefField.Flints,         0x5C9D, false, 0, 999),
        new(KeefField.Experience,     0x611C, true,  0, 1_000_000),
        new(KeefField.LockPicks,      0x7EB5, false, 0, 999),
    };

    private static readonly Dictionary<KeefField, KeefFieldInfo> InfoByField =
        Fields.ToDictionary(i => i.Field);

    public static KeefFieldInfo Info(KeefField f) => InfoByField[f];
}
