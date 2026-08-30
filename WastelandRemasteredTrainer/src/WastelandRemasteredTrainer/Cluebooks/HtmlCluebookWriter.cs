using System.Text;
using GameTrainers.Common.Documents;
using WastelandRemasteredTrainer.Game;

namespace WastelandRemasteredTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Wasteland Remastered — reference cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var body = new StringBuilder();
        body.AppendLine("<h1>Wasteland Remastered</h1>");
        body.AppendLine("<p class=\"lede\">A reference cluebook for planning a Desert Ranger campaign. Area plans are illustrative guide material, not live-game coordinates or decoded map geometry.</p>");
        Contents(body, cluebook.Options);
        Overview(body);
        if (cluebook.Options.IncludeMaps) Maps(body, cluebook);
        if (cluebook.Options.IncludeAttributes) Attributes(body);
        if (cluebook.Options.IncludeSkills) Skills(body);
        if (cluebook.Options.IncludeItems) Items(body);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(body);
        if (cluebook.Options.IncludeStrategy) Strategy(body);
        return new HtmlPage(Title).Style(Style).Append(body.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder body, CluebookOptions options)
    {
        body.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol><li><a href=\"#overview\">At a glance</a></li>");
        if (options.IncludeMaps) body.AppendLine("<li><a href=\"#maps\">Area references</a></li>");
        if (options.IncludeAttributes) body.AppendLine("<li><a href=\"#attributes\">Attributes</a></li>");
        if (options.IncludeSkills) body.AppendLine("<li><a href=\"#skills\">Skills</a></li>");
        if (options.IncludeItems) body.AppendLine("<li><a href=\"#items\">Items</a></li>");
        if (options.IncludeWalkthrough) body.AppendLine("<li><a href=\"#walkthrough\">Campaign route</a></li>");
        if (options.IncludeStrategy) body.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        body.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"overview\">At a glance</h2>");
        body.AppendLine("<table><tr><th>Game</th><td>Wasteland Remastered</td></tr><tr><th>Party</th><td>Up to seven Desert Rangers</td></tr>");
        body.AppendLine($"<tr><th>Attributes</th><td>{AttributeBook.Attributes.Count}</td></tr><tr><th>Skills</th><td>{SkillBook.Skills.Count}</td></tr><tr><th>Item entries</th><td>{ItemBook.Items.Count - 1}</td></tr></table>");
        body.AppendLine("<p>The Remastered edition preserves the original campaign's skill checks, resource pressure, and branching problem solving. Keep distinct specialists alive: an observant scout, technical lock and trap expert, medic, and high-IQ late-game technician all make different routes safer.</p>");
    }

    private static void Maps(StringBuilder body, Cluebook cluebook)
    {
        body.AppendLine("<h2 id=\"maps\">Area references</h2><p>These compact 20×20 diagrams provide landmark-oriented planning aids. They do not represent confirmed locations, coordinates, position tracking, or teleport targets.</p>");
        body.AppendLine("<p class=\"legend\"><b>R</b> Ranger Center &nbsp; <b>T</b> town &nbsp; <b>D</b> dungeon &nbsp; <b>I</b> item &nbsp; <b>N</b> NPC &nbsp; <b>E</b> enemy &nbsp; <b>S</b> start &nbsp; <b>B</b> boss/base</p>");
        foreach (var area in cluebook.Areas)
        {
            body.AppendLine($"<h3>{E(area.Name)}</h3><p>{E(area.Description)}</p>{AreaSvg(area, cluebook.Options.MapCellSize)}");
            body.AppendLine("<table><tr><th>Marker</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois) body.AppendLine($"<tr><td>{E(poi.Symbol)} ({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            body.AppendLine("</table>");
        }
    }

    private static void Attributes(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"attributes\">Attributes</h2><table><tr><th>Stat</th><th>Role</th><th>Field advice</th></tr>");
        foreach (var attribute in AttributeBook.Attributes)
            body.AppendLine($"<tr><td>{E(attribute.Abbr)} — {E(attribute.Name)}</td><td>{E(attribute.Role)}</td><td>{E(attribute.InPlay)}</td></tr>");
        body.AppendLine("</table>");
    }

    private static void Skills(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"skills\">Skills</h2><table><tr><th>#</th><th>Skill</th><th>IQ</th><th>Use</th><th>Where it matters</th></tr>");
        foreach (var skill in SkillBook.Skills)
            body.AppendLine($"<tr><td>{skill.Id}</td><td>{E(skill.Name)}</td><td>{skill.MinIq}</td><td>{E(skill.Use)}</td><td>{E(skill.Where)}</td></tr>");
        body.AppendLine("</table>");
    }

    private static void Items(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"items\">Items</h2><table><tr><th>#</th><th>Item</th><th>Category</th><th>Description</th><th>Combat</th></tr>");
        foreach (var item in ItemBook.Items.Where(item => item.Id != 0))
            body.AppendLine($"<tr><td>{item.Id}</td><td>{E(item.Name)}</td><td>{E(item.Category)}</td><td>{E(item.Description)}</td><td>{E(item.Damage)}</td></tr>");
        body.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"walkthrough\">Campaign route</h2><ol><li>Build a balanced ranger team and leave the Ranger Center with basic ammunition, healing supplies, and a clear division of skills.</li><li>Resolve Highpool and the Agricultural Center early; both introduce the environmental and social checks that define the campaign.</li><li>Use the Rail Nomads' Camp and Quartz to gather leads, equipment, and the skills needed for locked, trapped, or hostile spaces.</li><li>Push through Needles and Las Vegas only after the party can handle radiation, automatic weapons, and crowded combat zones.</li><li>Follow the late-game trail through Darwin, Guardian Citadel, and the Sleeper Base, preserving keys and technical components instead of selling them.</li><li>Enter Base Cochise with supplies, trained technical specialists, and an escape plan. The final objective rewards preparation more than brute force.</li></ol>");
    }

    private static void Strategy(StringBuilder body)
    {
        body.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul><li><b>Spread skills deliberately.</b> A skill check only helps if the right ranger is present and alive, so avoid making one character every specialist.</li><li><b>Guard scarce ammunition.</b> Save heavy explosives and energy cells for robots, bottlenecks, and enemies that threaten a party wipe.</li><li><b>Keep quest hardware.</b> Keys, passes, components, and unusual heads or boards often matter later; a full inventory is safer than an irreversible sale.</li><li><b>Use the terrain.</b> Open desert, town interiors, and base corridors change the value of range, movement, and burst fire.</li><li><b>Retreat before attrition wins.</b> Healing, ammunition, and replacement equipment are strategic resources, not just combat statistics.</li></ul>");
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        const int pad = 20;
        int width = pad * 2 + area.Width * cell;
        int height = pad * 2 + area.Height * cell;
        var svg = SvgCanvas.Responsive(width, height, $"Reference map: {area.Name}");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell,
                    ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            int x = pad + poi.X * cell;
            int y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", PoiColor(poi.Symbol)));
            svg.Text(x + cell / 2.0, y + cell * 0.7, poi.Symbol, ("text-anchor", "middle"),
                ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static string PoiColor(string symbol) => symbol switch
    {
        "R" => "#6FC276", "T" => "#79B9C8", "D" => "#A48AD4", "I" => "#C89B3C", "N" => "#799BD7", "E" => "#D86363", "S" => "#E0E2E8", "B" => "#DE8436", _ => "#E0E2E8",
    };

    private static string E(string value) => HtmlPage.Escape(value);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 980px; margin: 2em auto; padding: 0 1em; line-height: 1.55; color: #222; }
        h1 { border-bottom: 2px solid #444; padding-bottom: .3em; } h2 { margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; } h3 { margin-top: 1.5em; }
        .lede { color: #555; font-style: italic; } .toc { background: #f5f5f5; border: 1px solid #ddd; padding: .8em 1.4em; } .legend { background: #1e1f26; color: #e0e2e8; padding: .7em; }
        table { border-collapse: collapse; width: 100%; margin: 1em 0; font-size: .92em; } th, td { border: 1px solid #ccc; padding: 4px 7px; text-align: left; vertical-align: top; } th { background: #e8e8e8; } svg { display: block; max-width: 100%; height: auto; margin: 1em 0; border: 1px solid #bbb; }
        @media (max-width: 640px) { body { margin: 1em auto; } table { font-size: .8em; } th, td { padding: 3px; } }
        """;
}
