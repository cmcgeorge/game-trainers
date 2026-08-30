using System.Text;
using GameTrainers.Common.Documents;
using KnightsOfLegendTrainer.Game;

namespace KnightsOfLegendTrainer.Cluebooks;

public static class HtmlCluebookWriter
{
    public const string Title = "Knights of Legend — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var markup = new StringBuilder();
        markup.AppendLine($"<h1>{E(GameFacts.GameTitle)}</h1>");
        markup.AppendLine("<p class=\"lede\">A reference for the 1989 Origin Systems tactical fantasy RPG, covering Ashtalarea, character options, equipment, magic, and all 24 quests.</p>");
        Contents(markup, cluebook.Options);
        Overview(markup);
        if (cluebook.Options.IncludeMaps) Maps(markup, cluebook);
        if (cluebook.Options.IncludeReferences) References(markup);
        if (cluebook.Options.IncludeQuests) Quests(markup);
        if (cluebook.Options.IncludeWalkthrough) Walkthrough(markup);
        if (cluebook.Options.IncludeStrategy) Strategy(markup);
        return new HtmlPage(Title).Style(Style).Append(markup.ToString()).ToHtml();
    }

    private static void Contents(StringBuilder markup, CluebookOptions options)
    {
        markup.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        markup.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        if (options.IncludeMaps) markup.AppendLine("<li><a href=\"#maps\">Area maps</a></li>");
        if (options.IncludeReferences) markup.AppendLine("<li><a href=\"#references\">Characters, equipment, and magic</a></li>");
        if (options.IncludeQuests) markup.AppendLine("<li><a href=\"#quests\">Quest list</a></li>");
        if (options.IncludeWalkthrough) markup.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (options.IncludeStrategy) markup.AppendLine("<li><a href=\"#strategy\">Strategy notes</a></li>");
        markup.AppendLine("</ol></nav>");
    }

    private static void Overview(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"overview\">The game at a glance</h2><table class=\"ref\">");
        Row(markup, "Setting", $"{GameFacts.Setting}, kingdom of {GameFacts.Kingdom}");
        Row(markup, "Starting town", GameFacts.StartingTown);
        Row(markup, "Party", $"Up to {GameFacts.MaxPartySize} characters");
        Row(markup, "Attributes", string.Join(", ", CharacterFormat.PrimaryStatNames) + $" (maximum {GameFacts.MaxStatistic})");
        Row(markup, "Classes", GameFacts.ClassCount.ToString());
        Row(markup, "Magic", $"{GameFacts.MagicOrderCount} orders, {SpellBook.Spells.Count} spells");
        Row(markup, "Quests", GameFacts.QuestCount.ToString());
        Row(markup, "Training", $"{GameFacts.TrainingCost} {GameFacts.Currency} and {GameFacts.TrainingApCost} {GameFacts.Experience} per skill level");
        markup.AppendLine("</table><p>Build a balanced party, gather training and equipment, then work through the quest chain. Each quest requires speaking to its giver, using a required keyword where listed, reaching the target, and defeating or finding the objective.</p>");
    }

    private static void Maps(StringBuilder markup, Cluebook cluebook)
    {
        markup.AppendLine("<h2 id=\"maps\">Area maps</h2><p>These are schematic reference maps, not a reconstruction of the game world. North is at the top. Markers identify known services and quest landmarks.</p>");
        markup.AppendLine("<p><b>S</b> Start · <b>T</b> town gate/service · <b>C</b> castle · <b>D</b> dungeon · <b>I</b> item · <b>N</b> NPC · <b>E</b> enemy · <b>A</b> arena · <b>G</b> guild/training</p>");
        foreach (var area in cluebook.Areas)
        {
            markup.AppendLine($"<h3>{E(area.Name)}</h3><p>{E(area.Description)}</p>");
            markup.AppendLine(AreaSvg(area, cluebook.Options.MapCellSize));
            markup.AppendLine("<table class=\"ref\"><tr><th>Position</th><th>Landmark</th><th>Notes</th></tr>");
            foreach (var poi in area.Pois)
                markup.AppendLine($"<tr><td>({poi.X}, {poi.Y})</td><td>{E(poi.Name)}</td><td>{E(poi.Description)}</td></tr>");
            markup.AppendLine("</table>");
        }
    }

    private static string AreaSvg(AreaLevel area, int cell)
    {
        const int pad = 20;
        var svg = SvgCanvas.Responsive(pad * 2 + cell * area.Width, pad * 2 + cell * area.Height, $"{area.Name} schematic map");
        svg.Rect(0, 0, pad * 2 + cell * area.Width, pad * 2 + cell * area.Height, ("fill", "#14151A"));
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
                svg.Rect(pad + x * cell, pad + y * cell, cell, cell, ("fill", area.Grid[x, y] == CellKind.Wall ? "#3A3D4A" : "#1E1F26"));
        foreach (var poi in area.Pois)
        {
            var (fill, label) = PoiStyle(poi.Name);
            double x = pad + poi.X * cell;
            double y = pad + poi.Y * cell;
            svg.Rect(x, y, cell, cell, ("fill", fill));
            svg.Text(x + cell / 2.0, y + cell * 0.7, label, ("text-anchor", "middle"), ("font-family", "monospace"), ("font-size", cell * 0.55), ("fill", "#14151A"));
        }
        return svg.ToSvg();
    }

    private static (string fill, string label) PoiStyle(string name) => name switch
    {
        "Start" => ("#B070E0", "S"),
        "Trading Post" or "Town Gate" or "Lock Gate" or "Barrier Gate" => ("#6FC276", "T"),
        "Fortress of Brettle" or "Tower Keep" or "Lord Norgan's Keep" or "Krag Keep" or "Assembly Building" => ("#799BD7", "C"),
        "Forest Dungeon" or "Ghor Dungeon" => ("#A070B8", "D"),
        "Quest Item" or "Seggallion's Trail" => ("#C89B3C", "I"),
        "Quest Givers" or "Quest Contacts" or "Fistan Stockhard" or "Monvin the Elder" or "Ballaster" => ("#5FB9C8", "N"),
        "Cyclops Patrol" or "Enemy Guard" => ("#C86262", "E"),
        "Arena" => ("#D0824C", "A"),
        _ when name.Contains("Guild", StringComparison.Ordinal) || name.Contains("Training", StringComparison.Ordinal) => ("#65B8A0", "G"),
        _ => ("#E0E2E8", "?"),
    };

    private static void References(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"references\">Characters, equipment, and magic</h2><h3>Races</h3><table class=\"ref\"><tr><th>Race</th><th>Description</th><th>Notes</th></tr>");
        foreach (var race in RaceBook.Races) markup.AppendLine($"<tr><td>{E(race.Name)}</td><td>{E(race.Description)}</td><td>{E(race.Notes)}</td></tr>");
        markup.AppendLine("</table><h3>Classes</h3><table class=\"ref\"><tr><th>Class</th><th>Race</th><th>Gender</th><th>Level</th><th>Notes</th></tr>");
        foreach (var entry in ClassBook.Classes) markup.AppendLine($"<tr><td>{E(entry.Name)}</td><td>{E(entry.Race)}</td><td>{E(entry.Gender)}</td><td>{entry.Level}</td><td>{E(entry.Notes)}</td></tr>");
        markup.AppendLine("</table><h3>Weapons</h3><table class=\"ref\"><tr><th>Weapon</th><th>Master</th><th>Location</th><th>Notes</th></tr>");
        foreach (var weapon in WeaponBook.Weapons) markup.AppendLine($"<tr><td>{E(weapon.Name)}</td><td>{E(weapon.Master)}</td><td>{E(weapon.Location)}</td><td>{E(weapon.Notes)}</td></tr>");
        markup.AppendLine("</table><h3>Armor</h3><table class=\"ref\"><tr><th>Armor</th><th>Category</th><th>Notes</th></tr>");
        foreach (var armor in ArmorBook.Armor) markup.AppendLine($"<tr><td>{E(armor.Name)}</td><td>{E(armor.Category)}</td><td>{E(armor.Notes)}</td></tr>");
        markup.AppendLine("</table><h3>Spells</h3><table class=\"ref\"><tr><th>Spell</th><th>Order</th><th>Effect</th></tr>");
        foreach (var spell in SpellBook.Spells) markup.AppendLine($"<tr><td>{E(spell.Name)}</td><td>{E(spell.Order)}</td><td>{E(spell.Description)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Quests(StringBuilder markup)
    {
        markup.AppendLine("<h2 id=\"quests\">Quest list</h2><table class=\"ref\"><tr><th>#</th><th>Quest</th><th>Giver</th><th>Location</th><th>Keyword</th><th>Target</th><th>Reward</th></tr>");
        foreach (var quest in QuestBook.Quests) markup.AppendLine($"<tr><td>{quest.Id + 1}</td><td>{E(quest.Name)}</td><td>{E(quest.QuestGiver)}</td><td>{E(quest.Location)}</td><td>{E(quest.Keyword)}</td><td>{E(quest.TargetLocation)}</td><td>{E(quest.Reward)}</td></tr>");
        markup.AppendLine("</table>");
    }

    private static void Walkthrough(StringBuilder markup) => markup.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2><ol><li>Start in Brettle, assemble a six-character party, and buy core armor and weapons.</li><li>Speak with the Brettle quest givers and complete the first four quests around Tantowyn, Klvar Wood, and the southern road.</li><li>Visit Htron and Tegal Forest for training, the Blue Gem order, and the next quest objectives.</li><li>Learn basic spells from multiple orders before committing a character to one order.</li><li>Continue through Poitle Lock, Thimblewald, Olanthen, and Shellernoon in quest order.</li><li>Collect the Truth Sword, Flying Cloak, Courage Coat, Great Shield, and other key rewards before the late game.</li><li>Complete every preceding quest, then speak to Dundle at the Olanthen Barrier Assembly Building.</li><li>Travel to Ghor Hills, defeat the Cyclops, and rescue Seggallion.</li></ol>");

    private static void Strategy(StringBuilder markup) => markup.AppendLine("<h2 id=\"strategy\">Strategy notes</h2><ul><li><b>Balance the party.</b> Combine durable melee fighters, ranged specialists, and magic users.</li><li><b>Train broadly.</b> Weapons can break; proficiency in several weapons prevents a single failure from disabling a character.</li><li><b>Learn before joining.</b> A magic order fixes the race component, so collect basic spells first.</li><li><b>Manage encumbrance.</b> Heavy armor reduces Quickness and combat movement.</li><li><b>Use formations.</b> Tactical combat rewards protecting vulnerable spellcasters behind stronger fighters.</li><li><b>Focus regenerators.</b> Trolls and cliff trolls regenerate, so concentrate attacks instead of spreading damage.</li><li><b>Prepare for special fights.</b> Wear the Courage Coat against giants, trolls, and Cyclops; split the party for sledge creatures.</li><li><b>Keep a quest order.</b> Later quest contacts and areas are unlocked by the earlier chain.</li></ul>");

    private static void Row(StringBuilder markup, string label, string value) => markup.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.8em; border-bottom: 2px solid #444; padding-bottom: .3em; } h2 { margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: .2em; } h3 { margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; } .toc { background: #f5f5f5; border: 1px solid #ddd; padding: 1em 1.5em; border-radius: 4px; }
        table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; } table.ref th, table.ref td { padding: 4px 8px; border: 1px solid #ccc; text-align: left; vertical-align: top; } table.ref th { background: #e8e8e8; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; }
        """;
}
