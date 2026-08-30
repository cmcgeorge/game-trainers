using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as one self-contained HTML page with inline SVG maps,
/// spell tables, and a walkthrough. Uses <see cref="HtmlPage"/> for the scaffold and
/// <see cref="SvgCanvas"/> for the dungeon plans.
/// </summary>
public static class HtmlCluebookWriter
{
    public const string Title = "Wizardry: Proving Grounds of the Mad Overlord — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>Wizardry: Proving Grounds of the Mad Overlord</h1>");
        s.AppendLine("<p class=\"lede\">A cluebook for the 1981 Sir-Tech classic, with all ten dungeon levels, every spell, and a complete walkthrough.</p>");

        Contents(s, cluebook);
        Overview(s);
        Castle(s);
        if (cluebook.Options.IncludeMaps) DungeonMaps(s, cluebook);
        if (cluebook.Options.IncludeSpells) Spells(s);
        if (cluebook.Options.IncludeClasses) Classes(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        var page = new HtmlPage(Title).Style(Style);
        return page.Append(s.ToString()).ToHtml();
    }

    // ---- table of contents ---------------------------------------------------

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        s.AppendLine("<li><a href=\"#castle\">The Castle</a></li>");
        if (c.Options.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Dungeon maps — all ten levels</a></li>");
        if (c.Options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Spells</a></li>");
        if (c.Options.IncludeClasses) s.AppendLine("<li><a href=\"#classes\">Races, classes &amp; alignments</a></li>");
        if (c.Options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (c.Options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    // ---- overview ------------------------------------------------------------

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", GameFacts.GameTitle);
        Row(s, "Year", GameFacts.GameYear);
        Row(s, "Developer", GameFacts.GameDeveloper);
        Row(s, "Authors", GameFacts.GameAuthors);
        Row(s, "Dungeon", $"{GameFacts.DungeonLevels} levels, each {GameFacts.MazeSize}×{GameFacts.MazeSize} cells");
        Row(s, "Party size", $"Up to {GameFacts.MaxPartySize} characters");
        Row(s, "Spells", $"{SpellBook.Spells.Count} ({SpellBook.MageSpells.Count} mage, {SpellBook.PriestSpells.Count} priest)");
        Row(s, "Races", string.Join(", ", CharacterFormat.RaceNames));
        Row(s, "Classes", string.Join(", ", CharacterFormat.ClassNames));
        Row(s, "Alignments", string.Join(", ", CharacterFormat.AlignmentNames));
        s.AppendLine("</table>");

        s.AppendLine("<h3>The story</h3>");
        s.AppendLine("<p>The wizard <b>Trebor</b> once ruled the land from his castle. The evil archmage " +
                     "<b>Werdna</b> stole the <b>Amulet</b> from Trebor and fled into the ten-level maze he " +
                     "carved beneath the castle. Trebor cannot follow — the maze is full of monsters and " +
                     "traps — so he offers gold and glory to any adventurer brave enough to descend, defeat " +
                     "Werdna, and bring back the Amulet.</p>");
        s.AppendLine("<p>Your party starts at the <b>Edge of Town</b>, the staging area outside the dungeon " +
                     "entrance. You create characters, buy equipment, and then enter the maze to fight, " +
                     "explore, and eventually confront Werdna on level 10.</p>");
    }

    // ---- castle --------------------------------------------------------------

    private static void Castle(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"castle\">The Castle</h2>");
        s.AppendLine("<p>The Castle is the safe hub above the dungeon. Every service your party needs " +
                     "is here, accessed through single-key commands:</p>");
        s.AppendLine("<table class=\"ref\">");
        s.AppendLine("<tr><th>Key</th><th>Location</th><th>Function</th></tr>");
        s.AppendLine("<tr><td>(C)haracter</td><td>—</td><td>Create a new character</td></tr>");
        s.AppendLine("<tr><td>(G)ilgamesh's</td><td>Tavern</td><td>Add or remove party members</td></tr>");
        s.AppendLine("<tr><td>(B)oltac's</td><td>Trading Post</td><td>Buy, sell, identify, uncurse items</td></tr>");
        s.AppendLine("<tr><td>(T)emple</td><td>Temple of Cant</td><td>Heal, cure, resurrect characters</td></tr>");
        s.AppendLine("<tr><td>(R)eview</td><td>Review Board</td><td>Check status, level up characters</td></tr>");
        s.AppendLine("<tr><td>(E)dge</td><td>Edge of Town</td><td>Enter or exit the dungeon</td></tr>");
        s.AppendLine("<tr><td>(I)nn</td><td>Inn</td><td>Rest to restore HP and spell charges</td></tr>");
        s.AppendLine("</table>");
    }

    // ---- dungeon maps --------------------------------------------------------

    private static void DungeonMaps(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"maps\">Dungeon maps — all ten levels</h2>");
        s.AppendLine("<p>Each level is a 20×20 grid. North is at the top, west at the left. " +
                     "Row 0 is the north edge, column 0 the west edge.</p>");
        s.AppendLine("<ul class=\"legend\">");
        s.AppendLine("<li><span class=\"swatch sw-wall\"></span><b>Wall</b> — impassable</li>");
        s.AppendLine("<li><span class=\"swatch sw-floor\"></span><b>Floor</b> — walkable</li>");
        s.AppendLine("<li><span class=\"swatch sw-stairs\"></span><b>U/D</b> — stairs up / down</li>");
        s.AppendLine("<li><span class=\"swatch sw-elev\"></span><b>E</b> — elevator (level 3)</li>");
        s.AppendLine("<li><span class=\"swatch sw-item\"></span><b>B</b> — Blue Ribbon (level 4)</li>");
        s.AppendLine("<li><span class=\"swatch sw-amulet\"></span><b>A</b> — The Amulet (level 10)</li>");
        s.AppendLine("<li><span class=\"swatch sw-start\"></span><b>@</b> — Party start (level 1)</li>");
        s.AppendLine("</ul>");

        s.AppendLine("<h3>Stair and elevator connections</h3>");
        s.AppendLine("<pre class=\"connections\">");
        s.AppendLine("Level 1:  @ (10,1)            D (3,17)");
        s.AppendLine("Level 2:  U (3,17)            D (16,3)");
        s.AppendLine("Level 3:  U (16,3)            D (3,16)            E (10,10)");
        s.AppendLine("Level 4:  U (3,16)            D (16,16)           B (10,3)");
        s.AppendLine("Level 5:  U (16,16)           D (3,3)");
        s.AppendLine("Level 6:  U (3,3)             D (16,3)");
        s.AppendLine("Level 7:  U (16,3)            D (3,16)");
        s.AppendLine("Level 8:  U (3,16)            D (16,3)");
        s.AppendLine("Level 9:  U (16,3)            D (10,17)");
        s.AppendLine("Level 10: U (10,17)                               A (10,10)");
        s.AppendLine("</pre>");

        foreach (var level in c.Levels)
        {
            s.AppendLine($"<h3 id=\"lvl-{level.Index}\">Level {level.Index + 1}: {E(level.Name)}</h3>");
            s.AppendLine($"<p>{E(level.Description)}</p>");
            s.AppendLine(DungeonSvg(level, c.Options.MapCellSize));

            if (level.Pois.Count > 0)
            {
                s.AppendLine("<table class=\"ref\">");
                s.AppendLine("<tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
                foreach (var poi in level.Pois)
                    s.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
                s.AppendLine("</table>");
            }
        }
    }

    private static string DungeonSvg(DungeonLevel level, int cell)
    {
        int w = level.Width, h = level.Height;
        int pad = 20;
        int totalW = pad * 2 + cell * w;
        int totalH = pad * 2 + cell * h;

        var svg = SvgCanvas.Responsive(totalW, totalH, "Dungeon level map");

        svg.Rect(0, 0, totalW, totalH, ("fill", "#14151A"));

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool isWall = level.Grid[x, y] == CellKind.Wall;
                string fill = isWall ? "#3A3D4A" : "#1E1F26";
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell, ("fill", fill));
            }

        foreach (var poi in level.Pois)
        {
            var (fill, label) = PoiColor(poi.Name);
            int cx = pad + poi.X * cell;
            int cy = pad + poi.Y * cell;
            svg.Rect(cx, cy, cell, cell, ("fill", fill));
            if (label.Length > 0)
                svg.Text(cx + cell / 2.0, cy + cell * 0.7, label,
                    ("text-anchor", "middle"), ("font-family", "monospace"),
                    ("font-size", cell * 0.55), ("fill", "#14151A"));
        }

        return svg.ToSvg();
    }

    private static (string fill, string label) PoiColor(string name) => name switch
    {
        "Party Start" => ("#B070E0", "@"),
        "Stairs Up" => ("#6FC276", "U"),
        "Stairs Down" => ("#6FC276", "D"),
        "Elevator" => ("#799BD7", "E"),
        "Blue Ribbon" => ("#C89B3C", "B"),
        "The Amulet" => ("#E0B040", "A"),
        _ => ("#E0E2E8", ""),
    };

    // ---- spells --------------------------------------------------------------

    private static void Spells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"spells\">Spells</h2>");
        s.AppendLine("<p>Wizardry 1 has 50 spells: 21 mage spells and 29 priest spells, each " +
                     "divided across seven levels. Spell charges per level are tracked separately " +
                     "for mage and priest spells.</p>");

        SpellTable(s, "Mage Spells", SpellBook.MageSpells);
        SpellTable(s, "Priest Spells", SpellBook.PriestSpells);
    }

    private static void SpellTable(StringBuilder s, string title, IReadOnlyList<SpellBook.SpellInfo> spells)
    {
        s.AppendLine($"<h3>{E(title)}</h3>");
        s.AppendLine("<table class=\"ref\">");
        s.AppendLine("<tr><th>#</th><th>Name</th><th>Lvl</th><th>Effect</th></tr>");
        foreach (var sp in spells)
            s.AppendLine($"<tr><td>{sp.Index}</td><td>{E(sp.Name)}</td><td>{sp.Level}</td><td>{E(sp.Effect)}</td></tr>");
        s.AppendLine("</table>");
    }

    // ---- classes -------------------------------------------------------------

    private static void Classes(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"classes\">Races, classes &amp; alignments</h2>");

        s.AppendLine("<h3>Races</h3>");
        s.AppendLine("<table class=\"ref\">");
        s.AppendLine("<tr><th>Race</th><th>Notes</th></tr>");
        s.AppendLine("<tr><td>Human</td><td>No stat bonuses or penalties. Can be any class.</td></tr>");
        s.AppendLine("<tr><td>Elf</td><td>High INT and AGI; lower VIT. Good mages and samurai.</td></tr>");
        s.AppendLine("<tr><td>Dwarf</td><td>High STR and VIT; low INT and AGI. Good fighters.</td></tr>");
        s.AppendLine("<tr><td>Gnome</td><td>Balanced; good priests.</td></tr>");
        s.AppendLine("<tr><td>Hobbit</td><td>High LUK and AGI; low STR. Good thieves.</td></tr>");
        s.AppendLine("</table>");

        s.AppendLine("<h3>Classes</h3>");
        s.AppendLine("<table class=\"ref\">");
        s.AppendLine("<tr><th>Class</th><th>Key attributes</th><th>Notes</th></tr>");
        s.AppendLine("<tr><td>Fighter</td><td>STR</td><td>Melee combatant. High HP gain.</td></tr>");
        s.AppendLine("<tr><td>Mage</td><td>INT</td><td>Offensive spellcaster. Low HP, high damage.</td></tr>");
        s.AppendLine("<tr><td>Priest</td><td>PIE</td><td>Healing and support spells. Can wear some armor.</td></tr>");
        s.AppendLine("<tr><td>Thief</td><td>AGI, LUK</td><td>Disarm traps, open chests, hide. Low combat.</td></tr>");
        s.AppendLine("<tr><td>Bishop</td><td>INT, PIE</td><td>Casts both mage and priest spells. Slower progression.</td></tr>");
        s.AppendLine("<tr><td>Samurai</td><td>STR, INT</td><td>Fighter who can cast mage spells at higher levels.</td></tr>");
        s.AppendLine("<tr><td>Lord</td><td>STR, PIE</td><td>Fighter who can cast priest spells at higher levels.</td></tr>");
        s.AppendLine("<tr><td>Ninja</td><td>All high</td><td>Elite class. Critical hits, AC bonus, can use any weapon.</td></tr>");
        s.AppendLine("</table>");

        s.AppendLine("<h3>Alignments</h3>");
        s.AppendLine("<table class=\"ref\">");
        s.AppendLine("<tr><th>Alignment</th><th>Notes</th></tr>");
        s.AppendLine("<tr><td>Good</td><td>Can party with Good and Neutral. Some classes require Good.</td></tr>");
        s.AppendLine("<tr><td>Neutral</td><td>Can party with any alignment.</td></tr>");
        s.AppendLine("<tr><td>Evil</td><td>Can party with Evil and Neutral. Some classes require Evil.</td></tr>");
        s.AppendLine("</table>");
    }

    // ---- walkthrough ---------------------------------------------------------

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2>");
        s.AppendLine("<ol>");
        s.AppendLine("<li><b>Create your party.</b> A balanced party of six is recommended: two fighters " +
                     "(a Samurai and a Lord if stats allow), one Mage, one Priest, one Thief, and one " +
                     "Bishop. Make sure at least one character has high LUCK for disarming traps.</li>");
        s.AppendLine("<li><b>Buy equipment.</b> At Boltac's Trading Post, equip every character with " +
                     "the best weapons and armor you can afford. Don't forget the Thief needs a dagger " +
                     "and leather armor.</li>");
        s.AppendLine("<li><b>Grind on Level 1.</b> Enter the dungeon and fight weak monsters on level 1 " +
                     "until your characters reach level 2-3. Use the Inn to rest and restore HP/spell charges.</li>");
        s.AppendLine("<li><b>Descend to Level 2.</b> Take the stairs down at (3, 17). Fight tougher " +
                     "monsters for better experience and gold.</li>");
        s.AppendLine("<li><b>Reach the Elevator on Level 3.</b> The stairs down from Level 2 are at " +
                     "(16, 3). The elevator at (10, 10) lets you return to any previously visited level.</li>");
        s.AppendLine("<li><b>Get the Blue Ribbon on Level 4.</b> Descend from Level 3 at (3, 16). " +
                     "The Blue Ribbon at (10, 3) is required to access the deeper levels. Make sure " +
                     "your party is level 5-6 before attempting this.</li>");
        s.AppendLine("<li><b>Push to Level 6.</b> Continue descending through levels 5 and 6. Watch " +
                     "for traps on Level 6 — pits, teleporters, and darkness zones. Keep your Priest's " +
                     "Lomilwa (greater light) active.</li>");
        s.AppendLine("<li><b>Break through to Level 8.</b> Levels 7 and 8 have powerful undead and " +
                     "demons. Use Zilwan (destroy undead) and Mahalito/Madalto for area damage. " +
                     "Your party should be level 8-10.</li>");
        s.AppendLine("<li><b>Reach Level 10.</b> Descend through Level 9 to the bottom. The stairs " +
                     "down from Level 9 are at (10, 17). Your party should be level 12+ with the best " +
                     "equipment available.</li>");
        s.AppendLine("<li><b>Defeat Werdna.</b> Werdna is at (10, 10) on Level 10. He is a powerful " +
                     "spellcaster — use Masopic/Bamatu for AC buffs, save spell charges for the fight, " +
                     "and have your Priest ready with Madi (full heal) and Di (resurrect). " +
                     "Tiltowait (mage level 7) is the strongest attack spell.</li>");
        s.AppendLine("<li><b>Claim the Amulet.</b> After defeating Werdna, pick up the Amulet. " +
                     "Carry it back to the surface to win the game.</li>");
        s.AppendLine("</ol>");
    }

    // ---- strategy ------------------------------------------------------------

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2>");
        s.AppendLine("<ul>");
        s.AppendLine("<li><b>Always map.</b> Wizardry has no auto-map. Draw each level on graph paper " +
                     "as you explore, or use the Maps tab in this trainer.</li>");
        s.AppendLine("<li><b>Save often.</b> The game has no save points inside the dungeon. Return to " +
                     "the Castle regularly to save at the Inn.</li>");
        s.AppendLine("<li><b>Use Dumapic.</b> The mage spell Dumapic shows your exact coordinates and " +
                     "depth — essential for mapping.</li>");
        s.AppendLine("<li><b>Manage spell charges.</b> Spell charges are per level, not per spell. " +
                     "Rest at the Inn to restore all charges.</li>");
        s.AppendLine("<li><b>Disarm traps.</b> Always have a Thief (or character with high LUCK) " +
                     "inspect and disarm chests. Failed disarms can destroy the party.</li>");
        s.AppendLine("<li><b>Watch your age.</b> Resting at the Inn ages characters. High age reduces " +
                     "stats. Use the Temple to restore youth if needed.</li>");
        s.AppendLine("<li><b>Class changes.</b> Characters can change class at the Review Board, " +
                     "keeping their spells but resetting to level 1. This is how you build powerful " +
                     "multi-class characters.</li>");
        s.AppendLine("<li><b>The elevator is your friend.</b> Once you reach Level 3, the elevator " +
                     "lets you return to any previously visited level instantly. Use it to retreat, " +
                     "restock, and re-enter at the right depth.</li>");
        s.AppendLine("<li><b>Level 10 is a one-way trip.</b> The only way out of Level 10 is the " +
                     "stairs up at (10, 17). Make sure you're prepared before descending.</li>");
        s.AppendLine("<li><b>Lost characters.</b> If a character is lost (Status: Lost), use the " +
                     "Priest spell Kandi to find them, then Di or Kadorto to resurrect. Lost characters " +
                     "lose all their equipment.</li>");
        s.AppendLine("</ul>");
    }

    // ---- helpers -------------------------------------------------------------

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 900px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        .toc ul { padding-left: 1.5em; list-style: circle; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        table.facts th { width: 160px; white-space: nowrap; }
        .legend { list-style: none; padding: 0; }
        .legend li { display: inline-block; margin: 0 1em 0.5em 0; }
        .swatch { display: inline-block; width: 14px; height: 14px; border: 1px solid #444; vertical-align: middle; margin-right: 4px; }
        .sw-wall { background: #3A3D4A; } .sw-floor { background: #1E1F26; } .sw-stairs { background: #6FC276; }
        .sw-elev { background: #799BD7; } .sw-item { background: #C89B3C; } .sw-amulet { background: #E0B040; }
        .sw-start { background: #B070E0; }
        .connections { background: #1E1F26; color: #E0E2E8; padding: 1em; border-radius: 4px; font-family: monospace; overflow-x: auto; }
        pre { white-space: pre-wrap; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
