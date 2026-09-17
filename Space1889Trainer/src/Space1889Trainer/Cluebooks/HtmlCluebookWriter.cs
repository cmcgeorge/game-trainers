using System.Text;
using Space1889Trainer.Files;
using Space1889Trainer.Game;

namespace Space1889Trainer.Cluebooks;

/// <summary>Writes a <see cref="Cluebook"/> as one self-contained HTML page.</summary>
public static class HtmlCluebookWriter
{
    public const string Title = "Space: 1889 — cluebook";

    public static string Write(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);
        var s = new StringBuilder();
        var o = cluebook.Options;

        s.AppendLine("<h1>Space: 1889</h1>");
        s.AppendLine("<p class=\"lede\">A field guide to Paragon Software's 1990 Victorian science-fiction RPG: travel by ether flyer from London to Mars, Venus, Mercury and Luna, follow the trail of Tutankhamen's star map, and reach the hidden Saurians of Inner Earth.</p>");

        s.AppendLine("<nav class=\"toc\"><h2>Contents</h2><ol>");
        s.AppendLine("<li><a href=\"#overview\">The game at a glance</a></li>");
        s.AppendLine("<li><a href=\"#rules\">How the game really works</a></li>");
        if (o.IncludeControls) s.AppendLine("<li><a href=\"#controls\">Controls</a></li>");
        if (o.IncludeWalkthrough) s.AppendLine("<li><a href=\"#walkthrough\">Walkthrough</a></li>");
        if (o.IncludeSideQuests) s.AppendLine("<li><a href=\"#side\">Side quests, digs and locked doors</a></li>");
        if (o.IncludeItems) s.AppendLine("<li><a href=\"#items\">Item catalogue</a></li>");
        if (o.IncludeMaps) s.AppendLine("<li><a href=\"#maps\">Maps</a></li>");
        s.AppendLine("</ol></nav>");

        Overview(s);
        Rules(s);
        if (o.IncludeControls) Controls(s);
        if (o.IncludeWalkthrough) Walkthrough(s);
        if (o.IncludeSideQuests) SideQuests(s);
        if (o.IncludeItems) Items(s);
        if (o.IncludeMaps) Maps(s, cluebook.Maps);

