namespace HillsfarTrainer.Game;

/// <summary>An arena opponent and the tell that beats it.</summary>
/// <param name="Name">The opponent as the game's roster names it.</param>
/// <param name="Tell">
/// The physical tell. Where the game itself teaches it through pub gossip this is that text
/// condensed; otherwise it says so.
/// </param>
/// <param name="TellShipped">
/// True when the game ships a gossip paragraph spelling the tell out. Only four of the eight have
/// one — the rest have to be learned by watching.
/// </param>
public readonly record struct ArenaOpponent(string Name, string Tell, bool TellShipped);

/// <summary>One mission step that requires a named opponent beaten.</summary>
/// <param name="Mission">Class and mission number.</param>
/// <param name="Opponent">Who must be beaten.</param>
/// <remarks>
/// A record rather than a tuple for the same reason as <see cref="OverlandInfo"/>: tuple members are
/// fields, and WPF cannot bind to fields.
/// </remarks>
public readonly record struct MissionGate(string Mission, string Opponent);

/// <summary>
/// The eight arena opponents, read out of the game's own roster at <c>DGROUP:0x6509</c> (the roster
/// block runs <c>DGROUP:0x64F0</c>–<c>0x65BD</c>; see <c>docs/ReverseEngineering.md</c> §5.5),
/// together with the four fighting tells the game teaches through pub gossip.
///
/// <para>Every opponent telegraphs its next blow, and reading the tell is the whole fight. The four
/// shipped paragraphs establish the pattern: something moves — a staff end, a head, a tongue,
/// a helm feather — and the side it moves to tells you which side is coming. Block and watch for
/// several exchanges before attacking.</para>
/// </summary>
public static class ArenaBook
{
    /// <summary>The roster, in the order the game stores it.</summary>
    public static readonly IReadOnlyList<ArenaOpponent> Opponents = new[]
    {
        new ArenaOpponent("Lefty the left-handed Orc",
            "Drops his guard just before attacking. Whichever end of his staff is higher is the end "
            + "coming at you — left end up, counter with a quick left. Fights three left blows then a right.",
            true),
        new ArenaOpponent("The Red Minotaur",
            "Twitches his head before each attack, twice when he means to ram you. Head moves left "
            + "means he attacks with his right — hit him with a right to the head.",
            true),
        new ArenaOpponent("Ssslader, lizard man of the Vast Swamp",
            "Sticks his tongue out in the direction he will attack — tongue left, left jab. Uses a "
            + "right-left combo; tongue out twice means a tail attack, and hitting him straight after "
            + "the tail leaves him dizzy and open to a couple of free blows.",
            true),
        new ArenaOpponent("Morin the knight",
            "The feathers on his helm move before he attacks, and the higher end of his staff is the "
            + "end that lands. Attacks left for a while, then right for a while, then catches you "
            + "with a quick low blow.",
            true),
        new ArenaOpponent("Ottis the Orc, from the Thunder Peaks",
            "No shipped hint. Block and watch: like the other orc, expect the staff-end tell and a "
            + "repeating left/right pattern.",
            false),
        new ArenaOpponent("Taurus the Great, a mighty minotaur",
            "No shipped hint, and required by both the fighter's and the mage's third mission. "
            + "Expect a head-twitch tell like the Red Minotaur's, but do not attack blind.",
            false),
        new ArenaOpponent("Whiplash the lizard man",
            "No shipped hint beyond the roster's own warning to watch the tail. Expect Ssslader's "
            + "tongue tell plus a tail sweep.",
            false),
        new ArenaOpponent("Keller the Dark Knight",
            "No shipped hint; the toughest of the roster. Expect Morin's helm-and-staff tell with a "
            + "tighter pattern.",
            false),
    };

    /// <summary>Which missions require a specific opponent beaten.</summary>
    public static readonly IReadOnlyList<MissionGate> MissionGates = new[]
    {
        new MissionGate("Fighter, mission 1", "The Red Minotaur"),
        new MissionGate("Fighter, mission 2", "an Orc"),
        new MissionGate("Fighter, mission 3", "Taurus the Great"),
        new MissionGate("Magic-User, mission 3", "Taurus the Great"),
        new MissionGate("Thief, mission 3", "an Orc"),
    };
}
