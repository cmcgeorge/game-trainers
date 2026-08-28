using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as one self-contained HTML page.
///
/// <para>Self-contained on purpose: the style is inline, the 55 plans are inline SVG, and there is
/// no script and nothing fetched, so the file can be moved, mailed or opened on a machine with no
/// network in ten years' time and still be the same document. <see cref="HtmlPage.IsSelfContained"/>
/// is what checks it, and the harness runs that check on real output.</para>
///
/// <para>The scaffold, the escaping and that rule come from <see cref="HtmlPage"/>; what is here is
/// the section structure, which is the half that belongs to Might &amp; Magic 1.</para>
/// </summary>
public static class HtmlCluebookWriter
{
    /// <summary>The document's title, which is also the file's.</summary>
    public const string Title = "Might & Magic Book One — cluebook";

    /// <summary>Renders the whole page.</summary>
    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>Might &amp; Magic Book One</h1>");
        s.AppendLine("<p class=\"lede\">The Secret of the Inner Sanctum — a cluebook, decoded from the " +
                     "game's own data.</p>");

        Contents(s, cluebook);
        Overview(s, cluebook);
        Notes(s, cluebook);
        Walkthrough(s, cluebook);
        Gazetteer(s, cluebook);
        Puzzles(s, cluebook);
        Party(s, cluebook);
        Spells(s, cluebook);
        Items(s, cluebook);
        Bestiary(s, cluebook);
        Provenance(s, cluebook);

