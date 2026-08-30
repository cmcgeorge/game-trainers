using System.Text;
using GameTrainers.Common.Documents;
using MinesOfTitanTrainer.Game;

namespace MinesOfTitanTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Mines of Titan — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Mines of Titan</h1>");
        markup.AppendLine("<p class=\"lede\">A field guide to surviving Infocom's science-fiction adventure on Titan.</p>");
        Contents(markup, cluebook);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeItems) Items(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The mission</a></li>");
        if (cluebook.Options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (cluebook.Options.IncludeItems) markup.AppendLine("<li><a href=\"#items\">Equipment reference</a></li>");
        if (cluebook.Options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (cluebook.Options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The mission</h2>");
        markup.AppendLine("<p>Mines of Titan is an interactive fiction adventure with role-playing elements set on Saturn's moon Titan. Explore the abandoned human station, ancient alien ruins, and dangerous underground passages while preserving enough equipment and oxygen to reach the final control center.</p>");
        markup.AppendLine("<table class=\"ref\"><tr><th>Objective</th><th>Focus</th></tr>");
        markup.AppendLine("<tr><td>Explore Titan</td><td>Find routes through stations, mines, caverns, and alien structures.</td></tr>");
        markup.AppendLine("<tr><td>Recover equipment</td><td>Use tools, oxygen, crystals, and repair parts to overcome obstacles.</td></tr>");
        markup.AppendLine("<tr><td>Restore control</td><td>Solve the final systems puzzle and activate the escape route.</td></tr></table>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2><p>North is at the top and west is at the left. These maps are reference layouts: <b>#</b> wall, <b>.</b> open area, <b>S</b> start, <b>I</b> important item, <b>N</b> NPC, <b>E</b> enemy, <b>X</b> hazard, and <b>P</b> puzzle point.</p>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3 id=\"area-{area.Index}\">Area {area.Index + 1}: {Escape(area.Name)}</h3>");
            markup.AppendLine($"<p>{Escape(area.Description)}</p>");
            markup.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{Escape(poi.Name)}</td><td>{Escape(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        const int padding = 20;
        int width = padding * 2 + cell * area.Width;
        int height = padding * 2 + cell * area.Height;
        var svg = SvgCanvas.Responsive(width, height, "Mines of Titan area map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(padding + x * cell, padding + y * cell, cell, cell,
                    ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            var (fill, label) = PoiStyle(poi.Name);
            double x = padding + poi.X * cell;
            double y = padding + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string fill, string label) PoiStyle(string name) => name switch
    {
        "Start" => ("#B070E0", "S"),
        "Supply Cache" or "Medical Locker" or "Crystal Vein" or "Frozen Cache" or "Shuttle Wreck" => ("#C89B3C", "I"),
        "Mining Robot" or "Alien Guardian" or "Hostile Creature" => ("#D15D5D", "E"),
        "Cave-In" or "Ice Bridge" => ("#D9774A", "X"),
        "Station Survivor" => ("#6FC276", "N"),
        _ => ("#799BD7", "P"),
    };

    private static void Items(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"items\">Equipment reference</h2><table class=\"ref\"><tr><th>Item</th><th>Use</th></tr>");
        markup.AppendLine("<tr><td>Laser cutter</td><td>Cut through sealed panels and damaged mechanisms.</td></tr><tr><td>Space suit</td><td>Protects the explorer from Titan's hostile environment.</td></tr><tr><td>Oxygen tank</td><td>Extends exploration time in thin-air or contaminated areas.</td></tr><tr><td>Crystals</td><td>Alien power sources used in devices and temple mechanisms.</td></tr><tr><td>Tools</td><td>Repair equipment and interact with damaged machinery.</td></tr></table>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol><li>Search the landing site for supplies and identify the route into the abandoned station.</li><li>Use the station's records and terminal to learn which equipment and crystals are required.</li><li>Descend through the underground tunnels and mine shafts, restoring power where necessary.</li><li>Cross the ice caverns only with adequate protection and oxygen.</li><li>Explore the alien city, then use the observation dome to identify the shuttle crash site.</li><li>Recover useful repair parts from the crashed shuttle and return to the alien structures.</li><li>Use recovered crystals to open the temple and reach its inner sanctum.</li><li>Solve the control center puzzle, activate the launch link, and escape Titan.</li></ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul><li><b>Record every clue.</b> Terminal messages and alien symbols often explain a later obstacle.</li><li><b>Conserve oxygen.</b> Explore one branch at a time and return before entering deeper hazardous areas.</li><li><b>Keep critical tools.</b> Do not discard the laser cutter, suit, repair tools, or crystals without understanding their purpose.</li><li><b>Prepare for hazards.</b> Ice, cave-ins, environmental damage, and hostile machines can turn a route into a dead end.</li><li><b>Use the maps.</b> Mark each puzzle point and connection before committing to a long expedition.</li></ul>");
    }

    private static string Escape(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 900px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
