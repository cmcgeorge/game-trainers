using System.Text;
using GameTrainers.Common.Documents;
using CurseOfTheAzureBondsTrainer.Game;

namespace CurseOfTheAzureBondsTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Curse of the Azure Bonds — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        s.AppendLine("<h1>Curse of the Azure Bonds</h1>");
        s.AppendLine("<p class=\"lede\">A reference for the 1989 SSI/TSR Gold Box adventure across the Dalelands, with area maps, spells, character guidance, and a route through the five azure bonds.</p>");
        Contents(s, cluebook);
        Overview(s);
        Areas(s, cluebook);
        if (cluebook.Options.IncludeMaps) Maps(s, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(s, cluebook);
        if (cluebook.Options.IncludeClasses) Classes(s, cluebook);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s, cluebook);
        if (cluebook.Options.IncludeStrategy) Strategy(s);
        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        s.AppendLine("<li><a href=\"#areas\">Areas and key locations</a></li>");
        if (c.Options.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
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
        Row(s, "Title", "Curse of the Azure Bonds");
        Row(s, "Publisher", "Strategic Simulations, Inc. / TSR");
        Row(s, "Year", "1989");
        Row(s, "Setting", "The Dalelands of the Forgotten Realms");
        Row(s, "Party", "Up to six adventurers");
        Row(s, "Rules", "AD&D second edition Gold Box rules");
        Row(s, "Adventure", "Five azure bonds, multiple wilderness and dungeon areas, and a final battle");
        s.AppendLine("</table>");
        s.AppendLine("<h3>The azure bonds</h3>");
        s.AppendLine("<p>Your party wakes in Tilverton after an ambush, stripped of its equipment and marked with five azure-blue bonds. The bonds are magical compulsions: their makers can seize control of the party and force it to act against its will. Follow the clues through the Dalelands, confront the powers behind each bond, and break them one by one before facing the force that binds the final mark.</p>");
    }

    private static void Areas(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"areas\">Areas and key locations</h2>");
        s.AppendLine("<p>The game moves between outdoor travel, city encounters, and tactical dungeon maps. The area labels below identify the module and the role each place plays in the adventure.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Area</th><th>Role</th><th>Key locations</th></tr>");
        var rows = new[]
        {
            ("Tilverton", "Opening city and the party's first safe base", "Streets, the Pit, and the sewers; question the locals and prepare before travelling"),
            ("Yulash", "Ruined frontier town contested by rival forces", "Ruins and tunnels; meet Alias and Dragonbait and follow the trail to Moander"),
            ("Temple of Moander", "Cult stronghold and one bond-holder's lair", "The temple; defeat Mogion to break her bond"),
            ("Zhentil Keep", "Stronghold of the Zhentarim", "Streets, upper level, and dungeon; expect powerful casters and monsters"),
            ("Dracandros's stronghold", "Mage's grounds, tower, and vault", "Grounds, tower, and vault; Dracandros and the dracolich guard the route"),
            ("Myth Drannor", "The ruined elven city and endgame chapter", "Outer and inner ruins, catacombs, and the sanctum; Tyranthraxus awaits"),
        };
        foreach (var row in rows) s.AppendLine($"<tr><td>{E(row.Item1)}</td><td>{E(row.Item2)}</td><td>{E(row.Item3)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Maps(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"maps\">Area maps</h2>");
        s.AppendLine("<p>These schematics use the game's decoded sixteen-by-sixteen area geometry. North is up, west is left, and coordinates are (x, y) with the origin at the north-west corner. Doors and secret passages are shown with distinct edge colours.</p>");
        s.AppendLine("<ul class=\"legend\"><li><span class=\"swatch wall\"></span>Wall or unreachable square</li><li><span class=\"swatch floor\"></span>Walkable floor</li><li><span class=\"swatch door\"></span>Door</li><li><span class=\"swatch secret\"></span>Secret passage</li></ul>");
        foreach (var area in c.Areas)
        {
            s.AppendLine($"<h3>{E(area.Name)} <small>{E(area.Geo)}</small></h3>");
            s.AppendLine($"<p>{E(area.Notes)}</p>");
            if (area.Terrain != null) s.AppendLine(AreaSvg(area, c.Options.MapCellSize));
            if (area.Locations.Count > 0)
            {
                s.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Location</th><th>Notes</th></tr>");
                foreach (var location in area.Locations) s.AppendLine($"<tr><td>{E(location.Coord)}</td><td>{E(location.Name)}</td><td>{E(location.Notes)}</td></tr>");
                s.AppendLine("</table>");
            }
        }
    }

    private static string AreaSvg(MapArea area, int cell)
    {
        int pad = 12;
        int width = area.Width * cell + pad * 2;
        int height = area.Height * cell + pad * 2;
        var svg = SvgCanvas.Responsive(width, height, $"{area.Name} map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
            {
                var square = area.Terrain![x, y];
                string fill = square.Floor == FloorKind.Stone ? "#454957" : square.Floor == FloorKind.Water ? "#193B59" : "#20232B";
                double left = pad + x * cell;
                double top = pad + y * cell;
                svg.Rect(left, top, cell, cell, ("fill", fill));
                Edge(svg, left, top, cell, square.West, true);
                Edge(svg, left, top, cell, square.North, false);
                if (x == area.Width - 1) Edge(svg, left, top, cell, square.East, true);
                if (y == area.Height - 1) Edge(svg, left, top, cell, square.South, false);
            }
        return svg.ToSvg();
    }

    private static void Edge(SvgCanvas svg, double left, double top, int cell, WallKind wall, bool vertical)
    {
        if (wall == WallKind.None) return;
        string color = wall switch { WallKind.Door => "#6FC276", WallKind.SecretDoor => "#D4A64A", _ => "#A7ACBA" };
        if (vertical) svg.Rect(left, top, 2, cell, ("fill", color));
        else svg.Rect(left, top, cell, 2, ("fill", color));
    }

    private static void Spells(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"spells\">Mage and cleric spells</h2>");
        s.AppendLine("<p>Curse reaches fifth-level spells in both the mage and cleric lists. Rangers use the small druid list shown in the reference data; the tables below focus on the two principal spellcasting classes.</p>");
        SpellTable(s, "Cleric spells", c.Spells.Where(x => x.School == "Cleric"));
        SpellTable(s, "Mage spells", c.Spells.Where(x => x.School == "Mage"));
    }

    private static void SpellTable(StringBuilder s, string title, IEnumerable<SpellInfo> spells)
    {
        s.AppendLine($"<h3>{E(title)}</h3><table class=\"ref\"><tr><th>Level</th><th>Spell</th><th>Effect</th></tr>");
        foreach (var spell in spells) s.AppendLine($"<tr><td>{spell.Level}</td><td>{E(spell.Name)}</td><td>{E(spell.Description)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Classes(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"classes\">Classes and races</h2><h3>Classes</h3>");
        s.AppendLine("<table class=\"ref\"><tr><th>Class</th><th>Hit die</th><th>Prime stat</th><th>Cap</th><th>Notes</th></tr>");
        foreach (var item in c.Classes) s.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.HitDie)}</td><td>{E(item.PrimeStat)}</td><td>{item.GameCap}</td><td>{E(item.Notes)}</td></tr>");
        s.AppendLine("</table><h3>Races</h3><table class=\"ref\"><tr><th>Race</th><th>Class options</th><th>Notes</th></tr>");
        foreach (var item in c.Races) s.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.ClassOptions)}</td><td>{E(item.Notes)}</td></tr>");
        s.AppendLine("</table><p>Multi-class characters share experience between their classes. Build around the party's roles: front-line fighters, a cleric for healing and protection, a mage for area damage and control, and a thief for locks and traps.</p>");
    }

    private static void Walkthrough(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        foreach (var section in c.Walkthrough) s.AppendLine($"<li><b>{E(section.Title)}.</b> {E(section.Body)}</li>");
        s.AppendLine("<li><b>Break the bonds.</b> Follow the clues from Tilverton into Yulash and the Temple of Moander, then through Zhentil Keep and Dracandros's stronghold. Each chapter identifies the power holding a bond; defeat that power before moving on.</li>");
        s.AppendLine("<li><b>Enter Myth Drannor.</b> Search the outer ruins, inner ruins, and catacombs for the path into the sanctum. Keep healing and protective spells ready for the final approach.</li>");
        s.AppendLine("<li><b>Win the final battle.</b> Face Tyranthraxus in the sanctum. Once he is defeated, the final azure bond fades and the adventure is complete.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        s.AppendLine("<li><b>Prepare before travel.</b> Heal, memorize spells, identify and equip loot, and carry enough money for training and temple services.</li>");
        s.AppendLine("<li><b>Use the right control spell.</b> Sleep dominates early fights; Haste, Prayer, Hold Person, Silence, Fireball, and Lightning Bolt remain valuable throughout the campaign.</li>");
        s.AppendLine("<li><b>Protect the casters.</b> Keep the mage and cleric behind the front line, and use Protection from Evil, defensive positioning, and area spells before enemies close.</li>");
        s.AppendLine("<li><b>Save often.</b> The game mixes wilderness travel with dangerous tactical encounters. Keep multiple saved parties so a bond-controlled or badly drained party can be recovered.</li>");
        s.AppendLine("<li><b>Watch ability drains.</b> Curse stores current and maximum ability scores separately. Restoration and the trainer's paired-stat edits restore both halves rather than allowing a later drain recovery to undo the change.</li>");
        s.AppendLine("<li><b>Fight for loot and experience.</b> Weaken enemies rather than bypassing their death routine when using the trainer; the game must process the kill to award treasure and experience.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string value) => HtmlPage.Escape(value);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.55; color: #222; }
        h1 { font-size: 1.9em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { font-size: 1.45em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { margin-top: 1.5em; } small { color: #666; font-weight: normal; }
        .lede { font-style: italic; color: #555; } .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table { border-collapse: collapse; width: 100%; margin: 1em 0; } th { background: #e8e8e8; text-align: left; } th, td { padding: 5px 8px; border: 1px solid #ccc; vertical-align: top; }
        .legend { list-style: none; padding: 0; } .legend li { display: inline-block; margin: 0 1em .5em 0; } .swatch { display: inline-block; width: 14px; height: 14px; border: 1px solid #444; vertical-align: middle; margin-right: 4px; } .wall { background: #A7ACBA; } .floor { background: #20232B; } .door { background: #6FC276; } .secret { background: #D4A64A; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; } li { margin-bottom: .45em; }
        """;
}
