namespace CurseOfTheAzureBondsTrainer.Game;

public sealed record WalkthroughSection(string Title, string Body);

/// <summary>
/// A condensed in-app strategy reference for Curse of the Azure Bonds. Everything here is drawn
/// from sources that ship with the game — the Rule Book and Adventure Journal bundled as
/// <c>curseazure.pdf</c>, and the game's own data files (its monster archives, its level geometry
/// and its ending text) — rather than from a walkthrough. Where a section is general Gold Box
/// tactics rather than something the game states, it says so.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<WalkthroughSection> Sections = new List<WalkthroughSection>
    {
        new("The goal",
            "Your party came south to Tilverton, on the border between Cormyr and the Dalelands, hunting " +
            "the runaway princess Nacacia of Cormyr and the reward King Azoun had out for her return. You " +
            "were ambushed on the road by attackers who fired crossbows that dropped everyone they hit " +
            "without killing them. You wake in Tilverton a month later: wounds healed, equipment gone, a " +
            "stash of coins in your pockets, and five azure-blue symbols imprinted under the skin of each " +
            "character's sword arm. They are not tattoos, and they sometimes feel like they are moving. " +
            "Each bond can seize control of your actions. The quest is to find who put them there and get " +
            "them off — which means killing the five powers holding them, one per chapter, ending with " +
            "Tyranthraxus. The game's own ending text is explicit about the moment it is over: 'You are " +
            "certain he is destroyed because your final bond fades away.'"),

        new("The five bonds, and the shape of the game",
            "The game is built in five chapters, and the game's own files are organised the same way — " +
            "each chapter is a numbered set of resource archives with its own monster roster, its own " +
            "level geometry and its own item table. The rosters name the chapters outright: Tilverton and " +
            "the Fire Knives; Yulash and the Cultists of Moander, where the priestess Mogion holds a bond; " +
            "Zhentil Keep and the Zhentarim; the mage Dracandros's stronghold among the dark elves; and " +
            "Myth Drannor, where Tyranthraxus the Flamed One waits. Sixteen explorable levels are spread " +
            "across those five, and the Maps tab draws all sixteen from the game's own wall data."),

        new("Starting out",
            "New characters begin with 25,000 experience points and the level that buys — 5th for most " +
            "single-class characters — and 300 platinum pieces each, which the Rule Book states outright " +
            "and which is exactly what the character record holds. A non-human multi-class character " +
            "divides all experience between its classes, so it starts at 12,500 per class and a level " +
            "lower. Spend the platinum on arms and armour before you leave the first town: you were " +
            "robbed of everything, and 300 pp is 1,500 gp, enough for plate and a good weapon. " +
            "Characters can also be imported from Pool of Radiance and from Hillsfar; remove them from " +
            "their old party before transferring."),

        new("Party building",
            "Up to six characters. Only humans and half-elves can be clerics, and only humans can be " +
            "paladins; magic-users must be human, elf or half-elf. The racial level limits matter more " +
            "here than in the first game because Curse runs to level 12 — a dwarf fighter stops at 9 with " +
            "18 Strength, a halfling at 5, while a human fighter reaches 12. Thieves have no racial limit " +
            "at all. Curse reaches fifth-level spells on both lists, so a cleric is worth taking to 9 for " +
            "Raise Dead and a mage to 9 for Cloudkill and Cone of Cold. General tactics: two front-line " +
            "fighters, a cleric, a mage and a thief is a reliable spread."),

        new("Spells that decide fights",
            "Sleep ends most early encounters outright — up to sixteen one-hit-die targets, no saving " +
            "throw, nothing of five hit dice or more affected. Once you have third-level spells, Haste " +
            "doubles movement and melee attacks and is the strongest buff in the game, and Fireball and " +
            "Lightning Bolt clear rooms — mind the radius, and mind that a bolt rebounds off walls and can " +
            "come back through your own party. Prayer is a two-point swing (your side +1, theirs -1). " +
            "Hold Person paralyses up to three targets for a cleric and four for a mage; a held target is " +
            "trivial to finish. Silence 15' Radius shuts down enemy casters. Keep Cure Light Wounds " +
            "memorized in every spare slot until you have Cure Serious."),

        new("Money, training and healing",
            "Training costs 1,000 gold per level, so bank experience and train in town rather than " +
            "carrying it. Coins weigh: the encumbrance figure on a character record counts them, and 300 " +
            "platinum on its own is 300 units of weight. Convert bulk coin to gems and jewelry when you " +
            "can — the record keeps separate counters for both. Temples cure blindness, disease, poison, " +
            "curses and petrification and raise the dead, and they charge for it; elves cannot be raised."),

        new("The bestiary is the map",
            "If you want to know what a chapter holds, read the Monsters tab: it is decoded straight from " +
            "the game's own archives, so the creatures listed against a chapter are exactly the ones that " +
            "chapter can throw at you. The richest kills in the game are the dracolich (13,200 XP), the " +
            "beholder (12,900) and the Bit o' Moander (11,500); Tyranthraxus himself pays 5,850."),

        new("Using this trainer",
            "God mode freezes party hit points each poll tick and survives a whole battle. The combat " +
            "panel finds enemy records by itself while a fight is on screen. Weaken is the loot-safe way " +
            "to win a fight — it leaves the enemy alive on 1 HP with AC and THAC0 20 so your next blow " +
            "lands and kills, which means the game runs its own death routine and you still get the " +
            "treasure and the experience. Killing a monster by zeroing its record does not: the engine " +
            "never processes the death, the fight ends in a surrender, and a surrender pays nothing."),

        new("Restoring drained abilities",
            "Curse stores every ability score twice — the value in play and the maximum it was rolled at " +
            "— so a Ray of Enfeeblement, a shadow or a Feeblemind shows up in the record as the two " +
            "halves disagreeing. The Character tab flags a drained character and can put every score back " +
            "to its stored maximum, which is what a Restoration would have done. Editing a score writes " +
            "both halves, so a later restore cannot quietly undo your edit."),
    };
}
