using System.Globalization;
using System.Text;

namespace TheQuestTrainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as plain text.
///
/// The same document as the HTML one, minus the plan: something to grep, to diff between two
/// versions of an adventure, and to read in a terminal beside the game.
/// </summary>
public static class TextCluebookWriter
{
    /// <summary>Wrap column for prose.</summary>
    private const int Width = 92;

    /// <summary>Renders the whole document.</summary>
    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var a = cluebook.Adventure;
        var s = new StringBuilder();

        Title(s, a.Name);
        s.AppendLine($"A cluebook for The Quest, decompiled from {a.SourcePath}.");
        s.AppendLine();

        Heading(s, "THE ADVENTURE AT A GLANCE");
        Fact(s, "World", a.Name);
        Fact(s, "Resource pack", a.Pack);
        Fact(s, "Database", a.Database);
        Fact(s, "Outdoor grid", $"{a.GridWidth} x {a.GridHeight} cells of {Game.MapLayout.GridMapTiles} tiles");
        Fact(s, "Maps", $"{a.Maps.Count} ({a.OutdoorMaps.Count()} outdoor, {a.Interiors.Count()} interiors)");
        Fact(s, "Quests", a.Quests.Count.ToString(CultureInfo.CurrentCulture));
        Fact(s, "People", $"{a.People.Count} ({cluebook.Speakers.Count} with something to say)");
        Fact(s, "Topics", cluebook.TopicCount.ToString(CultureInfo.CurrentCulture));
        Fact(s, "Map objects", a.MapObjects.Count.ToString(CultureInfo.CurrentCulture));
        Fact(s, "Item types", a.Items.Count.ToString(CultureInfo.CurrentCulture));
        Fact(s, "Spells", a.Spells.Count.ToString(CultureInfo.CurrentCulture));
        Fact(s, "Creatures", $"{a.Monsters.Count} monster types, {a.NpcTypes.Count} person types");
        Fact(s, "Format version", a.FormatVersion.ToString(CultureInfo.CurrentCulture));
        s.AppendLine();

        Heading(s, "BEFORE YOU READ THIS");
        foreach (string note in cluebook.Notes) Bullet(s, note);
        s.AppendLine();

        Heading(s, "THE QUESTS");
        if (cluebook.Quests.Count == 0) s.AppendLine("  This adventure has no quest log.");
        foreach (var quest in cluebook.Quests)
        {
            s.AppendLine($"  {quest.Name}  [{quest.Id}]");
            if (quest.Description.Length > 0) Wrapped(s, quest.Description, "    ");
            foreach (var group in quest.Mentions.GroupBy(m => (m.Kind, m.Who)))
            {
                s.AppendLine($"    {group.Key.Who} ({group.Key.Kind})");
                foreach (var mention in group.DistinctBy(m => (m.Where, m.What)))
                {
                    s.AppendLine($"      - {mention.Where}");
                    if (mention.What.Length > 0) Wrapped(s, "\"" + mention.What + "\"", "        ");
                }
            }
            s.AppendLine();
        }

        Heading(s, "GAZETTEER");
        foreach (var chapter in cluebook.Chapters)
        {
            var m = chapter.Map;
            string where = m.IsOutdoorCell ? $"cell {m.CellLabel}" : "interior";
            s.AppendLine($"  {(m.Name.Length > 0 ? m.Name : m.Id)}  [{m.Id}]  {where}, {m.SizeLabel}" +
                         (m.Notes.Length > 0 ? $"  ({m.Notes})" : ""));

            if (chapter.People.Count > 0)
                Wrapped(s, "People: " + string.Join(", ", chapter.People.Select(p => p.Name.Length > 0 ? p.Name : p.Id)), "    ");

            foreach (var o in chapter.Objects)
            {
                s.AppendLine($"    - {o.Id}");
                foreach (string text in o.Text.Where(t => t.Contains(' ')))
                    Wrapped(s, "\"" + text + "\"", "        ");
            }

            if (chapter.UnresolvedIds.Count > 0)
                Wrapped(s, "Also named: " + string.Join(", ", chapter.UnresolvedIds), "    ");

            s.AppendLine();
        }

        if (cluebook.EmptyMaps.Count > 0)
        {
            s.AppendLine("  Places with nothing in them:");
            Wrapped(s, string.Join(", ", cluebook.EmptyMaps.Select(m => $"{(m.Name.Length > 0 ? m.Name : m.Id)} [{m.Id}]")), "    ");
            s.AppendLine();
        }

