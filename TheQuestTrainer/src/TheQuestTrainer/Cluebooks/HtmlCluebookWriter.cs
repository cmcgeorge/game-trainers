using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using TheQuestTrainer.Adventures;

namespace TheQuestTrainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as one self-contained HTML page.
///
/// Self-contained on purpose: the style is inline, the world plan is inline SVG, and there is no
/// script and nothing fetched, so the file can be moved, mailed or opened offline and still be the
/// same document. Same reason the FRUA cluebook next door writes one file.
///
/// The scaffold, the escaping and that self-contained rule come from <see cref="HtmlPage"/>; what is
/// here is the section structure, which is where the document says what it is about and is therefore
/// the part that belongs to The Quest.
/// </summary>
public static class HtmlCluebookWriter
{
    /// <summary>Renders the whole page.</summary>
    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var a = cluebook.Adventure;
        var s = new StringBuilder();

        s.AppendLine($"<h1>{E(a.Name)}</h1>");
        s.AppendLine($"<p class=\"lede\">A cluebook for <b>The Quest</b>, decompiled from " +
                     $"<code>{E(a.SourcePath)}</code>.</p>");

        Contents(s, cluebook);
        Overview(s, cluebook);
        Notes(s, cluebook);
        Walkthrough(s, cluebook);
        Gazetteer(s, cluebook);
        People(s, cluebook);
        Things(s, cluebook);
        Reference(s, cluebook);