        var page = new HtmlPage(Title).Style(Style);
        if (cluebook.Options.IncludePlans) page.Style(MazePlan.Style);
        return page.Append(s.ToString()).ToHtml();
    }

    // ---- sections -----------------------------------------------------------------------------

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        s.AppendLine("<li><a href=\"#notes\">Before you read this</a></li>");
        if (c.Options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">The solution, in order</a></li>");
        s.AppendLine("<li><a href=\"#gazetteer\">Gazetteer — all 55 places</a><ul>");
        foreach (var kind in PlaceBook.KindOrder)
            s.AppendLine($"<li><a href=\"#k-{(int)kind}\">{E(PlaceBook.KindName(kind))}</a></li>");
        s.AppendLine("</ul></li>");
        s.AppendLine("<li><a href=\"#puzzles\">The two ciphers</a></li>");
        if (c.Options.IncludeRules) s.AppendLine("<li><a href=\"#party\">The party, and how the game treats it</a></li>");
        if (c.Options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Spells</a></li>");
        if (c.Options.IncludeItems) s.AppendLine("<li><a href=\"#items\">Every item in the game</a></li>");
        if (c.Options.IncludeBestiary) s.AppendLine("<li><a href=\"#bestiary\">Bestiary</a></li>");
        s.AppendLine("<li><a href=\"#provenance\">Where all of this came from</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "The world", "VARN — a 5 × 4 grid of surface areas, five towns, six castles, and what is under them");
        Row(s, "Places", $"{c.Chapters.Count}, each a 16 × 16 grid of squares");
        Row(s, "Walls from", c.MazesAreExact ? $"{c.MazeSource} (exact)" : "the bundled layouts (a transcription)");
        Row(s, "Location text", c.HasEventText
            ? $"{c.MessageCount:N0} messages from {c.LocationsWithText} of {c.Chapters.Count} locations, " +
              $"read from {c.GameFolder}"
            : "not in this copy — see the notes below");
        Row(s, "Items", $"{ItemBook.Catalog.Count} in the game's own table");
        Row(s, "Monsters", $"{MonsterBook.Bestiary.Count}, in ten difficulty groups plus the aquatic and fixed ones");
        Row(s, "Spells", $"{Spellbook.Cleric.Count} Cleric and {Spellbook.Sorcerer.Count} Sorcerer, over seven levels");
        Row(s, "Classes", string.Join(", ", ClassBook.Classes.Select(x => x.Name)));
        s.AppendLine("</table>");

        if (!c.Options.IncludePlans) return;

        s.AppendLine("<h3>How to read a plan</h3>");
        s.AppendLine("<p>Every place is a 16 × 16 grid of squares, drawn with north at the top and " +
                     "column 0 at the west, which is how this project's maze atlas and the trainer's own " +
                     "Map (drawn) tab draw them. The numbers along the edges are the maze file's own x and " +
                     "y; <a href=\"#notes\">the notes below</a> say why the y a plan shows you may not be the " +
                     "y the game does.</p>");
        s.AppendLine("<ul class=\"legend\">");
        s.AppendLine("<li><span class=\"swatch sw-wall\"></span><b>Solid line</b> — a wall. You cannot pass.</li>");
        s.AppendLine("<li><span class=\"swatch sw-door\"></span><b>Gold dashes</b> — a door.</li>");
        s.AppendLine("<li><span class=\"swatch sw-special\"></span><b>Blue dots</b> — passable, and flagged by the " +
                     "game: a secret door, a one-way, or something that fires when you cross it.</li>");
        s.AppendLine("<li><span class=\"swatch sw-illusory\"></span><b>Faint dots</b> — <b>a wall you can walk " +
                     "straight through.</b> The game draws these from one plane and decides passability from " +
                     "another; where they disagree, you can walk through what you can see. Indoors that is a " +
                     "secret passage and every one is listed under its plan; outdoors it is terrain.</li>");
        s.AppendLine("<li><span class=\"swatch sw-mark\"></span><b>Green disc</b> — a numbered landmark, " +
                     "listed under the plan. There are few of them and the notes say why.</li>");
        s.AppendLine("<li><span class=\"swatch sw-oneway\"></span><b>Red dot</b> — the two squares either side " +
                     "disagree about this edge. It is drawn as the more solid of the two, so approach it from " +
                     "the other side before deciding it is shut.</li>");
        s.AppendLine("</ul>");
    }

    private static void Notes(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"notes\">Before you read this</h2>");
        s.AppendLine("<p>What this book knows, and how well:</p><ul class=\"notes\">");
        foreach (string note in c.Notes) s.AppendLine($"<li>{E(note)}</li>");
        s.AppendLine("</ul>");
    }

    private static void Walkthrough(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeWalkthrough) return;

        s.AppendLine("<h2 id=\"walkthrough\">The solution, in order</h2>");
        s.AppendLine("<p>Broadly ordered rather than strictly: Might &amp; Magic 1 lets you wander, and most of " +
                     "the middle of this list can be done in any order you can survive. Everything here is a " +
                     "spoiler.</p>");

        foreach (var section in Game.Walkthrough.Sections)
        {
            s.AppendLine($"<section class=\"entry\"><h3>{E(section.Title)}</h3><ol class=\"steps\">");
            foreach (string step in section.Steps) s.AppendLine($"<li>{E(step)}</li>");
            s.AppendLine("</ol></section>");
        }
    }

    private static void Gazetteer(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"gazetteer\">Gazetteer</h2>");
        s.AppendLine("<p>All 55 places the game holds, in the order its own data stores them: the towns, the " +
                     "surface, what is under it, the castles, the lairs, and the two places that are neither.</p>");

        foreach (var kind in PlaceBook.KindOrder)
        {
            var chapters = c.Of(kind).ToList();
            if (chapters.Count == 0) continue;

            s.AppendLine($"<h3 id=\"k-{(int)kind}\">{E(PlaceBook.KindName(kind))}</h3>");
            foreach (var chapter in chapters) Chapter(s, c, chapter);
        }
    }

    private static void Chapter(StringBuilder s, Cluebook c, LocationChapter chapter)
    {
        s.AppendLine($"<section class=\"place\"><h4 id=\"p-{A(chapter.RawName)}\">{E(chapter.Name)}</h4>");
        s.AppendLine($"<p class=\"id\">{E(chapter.RawName)} · map {chapter.Index} of {c.Chapters.Count} · " +
                     $"identification: {E(chapter.Confidence)} · {E(chapter.Stats.Summary)}</p>");

        if (chapter.Blurb.Length > 0) s.AppendLine($"<p>{E(chapter.Blurb)}</p>");

        if (c.Options.IncludePlans)
            s.AppendLine("<figure class=\"plan\">" +
                         MazePlan.RenderSvg(chapter.Maze, c.Options.PlanCellSize, includeStyle: false, chapter.Markers) +
                         "</figure>");

        Landmarks(s, chapter);
        SecretPassages(s, chapter);
        Messages(s, c, chapter);
        s.AppendLine("</section>");
    }

    /// <summary>The numbered marks under the plan, each with what is there and where it came from.</summary>
    private static void Landmarks(StringBuilder s, LocationChapter chapter)
    {
        if (chapter.Landmarks.Count == 0) return;

        s.AppendLine("<ol class=\"marks\">");
        foreach (var landmark in chapter.Landmarks)
        {
            var ways = chapter.WayInAt(landmark.X, landmark.Y);
            string wayIn = ways.Count == 0
                ? ""
                : $" <b>The wall on its {E(Join(ways))} side is not really there</b> — that is the way in.";

            s.AppendLine($"<li><b>{E(landmark.Name)}</b> <span class=\"at\">{E(landmark.Where)}</span> — " +
                         $"{E(landmark.Description)}{wayIn} <span class=\"quiet\">({E(landmark.Source)})</span></li>");
        }
        s.AppendLine("</ol>");
    }

    /// <summary>
    /// The walls that are not walls, as squares to walk out of.
    ///
    /// Listed rather than left to the eye: the faint dotted edge on the plan says one is there, and a
    /// coordinate says which square to stand on — and for a game that hides a wizard, a stronghold
    /// and half the Astral Plane behind drawn walls, that list is the single most useful thing a
    /// cluebook can compute.
    /// </summary>
    private static void SecretPassages(StringBuilder s, LocationChapter chapter)
    {
        if (chapter.SecretPassages.Count == 0) return;

        if (chapter.PassagesAreTerrain)
        {
            s.AppendLine($"<p class=\"secrets\">{chapter.SecretPassages.Count} of the walls drawn here can be " +
                         "walked through. Outdoors that is terrain rather than a secret — scrub, trees, the edge " +
                         "of a wood — so they are drawn on the plan but not listed.</p>");
            return;
        }

        var walks = chapter.SecretPassages.Select(p =>
            $"({p.X}, {p.Y}) {MazeMap.DirectionName(p.Dir)}");

        s.AppendLine($"<p class=\"secrets\"><b>Walls that are not there ({chapter.SecretPassages.Count}):</b> " +
                     E(string.Join(" · ", walks)) + ". Each is named by the square to stand on and the way to " +
                     "walk; do that and you go straight through.</p>");
    }

    private static void Messages(StringBuilder s, Cluebook c, LocationChapter chapter)
    {
        if (!c.Options.IncludeEventText) return;

        if (chapter.Messages.Count == 0)
        {
            if (c.HasEventText)
                s.AppendLine("<p class=\"quiet\">No overlay for this place was found in your installation, so " +
                             "what it says is not in this book.</p>");
            return;
        }

        s.AppendLine($"<p class=\"says\">What this place says — {chapter.Messages.Count} messages, in the order " +
                     $"{E(chapter.Overlay!.FileName)} stores them:</p>");
        s.AppendLine("<ul class=\"messages\">");
        foreach (var message in chapter.Messages)
        {
            s.Append("<li>");
            s.Append(string.Join("<br>", message.Lines.Select(E)));
            s.AppendLine("</li>");
        }
        s.AppendLine("</ul>");

        if (chapter.Overlay!.EventCount > 0)
            s.AppendLine($"<p class=\"quiet\">Its dispatcher knows {chapter.Overlay.EventCount} event squares. " +
                         "Which square is which is not decoded, so this book cannot tell you where to stand.</p>");
    }

    private static void Puzzles(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"puzzles\">The two ciphers</h2>");
        s.AppendLine("<p>The endgame is gated behind two collections. Nine <b>gold</b> messages, one per " +
                     "stronghold, are a scrambled riddle that reads in order 1–9. Six <b>silver</b> messages, one " +
                     "per castle, are a transposition whose ordering rule Castle Doom hands you " +
                     "(<code>INTERLEAVE 'FEDBAC'</code> and a nine-number sequence). Collecting them means nine " +
                     "dungeons and six castles; the table below is where each one lives.</p>");

        if (!c.HasEventText)
            s.AppendLine("<p class=\"quiet\">The fragments themselves are the game's own text, so they are here " +
                         "only when the cluebook has been pointed at your installation.</p>");

        Fragments(s, "Etched in gold — read 1 to 9", c.Gold);
        Fragments(s, "Etched in silver — re-ordered by Doom's rule", c.Silver);
    }

    private static void Fragments(StringBuilder s, string heading, IReadOnlyList<FoundFragment> fragments)
    {
        s.AppendLine($"<h3>{E(heading)}</h3>");
        s.AppendLine("<table class=\"grid\"><thead><tr><th>Fragment</th><th>Where</th><th>Text</th></tr></thead><tbody>");
        foreach (var found in fragments)
        {
            string where = found.Place is null
                ? found.Fragment.RawName
                : $"<a href=\"#p-{A(found.Fragment.RawName)}\">{E(found.Fragment.RawName)}</a>";

            string text = found.Message is null
                ? "<span class=\"quiet\">not read from your files</span>"
                : string.Join("<br>", found.Message.Lines.Select(E));

            s.AppendLine($"<tr><td>{E(found.Fragment.Label)}</td><td>{where}</td><td class=\"say\">{text}</td></tr>");
        }
        s.AppendLine("</tbody></table>");
    }

    private static void Party(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeRules) return;

        s.AppendLine("<h2 id=\"party\">The party, and how the game treats it</h2>");

        s.AppendLine("<h3>The six classes</h3>");
        s.AppendLine($"<p>A prime statistic has to be at least {ClassBook.MinPrimeValue} for a character to " +
                     "qualify for the class that wants it.</p>");
        s.AppendLine("<table class=\"grid\"><thead><tr><th>Class</th><th>Needs</th><th>HP/level</th>" +
                     "<th>Magic</th><th>What it is for</th></tr></thead><tbody>");
        foreach (var cls in ClassBook.Classes)
        {
            s.AppendLine($"<tr><td><b>{E(cls.Name)}</b></td><td>{E(cls.RequirementText)}</td>" +
                         $"<td class=\"n\">{E(cls.HitPointsPerLevel)}</td><td>{E(cls.SpellText)}</td>" +
                         $"<td>{E(cls.Description)}</td></tr>");
        }
        s.AppendLine("</tbody></table>");

        s.AppendLine("<h3>Hit points, and why Endurance decides them</h3>");
        s.AppendLine("<p>Every level rolls the class's die once and adds a bonus taken from Endurance, with a " +
                     "floor of one point. Over a whole game that bonus is worth more than any other attribute — " +
                     "and below Endurance 9 it is negative.</p>");
        s.AppendLine("<div class=\"twoup\">");
        s.AppendLine("<table class=\"grid\"><thead><tr><th>Class</th><th>Hit die</th></tr></thead><tbody>");
        foreach (var die in RulesBook.HitDice)
            s.AppendLine($"<tr><td>{E(die.ClassName)}</td><td class=\"n\">{E(die.DieText)}</td></tr>");
        s.AppendLine("</tbody></table>");

        s.AppendLine("<table class=\"grid\"><thead><tr><th>Endurance</th><th>HP per level</th></tr></thead><tbody>");
        foreach (var (min, bonus) in RulesBook.EnduranceBonuses)
            s.AppendLine($"<tr><td class=\"n\">{(min == 0 ? "under 5" : min + " and up")}</td>" +
                         $"<td class=\"n\">{bonus:+#;-#;0}</td></tr>");
        s.AppendLine("</tbody></table></div>");

        s.AppendLine("<h3>Levels and what they cost</h3>");
        s.AppendLine("<p>The manual's own approximation; the real curve is in the rules below.</p>");
        s.AppendLine("<table class=\"grid\"><thead><tr><th>Level</th><th>Experience for it</th>" +
                     "<th>Running total</th></tr></thead><tbody>");
        foreach (var step in ClassBook.ExperienceTable)
            s.AppendLine($"<tr><td class=\"n\">{step.Level}</td><td class=\"n\">{E(step.FromPreviousText)}</td>" +
                         $"<td class=\"n\">{E(step.CumulativeText)}</td></tr>");
        s.AppendLine("</tbody></table>");

        s.AppendLine("<h3>The rules under the numbers</h3>");
        s.AppendLine("<p>Recovered from the game's own code rather than the manual. Each says how firmly it is " +
                     "known and where it was read.</p><dl class=\"rules\">");
        foreach (var rule in RulesBook.Rules)
        {
            s.AppendLine($"<dt>{E(rule.Title)}</dt>");
            s.AppendLine($"<dd>{E(rule.Text)}<br><span class=\"quiet\">{E(rule.Confidence)} · " +
                         $"{E(rule.Source)}</span></dd>");
        }
        s.AppendLine("</dl>");
    }

    private static void Spells(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeSpells) return;

        s.AppendLine("<h2 id=\"spells\">Spells</h2>");
        s.AppendLine("<p>A spell is chosen in the game by its level and its number within that level, which is " +
                     "how they are listed here. Clerics and Paladins cast the first list; Sorcerers and Archers " +
                     "the second.</p>");

        SpellTable(s, "Cleric", Spellbook.Cleric);
        SpellTable(s, "Sorcerer", Spellbook.Sorcerer);
    }

    private static void SpellTable(StringBuilder s, string school, IReadOnlyList<Spell> spells)
    {
        s.AppendLine($"<h3>{E(school)}</h3>");
        s.AppendLine("<table class=\"grid\"><thead><tr><th>Cast</th><th>Name</th><th>Cost</th>" +
                     "<th>What it does</th></tr></thead><tbody>");
        foreach (var spell in spells)
        {
            s.AppendLine($"<tr><td class=\"n\">{spell.Level} · {spell.Number}</td><td><b>{E(spell.Name)}</b></td>" +
                         $"<td class=\"n\">{E(spell.CostText)}</td><td>{E(spell.Description)}</td></tr>");
        }
        s.AppendLine("</tbody></table>");
    }

    private static void Items(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeItems) return;

        s.AppendLine("<h2 id=\"items\">Every item in the game</h2>");
        s.AppendLine($"<p>All {ItemBook.Catalog.Count} entries of the game's own item table, in id order — which " +
                     "is also category order, so the weapons come first and the quest items last. The id is the " +
                     "byte the game stores in an inventory slot.</p>");

        foreach (var group in ItemBook.Catalog.GroupBy(i => i.Category))
        {
            s.AppendLine($"<h3>{E(group.Key)}</h3>");
            s.AppendLine("<table class=\"grid\"><thead><tr><th>Id</th><th>Name</th><th>Cost</th><th>Stats</th>" +
                         "<th>Charges</th><th>Effect, and who can use it</th></tr></thead><tbody>");
            foreach (var item in group)
            {
                s.AppendLine($"<tr><td class=\"n\">{item.Id}</td><td>{E(item.Name)}</td>" +
                             $"<td class=\"n\">{E(item.CostText)}</td>" +
                             $"<td class=\"n\">{E(item.StatText.Length > 0 ? item.StatText : "—")}</td>" +
                             $"<td class=\"n\">{(item.Charges > 0 ? item.Charges.ToString(CultureInfo.InvariantCulture) : "—")}</td>" +
                             $"<td>{E(ItemEffectBook.Describe(item.Id))}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
        }
    }

    private static void Bestiary(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeBestiary) return;

        s.AppendLine("<h2 id=\"bestiary\">Bestiary</h2>");
        s.AppendLine($"<p>All {MonsterBook.Bestiary.Count} monsters, in the order the game's table holds them: " +
                     "ten groups of sixteen that random encounters draw from by how dangerous the ground is, " +
                     "then the aquatic ones, then the fixed encounters. Hit points are the stored base — the game " +
                     "adds a little when it makes a group. Experience is split between the survivors.</p>");

        foreach (var group in MonsterBook.Bestiary.GroupBy(m => m.Group))
        {
            s.AppendLine($"<h3>{E(group.Key)}</h3>");
            s.AppendLine("<table class=\"grid\"><thead><tr><th>Id</th><th>Name</th><th>HP</th><th>AC</th>" +
                         "<th>Damage</th><th>Attacks</th><th>Speed</th><th>Up to</th><th>XP</th></tr></thead><tbody>");
            foreach (var m in group)
            {
                s.AppendLine($"<tr><td class=\"n\">{m.Id}</td><td>{E(m.Name)}</td><td class=\"n\">{m.HpBase}+</td>" +
                             $"<td class=\"n\">{m.ArmorClass}</td><td class=\"n\">{m.Damage}</td>" +
                             $"<td class=\"n\">{m.Attacks}</td><td class=\"n\">{m.Speed}</td>" +
                             $"<td class=\"n\">{m.MaxCount}</td><td class=\"n\">{m.Experience:N0}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
        }
    }

    private static void Provenance(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"provenance\">Where all of this came from</h2>");
        s.AppendLine("<ul class=\"notes\">");
        s.AppendLine("<li>The walls, doors and secret passages: the game's <code>Mazedata.dta</code>, two " +
                     "co-registered 16 × 16 planes per place — one saying what is drawn, the other what you may " +
                     "walk through.</li>");
        s.AppendLine("<li>What each place says: the 55 <code>.ovr</code> overlays, each the compiled event " +
                     "handlers for one location with its text embedded. Read from your own installation, never " +
                     "shipped.</li>");
        s.AppendLine("<li>Items, monsters and the class tables: extracted from <code>MM.EXE</code>.</li>");
        s.AppendLine("<li>The levelling, combat and dice rules: disassembled from <code>MM.EXE</code>'s own " +
                     "routines.</li>");
        s.AppendLine("<li>The walkthrough and the item effects: community guides, cross-checked.</li>");
        s.AppendLine("</ul>");

        if (c.Problems.Count == 0) return;

        s.AppendLine("<h3>Files that could not be read</h3><ul class=\"notes\">");
        foreach (string problem in c.Problems) s.AppendLine($"<li>{E(problem)}</li>");
        s.AppendLine("</ul>");
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>"west", "west and north", "west, north and east".</summary>
    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => "",
        1 => parts[0],
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
    };

    private static void Row(StringBuilder s, string name, string value) =>
        s.AppendLine($"<tr><th>{E(name)}</th><td>{E(value)}</td></tr>");

    /// <summary>Escapes text going between tags.</summary>
    private static string E(string value) => HtmlPage.Escape(value);

    /// <summary>
    /// Escapes a value going into a double-quoted attribute.
    ///
    /// A separate helper because the only strings that reach an attribute here are location names
    /// out of the game's own table: they are arbitrary bytes from somebody else's files, and a quote
    /// in one would close the attribute and turn the rest of it into markup.
    /// </summary>
    private static string A(string value) => HtmlPage.EscapeAttribute(value);

    private const string Style = """
        :root{color-scheme:light}
        body{margin:0 auto;max-width:62rem;padding:2rem 1.25rem 6rem;
             font:16px/1.6 'Iowan Old Style','Palatino Linotype',Georgia,serif;color:#2b2519;background:#fdfaf3}
        h1{font-size:2.1rem;margin:0 0 .25rem;letter-spacing:-.01em}
        h2{margin:3rem 0 .75rem;padding-bottom:.3rem;border-bottom:2px solid #d8cbaa;font-size:1.5rem}
        h3{margin:2rem 0 .4rem;font-size:1.15rem}
        h4{margin:1.6rem 0 .2rem;font-size:1.05rem}
        code{font:.85em/1.4 'Cascadia Mono',Consolas,monospace;background:#f0e9d8;padding:.1em .35em;border-radius:3px}
        .lede{color:#6f6449;margin:0 0 1.5rem}
        .id{color:#8a7d5e;font:.8rem/1.4 'Cascadia Mono',Consolas,monospace;margin:.1rem 0 .6rem}
        .quiet{color:#8a7d5e;font-size:.9rem}
        nav.toc{background:#f4eddc;border:1px solid #ddd0b0;border-radius:6px;padding:.75rem 1.25rem 1rem}
        nav.toc h2{margin:.25rem 0 .5rem;border:0;font-size:1.05rem}
        nav.toc a{color:#5d4b22}
        nav.toc ul{margin:.2rem 0}
        table.facts{border-collapse:collapse;margin:.5rem 0 1.5rem}
        table.facts th{text-align:left;padding:.2rem 1.5rem .2rem 0;font-weight:600;color:#6f6449;vertical-align:top}
        table.grid{border-collapse:collapse;width:100%;font-size:.9rem;margin:.5rem 0 1.5rem}
        table.grid th,table.grid td{border-bottom:1px solid #e4dac0;padding:.3rem .5rem;text-align:left;vertical-align:top}
        table.grid thead th{background:#f4eddc;border-bottom:2px solid #d8cbaa;position:sticky;top:0}
        table.grid td.n{text-align:right;white-space:nowrap}
        section.entry,section.place{margin:0 0 1.5rem}
        section.place{border-top:1px solid #ece2ca;padding-top:.5rem}
        ul.notes li{margin:.4rem 0}
        ol.steps li{margin:.25rem 0}
        ul.legend{list-style:none;padding:0;margin:.5rem 0 1.5rem}
        ul.legend li{margin:.3rem 0}
        .swatch{display:inline-block;width:2rem;height:0;border-top:3px solid #3a3222;margin-right:.6rem;
                vertical-align:middle}
        .sw-door{border-top-style:dashed;border-top-color:#b5892b}
        .sw-special{border-top-style:dotted;border-top-color:#3f7a8c}
        .sw-illusory{border-top-style:dotted;border-top-color:#b0a488;border-top-width:2px}
        .sw-oneway{border-top:0;height:.5rem;width:.5rem;border-radius:50%;background:#a3432b;margin:0 1.35rem 0 .6rem}
        .sw-mark{border-top:0;height:.9rem;width:.9rem;border-radius:50%;background:#2f6b4f;margin:0 1.15rem 0 .6rem}
        ol.marks{margin:.2rem 0 .6rem;padding-left:1.4rem;font-size:.92rem}
        ol.marks li{margin:.25rem 0}
        ol.marks .at{font:.85em 'Cascadia Mono',Consolas,monospace;color:#6f6449}
        p.secrets{font-size:.88rem;color:#4a4231;background:#f7f1e1;border-left:3px solid #b0a488;
                  padding:.4rem .7rem;margin:.4rem 0 .8rem}
        figure.plan{margin:.6rem 0 1rem;padding:.4rem;background:#fff;border:1px solid #ddd0b0;border-radius:6px;
                    max-width:32rem}
        .says{margin:.8rem 0 .2rem;font-weight:600}
        ul.messages{margin:.2rem 0 .8rem;padding-left:1.1rem}
        ul.messages li{margin:.5rem 0;font:.92rem/1.45 'Cascadia Mono',Consolas,monospace;color:#3f3524}
        td.say{font:.85rem/1.4 'Cascadia Mono',Consolas,monospace}
        dl.rules dt{font-weight:600;margin-top:.9rem}
        dl.rules dd{margin:.15rem 0 0 1.25rem}
        .twoup{display:flex;gap:2rem;flex-wrap:wrap}
        .twoup table.grid{width:auto;min-width:14rem;flex:1 1 14rem}
        @media print{nav.toc{break-after:page}section.place{break-inside:avoid}}
        """;
}
