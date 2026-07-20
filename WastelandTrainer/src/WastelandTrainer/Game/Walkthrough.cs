namespace WastelandTrainer.Game;

/// <summary>One condensed strategy section shown on the References ▸ Strategy sub-tab.</summary>
public sealed record WalkthroughSection(string Title, string Body);

/// <summary>
/// A condensed, original-wording strategy summary for Wasteland, surfaced read-only in the trainer.
/// These are paraphrased pointers (party building, skill priorities, and the main-quest route) — not
/// the game's copyrighted manual or paragraph text. The full guide lives in
/// <c>.docs\Wasteland-Strategy-Guide.md</c>. Reference only; drives no memory writes.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<WalkthroughSection> Sections = new WalkthroughSection[]
    {
        new("Getting started",
            "You command a squad of four Desert Rangers (add three recruits met in the world for a full seven). " +
            "Attributes matter most for IQ (unlocks and raises skills), DEX and Luck (initiative and hit chance), " +
            "and Strength (carry weight and melee). Save often — a fresh save before any hard fight or scripted " +
            "event costs nothing and undoes a bad roll."),

        new("Skills to prioritise",
            "Buy Clip Pistol early and raise it — pistols and their clips are the reliable early weapon. " +
            "Perception reveals hidden items, traps and passages; keep at least one high-Perception ranger. " +
            "Picklock, Safecrack, Alarm/Bomb Disarm and Demolition open the world's locked loot. " +
            "Medic then Doctor keep the party alive between towns. Skip Combat Shooting — it is a " +
            "creation-only skill the shipped game never actually reads (series creator Brian Fargo has " +
            "confirmed it does nothing), so put those points into the weapon skills instead. " +
            "Higher-IQ skills (Cryptology, Electronics, Toaster Repair) pay off later at Base Cochise."),

        new("Ranger Center (start)",
            "Home base south of the opening desert. Build and equip the party in the Roster Room, heal at the " +
            "Infirmary, and use the Radio Room to call in from the field for promotions once you have the XP. " +
            "Head south into the desert to reach the first towns."),

        new("Highpool & the Agricultural Center",
            "Highpool: repair the leaking water pipe (Perception + tools) to earn the town's trust and reach the " +
            "cave loot. The Agricultural Center is overrun by mutated plants — clear it, talk to Harry, and pick up " +
            "early upgrades. Both are gentle warm-ups for tougher towns."),

        new("Quartz & Needles",
            "Quartz is a bandit town: work Scott's Bar for contacts, break into the Courthouse to free the hostages, " +
            "and clear Ugly's hideout. Needles hides the blood-cult that stole the Bloodstaff — raid the Temple of " +
            "Blood, and search the Sphinx and Waste Dump for high-tier weapons and a Radiation Suit."),

        new("Las Vegas & Darwin",
            "Las Vegas is the hub to the endgame: chase the robot-investigation leads at Brygo's Palace and find the " +
            "sewer entries that lead down to the Sleeper Base. Darwin is Finster's hidden mutation lab — grab the " +
            "Blackstar Key, survive the Mind Maze, and stop the cloning of your own rangers."),

        new("Guardian Citadel & the endgame",
            "The Guardian Citadel is a fortified monastery — the Main Gate is a brutal choke point, but the Museum " +
            "and Inner Sanctum hold Power Armor and pre-war weapons worth the fight. From the Sleeper Base, gather " +
            "the power converter and plasma coupler, then assault Base Cochise: fight through the Assembly Line, use " +
            "the Core Terminal to trigger self-destruction, and escape before it detonates to win."),

        new("Cheat: rewind a death",
            "Wasteland writes changes to disk permanently, so a dead ranger normally stays dead — but there's a " +
            "built-in escape hatch. If a character dies, do NOT save and do NOT step to a new map (either one commits " +
            "the death). Instead reboot/restart the game and reload your last save or map-entry state; the ranger " +
            "comes back. The catch is you also lose anything gained since that state, so treat it as a safety net, " +
            "not a substitute for saving often."),

        new("Cheat: back up your save",
            "Because the game saves over itself, the simplest \"undo\" is to copy the GAME1/GAME2 save files to another " +
            "folder before a risky fight, a one-way door, or an irreversible trade — then restore the copy if it goes " +
            "wrong. This trainer's Save Editor already keeps a one-time .bak of the original when it first writes, but a " +
            "manual copy of your own gives you as many restore points as you want."),

        new("Cheat: grind skills & money for free",
            "Skills level up through use, not just by spending points, so repeatedly USE a skill where the game lets you " +
            "— pick a lock, disarm the same alarm, apply Medic after a scrape, take shots in a won fight — to raise it " +
            "for nothing. Record a macro (Ctrl+F1..F10) to automate the repetition. For money, keep Gamble high and work " +
            "the casinos, and use Pool on the first character screen to gather everyone's cash before a big purchase, then " +
            "Div Cash to share it back out."),

        new("Cheat: let the trainer do it",
            "The trainer is itself the fastest cheat. Party tab: \"Max Everything\" tops attributes, skills and money for " +
            "the whole squad; \"Freeze Health (CON)\" pins the party to full HP and \"Freeze Ammo\" keeps every weapon and " +
            "clip loaded while you explore. Inventory tab: drop any of the game's items straight into a slot. Create tab: " +
            "the roller auto-taps space until a ranger meets your target stats. Save Editor: teleport to any bookmarked " +
            "spot and fully edit rangers offline — the one place a teleport actually sticks, since the running game only " +
            "reads position from a save on load."),

        new("Trainer tips",
            "The Maps tab shows your live position but can't teleport — Wasteland never reads that value back, so " +
            "there's no position to write (see the README). Freeze CON to stay at full constitution during " +
            "exploration. \"Max\" actions are conservative caps; verify a change in-game (open the character or " +
            "inventory screen) before trusting it, and keep a save from before you started editing."),
    };
}
