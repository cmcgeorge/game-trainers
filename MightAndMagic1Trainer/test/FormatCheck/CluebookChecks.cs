using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using MightAndMagic1Trainer.Cluebooks;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.FormatCheck;

/// <summary>
/// The cluebook: the overlay reader, the plans, and the two writers.
///
/// <para><b>No game file is needed and none is used.</b> The overlay reader is exercised against
/// overlays this file builds byte by byte to the format spec in <c>docs/ovr-format.md</c>, which is
/// the only way to check the cases that matter — a file whose dispatch tables are not the documented
/// shape, one whose header is from another build, one whose table bytes happen to decode as a
/// printable word — since the shipped 55 are all well-formed and none of them are ours to ship.</para>
///
/// <para>The plans are checked by round-trip: the bundled maze grids are parsed into a
/// <see cref="MazeMap"/> and rendered back out, and all 55 have to come back character for
/// character. That single assertion covers the whole coordinate system — north up, y counting from
/// the south, shared edges given to both squares — which is otherwise the sort of thing that is
/// wrong by one row and looks plausible.</para>
/// </summary>
internal static class CluebookChecks
{
    public static void Run(Action<string, bool> check)
    {
        ArgumentNullException.ThrowIfNull(check);

        OverlayChecks(check);
        PlanChecks(check);
        RuleChecks(check);
        BookChecks(check);
        WriterChecks(check);
    }

    // ---- the overlay format -------------------------------------------------------------------

    private static void OverlayChecks(Action<string, bool> check)
    {
        Console.WriteLine("\nOverlay (.ovr) reader:");

        string[] planted =
        {
            "A SIGN ABOVE THE DOOR READS:\rTHE INN OF NOWHERE",
            "STAIRS GOING DOWN! TAKE THEM (Y/N)?",
            "ETCHED IN GOLD, MESSAGE 1 READS:\rONE-TWO-THREE",
        };

        var bytes = FakeOverlay(planted, events: 7);
        var overlay = Overlay.TryRead(bytes, "nowhere", "Nowhere.ovr", out string why);

        check("a well-formed overlay reads", overlay is not null);
        if (overlay is null)
        {
            Console.WriteLine($"    {why}");
            return;
        }

        check("the sections account for the whole file",
            Overlay.HeaderSize + overlay.CodeSize + overlay.DataSize == overlay.FileSize);
        check("data_addr is code_size + 0xF43E",
            overlay.DataAddress == (ushort)(overlay.CodeSize + Overlay.CodeLoadAddress));
        check("the text was found by the dispatch-table arithmetic",
            overlay.TextStart == OverlayTextStart.DispatchTable);
        check("the event count is read from the data section's first byte", overlay.EventCount == 7);
        check("the event ids are carried, not interpreted", overlay.EventIds.Count == 7);
        check("no note was raised for a clean file", overlay.Notes.Count == 0);

        check($"all {planted.Length} messages are recovered and no table bytes with them",
            overlay.Messages.Count == planted.Length);
        check("a message keeps the game's own window breaks",
            overlay.Messages[0].Lines.Count == 2 &&
            overlay.Messages[0].Lines[1] == "THE INN OF NOWHERE");
        check("the lines are not re-flowed into one",
            overlay.Messages[0].SearchText == "A SIGN ABOVE THE DOOR READS: THE INN OF NOWHERE");
        check("a cipher fragment is found by its marker",
            overlay.Find("ETCHED IN GOLD")?.Lines[1] == "ONE-TWO-THREE");

        // The format notes warn that the id/pointer tables sometimes decode as a short printable run
        // (8XVZ, GUTZ4:, ;.JBR). Those are not text, and a reader that took them would put nonsense
        // at the top of a location's chapter — where a reader would believe it.
        var junked = FakeOverlay(planted, events: 7, tableFill: "GUTZ4:8XVZ;.JBR"u8.ToArray());
        var withJunk = Overlay.TryRead(junked, "nowhere", "Nowhere.ovr", out _);
        check("printable table bytes are not read as a message",
            withJunk is not null && withJunk.Messages.Count == planted.Length &&
            withJunk.Messages[0].Lines[0].StartsWith("A SIGN", StringComparison.Ordinal));

        // A file whose dispatcher is not the shape the one disassembled file showed must still give
        // up its text — and must say that it had to go looking, so the cluebook can carry the caveat.
        var oddball = FakeOverlay(planted, events: 7, countByte: 3);
        var found = Overlay.TryRead(oddball, "nowhere", "Nowhere.ovr", out _);
        check("a file whose tables are not the documented size still yields its text",
            found is not null && found.Messages.Count == planted.Length);
        check("...and says the text was found by searching",
            found is not null && found.TextStart == OverlayTextStart.FirstPhrase &&
            found.Notes.Any(n => n.Contains("first phrase", StringComparison.Ordinal)));
        check("...and does not pretend to know the event ids",
            found is not null && found.EventCount == 0 && found.EventIds.Count == 0);

        // The header is a check, not decoration: anything that fails it is not this game's overlay,
        // and reading its data section as text would produce confident rubbish.
        var wrongSignature = (byte[])bytes.Clone();
        wrongSignature[0] ^= 0xFF;
        check("a file without the signature is refused",
            Overlay.TryRead(wrongSignature, "nowhere", "x.ovr", out _) is null);

        var overrun = (byte[])bytes.Clone();
        overrun[0x08] = 0xFF; overrun[0x09] = 0xFF;
        check("a file whose sections overrun it is refused",
            Overlay.TryRead(overrun, "nowhere", "x.ovr", out _) is null);

        var dirtyTail = bytes.Concat(new byte[] { 0, 0, 0x41 }).ToArray();
        check("a file with something after its two sections is refused",
            Overlay.TryRead(dirtyTail, "nowhere", "x.ovr", out _) is null);

        var otherBuild = (byte[])bytes.Clone();
        otherBuild[0x0C] ^= 0x11;
        var read = Overlay.TryRead(otherBuild, "nowhere", "x.ovr", out _);
        check("a wrong data_addr is read but noted, not refused",
            read is not null && read.Notes.Any(n => n.Contains("data_addr", StringComparison.Ordinal)));

        check("a file too short to hold a header is refused",
            Overlay.TryRead(new byte[8], "nowhere", "x.ovr", out _) is null);

        // A set lines up with the maze records by name; nothing else is asked of the file system.
        string folder = TempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Sorpigal.ovr"), FakeOverlay(planted, 7));
            File.WriteAllBytes(Path.Combine(folder, "Notmine.ovr"), new byte[64]);
            var set = OverlaySet.Load(folder, new[] { "sorpigal", "portsmit", "notmine" });

            check("an overlay is matched to its map whatever the file's case", set.For("sorpigal") is not null);
            check("a location with no file is simply absent", set.For("portsmit") is null);
            check("a file that is not an overlay is reported rather than skipped silently",
                set.Count == 1 && set.Problems.Count == 1);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---- the plans ----------------------------------------------------------------------------

    private static void PlanChecks(Action<string, bool> check)
    {
        Console.WriteLine("\nMaze plans:");

        var mazes = MazeData.BuiltIn();
        var wrong = new List<string>();

        for (int i = 0; i < mazes.Maps.Count; i++)
        {
            string[] expected = BuiltInMazes.Records[i];
            string[] actual = MazePlan.RenderAscii(mazes.Maps[i]);
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal)) wrong.Add(mazes.Maps[i].RawName);
        }

