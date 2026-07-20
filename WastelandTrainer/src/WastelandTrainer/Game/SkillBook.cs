namespace WastelandTrainer.Game;

/// <summary>
/// A Wasteland skill: its in-record id, name, the IQ needed to learn its first level, a short
/// paraphrase of what it does in play (<see cref="Use"/>), and where in the game it's notably used
/// (<see cref="Where"/>).
/// </summary>
public sealed record SkillInfo(int Id, string Name, int MinIq, string Use = "", string Where = "")
{
    /// <summary>The IQ gate to learn the skill's first level, phrased for display.</summary>
    public string Requirement => MinIq > 0 ? $"Requires IQ {MinIq} to learn." : "Learnable at any IQ.";

    /// <summary>What it does, where it's used, and its IQ gate — the row tooltip and reference text.</summary>
    public string Description => string.Join("  ", new[]
    {
        Use,
        string.IsNullOrEmpty(Where) ? "" : $"Where used: {Where}",
        Requirement,
    }.Where(s => !string.IsNullOrEmpty(s)));
}

/// <summary>
/// Reference table of Wasteland's 35 skills, keyed by the skill id stored in the packed skill list
/// at <see cref="CharacterFormat.OffSkills"/>. Ids and names were confirmed by decoding the four
/// dumped party members and matched against <c>.game\manual.txt</c>; the minimum-IQ groupings come
/// from the manual's skill listing, and the <see cref="SkillInfo.Use"/> blurbs paraphrase the manual's
/// skill functions (also summarised in <c>.docs\Wasteland-Strategy-Guide.md</c>). The
/// <see cref="SkillInfo.Where"/> notes are drawn from community walkthroughs (GameFAQs/archive.org
/// walkthroughs, the Wasteland fan wikis); combat/weapon skills apply in any fight, so those are marked
/// "not tied to one spot" rather than given an invented location, and the thinly-documented skills
/// (Alarm Disarm, Sleight of Hand) are kept deliberately narrow. The trainer edits skills by id; the
/// descriptions are reference only.
/// </summary>
public static class SkillBook
{
    public static readonly IReadOnlyList<SkillInfo> Skills = new SkillInfo[]
    {
        new(1,  "Brawling",         0,  "More unarmed attacks per round in hand-to-hand combat.",
            "Any unarmed fight; the fist-fights at the Rail Nomads' Camp are a known XP grind spot."),
        new(2,  "Climb",            0,  "Scale fences, cliff faces, and pits that would otherwise block the way.",
            "Terrain obstacles across the world — the hidden cave at Highpool, a shaft into the Las Vegas casino, the well at Savage Village, and the Quartz courthouse roof."),
        new(3,  "Clip Pistol",      0,  "Aim, load, and clear jams on the starting .45 and 9mm clip pistols; the reliable early weapon skill.",
            "Any pistol fight, from the opening desert onward — not tied to one spot."),
        new(4,  "Knife Fight",      0,  "Sharper accuracy and damage with knives at melee range.",
            "Any close-quarters fight with a blade — not tied to one spot."),
        new(5,  "Pugilism",         0,  "Trained boxing — land and slip punches better than plain Brawling.",
            "Any unarmed fight (pairs with Brawling); same Rail Nomads' Camp fist-fight grind."),
        new(6,  "Rifle",            0,  "Accurate single-fire with bolt/semi-auto rifles such as the M19.",
            "Any ranged fight — not tied to one spot."),
        new(7,  "Swim",             0,  "Cross deep water instead of being turned back by it.",
            "Water crossings — the lake in the Needles temple and the Las Vegas sewers."),
        new(8,  "Knife Throw",      0,  "Throw knives effectively at short range.",
            "Ranged combat, and one way to satisfy the forced stage act in Scott's Bar, Quartz."),
        new(9,  "Perception",       10, "Spot concealed items, traps, and hidden passages — keep at least one ranger high in it.",
            "Everywhere, on your lead ranger — finds the hidden cave at Highpool, searches bodies and drawers, and spots mines and pressure plates at Las Vegas and Base Cochise."),
        new(10, "Assault Rifle",    10, "Fire, load, and unjam AK-97 / M1989A1-class selective-fire rifles.",
            "Most mid- and late-game fights — not tied to one spot."),
        new(11, "AT Weapon",        10, "Recognise and fire anti-tank rockets such as the LAW and RPG-7.",
            "Armoured foes and robots at Guardian Citadel and Base Cochise; loading the Needles howitzer can grind it."),
        new(12, "SMG",              10, "Control burst and auto fire on the Uzi and MAC-17 in closer firefights.",
            "Any firefight — not tied to one spot."),
        new(13, "Acrobat",          10, "Agile escapes, tumbling, and dodging environmental hazards.",
            "Dodging feats; grindable on the Needles sand dunes and one way to pass the Scott's Bar stage act in Quartz."),
        new(14, "Gamble",           11, "Better odds at the casinos and a chance to spot a crooked game.",
            "The casinos — Las Vegas (Fat Freddy's, Spade's) and the desert nomad casino car."),
        new(15, "Picklock",         11, "Open locked doors and containers without the key.",
            "Locked doors and cabinets almost everywhere — the Agricultural Center, the Quartz and Needles jails and vaults, Las Vegas, Darwin, the Sleeper Base, and Base Cochise."),
        new(16, "Silent Move",      11, "Slip past guards and pursuers without being heard.",
            "Slipping past guards — the Quartz courthouse (to reach Dan Citrine) and the Las Vegas casino and hideout."),
        new(17, "Combat Shooting",  12, "A creation-only skill the shipped game never reads — it does nothing, so spend the points elsewhere.",
            "Nowhere — the shipped game never reads it, so it has no effect anywhere."),
        new(18, "Confidence",       12, "Bluff and persuade NPCs into talking or backing down.",
            "Dialogue with wary or greedy NPCs (pairs with Charisma); also one way to pass the Scott's Bar stage act in Quartz."),
        new(19, "Sleight of Hand",  12, "Pickpocket and pull off other thieving tricks.",
            "Rarely used; the clearest spot is entertaining the crowd in Scott's Bar, Quartz."),
        new(20, "Demolition",       13, "Judge and set explosives on obstacles without blowing up the party.",
            "Explosives use throughout; grindable by loading the Needles howitzer."),
        new(21, "Forgery",          13, "Spot forged documents and fake your own passes.",
            "Faking documents — passing the 'Webs of Lies' in Finster's Mind Maze at Darwin (a high IQ also works)."),
        new(22, "Alarm Disarm",     14, "Find and shut off alarm systems before they trigger.",
            "Shutting off alarms; thinly used — the one documented spot is in Needles."),
        new(23, "Bureaucracy",      14, "Talk your way past officials and bureaucratic red tape.",
            "Talking past officials (pairs with Charisma) — e.g. escaping the showers in Finster's Mind Maze at Darwin."),
        new(24, "Bomb Disarm",      15, "Defuse bombs, mines, and booby traps.",
            "Booby-trapped containers and bombs — Quartz (Stagecoach Inn, Ugly's hideout), the Needles ammo bunker, the Savage Village chests, and the Sleeper Base self-destruct."),
        new(25, "Medic",            15, "Field first aid that stabilises a badly wounded ranger before the wound turns fatal.",
            "Constantly, in the field everywhere — not a location puzzle."),
        new(26, "Safecrack",        16, "Open locked safes for their contents.",
            "Safes and vault doors — Ugly's hideout and the courthouse in Quartz, the Las Vegas casino, and the Sleeper Base safe holding a plasma coupler."),
        new(27, "Cryptology",       17, "Decode messages, ciphers, and computer passwords.",
            "Coded locks — Faran Brygo's hidden vault in Las Vegas and the cipher system at Base Cochise (a high IQ can sub for it)."),
        new(28, "Metallurgy",       18, "Identify metals and alloys; needed for a few late-game tech puzzles.",
            "The Needles mine — work the silver vein with a pick ax to dig out cash."),
        new(29, "Helicopter Pilot", 18, "Fly a helicopter — required to reach a couple of otherwise cut-off areas.",
            "Exactly once — start the chopper in the Guardian Citadel inner sanctum to fly to Base Cochise."),
        new(30, "Electronics",      19, "Repair and operate electronic gear, robots, and terminals.",
            "Electronic locks and panels — the Las Vegas Mushroom Church doors and the Base Cochise security center."),
        new(31, "Toaster Repair",   20, "Fix broken toasters — a running gag that actually hides a real cache.",
            "The workbench in the Guardian Citadel — repairing toasters there yields power packs and a plasma coupler."),
        new(32, "Doctor",           21, "Advanced medicine — heals serious wounds Medic can't and revives the near-dead.",
            "Serious wounds and poison in the field everywhere; notably cures the poisoned NPCs at Darwin."),
        new(33, "Clone Tech",       22, "Operate the cloning equipment found in the late-game labs.",
            "Only at the Sleeper Base — running the clone-fluid machine (its skill book is behind a Secpass-3 door there)."),
        new(34, "Energy Weapon",    23, "Wield laser and ion energy weapons, the strongest guns in the game.",
            "Late-game energy guns, mainly against the robots at Guardian Citadel and Base Cochise."),
        new(35, "Cyborg Tech",      24, "Understand and exploit cyborg technology at the endgame bases.",
            "Jacking into Finster's cyborg head at Darwin to enter the Mind Maze."),
    };

    private static readonly Dictionary<int, SkillInfo> ById =
        Skills.ToDictionary(s => s.Id);

    public static string SkillName(int id) =>
        ById.TryGetValue(id, out var s) ? s.Name : $"Skill #{id}";

    public static SkillInfo? Find(int id) => ById.TryGetValue(id, out var s) ? s : null;
}
