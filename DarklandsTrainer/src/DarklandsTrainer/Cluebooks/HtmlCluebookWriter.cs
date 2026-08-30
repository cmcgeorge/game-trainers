using System.Text;
using DarklandsTrainer.Game;
using GameTrainers.Common.Documents;

namespace DarklandsTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Darklands — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Darklands</h1>");
        markup.AppendLine("<p class=\"lede\">A companion for MicroProse's 1992 historical fantasy role-playing game, set across the Holy Roman Empire.</p>");
        Contents(markup, cluebook);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeAttributes) AttributesAndSkills(markup);
        if (cluebook.Options.IncludeSaintsAndPotions) SaintsAndPotions(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (cluebook.Options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Regional maps</a></li>");
        if (cluebook.Options.IncludeAttributes) markup.AppendLine("<li><a href=\"#attributes\">Attributes and skills</a></li>");
        if (cluebook.Options.IncludeSaintsAndPotions) markup.AppendLine("<li><a href=\"#saints\">Saints and potions</a></li>");
        if (cluebook.Options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Campaign outline</a></li>");
        if (cluebook.Options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        markup.AppendLine("<table class=\"ref\"><tr><th>Setting</th><td>15th-century Germany in the Holy Roman Empire</td></tr>");
        markup.AppendLine("<tr><th>Party</th><td>Up to four adventurers</td></tr><tr><th>Conflict</th><td>Uncover and stop a satanic conspiracy</td></tr>");
        markup.AppendLine($"<tr><th>Currency</th><td>1 florin = {GameFacts.GroschenPerFlorin} groschen = {GameFacts.PfennigPerFlorin} pfennigs</td></tr></table>");
        markup.AppendLine("<p>Darklands combines open-world travel, city encounters, tactical real-time combat, saints, alchemy, and a reputation system. Visit settlements for supplies and information, explore wilderness sites carefully, and build enough Fame to face the conspiracy's strongest forces.</p>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Regional maps</h2><p>These schematic reference maps identify major routes and points of interest. They are not a live position tracker or an exact game-coordinate map.</p>");
        markup.AppendLine("<ul class=\"legend\"><li><b>#</b> mountains or impassable terrain</li><li><b>.</b> road or open country</li><li><b>C/T/V</b> city, town, village</li><li><b>M/I/N/D</b> monastery, inn, castle, dungeon or cave</li><li><b>F</b> forest</li><li><b>S</b> starting area</li></ul>");
        foreach (var level in cluebook.Levels)
        {
            markup.AppendLine($"<h3>Area {level.Index + 1}: {Escape(level.Name)}</h3><p>{Escape(level.Description)}</p>");
            markup.AppendLine(MapSvg(level, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in level.Pois)
                markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{Escape(poi.Name)}</td><td>{Escape(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string MapSvg(AreaLevel level, int cell)
    {
        int padding = 20;
        int width = padding * 2 + level.Width * cell;
        int height = padding * 2 + level.Height * cell;
        var svg = SvgCanvas.Responsive(width, height, "Darklands area map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                svg.Rect(padding + x * cell, padding + y * cell, cell, cell, ("fill", CellColor(level.Grid[x, y])));
        foreach (var poi in level.Pois)
        {
            int x = padding + poi.X * cell;
            int y = padding + poi.Y * cell;
            string label = CellLabel(poi.Name);
            svg.Rect(x, y, cell, cell, ("fill", PoiColor(poi.Name)));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static void AttributesAndSkills(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"attributes\">Attributes and skills</h2><h3>Primary attributes</h3><table class=\"ref\"><tr><th>Attribute</th><th>What it governs</th></tr>");
        foreach (var attribute in AttributeBook.Primary) markup.AppendLine($"<tr><td>{Escape(attribute.Name)}</td><td>{Escape(attribute.Governs)}</td></tr>");
        markup.AppendLine($"<tr><td>{Escape(AttributeBook.DivineFavor.Name)}</td><td>{Escape(AttributeBook.DivineFavor.Governs)}</td></tr></table>");
        markup.AppendLine("<h3>Skills</h3><table class=\"ref\"><tr><th>Skill</th><th>Use</th></tr>");
        foreach (var skill in SkillBook.Skills) markup.AppendLine($"<tr><td>{Escape(skill.Name)}</td><td>{Escape(skill.Governs)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void SaintsAndPotions(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"saints\">Saints and potions</h2><p>Prayer spends Divine Favor, so reserve it for moments where an ordinary remedy or retreat cannot solve the problem. Religious Training and Virtue improve the party's relationship with the sacred side of the game.</p>");
        markup.AppendLine("<table class=\"ref\"><tr><th>Preparation</th><th>Use</th></tr><tr><td>Healing supplies</td><td>Keep them ready for injuries after tactical combat.</td></tr><tr><td>Alchemy reagents</td><td>Collect ingredients and use Alchemy to prepare useful remedies.</td></tr><tr><td>Saintly aid</td><td>Use prayer deliberately when a serious threat, injury, or supernatural obstacle demands it.</td></tr><tr><td>Religious Lore</td><td>Build this skill to better understand religious sites and miracles.</td></tr></table>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Campaign outline</h2><ol><li>Begin around Nuremberg, equip the party, and learn local rumours.</li><li>Travel between major cities to gather money, supplies, and information.</li><li>Improve combat skills, Riding, Stealth, Healing, and Woodwise before taking on isolated sites.</li><li>Explore forests, caves, mines, monasteries, and ruined temples while watching provisions and injuries.</li><li>Build Fame through successful quests and heroic deeds.</li><li>Follow evidence of the satanic conspiracy into its hidden cult sites.</li><li>Prepare carefully for the cult strongholds and the final fortress.</li></ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul><li><b>Pause in combat.</b> Real-time encounters reward deliberate positioning and target selection.</li><li><b>Travel prepared.</b> Carry food, money, healing supplies, and equipment before leaving a city.</li><li><b>Use specialists.</b> A balanced party benefits from combat ability, healing, stealth, social skills, and wilderness knowledge.</li><li><b>Listen for rumours.</b> Inns, towns, and cities point toward profitable and dangerous opportunities.</li><li><b>Respect terrain.</b> Forests and mountains make travel more hazardous; Riding and Woodwise reduce the risk.</li><li><b>Earn Fame.</b> Reputation opens opportunities and marks progress toward the campaign's greater challenges.</li></ul>");
    }

    private static string CellColor(CellKind kind) => kind switch
    {
        CellKind.Wall => "#4A4D5A", CellKind.Forest => "#3E724B", CellKind.City => "#B87248", CellKind.Town => "#C89B3C", CellKind.Village => "#B9A46A", CellKind.Monastery => "#9580C2", CellKind.Inn => "#D1886A", CellKind.Castle => "#8C9CB8", CellKind.Dungeon => "#905858", CellKind.Start => "#B070E0", _ => "#1E1F26",
    };

    private static string PoiColor(string name) => name.Contains("Monastery", StringComparison.Ordinal) ? "#9580C2" : name.Contains("Inn", StringComparison.Ordinal) ? "#D1886A" : name.Contains("Castle", StringComparison.Ordinal) || name.Contains("Fortress", StringComparison.Ordinal) ? "#8C9CB8" : name.Contains("Cave", StringComparison.Ordinal) || name.Contains("Temple", StringComparison.Ordinal) || name.Contains("Dungeon", StringComparison.Ordinal) ? "#905858" : name.Contains("Starting", StringComparison.Ordinal) ? "#B070E0" : name.Contains("Town", StringComparison.Ordinal) ? "#C89B3C" : "#B87248";
    private static string CellLabel(string name) => name.Contains("Monastery", StringComparison.Ordinal) ? "M" : name.Contains("Inn", StringComparison.Ordinal) ? "I" : name.Contains("Castle", StringComparison.Ordinal) || name.Contains("Fortress", StringComparison.Ordinal) ? "N" : name.Contains("Cave", StringComparison.Ordinal) || name.Contains("Temple", StringComparison.Ordinal) || name.Contains("Dungeon", StringComparison.Ordinal) ? "D" : name.Contains("Starting", StringComparison.Ordinal) ? "S" : name.Contains("Town", StringComparison.Ordinal) ? "T" : "C";
    private static string Escape(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 900px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        .legend { list-style: none; padding: 0; }
        .legend li { display: inline-block; margin: 0 1em 0.5em 0; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
