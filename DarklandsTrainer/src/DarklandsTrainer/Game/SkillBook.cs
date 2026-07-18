namespace DarklandsTrainer.Game;

/// <summary>One character skill: its stat-screen name, its short combat-dump label, and what it covers.</summary>
public readonly record struct SkillInfo(int Index, string Name, string ShortName, string Governs);

/// <summary>
/// The nineteen character skills, <b>Confirmed</b> from the executable's two parallel skill tables — the
/// long stat-sheet spelling and the short combat-dump spelling (see <c>.docs/ReverseEngineering.md</c>
/// §2.2). Skills are stored as single bytes. The byte→skill mapping inside the save's skills block is
/// still tentative, so the trainer surfaces this table for reference and lets the user pin a skill by
/// value scan rather than writing a fixed offset.
/// </summary>
public static class SkillBook
{
    /// <summary>Number of named skills.</summary>
    public const int SkillCount = 19;

    /// <summary>The nineteen skills, in stat-screen order.</summary>
    public static readonly IReadOnlyList<SkillInfo> Skills = Array.AsReadOnly(new SkillInfo[]
    {
        new( 0, "Edged Wpns",     "Edged",        "Swords, daggers and other edged weapons."),
        new( 1, "Impact Wpns",    "Impact",       "Maces, hammers and clubs."),
        new( 2, "Flail Wpns",     "Flails",       "Flails and chained weapons."),
        new( 3, "Polearm Wpns",   "Polearms",     "Spears, pikes and halberds."),
        new( 4, "Thrown Wpns",    "Thrown",       "Thrown daggers, axes and javelins."),
        new( 5, "Bow Weapons",    "Bow",          "Bows and crossbows."),
        new( 6, "Missile Device", "Mech. Misl",   "Firearms and mechanical missile devices."),
        new( 7, "Alchemy",        "Alchemy",      "Mixing potions from reagents and Philosopher's Stones."),
        new( 8, "Religious Trng", "Religion",     "Knowledge of saints; effectiveness of prayers."),
        new( 9, "Virtue",         "Virtue",       "Moral standing; required to face certain evils."),
        new(10, "Speak Common",   "Speak Common", "Everyday conversation and negotiation."),
        new(11, "Speak Latin",    "Speak Latin",  "Church, scholarly and inscription language."),
        new(12, "Read & Write",   "Read/Write",   "Literacy; reading books, notes and formulae."),
        new(13, "Healing",        "Healing",      "Treating wounds; only the party's highest matters."),
        new(14, "Artifice",       "Artifice",     "Crafting, repair and mechanical devices."),
        new(15, "Stealth",        "Stealth",      "Moving unseen; sneaking and theft."),
        new(16, "Streetwise",     "Streetwise",   "City survival, rumours and the criminal underside."),
        new(17, "Riding",         "Riding",       "Horsemanship; faster map travel."),
        new(18, "Woodwise",       "Woodwise",     "Wilderness survival, foraging and tracking."),
    });
}
