using System.Text;
using GameTrainers.Common.Documents;
using BardsTaleTrilogyTrainer.Game;

namespace BardsTaleTrilogyTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "The Bard's Tale Trilogy — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>The Bard's Tale Trilogy</h1>");
        s.AppendLine("<p class=\"lede\">A reference and strategy guide for the 2018 remaster of Interplay's classic dungeon-crawling trilogy.</p>");
        Contents(s, cluebook.Options);
        Overview(s);
        if (cluebook.Options.IncludeSpells) Spells(s, cluebook.Spells);
        if (cluebook.Options.IncludeClasses) Classes(s, cluebook.Classes);
        if (cluebook.Options.IncludeItems) Items(s, cluebook.Items);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The trilogy at a glance</a></li>");
        if (options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Spell reference</a></li>");
        if (options.IncludeClasses) s.AppendLine("<li><a href=\"#classes\">Classes and races</a></li>");
        if (options.IncludeItems) s.AppendLine("<li><a href=\"#items\">Item catalogue</a></li>");
        if (options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The trilogy at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Edition", "The Bard's Tale Trilogy (2018 remaster)");
        Row(s, "Original games", "The Bard's Tale I, II: The Destiny Knight, and III: Thief of Fate");
        Row(s, "Party", $"Up to {GameFacts.PartySlots} characters");
        Row(s, "Playable classes", ClassBook.Classes.Count.ToString());
        Row(s, "Spell reference", "155 named spell entries");
        Row(s, "Item catalogue", $"{ItemBook.MaxItemId} items");
        s.AppendLine("</table>");

        Game(s, "BT1 — Tales of the Unknown", "Skara Brae has fallen under the spell of Mangar the Dark. Build a party, explore the city and Mangar's Tower, and confront the wizard at its summit.");
        Game(s, "BT2 — The Destiny Knight", "Travel the realm of Tangramayne to recover the Destiny Stone. The journey spans wilderness, cities, dungeons, and the seven realms of the underworld.");
        Game(s, "BT3 — Thief of Fate", "Tarjan has stolen the Destiny Wand and shattered time itself. Pursue him through the realms of time, collect the tools to oppose him, and restore the world.");
        s.AppendLine("<p>The trainer's Maps tab provides the complete area catalogue and supports in-game navigation. This cluebook focuses on durable reference material and progression guidance.</p>");
    }

    private static void Game(StringBuilder s, string title, string text)
    {
        s.AppendLine($"<h3>{E(title)}</h3>");
        s.AppendLine($"<p>{E(text)}</p>");
    }

    private static void Spells(StringBuilder s, IReadOnlyList<SpellReference> spells)
    {
        s.AppendLine("<h2 id=\"spells\">Spell reference</h2>");
        s.AppendLine("<p>The remaster's complete spell descriptions, including four-letter codes, school, spell level, cost, and chapter availability, are loaded from the running game. This offline reference lists the 155 named spell entries carried by the game's spell enum. ZZGO and NUKE are highlighted because they are granted outright rather than earned through a school level.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>ID</th><th>Code</th><th>Spell</th></tr>");
        foreach (var spell in spells)
            s.AppendLine($"<tr><td>{spell.Id}</td><td>{E(spell.Code)}</td><td>{E(spell.Name)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Classes(StringBuilder s, IReadOnlyList<ClassInfo> classes)
    {
        s.AppendLine("<h2 id=\"classes\">Classes and races</h2>");
        s.AppendLine("<h3>Races</h3>");
        s.AppendLine("<p>Human, Elf, Dwarf, Hobbit, Half-Elf, Half-Orc, and Gnome are available throughout the remaster. Select a race that supports the class and party role you want to develop.</p>");
        s.AppendLine("<h3>Classes</h3>");
        s.AppendLine("<table class=\"ref\"><tr><th>Class</th><th>Role</th><th>First game</th><th>Notes</th></tr>");
        foreach (var info in classes)
            s.AppendLine($"<tr><td>{E(info.Name)}</td><td>{E(info.Role.ToString())}</td><td>{E(info.GameTag)}</td><td>{E(info.Description)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<h3>Bard songs</h3><ul>");
        foreach (var song in Spellbook.BardSongs)
            s.AppendLine($"<li>{E(song)}</li>");
        s.AppendLine("</ul>");
    }

    private static void Items(StringBuilder s, IReadOnlyList<ItemBook.ItemChoice> items)
    {
        s.AppendLine("<h2 id=\"items\">Item catalogue</h2>");
        s.AppendLine("<p>Items with charges are never consumed when their live charge value is zero. The trainer can set this value for carried items; use the in-game item descriptions to confirm an item's effects.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>ID</th><th>Item</th><th>Category</th><th>Garth's basic stock</th></tr>");
        foreach (var item in items)
        {
            bool atGarths = ItemBook.GarthShopBasicItems.Contains(item.Id);
            s.AppendLine($"<tr><td>{item.Id}</td><td>{E(item.Name)}</td><td>{E(ItemBook.CategoryOf(item.Id))}</td><td>{(atGarths ? "Yes" : "")}</td></tr>");
        }
        s.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2>");
        WalkthroughGame(s, "BT1 — Skara Brae and Mangar's Tower", new[]
        {
            "Create a balanced group with front-line fighters, a Rogue, a Bard, and at least two spellcasters.",
            "Explore Skara Brae methodically, buy equipment from Garth, and use the Review Board and inn between expeditions.",
            "Learn the tower's routes, conserve spell points for dangerous encounters, and climb Mangar's Tower only after the party can recover from attrition.",
            "Defeat Mangar at the top of the tower to free Skara Brae.",
        });
        WalkthroughGame(s, "BT2 — The Destiny Stone", new[]
        {
            "Carry forward a seasoned BT1 party or establish a new balanced party in Tangramayne.",
            "Use wilderness towns as safe resupply points while collecting the clues and artifacts that open the underworld realms.",
            "Recruit an Archmage once the four basic magic schools have been developed; the promotion is available only in BT2.",
            "Recover the Destiny Stone and complete the final confrontation to secure Tangramayne.",
        });
        WalkthroughGame(s, "BT3 — Thief of Fate", new[]
        {
            "Begin with a capable mixed party and keep a reliable supply of healing, light, detection, and escape magic.",
            "Use time-travel destinations to collect the tools and knowledge required to pursue Tarjan.",
            "Chronomancers and Geomancers become available in this chapter; plan class changes around their different promotion paths.",
            "Face Tarjan only after the party has the required quest tools and enough reserves to survive a long final sequence.",
        });
    }

    private static void WalkthroughGame(StringBuilder s, string title, IEnumerable<string> steps)
    {
        s.AppendLine($"<h3>{E(title)}</h3><ol>");
        foreach (var step in steps) s.AppendLine($"<li>{E(step)}</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        Note(s, "Build for roles", "Keep durable fighters in front, bring a Rogue for traps, and carry enough magic to heal, light, detect, and escape.");
        Note(s, "Map every area", "Use the trainer's Maps tab with the game's own map data to keep track of cities, wilderness, and dungeon routes.");
        Note(s, "Manage spell progression", "School levels grant spells in bulk. Record which schools a character has left: the Review Board does not permit returning to a departed magic school.");
        Note(s, "Use ZZGO carefully", "The Dream Spell is a BT2 travel tool. Its destination must be valid for the chapter currently loaded by the game.");
        Note(s, "Save before commitments", "Class changes, deep-dungeon expeditions, and major quest hand-ins are all safer with a current save.");
        Note(s, "Protect vital items", "Identify and preserve quest items. A party with strong equipment but without a required plot item can still be blocked.");
        Note(s, "Treat NUKE as a reserve", "Gotterdammerung is exceptionally destructive; keep it for encounters where ending the fight immediately is worth the cost.");
        s.AppendLine("</ul>");
    }

    private static void Note(StringBuilder s, string title, string text) =>
        s.AppendLine($"<li><b>{E(title)}.</b> {E(text)}</li>");

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        table.facts th { width: 180px; white-space: nowrap; }
        table.ref { font-size: 0.92em; }
        table.ref tr:nth-child(even) { background: #f8f8f8; }
        """;
}
