using System.Text;
using GameTrainers.Common.Documents;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Roadwar 2000 — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>Roadwar 2000</h1>");
        s.AppendLine("<p class=\"lede\">A field guide to SSI's 1987 post-apocalyptic road-war strategy game: build a capable gang, keep its fleet moving, search the ruins, and recover the eight G.U.B. scientists.</p>");
        Contents(s, cluebook.Options);
        Overview(s);
        if (cluebook.Options.IncludeVehicles) Vehicles(s, cluebook.Vehicles);
        if (cluebook.Options.IncludeCities) Cities(s, cluebook.Cities);
        if (cluebook.Options.IncludeMaps) Maps(s, cluebook.Cities);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (options.IncludeVehicles) s.AppendLine("<li><a href=\"#vehicles\">Vehicle reference</a></li>");
        if (options.IncludeCities) s.AppendLine("<li><a href=\"#cities\">City gazetteer highlights</a></li>");
        if (options.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Overland maps</a></li>");
        if (options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", "Roadwar 2000");
        Row(s, "Publisher", "Strategic Simulations, Inc. (SSI)");
        Row(s, "Year", "1987");
        Row(s, "Setting", "A plague-ravaged United States at the turn of the millennium");
        Row(s, "Objective", "Find the eight G.U.B. scientists and bring them home");
        Row(s, "Fleet", $"Up to {SaveFormat.MaxVehicleSlots} vehicles, chosen from {VehicleBook.All.Count} types");
        Row(s, "Cities", $"{CityBook.All.Count} across the West and East overland maps");
        Row(s, "Supplies", "Food, guns, tires, fuel, medical supplies, and ammunition");
        s.AppendLine("</table>");
        s.AppendLine("<p>You lead a road gang across two strategic overland maps. Search terrain and cities for supplies, vehicles, specialists, and clues while fighting rival gangs and surviving encounters. A successful expedition balances cargo capacity, fuel consumption, crew seats, combat strength, and enough medical support to preserve the people who make the gang effective.</p>");
        s.AppendLine("<p>The immediate goal is survival and expansion; the campaign goal is to recover all eight scientists from the ruins of the old United States. The Radio Direction Finder becomes especially valuable when only a few scientists remain unfound.</p>");
    }

    private static void Vehicles(StringBuilder s, IReadOnlyList<VehicleType> vehicles)
    {
        s.AppendLine("<h2 id=\"vehicles\">Vehicle reference</h2>");
        s.AppendLine("<p>Carrying capacity is exactly five times mass squared. Interior capacity below includes the driver, matching the game's display. Fuel is consumed per overland move.</p>");
        s.AppendLine("<table class=\"ref compact\"><tr><th>Vehicle</th><th>Mass</th><th>Structure</th><th>Speed</th><th>Capacity</th><th>Seats</th><th>Topside</th><th>Fuel</th></tr>");
        foreach (var vehicle in vehicles)
            s.AppendLine($"<tr><td>{E(vehicle.Name)}</td><td>{vehicle.Mass}</td><td>{vehicle.Structure}</td><td>{vehicle.MaxSpeedMph} MPH</td><td>{vehicle.CarryingCapacity}</td><td>{vehicle.DisplayInteriorCapacity}</td><td>{vehicle.TopsideCapacity}</td><td>{vehicle.FuelConsumption}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<p><b>Fleet roles.</b> Motorcycles and sports cars are fast, light scouts. Passenger vehicles let you carry a larger gang. Trucks, buses, and trailers turn a successful route into a supply-hauling operation; the trailer truck is the largest cargo carrier, but its fuel appetite makes route planning essential.</p>");
    }

    private static void Cities(StringBuilder s, IReadOnlyList<CityInfo> cities)
    {
        s.AppendLine("<h2 id=\"cities\">City gazetteer highlights</h2>");
        s.AppendLine("<p>Supply is the city's shipped starting level. It falls as the city is looted, so large cities are strong early targets. Coordinates use the engine's convention: X is 1-based from west to east and Y is 0-based from north to south.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>City</th><th>Map</th><th>Coordinates</th><th>Starting supply</th></tr>");
        foreach (var city in cities.OrderByDescending(c => c.Size).ThenBy(c => c.Name).Take(20))
            s.AppendLine($"<tr><td>{E(city.Name)}</td><td>{E(city.MapName)}</td><td>({city.X}, {city.Y})</td><td>{city.Size}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<p>The largest prizes are New York City, Los Angeles, Chicago, Philadelphia, Detroit, Dallas/Fort Worth, Washington, D.C., Houston, Montreal, and Boston. Use the trainer's full Reference and Cities tabs for the complete 120-city table and current city state.</p>");
    }

    private static void Maps(StringBuilder s, IReadOnlyList<CityInfo> cities)
    {
        s.AppendLine("<h2 id=\"maps\">Overland maps</h2>");
        s.AppendLine("<p>Roadwar divides the United States into West and East maps, each a 48 × 42 grid. Roads improve travel; water and wilderness are impassable. Cities are marked below by starting supply: pale gold is small, orange is large, and bright gold is a metroplex-scale prize.</p>");
        s.AppendLine("<h3>West</h3>");
        s.AppendLine("<p>The West covers the Pacific coast, Southwest, Rockies, Plains, and northern routes. It contains Los Angeles, San Francisco/Oakland, Dallas/Fort Worth, Denver, and the long fuel-sensitive routes through desert country.</p>");
        s.AppendLine(CitySvg(cities.Where(c => c.Map == 1), "West overland map city highlights"));
        s.AppendLine("<h3>East</h3>");
        s.AppendLine("<p>The East concentrates many of the game's biggest cities around the Great Lakes, Northeast, and Atlantic corridor. Chicago, Detroit, New York City, Philadelphia, Washington, D.C., and Montreal reward careful routes but also create frequent opportunities for combat and looting.</p>");
        s.AppendLine(CitySvg(cities.Where(c => c.Map == 2), "East overland map city highlights"));
        s.AppendLine("<p class=\"hint\">The diagrams identify cities and their relative positions; terrain and road details come from the live Map tab or the game's WEST.MAP and EAST.MAP files.</p>");
    }

    private static string CitySvg(IEnumerable<CityInfo> cities, string ariaLabel)
    {
        const int cell = 14;
        const int pad = 24;
        int width = pad * 2 + OverlandMap.Width * cell;
        int height = pad * 2 + OverlandMap.Height * cell;
        var svg = SvgCanvas.Responsive(width, height, ariaLabel);
        svg.Rect(0, 0, width, height, ("fill", "#171A20"));
        svg.Rect(pad, pad, OverlandMap.Width * cell, OverlandMap.Height * cell, ("fill", "#252A32"));

        for (int x = 0; x <= OverlandMap.Width; x += 8)
            svg.Line(pad + x * cell, pad, pad + x * cell, pad + OverlandMap.Height * cell, ("stroke", "#343B47"));
        for (int y = 0; y <= OverlandMap.Height; y += 7)
            svg.Line(pad, pad + y * cell, pad + OverlandMap.Width * cell, pad + y * cell, ("stroke", "#343B47"));

        foreach (var city in cities.Where(c => OverlandMap.IsInside(c.X, c.Y)))
        {
            string fill = city.Size >= 100 ? "#E0B341" : city.Size >= 40 ? "#D6813B" : "#B8A16B";
            int x = pad + (city.X - 1) * cell + 3;
            int y = pad + city.Y * cell + 3;
            svg.Rect(x, y, cell - 6, cell - 6, city.Name, ("fill", fill));
        }

        foreach (var city in cities.Where(c => c.Size >= 70 && OverlandMap.IsInside(c.X, c.Y)))
        {
            double x = pad + (city.X - 1) * cell + cell / 2.0;
            double y = pad + city.Y * cell - 2;
            svg.Text(x, y, city.Name, ("text-anchor", "middle"), ("font-size", 10), ("fill", "#F3E9C4"));
        }

        return svg.ToSvg();
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        s.AppendLine("<li><b>Build a durable starting gang.</b> Keep enough crew to operate and defend the vehicles you take. Food, fuel, tires, guns, ammunition, and medical supplies all matter before a long expedition.</li>");
        s.AppendLine("<li><b>Loot by terrain.</b> Farms and ranches favor food; roads make fuel storage tanks especially valuable. Search cities for more varied rewards, including vehicles and upgrade shops.</li>");
        s.AppendLine("<li><b>Grow the fleet deliberately.</b> Add cargo carriers before attempting long resource hauls, but account for their fuel consumption. A large fleet with insufficient fuel can become stranded.</li>");
        s.AppendLine("<li><b>Visit and clear cities.</b> Cities offer supplies, encounters, and paths to the people needed for the campaign. Clear hostile residents when necessary, but remember that removing residents is not the same as claiming a city.</li>");
        s.AppendLine("<li><b>Recruit specialists.</b> A doctor protects the gang, a drill sergeant improves its fighting quality, and a politician supports its broader operations. Obtain the Radio Direction Finder for the late search.</li>");
        s.AppendLine("<li><b>Follow leads across both regions.</b> Search systematically rather than repeatedly looting the same route. As the campaign progresses, use the map and city coordinates to cover unvisited areas.</li>");
        s.AppendLine("<li><b>Recover every G.U.B. scientist.</b> The eight scientists are the win condition. When the search narrows, use the Radio Direction Finder to close the remaining gaps and bring them home.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        s.AppendLine("<li><b>Protect fuel first.</b> Fuel is both a resource and a mobility budget. The Gang Status screen subtracts a two-move reserve for every vehicle; examine supplies to see the stored total.</li>");
        s.AppendLine("<li><b>Use roads as supply routes.</b> Roads improve movement and make fuel storage tanks common enough to support long-range travel. Stock up before crossing sparse terrain.</li>");
        s.AppendLine("<li><b>Match vehicle to role.</b> Light vehicles scout, buses carry people, and trailers carry supplies. Do not treat top speed as the only important statistic.</li>");
        s.AppendLine("<li><b>Keep the gang staffed.</b> Vehicle seats cap how many crew can ride safely. Better ranks improve the gang's capability, but every person also consumes supplies.</li>");
        s.AppendLine("<li><b>Loot efficiently.</b> Farms and ranches are food sources, armories and gun shops provide guns, tire stores provide tires, and fuel storage tanks are the richest fuel find. Ammunition comes with guns rather than from a separate loot category.</li>");
        s.AppendLine("<li><b>Repair before risk.</b> Damage, tires, and armour determine whether the fleet can survive the next encounter. Upgrade shops can strengthen a promising vehicle instead of replacing it.</li>");
        s.AppendLine("<li><b>Track untouched cities.</b> Their starting supply is finite. Use the gazetteer and map to plan a route through high-value cities rather than backtracking blindly.</li>");
        s.AppendLine("<li><b>Plan map transitions through play.</b> The two overland maps are separate loaded regions. Treat each as its own expedition and avoid assuming a position change alone will move the gang between them.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.9em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .hint { color: #555; font-size: 0.92em; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        table.facts th { width: 160px; white-space: nowrap; }
        .compact { font-size: 0.88em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
