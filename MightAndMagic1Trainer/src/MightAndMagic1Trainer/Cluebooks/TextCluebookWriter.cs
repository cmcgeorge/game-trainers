using System.Globalization;
using GameTrainers.Common.Documents;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Cluebooks;

/// <summary>
/// Renders a <see cref="Cluebook"/> as plain text.
///
/// <para>The same document as the HTML one, with the plans drawn in the characters
/// <c>docs/maze-atlas.md</c> uses rather than in SVG. Worth producing beside something richer: it
/// greps, it diffs between two versions of the same decode, and it reads in a terminal beside a
/// DOSBox window — which is where a cluebook for a 1986 game is actually used.</para>
///
/// <para>Headings, the label column, bullets and the wrapping come from <see cref="TextDocument"/>.
/// The game's own messages are written line by line instead, never re-wrapped: the breaks in them
/// are the ones the game's text window put there.</para>
/// </summary>
public static class TextCluebookWriter
{
    /// <summary>Wrap column for prose. Wide enough that a 33-column plan sits comfortably inside it.</summary>
    private const int Width = 92;

    /// <summary>Renders the whole document.</summary>
    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var doc = new TextDocument(Width) { LabelWidth = 20 };

        doc.Title("MIGHT & MAGIC BOOK ONE — THE SECRET OF THE INNER SANCTUM");
        doc.Line("A cluebook, decoded from the game's own data.");
        doc.Blank();

        Overview(doc, cluebook);
        Notes(doc, cluebook);
        Walkthrough(doc, cluebook);
        Gazetteer(doc, cluebook);
        Puzzles(doc, cluebook);
        Party(doc, cluebook);
        Spells(doc, cluebook);
        Items(doc, cluebook);
        Bestiary(doc, cluebook);
        Provenance(doc, cluebook);

