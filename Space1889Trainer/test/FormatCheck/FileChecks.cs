using System.IO;
using System.Text;
using Space1889Trainer.Cluebooks;
using Space1889Trainer.Files;
using Space1889Trainer.Game;

namespace Space1889Trainer.FormatCheck;

internal static partial class Program
{
    // ---- synthetic files ------------------------------------------------------------------------

    private static void CheckSyntheticFiles()
    {
        Section("PIC decoding (synthetic)");
        // 8 x 2 picture: row 0 = colours 1,2 x4 ; row 1 = colour 15 x8.  Packed: copy 4 bytes (0x12 x4) then repeat 0xFF x4.
        var pic = new List<byte>();
        pic.AddRange(new byte[] { 2, 0, 1, 0 });                 // height 2, width/8 = 1
        pic.AddRange(new byte[16]);
        pic.AddRange(new byte[] { 8, 0 });                       // unpacked size
        var packed = new byte[] { 0x03, 0x12, 0x12, 0x12, 0x12, 0xFD, 0xFF };
        pic.AddRange(new byte[] { (byte)packed.Length, 0 });
        pic.AddRange(packed);
        var decoded = PicFile.Decode(pic.ToArray());
        Check(decoded is { Width: 8, Height: 2 }, "PIC size");
        if (decoded is not null)
        {
            Check(decoded.Pixels.Take(8).SequenceEqual(new byte[] { 1, 2, 1, 2, 1, 2, 1, 2 }), "PIC row 0 nibbles, high first");
            Check(decoded.Pixels.Skip(8).All(p => p == 15), "PIC PackBits run");
        }
        Check(PicFile.Decode(new byte[10]) is null, "short PIC rejected");
        CheckEqual(0xAA5500, PicFile.EgaPalette[6], "EGA brown");
        CheckEqual(3, PicFile.UnpackBits(new byte[] { 0x80, 0xFE, 0x41 }, 0, 3, 16).Length, "0x80 no-op then a 3-byte run");

        Section("map decoding (synthetic)");
        // A 1 x 1 screen map (12 rows x 20 cols = 240 cells): SPR tile 5, then (rep digit 7 → 10 copies) DEF tile 150, then 229 x DEF 0.
        var codes = new List<int> { 0xF0 + 5, 0x1E0 + 7, 150 };
        int remaining = 240 - 1 - 10;
        // 229 = rep + 3 → rep 226 = 7*31 + 9
        codes.AddRange(new[] { 0x1E0 + 7, 0x1E0 + 9, 0 });
        Check(remaining == 229, "synthetic map arithmetic");
        var bits = new List<bool>();
        foreach (int c in codes) for (int b = 0; b < 9; b++) bits.Add(((c >> b) & 1) != 0);
        var stream = new byte[(bits.Count + 7) / 8];
        for (int i = 0; i < bits.Count; i++) if (bits[i]) stream[i >> 3] |= (byte)(1 << (i & 7));
        var sys = new List<byte>();
        sys.AddRange(BitConverter.GetBytes(8));                  // map 0 at offset 8
        sys.AddRange(BitConverter.GetBytes(-1));                 // map 1 absent
        sys.Add(1); sys.Add(1);                                  // 1 screen down, 1 across
        var name = new byte[24]; Encoding.ASCII.GetBytes("EARTH").CopyTo(name, 0);
        sys.AddRange(name);
        sys.AddRange(BitConverter.GetBytes((ushort)stream.Length));
        sys.AddRange(stream);
        var index = MapFile.ReadIndex(sys.ToArray());
        CheckEqual(1, index.Count, "index has one map");
        var map = MapFile.Load(sys.ToArray(), 0);
        Check(map is { Rows: 12, Columns: 20, HeaderName: "EARTH" }, "map header");
        if (map is not null)
        {
            CheckEqual(new MapCell(1, 5), map[0, 0], "SPR literal");
            CheckEqual(new MapCell(0, 150), map[0, 1], "repeat run start");
            CheckEqual(new MapCell(0, 150), map[0, 10], "repeat run end (rep 7 → 10 copies)");
            CheckEqual(new MapCell(0, 0), map[0, 11], "next run");
            CheckEqual(new MapCell(0, 0), map[11, 19], "last cell");
            CheckEqual(TileClass.Service, TileRules.Classify(map[0, 1]), "DEF row 7 col 10 is a service (inn)");
            CheckEqual(TileClass.Floor, TileRules.Classify(new MapCell(1, 150)), "SPR row 7 is walkable");
            CheckEqual(TileClass.Blocked, TileRules.Classify(new MapCell(1, 20)), "SPR row 1 is blocked");
            CheckEqual(TileClass.Water, TileRules.Classify(new MapCell(1, TileRules.DeepWaterTile)), "deep water");
            CheckEqual(TileClass.Door, TileRules.Classify(new MapCell(0, 162)), "DEF row 8 door");
            Check(TileRules.IsWalkable(new MapCell(0, 172)) && !TileRules.IsSafeLanding(new MapCell(0, 162)), "door rows 12-15 walkable; doors not a landing");
            CheckEqual("EXIT", TileRules.TriggerLabel(new MapCell(0, 140), false), "exit label");
            CheckEqual("→ map 3", TileRules.TriggerLabel(new MapCell(0, 153), false), "entrance label");
            CheckEqual("BANK", TileRules.TriggerLabel(new MapCell(0, 144), false), "bank label");
            var anns = MapAnnotations.For(map, Array.Empty<NpcInfo>());
            CheckEqual(1, anns.Count, "one run of ten identical inn signs is one label");
        }
        Check(MapFile.Load(sys.ToArray(), 1) is null, "absent map");
        Check(MapFile.Decode(new byte[4], 0, 0) is null, "truncated map rejected");

        Section("SET and NPC records (synthetic)");
        var set = new byte[40 + 0x47];
        Synthetic.Put16(set, 0, 40);                             // entry 0 → first record (table = 20 entries)
        for (int i = 1; i < 20; i++) Synthetic.Put16(set, i * 2, 0xFFFF);
        Synthetic.Put16(set, 11 * 2, 40);                        // planet 1 area 1
        Encoding.ASCII.GetBytes("1001100000000000").CopyTo(set, 40);
        Synthetic.Put16(set, 40 + 16 + 3 * 4, 29);
        Synthetic.Put16(set, 40 + 16 + 3 * 4 + 2, 18);
        Encoding.ASCII.GetBytes("LONDON\n").CopyTo(set, 40 + 0x38);
        var area = AreaFiles.ReadArea(set, 1, 1);
        Check(area is { Name: "LONDON" }, "area name");
        Check(area is not null && area.DarkMaps.SequenceEqual(new[] { 1, 4, 5 }), "dark maps from the flag string");
        CheckEqual((29, 18), area?.EntryPositions[3], "entry position for map 3");
        Check(AreaFiles.ReadArea(set, 1, 2) is null, "absent area");

        var npc = new byte[2000 + 2 + 140];
        for (int i = 0; i < 1000; i++) Synthetic.Put16(npc, i * 2, 0xFFFF);
        Synthetic.Put16(npc, 110 * 2, 2000);
        Synthetic.Put16(npc, 2000, 1);
        Encoding.ASCII.GetBytes("JACK THE RIPPER").CopyTo(npc, 2002);
        Synthetic.Put16(npc, 2002 + 0x19, 72);
        Synthetic.Put16(npc, 2002 + 0x1B, 110);
        npc[2002 + 0x1D] = 17; npc[2002 + 0x1E] = 40; npc[2002 + 0x26] = 132;
        var npcs = AreaFiles.ReadNpcs(npc, 110);
        CheckEqual(1, npcs.Count, "one NPC");
        CheckEqual(new NpcInfo("JACK THE RIPPER", 72, 110, 17, 40, 0, 132, 0), npcs.FirstOrDefault(), "NPC fields");
        CheckEqual(0, AreaFiles.ReadNpcs(npc, 111).Count, "map without NPCs");

        Section("mount-line parsing");
        CheckEqual(@"C:\Temp\Scratch\Win31DOSBox\C-DRIVE", GameFolder.ParseMountPath(@"mount c C:\Temp\Scratch\Win31DOSBox\C-DRIVE"), "path with a hyphen");
        CheckEqual(@"D:\My Games", GameFolder.ParseMountPath("mount d \"D:\\My Games\" -t dir"), "quoted path with options");
        CheckEqual(@"E:\DOS", GameFolder.ParseMountPath(@"  MOUNT e E:\DOS -freesize 1024"), "options stripped");
        Check(GameFolder.ParseMountPath("imgmount a floppy.img") is null, "not a mount line");
    }

