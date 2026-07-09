namespace PoolOfRadianceTrainer.Game;

public sealed record WalkthroughSection(string Title, string Body);

/// <summary>
/// A condensed in-app strategy reference. The full guide with maps lives in
/// <c>.docs/strategy-guide.md</c>; this is the quick-reference the trainer surfaces.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<WalkthroughSection> Sections = new List<WalkthroughSection>
    {
        new("The goal",
            "You are adventurers hired by the New Phlan City Council to reclaim the monster-held ruins of " +
            "Old Phlan, district by district. The hidden enemy — 'the Boss' — is Tyranthraxus, a possessing " +
            "spirit inhabiting an ancient bronze dragon in Valjevo Castle. Clear the commissions, gear up, " +
            "and kill the dragon: the spirit is then expelled and drawn back into the draining Pool of Radiance."),

        new("Party creation",
            "Up to 6 PCs (+2 NPC slots). Classic team: 2 fighters, 2 clerics, 1 thief (or Fighter/Thief), " +
            "1+ magic-user. Only Humans & Half-Elves can be clerics; only Humans/Elves/Half-Elves can be mages. " +
            "Roll, then MODIFY to max your prime stat and starting HP (can't be improved later). A male human " +
            "fighter can reach STR 18/00 (+3 to hit, +6 damage). Multiclass HP is averaged, and XP is split."),

        new("Core mechanics",
            "Lower AC and lower THAC0 are better (descending AC). You must TRAIN at Phlan's halls and pay " +
            "1,000 gp per level — leveling is not automatic, and you gain only one level per visit. Memorize " +
            "spells only while Encamped. Gold ~ XP (treasure is a bigger XP source than kills). Monsters that " +
            "flee or are charmed give no XP. Coin weight (10 coins = 1 lb) slows movement — convert up to platinum."),

        new("Combat tactics",
            "SLEEP is the early-game king: no save, and sleeping foes die to one hit — cast it behind the enemy " +
            "front rank. HOLD PERSON and STINKING CLOUD lock down humanoids; FIREBALL clears hordes (uncapped). " +
            "Hold the fighter line anchored to walls; concentrate fire; 1 point of damage interrupts an enemy caster. " +
            "Bows fire twice a round, darts three times. Stand on a slain troll's square so it can't regenerate."),

        new("Recommended order",
            "Slums → Sokal Keep → Kuto's Well → Podol Plaza → Cadorna Textile House → Mendor's Library → " +
            "Kovel Mansion → Wealthy District → Temple of Bane → Wilderness quests → Valhingen Graveyard → " +
            "Valjevo Castle. You CAN walk to the final boss any time, but you'll want levels 6-8 and magic gear."),

        new("Sokal Keep",
            "SEARCH the dead elf at (6,13) for the rune scroll; the code-wheel words are LUX, SAMOSUD, SHESTNI. " +
            "Say SHESTNI to skip patrols inbound. Parley the elven ghosts with LUX (before dealing with Ferran) " +
            "for a diary + 5 gems. Give Ferran Martinez LUX, then tell the TRUTH to complete the mission. Never " +
            "melee Ferran or the spectres — they drain 2 levels per hit."),

        new("Cadorna Textile House",
            "Councilman Cadorna wants his family's iron treasure box (contains the Gauntlets of Ogre Power → " +
            "STR 18/00) and his missing man, Skullcrusher. Opening the box breaks the seal and earns Cadorna's " +
            "enmity (but more XP); returning it sealed keeps him happy. He later betrays you at the Zhentil Keep " +
            "outpost — his Javelin-of-Lightning-carrying commandant is worth killing before the endgame."),

        new("Valhingen Graveyard (late)",
            "You CANNOT rest here — resting triggers escalating undead ambushes. Kill the three spectre 'spawners' " +
            "to stop the skeleton/zombie/wight zones, then destroy the vampire's coffin at (12,4) with holy water " +
            "before fighting him, or he regenerates. Carry Restoration scrolls (level drain) and magic weapons."),

        new("The endgame — Valjevo Castle",
            "Get disguises from the washerwomen; passwords are RHODIA (hedge maze) and TYRANTHRAXUS/HARASH. Beware " +
            "the poison hedge maze (201 damage per hedge). Final fight is two back-to-back battles with no rest: " +
            "12 level-8 fighters (Hold Person + magic weapons), then Tyranthraxus in the bronze dragon. He is " +
            "IMMUNE to all magic — win with melee. USE DUST OF DISAPPEARANCE (from the Temple of Bane): invisible " +
            "characters can't be targeted, denying his ~80-damage lightning breath. Cast Haste, surround, and pummel. " +
            "Protection from Good works (the dragon body is Lawful Good). Say NO if he offers to let you join."),
    };
}