        return new HtmlPage(Title).Style(Style).Append(s.ToString()).ToHtml();
    }

    // --- sections --------------------------------------------------------------------------------

    private static void Overview(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"overview\">The game at a glance</h2>");
        s.AppendLine("<table class=\"facts\">");
        Row(s, "Title", "Space: 1889");
        Row(s, "Developer / publisher", "Paragon Software, under licence from Game Designers' Workshop (1990)");
        Row(s, "Setting", "1889: Victorian adventurers with steam-age ether flyers explore Luna, Mercury, Mars and Venus");
        Row(s, "Party", "Five characters: generated in CG.EXE or the default party (Prof. Wells, Lady Marie, Elizabeth, Sydney Webb, Sir Walter)");
        Row(s, "Objective", "Follow the clues from Tutankhamen's tomb to Europa, then find the Saurians' entrance to Inner Earth at the North Pole");
        Row(s, "Money", "Pounds, shillings and pence: 12d = 1s, 240d = £1");
        Row(s, "Copy protection", "One word from the manual (page, paragraph, line, word) before the main menu");
        s.AppendLine("</table>");
    }

    private static void Rules(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"rules\">How the game really works</h2>");
        s.AppendLine("<p class=\"hint\">Read out of the game's code rather than the manual.</p>");
        s.AppendLine("<ul>");
        Li(s, "Health is STR + END. The status panel's \"HEALTH: a/b\" is current health over the knock-out line, max − ⌈(STR+END)/2⌉; at or below it the character is out of action.");
        Li(s, "Fatigue rises on a failed (END+1)d6 roll each game day. Carrying too much, travelling on Mars (+1) or Venus (+2), and running out of food make it likelier; a horse, rough-living clothing on Mars and foul-weather clothing on Venus make it less likely. Fatigue equal to STR, AGI or END knocks the character out. Rest at an inn or with a camping outfit.");
        Li(s, "Food is one party-wide stock: each able character eats 2 a day. The market sells it at 8d a unit up to 32,767. It weighs nothing, so buy thousands.");
        Li(s, "Every 30 days each character's monthly income (a thirtieth of their starting fortune) is paid into the party account. Draw it out at a bank.");
        Li(s, "Game time runs in real time on every map (one day per six steps on a planet's surface map). GAME → PAUSE stops the clock.");
        Li(s, "MENTAL is insanity: in a fight a character with MENTAL m misbehaves on a d6 roll below m. The madness potion adds 2.");
        Li(s, "There is no experience. Characters improve only through NPCs who teach a skill in return for an item.");
        Li(s, "Dark maps need a MINERS HAT, LANTERN or ELECTRIC LAMP marked in use with USE. Items that are worn or carried in use (clothing, water breathers, lamps) must be USEd too.");
        Li(s, "Items dropped on the ground stay where they fall (up to 100 per planet), so you can store things.");
        s.AppendLine("</ul>");
    }

    private static void Controls(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"controls\">Controls</h2>");
        s.AppendLine("<p>Every command is an icon with a highlighted letter: press the letter. Arrow keys move the party (Home, End, PgUp and PgDn move diagonally). Esc backs out of menus.</p>");
        s.AppendLine("<table class=\"ref\"><tr><th>Key</th><th>Command</th><th>Use</th></tr>");
        foreach (var (k, c, u) in new[]
                 {
                     ("T", "TAKE", "Pick up the brown bag (item) you are facing or standing on"),
                     ("D", "DROP", "Put an item down — also how the Teotihuacan tablets are placed on their altars"),
                     ("Q", "QUERY", "Talk to the NPC in front of the leader: TALK, BUY (INFO / OBJECT), SELL, LEAVE"),
                     ("C", "CURE", "A healer treats a patient; Medicine and a doctor career decide how well"),
                     ("I", "ITEMS", "Look through the leader's inventory"),
                     ("U", "USE", "Equip a weapon or armour, light a lamp, wear clothing, dig with a shovel, light dynamite, open chests with lockpicks"),
                     ("V", "VIEW", "Describe the square the leader faces (search coffins, altars and chests with it)"),
                     ("S", "STUDY", "Read an item: maps, reports and diaries carry the clues"),
                     ("L", "LEAD", "Change the party leader — most checks use the leader's skills"),
                     ("P", "PARTY", "Character sheets; Left/Right change character, Down shows weight, fatigue, food, mental and the 21 item slots (G gives an item)"),
                     ("F", "FIGHT", "Start ground combat (only the controlled character moves freely)"),
                     ("R", "ROB", "Pick the pocket of the NPC in front (Stealth and Crime)"),
                     ("H", "HUNT", "The leader tracks the nearest creature (Tracking)"),
                     ("G", "GAME", "SAVE, LOAD, PAUSE, SOUND, QUIT"),
                 })
            s.AppendLine($"<tr><td>{E(k)}</td><td>{E(c)}</td><td>{E(u)}</td></tr>");
        s.AppendLine("</table>");
        s.AppendLine("<p>Ground combat: N gives new orders, A attacks, R reloads, W changes weapon, B blocks, F flees. Space navigation: C plots a COURSE with the leader's Science, then fly with the arrows. Ship combat: A assigns bridge stations, L links to a crippled ship, B boards it, arrows manoeuvre, Enter fires.</p>");
    }

    private static void Walkthrough(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"walkthrough\">Walkthrough</h2>");
        s.AppendLine("<p class=\"hint\">Map ids are planet×100 + area×10 + map; positions are (row, column), as the Maps tab and the maps below show them.</p>");
        Steps(s, "Earth: London, New York and San Francisco", new[]
        {
            "Shop in London first: firearms and ammunition (weapon shop), lockpicks, shovels, rope, a lantern or miner's hat, dynamite, a doctor's bag, Conklin's Atlas and navigation instruments (pawn shop), and thousands of days of food (market). USE weapons and armour to equip them.",
            "Claus von Schmelling wanders London's streets: buy the LONDON REPORT from him for £2,000 (or ROB him). STUDY it: it names Hans Ogleby at the New York inn.",
            "Buy the FEVER SERUM from Doctor Maxwell Raven at the London inn (£50).",
            "Rent a zeppelin or boat at a harbor and cross to New York. Give the report to Hans Ogleby at the inn for LETTERS OF INTRODUCTION.",
            "Walk or ride west to San Francisco and give the letters to Nathaniel Johanssan in the pub: he gives you the MAPS OF EGYPT (\"8 paces north from stairs, 1 pace west\").",
        });
        Steps(s, "Earth: Egypt, Tutankhamen's tomb", new[]
        {
            "In Egypt take the stairs to the false tomb, blow the walls with dynamite (let your best Engineering character light it) and kill the Germans: Ansgaar Wuenschell drops a PAPER (\"14 paces directly south from the Eye of the Desert\") and a KEY.",
            "Back on the Egypt map, find the Eye of the Desert, walk 14 paces south to row 37, column 49 and USE a shovel: the tomb stairs open.",
            "On the lower level (Egypt map 7) dig 8 paces north and 1 west of the stairs (row 9, column 9) to open the burial chamber, then VIEW the sarcophagus: STONE TABLET and TUTS TREASURES (worth £10,000 — the price of your first ether flyer).",
            "In the Egypt museum use the KEY on the upstairs door. Give Mary Kingsley the fever serum for a message for Alfred C. Hobbs, and take the Mycenean gold mask for Schliemann in London.",
        });
        Steps(s, "Earth: Teotihuacan, Atlantis and Angkor", new[]
        {
            "Take Mary's message to Alfred C. Hobbs in New York (the Crystal Palace): he gives HOBBS LOCKPICKS, which open the pyramid doors of Teotihuacan map 4.",
            "In Teotihuacan buy a water breather for everyone at the alchemist. Take the TABLET OF MORTALS and TABLET OF SANCTUARY and DROP them on the two altars of map 3 (row 13, columns 17 and 21). \"You hear a rumbling\": the west pyramid's inner doors open, and the Atlantis cave entrance appears at row 35, column 76.",
            "Take the Atlantis map from the Inca guard's room, go to the new south-east entrance (dynamite the barrier if it is blocked), have everyone USE their water breather, and swim to Atlantis. VIEW the Red Captain's remains: FREEMERCHANTS ID, FREEMERCHANTS DIARY and SCROLLS OF THE ANCIENTS.",
            "In Angkor VIEW the great altar (map 8) with the scrolls in the party: the secrets lie \"in the bowels of the sacred companion of the Red Cyclops\" — Mars.",
            "Build an ether flyer at an ether port (UPDATE FLYER; hydrogen lift only on Earth). Sell the Tut treasures to pay for it. Lead with your best Science character carrying the Atlas and instruments, plot a COURSE and fly to Mars.",
        });
        Steps(s, "Mars and Venus: the German conspiracy", new[]
        {
            "Walk north from the Mars ether port to Ausonia. In the cave below, rescue Zoho Winiimolaak: he gives a Mars travel pass and tells you to steal German uniforms.",
            "Fly to Venus (firearms corrode there: keep spares unequipped). In Venusstadt's German warehouse bribe your way to the uniforms, then go to Fort Bismarck in the Thetis Mountains wearing them and kill Oberst Hans Kurt for the GERMAN HQ PASS.",
            "Back on Mars, rent a sand boat in Syrtis Major with the travel pass and reach the German headquarters. In uniform, climb to the third floor, open the door with the HQ pass and kill Baron von Gruber for the CASTLE KEY.",
            "In Boreo Syrtis the castle key opens King Hattabranx's rooms: kill him for the WORM CULT KEY.",
            "Find Teegok Quuglaani wandering in Moab for the WORM CULT MAP. Dig at Boreo Syrtis row 32, column 36 to open the cult's entrance, unlock its doors with the key, and on the lowest level give Kleuht Na Vriss the SCROLLS OF THE ANCIENTS. TAKE THE EMERALD he leaves.",
        });
        Steps(s, "Edison, Luna, Mercury and Europa", new[]
        {
            "Leaving Mars, the pirate ship Whisperdeath appears: disable it without destroying it, LINK and BOARD, fight to the cell and talk to Thomas Edison. He explains the voyage past the asteroid belt needs a new propeller, ammonia and a giant glow crystal; ether ports now take those as fuel.",
            "On Luna, carry THE EMERALD into the caves (their doors need it) and show it to Professor Vladimir Tereshkova: he gives Kleuht's PROPELLER.",
            "On Mercury find the giant GLOW CRYSTAL by the World River, and dig in the lowest mine of Princess Christiana (map 4) for AMMONIA.",
            "At an ether port fit the propeller (Saurian type) — the ammonia and glow crystal are loaded as fuel. Fly through the asteroid belt to Jupiter at the top right of the space chart (rows 2-4, columns 116-118): the Europa sequence plays.",
            "Return to Earth: the entrance to Inner Earth has appeared at the North Pole (row 2, column 52 of the Earth map). Descend and find Eoger Luirv among the Saurians for the ending.",
        });
    }

    private static void SideQuests(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"side\">Side quests, digs and locked doors</h2>");
        s.AppendLine("<h3>Rewards worth having</h3><ul>");
        foreach (var q in new[]
                 {
                     "Kill Jack the Ripper in London's alleys and take his scalpel to Inspector A. C. Doyle.",
                     "Silbury Hill (London maps 1, 4, 5; dark): dig 5 paces east of the Prophet's tomb on the lowest level (map 5, row 15, column 11) for the RUBY CHALICE — the Egypt museum curator pays £10,000.",
                     "Stonehenge: on a day divisible by 30, dig at London row 30, column 61 for the GOLD SHIELD (Sir Norman Lockyer on Venus tells you about it).",
                     "Heinrich Schliemann (London museum) pays £1,000 for the Mycenean gold mask from the Egypt museum.",
                     "Give Freemerchant's ID to Frederick Burnaby in Teotihuacan for his Medal of Honor; Herr Franz Wortmann (Venus) trades it for the chalice's location.",
                     "Johnny Wilson, curator on Mercury, pays for the Arrow of Kaundinya and the Garuda statue from Angkor and the sword of Uriah Tu.",
                     "Skill teachers: Dr. Vincent Buembats (New York, Medicine, for a doctor's bag), Guglielmo Marconi (Venus, Engineering, for a mineral detector), Buffalo Bill Cody (Venus, Marksmanship, for a Winchester), P. T. Barnum (Venus, Theatrics, for lockpicks), Robert Edwin Perry (Venus, Leadership, for a Remington rolling block), Grigori Rasputin (Stealth, for a Lebel rifle).",
                     "Martian unification: carry the bajuys Kai Urukta → Lopkan → Karkem Kubla → Ucuz Yuni → Photho Nhe for Observation (beware Tycuss Nhe).",
                     "Canal Keepers: Volaace Zeenkeer's bible to Glaar Skuguu for priest robes, then the AMULET OF SELDON (Miskiita Chkya pays £12,750).",
                     "Emilie Van Warren is held in the Lurkers' lair: dig at Syrtis Major map 1, row 18, column 23.",
                 })
            Li(s, q);
        s.AppendLine("</ul>");

        s.AppendLine("<h3>Scripted dig, drop and story spots</h3><table class=\"ref\"><tr><th>Map</th><th>Square</th><th>What happens</th></tr>");
        foreach (var spot in MapAnnotations.ScriptedSpots)
            s.AppendLine($"<tr><td>{spot.MapId}</td><td>row {spot.Row}, col {spot.Column}</td><td>{E(spot.Description)}</td></tr>");
        s.AppendLine("</table>");

        s.AppendLine("<h3>Doors that need an item</h3><table class=\"ref\"><tr><th>Map</th><th>Where</th><th>Carry</th></tr>");
        foreach (var (mapId, item, alt) in TileRules.LockedDoorMaps)
            s.AppendLine($"<tr><td>{mapId}</td><td>{E(PlaceBook.Describe(mapId / 100, mapId / 10 % 10, mapId % 10))}</td><td>{E(ItemBook.NameOf(item) + (alt != 0 ? " or " + ItemBook.NameOf(alt) : ""))}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Items(StringBuilder s)
    {
        s.AppendLine("<h2 id=\"items\">Item catalogue</h2>");
        s.AppendLine("<p class=\"hint\">From ITEMS.DAT. Prices are the base price before Bargaining; \"Shops\" lists the planets whose shops stock it.</p>");
        s.AppendLine("<table class=\"ref compact\"><tr><th>Id</th><th>Item</th><th>Type</th><th>Price</th><th>Weight (lb)</th><th>Shops</th></tr>");
        foreach (var item in ItemBook.Holdable)
            s.AppendLine($"<tr><td>{item.Id}</td><td>{E(item.Name)}</td><td>{E(item.TypeName)}</td><td>{E(item.PriceText)}</td><td>{item.WeightPounds:0.##}</td><td>{E(item.Shops)}</td></tr>");
        s.AppendLine("</table>");
    }

    private static void Maps(StringBuilder s, MapLibrary? library)
    {
        s.AppendLine("<h2 id=\"maps\">Maps</h2>");
        if (library is null)
        {
            s.AppendLine("<p>Maps are drawn from your own copy of the game: set the game folder in the trainer and save the cluebook again.</p>");
            return;
        }
        s.AppendLine("<p class=\"hint\">Schematics drawn from the game's map files: pale = walkable, dark = walls and scenery, blue = water, brown = doors, gold = exits, teal = shops, orange = entrances, purple = scripted dig/drop spots, pink = entrances a story event reveals, red dots = NPCs (hover for names).</p>");
        foreach (var entry in library.Maps.Where(e => e.File != "0.SYS" && e.Id is not (992 or 999) && (e.MapNumber == 0)))
        {
            if (library.Load(entry.Id) is not { } map) continue;
            var npcs = library.Npcs(entry.Id, entry.Planet, entry.Area);
            s.AppendLine($"<h3>{E(entry.Title)} <span class=\"hint\">(map {entry.Id}, {map.Columns} × {map.Rows})</span></h3>");
            s.AppendLine(SchematicSvg.Render(map, MapAnnotations.For(map, npcs), entry.Title));
        }
    }

    // --- helpers -----------------------------------------------------------------------------------

    private static void Steps(StringBuilder s, string heading, IEnumerable<string> steps)
    {
        s.AppendLine($"<h3>{E(heading)}</h3><ol>");
        foreach (var step in steps) Li(s, step);
        s.AppendLine("</ol>");
    }

    private static void Li(StringBuilder s, string text) => s.AppendLine($"<li>{E(text)}</li>");

    private static void Row(StringBuilder s, string label, string value) =>
        s.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");

    private static string E(string text) => HtmlPage.Escape(text);

    private const string Style = """
        body { font-family: Georgia, serif; max-width: 1000px; margin: 2em auto; padding: 0 1em; line-height: 1.6; color: #222; }
        h1 { font-size: 1.9em; border-bottom: 2px solid #6e5320; padding-bottom: 0.3em; }
        h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #999; padding-bottom: 0.2em; }
        h3 { font-size: 1.15em; margin-top: 1.5em; }
        .lede { font-style: italic; color: #555; }
        .hint { color: #555; font-size: 0.92em; }
        .toc { background: #f5f2ea; border: 1px solid #ddd3bd; padding: 1em 1.5em; border-radius: 4px; }
        .toc ol { padding-left: 1.5em; }
        table.facts, table.ref { border-collapse: collapse; width: 100%; margin: 1em 0; }
        table.facts th, table.ref th { background: #ece6d8; text-align: left; padding: 4px 8px; border: 1px solid #ccc; }
        table.facts td, table.ref td { padding: 4px 8px; border: 1px solid #ccc; }
        table.facts th { width: 180px; white-space: nowrap; }
        .compact { font-size: 0.88em; }
        svg { max-width: 100%; height: auto; display: block; margin: 1em 0; border: 1px solid #ddd; background: #1d2027; }
        """;
}