        return new HtmlPage($"{a.Name} — cluebook").Style(Style).Append(s.ToString()).ToHtml();
    }

    // ---- sections -----------------------------------------------------------------------------

    private static void Contents(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The adventure at a glance</a></li>");
        s.AppendLine("<li><a href=\"#notes\">Before you read this</a></li>");
        s.AppendLine("<li><a href=\"#quests\">The quests</a></li>");
        s.AppendLine("<li><a href=\"#gazetteer\">Gazetteer</a></li>");
        if (c.Options.IncludeConversations) s.AppendLine("<li><a href=\"#people\">People, and what they say</a></li>");
        if (c.Options.IncludeItems) s.AppendLine("<li><a href=\"#things\">Things</a></li>");
        if (c.Options.IncludeReference) s.AppendLine("<li><a href=\"#reference\">Bestiary, magic and rules</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s, Cluebook c)
    {
        var a = c.Adventure;
        s.AppendLine("<h2 id=\"overview\">The adventure at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "World", a.Name);
        Row(s, "Resource pack", a.Pack);
        Row(s, "Database", a.Database);
        Row(s, "Outdoor grid", $"{a.GridWidth} × {a.GridHeight} cells of {Game.MapLayout.GridMapTiles} tiles");
        Row(s, "Maps", $"{a.Maps.Count} — {a.OutdoorMaps.Count()} outdoor cells, {a.Interiors.Count()} interiors");
        Row(s, "Quests", a.Quests.Count.ToString(CultureInfo.CurrentCulture));
        Row(s, "People", $"{a.People.Count}, of whom {c.Speakers.Count} have something to say");
        Row(s, "Conversation topics", c.TopicCount.ToString(CultureInfo.CurrentCulture));
        Row(s, "Map objects", a.MapObjects.Count.ToString(CultureInfo.CurrentCulture));
        Row(s, "Item types", a.Items.Count.ToString(CultureInfo.CurrentCulture));
        Row(s, "Spells", a.Spells.Count.ToString(CultureInfo.CurrentCulture));
        Row(s, "Creatures", $"{a.Monsters.Count} monster types, {a.NpcTypes.Count} person types");
        Row(s, "Format version", a.FormatVersion.ToString(CultureInfo.CurrentCulture));
        s.AppendLine("</table>");

        if (!c.Options.IncludeMap) return;
        string plan = WorldPlan.Render(c);
        if (plan.Length == 0) return;

        s.AppendLine("<h3>The world</h3>");
        s.AppendLine("<p>North is up. Each square is one outdoor map, 21 tiles across; the numbers " +
                     "are the cell a map id spells out, so cell 8, 4 is <code>" +
                     E(c.Adventure.GridPrefix) + "0804</code>. Blue squares are cells the world has " +
                     "no map for.</p>");
        s.AppendLine($"<figure class=\"plan\">{plan}</figure>");
    }

    private static void Notes(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"notes\">Before you read this</h2><ul class=\"notes\">");
        foreach (string note in c.Notes) s.AppendLine($"<li>{E(note)}</li>");
        s.AppendLine("</ul>");
    }

    private static void Walkthrough(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"quests\">The quests</h2>");
        if (c.Quests.Count == 0)
        {
            s.AppendLine("<p>This adventure has no quest log.</p>");
            return;
        }

        s.AppendLine("<p>Each entry is the quest as the game's own log states it, followed by " +
                     "everyone who talks about it. That list is exact — it comes from the ids the " +
                     "conversations name, not from searching the prose.</p>");

        foreach (var quest in c.Quests)
        {
            s.AppendLine($"<section class=\"entry\"><h3 id=\"q-{A(quest.Id)}\">{E(quest.Name)}</h3>");
            s.AppendLine($"<p class=\"id\">{E(quest.Id)}</p>");
            if (quest.Description.Length > 0) s.AppendLine($"<p>{E(quest.Description)}</p>");
            Mentions(s, quest);
            s.AppendLine("</section>");
        }
    }

    private static void Gazetteer(StringBuilder s, Cluebook c)
    {
        s.AppendLine("<h2 id=\"gazetteer\">Gazetteer</h2>");
        s.AppendLine("<p>Outdoor cells first, row by row from the north, then the interiors.</p>");

        foreach (var chapter in c.Chapters)
        {
            var m = chapter.Map;
            s.AppendLine($"<section class=\"entry\"><h3 id=\"m-{A(m.Id)}\">{E(m.Name.Length > 0 ? m.Name : m.Id)}</h3>");
            s.AppendLine("<p class=\"id\">" + E(m.Id) + " · " +
                         (m.IsOutdoorCell ? $"cell {m.CellLabel}, world tiles {m.OriginX}–{m.OriginX + m.Tiles - 1} east, {m.OriginY}–{m.OriginY + m.Tiles - 1} south"
                                          : "interior") +
                         $" · {m.SizeLabel}" + (m.Notes.Length > 0 ? " · " + E(m.Notes) : "") + "</p>");

            if (chapter.People.Count > 0)
            {
                s.AppendLine("<p><b>People here:</b> " +
                    string.Join(", ", chapter.People.Select(p =>
                        $"<a href=\"#p-{A(p.Id)}\">{E(p.Name.Length > 0 ? p.Name : p.Id)}</a>")) + "</p>");
            }

            if (chapter.Objects.Count > 0)
            {
                s.AppendLine("<p><b>What stands here:</b></p><ul class=\"objects\">");
                foreach (var o in chapter.Objects)
                {
                    s.Append($"<li><code>{E(o.Id)}</code>");
                    if (o.Text.Count > 0)
                        s.Append(" — " + string.Join(" ", o.Text.Where(Readable).Select(t => $"<q>{E(t)}</q>")));
                    s.AppendLine("</li>");
                }
                s.AppendLine("</ul>");
            }

            if (chapter.UnresolvedIds.Count > 0)
            {
                s.AppendLine("<p class=\"quiet\"><b>Also named:</b> " +
                             string.Join(", ", chapter.UnresolvedIds.Select(i => $"<code>{E(i)}</code>")) + "</p>");
            }

            s.AppendLine("</section>");
        }

        if (c.EmptyMaps.Count == 0) return;

        s.AppendLine("<h3>Places with nothing in them</h3>");
        s.AppendLine("<p class=\"quiet\">" +
                     string.Join(", ", c.EmptyMaps.Select(m => $"{E(m.Name.Length > 0 ? m.Name : m.Id)} ({E(m.Id)})")) +
                     "</p>");
    }

    private static void People(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeConversations) return;

        s.AppendLine("<h2 id=\"people\">People, and what they say</h2>");
        s.AppendLine("<p>Every topic each person will discuss, and every reply they can give. A " +
                     "topic shared between several people is written out under each of them.</p>");

        foreach (var person in c.Speakers)
        {
            s.AppendLine($"<section class=\"entry\"><h3 id=\"p-{A(person.Id)}\">{E(person.Name.Length > 0 ? person.Name : person.Id)}</h3>");
            s.Append($"<p class=\"id\">{E(person.Id)}");
            if (person.TypeId.Length > 0) s.Append($" · {E(person.TypeId)}");
            if (person.Gold > 0) s.Append($" · {person.Gold:N0} gold");
            s.AppendLine("</p>");

            if (person.Stock.Count > 0)
            {
                s.AppendLine("<p><b>Sells:</b> " + string.Join(", ",
                    person.Stock.SelectMany(t => new[] { t.First, t.Second })
                                .Where(x => x.Length > 0)
                                .Distinct(StringComparer.Ordinal)
                                .Select(x => $"<code>{E(x)}</code>")) + "</p>");
            }

            foreach (var raw in person.Dialog!.All)
            {
                var topic = c.Adventure.ResolveTopic(raw);
                if (!topic.HasText) continue;

                s.AppendLine("<div class=\"topic\">");
                s.AppendLine($"<p class=\"ask\">{E(topic.Topic.Length > 0 ? topic.Topic : topic.Id)}</p>");
                if (topic.Question.Length > 0) s.AppendLine($"<p class=\"say\">“{E(topic.Question)}”</p>");
                if (topic.Gate.Length > 0) s.AppendLine($"<p class=\"gate\">only when <code>{E(topic.Gate)}</code></p>");

                foreach (var reply in topic.Replies)
                {
                    if (reply.Text.Length > 0) s.AppendLine($"<p class=\"reply\">“{E(reply.Text)}”</p>");

                    foreach (var choice in reply.Choices.Where(x => x.Text.Length > 0))
                    {
                        s.Append($"<p class=\"choice\">you: “{E(choice.Text)}”");
                        if (choice.Symbol.Length > 0) s.Append($" <code>{E(choice.Symbol)}</code>");
                        s.AppendLine("</p>");
                    }

                    if (reply.Symbols.Count > 0)
                    {
                        s.AppendLine("<p class=\"tags\">names " +
                            string.Join(", ", reply.Symbols.Select(t => $"<code>{E(t)}</code>")) + "</p>");
                    }
                }
                s.AppendLine("</div>");
            }
            s.AppendLine("</section>");
        }
    }

    private static void Things(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeItems) return;

        var items = c.Adventure.Items;
        s.AppendLine("<h2 id=\"things\">Things</h2>");
        s.AppendLine($"<p>{items.Count} item types, by category. Weight is in the game's own " +
                     "hundredths, so 1600 is 16.00.</p>");

        foreach (var group in items.GroupBy(i => i.Category).OrderBy(g => g.Key))
        {
            s.AppendLine($"<h3>{E(Game.ItemTables.CategoryName(group.Key))}</h3>");
            s.AppendLine("<table class=\"grid\"><thead><tr><th>Name</th><th>Kind</th><th>Value</th>" +
                         "<th>Weight</th><th>Damage</th><th>Armour</th><th>Condition</th><th>Notes</th></tr></thead><tbody>");

            foreach (var item in group.OrderBy(i => i.Name, StringComparer.CurrentCulture))
            {
                var notes = new List<string>();
                if (item.SpellId.Length > 0) notes.Add($"casts <code>{E(item.SpellId)}</code>");
                if (item.Alignment == 1) notes.Add("good only");
                if (item.Alignment == 2) notes.Add("evil only");
                foreach (var effect in item.Effects.Where(e => e.IsNamed))
                    notes.Add($"carries <code>{E(effect.SourceId)}</code>");
                if (item.Description.Length > 0) notes.Add(E(item.Description));

                s.AppendLine("<tr>" +
                    $"<td>{E(item.Name)}<br><span class=\"id\">{E(item.Id)}</span></td>" +
                    $"<td>{E(item.SubtypeName)}</td>" +
                    $"<td class=\"n\">{item.Value:N0}</td>" +
                    $"<td class=\"n\">{item.Weight:N0}</td>" +
                    $"<td class=\"n\">{(item.DamageMax > 0 ? $"{item.DamageMin}–{item.DamageMax}" : "—")}</td>" +
                    $"<td class=\"n\">{(item.Armour > 0 ? item.Armour.ToString(CultureInfo.CurrentCulture) : "—")}</td>" +
                    $"<td class=\"n\">{(item.MaxCondition > 0 ? item.MaxCondition.ToString("N0", CultureInfo.CurrentCulture) : "—")}</td>" +
                    $"<td>{string.Join("; ", notes)}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
        }
    }

    private static void Reference(StringBuilder s, Cluebook c)
    {
        if (!c.Options.IncludeReference) return;
        var a = c.Adventure;

        s.AppendLine("<h2 id=\"reference\">Bestiary, magic and rules</h2>");

        if (a.Spells.Count > 0)
        {
            s.AppendLine("<h3>Spells</h3><table class=\"grid\"><thead><tr><th>Name</th><th>Cost</th>" +
                         "<th>Difficulty</th><th>Duration</th><th>What it does</th></tr></thead><tbody>");
            foreach (var spell in a.Spells.OrderBy(x => x.Name, StringComparer.CurrentCulture))
            {
                s.AppendLine("<tr>" +
                    $"<td>{E(spell.Name)}<br><span class=\"id\">{E(spell.Id)}</span></td>" +
                    $"<td class=\"n\">{spell.Cost}</td><td class=\"n\">{spell.Difficulty}</td>" +
                    $"<td class=\"n\">{spell.Duration}</td><td>{E(spell.Description)}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
        }

        if (a.Monsters.Count > 0)
        {
            s.AppendLine("<h3>Monsters</h3><table class=\"grid\"><thead><tr><th>Name</th><th>Health</th>" +
                         "<th>Stored numbers</th></tr></thead><tbody>");
            foreach (var m in a.Monsters.OrderBy(x => x.Name, StringComparer.CurrentCulture))
            {
                s.AppendLine("<tr>" +
                    $"<td>{E(m.Name)}<br><span class=\"id\">{E(m.Id)}</span></td>" +
                    $"<td class=\"n\">{m.Health}</td>" +
                    $"<td class=\"n\">{string.Join(", ", m.Stats)}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
            s.AppendLine("<p class=\"quiet\">The ten stored numbers are shown as the record holds " +
                         "them; which is which was not established.</p>");
        }

        if (a.NpcTypes.Count > 0)
        {
            s.AppendLine("<h3>Kinds of person</h3><table class=\"grid\"><thead><tr><th>Name</th>" +
                         "<th>Stored numbers</th></tr></thead><tbody>");
            foreach (var t in a.NpcTypes.OrderBy(x => x.Name, StringComparer.CurrentCulture))
            {
                s.AppendLine("<tr>" +
                    $"<td>{E(t.Name.Length > 0 ? t.Name : t.Id)}<br><span class=\"id\">{E(t.Id)}</span></td>" +
                    $"<td class=\"n\">{string.Join(", ", t.Stats)}</td></tr>");
            }
            s.AppendLine("</tbody></table>");
        }

        if (a.Races.Count > 0)
        {
            s.AppendLine("<h3>Races</h3><dl>");
            foreach (var race in a.Races)
                s.AppendLine($"<dt>{E(race.Name)}</dt><dd>{E(race.Description)}</dd>");
            s.AppendLine("</dl>");
        }

        if (a.Skills.Count > 0)
        {
            s.AppendLine("<h3>Skills</h3><dl>");
            foreach (var skill in a.Skills)
                s.AppendLine($"<dt>{E(skill.Name)}</dt><dd>{E(skill.Description)}</dd>");
            s.AppendLine("</dl>");
        }

        if (a.Attributes.Count > 0)
        {
            s.AppendLine("<h3>Attributes</h3><p>" +
                string.Join(", ", a.Attributes.Select(x => $"{E(x.Name)} ({E(x.Abbreviation)})")) + "</p>");
        }
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static void Mentions(StringBuilder s, Dossier dossier)
    {
        if (dossier.Mentions.Count == 0)
        {
            s.AppendLine("<p class=\"quiet\">Nothing in the adventure names this.</p>");
            return;
        }

        s.AppendLine("<ul class=\"mentions\">");
        foreach (var group in dossier.Mentions.GroupBy(m => (m.Kind, m.Who)))
        {
            s.Append($"<li><b>{E(group.Key.Who)}</b> <span class=\"quiet\">({E(group.Key.Kind)})</span><ul>");
            foreach (var mention in group.DistinctBy(m => (m.Where, m.What)))
            {
                s.Append($"<li>{E(mention.Where)}");
                if (mention.What.Length > 0) s.Append($" — <q>{E(Shorten(mention.What))}</q>");
                s.Append("</li>");
            }
            s.AppendLine("</ul></li>");
        }
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string name, string value) =>
        s.AppendLine($"<tr><th>{E(name)}</th><td>{E(value)}</td></tr>");

    /// <summary>Whether a harvested string is worth showing rather than a resource id.</summary>
    private static bool Readable(string text) => text.Contains(' ') && !text.StartsWith("bres_", StringComparison.Ordinal);

    private static string Shorten(string text) => text.Length <= 220 ? text : text[..217] + "…";

    /// <summary>Escapes text going between tags.</summary>
    private static string E(string value) => HtmlPage.Escape(value);

    /// <summary>
    /// Escapes a value going into a double-quoted attribute.
    ///
    /// A separate helper because the ids these anchors are built from come out of a third-party
    /// adventure file, not out of this program: an id holding a quote would otherwise close the
    /// attribute and turn the rest of it into markup.
    /// </summary>
    private static string A(string value) => HtmlPage.EscapeAttribute(value);

    private const string Style = """
        :root{color-scheme:light}
        body{margin:0 auto;max-width:60rem;padding:2rem 1.25rem 6rem;
             font:16px/1.6 'Iowan Old Style','Palatino Linotype',Georgia,serif;color:#2b2519;background:#fdfaf3}
        h1{font-size:2.1rem;margin:0 0 .25rem;letter-spacing:-.01em}
        h2{margin:3rem 0 .75rem;padding-bottom:.3rem;border-bottom:2px solid #d8cbaa;font-size:1.5rem}
        h3{margin:2rem 0 .4rem;font-size:1.15rem}
        code{font:.85em/1.4 'Cascadia Mono',Consolas,monospace;background:#f0e9d8;padding:.1em .35em;border-radius:3px}
        .lede{color:#6f6449;margin:0 0 1.5rem}
        .id{color:#8a7d5e;font:.8rem/1.4 'Cascadia Mono',Consolas,monospace;margin:.1rem 0 .6rem}
        .quiet{color:#8a7d5e}
        nav.toc{background:#f4eddc;border:1px solid #ddd0b0;border-radius:6px;padding:.75rem 1.25rem 1rem}
        nav.toc h2{margin:.25rem 0 .5rem;border:0;font-size:1.05rem}
        nav.toc a{color:#5d4b22}
        table.facts{border-collapse:collapse;margin:.5rem 0 1.5rem}
        table.facts th{text-align:left;padding:.2rem 1.5rem .2rem 0;font-weight:600;color:#6f6449;vertical-align:top}
        table.grid{border-collapse:collapse;width:100%;font-size:.9rem;margin:.5rem 0 1.5rem}
        table.grid th,table.grid td{border-bottom:1px solid #e4dac0;padding:.35rem .5rem;text-align:left;vertical-align:top}
        table.grid thead th{background:#f4eddc;border-bottom:2px solid #d8cbaa}
        table.grid td.n{text-align:right;white-space:nowrap}
        section.entry{margin:0 0 1.5rem}
        ul.notes li{margin:.35rem 0}
        ul.mentions{margin:.4rem 0}
        ul.mentions ul{margin:.1rem 0 .5rem}
        .topic{border-left:3px solid #d8cbaa;padding:.1rem 0 .1rem .9rem;margin:.9rem 0}
        .ask{font-weight:600;margin:.2rem 0}
        .say{margin:.2rem 0;color:#4a5d75}
        .reply{margin:.35rem 0}
        .gate,.tags{font-size:.85rem;color:#8a7d5e;margin:.15rem 0}
        .choice{margin:.15rem 0 .15rem 1.25rem;color:#4a5d75}
        figure.plan{margin:1rem 0;padding:.5rem;background:#fff;border:1px solid #ddd0b0;border-radius:6px}
        dl dt{font-weight:600;margin-top:.6rem}
        dl dd{margin:.1rem 0 0 1.25rem}
        q{quotes:'“' '”'}
        """;
}
