namespace HillsfarTrainer.Game;

/// <summary>A keyboard command, and where in the game it applies.</summary>
/// <param name="Context">Which screen the key works on.</param>
/// <param name="Key">The key or keys.</param>
/// <param name="Action">What it does.</param>
public readonly record struct ControlInfo(string Context, string Key, string Action);

/// <summary>
/// Facts about <c>Hillsfar</c> that the trainer displays but never edits. The clock rate and the
/// healing formula are not from a manual — they were decoded out of the game's own clock-tick
/// routine and are exact.
/// </summary>
public static class GameFacts
{
    /// <summary>Title as the game prints it.</summary>
    public const string GameTitle = "Hillsfar";

    /// <summary>Publisher, developer and year.</summary>
    public const string Publisher = "SSI / Westwood Associates, 1989";

    /// <summary>The build's own version string, a literal at <c>DGROUP:0x0832</c>.</summary>
    public const string Version = "v1.2";

    /// <summary>
    /// Real seconds per game hour. The clock tick returns early while
    /// <c>now - lastTick &lt;= 121</c>, so an hour costs 122 seconds and a game day about 49 real
    /// minutes.
    /// </summary>
    public const int RealSecondsPerGameHour = 122;

    /// <summary>
    /// Length of a game day in real minutes, rounded rather than truncated. Integer division of
    /// 122 x 24 / 60 gives 48, but the true figure is 48.8 — and every doc and tip in the trainer says
    /// 49, so the computed strings have to agree with them.
    /// </summary>
    public static int RealMinutesPerGameDay { get; } =
        (int)Math.Round(RealSecondsPerGameHour * (double)CharacterFormat.HoursPerDay / 60.0);

    /// <summary>Constitution at or below which natural healing gives only the base 1 point per day.</summary>
    public const int HealingConstitutionThreshold = 14;

    /// <summary>Cap on the Constitution bonus to natural healing.</summary>
    public const int HealingConstitutionBonusCap = 5;

    /// <summary>Number of quest scripts the game ships — four classes times three missions.</summary>
    public const int QuestCount = 12;

    /// <summary>Process-name fragments that mark a likely emulator, floated to the top of the list.</summary>
    public static readonly IReadOnlyList<string> EmulatorHints = new[]
    {
        "dosbox", "dosbox-x", "dosbox-staging", "pcem", "86box", "qemu", "boxer",
    };

    /// <summary>
    /// Hit points regained per 24 game hours, exactly as the clock-tick routine computes it:
    /// <c>1 + clamp(Constitution - 14, 0, 5)</c>. So Constitution 14 or below heals 1 a day and
    /// Constitution 19 heals 6.
    /// </summary>
    public static int NaturalHealingPerDay(int constitution)
    {
        int bonus = constitution <= HealingConstitutionThreshold
            ? 0
            : constitution - HealingConstitutionThreshold;
        if (bonus > HealingConstitutionBonusCap) bonus = HealingConstitutionBonusCap;
        return 1 + bonus;
    }

    /// <summary>
    /// Formats an hour in 1..24 the way the game's own clock display does: subtract 12 when the hour
    /// exceeds 12, and print "am" when the hour is 24 or below 12, else "pm". Hour 24 is midnight.
    /// </summary>
    public static string FormatHour(int hour)
    {
        if (hour < 1 || hour > CharacterFormat.HoursPerDay) return "--";
        int shown = hour > 12 ? hour - 12 : hour;
        string suffix = hour == CharacterFormat.HoursPerDay || hour < 12 ? "am" : "pm";
        return $"{shown} {suffix}";
    }

    /// <summary>The keyboard controls, from the manual and confirmed in play.</summary>
    public static readonly IReadOnlyList<ControlInfo> Controls = new[]
    {
        new ControlInfo("City", "↑", "Move forward"),
        new ControlInfo("City", "← / →", "Turn left / right"),
        new ControlInfo("City", "↓", "Turn around 180°"),
        new ControlInfo("City", "Space", "Search / examine — use this constantly"),
        new ControlInfo("City", "R", "Recall the last clue given"),
        new ControlInfo("City", "P", "Use a healing potion"),
        new ControlInfo("City", "S", "Toggle sound"),
        new ControlInfo("City", "Backspace / Esc", "Pause"),
        new ControlInfo("Riding", "→ / ←", "Speed up / slow down"),
        new ControlInfo("Riding", "↑ / ↓", "Jump / duck"),
        new ControlInfo("Riding", "Space", "Fire the Rod of Blasting; take an unmarked trail when '?' shows"),
        new ControlInfo("Arena", "← / →", "Block left / right"),
        new ControlInfo("Arena", "← / → + fire", "Attack left / right"),
        new ControlInfo("Arena", "↑ / ↓", "Special block / special attack"),
        new ControlInfo("Locks", "Arrows", "Select a pick"),
        new ControlInfo("Locks", "Space", "Flip the pick over"),
        new ControlInfo("Locks", "Enter", "Try the pick on the current tumbler"),
        new ControlInfo("Locks", "F", "Force the lock"),
        new ControlInfo("Locks", "Z", "Use a knock ring"),
        new ControlInfo("Locks", "E", "Leave the lock — only before trying a pick"),
        new ControlInfo("Mazes", "Arrows", "Move; the exit is a stairway leading down"),
    };

    /// <summary>Short play notes drawn from the strategy guide.</summary>
    public static readonly IReadOnlyList<string> Tips = new[]
    {
        "Save at camp before every ride. There is no save inside the city.",
        "Bank your gold before pubs, mazes and rides — only carried gold is at risk.",
        "Press Space to search everywhere. Several mission steps are nothing but a search in the right place.",
        "Pubs open 5 pm–7 am; shops, bank, bookstore, archery and the Mages' Tower shut at 3 pm. Split your day.",
        "The Cemetery is open 12 am–7 am only, and the missions that send you there never say so.",
        "Castle, Haunted Mansion and Jail are never open — those are break-ins.",
        "One game hour costs 122 real seconds, so a game day is about 49 minutes. Resting at your guild is the efficient way to pass time.",
        "Natural healing is 1 + clamp(Con − 14, 0, 5) hit points per game day. Low-Constitution characters should buy potions instead.",
        "Knock rings open any lock, one ring each, and every class can buy them. 'Z' uses one.",
        "Hire the NPC rogue whenever he offers — half of found gold is cheap for reliable locks.",
        "Secret rooms are in the top-left of a maze, through a left-hand wall.",
        "In mazes, guard contact drains your time, not your hit points. Leave before the clock runs out or you lose everything you picked up.",
        "In the arena, block and watch first — every opponent telegraphs its next blow.",
        "Don't guzzle drinks: passing out costs all your gold. A meal soaks up some of the booze.",
        "At the archery range, practise free first, read the windmill, and prefer the dagger in high wind — the heaviest weapon drifts least.",
        "Dexterity is the best stat: less aim drift at the range, better thief skills.",
    };
}
