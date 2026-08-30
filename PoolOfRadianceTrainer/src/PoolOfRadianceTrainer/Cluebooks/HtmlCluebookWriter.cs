using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Pool of Radiance — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        s.AppendLine("<h1>Pool of Radiance</h1>");
        s.AppendLine("<p class=\"lede\">A cluebook for the 1988 SSI/TSR Gold Box classic: reclaim Phlan, complete the city's commissions, and destroy the Pool of Radiance.</p>");
        Contents(s, cluebook);
        Overview(s);
        if (cluebook.Options.IncludeMaps) Areas(s, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(s);
        if (cluebook.Options.IncludeClasses) Classes(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);
        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (c.Options.IncludeMaps) s.AppendLine("<li><a href=\"#areas\">Phlan and the surrounding areas</a></li>");
        if (c.Options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Mage and cleric spells</a></li>");
        if (c.Options.IncludeClasses) s.AppendLine("<li><a href=\"#classes\">Classes and races</a></li>");
        if (c.Options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (c.Options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Setting", "Phlan, near the Moonsea in the Forgotten Realms");
        Row(s, "Goal", "Reclaim the ruined city and destroy the Pool of Radiance");
        Row(s, "Party", "Up to six adventurers, with optional hired NPCs");
        Row(s, "Rules", "AD&D 1st edition adapted for the Gold Box engine");
        Row(s, "Developer", "Strategic Simulations, Inc. (SSI), 1988");
        Row(s, "Trainer maps", $"{MapBook.Areas.Count} keyed areas, including districts, lairs, and wilderness locations");
        s.AppendLine("</table>");
        s.AppendLine("<h3>The story</h3>");
        s.AppendLine("<p>Phlan was once a prosperous city on the Moonsea. Monsters now occupy its districts, while the surviving citizens hold a small civilized section. The City Council hires your party to reclaim the ruins, investigate the force behind the invasion, and restore Phlan one commission at a time.</p>");
        s.AppendLine("<p>The hidden enemy is Tyranthraxus, a possessing spirit using an ancient bronze dragon as its vessel. Follow the Council's leads through the monster-held districts, gather the equipment and knowledge needed for the endgame, then reach the Pool of Radiance and destroy the artifact that sustains the evil.</p>");
    }

    private static void Areas(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"areas\">Phlan and the surrounding areas</h2>");
        s.AppendLine("<p>The Maps tab in the trainer contains the same area reference and decoded indoor geometry. Coordinates use (x, y), with x increasing east and y increasing south.</p>");
        foreach (var area in c.Areas)
        {
            s.AppendLine($"<h3 id=\"area-{Slug(area.Name)}\">{E(area.Name)} <span class=\"dim\">({E(area.Size)})</span></h3>");
            s.AppendLine($"<p>{E(area.Notes)}</p>");
            if (area.Terrain != null) s.AppendLine(MapSvg(area, c.Options.MapCellSize));
            if (area.Locations.Count == 0) continue;
            s.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Location</th><th>Notes</th></tr>");
            foreach (var location in area.Locations)
                s.AppendLine($"<tr><td>{E(location.Coord)}</td><td>{E(location.Name)}</td><td>{E(location.Notes)}</td></tr>");
            s.AppendLine("</table>");
        }
    }

    private static string MapSvg(MapArea area, int cell)
    {
        int width = area.Width * cell + 12;
        int height = area.Height * cell + 12;
        var svg = SvgCanvas.Responsive(width, height, $"{area.Name} map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        if (area.Terrain != null)
        {
            for (int y = 0; y < area.Height; y++)
                for (int x = 0; x < area.Width; x++)
                {
                    var square = area.Terrain[x, y];
                    string fill = square.Floor switch
                    {
                        FloorKind.Water or FloorKind.DeepWater => "#263D5C",
                        FloorKind.Stone => "#555866",
                        FloorKind.Plains => "#334D35",
                        FloorKind.Swamp => "#3E4930",
                        FloorKind.Forest => "#243F2A",
                        FloorKind.Hills or FloorKind.Mountains => "#504637",
                        FloorKind.River => "#28506B",
                        FloorKind.Unknown => "#111217",
                        _ => "#24262D"
                    };
                    svg.Rect(6 + x * cell, 6 + y * cell, cell, cell, ("fill", fill));
                    if (square.West != WallKind.None) svg.Rect(6 + x * cell, 6 + y * cell, 2, cell, ("fill", "#B9BCC6"));
                    if (square.North != WallKind.None) svg.Rect(6 + x * cell, 6 + y * cell, cell, 2, ("fill", "#B9BCC6"));
                }
        }
        foreach (var location in area.Locations)
        {
            int x = 6 + location.X * cell;
            int y = 6 + location.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", "#C89B3C"));
            svg.Text(x + cell / 2.0, y + cell * 0.7, "•", ("text-anchor", "middle"), ("font-size", (cell * 0.7).ToString(CultureInfo.InvariantCulture)), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static void Spells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"spells\">Mage and cleric spells</h2>");
        s.AppendLine("<p>Pool of Radiance uses separate memorized-spell slots for clerics and magic-users. The list below follows the trainer's verified record order.</p>");
        SpellTable(s, "Cleric spells", SpellBook.All.Where(x => x.School == "Cleric"));
        SpellTable(s, "Mage spells", SpellBook.All.Where(x => x.School == "Mage"));
    }

    private static void SpellTable(StringBuilder s, string title, IEnumerable<SpellInfo> spells)
    {
        s.AppendLine($"<h3>{E(title)}</h3><table class=\"ref\"><tr><th>Level</th><th>Spell</th><th>Effect</th></tr>");
        foreach (var spell in spells)
            s.AppendLine($"<tr><td>{spell.Level}</td><td>{E(spell.Name)}</td><td>{E(spell.Description)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Classes(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"classes\">Classes and races</h2>");
        s.AppendLine("<h3>Classes</h3><table class=\"ref\"><tr><th>Class</th><th>Hit die</th><th>Prime stat</th><th>Cap</th><th>Notes</th></tr>");
        foreach (var item in ClassRaceBook.Classes)
            s.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.HitDie)}</td><td>{E(item.PrimeStat)}</td><td>{item.GameCap}</td><td>{E(item.Notes)}</td></tr>");
        s.AppendLine("</table><h3>Races</h3><table class=\"ref\"><tr><th>Race</th><th>Class options</th><th>Notes</th></tr>");
        foreach (var item in ClassRaceBook.Races)
            s.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.ClassOptions)}</td><td>{E(item.Notes)}</td></tr>");
        s.AppendLine("</table><h3>Alignment</h3><p>Lawful Good, Lawful Neutral, Lawful Evil, Neutral Good, True Neutral, Neutral Evil, Chaotic Good, Chaotic Neutral, and Chaotic Evil are represented in the character record.</p>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        Step(s, "Establish the party", "Create a balanced six-person party. Include a front-line fighter, a cleric for healing and turning undead, a thief for traps, and at least one magic-user for Sleep and area spells.");
        Step(s, "Reclaim the first districts", "Begin with the Slums, then Sokal Keep and Kuto's Well. Report completed commissions to the Council, train at the appropriate hall, and return to Phlan to heal, identify treasure, and restock.");
        Step(s, "Investigate the conspirators", "Work through Podol Plaza, Mendor's Library, Kovel Mansion, and the Cadorna Textile House. Use Knock and Find Traps, search thoroughly, and preserve clues that identify the Boss and the route to the castle.");
        Step(s, "Break the enemy's strongholds", "Clear the Wealthy District and Temple of Bane. Collect the holy symbols needed to enter Bane's temple and keep valuable artifacts, scrolls, and Dust of Disappearance for later missions.");
        Step(s, "Complete the wilderness commissions", "Travel beyond Sokal Keep for the nomad, Valjevo, Zhentil, and other wilderness encounters. These missions provide powerful equipment and the levels needed for the undead districts and final castle.");
        Step(s, "Clear Valhingen Graveyard", "Save this commission for a strong party. Kill the spectre groups that block the vampire, destroy the vampire's coffin, then defeat the vampire again. Carry Restoration scrolls and use magic weapons against level-draining undead.");
        Step(s, "Reach Valjevo Castle", "Use the washerwomen's disguises and the passwords RHODIA, TYRANTHRAXUS, and HARASH to pass the approach. Survive the poison hedge maze and do not spend the party's best consumables before the final sequence.");
        Step(s, "Destroy the Pool", "Fight the castle defenders, then face Tyranthraxus in the bronze dragon. He is immune to normal spell attacks, so protect the party, use Dust of Disappearance to deny his lightning breath, and win with enchanted weapons. Refuse his offer to join, then destroy the Pool of Radiance.");
        s.AppendLine("</ol>");
    }

    private static void Step(StringBuilder s, string title, string body) => s.AppendLine($"<li><b>{E(title)}.</b> {E(body)}</li>");

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        Note(s, "Training matters", "Experience does not automatically grant a level. Return to the correct Training Hall with 1,000 gp per level and train one level at a time.");
        Note(s, "Treasure is experience", "Gold from treasure is a major source of experience. Monsters that flee or are charmed do not award normal experience, so do not let every encounter escape.");
        Note(s, "Use the right spell", "Sleep is decisive early, Hold Person disables humanoids, Stinking Cloud controls doorways, and Fireball clears large groups. Memorize Cure Light Wounds and save scrolls for emergencies.");
        Note(s, "Manage equipment", "Identify magic items, convert excess coin into portable forms, and keep silver or enchanted weapons available for creatures that resist ordinary steel.");
        Note(s, "Treat undead seriously", "Level drain is permanent until restored. Bring Restoration scrolls, use cleric protections, and do not rest in Valhingen Graveyard while it remains uncleared.");
        Note(s, "Keep a backup", "Save before commissions, boss encounters, and risky treasure searches. The trainer's live edits are separate from the game's own save process, so save from the game after confirming changes.");
        s.AppendLine("</ul>");
    }

    private static void Note(StringBuilder s, string title, string body) => s.AppendLine($"<li><b>{E(title)}.</b> {E(body)}</li>");
    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);
    private static string Slug(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.9em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { font-size: 1.45em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { margin-top: 1.5em; } .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table { border-collapse: collapse; width: 100%; margin: 1em 0; } th { background: #e8e8e8; text-align: left; }
        th, td { padding: 4px 8px; border: 1px solid #ccc; vertical-align: top; } th { white-space: nowrap; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; image-rendering: pixelated; }
        .dim { font-size: .8em; color: #777; font-weight: normal; } li { margin-bottom: .6em; }
        """;
}
