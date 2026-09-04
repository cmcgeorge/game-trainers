using System.Text;
using GameTrainers.Common.Documents;
using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "The Bard's Tale I — Skara Brae cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        s.AppendLine("<h1>The Bard's Tale: Tales of the Unknown</h1>");
        s.AppendLine("<p class=\"lede\">A practical cluebook for exploring frozen Skara Brae, mastering the arts, and defeating Mangar.</p>");
        Contents(s, cluebook);
        Overview(s);
        City(s, cluebook.City);
        if (cluebook.Options.IncludeMaps) Maps(s, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(s);
        if (cluebook.Options.IncludeClasses) Classes(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);
        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        s.AppendLine("<li><a href=\"#city\">Skara Brae</a></li>");
        if (c.Options.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Dungeon maps and levels</a></li>");
        if (c.Options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Mage spells and bard songs</a></li>");
        if (c.Options.IncludeClasses) s.AppendLine("<li><a href=\"#classes\">Classes and races</a></li>");
        if (c.Options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (c.Options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<p>The Bard's Tale is a first-person party dungeon crawler set in Skara Brae. Mangar the Dark has frozen the city and sealed it behind monsters, traps, and magical barriers. Build a party, explore sixteen levels across the city's dungeons, recover the key, and climb Mangar's Tower to end the curse.</p>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Setting", "Skara Brae, a city frozen by Mangar");
        Row(s, "Dungeon areas", "Catacombs, Harkyn's Castle, Kylearan's Tower, Mangar's Tower, and the sewers");
        Row(s, "Playable classes", "Warrior, Paladin, Rogue, Bard, Hunter, Monk, Conjurer, Magician, Sorcerer, Wizard");
        Row(s, "Party", "Seven packed slots, including a special summon slot");
        Row(s, "Magic", $"{Spellbook.All.Count} spells across four arts, plus {Spellbook.BardSongs.Length} bard songs");
        s.AppendLine("</table>");
        s.AppendLine("<h3>The central objective</h3><p>Mangar's magic is sustained from his tower. The route is not a straight descent: investigate the city, learn which dungeon entrances connect, collect the items that open deeper routes, and return to town often to heal, save, identify equipment, and advance your characters.</p>");
    }

    private static void City(StringBuilder s, GameMap city)
    {
        s.AppendLine("<h2 id=\"city\">Skara Brae</h2>");
        s.AppendLine($"<p>{E(city.Description)} The city is your safe hub and the reference point for every expedition. Streets connect the guild, inn, temple, taverns, shops, review board, and dungeon entrances.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Place</th><th>Use</th></tr>");
        s.AppendLine("<tr><td>Adventurer's Guild</td><td>Create characters, form the party, and manage the roster.</td></tr>");
        s.AppendLine("<tr><td>Garth's Equipment Shoppe</td><td>Buy, sell, identify, and uncurse equipment.</td></tr>");
        s.AppendLine("<tr><td>Temple</td><td>Heal, cure, and restore fallen characters when you can afford it.</td></tr>");
        s.AppendLine("<tr><td>Review Board</td><td>Review statistics and take eligible characters up a level.</td></tr>");
        s.AppendLine("<tr><td>Taverns and Roscoe's</td><td>Recover resources and use the city's services between expeditions.</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Maps(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"maps\">Dungeon maps and levels</h2>");
        s.AppendLine("<p>Maps use the game's square grid: north is up and west is left. A pale edge is a wall, a green gap is a door, and amber dashed edges are secret or one-way passages. The Maps tab in the trainer includes the same area data and can track the party's position.</p>");
        s.AppendLine("<ul class=\"legend\"><li><span class=\"swatch sw-wall\"></span>wall</li><li><span class=\"swatch sw-door\"></span>door</li><li><span class=\"swatch sw-secret\"></span>secret door</li><li><span class=\"swatch sw-building\"></span>city building</li></ul>");
        s.AppendLine("<h3>Area connections</h3><p>The Wine Cellar leads into the sewers. The catacombs lie beneath the temple. Harkyn's Castle and Kylearan's Tower are separate challenges in the city. The final route is through the five levels of Mangar's Tower.</p>");
        foreach (var guide in c.Dungeons)
        {
            s.AppendLine($"<h3>{E(guide.Map.Name)}</h3><p>{E(guide.Map.Description)}</p>");
            s.AppendLine(DungeonSvg(guide.Map, c.Options.MapCellSize));
            s.AppendLine("<ul class=\"locations\">");
            foreach (var location in guide.KeyLocations) s.AppendLine($"<li>{E(location)}</li>");
            s.AppendLine("</ul>");
        }
    }

    private static string DungeonSvg(GameMap map, int cell)
    {
        const int pad = 18;
        var svg = SvgCanvas.Responsive(pad * 2 + cell * map.Width, pad * 2 + cell * map.Height, $"{map.Name} map");
        svg.Rect(0, 0, pad * 2 + cell * map.Width, pad * 2 + cell * map.Height, ("fill", "#14151A"));
        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
            {
                var square = map.Terrain[x, y];
                var fill = square.IsBlocked ? "#514354" : "#1E1F26";
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell, ("fill", fill));
                DrawEdge(svg, square.West, pad + x * cell, pad + y * cell, cell, false);
                DrawEdge(svg, square.North, pad + x * cell, pad + y * cell, cell, true);
                if (x == map.Width - 1) DrawEdge(svg, square.East, pad + (x + 1) * cell, pad + y * cell, cell, false);
                if (y == map.Height - 1) DrawEdge(svg, square.South, pad + x * cell, pad + (y + 1) * cell, cell, true);
                if (square.Label is { } label)
                    svg.Text(pad + x * cell + cell / 2.0, pad + y * cell + cell * 0.68, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", Math.Max(7, cell * 0.28).ToString(System.Globalization.CultureInfo.InvariantCulture)), ("fill", "#E0B040"));
            }
        return svg.ToSvg();
    }

    private static void DrawEdge(SvgCanvas svg, WallKind kind, int x, int y, int cell, bool horizontal)
    {
        if (!kind.IsDrawn()) return;
        var color = kind.IsSecret() || kind.IsOneWay() ? "#C89B3C" : kind.IsDoorway() ? "#6FC276" : "#B9BBC7";
        var dash = kind.IsSecret() ? "4 3" : "";
        if (horizontal)
            svg.Line(x, y, x + cell, y, ("stroke", color), ("stroke-width", "2"), ("stroke-dasharray", dash));
        else
            svg.Line(x, y, x, y + cell, ("stroke", color), ("stroke-width", "2"), ("stroke-dasharray", dash));
    }

    private static void Spells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"spells\">Mage spells and bard songs</h2>");
        s.AppendLine("<p>The four mage arts are learned in sequence as characters change class: Magician, Conjurer, Sorcerer, and Wizard. Spell levels are independent per art. A Bard uses songs rather than mage spells; keep a bard supplied with an instrument and remember that songs are sustained effects, not ordinary spell casts.</p>");
        foreach (var art in Enum.GetValues<SpellClass>().Where(a => a != SpellClass.None)) SpellTable(s, Spellbook.ArtName(art), Spellbook.For(art));
        s.AppendLine("<h3>Bard songs</h3><table class=\"ref\"><tr><th>Play order</th><th>Song</th><th>Use</th></tr>");
        var uses = new[] { "Offensive combat aid", "Improves exploration and awareness", "Protective effect", "Combat enhancement", "Travel and party support", "Luck and survival aid" };
        for (var i = 0; i < Spellbook.BardSongs.Length; i++) s.AppendLine($"<tr><td>{i + 1}</td><td>{E(Spellbook.BardSongs[i])}</td><td>{uses[i]}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void SpellTable(StringBuilder s, string title, IEnumerable<Spell> spells)
    {
        s.AppendLine($"<h3>{E(title)}</h3><table class=\"ref\"><tr><th>Level</th><th>Code</th><th>Name</th></tr>");
        foreach (var spell in spells) s.AppendLine($"<tr><td>{spell.Level}</td><td><code>{E(spell.Code)}</code></td><td>{E(spell.Name)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Classes(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"classes\">Classes and races</h2><p>Start with a durable front line, a Rogue for trapped chests, a Bard for sustained support, and at least one Conjurer or Magician. Advanced classes are reached by changing class after meeting the game's requirements; characters retain useful progress as they develop.</p>");
        s.AppendLine("<h3>Classes</h3><table class=\"ref\"><tr><th>Class</th><th>Role</th><th>Notes</th></tr>");
        foreach (var cls in ClassBook.Classes) s.AppendLine($"<tr><td>{E(cls.Name)}</td><td>{E(cls.Tag)}</td><td>{E(cls.Description)}</td></tr>");
        s.AppendLine("</table><h3>Races</h3><table class=\"ref\"><tr><th>Race</th><th>Notes</th></tr>");
        foreach (var race in ClassBook.Races) s.AppendLine($"<tr><td>{E(race.Name)}</td><td>{E(race.Description)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        s.AppendLine("<li><b>Establish a party.</b> Create a front line, a Rogue, a Bard, and starting spellcasters. Visit Garth's for equipment and save a copy before exploring.</li>");
        s.AppendLine("<li><b>Learn the city.</b> Draw Skara Brae and note every marked entrance. The city map is the key to returning from any dungeon without getting lost.</li>");
        s.AppendLine("<li><b>Explore the sewers and catacombs.</b> Work through the lower routes, search for stairs and useful equipment, and use the Temple and Review Board between trips.</li>");
        s.AppendLine("<li><b>Find the wizard's route.</b> Investigate Kylearan's Tower and Harkyn's Castle. The clues and items found there open the route to the deeper tower complex.</li>");
        s.AppendLine("<li><b>Get the key.</b> Push through the castle and catacomb objectives until the key needed for Mangar's Tower is recovered. Do not spend the key quest item or leave it behind when rearranging the party.</li>");
        s.AppendLine("<li><b>Climb Mangar's Tower.</b> Restock, heal, and bring your best identified equipment before entering. Advance one level at a time, marking stairs and checking for teleporters and traps.</li>");
        s.AppendLine("<li><b>Defeat Mangar.</b> At the top, use the Bard's sustained song and your strongest spells early, keep the front line healed, and finish the encounter to lift the freeze from Skara Brae.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        s.AppendLine("<li><b>Save and identify.</b> Unidentified equipment can be cursed. Return to town before a long expedition and keep a clean save.</li>");
        s.AppendLine("<li><b>Use the Bard early.</b> A song can protect or strengthen the whole party while spellcasters conserve charges for emergencies.</li>");
        s.AppendLine("<li><b>Protect the Rogue.</b> Let the Rogue handle chests and traps. A failed disarm can be more expensive than the treasure is worth.</li>");
        s.AppendLine("<li><b>Map every turn.</b> Mark doors, secret doors, one-way passages, stairs, teleports, and dead ends. The supplied Maps tab is useful for orientation, but your current exploration state still matters.</li>");
        s.AppendLine("<li><b>Change classes deliberately.</b> Advanced classes are powerful, but a class change can leave a character temporarily weaker. Plan the party's spell coverage before changing a caster.</li>");
        s.AppendLine("<li><b>Spend resources on progress.</b> Gold, healing, and spell charges are tools for reaching the next safe point. Do not attempt the upper tower under-levelled or with unidentified gear.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 980px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.9em; border-bottom: 2px solid #444; padding-bottom: .3em; }
        h2 { font-size: 1.45em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; }
        h3 { margin-top: 1.5em; } .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table.ref, table.facts { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th, table.ref td, table.facts th, table.facts td { border: 1px solid #ccc; padding: 4px 8px; text-align: left; vertical-align: top; }
        table.ref th, table.facts th { background: #e8e8e8; } table.facts th { width: 170px; white-space: nowrap; }
        .legend { list-style: none; padding: 0; } .legend li { display: inline-block; margin-right: 1em; }
        .swatch { display: inline-block; width: 14px; height: 14px; border: 1px solid #444; vertical-align: middle; margin-right: 4px; }
        .sw-wall { background: #B9BBC7; } .sw-door { background: #6FC276; } .sw-secret { background: #C89B3C; } .sw-building { background: #514354; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; } code { font-family: monospace; }
        """;
}
