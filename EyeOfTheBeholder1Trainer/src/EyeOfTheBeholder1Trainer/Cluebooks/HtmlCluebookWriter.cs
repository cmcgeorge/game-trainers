using System.Text;
using GameTrainers.Common.Documents;
using EyeOfTheBeholder1Trainer.Game;

namespace EyeOfTheBeholder1Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Eye of the Beholder — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine($"<h1>{E(GameFacts.GameTitle)}</h1>");
        markup.AppendLine("<p class=\"lede\">A cluebook for the 1991 Westwood and SSI dungeon-crawling classic, with all twelve levels, spell references, and a route to Xanathar.</p>");
        Contents(markup, cluebook);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(markup);
        if (cluebook.Options.IncludeClasses) CharacterReference(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (cluebook.Options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Dungeon maps</a></li>");
        if (cluebook.Options.IncludeSpells) markup.AppendLine("<li><a href=\"#spells\">Spells</a></li>");
        if (cluebook.Options.IncludeClasses) markup.AppendLine("<li><a href=\"#characters\">Races and classes</a></li>");
        if (cluebook.Options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (cluebook.Options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The game at a glance</h2><table class=\"ref\">");
        Row(markup, "Title", GameFacts.GameTitle);
        Row(markup, "Year", GameFacts.ReleaseYear.ToString());
        Row(markup, "Developer", GameFacts.Developer);
        Row(markup, "Dungeon", $"{GameFacts.DungeonLevels} levels, each {GameFacts.LevelGridSize}×{GameFacts.LevelGridSize} cells");
        Row(markup, "Party", $"Up to {GameFacts.MaxPartySize} adventurers");
        Row(markup, "Spells", $"{SpellBook.Spells.Count} spells: {SpellBook.MageSpells.Count} mage and {SpellBook.ClericSpells.Count} cleric");
        markup.AppendLine("</table>");
        markup.AppendLine("<p>Waterdeep's lords ask the party to enter the sewers beneath the Yawning Tavern, unravel the dungeon's threats, and end Xanathar's schemes. The maze rewards careful mapping, measured rests, and a balanced AD&amp;D party.</p>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Dungeon maps</h2><p>North is at the top and west is at the left. These maps are reference layouts; the trainer cannot track live position or teleport.</p>");
        markup.AppendLine("<ul class=\"legend\"><li><b>U/D</b> stairs up/down</li><li><b>P</b> portal</li><li><b>B</b> boss</li><li><b>S</b> secret</li><li><b>N</b> NPC</li><li><b>I</b> important item</li></ul>");
        foreach (var level in cluebook.Levels)
        {
            markup.AppendLine($"<h3>Level {level.Index + 1}: {E(level.Name)}</h3><p>{E(level.Description)}</p>");
            markup.AppendLine(DungeonSvg(level, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in level.Pois)
                markup.AppendLine($"<tr><td>{E(poi.Position)}</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string DungeonSvg(DungeonLevel level, int cell)
    {
        int pad = 20;
        int width = pad * 2 + cell * level.Width;
        int height = pad * 2 + cell * level.Height;
        var svg = SvgCanvas.Responsive(width, height, $"{level.Name} dungeon map");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell,
                    ("fill", level.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in level.Pois)
        {
            var (fill, label) = PoiColor(poi.Name);
            double x = pad + poi.X * cell;
            double y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"),
                ("font-family", "monospace"), ("font-size", cell * 0.6), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string Fill, string Label) PoiColor(string name) => name switch
    {
        "Stairs Up" => ("#6FC276", "U"),
        "Stairs Down" => ("#6FC276", "D"),
        "Portal" or "Secret Portal" => ("#799BD7", "P"),
        "Xanathar" or "Skeleton King" => ("#C86464", "B"),
        "Secret" => ("#B070E0", "S"),
        "Piergeiron" => ("#68BBC4", "N"),
        _ => ("#E0B040", "I"),
    };

    private static void Spells(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"spells\">Spells</h2><p>The game has mage and cleric spell lists over five levels.</p>");
        SpellTable(markup, "Mage spells", SpellBook.MageSpells);
        SpellTable(markup, "Cleric spells", SpellBook.ClericSpells);
    }

    private static void SpellTable(StringBuilder markup, string title, IReadOnlyList<SpellBook.SpellInfo> spells)
    {
        markup.AppendLine($"<h3>{E(title)}</h3><table class=\"ref\"><tr><th>Name</th><th>Level</th><th>Effect</th></tr>");
        foreach (var spell in spells)
            markup.AppendLine($"<tr><td>{E(spell.Name)}</td><td>{spell.Level}</td><td>{E(spell.Description)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void CharacterReference(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"characters\">Races, classes and alignment</h2>");
        markup.AppendLine("<h3>Races</h3><p>Human, Elf, Half-Elf, Dwarf, Gnome, and Halfling are represented by male and female entries in the game data.</p>");
        markup.AppendLine($"<p>{E(string.Join(", ", CharacterFormat.RaceNames))}</p>");
        markup.AppendLine("<h3>Classes</h3><p>Build a party with durable front-line fighters, magical offense, healing, and a thief for traps.</p>");
        markup.AppendLine($"<p>{E(string.Join(", ", CharacterFormat.ClassNames))}</p>");
        markup.AppendLine("<h3>Alignments</h3><p>Alignment influences party composition and available character choices.</p>");
        markup.AppendLine($"<p>{E(string.Join(", ", CharacterFormat.AlignmentNames))}</p>");
    }

    private static void Walkthrough(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        markup.AppendLine("<li>Build a balanced party in Waterdeep, then enter from the Yawning Tavern.</li>");
        markup.AppendLine("<li>Map the Sewers and Dwarven Ruins, collect the Dwarven Key, and solve the lever passages.</li>");
        markup.AppendLine("<li>Clear the Skeleton Crypts and survive the Drow Outpost before pressing into the lower ruins.</li>");
        markup.AppendLine("<li>Use the Hall of the Dead portal to explore the Secret Level when prepared for its rewards.</li>");
        markup.AppendLine("<li>Navigate the Catacomb teleport maze, then prepare anti-psionic and anti-beholder resources for the lower levels.</li>");
        markup.AppendLine("<li>Carry the Scroll of Xanathar through the final lairs and defeat Xanathar in level 11.</li>");
        markup.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        markup.AppendLine("<li><b>Map carefully.</b> Record turns, doors, stairs, and teleporters before committing to a long route.</li>");
        markup.AppendLine("<li><b>Protect the rear ranks.</b> Put fighters, paladins, and rangers in front; preserve spellcasters and thieves.</li>");
        markup.AppendLine("<li><b>Rest deliberately.</b> Food and spell resources are limited; retreat before the party is too depleted to survive an encounter.</li>");
        markup.AppendLine("<li><b>Check secrets.</b> Search suspicious walls for hidden compartments, keys, and alternate passages.</li>");
        markup.AppendLine("<li><b>Prepare for Xanathar.</b> Enter the final lair with healing, ranged magic, and enough supplies for a prolonged fight.</li>");
        markup.AppendLine("</ul>");
    }

    private static void Row(StringBuilder markup, string label, string value) =>
        markup.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 960px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        .legend { display: flex; flex-wrap: wrap; gap: 0.75em 1.5em; padding-left: 1.25em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
