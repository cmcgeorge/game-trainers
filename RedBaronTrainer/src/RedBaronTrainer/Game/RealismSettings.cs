namespace RedBaronTrainer.Game;

/// <summary>How a realism setting is presented: a tick box, or a three-way selector.</summary>
public enum RealismKind
{
    /// <summary>0 = off, 1 = on.</summary>
    Toggle,

    /// <summary>Combat level: 0 = Easy, 1 = Standard, 2 = Hard.</summary>
    CombatLevel,

    /// <summary>Flight model: 0 = Novice, 1 = Intermediate, 2 = Expert.</summary>
    FlightModel,
}

/// <summary>One entry of the realism panel, and what turning it off buys you.</summary>
public sealed record RealismSetting(int Index, string Name, RealismKind Kind, string Description)
{
    /// <summary>True when leaving this setting <b>off</b> is the player-favouring choice.</summary>
    public bool OffIsEasier => Index is 3 or 6 or 7 or 8 or 9;
}

/// <summary>
/// The thirteen values behind Red Baron's Realism Panel.
///
/// <para><b>Layout.</b> <c>MREAL.PRF</c> (single missions) and <c>CREAL.PRF</c> (careers) are each
/// 26 bytes: thirteen little-endian 16-bit values, in the order below. The order was pinned by
/// reading the panel on screen at the Novice and Expert presets and diffing the file the sim wrote:
/// exactly one entry falls from 1 to 0 between those presets (Midair Collisions) and exactly two go
/// from 0 to 2 (Combat Level, Flight Model), which fixes indices 10-12; the remaining ten then match
/// the panel's reading order, left column then right column, in both presets.</para>
///
/// <para><b>Why it is worth a trainer.</b> Indices 6, 7 and 9 are the whole point: with Limited
/// Ammunition, Limited Fuel and Aircraft May Be Damaged off, the sim stops decrementing ammunition
/// and fuel and stops applying hits. Turning them off through this file leaves Combat Level alone,
/// so a career keeps its scoring multiplier from a difficulty it is no longer actually flying.</para>
/// </summary>
public static class RealismSettings
{
    public static readonly RealismSetting[] All =
    {
        new(0,  "Realistic instruments",  RealismKind.Toggle,      "Period gauges instead of the simplified panel."),
        new(1,  "Sun blind spot",         RealismKind.Toggle,      "Looking into the sun washes the view out."),
        new(2,  "Realistic weather",      RealismKind.Toggle,      "Wind, cloud and visibility affect the flight."),
        new(3,  "Gun jams allowed",       RealismKind.Toggle,      "Long bursts can jam a Vickers or Spandau (press U to clear)."),
        new(4,  "Blackouts allowed",      RealismKind.Toggle,      "High-g manoeuvres grey the screen out."),
        new(5,  "Carburettor freezes",    RealismKind.Toggle,      "The engine can cut at altitude."),
        new(6,  "Limited ammunition",     RealismKind.Toggle,      "Off = the guns never run dry."),
        new(7,  "Limited fuel",           RealismKind.Toggle,      "Off = the tank never empties."),
        new(8,  "Real navigation",        RealismKind.Toggle,      "Off = the map keeps showing where you are."),
        new(9,  "Aircraft may be damaged",RealismKind.Toggle,      "Off = hits, flak and hard landings do nothing."),
        new(10, "Combat level",           RealismKind.CombatLevel, "Easy / Standard / Hard. Drives the score multiplier."),
        new(11, "Midair collisions",      RealismKind.Toggle,      "Off = you fly through other aircraft."),
        new(12, "Flight model",           RealismKind.FlightModel, "Novice / Intermediate / Expert handling."),
    };

    public static readonly string[] CombatLevelNames = { "Easy", "Standard", "Hard" };
    public static readonly string[] FlightModelNames = { "Novice", "Intermediate", "Expert" };

    /// <summary>The panel exactly as the in-game NOVICE button sets it.</summary>
    public static readonly ushort[] Novice = { 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0 };

    /// <summary>The panel exactly as the in-game EXPERT button sets it.</summary>
    public static readonly ushort[] Expert = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 0, 2 };

    /// <summary>
    /// Expert everywhere it costs nothing, off everywhere it hurts. Exactly the five settings
    /// <see cref="RealismSetting.OffIsEasier"/> marks are cleared — gun jams, limited ammunition,
    /// limited fuel, real navigation, aircraft may be damaged — and everything else is left as the
    /// game's own Expert button sets it, so Combat Level stays on Hard and career scoring keeps the
    /// top multiplier.
    /// </summary>
    public static readonly ushort[] Invulnerable = { 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 2, 0, 2 };

    public static int MaximumValue(RealismKind kind) => kind == RealismKind.Toggle ? 1 : 2;

    /// <summary>Decodes a 26-byte realism block. Returns null when the buffer is the wrong size or out of range.</summary>
    public static ushort[]? Decode(ReadOnlySpan<byte> block)
    {
        if (block.Length < GameFacts.RealismBlockSize) return null;
        var values = new ushort[GameFacts.RealismSettingCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (ushort)(block[i * 2] | (block[i * 2 + 1] << 8));
            if (values[i] > MaximumValue(All[i].Kind)) return null;
        }
        return values;
    }

    /// <summary>Encodes thirteen settings into the 26-byte on-disk / in-memory form.</summary>
    public static byte[] Encode(IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != GameFacts.RealismSettingCount)
            throw new ArgumentException($"expected {GameFacts.RealismSettingCount} values", nameof(values));
        var block = new byte[GameFacts.RealismBlockSize];
        for (int i = 0; i < values.Count; i++)
        {
            ushort v = Math.Min(values[i], (ushort)MaximumValue(All[i].Kind));
            block[i * 2] = (byte)(v & 0xFF);
            block[i * 2 + 1] = (byte)(v >> 8);
        }
        return block;
    }

    /// <summary>
    /// True when the block could be a realism panel: every value within its setting's range, and not
    /// uniformly zero (a 26-byte run of zeroes is far too common in a 64 KB data segment to accept).
    /// </summary>
    public static bool LooksPlausible(ReadOnlySpan<byte> block)
    {
        var values = Decode(block);
        return values != null && Array.Exists(values, v => v != 0);
    }
}