        check($"all {mazes.Maps.Count} bundled mazes render back character for character" +
              (wrong.Count == 0 ? "" : " — " + string.Join(", ", wrong.Take(5))), wrong.Count == 0);

        var sorpigal = mazes.Maps[0];
        var stats = sorpigal.Counts();
        check("a plan counts each shared edge once, not twice",
            stats.Walls + stats.Doors + stats.Special + stats.Illusory <= 2 * MazePlan.Size * (MazePlan.Size + 1));
        check("the starting town has walls and doors", stats.Walls > 0 && stats.Doors > 0);
        check("somewhere in the game a drawn wall is walkable",
            mazes.Maps.Any(m => m.Counts().Illusory > 0));

        // A marker goes in the middle of its square, which the grid leaves blank -- so a mark can
        // never cover a wall, and the round-trip above still has to hold with none asked for.
        var marked = MazePlan.RenderAscii(sorpigal, new[] { new PlanMarker(0, 0, "7", "the south-west corner") });
        check("a marker lands in its own square, counting y from the south",
            marked[^2][1] == '7' && marked.Length == 33);
        check("...and changes nothing else about the plan",
            marked.Where((_, i) => i != marked.Length - 2)
                  .SequenceEqual(BuiltInMazes.Records[0].Where((_, i) => i != marked.Length - 2), StringComparer.Ordinal));
        check("a marker outside the grid is dropped, not clamped onto an edge",
            MazePlan.RenderAscii(sorpigal, new[] { new PlanMarker(-1, 99, "9", "nowhere") })
                    .SequenceEqual(BuiltInMazes.Records[0], StringComparer.Ordinal));

