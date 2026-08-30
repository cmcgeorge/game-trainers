using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using HillsfarTrainer.Game;

namespace HillsfarTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Hillsfar — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Hillsfar</h1>");
        markup.AppendLine("<p class=\"lede\">A reference guide to the SSI / Westwood adventure in the city of Hillsfar and its surrounding lands.</p>");
        Contents(markup, cluebook);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) AreaMaps(markup, cluebook);
        if (cluebook.Options.IncludeClasses) Classes(markup);
        if (cluebook.Options.IncludeQuestGuide) QuestGuide(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">At a glance</a></li>");
        if (cluebook.Options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (cluebook.Options.IncludeClasses) markup.AppendLine("<li><a href=\"#classes\">Class reference</a></li>");
        if (cluebook.Options.IncludeQuestGuide) markup.AppendLine("<li><a href=\"#quests\">Quest guide</a></li>");
        if (cluebook.Options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (cluebook.Options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">At a glance</h2><table class=\"ref\">");
        Row(markup, "Title", GameFacts.GameTitle);
        Row(markup, "Publisher", GameFacts.Publisher);
        Row(markup, "Build", GameFacts.Version);
        Row(markup, "Game day", $"About {GameFacts.RealMinutesPerGameDay} real minutes");
        Row(markup, "Natural healing", "1 + clamp(Constitution − 14, 0, 5) HP each game day");
        Row(markup, "Quest scripts", GameFacts.QuestCount.ToString(CultureInfo.InvariantCulture));
        markup.AppendLine("</table>");
        markup.AppendLine("<p>Hillsfar is a single-character AD&amp;D adventure. The city is its hub: guilds issue missions, pubs supply clues, and the arena, mazes, roads, and action sequences lead to the objectives.</p>");
    }

    private static void AreaMaps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2>");
        markup.AppendLine("<p>These schematic maps organize the major game areas and their common landmarks. They are navigation references, not a replacement for searching every location in the game.</p>");
        markup.AppendLine("<ul class=\"legend\"><li><b>#</b> wall</li><li><b>.</b> open route</li><li><b>S</b> shop</li><li><b>T</b> tavern</li><li><b>M</b> temple</li><li><b>A</b> arena</li><li><b>G</b> government</li><li><b>D</b> docks</li><li><b>C</b> crypt</li><li><b>I</b> item</li><li><b>N</b> NPC</li><li><b>E</b> enemy</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3 id=\"area-{area.Index}\">{E(area.Name)}</h3>");
            markup.AppendLine($"<p>{E(area.Description)}</p>");
            markup.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        int padding = 20;
        int width = padding * 2 + cell * area.Width;
        int height = padding * 2 + cell * area.Height;
        var svg = SvgCanvas.Responsive(width, height, $"Map of {area.Name}");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(padding + x * cell, padding + y * cell, cell, cell,
                    ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            var (fill, label) = PoiColor(poi.Name);
            int x = padding + poi.X * cell;
            int y = padding + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"),
                ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string Fill, string Label) PoiColor(string name) => name switch
    {
        "Shop" => ("#68A6D7", "S"), "Tavern" => ("#B87243", "T"), "Temple" => ("#D0D0A0", "M"),
        "Arena" => ("#C66565", "A"), "Government" => ("#927AC9", "G"), "Docks" => ("#59A9B5", "D"),
        "Crypt" => ("#8D8D9B", "C"), "Item" => ("#C89B3C", "I"), "NPC" => ("#6FC276", "N"),
        "Enemy" => ("#D45757", "E"), _ => ("#E0E2E8", ""),
    };

    private static void Classes(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"classes\">Class reference</h2><table class=\"ref\"><tr><th>Class</th><th>Role</th></tr>");
        foreach (var @class in ClassBook.Classes)
            markup.AppendLine($"<tr><td>{E(@class.Name)}</td><td>{E(ClassRole(@class))}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static string ClassRole(ClassInfo @class)
    {
        var roles = new List<string>();
        if (@class.IsFighter) roles.Add("front-line combat");
        if (@class.IsMagicUser) roles.Add("arcane magic");
        if (@class.IsCleric) roles.Add("healing and priest magic");
        if (@class.IsThief) roles.Add("locks and traps");
        return string.Join(", ", roles);
    }

    private static void QuestGuide(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"quests\">Quest guide</h2>");
        markup.AppendLine("<p>Guild missions advance through conversations, searches, rides, mazes, and arena victories. Read pub rumors, use <b>Space</b> to search, and return to your guild after every important discovery.</p>");
        markup.AppendLine("<table class=\"ref\"><tr><th>Mission gate</th><th>Required opponent</th></tr>");
        foreach (var gate in ArenaBook.MissionGates)
            markup.AppendLine($"<tr><td>{E(gate.Mission)}</td><td>{E(gate.Opponent)}</td></tr>");
        markup.AppendLine("</table><h3>Overland destinations</h3><table class=\"ref\"><tr><th>Location</th><th>Reached from</th><th>Purpose</th></tr>");
        foreach (var destination in LocationBook.Overland)
            markup.AppendLine($"<tr><td>{E(destination.Name)}</td><td>{E(destination.ReachedFrom)}</td><td>{E(destination.Why)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        markup.AppendLine("<li>Create or import a character, then save at camp before leaving.</li>");
        markup.AppendLine("<li>Visit the class guild for the current mission and use pubs to collect clues.</li>");
        markup.AppendLine("<li>Use city shops in daytime, then bank gold before risks such as mazes, pubs, and rides.</li>");
        markup.AppendLine("<li>Search every named location. Several objectives are triggered only by searching the right place.</li>");
        markup.AppendLine("<li>Ride the roads for overland destinations; when a question mark appears, take the unmarked trail.</li>");
        markup.AppendLine("<li>Complete required arena bouts by watching the opponent's tell before attacking.</li>");
        markup.AppendLine("<li>Return to the guild whenever you obtain a mission item or learn a new clue.</li>");
        markup.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy</h2><ul>");
        foreach (var tip in GameFacts.Tips) markup.AppendLine($"<li>{E(tip)}</li>");
        markup.AppendLine("</ul>");
    }

    private static void Row(StringBuilder markup, string label, string value) =>
        markup.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

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
