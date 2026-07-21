namespace ColonizationTrainer.Game;

/// <summary>A titled block of condensed strategy text for the References ▸ Strategy sub-tab.</summary>
public sealed record StrategySection(string Title, string Body);

/// <summary>
/// A condensed digest of <c>docs/Colonization-Strategy-Guide.md</c> for the in-app References tab, so
/// a player never has to leave the trainer to remember how to win. Not a substitute for the full
/// guide.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<StrategySection> Sections = new[]
    {
        new StrategySection("The goal",
            "There is one win condition: declare independence and defeat the King's army. Grow " +
            "colonies, print Liberty Bells until your Sons of Liberty is at least 50%, then Game menu " +
            "(Alt-G) ▸ DECLARE INDEPENDENCE. You must win the war by 1850 or you lose."),

        new StrategySection("Economy",
            "Refine raw goods before selling: Sugar→Rum, Tobacco→Cigars, Cotton→Cloth, Furs→Coats, " +
            "Ore→Tools→Muskets. Each chain has a building tier and a specialist who doubles output. " +
            "Silver is sold raw. A Custom House (needs Peter Stuyvesant) auto-exports even during the " +
            "revolution."),

        new StrategySection("Liberty Bells & Sons of Liberty",
            "Bells come from the Town Hall (boost it with Printing Press → Newspaper, staff it with " +
            "Elder Statesmen). ~200 bells per colonist gives 100% support. Founding Fathers Jefferson " +
            "(+50% bells), Paine (+bells = tax rate) and Bolívar (+20% SoL) accelerate it. Push toward " +
            "100% before you declare — leftover Tory unrest becomes an attack bonus for the King."),

        new StrategySection("Founding Fathers",
            "Liberty Bells elect them in the Continental Congress (F3), one at a time, each costing " +
            "more than the last. 25 of them, five per category (Trade, Exploration, Military, " +
            "Political, Religious). Adam Smith unlocks factories; Washington auto-promotes winners; " +
            "Magellan speeds ships; Fugger clears boycotts."),

        new StrategySection("Combat",
            "One roll of attack vs. defense with modifiers. Fortifications give +100/150/200% " +
            "(Stockade/Fort/Fortress); Fortify adds +50%. Losers demote a step (Dragoon→Soldier→" +
            "Colonist) rather than dying outright. Dragoons are the best attackers; Artillery (7/5) " +
            "wrecks colonies but is weak in the open and degrades permanently when it loses."),

        new StrategySection("Taxes & boycotts",
            "The King periodically demands a tax hike. Refuse and hold a 'party' to dump the named " +
            "good and start a boycott (blocks Europe trade in it) instead of paying — lift a boycott " +
            "with 500× the good's price, or get Jakob Fugger. Thomas Paine turns your tax rate into " +
            "bonus bells, so high tax isn't purely bad."),

        new StrategySection("Nations",
            "England: more immigration. France: peaceful natives (+ a Hardy Pioneer). Spain: +50% vs. " +
            "native settlements (+ a Veteran Soldier). Netherlands: better prices and a 4-hold " +
            "Merchantman. Difficulty runs Discoverer → Viceroy; higher means a bigger King's army and " +
            "fewer Tories tolerated before a production penalty."),
    };
}
