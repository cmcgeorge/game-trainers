using System.Text;
using GameTrainers.Common.Documents;
using Questron2Trainer.Game;

namespace Questron2Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Questron II — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Questron II</h1>");
        markup.AppendLine("<p class=\"lede\">A companion for SSI and Westwood Associates' 1988 single-character RPG.</p>");
        Contents(markup, cluebook.Options);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(markup);
        if (cluebook.Options.IncludeEquipment) Equipment(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, CluebookOptions options)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (options.IncludeSpells) markup.AppendLine("<li><a href=\"#spells\">Spells</a></li>");
        if (options.IncludeEquipment) markup.AppendLine("<li><a href=\"#equipment\">Equipment and items</a></li>");
        if (options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The game at a glance</h2><table class=\"ref\">");
        Row(markup, "Title", GameFacts.GameTitle);
        Row(markup, "Year", GameFacts.ReleaseYear.ToString());
        Row(markup, "Publisher", GameFacts.Publisher);
        Row(markup, "Developer", GameFacts.Developer);
        Row(markup, "Character", "One adventurer with five attributes: Charisma, Strength, Agility, Stamina, and Intelligence.");
        Row(markup, "Magic", $"{SpellBook.Count} spells, including {SpellBook.BuyableCount} available in towns.");
        Row(markup, "Equipment", $"{WeaponBook.Count} weapons, {ArmorBook.Count} armor types, and {ItemBook.Count} key items.");
        markup.AppendLine("</table>");
        markup.AppendLine("<p>Explore towns, wilderness, dungeons, islands, and ancient structures. The maps in this cluebook are reference plans for important areas; they are not live position tracking or a teleport tool.</p>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2><p>North is at the top and west is at the left. Coordinates begin at (0, 0).</p>");
        markup.AppendLine("<ul class=\"legend\"><li><b>T</b> town or route</li><li><b>D</b> dungeon or stairs</li><li><b>C</b> castle</li><li><b>I</b> important item</li><li><b>N</b> NPC</li><li><b>S</b> shore</li><li><b>B</b> boss</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3>Area {area.Index + 1}: {Escape(area.Name)}</h3><p>{Escape(area.Description)}</p>");
            markup.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                markup.AppendLine($"<tr><td>{Escape(poi.Position)}</td><td>{Escape(poi.Name)}</td><td>{Escape(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        int pad = 20;
        int width = pad * 2 + area.Width * cell;
        int height = pad * 2 + area.Height * cell;
        var svg = SvgCanvas.Responsive(width, height, $"Map of {area.Name}");
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

    private static (string fill, string label) PoiStyle(string name) => name switch
    {
        "Stairs Up" => ("#799BD7", "U"),
        "Stairs Down" => ("#799BD7", "D"),
        var n when n.Contains("Town") || n.Contains("Gate") || n.Contains("Trail") || n.Contains("Road") => ("#6FC276", "T"),
        var n when n.Contains("Dungeon") || n.Contains("Entrance") || n.Contains("Passage") => ("#799BD7", "D"),
        var n when n.Contains("Boss") || n.Contains("Gargoyle") || n.Contains("Keeper") => ("#D77070", "B"),
        var n when n.Contains("Castle") => ("#B070E0", "C"),
        var n when n.Contains("Shore") || n.Contains("Harbor") => ("#6FC276", "S"),
        var n when n.Contains("Cache") || n.Contains("Relic") || n.Contains("Treasure") => ("#C89B3C", "I"),
        _ => ("#C89B3C", "N"),
    };

    private static void Spells(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"spells\">Spells</h2><table class=\"ref\"><tr><th>#</th><th>Name</th><th>Availability</th><th>Effect</th></tr>");
        foreach (var spell in SpellBook.Spells)
            markup.AppendLine($"<tr><td>{spell.Id}</td><td>{Escape(spell.Name)}</td><td>{(spell.Buyable ? "Town shop" : "Special")}</td><td>{Escape(spell.Description)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Equipment(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"equipment\">Equipment and items</h2>");
        Table(markup, "Weapons", WeaponBook.Weapons.Select(value => (value.Id, value.Name, "Weapon")));
        Table(markup, "Armor", ArmorBook.Armors.Select(value => (value.Id, value.Name, "Armor")));
        Table(markup, "Keys, quest items, and transport", ItemBook.Items.Select(value => (value.Id, value.Name, value.Category)));
    }

    private static void Table(StringBuilder markup, string title, IEnumerable<(int Id, string Name, string Type)> values)
    {
        markup.AppendLine($"<h3>{Escape(title)}</h3><table class=\"ref\"><tr><th scope=\"col\">#</th><th scope=\"col\">Name</th><th scope=\"col\">Type</th></tr>");
        foreach (var value in values)
            markup.AppendLine($"<tr><td>{value.Id}</td><td>{Escape(value.Name)}</td><td>{Escape(value.Type)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        markup.AppendLine("<li>Begin at Redstone Castle and speak with the king.</li>");
        markup.AppendLine("<li>Visit Hidden Rock and Bay View for equipment, supplies, food, and spells.</li>");
        markup.AppendLine("<li>Explore the Great Plains and enter the Dungeon of Despair only when prepared.</li>");
        markup.AppendLine("<li>Use keys and recovered artifacts to open the route through the Hall of the Gargoyle.</li>");
        markup.AppendLine("<li>Search the Tomb of Grelminar and the Pyramid for quest items and guidance.</li>");
        markup.AppendLine("<li>Acquire transport for island and shore routes, then make the final approach to the Final Castle.</li>");
        markup.AppendLine("<li>Save resources for the final guardian and complete the king's quest.</li></ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        markup.AppendLine("<li>Keep food stocked before long wilderness or dungeon excursions.</li>");
        markup.AppendLine("<li>Improve all five attributes steadily; Strength and Stamina are especially useful for surviving combat.</li>");
        markup.AppendLine("<li>Buy spells in towns and preserve charges for difficult encounters.</li>");
        markup.AppendLine("<li>Record where each colored key and quest item was found before committing to a deep route.</li>");
        markup.AppendLine("<li>Use towns and safe locations to restock before attempting a boss area.</li>");
        markup.AppendLine("<li>Keep a separate save before entering a major dungeon or the Final Castle.</li></ul>");
    }

    private static void Row(StringBuilder markup, string label, string value) => markup.AppendLine($"<tr><th>{Escape(label)}</th><td>{Escape(value)}</td></tr>");
    private static string Escape(string value) => HtmlPage.Escape(value);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 900px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        .legend { display: flex; flex-wrap: wrap; gap: 0.5em 1.5em; padding-left: 1.2em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
