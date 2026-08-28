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
/// <para><b>Every entry here is a coordinate somebody published or the game's own text names, not
/// one this project decoded from the overlay event tables.</b>
/// The game does hold the answer — each location's overlay carries a table of event ids its
/// dispatcher matches against the square you are standing on — but what those ids index is not
/// established (<c>docs/ovr-format.md</c> §7 gets as far as "small byte values that look like map
/// coordinates"), so the trainer can say a place has fourteen event squares and cannot say which
/// fourteen. Until that is worked out, a marked square comes from a walkthrough or from the game's
/// own overlay text and is tagged with which.</para>
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
    private const string FromGameText = "the game's own text";

    private static Landmark L(string raw, int x, int y, string name, string description) =>
        new(raw, x, y, name, description, FromWalkthrough);

    private static Landmark G(string raw, int x, int y, string name, string description) =>
        new(raw, x, y, name, description, FromGameText);

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
        G("areaa1", 15, 7, "The secret passage to Doom",
            "A block of ice carries the game's own directions: \"START AT 15-7 AND WALK TO DOOM!\" " +
            "Standing on this square on the overworld reveals the passage into Castle Doom."),
        G("areab2", 9, 9, "Raven's Lair entrance",
            "A tree carving reads \"9-9 RAVEN'S LAIR\". A cave entrance is offered on this square."),
        G("areab3", 14, 2, "The sealed stronghold",
            "A note found in the Ruby Whistle chest reads \"THE STRONGHOLD LIES AT B-3,14-2 / " +
            "BLOW TWICE TO ENTER.\" Raid it for the Crystal Key and the Gold Key."),

        L("areaa1", 12, 1, "The Pool of Health",
            "\"THE POOL OF HEALTH GRANTS THOSE WHO ARE WORTHY +4 ENDURANCE!\" A permanent gain — " +
            "every character can drink again after the Clerics of the South bless the party."),
        L("areaa2", 2, 4, "Pirate's Secret Cove",
            "Lord Ironfist's fourth quest. Search the cove."),
        L("areaa3", 3, 6, "The Wheel of Luck",
            "A large wheel covered in archaic symbols. Spin it for a random reward. Defeat all four " +
            "overworld monsters first — the Dark Rider (around A-2 (5, 2)) explains how the payoff works."),
        L("areaa4", 4, 6, "The gypsy bridge",
            "A hooded figure demands each character's colour — the gypsy seer at C-2 (9, 11) gives each one. " +
            "Correct answers for at least three party members let you walk across to (4, 2) and collect the " +
            "Coral Key. A wrong answer kills the character."),
        L("areab1", 4, 7, "The Silver Key",
            "Approach from the west: enter from (0, 6) and walk east. The key opens a silver door deep " +
            "inside the Warrior's Stronghold (Raven's Lair)."),
        L("areab2", 4, 4, "The Ice Princess",
            "Answer her riddle — the answer is LOVE — and search immediately for the Bronze Key. " +
            "Answer a second time for the Diamond Key. " +
            "Bronze opens the dungeon below Portsmith; Diamond opens the Astral Plane door in E-3."),
        L("areab2", 8, 4, "Cave to the Medusa",
            "Lord Hacker's fourth quest demands a Medusa's head. Fight through the basilisks and " +
            "collect the head at (15, 3) inside the cave."),
        L("areab3", 9, 6, "Blyth's Peak",
            "Lord Inspectron's second quest. Stand on the peak and then return to the castle."),
        L("areac2", 0, 2, "The only entrance to Raven's Lair",
            "Stone statues block the path and come to life as guards. This is the sole overworld " +
            "entrance to the Raven's Lair dungeon in B-2."),
        L("areac3", 7, 7, "Wyvern's Eye",
            "Lord Hacker's fifth quest. The wyverns attack from above; once they are dead, one eye " +
            "glows and can be taken."),
        L("areac4", 8, 13, "Jolly Raven shipwreck",
            "Lord Ironfist's fifth quest. Search the decaying hull."),
        L("aread2", 10, 12, "The Pool of Wisdom",
            "\"THE POOL OF WISDOM GRANTS THOSE WHO ARE WORTHY +4 PERSONALITY.\" A permanent gain. " +
            "The Clerical Retreat nearby cures curses, removes conditions and restores alignment at no charge."),
        L("aread3", 0, 2, "Arenko Guire's grove",
            "Speak to Arenko Guire first, then climb all nineteen trees without leaving the area and return. " +
            "Gold, gems or a magic item are the reward. The trees are just trees if you don't talk to him first."),
        L("aread3", 7, 13, "The Magic Square cave",
            "Set the sixteen polyhedrons so every row, column and diagonal sums to 34, then pull the " +
            "lever at dungeon square (0, 15) for +2 Intellect, 20 gems, 200 gold and 2,000 experience."),
        L("aread4", 7, 1, "Og",
            "The winged beast who has lost his sight. Bring both non-ruby idols and answer " +
            "\"Queen to King's level 1\" to restore him. He awards 25,000 experience and reveals " +
            "that a prisoner in Castle Doom holds your sight."),
        L("areae1", 3, 3, "The Sands of Time",
            "\"AT THE CENTER OF THE LAND THAT TIME FORGOT STANDS AN HOURGLASS, TURN IT.\" " +
            "Makes characters younger — essential if old age is dragging down their statistics. " +
            "There are guardians; be prepared."),
        L("areae1", 9, 12, "The Scale of Judgement",
            "\"STATUE OF A GIANT HOLDING THE SCALE OF JUDGEMENT.\" Awards experience per character " +
            "based on how you treated the prisoners in the six castles. Visit all six first."),
        L("areae2", 3, 13, "Strange alien device",
            "\"STRANGE ALIEN DEVICE GRANTS THOSE WHO ARE WORTHY +4 INTELLECT!\" A permanent gain. " +
            "The crashed craft nearby holds an alien whose message is worth reading."),
        L("areae3", 14, 7, "Castle Alamar",
            "The final castle. Bring the King's Pass (from Percella in A-2 at (0, 15)) and the " +
            "Eye of Goros (from Castle Doom). Show the Eye to the king; he reveals his true form " +
            "and casts the party into the Soul Maze."),
        L("areae4", 10, 5, "The City of Gold",
            "Entrance to the Building of Gold dungeon, which carries Gold Message #7 and a Crystal " +
            "Grate requiring the Crystal Key. The Dragon City town meeting at dungeon square (8, 5) " +
            "inside is extremely dangerous unless the party is level 25 or higher."),
        L("erliquin", 2, 5, "Wizard Agar",
            "He is the destination of the vellum scroll from the old man in the Sorpigal caverns. " +
            "He then sends you to Telengar in Dusk."),

        L("sorpigal", 14, 14, "Statue of a frog",
            "Behind a hidden door. The frog warns that in areas with black-and-white checked floors " +
            "you will find what you need to help Og — one of the two idols Og requires."),
        L("portsmit", 10, 4, "The Succubus",
            "Lord Ironfist's third quest. Do not enter this hidden room unless every character is level 15 or higher — " +
            "the Succubus and her accompanying devils are extremely dangerous, and there is no way out."),
        L("portsmit", 0, 8, "Stairs to the Portsmith dungeon",
            "Leads down to Cave 3, which holds the sex reversal fountain, the Pool of Might and a portal to Erliquin."),
        L("dusk", 14, 0, "Stairs to the Dusk dungeon",
            "Leads down to Cave 5, which holds the Shrine of Okzar, the Flame of Agility and the Prism of Accuracy."),

        L("cave3", 8, 2, "Bronze door",
            "Requires the Bronze Key. The Ice Princess in B-2 at (4, 4) gives it on the first correct answer (LOVE)."),
        L("cave3", 8, 7, "Demons in Conference",
            "\"DEMONS IN CONFERENCE, DO NOT DISTURB!\" They are as dangerous as the Succubus upstairs. Leave them alone."),
        L("cave3", 11, 15, "Sex reversal fountain",
            "Reverses the sex of every character who steps in. Portsmith drains male characters, so male party members " +
            "can come here to become female, complete the town, then reverse again."),
        L("cave3", 0, 12, "Pool of Might",
            "\"THOSE WHO ARE WORTHY +4 MIGHT!\" A permanent gain."),

        L("cave4", 4, 9, "Access code terminal",
            "Enter YICU2ME3 to deactivate the flame barriers that block part of this dungeon. " +
            "The code is written on the wall of Cave 5 (the Dusk dungeon) at (2, 5)."),
        L("cave4", 0, 5, "Teleport to the east side",
            "One of two teleports that move the party between the sealed halves of this dungeon. " +
            "The matching teleport is at (15, 5). There is also a back door to the B-1 overworld at (15, 7)."),

        L("cave5", 0, 15, "The Shrine of Okzar",
            "Lord Inspectron's fourth quest. The party leader must be of clear mind (not confused or cursed) to pray."),
        L("cave5", 12, 5, "Secret door to the Flame of Agility",
            "Enter the secret door here to reach (14, 5), where the Flame grants +4 Speed permanently to those who are worthy."),
        L("cave5", 15, 15, "Prism of Accuracy",
            "\"THE PRISM OF PRECISION GRANTS THOSE WHO ARE WORTHY +4 ACCURACY!\" A permanent gain."),
        L("cave5", 2, 5, "Gold message — and the Erliquin access code",
            "The gold message here is one of nine needed for the final cipher. It also spells out the access code " +
            "YICU2ME3, which disables the flame barriers in the Erliquin caves (Cave 4) at (4, 9)."),

        L("cave6", 5, 15, "Wizard Ranalou",
            "He maintains six portals, one to each castle. The Statue of Judgement in E-1 requires you to find " +
            "one prisoner in each castle before it will award experience."),

        L("cave7", 7, 11, "The Volcano God",
            "Set the stabilisation dials to BJ (one dial at (6, 3), one at (8, 3)) before entering — otherwise the " +
            "teleports send you in random directions. The God gives a clue and eventually rewards the Key Card " +
            "needed to unlock the Inner Sanctum."),

        L("cave8", 0, 15, "The platinum lever",
            "Pull it after all sixteen polyhedrons sum to 34 in every row, column and diagonal for +2 Intellect, " +
            "20 gems, 200 gold and 2,000 experience."),

        L("qvl1", 0, 15, "Chess piece",
            "One of two chess pieces needed for Og's quest in D-4 at (7, 1). " +
            "The second is at level 4 of the Building of Gold dungeon (E-4)."),
        L("qvl1", 12, 12, "Ring of Okrim",
            "Lord Hacker's seventh quest. Defeat Okrim — his ghost offers a trade; the ring is the prize."),

        L("rwl1", 6, 11, "The Crystal Key riddle",
            "The riddle is at the centre of the room. Answer CRYSTAL to receive the Crystal Key, " +
            "which opens the Crystal Grate in the Building of Gold dungeon (E-4)."),
        L("rwl1", 1, 5, "Silver door",
            "Requires the Silver Key from B-1 overworld at (4, 7). Beyond it is the path to Lord Raven on the next level."),

        L("rwl2", 6, 14, "Trial by combat",
            "Five tests of combat in sequence. The fifth is reached through a secret door at (3, 4), not in the obvious series. " +
            "Jump the conveyor belt at (14, 4–14) to reach the button at (15, 4), then proceed to (14, 1)."),
        L("rwl2", 14, 1, "Lord Raven",
            "The master of Raven's Lair. Surrender, and he lets the party live but takes all their gold. " +
            "His defeat (or surrender) fulfils Lord Ironfist's seventh quest."),

        L("enf1", 10, 15, "Gold message 3",
            "One of nine gold messages needed for the final cipher. The other eight are scattered across the other major dungeons."),

        L("enf2", 9, 14, "The Minotaur",
            "Killing or retreating from the Minotaur fulfils Lord Inspectron's seventh quest. " +
            "The entrances to his maze are at (9,3), (8,3), (7,3) and (6,3); use Etherialize to skip the maze."),
        L("enf2", 3, 4, "The doggie",
            "Answers the question \"WHO BE YE?\" — the correct answer is \"I BE ME.\" " +
            "Do not desecrate him afterwards; search instead, and he gives the Gold Key needed for Castle Doom."),

        L("doom", 15, 0, "The silver interleave",
            "Decoding all six silver messages in the order given here reveals the six locations where " +
            "statistics can be permanently raised."),
        L("doom", 4, 3, "The spiral",
            "A hidden door leads to the spiral maze. Without the Gold Key (from the Enchanted Forest doggie) " +
            "do not attempt to navigate to the centre — you cannot reach King Alamar without it."),

        L("udrag3", 0, 12, "The Clerics of the South",
            "Bless the party after it has brought them the three tones (which roam the level). " +
            "Their blessing allows every stat-boosting pool and flame in the game to work a second time."),
        L("udrag3", 14, 11, "Gold message 5",
            "One of nine gold messages needed for the final cipher. The only notable treasure on this level."),

        L("udrag1", 13, 12, "Gold message 8",
            "One of nine gold messages needed for the final cipher. The 10-ft level is otherwise a transit corridor."),

        L("cave2", 11, 7, "Slide control button",
            "Disables the slides that otherwise block passage through this dungeon. " +
            "The cave entrance is at C-2 overworld (15, 11)."),

        L("cave9", 8, 13, "Gold message 7",
            "One of nine gold messages needed for the final cipher. " +
            "The Building of Gold is the E-4 dungeon entered from overworld (10, 5)."),
        L("cave9", 13, 2, "Crystal Grate",
            "Requires the Crystal Key, awarded by the riddle at Raven's Lair level 1 (6, 11) when you answer CRYSTAL."),
        L("cave9", 8, 5, "Dragon City Town Meeting",
            "Do not interrupt unless every character is level 25 or higher — the meeting's defenders are extremely dangerous."),

        L("qvl1", 3, 14, "Gold message 1",
            "One of nine gold messages needed for the final cipher. This is the Wizard's Lair, entered from B-1 overworld."),
        L("qvl1", 12, 15, "The Ancient Ruins",
            "The destination for Lord Inspectron's first quest. Reach here via the slide in Blackridge South Castle " +
            "or through the Erliquin cave back door at (15, 7)."),

        L("qvl2", 12, 13, "Gold message 4",
            "One of nine gold messages. This is the second level of the Wizard's Lair; stairs down are at (0, 6) on level 1."),

        L("rwl1", 4, 0, "Gold message 6",
            "One of nine gold messages. This is the first level of the Raven's Wood Warrior's Stronghold."),

        L("rwl2", 15, 6, "Gold message 2",
            "One of nine gold messages. This is the second level of the Warrior's Stronghold; stairs down are at (8, 1) on level 1."),

        L("enf2", 10, 4, "Gold message 9",
            "The ninth and final gold message. Combining all nine in their numbered order reveals how to finish the game."),

        L("blackrn", 12, 2, "The prisoner",
            "Every castle holds one prisoner. Show mercy or cruelty — each choice is tallied and affects the " +
            "experience the Scale of Judgement awards in E-1 at (9, 12)."),
        L("blackrn", 9, 5, "Silver message A",
            "One of six silver messages. Re-order all six by Castle Doom's interleave rule to read the six places in Varn " +
            "where statistics can be permanently raised."),

        L("blackrs", 13, 2, "The prisoner",
            "Show mercy or cruelty; the Scale of Judgement in E-1 notes every castle's choice."),
        L("blackrs", 15, 8, "Silver message B",
            "One of six silver messages needed for the stat-boost cipher."),
        L("blackrs", 3, 11, "The slide",
            "Enter this dark area to reach the slide at (11, 11), which deposits the party at the Ancient Ruins " +
            "in the Quivering Forest lair (B-1). The only way in without the Erliquin back door."),

        L("whitew", 12, 4, "The prisoner",
            "Show mercy or cruelty; the Scale of Judgement in E-1 notes every castle's choice."),
        L("whitew", 0, 1, "Silver message C",
            "One of six silver messages. Wolf Castle is Lord Ironfist's domain."),

        L("dragad", 14, 1, "The prisoner",
            "Show mercy or cruelty; the Scale of Judgement in E-1 notes every castle's choice."),
        L("dragad", 10, 3, "Silver message F",
            "One of six silver messages. Castle Dragadune is unusual: it has no lord and multiple levels."),

        L("doom", 1, 14, "The prisoner",
            "Show mercy or cruelty; the Scale of Judgement in E-1 notes every castle's choice."),
        L("doom", 1, 1, "Silver message D",
            "One of six silver messages from the six castles."),

        L("alamar", 2, 2, "The prisoner",
            "Show mercy or cruelty — but have the Eye of Goros from Castle Doom ready. Without it, " +
            "King Alamar sends the party on an impossible quest."),
        L("alamar", 7, 12, "Silver message E",
            "One of six silver messages. Castle Alamar is the sixth and final castle."),
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