        // The secret passages are the one annotation computed rather than quoted, so they have to
        // agree with what the plan draws: one entry per illusory edge, each naming a real square.
        var passages = sorpigal.SecretPassages();
        check("every illusory edge is listed exactly once",
            passages.Count == stats.Illusory);
        check("a listed passage names a square on the map and a direction out of it",
            passages.All(p => p.X is >= 0 and < MazePlan.Size && p.Y is >= 0 and < MazePlan.Size &&
                              sorpigal.Face(p.X, p.Y, p.Dir) == EdgeFace.Illusory));
        check("every map's passages line up with its own count",
            mazes.Maps.All(m => m.SecretPassages().Count == m.Counts().Illusory));

        string svg = MazePlan.RenderSvg(sorpigal, 30);
        check("a plan is one <svg> with a path per edge style",
            svg.StartsWith("<svg", StringComparison.Ordinal) && svg.Contains("class=\"mp-wall\"", StringComparison.Ordinal));
        check("the plan's stylesheet can be hoisted out of it",
            !MazePlan.RenderSvg(sorpigal, 30, includeStyle: false).Contains("<style", StringComparison.Ordinal));

        // A machine with a comma decimal separator must not emit d="M 22,5 …". SvgCanvas.Number is
        // what prevents that; this is the call site that would defeat it by formatting by hand.
        var was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            // Without the stylesheet (whose font stack has commas of its own) the only commas a plan
            // could contain are decimal separators that got through.
            string german = MazePlan.RenderSvg(sorpigal, 25, includeStyle: false);
            check("plan coordinates are invariant whatever the machine's culture is",
                german.Contains("34.5", StringComparison.Ordinal) &&
                !german.Contains(",", StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    // ---- the rules ----------------------------------------------------------------------------

    private static void RuleChecks(Action<string, bool> check)
    {
        Console.WriteLine("\nRules reference:");

        check("one hit die per class", RulesBook.HitDice.Count == ClassBook.Classes.Count);
        check("the Knight rolls the biggest die and the Sorcerer the smallest",
            RulesBook.HitDice.Max(d => d.Die) == 12 && RulesBook.HitDice.Min(d => d.Die) == 6);

        check("Endurance 40 is worth +10 hit points a level", RulesBook.EnduranceBonus(40) == 10);
        check("Endurance 13 is the first point that pays", RulesBook.EnduranceBonus(13) == 1 && RulesBook.EnduranceBonus(12) == 0);
        check("Endurance below 9 costs hit points", RulesBook.EnduranceBonus(8) == -1 && RulesBook.EnduranceBonus(4) == -3);

        bool monotonic = true;
        for (int e = 1; e < 60; e++) monotonic &= RulesBook.EnduranceBonus(e) >= RulesBook.EnduranceBonus(e - 1);
        check("the Endurance bonus never goes backwards", monotonic);

        check("every rule says how well it is known and where it came from",
            RulesBook.Rules.All(r => r.Confidence.Length > 0 && r.Source.Length > 0));
    }

    // ---- the book -----------------------------------------------------------------------------

    private static void BookChecks(Action<string, bool> check)
    {
        Console.WriteLine("\nCluebook:");

        var bundled = Cluebook.Build(CluebookSources.Bundled(), new CluebookOptions());

        check($"a cluebook is written with no game files at all, and covers all {MazeData.MapCount} places",
            bundled.Chapters.Count == MazeData.MapCount);
        check("every maze record is identified as a place",
            bundled.Chapters.All(c => c.Place is not null));
        check("every place lands in a chapter of the gazetteer",
            PlaceBook.KindOrder.Sum(k => bundled.Of(k).Count()) == bundled.Chapters.Count);
        check("a place says how firmly it is identified",
            bundled.Chapters.All(c => c.Confidence is "Confirmed" or "Inferred" or "Uncertain"));
        check("with no installation there is no location text",
            bundled.MessageCount == 0 && !bundled.HasEventText);
        check("the notes say the walls are a transcription, not the game's own bytes",
            bundled.Notes.Any(n => n.Contains("transcription", StringComparison.Ordinal)));
        check("the notes say where the missing text would come from",
            bundled.Notes.Any(n => n.Contains(".ovr", StringComparison.Ordinal)));
        // Indoors and outdoors are the same bytes and different meanings, and the rule has one home.
        check("the twenty surface areas know they are outdoors, and nothing else claims to be",
            bundled.Chapters.Count(c => c.Maze.IsOutdoor) == 20 &&
            bundled.Chapters.Where(c => c.Maze.IsOutdoor).All(c => c.Kind == PlaceKind.Overworld));

        check("every landmark sits on a square of a place the book has a chapter for",
            LandmarkBook.Landmarks.All(l => l.X is >= 0 and < MazePlan.Size && l.Y is >= 0 and < MazePlan.Size &&
                                            bundled.Chapters.Any(c => c.RawName == l.RawName)));
        check("every landmark says where its coordinate came from",
            LandmarkBook.Landmarks.All(l => l.Source.Length > 0 && l.Description.Length > 0));
        check("a place's marks are numbered from one, in order",
            bundled.Chapters.Where(c => c.Landmarks.Count > 0)
                   .All(c => c.Markers.Select(m => m.Label).SequenceEqual(
                       Enumerable.Range(1, c.Landmarks.Count).Select(i => i.ToString()))));
        // The two halves of the annotation check each other: a landmark described as being behind a
        // secret wall should land on a square the maze data finds one on. Both of the ones that are
        // so described do, and neither does when the published coordinate is mirrored top to bottom
        // -- which is the only evidence in the project either way about which end the game counts y
        // from. If this ever fails, the landmark and the maze data have stopped agreeing; find out
        // which moved before changing the coordinate.
        var leprechaun = bundled.Chapters.Single(c => c.RawName == "sorpigal");
        var secretRoom = bundled.Chapters.Single(c => c.RawName == "portsmit");
        check("the leprechaun's square has the walk-through wall its description implies",
            leprechaun.WayInAt(11, 3).Count > 0 && leprechaun.WayInAt(11, 15 - 3).Count == 0);
        check("Portsmith's secret room likewise",
            secretRoom.WayInAt(12, 2).Count > 0 && secretRoom.WayInAt(12, 15 - 2).Count == 0);
        check("outdoors nothing is corroborated, because a walk-through edge there is scenery",
            bundled.Of(PlaceKind.Overworld).All(c => c.WayInAt(5, 7).Count == 0));

        check("the notes say the marks are quoted and the passages computed",
            bundled.Notes.Any(n => n.Contains("not squares this project decoded", StringComparison.Ordinal)));
        check("both ciphers are laid out even when their text is not to hand",
            bundled.Gold.Count == 9 && bundled.Silver.Count == 6 &&
            bundled.Gold.All(g => g.Message is null));

        // A fake installation: the exact maze bytes and a handful of overlays, so the half of the
        // book that needs the player's own files can be exercised without any of them.
        string folder = TempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Mazedata.dta"), SyntheticMazedata());
            File.WriteAllBytes(Path.Combine(folder, "Sorpigal.ovr"),
                FakeOverlay(new[] { "THE INN OF SORPIGAL", "STAIRS GOING DOWN! TAKE THEM (Y/N)?" }, 5));
            File.WriteAllBytes(Path.Combine(folder, "Qvl1.ovr"),
                FakeOverlay(new[] { "ETCHED IN GOLD, MESSAGE 1 READS:\rCOMPLETION-MUST-EACH" }, 2));
            File.WriteAllBytes(Path.Combine(folder, "Doom.ovr"),
                FakeOverlay(new[] { "ETCHED IN SILVER, MESSAGE D READS:\r//SV/21;-22R" }, 2));

            var sources = CluebookSources.FromFolder(folder, out string detail);
            var book = Cluebook.Build(sources, new CluebookOptions());

            check("a game folder is read for both its walls and its words",
                book.MazesAreExact && book.LocationsWithText == 3);
            check("what was found is reported back", detail.Contains("3 of 55", StringComparison.Ordinal));
            check("a location's chapter carries its own messages",
                book.Chapters.Single(c => c.RawName == "sorpigal").Messages.Count == 2);
            check("a gold fragment is collected from the file that holds it",
                book.Gold[0].Message?.Lines[1] == "COMPLETION-MUST-EACH");
            check("a silver fragment likewise",
                book.Silver.Single(f => f.Fragment.Label == "Silver D").Message is not null);
            check("the notes change to say the text came from the player's own files",
                book.Notes.Any(n => n.Contains("your own installation", StringComparison.Ordinal)));

            // Asking for a book without the text must leave the text out of every part of it.
            var quiet = Cluebook.Build(sources, new CluebookOptions { IncludeEventText = false });
            check("turning the location text off turns it off everywhere",
                quiet.MessageCount == 0 && quiet.Gold.All(g => g.Message is null));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---- the writers --------------------------------------------------------------------------

    private static void WriterChecks(Action<string, bool> check)
    {
        Console.WriteLine("\nCluebook writers:");

        var book = Cluebook.Build(CluebookSources.Bundled(), new CluebookOptions());
        string html = HtmlCluebookWriter.Write(book);
        string text = TextCluebookWriter.Write(book);

        check("the page is self-contained: nothing fetched, nothing that executes",
            HtmlPage.IsSelfContained(html, out string why) || Fail(why));
        check("the page carries the plan stylesheet once, not 55 times",
            Count(html, ".mp-wall{") == 1);
        check("every place has a chapter in the page",
            book.Chapters.All(c => html.Contains($"id=\"p-{c.RawName}\"", StringComparison.Ordinal)));
        check("every place has a chapter in the plain text",
            book.Chapters.All(c => text.Contains($"[{c.RawName}]", StringComparison.Ordinal)));
        check("the reference tables are all there",
            html.Contains("id=\"items\"", StringComparison.Ordinal) &&
            html.Contains("id=\"bestiary\"", StringComparison.Ordinal) &&
            html.Contains("id=\"spells\"", StringComparison.Ordinal) &&
            Count(html, "<tr>") > ItemBook.Catalog.Count + MonsterBook.Bestiary.Count);
        check("the page marks its landmarks and lists them under the plan",
            html.Contains("class=\"mp-mark\"", StringComparison.Ordinal) &&
            html.Contains("The leprechaun", StringComparison.Ordinal) &&
            Count(html, "<ol class=\"marks\">") == book.Chapters.Count(c => c.Landmarks.Count > 0));
        check("the page lists the walls that are not there, with a square to walk out of",
            html.Contains("Walls that are not there", StringComparison.Ordinal) &&
            Count(html, "class=\"secrets\"") == book.Chapters.Count(c => c.SecretPassages.Count > 0));
        // Outdoors a drawn-but-walkable edge is scrub, not a secret. The surface maps have up to 257
        // of them; listing those would bury a town's thirty real ones and teach the reader to skip
        // the list, so they are counted and explained instead.
        check("an outdoor map's walk-through edges are explained as terrain, not listed as secrets",
            book.Of(PlaceKind.Overworld).All(c => c.PassagesAreTerrain) &&
            book.Of(PlaceKind.Town).All(c => !c.PassagesAreTerrain) &&
            html.Contains("Outdoors that is terrain rather than a secret", StringComparison.Ordinal));
        check("...and no plan lists more coordinates than a reader could act on",
            book.Chapters.Where(c => !c.PassagesAreTerrain).Max(c => c.SecretPassages.Count) <= 80);
        check("the plain text carries the same annotations",
            text.Contains("The leprechaun", StringComparison.Ordinal) &&
            text.Contains("Walls that are not there", StringComparison.Ordinal));
        check("the plain text draws its plans in the atlas's own characters",
            text.Contains("+#+", StringComparison.Ordinal) || text.Contains("+o+", StringComparison.Ordinal));

        var lines = text.Replace("\r\n", "\n").Split('\n');
        string? runaway = lines.FirstOrDefault(l => l.Length > 100);
        check("no line of the plain text runs away" + (runaway is null ? "" : $" — \"{runaway[..40]}…\""),
            runaway is null);

        // Options have to actually leave things out; a "small" book that is the same size is a lie.
        var minimal = Cluebook.Build(CluebookSources.Bundled(), new CluebookOptions
        {
            IncludePlans = false, IncludeItems = false, IncludeBestiary = false,
            IncludeSpells = false, IncludeWalkthrough = false, IncludeRules = false,
        });
        string small = HtmlCluebookWriter.Write(minimal);
        check("leaving sections out leaves them out",
            !small.Contains("id=\"items\"", StringComparison.Ordinal) &&
            !small.Contains("<svg", StringComparison.Ordinal) &&
            small.Length < html.Length / 4);

        // The game's own words are arbitrary bytes out of somebody else's files. A quote or an angle
        // bracket in one must come out as text, not as markup — this is the one place in the whole
        // document where untrusted content reaches the page.
        string folder = TempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "Sorpigal.ovr"),
                FakeOverlay(new[] { "<script>alert(\"pwned\")</script> & SO ON" }, 2));
            var hostile = Cluebook.Build(CluebookSources.FromFolder(folder, out _), new CluebookOptions());
            string page = HtmlCluebookWriter.Write(hostile);

            check("a message that is markup comes out inert",
                !page.Contains("<script>", StringComparison.Ordinal) &&
                page.Contains("&lt;script&gt;", StringComparison.Ordinal) &&
                page.Contains("&quot;pwned&quot;", StringComparison.Ordinal) &&
                page.Contains("&amp; SO ON", StringComparison.Ordinal));

            // And the same text in the plain-text copy is left exactly as the game holds it.
            check("the plain text leaves a message exactly as the file holds it",
                TextCluebookWriter.Write(hostile).Contains("<script>alert(\"pwned\")</script>", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        // End to end, into a real folder, because that is what the tab does.
        string output = TempFolder();
        try
        {
            string page = Path.Combine(output, "cluebook.html");
            File.WriteAllText(page, html);
            File.WriteAllText(Path.Combine(output, "cluebook.txt"), text);
            check("the written page is a whole document",
                File.ReadAllText(page).StartsWith("<!DOCTYPE html>", StringComparison.Ordinal) &&
                new FileInfo(page).Length > 200_000);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }

        static bool Fail(string why)
        {
            Console.WriteLine($"    {why}");
            return false;
        }
    }

    // ---- fixtures -----------------------------------------------------------------------------

    /// <summary>
    /// Builds an overlay byte for byte to <c>docs/ovr-format.md</c>: the 14-byte header, the shared
    /// init stub, and a data section of dispatch tables followed by null-terminated strings.
    ///
    /// <para>The knobs are the two things a real file could differ in and a reader could get wrong:
    /// <paramref name="countByte"/> writes an event count that does not match the tables actually
    /// present, which is what sends the reader down its fallback, and <paramref name="tableFill"/>
    /// plants printable bytes in the tables, which is what tempts it to read them as text.</para>
    /// </summary>
    private static byte[] FakeOverlay(IReadOnlyList<string> messages, int events,
                                      byte[]? tableFill = null, int? countByte = null)
    {
        // The stub every overlay opens with, verbatim from the format notes, then filler.
        byte[] stub =
        [
            0xB8, 0x8E, 0xF4, 0xA3, 0x34, 0x01, 0xB8, 0x40, 0xC9, 0xA3, 0x32, 0x01,
            0xC6, 0x06, 0xA6, 0x0D, 0x00, 0xC3,
        ];
        var code = new List<byte>(stub);
        while (code.Count < 64) code.Add(0x90);

        var data = new List<byte> { (byte)(countByte ?? events) };
        var table = new List<byte>();
        for (int i = 0; i < events; i++) table.Add((byte)(0x20 + i));         // event ids
        for (int i = 0; i < events; i++) table.Add(0x01);                     // per-event flag masks
        for (int i = 0; i < events; i++) { table.Add(0x8F); table.Add(0xC9); } // handler pointers
        if (tableFill is not null)
            for (int i = 0; i < tableFill.Length && i < table.Count; i++) table[i] = tableFill[i];
        data.AddRange(table);

        foreach (string message in messages)
        {
            data.AddRange(Encoding.ASCII.GetBytes(message));
            data.Add(0);
        }

        var file = new List<byte>();
        Add16(file, Overlay.Signature0);
        Add16(file, Overlay.Signature1);
        Add16(file, (ushort)code.Count);
        Add16(file, Overlay.ResidentBase);
        Add16(file, (ushort)data.Count);
        Add16(file, 0);                                                       // data_size is a dword
        Add16(file, (ushort)(code.Count + Overlay.CodeLoadAddress));
        file.AddRange(code);
        file.AddRange(data);
        return file.ToArray();

        static void Add16(List<byte> to, ushort value)
        {
            to.Add((byte)value);
            to.Add((byte)(value >> 8));
        }
    }

    /// <summary>A stand-in <c>Mazedata.dta</c>: 55 records of unrelated bytes, which is all the exact
    /// path needs to be exercised without shipping the game's own file.</summary>
    private static byte[] SyntheticMazedata()
    {
        var bytes = new byte[MazeData.FileSize];
        for (int i = 0; i < MazeData.MapCount; i++)
            for (int k = 0; k < MazeData.RecordSize; k++)
            {
                uint h = (uint)(i * 0x9E3779B1 + k * 0x85EBCA6B);
                h ^= h >> 15;
                h *= 0x2545F491;
                h ^= h >> 13;
                bytes[i * MazeData.RecordSize + k] = (byte)h;
            }
        return bytes;
    }

    private static string TempFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "mm1-cluebook-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0;
        for (int at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