    private static void CheckCluebookWithoutGame()
    {
        Section("cluebook (no game folder)");
        string html = HtmlCluebookWriter.Write(Cluebook.Build(new CluebookOptions()));
        Check(HtmlPage.IsSelfContained(html, out string why), "self-contained: " + why);
        Check(html.Contains("id=\"walkthrough\"") && html.Contains("id=\"items\""), "sections present");
        Check(html.Contains("LIGHT REVOLVER"), "item catalogue");
        Check(html.Contains("set the game folder"), "maps section explains the missing folder");
        string minimal = HtmlCluebookWriter.Write(Cluebook.Build(new CluebookOptions { IncludeItems = false, IncludeMaps = false }));
        Check(!minimal.Contains("id=\"items\"") && !minimal.Contains("href=\"#maps\""), "options remove sections");
    }

    // ---- shipped files -------------------------------------------------------------------------

    private static void CheckShippedState(string folder)
    {
        Section("shipped DEF.S (the default party's new game)");
        var path = GameFolder.Find(folder, "DEF.S");
        if (path is null) { Console.WriteLine("    (no DEF.S)"); return; }
        var save = SaveGame.Load(path, out string error);
        Check(save is not null, "DEF.S loads: " + error);
        if (save is null) return;
        var s = save.State;
        var names = Enumerable.Range(0, 5).Select(i => new CharacterRecord(s, i).Name).ToArray();
        Check(names.SequenceEqual(Synthetic.Names), "party names: " + string.Join(", ", names));
        var wells = new CharacterRecord(s, 0);
        CheckEqual(24000L, wells.Wealth, "Wells' GOLD 24000 on the status panel");
        CheckEqual(800L, wells.MonthlyIncome, "Wells' income = wealth / 30");
        CheckEqual(200, wells.BodyWeight, "PERSON'S WEIGHT 200");
        CheckEqual("HEALTH 8/4", $"HEALTH {wells.Health}/{wells.UnconsciousThreshold}", "Wells' health line");
        Check(Enumerable.Range(0, 6).Select(wells.GetAttribute).SequenceEqual(new[] { 5, 5, 4, 5, 4, 4 }), "Wells' attributes STR 5 AGI 5 END 4 INT 5 CHA 4 SOC 4");
        CheckEqual(5, wells.GetSkill(12), "Observation 5");
        CheckEqual(5, wells.GetSkill(13), "Engineering 5");
        CheckEqual(4, wells.GetSkill(14), "Science 4");
        CheckEqual(2, wells.GetSkill(7), "Mechanics 2");
        CheckEqual("INVENTOR", wells.Career1, "Wells' career");
        CheckEqual(26, wells.Career1Id, "career id 26");
        CheckEqual("Gentry", wells.SocialClass, "Wells is Gentry");
        var marie = new CharacterRecord(s, 1);
        CheckEqual(384000L, marie.Wealth, "Lady Marie's WEALTH 384000");
        CheckEqual("HEALTH 10/5", $"HEALTH {marie.Health}/{marie.UnconsciousThreshold}", "Lady Marie's health line");
        CheckEqual("Aristocracy", marie.SocialClass, "Lady Marie is Aristocracy");
        Check(marie.IsFemale, "Lady Marie is female");
        CheckEqual("Wealthy Gentry", new CharacterRecord(s, 2).SocialClass, "Elizabeth is Wealthy Gentry");
        CheckEqual(312000L, new CharacterRecord(s, 4).Wealth, "Sir Walter's WEALTH 312000");
        var world = new WorldRecord(s);
        CheckEqual(0L, world.PartyAccount, "party account starts empty");
        CheckEqual(31200L, Enumerable.Range(0, 5).Sum(i => new CharacterRecord(s, i).MonthlyIncome), "the day-30 payment seen live (31,200)");
        CheckEqual(113, world.MapId, "starts in the London museum (map 113)");
        CheckEqual("LONDON", world.AreaNameText, "area record name");
        CheckEqual((29, 18), (world.Row, world.Column), "starts at row 29, column 18");
        CheckEqual(0L, world.Day, "day 0");
        CheckEqual(0, world.StoryFlags, "no story flags");
        Check(Enumerable.Range(0, 5).All(i => world.MarchingOrder(i) == i), "marching order 0-4");
        CheckEqual(0, wells.ItemCount, "empty packs");
    }

