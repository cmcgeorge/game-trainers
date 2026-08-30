using System.Text;
using GameTrainers.Common.Documents;
using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Fountain of Dreams — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Fountain of Dreams</h1>");
        markup.AppendLine("<p class=\"lede\">A reference guide to Electronic Arts' 1990 post-apocalyptic role-playing game, with area maps, character guidance, and a route to the Fountain.</p>");
        Contents(markup, cluebook.Options);
        Overview(markup);
        Attributes(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeProfessions) Professions(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, CluebookOptions options)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        markup.AppendLine("<li><a href=\"#attributes\">Attributes</a></li>");
        if (options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (options.IncludeProfessions) markup.AppendLine("<li><a href=\"#professions\">Professions</a></li>");
        if (options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The game at a glance</h2><table class=\"ref\">");
        Row(markup, "Title", GameFacts.GameTitle);
        Row(markup, "Publisher", GameFacts.Publisher);
        Row(markup, "Release", $"{GameFacts.ReleaseYear}, DOS");
        Row(markup, "Party", $"Up to {CharacterFormat.MaxSlots} members");
        Row(markup, "Character record", $"{CharacterFormat.RecordSize} bytes");
        Row(markup, "Objective", "Find the Fountain of Dreams and stop the spread of mutations.");
        markup.AppendLine("</table>");
        markup.AppendLine("<p>Florida has become an irradiated wasteland. Miami offers shelter, but the roads beyond it lead through ruined settlements, hostile machines, mutant territory, and the desert paths that eventually reach the Garden and its mysterious Fountain.</p>");
    }

    private static void Attributes(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"attributes\">Attributes</h2><p>Attributes normally range from 3 to 20. Build a party whose strengths cover combat, technical work, medicine, and wilderness travel.</p><table class=\"ref\"><tr><th>Abbr.</th><th>Attribute</th><th>Effect</th></tr>");
        foreach (var attribute in AttributeBook.Attributes)
            markup.AppendLine($"<tr><td>{E(attribute.Abbr)}</td><td>{E(attribute.Name)}</td><td>{E(attribute.Description)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2><p>These reference maps use plausible area layouts to organize the major locations. North is at the top and west is at the left.</p><ul class=\"legend\"><li><b>T</b> Town or route</li><li><b>F</b> Fountain</li><li><b>I</b> Important item</li><li><b>N</b> NPC</li><li><b>E</b> Enemy</li><li><b>X</b> Hazard</li><li><b>S</b> Start</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3 id=\"area-{area.Index}\">{E(area.Name)}</h3><p>{E(area.Description)}</p>");
            markup.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        const int pad = 20;
        int width = pad * 2 + cell * area.Width;
        int height = pad * 2 + cell * area.Height;
        var svg = SvgCanvas.Responsive(width, height, "Fountain of Dreams area map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell,
                    ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            var (fill, label) = PoiStyle(poi.Name);
            int x = pad + poi.X * cell;
            int y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static void Professions(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"professions\">Professions</h2><table class=\"ref\"><tr><th>Profession</th><th>CON</th><th>Role</th></tr>");
        foreach (var profession in ProfessionBook.Playable)
            markup.AppendLine($"<tr><td>{E(profession.Name)}</td><td>{profession.ConMin}–{profession.ConMax}</td><td>{E(profession.Description)}</td></tr>");
        markup.AppendLine("</table><p>Yuppie and Clown are NPC-only types and are not available at character creation.</p>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        markup.AppendLine("<li><b>Form a balanced party.</b> Include combat ability, medical skill, and a character capable of handling technical obstacles.</li>");
        markup.AppendLine("<li><b>Use Miami as your base.</b> Stock supplies, gather rumors, and learn which routes are safe enough for your current party.</li>");
        markup.AppendLine("<li><b>Explore Quartz and Needles.</b> These settlements offer supplies and information about desert travel and the Fountain.</li>");
        markup.AppendLine("<li><b>Prepare for radiation.</b> Carry healing, water, protective equipment, and a way to detect dangerous ground before entering the desert or coast.</li>");
        markup.AppendLine("<li><b>Search the factory and ruins.</b> Technical gear and clues can be found in guarded pre-war sites.</li>");
        markup.AppendLine("<li><b>Cross the desert carefully.</b> Avoid hazards and raider patrols where possible; the Garden trail leads toward the objective.</li>");
        markup.AppendLine("<li><b>Reach the Garden.</b> Deal with its guardians, investigate the relics, and find the Fountain of Dreams at its center.</li>");
        markup.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        markup.AppendLine("<li><b>Keep a mixed party.</b> Combat specialists alone cannot solve every medical, social, or mechanical obstacle.</li>");
        markup.AppendLine("<li><b>Conserve supplies.</b> Water, rations, ammunition, and medical gear are more valuable the farther you travel from Miami.</li>");
        markup.AppendLine("<li><b>Treat radiation as a route-planning problem.</b> Use safer paths when possible and do not enter obvious hot zones without preparation.</li>");
        markup.AppendLine("<li><b>Search before spending.</b> Caches, ruins, and defeated enemies can provide equipment that is expensive or unavailable in settlements.</li>");
        markup.AppendLine("<li><b>Return to safety regularly.</b> Re-evaluate equipment and party condition before pushing deeper into hostile territory.</li>");
        markup.AppendLine("</ul>");
    }

    private static (string fill, string label) PoiStyle(string name) => name switch
    {
        var n when n.Contains("Starting") => ("#E0E2E8", "S"),
        var n when n.Contains("Gate") || n.Contains("Road") || n.Contains("Entry") || n.Contains("Exit") => ("#6FC276", "T"),
        var n when n.Contains("Fountain") => ("#799BD7", "F"),
        var n when n.Contains("Cache") || n.Contains("Relic") || n.Contains("Supplies") => ("#C89B3C", "I"),
        var n when n.Contains("Raider") || n.Contains("Robot") || n.Contains("Mutant") => ("#C95B5B", "E"),
        var n when n.Contains("Radiation") || n.Contains("Hazard") || n.Contains("Toxic") || n.Contains("Contaminated") => ("#D09A3F", "X"),
        _ => ("#B070E0", "N"),
    };

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
        .legend { display: flex; flex-wrap: wrap; gap: 1em; padding-left: 1.5em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
