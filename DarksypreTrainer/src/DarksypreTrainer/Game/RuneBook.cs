namespace DarksypreTrainer.Game;

/// <summary>One rune in DarkSpyre. [Confirmed from manual and walkthrough]</summary>
public sealed record RuneEntry(
    string Norse,
    string English,
    string Effect,
    bool IsPowerRune);

/// <summary>
/// All 25 runes in DarkSpyre. The 5 power runes (Strength, Agility, Endurance, Accuracy,
/// Talent) must be collected and exchanged on Level 36 for gifts from the gods before
/// entering the final 3 levels. Raido is essential — it is the only way to save the game.
/// [Confirmed from manual and walkthrough]
/// </summary>
internal static class RuneBook
{
    public static IReadOnlyList<RuneEntry> Runes { get; } = new[]
    {
        new RuneEntry("Uraz",      "Strength",    "N/A — power rune, exchange on Level 36",                  true),
        new RuneEntry("Ehwaz",     "Agility",     "N/A — power rune, exchange on Level 36",                  true),
        new RuneEntry("Eihwaz",    "Accuracy",    "N/A — power rune, exchange on Level 36",                  true),
        new RuneEntry("Teiwaz",    "Endurance",   "N/A — power rune, exchange on Level 36",                  true),
        new RuneEntry("Inguz",     "Talent",      "N/A — power rune, exchange on Level 36",                  true),
        new RuneEntry("Raido",     "Quest",       "Saves the game (one use per rune)",                       false),
        new RuneEntry("Thurisaz",  "Gateway",     "Takes you to the next level",                             false),
        new RuneEntry("Jera",      "Sustenance",  "Restores HP",                                             false),
        new RuneEntry("Algit",     "Protection",  "Cures poison",                                            false),
        new RuneEntry("Sowelu",    "Unity",       "Cures poison and confusion",                              false),
        new RuneEntry("Keno",      "Opening",     "Knock spell effect",                                      false),
        new RuneEntry("Fehu",      "Wealth",      "Becomes a knock scroll",                                  false),
        new RuneEntry("Gebo",      "Alliance",    "Magic Map effect",                                        false),
        new RuneEntry("Dagaz",     "Discovery",   "Destroys a monster",                                      false),
        new RuneEntry("Isa",       "Stagnant",    "Poisons you",                                             false),
        new RuneEntry("Ansuz",     "Omens",       "Unknown effect",                                          false),
        new RuneEntry("Berkana",   "Enhancement", "Unknown effect",                                          false),
        new RuneEntry("Hagalaz",   "Progress",    "Unknown effect",                                          false),
        new RuneEntry("Laquz",     "Progress",    "Unknown effect",                                          false),
        new RuneEntry("Mannaz",    "Illusions",   "Unknown effect",                                          false),
        new RuneEntry("Nauthiz",   "Force",       "Unknown effect",                                          false),
        new RuneEntry("Odin",      "Hidden",      "Unknown effect",                                          false),
        new RuneEntry("Othilia",   "Severance",   "Unknown effect",                                          false),
        new RuneEntry("Perth",     "Initiation",  "Unknown effect",                                          false),
        new RuneEntry("Wunjo",     "Charm",       "Unknown effect",                                          false),
    };

    public static IReadOnlyList<RuneEntry> PowerRunes =>
        Runes.Where(r => r.IsPowerRune).ToList();
}
