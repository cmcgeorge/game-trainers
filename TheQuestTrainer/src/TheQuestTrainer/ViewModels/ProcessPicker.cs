using TheQuestTrainer.Game;

namespace TheQuestTrainer.ViewModels;

/// <summary>How well a process name matches the game we are looking for.</summary>
public enum ProcessMatch
{
    /// <summary>Nothing in the name suggests it is the game.</summary>
    None = 0,

    /// <summary>The name contains one of the hint substrings.</summary>
    Hint = 1,

    /// <summary>The name is exactly the game's process name.</summary>
    Exact = 2,
}

/// <summary>One attachable process, as the picker shows it.</summary>
public sealed record ProcessEntry(int Id, string Name, string WindowTitle)
{
    /// <summary>How well this process matches the game.</summary>
    public ProcessMatch Match => ProcessPicker.Rank(Name);

    /// <summary>Label for the combo box.</summary>
    public string Display => string.IsNullOrWhiteSpace(WindowTitle)
        ? $"{Name} ({Id})"
        : $"{Name} ({Id}) — {WindowTitle}";
}

/// <summary>
/// Ranks and orders candidate processes for the attach picker.
///
/// Pure so it can be tested headlessly, and separate because getting it wrong is not cosmetic: the
/// trainer's own executable is <c>TheQuestTrainer.exe</c>, whose process name contains "quest" and
/// sorts <i>after</i> <c>TheQuest</c> only by luck. A picker that merely substring-matched would
/// happily offer — and on another day auto-select — the trainer itself, attach to a 64-bit .NET
/// process, and then report that no character record could be found. Exact matches therefore
/// outrank hints, the trainer's own process is excluded outright, and a hint-only match is never
/// chosen automatically.
/// </summary>
public static class ProcessPicker
{
    /// <summary>How well <paramref name="processName"/> matches the game (no ".exe" expected).</summary>
    public static ProcessMatch Rank(string processName)
    {
        if (string.Equals(processName, GameFacts.ProcessName, StringComparison.OrdinalIgnoreCase))
            return ProcessMatch.Exact;

        foreach (string hint in GameFacts.TargetHints)
            if (processName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return ProcessMatch.Hint;

        return ProcessMatch.None;
    }

    /// <summary>Whether a process should be offered at all. The trainer never attaches to itself.</summary>
    public static bool IsSelectable(int processId, int ownProcessId) => processId != ownProcessId;

    /// <summary>
    /// Orders candidates: exact matches first, then hint matches, then everything else, each group
    /// sorted by name.
    /// </summary>
    public static IEnumerable<T> Order<T>(IEnumerable<T> entries, Func<T, ProcessMatch> rank, Func<T, string> name)
        => entries.OrderByDescending(rank).ThenBy(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Picks the default selection: whatever was selected before if it is still there, else the only
    /// exact match, else nothing. Deliberately refuses to default to a hint-only match — the user
    /// should have to choose rather than be silently pointed at the wrong process.
    /// </summary>
    public static T? ChooseDefault<T>(IReadOnlyList<T> ordered, Func<T, ProcessMatch> rank,
                                      Func<T, int> id, int? previouslySelected) where T : class
    {
        if (previouslySelected is { } prev)
            foreach (var e in ordered)
                if (id(e) == prev) return e;

        foreach (var e in ordered)
            if (rank(e) == ProcessMatch.Exact) return e;

        return null;
    }
}
