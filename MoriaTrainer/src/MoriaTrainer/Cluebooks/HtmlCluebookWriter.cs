using System.Text;
using GameTrainers.Common.Documents;
using MoriaTrainer.Game;

namespace MoriaTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "The Dungeons of Moria — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();

        s.AppendLine("<h1>The Dungeons of Moria</h1>");
        s.AppendLine("<p class=\"lede\">A reference and strategy guide for UMoria 5.5.2: descend to level 50, defeat the Balrog, and return alive to the surface.</p>");

        Contents(s, cluebook.Options);
        Overview(s);
        if (cluebook.Options.IncludeLevels) Levels(s, cluebook.Levels);
        if (cluebook.Options.IncludeRacesAndClasses) RacesAndClasses(s);
        if (cluebook.Options.IncludeSpells) Spells(s);
        if (cluebook.Options.IncludeItems) Items(s);
        if (cluebook.Options.IncludeBestiary) Bestiary(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (options.IncludeLevels) s.AppendLine("<li><a href=\"#levels\">Descent reference</a></li>");
        if (options.IncludeRacesAndClasses) s.AppendLine("<li><a href=\"#characters\">Races and classes</a></li>");
        if (options.IncludeSpells) s.AppendLine("<li><a href=\"#spells\">Spells and prayers</a></li>");
        if (options.IncludeItems) s.AppendLine("<li><a href=\"#items\">Item reference</a></li>");
        if (options.IncludeBestiary) s.AppendLine("<li><a href=\"#bestiary\">Monster bestiary</a></li>");
        if (options.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) s.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", "The Dungeons of Moria (UMoria)");
        Row(s, "Version", PlayerFormat.GameVersion);
        Row(s, "Authors", "Robert Alan Koeneke and James E. Wilson");
        Row(s, "Dungeon", "50 procedural dungeon levels beneath the town");
        Row(s, "Goal", "Defeat the Balrog on level 50 and return to the surface");
        Row(s, "Character", "One adventurer, selected from 8 races and 6 classes");
        Row(s, "Stats", "STR, INT, WIS, DEX, CON, CHR (3 through 18/100)");
        Row(s, "Magic", $"{SpellBook.MageSpells.Count} mage spells and {SpellBook.PriestPrayers.Count} priest prayers");
        Row(s, "Maximum level", PlayerFormat.MaxLevel.ToString());
        s.AppendLine("</table>");
        s.AppendLine("<p>Moria is a single-character roguelike. Every dungeon layout is generated when you descend, so there are no fixed dungeon maps to memorize. Learn the symbols, carry light and food, identify dangerous enemies early, and keep a reliable escape route through Word of Recall.</p>");
    }

    private static void Levels(StringBuilder s, IReadOnlyList<LevelInfo> levels)
    {
        s.AppendLine("<h2 id=\"levels\">Descent reference</h2>");
        s.AppendLine("<p>The town is depth 0. Dungeon depth is measured in 50-foot increments. The level layouts are procedural; this reference lists important milestones rather than fixed maps.</p>");
        s.AppendLine(DescentSvg());
        s.AppendLine("<table class=\"ref\"><tr><th scope=\"col\">Depth</th><th scope=\"col\">Feet</th><th scope=\"col\">Notable monsters</th><th scope=\"col\">Notable items</th><th scope=\"col\">Advice</th></tr>");
        foreach (var level in levels)
            s.AppendLine($"<tr><td>{E(level.Name)}</td><td>{level.Feet}</td><td>{E(level.NotableMonsters)}</td><td>{E(level.NotableItems)}</td><td>{E(level.Notes)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static string DescentSvg()
    {
        const int width = 800;
        const int height = 100;
        var svg = SvgCanvas.Responsive(width, height, "Moria descent from town to the Balrog");
        svg.Rect(0, 0, width, height, ("fill", "#14151A"));
        svg.Rect(30, 42, 740, 16, ("fill", "#6C7488"));
        svg.Rect(755, 34, 15, 32, ("fill", "#A95353"));
        svg.Text(30, 32, "Town", ("text-anchor", "middle"), ("fill", "#E0E2E8"));
        svg.Text(400, 32, "Level 25", ("text-anchor", "middle"), ("fill", "#E0E2E8"));
        svg.Text(770, 32, "Level 50", ("text-anchor", "middle"), ("fill", "#E0E2E8"));
        svg.Text(770, 82, "Balrog", ("text-anchor", "middle"), ("fill", "#F0C46B"));
        return svg.ToSvg();
    }

    private static void RacesAndClasses(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"characters\">Races and classes</h2>");
        s.AppendLine("<h3>Races</h3><table class=\"ref\"><tr><th scope=\"col\">Race</th><th scope=\"col\">Allowed classes</th><th scope=\"col\">Key adjustments</th><th scope=\"col\">Infravision</th><th scope=\"col\">Notes</th></tr>");
        foreach (var race in RaceBook.Races)
            s.AppendLine($"<tr><td>{E(race.Name)}</td><td>{E(race.AllowedClasses)}</td><td>{E(race.KeyAdjustments)}</td><td>{(race.Infravision ? "Yes" : "No")}</td><td>{E(race.Notes)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<h3>Classes</h3><table class=\"ref\"><tr><th scope=\"col\">Class</th><th scope=\"col\">Prime stat</th><th scope=\"col\">Hit die</th><th scope=\"col\">Mana basis</th><th scope=\"col\">Notes</th><th scope=\"col\">Skill gain (fight/bow/device/disarm/throw)</th></tr>");
        foreach (var characterClass in ClassBook.Classes)
            s.AppendLine($"<tr><td>{E(characterClass.Name)}</td><td>{E(characterClass.PrimeStat)}</td><td>{E(characterClass.HitDie)}</td><td>{E(characterClass.ManaBasis)}</td><td>{E(characterClass.Notes)}</td><td>{E(characterClass.SkillRow)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Spells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"spells\">Spells and prayers</h2>");
        s.AppendLine("<p>Spells are assigned stable letters in UMoria 5.5.2. Carry the appropriate spellbook and use the displayed letter when prompted.</p>");
        SpellTable(s, "Mage spells", SpellBook.MageSpells);
        SpellTable(s, "Priest prayers", SpellBook.PriestPrayers);
    }

    private static void SpellTable(StringBuilder s, string heading, IReadOnlyList<SpellInfo> spells)
    {
        s.AppendLine($"<h3>{E(heading)}</h3><table class=\"ref\"><tr><th scope=\"col\">Letter</th><th scope=\"col\">Name</th><th scope=\"col\">Level</th><th scope=\"col\">Mana</th><th scope=\"col\">Book</th><th scope=\"col\">Effect</th><th scope=\"col\">Damage</th></tr>");
        foreach (var spell in spells)
            s.AppendLine($"<tr><td>{E(spell.Letter)}</td><td>{E(spell.Name)}</td><td>{spell.MinLevel}</td><td>{spell.ManaCost}</td><td>{E(spell.Book)}</td><td>{E(spell.Effect)}</td><td>{E(spell.Damage)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Items(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"items\">Item reference</h2>");
        s.AppendLine("<h3>Item categories</h3><table class=\"ref\"><tr><th scope=\"col\">Symbol</th><th scope=\"col\">Category</th><th scope=\"col\">Examples</th><th scope=\"col\">Notes</th></tr>");
        foreach (var item in ItemBook.Items)
            s.AppendLine($"<tr><td>{E(item.DisplayChar)}</td><td>{E(item.Category)}</td><td>{E(item.Examples)}</td><td>{E(item.Notes)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<h3>Ego weapons</h3><table class=\"ref\"><tr><th scope=\"col\">Code</th><th scope=\"col\">Name</th><th scope=\"col\">Effect</th></tr>");
        foreach (var weapon in ItemBook.EgoWeapons)
            s.AppendLine($"<tr><td>{E(weapon.Code)}</td><td>{E(weapon.Name)}</td><td>{E(weapon.Effect)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<h3>Wearable flags</h3><table class=\"ref\"><tr><th scope=\"col\">Code</th><th scope=\"col\">Effect</th></tr>");
        foreach (var flag in ItemBook.WearableFlags)
            s.AppendLine($"<tr><td>{E(flag.Code)}</td><td>{E(flag.Effect)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Bestiary(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"bestiary\">Monster bestiary</h2>");
        s.AppendLine("<p>This is a curated field guide to early threats, major dragons, liches, and the Balrog.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th scope=\"col\">Symbol</th><th scope=\"col\">Creature</th><th scope=\"col\">Depth</th><th scope=\"col\">AC</th><th scope=\"col\">HP</th><th scope=\"col\">XP</th><th scope=\"col\">Traits</th><th scope=\"col\">Advice</th></tr>");
        foreach (var creature in MonsterBook.Creatures)
            s.AppendLine($"<tr><td>{E(creature.Symbol)}</td><td>{E(creature.Name)}</td><td>{creature.Level}</td><td>{creature.ArmorClass}</td><td>{E(creature.HitDice)}</td><td>{creature.Exp}</td><td>{E(creature.Flags)}</td><td>{E(creature.Recall)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        s.AppendLine("<li><b>Create a forgiving character.</b> A Dwarf Warrior is a strong first run; a Half-Elf Rogue, Ranger, or Mage trades durability for utility and magic.</li>");
        s.AppendLine("<li><b>Prepare in town.</b> Buy a light source, food, a weapon, armor, and an escape resource before the first descent. Keep the town stores in mind for selling identified loot.</li>");
        s.AppendLine("<li><b>Learn the early dungeon.</b> Explore shallow levels slowly, search suspicious walls and doors, and retreat whenever your HP, light, or food becomes uncertain.</li>");
        s.AppendLine("<li><b>Build core defenses.</b> Before the deep dungeon, prioritize free action, see invisible, healing, and the elemental resistances appropriate to the dragons you meet.</li>");
        s.AppendLine("<li><b>Farm useful depths.</b> Level 25 introduces valuable permanent-stat and mana-restoration resources. Do not descend merely because you can find stairs.</li>");
        s.AppendLine("<li><b>Prepare for deep threats.</b> By level 40, speed, healing, escape tools, and resistance coverage are mandatory. Avoid fighting emperor liches in open ground.</li>");
        s.AppendLine("<li><b>Hunt the Balrog.</b> Enter level 49 or 50 only with endgame equipment, direct damage, and a safe combat space. The Balrog resists many conventional emergency tools.</li>");
        s.AppendLine("<li><b>Return to town.</b> Killing the Balrog wins only when the character returns to the surface alive.</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        s.AppendLine("<li><b>Respect procedural generation.</b> There is no route through Moria to memorize. Use mapping, detection, and careful exploration to create your own reliable route.</li>");
        s.AppendLine("<li><b>Keep an escape plan.</b> Word of Recall is the most important safety resource. Carry scrolls or retain the spell whenever you descend beyond a comfortable depth.</li>");
        s.AppendLine("<li><b>Identify before relying on loot.</b> Unidentified equipment may be cursed or less useful than it appears. Town services and identify scrolls prevent expensive mistakes.</li>");
        s.AppendLine("<li><b>Protect against status effects.</b> Free action prevents paralysis and slow effects; see invisible removes a major tactical surprise; healing and cure poison remain useful throughout the game.</li>");
        s.AppendLine("<li><b>Match resistance to depth.</b> Cold, lightning, fire, and acid dragons arrive in sequence. Equip each resistance before treating that depth as a farming destination.</li>");
        s.AppendLine("<li><b>Use terrain.</b> Fight dangerous fast creatures around pillars and corridors instead of allowing several attacks in open rooms. Speed is a defensive stat.</li>");
        s.AppendLine("<li><b>Treat liches differently.</b> Emperor liches drain device charges for health. Do not feed them charged wands or staves; use movement, terrain, and direct damage.</li>");
        s.AppendLine("<li><b>Do not waste the endgame.</b> The Balrog cannot be slept, polymorphed, confused, genocided, or destroyed. Save your strongest conventional damage and consumables for a controlled fight.</li>");
        s.AppendLine("</ul>");
    }

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th scope=\"row\">{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1100px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #e8e8e8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; vertical-align: top; }
        table.facts th { width: 160px; white-space: nowrap; }
        table.ref { font-size: 0.9em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
