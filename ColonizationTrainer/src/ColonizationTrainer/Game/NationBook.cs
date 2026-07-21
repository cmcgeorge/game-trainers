namespace ColonizationTrainer.Game;

/// <summary>A playable European power: its save index, name, and signature bonus.</summary>
public sealed record Nation(int Index, string Name, string Bonus, string StartUnit);

/// <summary>A difficulty level (index 0..4) and what it changes.</summary>
public sealed record Difficulty(int Level, string Name, string Notes);

/// <summary>
/// The four playable nations (save indices 0..3 — the order the four nation records sit in) plus the
/// five difficulty levels. Verified against the viceroy <c>nation_list</c> / <c>difficulty_list</c>
/// and <c>.games/GAME.TXT</c> (<c>@PICKNATION</c> / <c>@DIFFICULTY</c>).
/// </summary>
public static class NationBook
{
    public static readonly IReadOnlyList<Nation> Nations = new[]
    {
        new Nation(0, "England",     "More immigration — faster arrivals on the docks", "—"),
        new Nation(1, "France",      "Better native relations; alarm rises slowly",     "Hardy Pioneer"),
        new Nation(2, "Spain",       "+50% attack vs. native settlements",              "Veteran Soldier"),
        new Nation(3, "Netherlands", "Better trade prices; a bigger starting ship",     "Merchantman"),
    };

    public static readonly IReadOnlyList<Difficulty> Difficulties = new[]
    {
        new Difficulty(0, "Discoverer",   "Easiest — small King's army, up to 10 Tories before a penalty"),
        new Difficulty(1, "Explorer",     "Easy"),
        new Difficulty(2, "Conquistador", "Moderate"),
        new Difficulty(3, "Governor",     "Tough — +1 to your combat rolls"),
        new Difficulty(4, "Viceroy",      "Hardest — huge King's army, only 6 Tories before a penalty"),
    };

    /// <summary>Human-readable nation name for a save index (0..3), or a fallback for out-of-range.</summary>
    public static string NameOf(int index) =>
        index >= 0 && index < Nations.Count ? Nations[index].Name : $"Nation {index}";

    /// <summary>Human-readable difficulty name for a level (0..4), or a fallback.</summary>
    public static string DifficultyName(int level) =>
        level >= 0 && level < Difficulties.Count ? Difficulties[level].Name : $"Level {level}";
}
