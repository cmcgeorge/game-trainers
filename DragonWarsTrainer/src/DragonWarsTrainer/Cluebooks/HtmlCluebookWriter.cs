using System.Text;
using GameTrainers.Common.Documents;
using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Dragon Wars — Oceana cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        s.AppendLine("<h1>Dragon Wars</h1>");
        s.AppendLine("<p class=\"lede\">A field guide to Interplay's 1989 fantasy RPG, its regions, skills, spells, and the road to Namtar.</p>");
        Contents(s, cluebook);
        Overview(s);
        if (cluebook.Options.IncludeAreas) Areas(s, cluebook);
        if (cluebook.Options.IncludeSpells || cluebook.Options.IncludeSkills) References(s, cluebook);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s, cluebook);
        if (cluebook.Options.IncludeStrategy) Strategy(s);
        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (c.Options.IncludeAreas) s.AppendLine("<li><a href=\"#areas\">Areas of Oceana</a></li>");
        if (c.Options.IncludeSpells || c.Options.IncludeSkills) s.AppendLine("<li><a href=\"#references\">Spells and skills</a></li>");
        if (c.Options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (c.Options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Setting", "Oceana, kingdom of Avalon");
        Row(s, "Developer", "Interplay Entertainment");
        Row(s, "Year", "1989");
        Row(s, "Party", "Up to six characters");
        Row(s, "Development", "Skill points instead of traditional classes");
        Row(s, "Goal", "Escape Purgatory, defeat Namtar, and cast his Dead Body into the Pit");
        s.AppendLine("</table>");
        s.AppendLine("<h3>Story</h3>");
        s.AppendLine("<p>Drake of Phoebus has thrown your prisoners into Purgatory. Escape the slave camp, cross a war-torn land, and gather the relics needed to challenge Namtar, the necromancer behind the dragon siege. The Sword of Freedom, Dragon Gem, Silver Key, and the help of the old gods turn a desperate escape into a campaign to save Oceana.</p>");
        s.AppendLine("<p>Dragon Wars has no fixed character classes. Spend advancement points on attributes, weapon proficiencies, lore, movement skills, and schools of magic, shaping each character around the party's needs.</p>");
    }

    private static void Areas(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"areas\">Areas of Oceana</h2>");
        s.AppendLine("<p>The map is a chain of connected boards rather than a single open world. Record exits and use lore skills before committing the party to a dangerous route.</p>");
        foreach (var area in c.Areas)
        {
            s.AppendLine($"<h3 id=\"area-{area.Id:X2}\">{E(area.Name)}</h3>");
            s.AppendLine($"<p><b>Board:</b> 0x{area.Id:X2}, {E(area.Size)}. {E(area.Notes)}</p>");
            s.AppendLine(AreaSvg(area, c.Options.MapCellSize));
            if (area.Locations.Count == 0) continue;
            s.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Location</th><th>Notes</th></tr>");
            foreach (var location in area.Locations)
                s.AppendLine($"<tr><td>{E(location.Coord)}</td><td>{E(location.Name)}</td><td>{E(location.Notes)}</td></tr>");
            s.AppendLine("</table>");
        }
    }

    private static string AreaSvg(MapArea area, int cell)
    {
        int width = area.GridWidth;
        int height = area.GridHeight;
        const int pad = 12;
        int totalWidth = pad * 2 + width * cell;
        int totalHeight = pad * 2 + height * cell;
        var svg = SvgCanvas.Responsive(totalWidth, totalHeight, $"{area.Name} area map");
        svg.Rect(0, 0, totalWidth, totalHeight, ("fill", "#17202A"));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                svg.Rect(pad + x * cell, pad + (height - y - 1) * cell, cell, cell, ("fill", (x + y) % 2 == 0 ? "#253746" : "#21313F"));
        foreach (var location in area.Locations)
        {
            int x = pad + location.X * cell;
            int y = pad + (height - location.Y - 1) * cell;
            svg.Rect(x, y, cell, cell, ("fill", "#D6A84F"));
            svg.Text(x + cell / 2.0, y + cell * 0.7, "•", ("text-anchor", "middle"), ("font-size", cell * 0.8), ("fill", "#17202A"));
        }
        return svg.ToSvg();
    }

    private static void References(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"references\">Spells and skills</h2>");
        if (c.Options.IncludeSpells)
        {
            s.AppendLine("<h3>Spells</h3><table class=\"ref\"><tr><th>School</th><th>Spell</th><th>Cost</th><th>Effect</th></tr>");
            foreach (var spell in c.Spells)
                s.AppendLine($"<tr><td>{E(spell.School)}</td><td>{E(spell.Name)}</td><td>{E(spell.Cost)}</td><td>{E(spell.Effect)}</td></tr>");
            s.AppendLine("</table>");
        }
        if (c.Options.IncludeSkills)
        {
            s.AppendLine("<h3>Skills</h3><table class=\"ref\"><tr><th>#</th><th>Skill</th><th>Use</th></tr>");
            foreach (var skill in c.Skills)
                s.AppendLine($"<tr><td>{skill.Index + 1}</td><td>{E(skill.Name)}</td><td>{E(skill.Description)}</td></tr>");
            s.AppendLine("</table>");
        }
    }

    private static void Walkthrough(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        foreach (var section in c.Walkthrough)
            s.AppendLine($"<li><b>{E(section.Title)}.</b> {E(section.Body)}</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        s.AppendLine("<li><b>Build a toolkit party.</b> Cover Bandage, Swim, Climb, Lockpick, Arcane Lore, and the four environmental lore skills before specializing in damage.</li>");
        s.AppendLine("<li><b>Use Stun.</b> Most physical attacks remove Stun before Health; keep enemies unconscious while the party finishes the fight.</li>");
        s.AppendLine("<li><b>Spend Power carefully.</b> Recharge at pools, shrines, and safe camps. Variable-cost spells are strongest when invested into a school rank that supports them.</li>");
        s.AppendLine("<li><b>Explore in sequence.</b> Key items open routes: escape Purgatory, repair Lanac'toor, resolve Byzanople, then pursue the relics needed for Nisir and Namtar.</li>");
        s.AppendLine("<li><b>Summon before difficult battles.</b> Elementals, beasts, spirits, and salamanders add bodies and damage while preserving the party's Health.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 980px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 2em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table { border-collapse: collapse; width: 100%; margin: 1em 0; }
        th { background: #e8e8e8; text-align: left; }
        th, td { padding: 4px 8px; border: 1px solid #ccc; vertical-align: top; }
        table.facts th { width: 180px; white-space: nowrap; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #aaa; }
        """;
}