    private static void CheckShippedItems(string folder)
    {
        Section("shipped ITEMS.DAT against ItemBook");
        var bytes = GameFolder.ReadFile(folder, "ITEMS.DAT");
        if (bytes is null) { Check(false, "ITEMS.DAT readable"); return; }
        CheckEqual(2 + ItemBook.Count * ItemBook.RecordSize, bytes.Length, "ITEMS.DAT size");
        CheckEqual(ItemBook.Count, BitConverter.ToUInt16(bytes, 0), "record count");
        int mismatches = 0;
        foreach (var item in ItemBook.All)
        {
            int r = 2 + item.Index * ItemBook.RecordSize;
            int n = 0;
            while (n < 40 && bytes[r + n] != 0) n++;
            bool same = Encoding.ASCII.GetString(bytes, r, n) == item.Name
                && bytes[r + 0x29] == item.ShopMask && bytes[r + 0x2A] == item.Type
                && BitConverter.ToInt32(bytes, r + 0x2B) == item.Price && BitConverter.ToInt32(bytes, r + 0x2F) == item.Weight16
                && bytes[r + 0x34] == item.Shots && bytes[r + 0x36] == item.Damage && BitConverter.ToUInt16(bytes, r + 0x39) == item.Range;
            if (!same && mismatches++ < 5) Console.Error.WriteLine($"    item {item.Id} differs from ITEMS.DAT");
        }
        CheckEqual(0, mismatches, "every ItemBook row matches ITEMS.DAT");
    }

