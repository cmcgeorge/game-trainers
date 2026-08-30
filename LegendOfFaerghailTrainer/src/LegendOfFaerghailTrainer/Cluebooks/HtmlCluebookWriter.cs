using System.Text;
using GameTrainers.Common.Documents;
using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var body = new StringBuilder();
        body.Append("<header><p class=eyebrow>1990 DOS RPG reference</p><h1>Legend of Faerghail</h1><p class=lede>A practical atlas and companion guide for the world of Faerghail.</p></header>");
        body.Append("<nav><b>Contents</b><a href=#world>World</a>");
        if (cluebook.Options.IncludeMaps) body.Append("<a href=#maps>Maps</a>");
        if (cluebook.Options.IncludeSpells) body.Append("<a href=#spells>Spells</a>");
        if (cluebook.Options.IncludeItems) body.Append("<a href=#items>Items</a>");
        if (cluebook.Options.IncludeClasses) body.Append("<a href=#people>Races &amp; trades</a>");
        if (cluebook.Options.IncludeWalkthrough) body.Append("<a href=#walkthrough>Walkthrough</a>");
        if (cluebook.Options.IncludeStrategy) body.Append("<a href=#strategy>Strategy</a>");
        body.Append("</nav>");
        World(body);
        if (cluebook.Options.IncludeMaps) Maps(body, cluebook.Maps);
        if (cluebook.Options.IncludeSpells) Spells(body);
        if (cluebook.Options.IncludeItems) Items(body);
        if (cluebook.Options.IncludeClasses) People(body);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(body);
        if (cluebook.Options.IncludeStrategy) Strategy(body);
        return new HtmlPage("Legend of Faerghail cluebook").Style(Css).Append(body.ToString()).ToHtml();
    }

    private static void World(StringBuilder s)
    {
        s.Append("<section id=world><h2>The journey</h2><p>Faerghail is a party-based fantasy adventure. Keep a balanced group, preserve rations and magic for difficult passages, and use the monastery and other safe places to prepare before committing to deeper routes.</p><table><tr><th>Game</th><td>Legend of Faerghail</td></tr><tr><th>Developer</th><td>").Append(E(GameFacts.Developer)).Append("</td></tr><tr><th>Publisher</th><td>").Append(E(GameFacts.Publisher)).Append("</td></tr><tr><th>Build</th><td>").Append(E(GameFacts.BuildStamp)).Append("</td></tr><tr><th>Party</th><td>Up to 6 companions</td></tr></table></section>");
    }

    private static void Maps(StringBuilder s, IReadOnlyList<AreaLevel> maps)
    {
        s.Append("<section id=maps><h2>Area maps</h2><p>These are reference plans: north is up and west is left. They do not represent a live position and cannot teleport the party.</p><p class=legend><i></i> wall <i class=floor></i> walkable floor <i class=poi></i> point of interest</p>");
        foreach (var map in maps)
        {
            s.Append("<article><h3>").Append(E(map.Name)).Append("</h3><p>").Append(E(map.Description)).Append("</p>").Append(Svg(map));
            s.Append("<table><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in map.Pois) s.Append("<tr><td>").Append(E(poi.Position)).Append("</td><td>").Append(E(poi.Name)).Append("</td><td>").Append(E(poi.Description)).Append("</td></tr>");
            s.Append("</table></article>");
        }
        s.Append("</section>");
    }

    private static string Svg(AreaLevel map)
    {
        const int cell = 16;
        const int pad = 18;
        var svg = SvgCanvas.Responsive(pad * 2 + cell * map.Width, pad * 2 + cell * map.Height, map.Name + " map");
        svg.Rect(0, 0, pad * 2 + cell * map.Width, pad * 2 + cell * map.Height, ("fill", "#14151a"));
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell, ("fill", map.Grid[x, y] == CellKind.Wall ? "#3a3d4a" : "#1e1f26"));
        foreach (var poi in map.Pois)
        {
            double x = pad + poi.X * cell;
            double y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, poi.Name + ": " + poi.Description, ("fill", "#c89b3c"));
            svg.Text(x + cell / 2.0, y + 12, "•", ("text-anchor", "middle"), ("font-size", 14), ("fill", "#14151a"));
        }
        return svg.ToSvg();
    }

    private static void Spells(StringBuilder s)
    {
        s.Append("<section id=spells><h2>Spells</h2><p>The game spell table, including special and monster effects retained under their original names.</p><table><tr><th>Id</th><th>Spell</th></tr>");
        foreach (var spell in SpellBook.All) s.Append("<tr><td>").Append(spell.Id).Append("</td><td>").Append(E(spell.Name)).Append("</td></tr>");
        s.Append("</table></section>");
    }

    private static void Items(StringBuilder s)
    {
        s.Append("<section id=items><h2>Items</h2><p>Names and shop prices come from the game's own item table.</p><table><tr><th>Id</th><th>Item</th><th>Shop price</th></tr>");
        foreach (var item in ItemBook.All) s.Append("<tr><td>").Append(item.Id).Append("</td><td>").Append(E(item.Name)).Append("</td><td>").Append(item.Price).Append("</td></tr>");
        s.Append("</table></section>");
    }

    private static void People(StringBuilder s)
    {
        s.Append("<section id=people><h2>Races and trades</h2><div class=columns><div><h3>Races</h3><table><tr><th>Id</th><th>Race</th></tr>");
        for (int i = 0; i < RaceBook.Count; i++) s.Append("<tr><td>").Append(i).Append("</td><td>").Append(E(RaceBook.NameOf(i))).Append("</td></tr>");
        s.Append("</table></div><div><h3>Trades</h3><table><tr><th>Id</th><th>Game name</th><th>Manual note</th></tr>");
        for (int i = 0; i < ClassBook.Count; i++) s.Append("<tr><td>").Append(i).Append("</td><td>").Append(E(ClassBook.NameOf(i))).Append("</td><td>").Append(E(ClassBook.DescriptionOf(i))).Append("</td></tr>");
        s.Append("</table></div></div></section>");
    }

    private static void Walkthrough(StringBuilder s) => s.Append("<section id=walkthrough><h2>Walkthrough</h2><ol><li>Build a balanced party and make sure each companion has suitable weapons, armour, rations, and a role.</li><li>Use the valley and monastery to establish supplies before exploring the catacombs.</li><li>Map each branch as you move through the catacombs and mines; retreat to a known safe route when resources run low.</li><li>Use the pyramid and temple landmarks to orient the later journey, then prepare carefully for the castle and mountain.</li></ol></section>");
    private static void Strategy(StringBuilder s) => s.Append("<section id=strategy><h2>Strategy notes</h2><ul><li>Keep the party equipped before spending gold on marginal upgrades.</li><li>Use trained abilities deliberately: lock picking and trap skills protect scarce resources, while negotiating helps at peaceful encounters.</li><li>Rest before a new area rather than entering it with depleted magic and rations.</li><li>Carry multiple languages across the party; a single specialist can leave the group unable to understand an encounter.</li></ul></section>");

    private static string E(string value) => HtmlPage.Escape(value);

    private const string Css = "body{max-width:1100px;margin:0 auto;padding:2rem;background:#f4f1e9;color:#25251f;font:16px Georgia,serif;line-height:1.55}header{border-bottom:4px solid #413d31;padding-bottom:1rem}.eyebrow{text-transform:uppercase;letter-spacing:.12em;font:12px monospace;color:#766b4d}h1,h2,h3{font-family:Georgia,serif;color:#292719}h1{font-size:3rem;margin:.1rem 0}.lede{font-size:1.2rem}nav{display:flex;gap:1rem;flex-wrap:wrap;padding:1rem 0;border-bottom:1px solid #b9b09a}nav a{color:#4b4023}section{margin:3rem 0}article{background:#fffdf7;padding:1rem 1.25rem;margin:1.5rem 0;border-left:5px solid #766b4d}table{width:100%;border-collapse:collapse;margin:1rem 0}th,td{padding:.35rem .55rem;border-bottom:1px solid #d7d0be;text-align:left}th{background:#e8e1ce}.columns{display:grid;grid-template-columns:1fr 1fr;gap:1.5rem}svg{display:block;max-width:560px;margin:1rem 0;border:1px solid #5d5d58}.legend i{display:inline-block;width:1em;height:1em;background:#3a3d4a;border:1px solid #14151a;margin:0 .25rem;vertical-align:middle}.legend .floor{background:#1e1f26}.legend .poi{background:#c89b3c}@media(max-width:700px){body{padding:1rem;font-size:15px}h1{font-size:2.2rem}.columns{grid-template-columns:1fr}table{font-size:.86rem}}";
}
