namespace MightAndMagic1Trainer.Game;

/// <summary>
/// One square worth walking to, and why.
/// </summary>
/// <param name="RawName">The place it is in, by the engine's own name for that place.</param>
/// <param name="X">Column, 0–15 west to east.</param>
/// <param name="Y">Row, 0–15 as the guide that supplied it counts them.</param>
/// <param name="Name">What is there.</param>
/// <param name="Description">What it does for you.</param>
/// <param name="Source">Where the coordinate came from, so a reader can weigh it.</param>
public sealed record Landmark(string RawName, int X, int Y, string Name, string Description, string Source)
{
    /// <summary>"(11, 3)".</summary>
    public string Where => $"({X}, {Y})";
}

/// <summary>
/// The squares worth walking to, and the honest limits on how many of them are known.
///
/// <para><b>Every entry here is a coordinate somebody published, not one this project decoded.</b>
/// The game does hold the answer — each location's overlay carries a table of event ids its
/// dispatcher matches against the square you are standing on — but what those ids index is not
/// established (<c>docs/ovr-format.md</c> §7 gets as far as "small byte values that look like map
/// coordinates"), so the trainer can say a place has fourteen event squares and cannot say which
/// fourteen. Until that is worked out, a marked square comes from a walkthrough and is tagged with
/// the fact.</para>
///
/// <para>That is why this list is short. It is drawn from the coordinates in
/// <see cref="Walkthrough"/> — the ones already cross-checked between community guides for that
/// tab — rather than padded out with landmarks nobody has checked. A cluebook with twelve marks that
/// are right is worth more than one with sixty that might be. The other half of the annotation, the
/// secret passages, is computed from the maze data instead and is exact: see
/// <see cref="MazeMap.SecretPassages"/>.</para>
/// </summary>
public static class LandmarkBook
{
    /// <summary>What the coordinates below were taken from.</summary>
    private const string FromWalkthrough = "the walkthrough";

    private static Landmark L(string raw, int x, int y, string name, string description) =>
        new(raw, x, y, name, description, FromWalkthrough);

    /// <summary>Every marked square, grouped into places by <see cref="For"/>.</summary>
    public static readonly IReadOnlyList<Landmark> Landmarks = new[]
    {
        L("sorpigal", 11, 3, "The leprechaun",
            "Give him one gem and he moves the whole party to any of the five towns — the only travel " +
            "worth having before a Sorcerer learns Fly."),
        L("cave1", 1, 2, "The old man",
            "He hands over the letter the courier quest is built around."),
        L("portsmit", 12, 2, "Zam, in the secret room",
            "The first astral brother. His clue is C-15, which means nothing until Zom gives you the other half."),
        L("algary", 1, 1, "Zom",
            "The second astral brother, and the other half of the clue: 1-15."),
        L("dusk", 8, 0, "Telgoran",
            "Sets the task of finding the other two brothers, which is what makes their clues worth having."),
        L("areac1", 15, 15, "The Ruby Whistle",
            "Where Zam's C-15 and Zom's 1-15 point when you put them together. Fly here and search."),
        L("areac1", 5, 7, "The ogres with the Merchant Pass",
            "Kill them for the pass. No castle lord will grant an audience without it."),
        L("doom", 15, 15, "The interleave clue",
            "Spells out what the Inner Sanctum wants — the five portals, and the order the silver " +
            "messages have to be read in."),
        L("dragad", 13, 15, "Gold into experience",
            "Takes every gold piece the party carries and pays experience for it. Bring all of it."),
        L("dragad", 1, 1, "The judgement",
            "The worthy gain two points of Luck, permanently."),
        L("areaa2", 0, 15, "Percella the Druid",
            "Hands over the King's Pass, which opens the areas nothing else will."),
        L("areac3", 6, 14, "Lord Kilburn",
            "Gives the Desert Map, without which the desert turns you around."),
        L("areac2", 9, 11, "The gypsy",
            "Reads each character's colour and zodiac sign. Write all six down — the gypsy bridge in " +
            "A-4 asks for them one character at a time."),
    };

    private static readonly Dictionary<string, List<Landmark>> ByPlace =
        Landmarks.GroupBy(l => l.RawName, StringComparer.OrdinalIgnoreCase)
                 .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The marked squares of one place, in the order they are listed above.</summary>
    public static IReadOnlyList<Landmark> For(string rawName) =>
        rawName is not null && ByPlace.TryGetValue(rawName, out var found)
            ? found
            : Array.Empty<Landmark>();
}