        return doc.ToString();
    }

    // ---- sections -----------------------------------------------------------------------------

    private static void Overview(TextDocument doc, Cluebook c)
    {
        doc.Heading("THE GAME AT A GLANCE");
        doc.Fact("Places", $"{c.Chapters.Count}, each a 16 x 16 grid of squares");
        doc.Fact("Walls from", c.MazesAreExact ? $"{c.MazeSource} (exact)" : "the bundled layouts (a transcription)");
        doc.Fact("Location text", c.HasEventText
            ? $"{c.MessageCount:N0} messages from {c.LocationsWithText} of {c.Chapters.Count} locations"
            : "not in this copy - see the notes below");
        doc.Fact("Items", ItemBook.Catalog.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Monsters", MonsterBook.Bestiary.Count.ToString(CultureInfo.CurrentCulture));
        doc.Fact("Spells", $"{Spellbook.Cleric.Count} Cleric, {Spellbook.Sorcerer.Count} Sorcerer");
        doc.Blank();

        if (!c.Options.IncludePlans) return;

        doc.Line("  How to read a plan: north is up, column 0 is the west edge, and the");
        doc.Line("  characters between the squares are");
        doc.Line("      #   a wall you cannot pass");
        doc.Line("      D   a door");
        doc.Line("      S   passable, and flagged by the game (secret, one-way, or a trigger)");
        doc.Line("      o   a wall that is drawn but WALKABLE - a secret passage");
        doc.Line("          (a space is open floor)");
        doc.Line("  A digit inside a square is a numbered landmark, listed under that plan.");
        doc.Blank();
    }

    private static void Notes(TextDocument doc, Cluebook c)
    {
        doc.Heading("BEFORE YOU READ THIS");
        foreach (string note in c.Notes) doc.Bullet(note);
        doc.Blank();
    }

    private static void Walkthrough(TextDocument doc, Cluebook c)
    {
        if (!c.Options.IncludeWalkthrough) return;

        doc.Heading("THE SOLUTION, IN ORDER");
        doc.Paragraph("Broadly ordered rather than strictly. Everything here is a spoiler.", "  ");
        doc.Blank();

        foreach (var section in Game.Walkthrough.Sections)
        {
            doc.Line($"  {section.Title}");
            for (int i = 0; i < section.Steps.Count; i++)
                doc.Paragraph($"{i + 1}. {section.Steps[i]}", "     ");
            doc.Blank();
        }
    }

    private static void Gazetteer(TextDocument doc, Cluebook c)
    {
        doc.Heading("GAZETTEER");

        foreach (var kind in PlaceBook.KindOrder)
        {
            var chapters = c.Of(kind).ToList();
            if (chapters.Count == 0) continue;

            doc.Blank();
            doc.Line($"  == {PlaceBook.KindName(kind).ToUpperInvariant()} ==");
            doc.Blank();

            foreach (var chapter in chapters) Chapter(doc, c, chapter);
        }
    }

    private static void Chapter(TextDocument doc, Cluebook c, LocationChapter chapter)
    {
        doc.Line($"  {chapter.Name}   [{chapter.RawName}]");
        doc.Line($"    map {chapter.Index} · identification: {chapter.Confidence} · {chapter.Stats.Summary}");
        if (chapter.Blurb.Length > 0) doc.Paragraph(chapter.Blurb, "    ");

        if (c.Options.IncludePlans)
        {
            doc.Blank();
            foreach (string line in MazePlan.RenderAscii(chapter.Maze, chapter.Markers)) doc.Line("    " + line);
        }

        if (chapter.Landmarks.Count > 0)
        {
            doc.Blank();
            for (int i = 0; i < chapter.Landmarks.Count; i++)
            {
                var landmark = chapter.Landmarks[i];
                var ways = chapter.WayInAt(landmark.X, landmark.Y);
                string wayIn = ways.Count == 0
                    ? ""
                    : $" The wall on its {string.Join("/", ways)} side is not really there - that is the way in.";

                doc.Paragraph($"{i + 1}. {landmark.Name} {landmark.Where} - {landmark.Description}{wayIn} " +
                              $"[{landmark.Source}]", "    ");
            }
        }

        if (chapter.SecretPassages.Count > 0)
        {
            doc.Blank();
            doc.Paragraph(chapter.PassagesAreTerrain
                ? $"{chapter.SecretPassages.Count} of the walls drawn here can be walked through. Outdoors " +
                  "that is terrain rather than a secret, so they are drawn but not listed."
                : $"Walls that are not there ({chapter.SecretPassages.Count}): " +
                  string.Join(" · ", chapter.SecretPassages.Select(
                      t => $"({t.X}, {t.Y}) {MazeMap.DirectionName(t.Dir)}")) +
                  ". Stand on the square, walk that way, and you go straight through.", "    ");
        }

        if (chapter.Messages.Count > 0)
        {
            doc.Blank();
            doc.Line($"    What this place says ({chapter.Messages.Count} messages, in file order):");
            foreach (var message in chapter.Messages)
            {
                doc.Blank();
                foreach (string line in message.Lines) doc.Line("      | " + line);
            }
        }

        doc.Blank();
    }

    private static void Puzzles(TextDocument doc, Cluebook c)
    {
        doc.Heading("THE TWO CIPHERS");
        doc.Paragraph("Nine gold messages, one per stronghold, read in order 1-9. Six silver messages, " +
                      "one per castle, re-ordered by the rule Castle Doom gives you.", "  ");
        doc.Blank();

        Fragments(doc, "Etched in gold", c.Gold);
        Fragments(doc, "Etched in silver", c.Silver);
    }

    private static void Fragments(TextDocument doc, string heading, IReadOnlyList<FoundFragment> fragments)
    {
        doc.Line($"  {heading}");
        foreach (var found in fragments)
        {
            doc.Line($"    {found.Fragment.Label}  —  in {found.Fragment.RawName}");
            if (found.Message is null)
            {
                doc.Line("      (not read from your files)");
                continue;
            }
            foreach (string line in found.Message.Lines) doc.Line("      | " + line);
        }
        doc.Blank();
    }

    private static void Party(TextDocument doc, Cluebook c)
    {
        if (!c.Options.IncludeRules) return;

        doc.Heading("THE PARTY, AND HOW THE GAME TREATS IT");
        foreach (var cls in ClassBook.Classes)
        {
            doc.Line($"  {cls.Name}   HP/level {cls.HitPointsPerLevel}   {cls.SpellText}");
            doc.Paragraph(cls.RequirementText, "    ");
            doc.Paragraph(cls.Description, "    ");
            doc.Blank();
        }

        doc.Line("  Hit die per class:");
        doc.Line("    " + string.Join("   ", RulesBook.HitDice.Select(d => $"{d.ClassName} {d.DieText}")));
        doc.Blank();

        doc.Line("  Hit points added per level by Endurance:");
        foreach (var (min, bonus) in RulesBook.EnduranceBonuses)
            doc.Line($"    {(min == 0 ? "under 5" : min + " and up"),-12}{bonus,3}");
        doc.Blank();

        doc.Line("  Experience per level (the manual's approximation):");
        foreach (var step in ClassBook.ExperienceTable)
            doc.Line($"    level {step.Level,-3} {step.FromPreviousText,12}   running total {step.CumulativeText,14}");
        doc.Blank();

        doc.Line("  The rules under the numbers:");
        foreach (var rule in RulesBook.Rules)
        {
            doc.Blank();
            doc.Line($"    {rule.Title}");
            doc.Paragraph(rule.Text, "      ");
            doc.Paragraph($"[{rule.Confidence} — {rule.Source}]", "      ");
        }
        doc.Blank();
    }

    private static void Spells(TextDocument doc, Cluebook c)
    {
        if (!c.Options.IncludeSpells) return;

        doc.Heading("SPELLS");
        SpellList(doc, "Cleric", Spellbook.Cleric);
        SpellList(doc, "Sorcerer", Spellbook.Sorcerer);
    }

    private static void SpellList(TextDocument doc, string school, IReadOnlyList<Spell> spells)
    {
        doc.Line($"  {school}");
        foreach (var spell in spells)
        {
            doc.Line($"    {spell.Level}·{spell.Number,-3} {spell.Name,-24} {spell.CostText}");
            doc.Paragraph(spell.Description, "         ");
        }
        doc.Blank();
    }

    private static void Items(TextDocument doc, Cluebook c)
    {
        if (!c.Options.IncludeItems) return;

        doc.Heading("EVERY ITEM IN THE GAME");
        foreach (var group in ItemBook.Catalog.GroupBy(i => i.Category))
        {
            doc.Line($"  {group.Key}");
            foreach (var item in group)
            {
                doc.Line($"    {item.Id,3}  {item.Name,-20} {item.CostText,10}  {item.StatText}");
                string effect = ItemEffectBook.Describe(item.Id);
                if (effect.Length > 0) doc.Paragraph(effect, "         ");
            }
            doc.Blank();
        }
    }

    private static void Bestiary(TextDocument doc, Cluebook c)
    {
        if (!c.Options.IncludeBestiary) return;

        doc.Heading("BESTIARY");
        foreach (var group in MonsterBook.Bestiary.GroupBy(m => m.Group))
        {
            doc.Line($"  {group.Key}");
            foreach (var m in group)
                doc.Line($"    {m.Id,3}  {m.Name,-18} HP {m.HpBase,3}+  AC {m.ArmorClass,2}  " +
                         $"dmg {m.Damage,2} x{m.Attacks}  speed {m.Speed,3}  up to {m.MaxCount,2}  {m.Experience,7:N0} XP");
            doc.Blank();
        }
    }

    private static void Provenance(TextDocument doc, Cluebook c)
    {
        doc.Heading("WHERE ALL OF THIS CAME FROM");
        doc.Bullet("The walls: the game's Mazedata.dta, two co-registered planes per place — one saying " +
                   "what is drawn, the other what you may walk through.");
        doc.Bullet("What each place says: the 55 .ovr overlays, read from your own installation, never shipped.");
        doc.Bullet("Items, monsters and the class tables: extracted from MM.EXE.");
        doc.Bullet("The levelling, combat and dice rules: disassembled from MM.EXE's own routines.");
        doc.Bullet("The walkthrough and the item effects: community guides, cross-checked.");

        if (c.Problems.Count == 0) return;

        doc.Blank();
        doc.Line("  Files that could not be read:");
        foreach (string problem in c.Problems) doc.Bullet(problem, "    ");
    }
}
