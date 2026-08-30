using System.Text;
using GameTrainers.Common.Documents;
using AmberstarTrainer.Game;

namespace AmberstarTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Amberstar — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Amberstar</h1>");
        markup.AppendLine("<p class=\"lede\">A reference guide to Thalion Software's 1992 role-playing adventure on Umajin.</p>");
        Contents(markup, cluebook);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(markup);
        if (cluebook.Options.IncludeClasses) Characters(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">At a glance</a></li>");
        if (cluebook.Options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (cluebook.Options.IncludeSpells) markup.AppendLine("<li><a href=\"#spells\">Spells</a></li>");
        if (cluebook.Options.IncludeClasses) markup.AppendLine("<li><a href=\"#characters\">Races and classes</a></li>");
        if (cluebook.Options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (cluebook.Options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">At a glance</h2><table class=\"ref\">");
        Row(markup, "Title", "Amberstar");
        Row(markup, "Developer", "Thalion Software");
        Row(markup, "Year", "1992");
        Row(markup, "Setting", "Umajin, primarily the Twinlake region");
        Row(markup, "Party", $"Up to {CharacterFormat.MaxSlots} characters");
        Row(markup, "Races", string.Join(", ", RaceBook.Names));
        Row(markup, "Classes", string.Join(", ", ClassBook.Selectable.Skip(1)));
        Row(markup, "Spells", $"{SpellBook.TotalCount} across White, Grey, Black, and Special schools");
        markup.AppendLine("</table>");
        markup.AppendLine("<p>Amberstar is an open-ended fantasy RPG. Twinlake City is the starting hub; exploration through villages, wilderness, ruins, mines, caves, and the final fortress reveals the route through Umajin.</p>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2>");
        markup.AppendLine("<p>These reference maps show major areas and landmarks. They are not a live position tracker or teleport system.</p>");
        markup.AppendLine("<ul class=\"legend\"><li><b>Dark grey</b> wall or structure</li><li><b>Blue</b> water</li><li><b>Green</b> forest</li><li><b>Brown</b> mountain</li><li><b>Gold</b> desert</li><li><b>Amber</b> point of interest</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3 id=\"area-{area.Index}\">{area.Index + 1}. {E(area.Name)}</h3>");
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
        int width = padding * 2 + area.Width * cell;
        int height = padding * 2 + area.Height * cell;
        var svg = SvgCanvas.Responsive(width, height, $"Map of {area.Name}");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(padding + x * cell, padding + y * cell, cell, cell,
                    ("fill", Color(area.Grid[x, y])));
        foreach (var poi in area.Pois)
        {
            int x = padding + poi.X * cell;
            int y = padding + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", "#D9A442"));
            svg.Text(x + cell / 2.0, y + cell * .7, Marker(poi.Name), ("text-anchor", "middle"),
                ("font-family", "monospace"), ("font-size", cell * .55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static string Color(AreaCellKind kind) => kind switch
    {
        AreaCellKind.Floor => "#1E1F26",
        AreaCellKind.Water => "#3B82B6",
        AreaCellKind.Mountain => "#7A6D62",
        AreaCellKind.Forest => "#3F7D4A",
        AreaCellKind.Desert => "#BE994E",
        _ => "#3A3D4A",
    };

    private static string Marker(string name) => name switch
    {
        "Temple" or "Sun Temple" or "Hidden Shrine" => "T",
        "Tavern" => "V",
        "Shops" or "Shop" => "S",
        "Lord Chile" => "L",
        _ => "•",
    };

    private static void Spells(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"spells\">Spells</h2><p>Spell knowledge is organized into four schools.</p>");
        foreach (SpellBook.School school in Enum.GetValues<SpellBook.School>())
        {
            markup.AppendLine($"<h3>{E(SpellBook.SchoolNames[(int)school])} magic</h3><table class=\"ref\"><tr><th>#</th><th>Spell</th></tr>");
            var spells = SpellBook.Spells(school);
            for (int index = 0; index < spells.Length; index++)
                markup.AppendLine($"<tr><td>{index + 1}</td><td>{E(spells[index])}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static void Characters(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"characters\">Races and classes</h2>");
        markup.AppendLine("<h3>Races</h3><table class=\"ref\"><tr><th>#</th><th>Race</th></tr>");
        for (int index = 0; index < RaceBook.Names.Length; index++) markup.AppendLine($"<tr><td>{index}</td><td>{E(RaceBook.Names[index])}</td></tr>");
        markup.AppendLine("</table><h3>Classes</h3><table class=\"ref\"><tr><th>#</th><th>Class</th></tr>");
        for (int index = 1; index < ClassBook.Selectable.Length; index++) markup.AppendLine($"<tr><td>{index}</td><td>{E(ClassBook.Selectable[index])}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder markup) => markup.AppendLine("""
        <h2 id="walkthrough">Walkthrough</h2><ol>
        <li>Establish the party in Twinlake City. Visit the shops, temple, tavern, and guilds before venturing far from town.</li>
        <li>Explore the Twinlake roads and visit Haste for supplies and local leads.</li>
        <li>Use the forest routes to investigate the Elven Ruins and their hidden shrine.</li>
        <li>Travel through Grim-path to reach the Dwarven Mines, then follow the deeper cave routes.</li>
        <li>Prepare for the Crystal dungeon and Desert Temples with healing, light, food, and a balanced spell selection.</li>
        <li>Use the Underground Caves to approach Lord Chile's Fortress only after the party is well equipped.</li>
        <li>Explore the fortress carefully and confront Lord Chile to complete the main campaign.</li></ol>
        """);

    private static void Strategy(StringBuilder markup) => markup.AppendLine("""
        <h2 id="strategy">Strategy</h2><ul>
        <li><b>Keep a balanced party.</b> Front-line fighters, healing, and utility magic all matter in long dungeon expeditions.</li>
        <li><b>Carry supplies.</b> Food, healing, and spare resources are more valuable before committing to caves or desert routes.</li>
        <li><b>Use the right school.</b> White magic supports recovery, Grey magic provides utility, and Black magic supplies direct offence.</li>
        <li><b>Map dangerous areas.</b> Use these area plans as orientation references while keeping notes on local routes and secrets.</li>
        <li><b>Return to Twinlake.</b> Treat the city as the safe resupply point between major expeditions.</li></ul>
        """);

    private static void Row(StringBuilder markup, string label, string value) =>
        markup.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string value) => HtmlPage.Escape(value);

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
        .legend { list-style: none; padding: 0; }
        .legend li { display: inline-block; margin: 0 1em .5em 0; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
