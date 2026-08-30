using System.Text;
using GameTrainers.Common.Documents;
using AlternateRealityTrainer.Game;

namespace AlternateRealityTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Alternate Reality: The City — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>Alternate Reality: The City</h1>");
        s.AppendLine("<p class=\"lede\">A cluebook for the strange streets of Xebec's Demise: city services, attributes, potions, and surviving life after abduction.</p>");
        Contents(s, cluebook.Options);
        Overview(s);
        if (cluebook.Options.IncludeCityMap) CitySection(s, cluebook.Options.MapCellSize);
        if (cluebook.Options.IncludeAttributes) Attributes(s);
        if (cluebook.Options.IncludePotions) Potions(s);
        if (cluebook.Options.IncludeSurvival) Survival(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The City at a glance</a></li>");
        if (options.IncludeCityMap) s.AppendLine("<li><a href=\"#map\">City map and building locations</a></li>");
        if (options.IncludeAttributes) s.AppendLine("<li><a href=\"#attributes\">Attribute reference</a></li>");
        if (options.IncludePotions) s.AppendLine("<li><a href=\"#potions\">Potion reference</a></li>");
        if (options.IncludeSurvival) s.AppendLine("<li><a href=\"#survival\">Survival guide</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The City at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", GameFacts.GameTitle);
        Row(s, "Publisher", GameFacts.Publisher);
        Row(s, "City", GameFacts.CityName);
        Row(s, "Map", $"{GameFacts.CitySize} × {GameFacts.CitySize} squares");
        Row(s, "Character", "One adventurer");
        Row(s, "Game time", $"About one game hour per {GameFacts.RealMinutesPerGameHour} real minutes");
        s.AppendLine("</table>");
        s.AppendLine("<p>You are abducted by aliens and left to make a life in Xebec's Demise, a city of streets, services, guilds, and dangerous encounters. The City was planned as the first chapter of a larger Alternate Reality series, followed by The Dungeon, The Arena, The Palace, and The Wilderness.</p>");
    }

    private static void CitySection(StringBuilder s, int cellSize)
    {
        s.AppendLine("<h2 id=\"map\">City map and building locations</h2>");
        s.AppendLine("<p>The City is a 64 × 64 grid. Square 1N, 1E is the south-west corner: north increases upward and east increases rightward. The map marks every known service door; it does not reveal a current party position or provide teleportation.</p>");
        s.AppendLine(CitySvg(Math.Clamp(cellSize, 6, 20)));
        s.AppendLine("<p>Markers show inns, taverns, banks, shops, smithies, healers, and guilds. Building notes include prices, opening constraints, and known approaches where a direct route is blocked.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Type</th><th>Coordinate</th><th>Notes</th></tr>");
        foreach (var place in CityBook.Places)
            s.AppendLine($"<tr><td>{E(CityMap.LabelFor(place.Kind))}</td><td>{E(place.Coordinate)}</td><td>{E(place.Note)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static string CitySvg(int cell)
    {
        int margin = 24;
        int grid = GameFacts.CitySize * cell;
        int size = grid + margin * 2;
        var svg = SvgCanvas.Responsive(size, size, "Map of Xebec's Demise with known building locations");

        svg.Rect(0, 0, size, size, ("fill", "#FBF8F2"));
        svg.Rect(margin, margin, grid, grid, ("fill", "#F4EEDF"), ("stroke", "#B9AE9B"));

        for (int i = 0; i <= GameFacts.CitySize; i += 8)
        {
            int p = margin + i * cell;
            svg.Line(p, margin, p, margin + grid, ("stroke", "#D6CCBB"));
            svg.Line(margin, p, margin + grid, p, ("stroke", "#D6CCBB"));
        }

        foreach (var place in CityBook.Places)
        {
            int x = margin + (place.East - 1) * cell;
            int y = margin + (GameFacts.CitySize - place.North) * cell;
            svg.Rect(x + 1, y + 1, cell - 2, cell - 2, ("fill", CityMap.ColourFor(place.Kind)));
            svg.Text(x + cell / 2.0, y + cell * 0.72, place.Symbol.ToString(),
                ("text-anchor", "middle"), ("font-size", Math.Max(7, cell * 0.75)),
                ("font-family", "Arial, sans-serif"), ("font-weight", "bold"), ("fill", "#FFFFFF"));
        }

        return svg.ToSvg();
    }

    private static void Attributes(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"attributes\">Attribute reference</h2>");
        s.AppendLine("<p>Attributes have current, maximum, and natural-maximum values. A Wraith can drain current and maximum values while leaving the natural maximum intact. The status bar displays six attributes; Physical Speed remains hidden there.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Attribute</th><th>Abbreviation</th><th>Display</th></tr>");
        foreach (var attribute in AttributeBook.All)
            s.AppendLine($"<tr><td>{E(attribute.Name)}</td><td>{E(attribute.Abbreviation)}</td><td>{(attribute.Hidden ? "Hidden on the status bar" : "Shown on the status bar")}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<p>The visible status-bar order is STA, CHR, STR, INT, WIS, SKL. Guild visits can improve a related attribute, and the first visit to each guild is free.</p>");
    }

    private static void Potions(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"potions\">Potion reference</h2>");
        s.AppendLine("<p>Sealed potion identities are random when unsealed. Examine and taste before sipping; Wisdom and Intelligence improve identification. This table records known colour, taste, risk, and effect combinations.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Colour</th><th>Taste</th><th>Sip</th><th>Effect</th></tr>");
        foreach (var potion in PotionBook.All)
            s.AppendLine($"<tr><td>{E(potion.Colour)}</td><td>{E(potion.Taste)}</td><td>{E(potion.SipLabel)}</td><td>{E(potion.Effect)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine($"<p>Save these effects when found: {E(string.Join(", ", PotionBook.WorthHoarding))}.</p>");
    }

    private static void Survival(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"survival\">Survival guide</h2>");
        s.AppendLine("<p>Food, water, and rest are continuous concerns. Hunger, thirst, and weariness worsen while travelling, so carry food packets and water flasks, watch the condition banner, and use inns to recover before a long route.</p>");
        s.AppendLine("<ul>");
        s.AppendLine("<li><b>Hunger:</b> keep food on hand; Famished and Starving mean supplies need immediate attention.</li>");
        s.AppendLine("<li><b>Thirst:</b> carry water and do not rely on finding a tavern before Thirsty, Very Thirsty, or Parched conditions escalate.</li>");
        s.AppendLine("<li><b>Weariness:</b> rest before Tired, Very Tired, or Weary conditions leave you vulnerable during encounters.</li>");
        s.AppendLine("<li><b>Time:</b> pause with P while mapping or reviewing notes. Nights and rain both increase encounter frequency.</li>");
        s.AppendLine("<li><b>Conditions:</b> disease can surface days after contact with Giant Rats, Brown Mold, or Black Slime; seek a healer when affected.</li>");
        s.AppendLine("</ul>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        foreach (var tip in GameFacts.Tips)
            s.AppendLine($"<li>{E(tip)}</li>");
        s.AppendLine("<li>Do not remain in a tavern or bank at closing time: that type of building becomes barred to you permanently.</li>");
        s.AppendLine("<li>Do not leave encounters with Thieves or Muggers, because disengaging is when they rob you.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        table.facts th { width: 160px; white-space: nowrap; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
