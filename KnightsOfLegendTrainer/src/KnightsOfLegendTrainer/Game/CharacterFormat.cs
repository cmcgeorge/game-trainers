namespace KnightsOfLegendTrainer.Game;

/// <summary>
/// Known details about the Knights of Legend character data format. No game binary or
/// memory dumps were available for static analysis, so the live memory layout is unknown
/// — this trainer uses the value scanner exclusively. What is documented here comes from
/// the game manual and the online community, and every field carries a confidence marker.
///
/// The chardata save file stores one character's complete state. Its total size and full
/// layout are not publicly documented; only the quest status region (offsets 482-487) is
/// confirmed. [Manual]
/// </summary>
internal static class CharacterFormat
{
    /// <summary>Primary statistic names, in the order the manual lists them. [Manual]</summary>
    public static readonly string[] PrimaryStatNames =
    {
        "Strength", "Quickness", "Size", "Health", "Foresight", "Charisma", "Intellect"
    };

    /// <summary>Primary statistic abbreviations. [Manual]</summary>
    public static readonly string[] PrimaryStatAbbr =
    {
        "STR", "QUI", "SIZ", "HEA", "FOR", "CHA", "INT"
    };

    /// <summary>Secondary statistic names. [Manual]</summary>
    public static readonly string[] SecondaryStatNames =
    {
        "Body Points", "Balance", "Endurance"
    };

    /// <summary>Combat attack types (weapon). [Manual]</summary>
    public static readonly string[] WeaponAttackTypes =
    {
        "None", "Berserk", "Hack", "Thrust", "Slash"
    };

    /// <summary>Combat attack types (unarmed). [Manual]</summary>
    public static readonly string[] UnarmedAttackTypes =
    {
        "Kick", "Bash", "Head Butt", "Punch"
    };

    /// <summary>Combat aiming options. [Manual]</summary>
    public static readonly string[] AimOptions =
    {
        "High Shot", "Body Shot", "Low Shot"
    };

    /// <summary>Combat defense options. [Manual]</summary>
    public static readonly string[] DefenseOptions =
    {
        "None", "Panic", "Stand", "Back Up", "Duck", "Dodge", "Jump"
    };

    /// <summary>Combat movement options. [Manual]</summary>
    public static readonly string[] MovementOptions =
    {
        "Walk", "Run", "Sprint", "Fly", "Fly Faster", "Zoom"
    };

    /// <summary>Reads a little-endian 16-bit value.</summary>
    public static int ReadU16(byte[] buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8);

    /// <summary>Writes a little-endian 16-bit value.</summary>
    public static void WriteU16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>Reads a little-endian 32-bit value.</summary>
    public static long ReadU32(byte[] buffer, int offset) =>
        (long)buffer[offset]
        | ((long)buffer[offset + 1] << 8)
        | ((long)buffer[offset + 2] << 16)
        | ((long)buffer[offset + 3] << 24);

    /// <summary>Writes a little-endian 32-bit value.</summary>
    public static void WriteU32(byte[] buffer, int offset, long value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
