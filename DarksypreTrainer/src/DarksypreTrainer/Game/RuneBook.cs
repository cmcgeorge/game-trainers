namespace DarksypreTrainer.Game;

/// <summary>
/// One rune. <paramref name="Norse"/> is the spelling the game itself uses in
/// <c>OBJ.DAT</c>; where the printed manual spells it differently that spelling is kept in
/// <paramref name="ManualSpelling"/>.
/// </summary>
public sealed record RuneEntry(
    string Norse,
    string English,
    string Effect,
    bool IsPowerRune,
    string ManualSpelling = "")
{
    /// <summary>Manual spelling when it differs from the game's own, otherwise empty.</summary>
    public string Variant => ManualSpelling.Length == 0 ? "" : $"manual: {ManualSpelling}";
}

/// <summary>
/// All 25 runes. Names come from the game's own object table (<c>OBJ.DAT</c> ships exactly 25
/// <c>"… rune"</c> entries); the English meanings are the manual's rune table. The five power
/// runes (Strength, Agility, Endurance, Accuracy, Talent) are collected through the game and
/// exchanged on Level 36 for gifts from the gods before the final levels. Raido is essential —
/// it is the only way to save.
/// </summary>
internal static class RuneBook
{
    public static IReadOnlyList<RuneEntry> Runes { get; } = new[]
    {
        new RuneEntry("Uraz",     "Strength",    "Power rune — exchange on Level 36",       true),
        new RuneEntry("Ehwaz",    "Agility",     "Power rune — exchange on Level 36",       true),
        new RuneEntry("Eihwaz",   "Accuracy",    "Power rune — exchange on Level 36",       true),
        new RuneEntry("Teiwaz",   "Endurance",   "Power rune — exchange on Level 36",       true),
        new RuneEntry("Inguz",    "Talent",      "Power rune — exchange on Level 36",       true),
        new RuneEntry("Raido",    "Quest",       "Saves the game (one save per rune)",      false),
        new RuneEntry("Thurisaz", "Gateway",     "Takes you to the next level",             false),
        new RuneEntry("Jera",     "Sustenance",  "Restores hit points",                     false, "Sustanance"),
        new RuneEntry("Algit",    "Protection",  "Cures poison",                            false),
        new RuneEntry("Sowelu",   "Unity",       "Cures poison and confusion",              false),
        new RuneEntry("Kano",     "Opening",     "Knock spell effect",                      false, "Keno"),
        new RuneEntry("Fehu",     "Wealth",      "Becomes a knock scroll",                  false),
        new RuneEntry("Gebo",     "Alliance",    "Magic Map effect",                        false),
        new RuneEntry("Dagaz",    "Discovery",   "Destroys a monster",                      false),
        new RuneEntry("Isa",      "Stagnant",    "Poisons you — harmful",                   false),
        new RuneEntry("Ansuz",    "Omens",       "Effect not documented",                   false),
        new RuneEntry("Berkana",  "Enhancement", "Effect not documented",                   false),
        new RuneEntry("Hagalaz",  "Progress",    "Effect not documented",                   false),
        new RuneEntry("Laguz",    "Progress",    "Effect not documented",                   false),
        new RuneEntry("Mannaz",   "Illusions",   "Effect not documented",                   false),
        new RuneEntry("Nauthiz",  "Force",       "Effect not documented",                   false),
        new RuneEntry("Odin",     "Hidden",      "Effect not documented",                   false),
        new RuneEntry("Othila",   "Severance",   "Effect not documented",                   false, "Othilia"),
        new RuneEntry("Perth",    "Initiation",  "Effect not documented",                   false),
        new RuneEntry("Wunjo",    "Charm",       "Effect not documented",                   false),
    };

    /// <summary>The five runes exchanged on Level 36.</summary>
    public static IReadOnlyList<RuneEntry> PowerRunes => Runes.Where(r => r.IsPowerRune).ToList();
}
