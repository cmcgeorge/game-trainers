using System.Text;
using GameTrainers.Common.Documents;
using QuestForGlory1Trainer.Game;

namespace QuestForGlory1Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Quest for Glory I: So You Want to Be a Hero — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine("<h1>Quest for Glory I: So You Want to Be a Hero</h1>");
        markup.AppendLine("<p class=\"lede\">A Spielburg Valley cluebook with area maps, character guidance, and a route to the endgame.</p>");
        Contents(markup, cluebook.Options);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeSkills) SkillsAndSpells(markup);
        if (cluebook.Options.IncludeClasses) Classes(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">Spielburg Valley</a></li>");
        if (options.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (options.IncludeSkills) s.AppendLine("<li><a href=\"#skills\">Skills and spells</a></li>");
        if (options.IncludeClasses) s.AppendLine("<li><a href=\"#classes\">Character classes</a></li>");
        if (options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">Spielburg Valley</h2>");
        s.AppendLine("<p>Quest for Glory I is Sierra's 1989 adventure/RPG hybrid. As a Fighter, Magic User, or Thief, explore Spielburg Valley, train your abilities, solve its puzzles, and end the curse over Baron Stefan's castle.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Feature</th><th>Details</th></tr>");
        Row(s, "Setting", "Spielburg Valley, a forested valley beneath a cursed castle");
        Row(s, "Classes", "Fighter, Magic User, Thief");
        Row(s, "Main hub", "Spielburg Town: tavern, inn, shops, healer, and sheriff");
        Row(s, "Goal", "Break the curse, rescue Elsa, and defeat the brigand threat");
        s.AppendLine("</table>");
    }

    private static void Maps(StringBuilder s, Cluebook cluebook)
    {
        s.AppendLine("<h2 id=\"maps\">Area maps</h2>");
        s.AppendLine("<p>These reference maps show important routes and landmarks. North is at the top and west is at the left.</p>");
        s.AppendLine("<ul class=\"legend\"><li><b>T</b> Town</li><li><b>C</b> Castle</li><li><b>B</b> Brigands</li><li><b>H</b> Baba Yaga</li><li><b>E</b> Erana's Peace</li><li><b>I</b> Item</li><li><b>N</b> NPC</li><li><b>S</b> Start</li></ul>");
        foreach (var area in cluebook.Areas)
        {
            s.AppendLine($"<h3 id=\"area-{area.Index}\">{E(area.Name)}</h3>");
            s.AppendLine($"<p>{E(area.Description)}</p>");
            s.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            s.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                s.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            s.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        int pad = 20;
        int width = pad * 2 + cell * area.Width;
        int height = pad * 2 + cell * area.Height;
        var svg = SvgCanvas.Responsive(width, height, $"Map of {area.Name}");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell,
                    ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            var (fill, label) = PoiStyle(poi.Name);
            double x = pad + poi.X * cell;
            double y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string Fill, string Label) PoiStyle(string name) => name switch
    {
        "Town Gate" or "Spielburg Town" or "Sheriff's Office" or "Acker Berg Tavern" or "Bakery" or "General Store" or "Healer's Hut" or "Dry Grape Inn" => ("#B58247", "T"),
        "Castle Spielburg" or "Drawbridge" or "Courtyard" => ("#8291A6", "C"),
        "Brigand Trail" or "Camp Approach" or "Barracks" or "Brigand Leader" => ("#B45850", "B"),
        "Baba Yaga's Hut" => ("#9A6BB4", "H"),
        "Erana's Peace" => ("#6FC276", "E"),
        "Start" => ("#B070E0", "S"),
        "Baron Stefan" or "Elsa's Room" or "Mead Brewer" or "Dryad" => ("#799BD7", "N"),
        _ => ("#E0B040", "I"),
    };

    private static void SkillsAndSpells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"skills\">Skills and spells</h2><h3>Character skills</h3>");
        s.AppendLine("<table class=\"ref\"><tr><th>Skill</th><th>Notes</th></tr>");
        foreach (var skill in SkillBook.Stats)
            s.AppendLine($"<tr><td>{E(skill.Name)}</td><td>{E(skill.Notes)}</td></tr>");
        s.AppendLine("</table><h3>Magic-user spells</h3>");
        s.AppendLine("<table class=\"ref\"><tr><th>Spell</th><th>Use</th></tr>");
        Spell(s, "Zap", "A quick magical attack.");
        Spell(s, "Open", "Opens locked objects and doors.");
        Spell(s, "Detect Magic", "Reveals magical properties.");
        Spell(s, "Fetch", "Retrieves objects at a distance.");
        Spell(s, "Flame Dart", "A stronger ranged attack.");
        Spell(s, "Force Bolt", "A powerful magical blast.");
        Spell(s, "Calm", "Pacifies creatures and hostile situations.");
        Spell(s, "Hide", "Makes the hero harder to notice.");
        s.AppendLine("</table>");
    }

    private static void Classes(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"classes\">Character classes</h2><table class=\"ref\"><tr><th>Class</th><th>Strengths</th><th>Advice</th></tr>");
        s.AppendLine("<tr><td>Fighter</td><td>Weapon Use, Parry, Dodge, high survivability</td><td>Train combat skills often and carry healing potions.</td></tr>");
        s.AppendLine("<tr><td>Magic User</td><td>Spells, Magic skill, flexible puzzle solutions</td><td>Conserve mana and learn spells for both combat and utility.</td></tr>");
        s.AppendLine("<tr><td>Thief</td><td>Stealth, Pick Locks, climbing, alternate routes</td><td>Practice stealth and keep Thief's Tools for locked obstacles.</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        s.AppendLine("<li>Explore Spielburg Town. Visit the sheriff, tavern, inn, shops, and healer for supplies and rumours.</li>");
        s.AppendLine("<li>Train your class skills regularly. Combat, stealth, climbing, and magic all improve through use.</li>");
        s.AppendLine("<li>Use Erana's Peace as a safe resting point while mapping the forest roads.</li>");
        s.AppendLine("<li>Investigate the Dryad's Tree, caves, goblin camp, and Mead Maze for quest items and clues.</li>");
        s.AppendLine("<li>Reach the brigand camp using your class's strongest approach: combat, magic, or stealth and disguise.</li>");
        s.AppendLine("<li>Gather the ingredients for the dispel potion, then prepare for Baba Yaga's hut.</li>");
        s.AppendLine("<li>Break the curse surrounding Castle Spielburg, rescue Elsa, and resolve the valley's final threats.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy</h2><ul>");
        s.AppendLine("<li><b>Save frequently.</b> Keep multiple saves before major puzzles and hostile areas.</li>");
        s.AppendLine("<li><b>Listen for rumours.</b> Tavern conversations and town NPCs point toward required items and routes.</li>");
        s.AppendLine("<li><b>Train deliberately.</b> Repeated use improves class skills, opening stronger solutions later.</li>");
        s.AppendLine("<li><b>Carry essentials.</b> Healing potions, food, and the right tools prevent avoidable detours.</li>");
        s.AppendLine("<li><b>Respect time.</b> NPC schedules and some quests change with the day and time.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static void Spell(StringBuilder s, string name, string use) => s.AppendLine($"<tr><td>{E(name)}</td><td>{E(use)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);

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
        .legend { display: flex; flex-wrap: wrap; gap: 0.5em 1em; padding-left: 1.2em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
