using System.Text;
using GameTrainers.Common.Documents;
using LegendOfGrimrock1Trainer.Game;

namespace LegendOfGrimrock1Trainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Legend of Grimrock — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        s.AppendLine("<h1>Legend of Grimrock</h1>");
        s.AppendLine("<p class=\"lede\">A reference for Almost Human's 2012 first-person dungeon crawler: Mount Grimrock's thirteen levels, character building, magic, equipment, monsters, and survival.</p>");
        Contents(s, cluebook.Options);
        if (cluebook.Options.IncludeOverview) Overview(s);
        if (cluebook.Options.IncludeDungeon) Dungeon(s, cluebook.Levels);
        if (cluebook.Options.IncludeCharacters) Characters(s);
        if (cluebook.Options.IncludeSpells) Spells(s);
        if (cluebook.Options.IncludeSkills) Skills(s);
        if (cluebook.Options.IncludeEquipment) Equipment(s);
        if (cluebook.Options.IncludeBestiary) Bestiary(s);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(s);
        if (cluebook.Options.IncludeStrategy) Strategy(s);
        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder s, CluebookOptions options)
    {
        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        if (options.IncludeOverview) Link(s, "overview", "The game at a glance");
        if (options.IncludeDungeon) Link(s, "dungeon", "The thirteen levels");
        if (options.IncludeCharacters) Link(s, "characters", "Races and classes");
        if (options.IncludeSpells) Link(s, "spells", "Spells and runes");
        if (options.IncludeSkills) Link(s, "skills", "Skill ladders");
        if (options.IncludeEquipment) Link(s, "equipment", "Weapons and armour");
        if (options.IncludeBestiary) Link(s, "bestiary", "Bestiary");
        if (options.IncludeWalkthrough) Link(s, "walkthrough", "Walkthrough");
        if (options.IncludeStrategy) Link(s, "strategy", "Strategy notes");
        s.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"ref\"><tr><th>Game</th><td>Legend of Grimrock</td></tr>");
        Row(s, "Developer", "Almost Human");
        Row(s, "Year", "2012");
        Row(s, "Party", $"{GameFacts.PartySize} champions, two front and two back");
        Row(s, "Dungeon", $"Mount Grimrock — {GameFacts.CampaignLevels} levels");
        Row(s, "Character model", "4 races, 4 classes, 12 tracked stats, 18 conditions, and 17 skills");
        s.AppendLine("</table>");
        s.AppendLine("<p>Four prisoners descend through Mount Grimrock seeking a way out. Combat happens in real time on a grid: the front row attacks in melee while both rows can throw, shoot, and cast. Careful movement, party formation, food, light, and puzzle-solving matter as much as raw damage.</p>");
    }

    private static void Dungeon(StringBuilder s, IReadOnlyList<DungeonLevelInfo> levels)
    {
        s.AppendLine("<h2 id=\"dungeon\">The thirteen levels</h2><p>Every campaign level is a 32×32 tile map. Use the trainer's Dungeon tab to inspect the loaded level, move within it, and reveal its automap.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>#</th><th>Level</th><th>What to expect</th></tr>");
        foreach (var level in levels)
            s.AppendLine($"<tr><td>{level.Number}</td><td>{E(level.Name)}</td><td>{E(level.Description)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Characters(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"characters\">Races and classes</h2><h3>Races</h3>");
        Table(s, new[] { "Race", "Notes" }, new[]
        {
            new[] { "Human", "Balanced attributes and four starting skill points." },
            new[] { "Minotaur", "Strong, durable front-liner; Head Hunter grants +3 Attack Power per skull carried." },
            new[] { "Insectoid", "Natural Armor grants +5 Protection; well suited to a durable build." },
            new[] { "Ratling", "An agile, nimble option suited to evasive or ranged builds." },
        });
        s.AppendLine("<h3>Classes</h3>");
        Table(s, new[] { "Class", "Role" }, new[]
        {
            new[] { "Fighter", "Front-row specialist for weapon skills, armour, and sustained melee damage." },
            new[] { "Rogue", "Evasion, ranged weapons, throwing, daggers, and assassination techniques." },
            new[] { "Mage", "Spellcraft and elemental magic. Best protected from the back row." },
            new[] { "Alchemist", "Potion and utility specialist; supports the party with consumables." },
        });
        s.AppendLine("<p>Two front-line fighters, a back-row rogue, and a mage is a reliable first-party shape. Skilled is a strong trait on any champion because its three extra skill points reach important milestones early.</p>");
    }

    private static void Spells(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"spells\">Spells and runes</h2><p>Click the required runes in any order, then cast. A spell also requires the listed skill level and its scroll. The board reads left to right, top to bottom.</p>");
        s.AppendLine(RuneBoard());
        s.AppendLine("<table class=\"ref\"><tr><th>Spell</th><th>Skill</th><th>Level</th><th>Runes</th><th>Energy</th></tr>");
        foreach (var spell in GameTables.Spells.Where(spell => spell.ManaCost > 0))
            s.AppendLine($"<tr><td>{E(spell.UiName)}</td><td>{E(GameTables.SkillUiNames[spell.Skill])}</td><td>{spell.SkillLevel}</td><td><code>{E(SpacedRunes(spell.Runes))}</code></td><td>{spell.ManaCost}</td></tr>");
        s.AppendLine("</table><p><b>Light</b> (Spellcraft 5, B E) saves torches. <b>Poison Cloud</b> is efficient corridor control, while elemental shields protect the whole party against their respective damage type.</p>");
    }

    private static void Skills(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"skills\">Skill ladders</h2><p>One skill point is earned per level. Skill levels run from 0 to 50; each skill's milestone talents are more valuable than spreading points between several weapon types.</p>");
        Table(s, new[] { "Skill", "Key milestones", "Level 50" }, new[]
        {
            new[] { "Athletics", "Endurance 10: food consumption −25%; Porter 20: +15 kg capacity", "Iron Body: Health +100" },
            new[] { "Armors", "Light Armor Proficiency 8; Heavy Armor Proficiency 16; Shield Expert 25", "Armor Master: Protection +25" },
            new[] { "Swords", "Slash 10; Parry 16; Thrust 23; Flurry 33", "Sword Master: double attack speed" },
            new[] { "Axes", "Chop 10; Cleave 22; Rampage 33", "Axe Master: Attack Power +20" },
            new[] { "Maces", "Bash 10; Crushing Blow 20; Devastating Blow 33", "Mace Master: attacks ignore armour" },
            new[] { "Daggers", "Stab 10; Piercing Strike 22; Flurry 33", "Death Strike" },
            new[] { "Unarmed Combat", "Fist Fighter 8; Bear Trap 20; Blazing Strike 33", "Unarmed Master: Attack Power +20" },
            new[] { "Assassination", "Backstab 8; Reach Attack 12; Improved Critical 31", "Master Assassin" },
            new[] { "Missile Weapons", "Quick Shot 12; Improved Quick Shot 24; Volley 32", "Master Archer: double critical chance" },
            new[] { "Throwing Weapons", "Quick Throw 12; Improved Quick Throw 24; Double Throw 32", "Throwing Master: double critical chance" },
            new[] { "Dodge", "Stealth 11; Improved Stealth 24", "Ninja Master: Evasion +50" },
            new[] { "Staff Defense", "Light Armor Proficiency 14", "Staff Master: Protection +10, Evasion +30" },
            new[] { "Spellcraft", "Light and Darkness 5; Combat Caster 10; Improved Combat Caster 18", "Archmage: spells cost half energy" },
            new[] { "Fire Magic", "Fireburst 2; Fireball 13; Fire Shield 16; Circle of Protection 32", "Fire Mastery: +100 Fire resistance" },
            new[] { "Air Magic", "Shock 4; Lightning Bolt 14; Invisibility 19; Shock Shield 22; Circle of Protection 32", "Air Mastery: +100 Shock resistance" },
            new[] { "Ice Magic", "Ice Shards 3; Frostbolt 13; Frost Shield 19; Circle of Protection 32", "Ice Mastery: +100 Cold resistance" },
            new[] { "Earth Magic", "Poison Cloud 3; Poison Bolt 7; Poison Shield 13; Circle of Protection 32", "Earth Mastery: +100 Poison resistance" },
        });
    }

    private static void Equipment(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"equipment\">Weapons and armour</h2><p>Attack power alone is misleading: divide it by cool-down to compare damage over time.</p><h3>Selected weapons</h3>");
        Table(s, new[] { "Weapon", "Attack", "Cool-down", "Weight" }, new[]
        {
            new[] { "Dismantler", "27", "4.0 s", "4.8 kg" }, new[] { "Cutlass", "19", "3.3 s", "3.5 kg" },
            new[] { "Ancient Axe", "36", "6.3 s", "7.3 kg" }, new[] { "Ogre Hammer", "36", "6.0 s", "13.0 kg" },
            new[] { "Assassin Dagger", "15", "2.8 s", "1.0 kg" }, new[] { "Longbow", "19", "4.5 s", "1.0 kg" },
            new[] { "Crossbow", "20", "5.5 s", "1.5 kg" }, new[] { "Throwing Axe", "15", "5.5 s", "0.5 kg" },
        });
        s.AppendLine("<h3>Armour sets</h3>");
        Table(s, new[] { "Set", "Protection", "Notes" }, new[]
        {
            new[] { "Valor", "15 per piece", "Best protection set." }, new[] { "Plate", "12 per piece", "Heavy; needs Armors 16." },
            new[] { "Chitin", "9 per piece", "Mask, mail, greaves, boots." }, new[] { "Ring", "6 per piece", "Reliable mid-game set." },
            new[] { "Leather", "4 per piece", "Light armour." }, new[] { "Lurker", "0", "Evasion +5 per piece; ideal for a rogue." },
        });
    }

    private static void Bestiary(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"bestiary\">Bestiary</h2><p>Exact health, experience, protection, and evasion values from the game's monster archetypes.</p>");
        Table(s, new[] { "Monster", "Health", "XP", "Protection", "Advice" }, new[]
        {
            new[] { "Snail", "90", "60", "—", "Slow; practise sidestepping." }, new[] { "Scavenger", "100", "75", "—", "Fast but fragile." },
            new[] { "Skeleton Warrior", "120", "90", "5", "Maces are effective." }, new[] { "Spider", "160", "175", "—", "Prepare poison resistance." },
            new[] { "Green Slime", "450", "190", "—", "Slow and easy to evade." }, new[] { "Crab", "410", "450", "8", "Use doorways." },
            new[] { "Uggardian", "235", "500", "5", "Use cold damage and Fire Shield." }, new[] { "Ice Lizard", "650", "675", "5", "Use fire and Frost Shield." },
            new[] { "Ogre", "700", "750", "17", "Mace Master or Piercing Attack helps." }, new[] { "Warden", "1200", "750", "20", "Use every available resource." },
            new[] { "Goromorg", "400", "1000", "0", "Break its shield first." },
        });
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol>");
        foreach (var step in new[] { "Build a balanced four-person party with two front-liners, a ranged specialist, and a mage.", "On the early levels, learn the waltz: attack, sidestep, and attack again while enemies swing at the tile you left.", "Prioritise Athletics 10 for Endurance, Armors 8 for light armour proficiency, and Spellcraft 5 for Light.", "Use level 3 to prepare for puzzle-heavy Archives, then keep supplies for the traps on level 6.", "Use Fire Shield and cold damage against Uggardians on level 7; approach the Vault with lock and puzzle solutions in mind.", "Against Goromorgs, break defensive shields before spending your high-damage attacks.", "Before the Prison and the Cemetery, rest to full, carry food, and save consumables for the Warden and final encounters." })
            s.AppendLine($"<li>{E(step)}</li>");
        s.AppendLine("</ol>");
    }

    private static void Strategy(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul>");
        foreach (var note in new[] { "Do not fight hungry: starvation halves Attack Power and prevents healing while resting.", "Capacity is 3 × Strength kg, plus 15 kg from Porter. At 85% capacity the party becomes Burdened; over capacity it cannot move.", "Both back-row champions can throw, shoot, and cast. Rotate wounded front-liners backward without losing their contribution.", "Commit each attacker to a primary weapon skill so that important milestones arrive early.", "Use Light instead of consuming torches whenever possible. Darkness is useful for specific puzzles.", "The trainer's safest recovery action is Heal + restore energy, which writes each champion's own maximum. Prefer granting skill points and spending them in-game over directly setting a skill level, because the game applies the milestone rewards itself." })
            s.AppendLine($"<li>{E(note)}</li>");
        s.AppendLine("</ul>");
    }

    private static string RuneBoard()
    {
        const int cell = 42;
        const int pad = 4;
        var svg = SvgCanvas.Responsive(cell * 3 + pad * 2, cell * 3 + pad * 2, "Three by three spell rune board");
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
            {
                var left = pad + x * cell;
                var top = pad + y * cell;
                var rune = ((char)('A' + y * 3 + x)).ToString();
                svg.Rect(left, top, cell, cell, ("fill", "#25334a"));
                svg.Text(left + cell / 2.0, top + cell * 0.65, rune, ("text-anchor", "middle"), ("fill", "#ffffff"), ("font-size", "20"));
            }
        return svg.ToSvg();
    }

    private static void Table(StringBuilder s, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        s.AppendLine("<table class=\"ref\"><tr>");
        foreach (var header in headers) s.Append($"<th scope=\"col\">{E(header)}</th>");
        s.AppendLine("</tr>");
        foreach (var row in rows)
        {
            s.AppendLine("<tr>");
            foreach (var value in row) s.Append($"<td>{E(value)}</td>");
            s.AppendLine("</tr>");
        }
        s.AppendLine("</table>");
    }

    private static void Link(StringBuilder s, string id, string text) => s.AppendLine($"<li><a href=\"#{id}\">{E(text)}</a></li>");
    private static void Row(StringBuilder s, string label, string value) => s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);
    private static string SpacedRunes(string runes) => string.Join(' ', runes);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 960px; margin: 2em auto; padding: 0 1em; color: #20242c; line-height: 1.6; }
        h1 { border-bottom: 2px solid #405675; padding-bottom: .3em; } h2 { margin-top: 2em; border-bottom: 1px solid #9cacbf; padding-bottom: .2em; }
        h3 { margin-top: 1.5em; } .lede { color: #526070; font-style: italic; } .toc { background: #eef2f7; border: 1px solid #c4d0de; border-radius: 4px; padding: 1em 1.5em; }
        .toc ol { padding-left: 1.5em; } table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; } th, td { border: 1px solid #c4d0de; padding: 4px 8px; text-align: left; vertical-align: top; }
        th { background: #e5ebf2; } code { font-family: Consolas, monospace; } svg { display: block; margin: 1em auto; max-width: 100%; height: auto; }
        """;
}
