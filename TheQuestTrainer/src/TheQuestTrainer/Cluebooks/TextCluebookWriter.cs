using System.Globalization;
using GameTrainers.Common.Documents;

namespace TheQuestTrainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as plain text.
///
/// The same document as the HTML one, minus the plan: something to grep, to diff between two
/// versions of an adventure, and to read in a terminal beside the game.
///
/// Headings, the label column, bullets and the wrapping all come from <see cref="TextDocument"/>;
/// what is here is which sections there are and what goes in them.
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
        var doc = new TextDocument(Width);

        doc.Title(a.Name);
        doc.Line($"A cluebook for The Quest, decompiled from {a.SourcePath}.");
        doc.Blank();

        doc.Heading("THE ADVENTURE AT A GLANCE");
        doc.Fact("World", a.Name);
        doc.Fact("Resource pack", a.Pack);
        doc.Fact("Database", a.Database);
        doc.Fact("Outdoor grid", $"{a.GridWidth} x {a.GridHeight} cells of {Game.MapLayout.GridMapTiles} tiles");
        doc.Fact("Maps", $"{a.Maps.Count} ({a.OutdoorMaps.Count()} outdoor, {a.Interiors.Count()} interiors)");
        doc.Fact("Quests", a.Quests.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("People", $"{a.People.Count} ({cluebook.Speakers.Count} with something to say)");
        doc.Fact("Topics", cluebook.TopicCount.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Map objects", a.MapObjects.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Item types", a.Items.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Spells", a.Spells.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Creatures", $"{a.Monsters.Count} monster types, {a.NpcTypes.Count} person types");
        doc.Fact("Format version", a.FormatVersion.ToString(CultureInfo.CurrentCulture));
        doc.Blank();

        doc.Heading("BEFORE YOU READ THIS");
        foreach (string note in cluebook.Notes) doc.Bullet(note);
        doc.Blank();

        doc.Heading("THE QUESTS");
        if (cluebook.Quests.Count == 0) doc.Line("  This adventure has no quest log.");
        foreach (var quest in cluebook.Quests)
        {
            doc.Line($"  {quest.Name}  [{quest.Id}]");
            if (quest.Description.Length > 0) doc.Paragraph(quest.Description, "    ");
            foreach (var group in quest.Mentions.GroupBy(m => (m.Kind, m.Who)))
            {
                doc.Line($"    {group.Key.Who} ({group.Key.Kind})");
                foreach (var mention in group.DistinctBy(m => (m.Where, m.What)))
                {
                    doc.Line($"      - {mention.Where}");
                    if (mention.What.Length > 0) doc.Paragraph("\"" + mention.What + "\"", "        ");
                }
            }
            doc.Blank();
        }

        doc.Heading("GAZETTEER");
        foreach (var chapter in cluebook.Chapters)
        {
            var m = chapter.Map;
            string where = m.IsOutdoorCell ? $"cell {m.CellLabel}" : "interior";
            doc.Line($"  {(m.Name.Length > 0 ? m.Name : m.Id)}  [{m.Id}]  {where}, {m.SizeLabel}" +
                         (m.Notes.Length > 0 ? $"  ({m.Notes})" : ""));

            if (chapter.People.Count > 0)
                doc.Paragraph("People: " + string.Join(", ", chapter.People.Select(p => p.Name.Length > 0 ? p.Name : p.Id)), "    ");

            foreach (var o in chapter.Objects)
            {
                doc.Line($"    - {o.Id}");
                foreach (string text in o.Text.Where(t => t.Contains(' ')))
                    doc.Paragraph("\"" + text + "\"", "        ");
            }

            if (chapter.UnresolvedIds.Count > 0)
                doc.Paragraph("Also named: " + string.Join(", ", chapter.UnresolvedIds), "    ");

            doc.Blank();
        }

        if (cluebook.EmptyMaps.Count > 0)
        {
            doc.Line("  Places with nothing in them:");
            doc.Paragraph(string.Join(", ", cluebook.EmptyMaps.Select(m => $"{(m.Name.Length > 0 ? m.Name : m.Id)} [{m.Id}]")), "    ");
            doc.Blank();
        }

        if (cluebook.Options.IncludeConversations)
        {
            doc.Heading("PEOPLE, AND WHAT THEY SAY");
            foreach (var person in cluebook.Speakers)
            {
                doc.Line($"  {(person.Name.Length > 0 ? person.Name : person.Id)}  [{person.Id}]" +
                             (person.Gold > 0 ? $"  {person.Gold:N0} gold" : ""));

                foreach (var raw in person.Dialog!.All)
                {
                    var topic = a.ResolveTopic(raw);
                    if (!topic.HasText) continue;

                    doc.Line($"    * {(topic.Topic.Length > 0 ? topic.Topic : topic.Id)}");
                    if (topic.Question.Length > 0) doc.Paragraph("You: \"" + topic.Question + "\"", "      ");
                    if (topic.Gate.Length > 0) doc.Line($"      only when {topic.Gate}");

                    foreach (var reply in topic.Replies)
                    {
                        if (reply.Text.Length > 0) doc.Paragraph("\"" + reply.Text + "\"", "      ");
                        foreach (var choice in reply.Choices.Where(x => x.Text.Length > 0))
                        {
                            doc.Paragraph("You: \"" + choice.Text + "\"" +
                                       (choice.Symbol.Length > 0 ? $" [{choice.Symbol}]" : ""), "        ");
                        }
                        if (reply.Symbols.Count > 0)
                            doc.Line("      names " + string.Join(", ", reply.Symbols));
                    }
                }
                doc.Blank();
            }
        }

        if (cluebook.Options.IncludeItems)
        {
            doc.Heading("THINGS");
            foreach (var group in a.Items.GroupBy(i => i.Category).OrderBy(g => g.Key))
            {
                doc.Line($"  {Game.ItemTables.CategoryName(group.Key)}");
                foreach (var item in group.OrderBy(i => i.Name, StringComparer.CurrentCulture))
                {
                    var bits = new List<string> { $"{item.Value:N0}g", $"wt {item.Weight}" };
                    if (item.DamageMax > 0) bits.Add($"dmg {item.DamageMin}-{item.DamageMax}");
                    if (item.Armour > 0) bits.Add($"armour {item.Armour}");
                    if (item.MaxCondition > 0) bits.Add($"condition {item.MaxCondition}");
                    if (item.SpellId.Length > 0) bits.Add($"casts {item.SpellId}");
                    doc.Line($"    {item.Name}  [{item.Id}]  {item.SubtypeName}; {string.Join("; ", bits)}");
                    if (item.Description.Length > 0) doc.Paragraph(item.Description, "      ");
                }
                doc.Blank();
            }
        }

        if (cluebook.Options.IncludeReference)
        {
            doc.Heading("BESTIARY, MAGIC AND RULES");

            if (a.Spells.Count > 0)
            {
                doc.Line("  Spells");
                foreach (var spell in a.Spells.OrderBy(x => x.Name, StringComparer.CurrentCulture))
                {
                    doc.Line($"    {spell.Name}  [{spell.Id}]  cost {spell.Cost}, difficulty {spell.Difficulty}, duration {spell.Duration}");
                    if (spell.Description.Length > 0) doc.Paragraph(spell.Description, "      ");
                }
                doc.Blank();
            }

            if (a.Monsters.Count > 0)
            {
                doc.Line("  Monsters");
                foreach (var m in a.Monsters.OrderBy(x => x.Name, StringComparer.CurrentCulture))
                    doc.Line($"    {m.Name} ({m.PluralName})  [{m.Id}]  health {m.Health}; stored {string.Join(", ", m.Stats)}");
                doc.Blank();
            }

            if (a.Races.Count > 0)
            {
                doc.Line("  Races");
                foreach (var race in a.Races)
                {
                    doc.Line($"    {race.Name}");
                    if (race.Description.Length > 0) doc.Paragraph(race.Description, "      ");
                }
                doc.Blank();
            }

            if (a.Skills.Count > 0)
            {
                doc.Line("  Skills");
                foreach (var skill in a.Skills)
                {
                    doc.Line($"    {skill.Name}");
                    if (skill.Description.Length > 0) doc.Paragraph(skill.Description, "      ");
                }
                doc.Blank();
            }

            if (a.Attributes.Count > 0)
            {
                doc.Line("  Attributes: " +
                    string.Join(", ", a.Attributes.Select(x => $"{x.Name} ({x.Abbreviation})")));
                doc.Blank();
            }
        }

        return doc.ToString();
    }

}
