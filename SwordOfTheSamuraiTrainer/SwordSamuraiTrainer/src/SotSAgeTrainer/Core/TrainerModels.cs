using System;
using System.Collections.Generic;
using System.Linq;

namespace SotSAgeTrainer.Core;

/// <summary>
/// The protagonist's life-stage byte (live character record, rec+0x33). Verified live in-game:
/// Christopher = Youth = 0; the rivals shown as "Young Adult" = 1. Higher = older.
/// </summary>
public enum LifeStage : byte
{
    Youth = 0,
    YoungAdult = 1,
    MatureAdult = 2,
    Old = 3,
}

public static class LifeStages
{
    // Age ladder as read from the live game (byte 0 = Youth, 1 = Young Adult, …).
    private static readonly string[] Ladder =
        { "Youth", "Young adult", "Mature adult", "Old", "Aged", "Venerable" };

    public static string Label(byte value) =>
        value < Ladder.Length ? Ladder[value] : $"stage {value}";

    public static string Label(LifeStage stage) => Label((byte)stage);
}

/// <summary>A character's role relative to the player.</summary>
public enum CharacterRole
{
    You,   // record 0 of a character array
    Kin,   // shares the player's family index (+0x32)
    Rival, // everyone else
}

/// <summary>
/// One character record parsed from the live in-memory array (word-scaled, stride 0x60). Verified
/// field map: age byte rec+0x33, family byte rec+0x32, army word rec+0x40, stat cluster words rec+0x3A…0x58.
/// </summary>
public sealed record CharacterRecord(
    int Index,
    IntPtr Address,
    CharacterRole Role,
    byte FamilyIndex,
    byte AgeStage,
    int Army,
    IReadOnlyList<int> Stats)
{
    public IntPtr AgeAddress => (IntPtr)((long)Address + 0x33);
    public IntPtr ArmyAddress => (IntPtr)((long)Address + 0x40);
    public string RoleText => Role switch
    {
        CharacterRole.You => "YOU",
        CharacterRole.Kin => "kin",
        _ => "rival",
    };
    public string AgeText => LifeStages.Label(AgeStage);
    public string StatsText => string.Join(" ", Stats);
}

/// <summary>One located copy of a character array (there are several: the live working copies the game
/// displays from, plus save-image buffers). Edits are applied to every copy so the live one is covered.</summary>
public sealed class GameBlock
{
    public IntPtr BaseAddress { get; init; }
    public IReadOnlyList<CharacterRecord> Records { get; init; } = Array.Empty<CharacterRecord>();
    public CharacterRecord? Protagonist => Records.Count > 0 ? Records[0] : null;
}

/// <summary>An immutable snapshot the engine hands to the UI on every tick.</summary>
public sealed record TrainerStatus
{
    public bool Connected { get; init; }
    public int ProcessId { get; init; }
    public bool Freezing { get; init; }
    public LifeStage Target { get; init; } = LifeStage.Youth;
    public IReadOnlyList<GameBlock> Blocks { get; init; } = Array.Empty<GameBlock>();
    public string Message { get; init; } = "";

    public int BlockCount => Blocks.Count;

    /// <summary>Roster from the first located array (all copies mirror the same characters).</summary>
    public IReadOnlyList<CharacterRecord> Roster =>
        Blocks.Count > 0 ? Blocks[0].Records : Array.Empty<CharacterRecord>();

    public byte? CurrentStage => Blocks.Count > 0 ? Blocks[0].Protagonist?.AgeStage : null;
    public int? CurrentArmy => Blocks.Count > 0 ? Blocks[0].Protagonist?.Army : null;
    public int RivalCount => Roster.Count(r => r.Role == CharacterRole.Rival);
}
