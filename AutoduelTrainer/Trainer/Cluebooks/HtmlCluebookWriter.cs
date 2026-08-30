using System.Text;
using GameTrainers.Common.Documents;
using AutoduelTrainer.Game;

namespace AutoduelTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Autoduel cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Autoduel</h1>");
        markup.AppendLine("<p class=\"lede\">A quick-reference cluebook for Origin Systems' 1985 vehicular-combat role-playing game.</p>");
        Contents(markup, cluebook.Options);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeWeapons) Weapons(markup);
        if (cluebook.Options.IncludeVehicles) Vehicles(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, CluebookOptions options)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol><li><a href=\"#overview\">Overview</a></li>");
        if (options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (options.IncludeWeapons) markup.AppendLine("<li><a href=\"#weapons\">Weapon reference</a></li>");
        if (options.IncludeVehicles) markup.AppendLine("<li><a href=\"#vehicles\">Vehicle reference</a></li>");
        if (options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Career route</a></li>");
        if (options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">Overview</h2>");
        markup.AppendLine("<p>Autoduel is a car-combat RPG based on Steve Jackson's Car Wars. Work courier jobs, enter arena deathmatches, improve your vehicle, and survive the outlaw-controlled highways linking the fortress towns.</p>");
        markup.AppendLine("<table class=\"ref\"><tr><th>Activity</th><th>Purpose</th></tr><tr><td>Courier work</td><td>Earn money by moving cargo between cities.</td></tr><tr><td>Arena combat</td><td>Win money and prestige in vehicle deathmatches.</td></tr><tr><td>Highway travel</td><td>Reach cities, encounter outlaws, and find salvage.</td></tr><tr><td>Vehicle upgrades</td><td>Balance armor, weapons, cargo space, and weight.</td></tr></table>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2><p>These reference maps show city districts and representative highway routes. Roads are brown; buildings and obstacles are gray.</p><ul class=\"legend\"><li><b>C</b> city</li><li><b>A</b> arena</li><li><b>S</b> shop</li><li><b>T</b> truck stop</li><li><b>I</b> item or cargo</li><li><b>N</b> NPC or contact</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3>{E(area.Name)}</h3><p>{E(area.Description)}</p>");
            markup.AppendLine(MapSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois) markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string MapSvg(AreaLevel area, int cell)
    {
        const int pad = 20;
        int width = pad * 2 + cell * area.Width;
        int height = pad * 2 + cell * area.Height;
        var svg = SvgCanvas.Responsive(width, height, $"{area.Name} map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell, ("fill", area.Grid[x, y] switch { CellKind.Wall => "#3A3D4A", CellKind.Road => "#5A5244", _ => "#242830" }));
        foreach (var poi in area.Pois)
        {
            var (color, label) = PoiColor(poi.Name);
            int x = pad + poi.X * cell;
            int y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", color));
            svg.Text(x + cell / 2.0, y + cell * .7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * .55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string Color, string Label) PoiColor(string name) => name switch
    {
        _ when name.Contains("Arena", StringComparison.Ordinal) => ("#CF7252", "A"),
        _ when name.Contains("Shop", StringComparison.Ordinal) || name.Contains("Plant", StringComparison.Ordinal) || name.Contains("Dealer", StringComparison.Ordinal) || name.Contains("Upgrade", StringComparison.Ordinal) => ("#75B8D0", "S"),
        _ when name.Contains("Truck Stop", StringComparison.Ordinal) => ("#76C28A", "T"),
        _ when name.Contains("Cargo", StringComparison.Ordinal) || name.Contains("Cache", StringComparison.Ordinal) || name.Contains("Wreckage", StringComparison.Ordinal) || name.Contains("Salvage", StringComparison.Ordinal) => ("#D5B852", "I"),
        _ when name.Contains("City", StringComparison.Ordinal) || name.Contains("Town", StringComparison.Ordinal) || name is "New York" or "Boston" or "Chicago" or "Los Angeles" or "Detroit" or "Houston" or "Watertown" or "Manchester" or "Albany" or "Buffalo" or "Pittsburgh" or "Philadelphia" or "Baltimore" or "Washington" => ("#B070E0", "C"),
        _ => ("#E0E2E8", "N"),
    };

    private static void Weapons(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"weapons\">Weapon reference</h2><table class=\"ref\"><tr><th>Weapon</th><th>Cost</th><th>Weight</th><th>Damage</th><th>Spaces</th></tr>");
        string[,] weapons = { { "Machine gun", "$1,000", "150", "3", "1" }, { "Flamethrower", "$550", "465", "3", "3" }, { "Rocket launcher", "$1,050", "215", "3", "3" }, { "Recoilless rifle", "$1,550", "315", "5", "3" }, { "Anti-tank gun", "$2,050", "615", "6", "4" }, { "Laser", "$8,000", "500", "2", "2" }, { "Minedropper", "$550", "165", "3", "3" }, { "Spikedropper", "$150", "40", "5", "2" }, { "Heavy rocket", "$200", "100", "2", "1" } };
        for (int i = 0; i < weapons.GetLength(0); i++) markup.AppendLine($"<tr><td>{weapons[i, 0]}</td><td>{weapons[i, 1]}</td><td>{weapons[i, 2]} lb</td><td>{weapons[i, 3]}</td><td>{weapons[i, 4]}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Vehicles(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"vehicles\">Vehicle reference</h2><p>Cars, cycles, and trucks are built around a trade-off: bigger chassis and power plants carry more armor and weapons, but cost more and may be harder to control.</p><table class=\"ref\"><tr><th>Component</th><th>Use</th></tr><tr><td>Chassis</td><td>Sets the weight and space budget.</td></tr><tr><td>Power plant</td><td>Supports acceleration and loaded vehicle weight.</td></tr><tr><td>Suspension</td><td>Improves handling for combat driving.</td></tr><tr><td>Armor</td><td>Protect front, rear, sides, and underbody independently.</td></tr><tr><td>Cargo space</td><td>Reserve at least one space for courier work.</td></tr></table>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Career route</h2><ol><li>Start in New York and buy or build a durable starter car.</li><li>Fit a front machine gun, armor the front and rear, and keep cargo space free.</li><li>Run short courier jobs between nearby cities to build funds.</li><li>Use arena events for money and prestige; repair and reload after every fight.</li><li>Buy a clone in New York or Boston before attempting dangerous routes.</li><li>Listen for rumors at bars and truck stops to open quest deliveries.</li><li>Upgrade to heavier weapons and a larger plant before the longest highway runs.</li></ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy</h2><ul><li><b>Protect the front.</b> Most fights begin head-on, so front armor and a forward weapon are early priorities.</li><li><b>Carry a rear defense.</b> Oil, spikes, or mines discourage pursuers.</li><li><b>Watch weight.</b> An overloaded car sacrifices the handling and performance needed to survive.</li><li><b>Keep cash in reserve.</b> Repairs, ammunition, fuel, and cloning costs can end a profitable run.</li><li><b>Travel prepared.</b> Reload weapons and repair every component before leaving town.</li></ul>");
    }

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 900px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        .legend { padding-left: 1.5em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
