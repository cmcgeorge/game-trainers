using System.Text;
using GameTrainers.Common.Documents;
using WastelandTrainer.Game;

namespace WastelandTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Wasteland — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>Wasteland</h1>");
        s.AppendLine("<p class=\"lede\">A cluebook for Interplay and Electronic Arts' 1988 post-apocalyptic role-playing game.</p>");
        Contents(s, cluebook.Options);
        Overview(s);
        if (cluebook.Options.IncludeAreas) Areas(s, cluebook.Areas);
        if (cluebook.Options.IncludeSkills) Skills(s);
        if (cluebook.Options.IncludeItems) Items(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (options.IncludeAreas) s.AppendLine("<li><a href=\"#areas\">Areas</a></li>");
        if (options.IncludeSkills) s.AppendLine("<li><a href=\"#skills\">Skill reference</a></li>");
        if (options.IncludeItems) s.AppendLine("<li><a href=\"#items\">Item reference</a></li>");
        if (options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", "Wasteland");
        Row(s, "Year", "1988");
        Row(s, "Developer", "Interplay Productions");
        Row(s, "Publisher", "Electronic Arts");
        Row(s, "Setting", "Arizona desert after a nuclear war");
        Row(s, "Party", "Four Desert Rangers, with up to three recruits met in the world");
        Row(s, "Skills", $"{SkillBook.Skills.Count} skills");
        Row(s, "Items", $"{ItemCatalog.Items.Count - 1} items");
        s.AppendLine("</table>");
        s.AppendLine("<p>You command Desert Rangers investigating a growing robot threat across the ruins of Arizona. Build a capable party, earn the trust of scattered settlements, and follow the trail through Las Vegas to Base Cochise. There, stop the Machine Priest Faran Brygo and the robotic army before it reaches the surface.</p>");
        s.AppendLine("<p>The trainer's Maps tab provides a live position display and the Save Editor can teleport an offline save. This cluebook is a read-only reference and needs neither an attached process nor a game installation.</p>");
    }

    private static void Areas(StringBuilder s, IReadOnlyList<MapArea> areas)
    {
        s.AppendLine("<h2 id=\"areas\">Areas</h2>");
        foreach (var area in areas)
        {
            s.AppendLine($"<h3>{E(area.Name)}</h3>");
            s.AppendLine($"<p>{E(area.Notes)}</p>");
            s.AppendLine("<table class=\"ref\"><tr><th>Landmark</th><th>Notes</th></tr>");
            foreach (var landmark in area.Landmarks)
                s.AppendLine($"<tr><td>{E(landmark.Name)}</td><td>{E(landmark.Notes)}</td></tr>");
            s.AppendLine("</table>");
        }
    }

    private static void Skills(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"skills\">Skill reference</h2>");
        s.AppendLine("<table class=\"ref\"><tr><th>#</th><th>Skill</th><th>Minimum IQ</th><th>Use</th><th>Where used</th></tr>");
        foreach (var skill in SkillBook.Skills)
            s.AppendLine($"<tr><td>{skill.Id}</td><td>{E(skill.Name)}</td><td>{skill.MinIq}</td><td>{E(skill.Use)}</td><td>{E(skill.Where)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Items(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"items\">Item reference</h2>");
        s.AppendLine("<table class=\"ref\"><tr><th>#</th><th>Item</th><th>Category</th><th>Description</th><th>Damage / use</th></tr>");
        foreach (var item in ItemCatalog.Items.Where(item => item.Id != 0))
            s.AppendLine($"<tr><td>{item.Id}</td><td>{E(item.Name)}</td><td>{E(item.Category)}</td><td>{E(item.Description)}</td><td>{E(item.Damage)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2>");
        s.AppendLine("<ol>");
        s.AppendLine("<li><b>Prepare at Ranger Center.</b> Build a balanced four-ranger party, equip pistols and armor, then leave for the southern desert.</li>");
        s.AppendLine("<li><b>Earn local allies.</b> Help Highpool repair its water pipe and clear the Agricultural Center. These areas provide early experience, gear, and a safe introduction to the wasteland.</li>");
        s.AppendLine("<li><b>Work through Quartz and Needles.</b> Free the Quartz hostages, defeat Ugly's gang, then investigate the Temple of Blood in Needles to recover the Bloodstaff.</li>");
        s.AppendLine("<li><b>Follow the robot trail.</b> Use Las Vegas and Brygo's Palace to uncover the routes into the sewers and Sleeper Base. Visit Darwin for Finster, the Mind Maze, and the Blackstar Key.</li>");
        s.AppendLine("<li><b>Arm for the endgame.</b> Gather the Guardian Citadel keys, Power Armor, power converter, plasma coupler, passes, and energy weapons. Keep quest components rather than selling them.</li>");
        s.AppendLine("<li><b>End the threat at Base Cochise.</b> Reach the base, survive the robot defenses, trigger the Core Terminal's self-destruct sequence, and escape before detonation.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        foreach (var section in WastelandTrainer.Game.Walkthrough.Sections)
            s.AppendLine($"<li><b>{E(section.Title)}.</b> {E(section.Body)}</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #6a4a2f; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #b9a489; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f0e8; border: 1px solid #d6c6ae; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #ece3d5; text-align: left; padding: 4px 8px; border: 1px solid #cdbda7; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #cdbda7; vertical-align: top; }
        table.facts th { width: 160px; white-space: nowrap; }
        table.ref { font-size: 0.92em; }
        """;
}