    private static void CheckShippedCareers(string folder)
    {
        Section("shipped 89CAREER.DAT against CareerBook");
        var bytes = GameFolder.ReadFile(folder, "89CAREER.DAT");
        if (bytes is null) { Console.WriteLine("    (no 89CAREER.DAT)"); return; }
        CheckEqual(40 * 49, bytes.Length, "40 × 49-byte records");
        int mismatches = 0;
        for (int i = 0; i < 40; i++)
        {
            int n = 0;
            while (n < 30 && bytes[i * 49 + n] != 0) n++;
            string name = Encoding.ASCII.GetString(bytes, i * 49, n);
            if (!name.Equals(CareerBook.Names[i], StringComparison.OrdinalIgnoreCase) || bytes[i * 49 + 0x20] != i)
                if (mismatches++ < 5) Console.Error.WriteLine($"    career {i}: file '{name}' id {bytes[i * 49 + 0x20]}, book '{CareerBook.Names[i]}'");
        }
        CheckEqual(0, mismatches, "career names and ids match");
    }

    private static void CheckShippedMaps(string folder)
    {
        Section("shipped maps");
        var lib = new MapLibrary(folder);
        CheckEqual(144, lib.Maps.Count, "144 distinct maps (54 in A.SYS + 91 in B.SYS − the shared pub and inn + space)");
        int bad = 0;
        foreach (var e in lib.Maps)
            if ((e.File == "0.SYS" ? lib.Load(0, space: true) : lib.Load(e.Id, e.Planet >= 4 ? 4 : 1)) is null) bad++;
        CheckEqual(0, bad, "every map decodes");
        var ground = lib.Maps.Where(e => e.File != "0.SYS").Select(e => lib.Load(e.Id, e.Planet >= 4 ? 4 : 1)).Where(m => m is not null).ToList();
        CheckEqual(MapFile.LargestGroundRows, ground.Max(m => m!.Rows), "LargestGroundRows is the tallest ground map");
        CheckEqual(MapFile.LargestGroundColumns, ground.Max(m => m!.Columns), "LargestGroundColumns is the widest ground map");
        Check(lib.Load(992, 1) is not null && lib.Load(992, 4) is not null, "the pub exists in both A.SYS and B.SYS");

        var london = lib.Load(110);
        Check(london is { Columns: 80, Rows: 48, HeaderName: "EARTH" }, "London is 80 × 48");
        var earth = lib.Load(100);
        Check(earth is { Columns: 100, Rows: 108 }, "the Earth map is 100 × 108");
        var space = lib.Load(0, space: true);
        Check(space is { Columns: 120, Rows: 120 }, "the space chart is 120 × 120");
        var museum = lib.Load(113);
        Check(museum is not null && museum.Contains(29, 18) && TileRules.IsWalkable(museum[29, 18]), "the starting square (29, 18) is walkable");

        var area = lib.Area(1, 1);
        Check(area is { Name: "LONDON" } && area.DarkMaps.SequenceEqual(new[] { 1, 4, 5 }), "London's dark maps are 1, 4, 5 (Silbury Hill)");
        var npcs = lib.Npcs(110, 1, 1);
        Check(npcs.Any(n => n.Name == "JACK THE RIPPER" && n.Id == 72), "Jack the Ripper wanders London");
        Check(npcs.Any(n => n.Name.StartsWith("CLAUS VON SCHMELLING")), "Claus von Schmelling is in London");
        Check(lib.Npcs(999, 1, 1).Any(n => n.Name.Contains("RAVEN")), "Dr Raven is in London's inn");

        if (london is not null)
        {
            var anns = MapAnnotations.For(london, npcs);
            foreach (var shop in new[] { "PAWN", "ARCH", "MKT", "WPN", "PUB", "INN" })
                Check(anns.Any(a => a.Kind == AnnotationKind.Service && a.Label == shop), $"London has a {shop} sign");
            Check(anns.Any(a => a.Kind == AnnotationKind.DigSite && a.Row == 30 && a.Column == 61), "Stonehenge dig marked");
            if (lib.Load(160) is { } teotihuacan)
                Check(MapAnnotations.For(teotihuacan, Array.Empty<NpcInfo>()).Any(a => a.Kind == AnnotationKind.Hidden && a.Row == 35 && a.Column == 76),
                      "the Atlantis entrance a story event reveals is marked");
            string svg = SchematicSvg.Render(london, anns, "London");
            Check(svg.StartsWith("<svg") && svg.EndsWith("</svg>") && svg.Length < 120_000, $"London schematic SVG ({svg.Length:N0} chars)");
        }
        foreach (var spot in MapAnnotations.ScriptedSpots)
        {
            var m = lib.Load(spot.MapId);
            Check(m is not null && m.Contains(spot.Row, spot.Column), $"scripted spot on map {spot.MapId} at ({spot.Row}, {spot.Column}) is on the map");
        }
        foreach (var (mapId, _, _) in TileRules.LockedDoorMaps)
            Check(lib.Maps.Any(e => e.Id == mapId), $"locked-door map {mapId} exists");

        var sheet = lib.Sheet(1, 1);
        Check(sheet is { Width: 320, Height: 192 }, "1.SPR is a 320 × 192 sheet of 16 × 16 tiles");
        if (london is not null)
        {
            var tiles = lib.Render(london, MapRenderMode.Tiles, 16, 1);
            CheckEqual(80 * 16 * 48 * 16 * 4, tiles.Bgra.Length, "tile render size");
            var flat = lib.Render(london, MapRenderMode.Schematic, 4, 1);
            CheckEqual(80 * 4, flat.Width, "schematic render width");
        }

        string html = HtmlCluebookWriter.Write(Cluebook.Build(new CluebookOptions(), folder));
        Check(HtmlPage.IsSelfContained(html, out string why), "cluebook with maps is self-contained: " + why);
        Check(html.Contains("<svg") && html.Length < 3_000_000, $"cluebook maps embedded ({html.Length:N0} chars)");
    }

    private static void ExportMaps(string folder, string outDir)
    {
        Section($"exporting schematic maps to {outDir}");
        Directory.CreateDirectory(outDir);
        var lib = new MapLibrary(folder);
        int n = 0;
        foreach (var e in lib.Maps)
        {
            if (e.File == "0.SYS") continue;
            int planet = e.Id is 992 or 999 ? 1 : e.Planet;
            var map = lib.Load(e.Id, planet);
            if (map is null) continue;
            var npcs = e.Id is 992 or 999 ? Array.Empty<NpcInfo>() : lib.Npcs(e.Id, e.Planet, e.Area);
            var png = SchematicPng.Render(map, MapAnnotations.For(map, npcs), e.Area == 0 && e.Id < 900 ? 6 : 8);
            File.WriteAllBytes(Path.Combine(outDir, $"map-{e.Id:000}.png"), png);
            n++;
        }
        Console.WriteLine($"    wrote {n} PNGs");
        Check(n == lib.Maps.Count - 1, "one PNG per map");
    }
}
