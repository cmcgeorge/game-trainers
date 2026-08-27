using System.IO;
using TheQuestTrainer.Adventures;
using TheQuestTrainer.Cluebooks;
using TheQuestTrainer.Game;
using TheQuestTrainer.ViewModels;

namespace TheQuestTrainer.FormatCheck;

/// <summary>
/// The offline half of the harness: the adventure reader and the cluebook it feeds.
///
/// Nothing here needs a game, a process or a copyrighted file. The fixture in
/// <see cref="FakeAdventure"/> writes a whole synthetic world with its own <see cref="ArchiveWriter"/>,
/// so the reader is checked against bytes laid out by something other than itself — a fixture that
/// reused the reader's own arithmetic would agree with any alignment bug it happened to contain.
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// The archive's alignment, which is the load-bearing part of the whole adventure reader: a
    /// 16-bit read skips to an even offset and a 32-bit read to a multiple of four, while bytes and
    /// strings do not move at all. Several of these fail if that is dropped.
    /// </summary>
    private static void ArchiveChecks()
    {
        Section("record archive");

        var bytes = new ArchiveWriter().Byte(0x2A).Word(0x1234).Byte(7).Dword(0xDEADBEEF).ToExactRecord();
        Check("a word after a byte is written on an even offset", bytes.Length == 12);
        Check("the skipped byte is zero", bytes[1] == 0);

        var a = new RecordArchive(bytes);
        Check("the tag reads back", a.ReadByte() == 0x2A);
        Check("the word reads back over the padding", a.ReadUInt16() == 0x1234);
        Check("the byte after the word is unaligned", a.ReadByte() == 7 && a.Position == 5);
        Check("the dword skips to a multiple of four", a.ReadUInt32() == 0xDEADBEEF && a.Position == 12);
        Check("the record is fully consumed", a.ConsumedWithinPadding);

        // A reader that ignored alignment would land on 0x0012 rather than 0x1234, so this is the
        // counterfactual the alignment rule exists for.
        Check("unaligned reading would have given a different word", BitConverter.ToUInt16(bytes, 1) != 0x1234);

        var strings = new RecordArchive(new ArchiveWriter().Text("").Text("id").Text("name").ToExactRecord());
        Check("an empty string still costs its terminator", strings.ReadString().Length == 0 && strings.Position == 1);
        Check("strings follow one another unaligned", strings.ReadString() == "id" && strings.Position == 4);
        Check("the last string reads back", strings.ReadString() == "name");

        var overrun = new RecordArchive([1, 2, 3]);
        overrun.ReadByte();
        Check("a dword past the end throws rather than returning junk", Throws(() => overrun.ReadUInt32()));
        Check("an unterminated string throws", Throws(() => new RecordArchive([65, 66]).ReadString()));
        Check("a wrong tag throws", Throws(() => new RecordArchive([9]).ExpectTag(8, "thing")));

        // Trailing slack is only forgiven when it really is padding: content left behind means a
        // field was missed, and that has to be loud.
        var padded = new RecordArchive([1, 0, 0, 0, 0]);
        padded.ReadByte();
        Check("four zero bytes left over count as consumed", padded.ConsumedWithinPadding);

        var leftover = new RecordArchive([1, 0, 0, 9]);
        leftover.ReadByte();
        Check("a non-zero byte left over does not count as consumed", !leftover.ConsumedWithinPadding);

        var wide = new RecordArchive(new byte[RecordArchive.MaxTrailingPadding + 2]);
        wide.ReadByte();
        Check("more than a word of slack does not count as consumed", !wide.ConsumedWithinPadding);

        var blob = new RecordArchive(new ArchiveWriter().Blob([9, 8, 7]).ToExactRecord());
        Check("a blob reads its length then its bytes", blob.ReadBlob() is [9, 8, 7]);
    }

    /// <summary>The Palm container: the header, the record list, and what a malformed one does.</summary>
    private static void PalmDatabaseChecks()
    {
        Section("Palm database");

        var file = FakeAdventure.Build();
        var db = PalmDatabase.Parse(file, out string why);
        Check("the fixture parses as a Palm database", db is not null);
        if (db is null) return;

        Check("the name comes out of the header", db.Name == "TheQuestTest");
        Check("the type and creator identify a Quest world", db.IsQuestWorld);
        Check("the header record is the first one", db.Records[0].UniqueId == AdventureLayout.HeaderRecordId);
        Check("every record has a length", db.Records.All(r => r.Length > 0));
        Check("record lengths are the gaps between offsets",
            db.Records.Zip(db.Records.Skip(1)).All(p => p.First.Offset + p.First.Length == p.Second.Offset));
        Check("the last record runs to the end of the file",
            db.Records[^1].Offset + db.Records[^1].Length == file.Length);
        Check("record lengths are multiples of four", db.Records.All(r => r.Length % 4 == 0));

        Check("a truncated file is refused with a reason",
            PalmDatabase.Parse(file[..(file.Length / 2)], out string cut) is null && cut.Length > 0);
        Check("a file shorter than the header is refused", PalmDatabase.Parse(new byte[40], out _) is null);

        // A record list claiming more records than the file can hold must not be believed.
        var lying = (byte[])file.Clone();
        lying[76] = 0xFF;
        lying[77] = 0xFF;
        Check("an impossible record count is refused", PalmDatabase.Parse(lying, out _) is null);

        var notAWorld = new PalmDatabaseBuilder("Art", "ThQW", "Xxxx").Add(1, [0, 0, 0, 0]).Build();
        Check("a foreign creator is not a Quest world",
            PalmDatabase.Parse(notAWorld, out _)?.IsQuestWorld == false);
    }

    /// <summary>The world header, which decides the field set of everything after it.</summary>
    private static void AdventureHeaderChecks()
    {
        Section("adventure header");

        var adventure = FakeAdventure.Read(out _);

        Check("the world name comes out of the header", adventure.Name == "Testland");
        Check("the resource pack comes out of the header", adventure.Pack == "test");
        Check("the database name comes out of the header", adventure.Database == "TheQuestTest");
        Check("the grid prefix comes out of the header", adventure.GridPrefix == FakeAdventure.GridPrefix);
        Check("the grid size comes out of the header", adventure.GridWidth == 3 && adventure.GridHeight == 2);
        Check("the version comes out of the header, not a constant",
            adventure.FormatVersion == FakeAdventure.Version);

        var wrongMagic = new PalmDatabaseBuilder("Bad")
            .Add(AdventureLayout.HeaderRecordId, FakeAdventure.Header("X", "x", "X", "x_s", 1, 1, magic0: 0x00))
            .Build();
        Check("a header without the magic is refused",
            AdventureReader.Read(PalmDatabase.Parse(wrongMagic, out _)!, "x", out string magicWhy) is null
            && magicWhy.Length > 0);

        var ancient = new PalmDatabaseBuilder("Old")
            .Add(AdventureLayout.HeaderRecordId, FakeAdventure.Header("X", "x", "X", "x_s", 1, 1, version: 0x20))
            .Build();
        Check("a version older than the reader understands is refused, not guessed at",
            AdventureReader.Read(PalmDatabase.Parse(ancient, out _)!, "x", out _) is null);

        var noHeader = new PalmDatabaseBuilder("Empty").Add(1, [0, 0, 0, 0]).Build();
        Check("a database with no header record is refused",
            AdventureReader.Read(PalmDatabase.Parse(noHeader, out _)!, "x", out _) is null);
    }

    /// <summary>Everything the walk decodes, and the traps a tag-only reader falls into.</summary>
    private static void AdventureReaderChecks()
    {
        Section("adventure reader");

        var a = FakeAdventure.Read(out _);

        Check("no record was left undecoded", a.Warnings.Count == 0);

        Check("both quests are read in order",
            a.Quests.Count == 2 && At(a.Quests, 0)?.Id == "test_errand" && At(a.Quests, 1)?.Name == "A rescue");
        Check("a quest keeps its description", At(a.Quests, 0)?.Description == "Fetch the thing.");

        Check("both items are read", a.Items.Count == 2);
        var sword = a.Items.FirstOrDefault(i => i.Id == "test_sword");
        Check("an item's name and value survive", sword?.Name == "Test Sword" && sword.Value == 250);
        Check("an item's weight, damage and category are the panel's own fields",
            sword is { Weight: 900, DamageMin: 3, DamageMax: 9, Category: 1 });
        Check("an item's carried effect names its source",
            sword?.Effects.Count == 1 && At(sword.Effects, 0)?.SourceId == "test_effect");

        var shield = a.Items.FirstOrDefault(i => i.Id == "test_shield");
        Check("an item that casts a spell records which", shield?.SpellId == "test_spellward");
        Check("an item's armour is read", shield?.Armour == 7);

        Check("the spell is read with its numbers",
            a.Spells.Count == 1 && At(a.Spells, 0) is { Cost: 12, Difficulty: 34, Duration: 56 });
        Check("the monster keeps both its names and its health",
            a.Monsters.Count == 1 && At(a.Monsters, 0) is
                { Name: "Test Beast", PluralName: "Test Beasts", Health: 42 });
        Check("the person type is read", a.NpcTypes.Count == 1 && At(a.NpcTypes, 0)?.Name == "Townsfolk");
        Check("races, skills and attributes are read",
            a.Races.Count == 1 && a.Skills.Count == 1 && a.Attributes.Count == 2);
        Check("an attribute keeps its short form", At(a.Attributes, 0)?.Abbreviation == "Str");
        Check("the map object keeps its id and its text",
            a.MapObjects.Count == 1 && At(a.MapObjects, 0)?.Id == "test_sign" &&
            At(a.MapObjects, 0)!.Text.Any(t => t.Contains("beware the test", StringComparison.Ordinal)));

        var villager = a.People.FirstOrDefault(p => p.Id == "test_villager");
        Check("the person is read with their purse",
            a.People.Count == 1 && villager?.Name == "Villager" && villager.Gold == 120);
        Check("the shop stock is read",
            villager?.Stock.Count == 1 && At(villager.Stock, 0).First == "test_sword");

        var dialog = villager?.Dialog;
        Check("the conversation has both topics", dialog?.Topics.Count == 2);
        if (dialog is null || dialog.Topics.Count < 2) return;

        var referenced = dialog.Topics[0];
        Check("a referenced topic carries only its id", referenced.IsReference && !referenced.HasText);
        Check("the shared pool has the words", a.DialogPool.ContainsKey("test_shared"));
        Check("a referenced topic resolves against the pool", a.ResolveTopic(referenced).Topic == "About the town");
        Check("the resolved topic brings its reply",
            a.ResolveTopic(referenced).Replies.Count == 1 &&
            a.ResolveTopic(referenced).Replies[0].Text == "It is a fixture.");

        var own = dialog.Topics[1];
        Check("a topic that is not a reference carries its own words",
            !own.IsReference && own.Topic == "About the rescue" && own.Question == "Who needs rescuing?");
        Check("a reply names the ids it touches",
            own.Replies.Count == 1 && own.Replies[0].Symbols.SequenceEqual(new[] { "test_rescue" }));
        Check("a reply carries what the player may say back",
            own.Replies[0].Choices.Count == 1 && own.Replies[0].Choices[0].Text == "I will help.");

        Check("every map is read", a.Maps.Count == 3);
        if (a.Maps.Count != 3) return;

        var field = a.Maps.Single(m => m.Id == FakeAdventure.GridPrefix + "0101");
        Check("an outdoor id gives its one-based cell", field.Column == 1 && field.Row == 1);
        Check("an outdoor map is 21 tiles across", field.Tiles == MapLayout.GridMapTiles);
        Check("an outdoor map's origin follows from its cell", field.OriginX == 0 && field.OriginY == 0);

        var moor = a.Maps.Single(m => m.Id == FakeAdventure.GridPrefix + "0201");
        Check("the second cell's origin is one map east", moor.OriginX == MapLayout.GridMapTiles);
        Check("the flag word is carried through, not re-derived",
            (moor.Flags & MapLayout.FlagTeleportDenied) != 0 &&
            moor.Notes.Contains("Teleport", StringComparison.Ordinal));

        // An id ending in four digits that does not carry the grid prefix is an interior, not cell
        // 1, 2. Getting this wrong would drop a house into the middle of the world.
        var house = a.Maps.Single(m => m.Id == "test_house0102");
        Check("an id ending in digits is still an interior without the grid prefix",
            !house.IsOutdoorCell && house.Column is null);
        Check("an interior is 35 tiles across", house.Tiles == 35);

        Check("the reader looks for a placement list where the worlds keep one",
            AdventureReader.RecordsPerMap == FakeAdventure.MapStride &&
            AdventureReader.PlacementRecordOffset == FakeAdventure.PlacementOffset);
        Check("a map's placement list is found at its record id plus three",
            field.HasPlacements && field.ObjectIds.Contains("test_sign") &&
            field.ObjectIds.Contains("test_villager"));
        Check("a map with no placement record is bare, not merely unnamed", !moor.HasPlacements);
        Check("an interior gets its placements too", house.HasPlacements && house.ObjectIds.Count == 1);

        // The trap the ordered walk exists for: a per-map record whose first byte happens to be an
        // item's tag. A reader that went by tags alone would decode it and add a phantom item.
        Check("a per-map record starting with an item tag is not read as an item", a.Items.Count == 2);
    }

    /// <summary>The cluebook itself: chapters, dossiers, the plan and the two writers.</summary>
    private static void CluebookChecks()
    {
        Section("cluebook");

        var adventure = FakeAdventure.Read(out _);
        var book = Cluebook.Build(adventure);

        Check("a map with placements gets a chapter",
            book.Chapters.Any(c => c.Map.Id.EndsWith("0101", StringComparison.Ordinal)));
        Check("a map with none is listed as empty instead",
            book.EmptyMaps.Any(m => m.Id.EndsWith("0201", StringComparison.Ordinal)) &&
            book.Chapters.All(c => !c.Map.Id.EndsWith("0201", StringComparison.Ordinal)));

        var chapter = book.Chapters.FirstOrDefault(c => c.Map.Id.EndsWith("0101", StringComparison.Ordinal));
        Check("a chapter resolves a placed object against the catalog",
            chapter?.Objects.Count == 1 && At(chapter.Objects, 0)?.Id == "test_sign");
        Check("a chapter resolves a placed person against the cast",
            chapter?.People.Count == 1 && At(chapter.People, 0)?.Name == "Villager");

        var rescue = book.Quests.FirstOrDefault(d => d.Id == "test_rescue");
        Check("a quest is credited to the person whose reply names it",
            rescue?.Mentions.Any(m => m.Who == "Villager") == true);
        Check("a quest mention carries the line that was said",
            rescue?.Mentions.Any(m => m.What.Contains("cousin", StringComparison.Ordinal)) == true);

        Check("a quest named only through the shared pool is still credited",
            book.Quests.FirstOrDefault(d => d.Id == "test_errand")?.IsUsed == true);

        Check("an item on a shop's shelf is credited to the shop",
            book.Items.FirstOrDefault(d => d.Id == "test_sword")?.Mentions.Any(m => m.Kind == "Shop") == true);
        Check("a spell an item casts is credited to the item",
            book.Spells.FirstOrDefault(d => d.Id == "test_spellward")?
                .Mentions.Any(m => m.Kind == "Item" && m.Who == "Test Shield") == true);

        Check("the notes always say where this came from",
            book.Notes.Any(n => n.Contains("installed on this machine", StringComparison.Ordinal)));

        string plan = WorldPlan.Render(book);
        Check("the plan is an SVG element",
            plan.StartsWith("<svg", StringComparison.Ordinal) && plan.EndsWith("</svg>", StringComparison.Ordinal));
        Check("the plan names a place it has a map for", plan.Contains("Testfield", StringComparison.Ordinal));
        Check("the plan draws one square per grid cell, filled or not", CountOf(plan, "<rect") == 3 * 2);
        Check("angle brackets in a name cannot break the plan",
            WorldPlan.Escape("a<b>&\"c\"") == "a&lt;b&gt;&amp;&quot;c&quot;");

        string html = HtmlCluebookWriter.Write(book);
        Check("the HTML is a whole document",
            html.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal) &&
            html.TrimEnd().EndsWith("</html>", StringComparison.Ordinal));
        // The only URL a self-contained page may carry is the SVG namespace, which is an identifier
        // rather than something a browser goes and asks for.
        string withoutNamespace = html.Replace("http://www.w3.org/2000/svg", "", StringComparison.Ordinal);
        Check("the HTML fetches nothing and runs nothing",
            !withoutNamespace.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
            !withoutNamespace.Contains("https://", StringComparison.OrdinalIgnoreCase) &&
            !withoutNamespace.Contains("<script", StringComparison.OrdinalIgnoreCase) &&
            !withoutNamespace.Contains("<img", StringComparison.OrdinalIgnoreCase));
        Check("the HTML contains the world plan", html.Contains("<svg", StringComparison.Ordinal));
        Check("the HTML contains a conversation", html.Contains("Who needs rescuing?", StringComparison.Ordinal));

        string text = TextCluebookWriter.Write(book);
        Check("the text names the world", text.StartsWith("Testland", StringComparison.Ordinal));
        Check("the text carries the quests", text.Contains("A rescue", StringComparison.Ordinal));
        Check("the text carries the gazetteer", text.Contains("Testfield", StringComparison.Ordinal));
        Check("no line of the text runs away", text.Split('\n').All(l => l.TrimEnd().Length <= 120));

        var trimmed = Cluebook.Build(adventure, new CluebookOptions
        {
            IncludeItems = false,
            IncludeConversations = false,
            IncludeReference = false,
        });
        string small = HtmlCluebookWriter.Write(trimmed);
        Check("switching the item catalogue off drops it",
            !small.Contains("id=\"things\"", StringComparison.Ordinal));
        Check("switching conversations off drops them",
            !small.Contains("Who needs rescuing?", StringComparison.Ordinal));
        Check("the quests survive whatever else is switched off",
            small.Contains("A rescue", StringComparison.Ordinal));

        var everything = Cluebook.Build(adventure, new CluebookOptions { IncludeEmptyMaps = true });
        Check("asking for empty maps gives every map a chapter",
            everything.Chapters.Count == adventure.Maps.Count && everything.EmptyMaps.Count == 0);

        Check("a world name is reduced to something a file system takes",
            !CluebookViewModel.Sanitise("a/b:c*d").Any(c => Path.GetInvalidFileNameChars().Contains(c)));
        Check("a name that is only punctuation still gives a file name",
            CluebookViewModel.Sanitise("///").Length > 0);
    }

    /// <summary>
    /// The Cluebook tab's own logic, driven without a window: find the adventures in a folder, then
    /// write them.
    ///
    /// The folder is a real one under the temporary directory holding a real zip, because the whole
    /// point of <see cref="AdventureCatalog"/> is what it does with a pak, and a stub would check
    /// nothing. Everything it contains is the synthetic world; no game file is touched.
    /// </summary>
    private static void CluebookTabChecks()
    {
        Section("cluebook tab");

        string root = Path.Combine(Path.GetTempPath(), "TheQuestTrainer.FormatCheck." + Guid.NewGuid().ToString("N"));
        string game = Path.Combine(root, "game");
        string output = Path.Combine(root, "out");

        try
        {
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(Path.Combine(game, AdventureCatalog.ExpansionsFolder));

            WritePak(Path.Combine(game, "data.pak"), FakeAdventure.Build());
            WritePak(Path.Combine(game, AdventureCatalog.ExpansionsFolder, "extra.pak"), FakeAdventure.Build());

            // A pak holding a database that is not a world must not be offered as an adventure.
            WritePak(Path.Combine(game, "art.pak"),
                     new PalmDatabaseBuilder("TheQuestArt")
                         .Add(AdventureLayout.HeaderRecordId,
                              FakeAdventure.Header("The Quest Art", "tres", "TheQuestArt", "", 14, 14))
                         .Build());

            var vm = new CluebookViewModel { GameFolder = game, OutputFolder = output };
            vm.Find();

            Check("both paks are offered as adventures", vm.Adventures.Count == 2);
            Check("the base game is listed first, and says so",
                vm.Adventures.Count == 2 &&
                vm.Adventures[0].Display.Contains("base game", StringComparison.Ordinal) &&
                vm.Adventures[1].Display.Contains("extra.pak", StringComparison.Ordinal));
            Check("a resource database in a pak is not an adventure",
                vm.Adventures.All(r => r.Source.Database != "TheQuestArt"));
            Check("everything found is ticked to be written", vm.Adventures.All(r => r.IsSelected));

            vm.Adventures[1].IsSelected = false;
            vm.Write();

            string stem = Path.Combine(output, CluebookViewModel.Sanitise("Testland"));
            Check("the HTML cluebook is written", File.Exists(stem + ".html"));
            Check("the text cluebook is written beside it", File.Exists(stem + ".txt"));
            Check("the status says where they went",
                vm.Status.Contains(output, StringComparison.OrdinalIgnoreCase));
            Check("the row reports what the adventure holds",
                vm.Adventures[0].Detail.Contains("3 maps", StringComparison.Ordinal));
            Check("an adventure that was not ticked is not written",
                vm.Adventures[1].Detail == "Not read yet.");

            Check("the text is written with a byte-order mark so any editor reads it right",
                File.ReadAllBytes(stem + ".txt") is [0xEF, 0xBB, 0xBF, ..]);

            // A folder with nothing in it must say so rather than throw.
            var empty = new CluebookViewModel { GameFolder = Path.Combine(root, "nothing") };
            empty.Find();
            Check("a folder that is not an installation is reported, not thrown at",
                empty.Adventures.Count == 0 && empty.Status.Length > 0);
        }
        catch (IOException)
        {
            Check("the cluebook tab could use a temporary folder", false);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Writes a one-entry pak: a zip with the database where the game keeps one.</summary>
    private static void WritePak(string path, byte[] database)
    {
        using var zip = new System.IO.Compression.ZipArchive(File.Create(path),
                                                             System.IO.Compression.ZipArchiveMode.Create);
        var entry = zip.CreateEntry("pdbs/TheQuestTest.pdb");
        using var stream = entry.Open();
        stream.Write(database);
    }

    /// <summary>The element at <paramref name="index"/>, or null when the list is shorter.</summary>
    private static T? At<T>(IReadOnlyList<T> list, int index) => index < list.Count ? list[index] : default;

    private static int CountOf(string haystack, string needle)
    {
        int n = 0;
        for (int at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (ArchiveException) { return true; }
    }
}