        if (cluebook.Options.IncludeConversations)
        {
            Heading(s, "PEOPLE, AND WHAT THEY SAY");
            foreach (var person in cluebook.Speakers)
            {
                s.AppendLine($"  {(person.Name.Length > 0 ? person.Name : person.Id)}  [{person.Id}]" +
                             (person.Gold > 0 ? $"  {person.Gold:N0} gold" : ""));

                foreach (var raw in person.Dialog!.All)
                {
                    var topic = a.ResolveTopic(raw);
                    if (!topic.HasText) continue;

                    s.AppendLine($"    * {(topic.Topic.Length > 0 ? topic.Topic : topic.Id)}");
                    if (topic.Question.Length > 0) Wrapped(s, "You: \"" + topic.Question + "\"", "      ");
                    if (topic.Gate.Length > 0) s.AppendLine($"      only when {topic.Gate}");

                    foreach (var reply in topic.Replies)
                    {
                        if (reply.Text.Length > 0) Wrapped(s, "\"" + reply.Text + "\"", "      ");
                        foreach (var choice in reply.Choices.Where(x => x.Text.Length > 0))
                        {
                            Wrapped(s, "You: \"" + choice.Text + "\"" +
                                       (choice.Symbol.Length > 0 ? $" [{choice.Symbol}]" : ""), "        ");
                        }
                        if (reply.Symbols.Count > 0)
                            s.AppendLine("      names " + string.Join(", ", reply.Symbols));
                    }
                }
                s.AppendLine();
            }
        }

        if (cluebook.Options.IncludeItems)
        {
            Heading(s, "THINGS");
            foreach (var group in a.Items.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                s.AppendLine($"  {Game.ItemTables.CategoryName(group.Key)}");
                foreach (var item in group.OrderBy(i => i.Name, StringComparer.CurrentCulture))
                {
                    var bits = new List<string> { $"{item.Value:N0}g", $"wt {item.Weight}" };
                    if (item.DamageMax > 0) bits.Add($"dmg {item.DamageMin}-{item.DamageMax}");
                    if (item.Armour > 0) bits.Add($"armour {item.Armour}");
                    if (item.MaxCondition > 0) bits.Add($"condition {item.MaxCondition}");
                    if (item.SpellId.Length > 0) bits.Add($"casts {item.SpellId}");
                    s.AppendLine($"    {item.Name}  [{item.Id}]  {item.SubtypeName}; {string.Join("; ", bits)}");
                    if (item.Description.Length > 0) Wrapped(s, item.Description, "      ");
                }
                s.AppendLine();
            }
        }

        if (cluebook.Options.IncludeReference)
        {
            Heading(s, "BESTIARY, MAGIC AND RULES");

            if (a.Spells.Count > 0)
            {
                s.AppendLine("  Spells");
                foreach (var spell in a.Spells.OrderBy(x => x.Name, StringComparer.CurrentCulture))
                {
                    s.AppendLine($"    {spell.Name}  [{spell.Id}]  cost {spell.Cost}, difficulty {spell.Difficulty}, duration {spell.Duration}");
                    if (spell.Description.Length > 0) Wrapped(s, spell.Description, "      ");
                }
                s.AppendLine();
            }

            if (a.Monsters.Count > 0)
            {
                s.AppendLine("  Monsters");
                foreach (var m in a.Monsters.OrderBy(x => x.Name, StringComparer.CurrentCulture))
                    s.AppendLine($"    {m.Name} ({m.PluralName})  [{m.Id}]  health {m.Health}; stored {string.Join(", ", m.Stats)}");
                s.AppendLine();
            }

            if (a.Races.Count > 0)
            {
                s.AppendLine("  Races");
                foreach (var race in a.Races)
                {
                    s.AppendLine($"    {race.Name}");
                    if (race.Description.Length > 0) Wrapped(s, race.Description, "      ");
                }
                s.AppendLine();
            }

            if (a.Skills.Count > 0)
            {
                s.AppendLine("  Skills");
                foreach (var skill in a.Skills)
                {
                    s.AppendLine($"    {skill.Name}");
                    if (skill.Description.Length > 0) Wrapped(s, skill.Description, "      ");
                }
                s.AppendLine();
            }

            if (a.Attributes.Count > 0)
            {
                s.AppendLine("  Attributes: " +
                    string.Join(", ", a.Attributes.Select(x => $"{x.Name} ({x.Abbreviation})")));
                s.AppendLine();
            }
        }

        return s.ToString();
    }

    private static void Title(StringBuilder s, string text)
    {
        s.AppendLine(text);
        s.AppendLine(new string('=', Math.Min(Width, Math.Max(4, text.Length))));
    }

    private static void Heading(StringBuilder s, string text)
    {
        s.AppendLine();
        s.AppendLine(text);
        s.AppendLine(new string('-', Math.Min(Width, Math.Max(4, text.Length))));
    }

    private static void Fact(StringBuilder s, string name, string value) =>
        s.AppendLine($"  {name,-18}{value}");

    /// <summary>A bullet whose continuation lines hang under the text, not under the dash.</summary>
    private static void Bullet(StringBuilder s, string text)
    {
        var lines = new StringBuilder();
        Wrapped(lines, text, "    ");
        s.Append("  - ").Append(lines.ToString(4, lines.Length - 4));
    }

    /// <summary>Wraps <paramref name="text"/> at <see cref="Width"/>, indenting every line.</summary>
    private static void Wrapped(StringBuilder s, string text, string indent)
    {
        int room = Math.Max(20, Width - indent.Length);
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > room)
            {
                s.AppendLine(indent + line);
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) s.AppendLine(indent + line);
    }
}
