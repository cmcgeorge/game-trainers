using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen;

/// <summary>
/// Renders a game-themed application icon for every trainer, on a dark rounded
/// tile, at seven sizes (16..256), packed into a multi-resolution <c>app.ico</c>
/// per trainer.  WPF-only; no extra deps.  Supersedes the per-trainer IconGen that
/// once lived inside <c>MightAndMagic1Trainer/tools/</c>.
/// Run from the repo root: <c>dotnet run --project tools\IconGen</c>
/// </summary>
internal static class Program
{
    private const double C = 256.0;
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static readonly Color BgTop = Color.FromRgb(0x2B, 0x2E, 0x3A);
    private static readonly Color BgBot = Color.FromRgb(0x18, 0x19, 0x20);
    private static readonly Color BgBorder = Color.FromRgb(0x3C, 0x41, 0x50);

    private record IconSpec(string Name, string TrainerFolder, Action<DrawingContext> Draw);

    private static readonly IconSpec[] Icons =
    {
        new("Autoduel",             "AutoduelTrainer",             DrawAutoduel),
        new("BattleTech",           "BattleTech1Trainer",          DrawBattleTech),
        new("Colonization",         "ColonizationTrainer",         DrawColonization),
        new("Darklands",            "DarklandsTrainer",            DrawDarklands),
        new("Dragon Wars",          "DragonWarsTrainer",           DrawDragonWars),
        new("Imperialism II",       "ImperialismIITrainer",        DrawImperialismII),
        new("Keef the Thief",       "KeefTrainer",                 DrawKeef),
        new("Lords of the Realm",   "LordsOfTheRealmTrainer",      DrawLords),
        new("Might & Magic 1",      "MightAndMagic1Trainer",       DrawMightAndMagic1),
        new("Mines of Titan",       "MinesOfTitanTrainer",         DrawMinesOfTitan),
        new("Moria",                "MoriaTrainer",                DrawMoria),
        new("Pool of Radiance",     "PoolOfRadianceTrainer",       DrawPoolOfRadiance),
        new("Quest for Glory I",    "QuestForGlory1Trainer",       DrawQuestForGlory),
        new("Railroad Tycoon",      "RailroadTycoonTrainer",       DrawRailroadTycoon),
        new("Shogun",               "ShogunTrainer",               DrawShogun),
        new("Sword of the Samurai", "SwordOfTheSamuraiTrainer",    DrawSwordOfSamurai),
        new("Syndicate Plus",       "SyndicatePlusTrainer",        DrawSyndicate),
        new("The Perfect General II","ThePerfectGeneral2Trainer",  DrawPerfectGeneral),
        new("War of the Lance",     "WarOfTheLanceTrainer",        DrawWarOfTheLance),
        new("Wasteland",            "WastelandTrainer",            DrawWasteland),
    };

    [STAThread]
    private static int Main(string[] args)
    {
        string repoRoot = FindRepoRoot();

        if (args.Length > 0 && args[0] is "--list" or "-l")
        {
            foreach (var s in Icons) Console.WriteLine($"  {s.Name,-22} -> {s.TrainerFolder}");
            return 0;
        }

        // Optional: render a single icon by name keyword.
        var targets = args.Length > 0
            ? Icons.Where(s => s.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray()
            : Icons;

        if (targets.Length == 0)
        {
            Console.Error.WriteLine($"No icon matched '{args[0]}'.  Use --list to see names.");
            return 1;
        }

        foreach (var spec in targets)
        {
            var pngs = Sizes.Select(s => RenderPng(s, spec.Draw)).ToList();
            string relPath = FindAssetsPath(repoRoot, spec.TrainerFolder);
            string full = Path.Combine(repoRoot, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            WriteIco(full, Sizes, pngs);
            Console.WriteLine($"  {spec.Name,-22} -> {relPath}");
        }

        Console.WriteLine($"\nWrote {targets.Length} icon(s) ({Sizes.Length} sizes each: {string.Join(", ", Sizes)}).");
        return 0;
    }

    // --- rendering plumbing --------------------------------------------------
    private static byte[] RenderPng(int size, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / C, size / C));
            draw(dc);
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        w.Write((ushort)0);            // reserved
        w.Write((ushort)1);            // type: 1 = icon
        w.Write((ushort)sizes.Length); // image count
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            w.Write((byte)(s >= 256 ? 0 : s)); // width  (0 == 256)
            w.Write((byte)(s >= 256 ? 0 : s)); // height (0 == 256)
            w.Write((byte)0);                  // palette count
            w.Write((byte)0);                  // reserved
            w.Write((ushort)1);                // color planes
            w.Write((ushort)32);               // bits per pixel
            w.Write((uint)pngs[i].Length);     // bytes in resource (PNG)
            w.Write((uint)offset);             // offset to image data
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "AGENTS.md")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Discovers the WPF WinExe project inside a trainer's top-level folder and
    /// returns the repo-relative path to its <c>Assets/app.ico</c>.  Avoids
    /// hardcoding internal folder structures (src/Name/ vs Trainer/ etc.).
    /// </summary>
    private static string FindAssetsPath(string repoRoot, string trainerFolder)
    {
        string folder = Path.Combine(repoRoot, trainerFolder);
        foreach (var csproj in Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
        {
            // Skip bin/obj intermediates — they don't contain .csproj files, but
            // be defensive in case a stale build artifact lingers.
            if (IsInBuildDir(csproj))
                continue;

            string text = File.ReadAllText(csproj);
            if (text.Contains("<UseWPF>true</UseWPF>", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("<OutputType>WinExe</OutputType>", StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(repoRoot,
                    Path.Combine(Path.GetDirectoryName(csproj)!, "Assets", "app.ico"));
        }
        throw new FileNotFoundException(
            $"No WPF WinExe project found under {trainerFolder}/ — cannot determine Assets path.");
    }

    /// <summary>Returns true if any path segment is exactly <c>bin</c> or <c>obj</c>.</summary>
    private static bool IsInBuildDir(string path)
    {
        foreach (var segment in path.Split('\\', '/'))
            if (segment is "bin" or "obj")
                return true;
        return false;
    }

    // --- shared helpers ------------------------------------------------------
    private static void DrawTile(DrawingContext dc)
    {
        var bg = new LinearGradientBrush(BgTop, BgBot, 90);
        dc.DrawRoundedRectangle(bg, new Pen(new SolidColorBrush(BgBorder), 4),
            new Rect(0, 0, C, C), 50, 50);
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private static StreamGeometry Poly(params Point[] pts) => Poly(true, pts);

    private static StreamGeometry Poly(bool close, params Point[] pts)
    {
        var geo = new StreamGeometry();
        using (StreamGeometryContext ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: true, isClosed: close);
            ctx.PolyLineTo(pts[1..], isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    private static StreamGeometry Curves(Point start, bool close, params (Point Ctrl, Point End)[] segs)
    {
        var geo = new StreamGeometry();
        using (StreamGeometryContext ctx = geo.Open())
        {
            ctx.BeginFigure(start, isFilled: true, isClosed: close);
            foreach (var (ctrl, end) in segs)
                ctx.QuadraticBezierTo(ctrl, end, isStroked: true, isSmoothJoin: true);
        }
        geo.Freeze();
        return geo;
    }

    private static Point P(double x, double y) => new(x, y);
    private static Point Ray(Point c, double angleDeg, double r) =>
        new(c.X + r * Math.Cos(angleDeg * Math.PI / 180), c.Y + r * Math.Sin(angleDeg * Math.PI / 180));

    // --- per-game icons ------------------------------------------------------

    /// <summary>Steering wheel — Autoduel is a car-combat RPG.</summary>
    private static void DrawAutoduel(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = Brush(0xE4, 0xB2, 0x3C);
        var goldDk = Brush(0xB8, 0x89, 0x1F);
        var c = P(128, 134);
        const double rim = 72, hub = 20;

        dc.DrawGeometry(null, new Pen(gold, 12), new EllipseGeometry(c, rim, rim));
        var spoke = new Pen(gold, 9);
        for (int i = 0; i < 3; i++)
        {
            double a = 90 + i * 120;
            dc.DrawLine(spoke, Ray(c, a, hub), Ray(c, a, rim));
        }
        dc.DrawGeometry(gold, new Pen(goldDk, 2), new EllipseGeometry(c, hub, hub));
    }

    /// <summary>Targeting reticle — BattleTech is a mech tactical game.</summary>
    private static void DrawBattleTech(DrawingContext dc)
    {
        DrawTile(dc);
        var col = Brush(0xE0, 0x60, 0x40);
        var pen = new Pen(col, 5);
        const double ins = 52, len = 28;

        // Corner brackets.
        dc.DrawLine(pen, P(ins, ins + len), P(ins, ins));  dc.DrawLine(pen, P(ins, ins), P(ins + len, ins));
        dc.DrawLine(pen, P(C - ins - len, ins), P(C - ins, ins));  dc.DrawLine(pen, P(C - ins, ins), P(C - ins, ins + len));
        dc.DrawLine(pen, P(ins, C - ins - len), P(ins, C - ins));  dc.DrawLine(pen, P(ins, C - ins), P(ins + len, C - ins));
        dc.DrawLine(pen, P(C - ins - len, C - ins), P(C - ins, C - ins));  dc.DrawLine(pen, P(C - ins, C - ins), P(C - ins, C - ins - len));

        // Crosshair with gap.
        const double gap = 18;
        dc.DrawLine(pen, P(68, 128), P(128 - gap, 128));  dc.DrawLine(pen, P(128 + gap, 128), P(188, 128));
        dc.DrawLine(pen, P(128, 68), P(128, 128 - gap));  dc.DrawLine(pen, P(128, 128 + gap), P(128, 188));
        dc.DrawGeometry(col, null, new EllipseGeometry(P(128, 128), 5, 5));
    }

    /// <summary>Compass rose — Colonization is a colonial-era exploration strategy.</summary>
    private static void DrawColonization(DrawingContext dc)
    {
        DrawTile(dc);
        var blue = Brush(0x4A, 0x9E, 0xCC);
        var blueLt = Brush(0x7A, 0xC4, 0xE4);
        var star = Poly(
            P(128, 44), P(142, 116), P(212, 128), P(142, 140),
            P(128, 212), P(114, 140), P(44, 128), P(114, 116));
        dc.DrawGeometry(blue, new Pen(blueLt, 2), star);
        dc.DrawGeometry(blueLt, null, new EllipseGeometry(P(128, 128), 8, 8));
    }

    /// <summary>Shield with cross — Darklands is a medieval Germany RPG.</summary>
    private static void DrawDarklands(DrawingContext dc)
    {
        DrawTile(dc);
        var pur = Brush(0x99, 0x66, 0xBB);
        var purLt = Brush(0xC0, 0x98, 0xDD);
        var white = Brush(0xE8, 0xE0, 0xF0);
        var shield = Poly(P(66, 56), P(190, 56), P(190, 128), P(128, 204), P(66, 128));
        dc.DrawGeometry(pur, new Pen(purLt, 3), shield);
        dc.DrawRectangle(white, null, new Rect(120, 76, 16, 108));
        dc.DrawRectangle(white, null, new Rect(84, 108, 88, 16));
    }

    /// <summary>Dragon eye — Dragon Wars is a dragon-themed RPG.</summary>
    private static void DrawDragonWars(DrawingContext dc)
    {
        DrawTile(dc);
        var red = Brush(0xD0, 0x40, 0x30);
        var redDk = Brush(0x80, 0x20, 0x18);
        var gold = Brush(0xF0, 0xD0, 0x40);

        var eye = Curves(P(44, 128), true,
            (P(128, 72), P(212, 128)),
            (P(128, 184), P(44, 128)));
        dc.DrawGeometry(red, new Pen(redDk, 3), eye);

        var pupil = Curves(P(128, 92), true,
            (P(135, 128), P(128, 164)),
            (P(121, 128), P(128, 92)));
        dc.DrawGeometry(gold, null, pupil);
    }

    /// <summary>Crown — Imperialism II is an age-of-exploration empire strategy.</summary>
    private static void DrawImperialismII(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = new LinearGradientBrush
        {
            StartPoint = P(0, 0), EndPoint = P(0, 1),
            GradientStops =
            {
                new(Color.FromRgb(0xF4, 0xD9, 0x90), 0),
                new(Color.FromRgb(0xD4, 0xA8, 0x43), 0.6),
                new(Color.FromRgb(0x9E, 0x6E, 0x1C), 1),
            }
        };
        var ruby = Brush(0xE0, 0x50, 0x50);
        var sapphire = Brush(0x50, 0x80, 0xE0);
        var emerald = Brush(0x50, 0xD0, 0x60);

        // Base bar.
        dc.DrawRectangle(gold, null, new Rect(52, 162, 152, 26));
        // Three spikes.
        dc.DrawGeometry(gold, null, Poly(P(52, 162), P(80, 74), P(108, 162)));
        dc.DrawGeometry(gold, null, Poly(P(100, 162), P(128, 56), P(156, 162)));
        dc.DrawGeometry(gold, null, Poly(P(148, 162), P(176, 74), P(204, 162)));
        // Jewels on tips.
        dc.DrawGeometry(ruby, null, new EllipseGeometry(P(80, 72), 10, 10));
        dc.DrawGeometry(sapphire, null, new EllipseGeometry(P(128, 54), 11, 11));
        dc.DrawGeometry(emerald, null, new EllipseGeometry(P(176, 72), 10, 10));
    }

    /// <summary>Skeleton key — Keef the Thief is a thief adventure.</summary>
    private static void DrawKeef(DrawingContext dc)
    {
        DrawTile(dc);
        var grn = Brush(0x3E, 0x8E, 0x5A);
        // Bow (ring).
        dc.DrawGeometry(null, new Pen(grn, 14), new EllipseGeometry(P(128, 66), 26, 26));
        // Shaft.
        dc.DrawRectangle(grn, null, new Rect(122, 90, 12, 98));
        // Teeth.
        dc.DrawRectangle(grn, null, new Rect(134, 158, 24, 12));
        dc.DrawRectangle(grn, null, new Rect(134, 174, 16, 12));
    }

    /// <summary>Castle — Lords of the Realm is a medieval strategy.</summary>
    private static void DrawLords(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = Brush(0xC9, 0xA2, 0x4B);
        var goldDk = Brush(0x8E, 0x72, 0x30);
        var pen = new Pen(goldDk, 2);

        // Main wall.
        dc.DrawRectangle(gold, pen, new Rect(52, 112, 152, 92));
        // Wall battlements.
        for (int i = 0; i < 5; i++)
            dc.DrawRectangle(gold, pen, new Rect(54 + i * 30, 96, 18, 16));
        // Central tower.
        dc.DrawRectangle(gold, pen, new Rect(106, 64, 44, 48));
        // Tower battlements.
        for (int i = 0; i < 3; i++)
            dc.DrawRectangle(gold, pen, new Rect(106 + i * 16, 50, 10, 14));
        // Door.
        dc.DrawRectangle(goldDk, null, new Rect(116, 166, 24, 38));
        dc.DrawGeometry(goldDk, null, new EllipseGeometry(P(128, 166), 12, 12));
    }

    /// <summary>Gold faceted gem — Might &amp; Magic 1 is the architectural template trainer.</summary>
    private static void DrawMightAndMagic1(DrawingContext dc)
    {
        DrawTile(dc);

        // Brilliant-cut diamond outline.
        var pTableL = P(82, 78);
        var pTableR = P(174, 78);
        var pTableM = P(128, 78);
        var pGirdleL = P(46, 116);
        var pGirdleR = P(210, 116);
        var pTip = P(128, 200);

        var outline = Poly(pTableL, pTableR, pGirdleR, pTip, pGirdleL);

        // Faceted gold fill, lighter at the table, deeper toward the tip.
        var gold = new LinearGradientBrush
        {
            StartPoint = P(0, 0), EndPoint = P(0, 1),
            GradientStops =
            {
                new(Color.FromRgb(0xF4, 0xD9, 0x90), 0.0),
                new(Color.FromRgb(0xE0, 0xA9, 0x3C), 0.45),
                new(Color.FromRgb(0x9E, 0x6E, 0x1C), 1.0),
            }
        };
        dc.DrawGeometry(gold, null, outline);

        // Sheen across the crown (table band).
        var crown = Poly(pTableL, pTableR, pGirdleR, pGirdleL);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), null, crown);

        // Facet lines.
        var facet = new Pen(new SolidColorBrush(Color.FromArgb(0xCC, 0x6B, 0x4A, 0x10)), 2.2)
        { LineJoin = PenLineJoin.Round };
        dc.DrawLine(facet, pTableL, pGirdleL);   // left crown
        dc.DrawLine(facet, pTableR, pGirdleR);   // right crown
        dc.DrawLine(facet, pGirdleL, pGirdleR);  // girdle
        dc.DrawLine(facet, pTableM, pTip);       // center
        dc.DrawLine(facet, pTableL, pTip);       // left pavilion
        dc.DrawLine(facet, pTableR, pTip);       // right pavilion

        // Bright glint on the upper-left table facet.
        var glint = Poly(P(92, 82), P(122, 82), P(110, 104), P(80, 104));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), null, glint);

        // Crisp outer edge on the gem.
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x7A, 0x56, 0x16)), 2.5), outline);
    }

    /// <summary>Ringed planet — Mines of Titan is a sci-fi RPG set on Titan.</summary>
    private static void DrawMinesOfTitan(DrawingContext dc)
    {
        DrawTile(dc);
        var orange = Brush(0xD9, 0x7A, 0x2C);
        var orangeLt = Brush(0xF0, 0xA0, 0x50);
        var c = P(128, 134);

        // Ring (behind planet, tilted).
        var ring = new EllipseGeometry(c, 84, 26);
        ring.Transform = new RotateTransform(-20, c.X, c.Y);
        dc.DrawGeometry(null, new Pen(orange, 6), ring);
        // Planet.
        dc.DrawGeometry(orange, new Pen(orangeLt, 2), new EllipseGeometry(c, 46, 46));
        // Ring front half (drawn on top of planet).
        dc.DrawGeometry(null, new Pen(orange, 6), ring);
    }

    /// <summary>Sword — Moria is a roguelike dungeon crawler.</summary>
    private static void DrawMoria(DrawingContext dc)
    {
        DrawTile(dc);
        var steel = Brush(0xB8, 0xC4, 0xD0);
        var steelDk = Brush(0x80, 0x88, 0x90);
        var gold = Brush(0xC9, 0xA2, 0x4B);
        var brown = Brush(0x5A, 0x42, 0x24);

        // Blade.
        dc.DrawGeometry(steel, new Pen(steelDk, 1.5), Poly(P(128, 42), P(116, 142), P(140, 142)));
        dc.DrawLine(new Pen(steelDk, 1.5), P(128, 48), P(128, 138));
        // Crossguard.
        dc.DrawRectangle(gold, new Pen(steelDk, 1), new Rect(94, 142, 68, 10));
        // Grip.
        dc.DrawRectangle(brown, null, new Rect(122, 152, 12, 38));
        for (int i = 0; i < 4; i++)
            dc.DrawLine(new Pen(gold, 2), P(120, 158 + i * 8), P(136, 162 + i * 8));
        // Pommel.
        dc.DrawGeometry(gold, new Pen(steelDk, 1), new EllipseGeometry(P(128, 196), 10, 10));
    }

    /// <summary>Flame — Pool of Radiance is a D&D RPG (the pool glows).</summary>
    private static void DrawPoolOfRadiance(DrawingContext dc)
    {
        DrawTile(dc);
        var flame = new LinearGradientBrush
        {
            StartPoint = P(0, 1), EndPoint = P(0, 0),
            GradientStops =
            {
                new(Color.FromRgb(0xD0, 0x50, 0x20), 0),
                new(Color.FromRgb(0xE0, 0xB3, 0x41), 0.55),
                new(Color.FromRgb(0xFF, 0xE0, 0x80), 1),
            }
        };

        var outer = Curves(P(128, 48), true,
            (P(176, 96), P(168, 152)),
            (P(160, 188), P(128, 204)),
            (P(96, 188), P(88, 152)),
            (P(80, 96), P(128, 48)));
        dc.DrawGeometry(flame, null, outer);

        var inner = Curves(P(128, 92), true,
            (P(152, 120), P(148, 156)),
            (P(140, 178), P(128, 188)),
            (P(116, 178), P(108, 156)),
            (P(104, 120), P(128, 92)));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xC8)), null, inner);
    }

    /// <summary>Shield with sword — Quest for Glory I is a hero RPG.</summary>
    private static void DrawQuestForGlory(DrawingContext dc)
    {
        DrawTile(dc);
        var blue = Brush(0x5A, 0x8F, 0xCC);
        var blueDk = Brush(0x3A, 0x60, 0x9E);
        var steel = Brush(0xC8, 0xD0, 0xDC);
        var gold = Brush(0xC9, 0xA2, 0x4B);
        var brown = Brush(0x5A, 0x42, 0x24);

        var shield = Poly(P(72, 54), P(184, 54), P(184, 122), P(128, 204), P(72, 122));
        dc.DrawGeometry(blue, new Pen(blueDk, 3), shield);
        // Sword on shield.
        dc.DrawGeometry(steel, null, Poly(P(128, 74), P(120, 136), P(136, 136)));
        dc.DrawRectangle(gold, null, new Rect(108, 136, 40, 8));
        dc.DrawRectangle(brown, null, new Rect(124, 144, 8, 28));
        dc.DrawGeometry(gold, null, new EllipseGeometry(P(128, 176), 7, 7));
    }

    /// <summary>Steam locomotive — Railroad Tycoon is a railway-empire strategy.</summary>
    private static void DrawRailroadTycoon(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = Brush(0xC9, 0xA2, 0x4B);
        var goldDk = Brush(0x8E, 0x72, 0x30);
        var steel = Brush(0xB8, 0xC4, 0xD0);
        var smoke = Brush(0x9A, 0xA4, 0xB0);
        var pen = new Pen(goldDk, 2);

        // Rail bed (a horizontal line the wheels sit on).
        dc.DrawRectangle(goldDk, null, new Rect(30, 196, 196, 6));

        // Boiler (rounded, side-on) + cab at the rear.
        dc.DrawRoundedRectangle(gold, pen, new Rect(60, 120, 104, 44), 12, 12);
        dc.DrawRectangle(gold, pen, new Rect(150, 96, 46, 68));       // cab
        dc.DrawRectangle(steel, null, new Rect(158, 106, 30, 24));    // cab window

        // Smokestack + steam dome.
        dc.DrawRectangle(gold, pen, new Rect(76, 92, 22, 30));
        dc.DrawGeometry(gold, pen, new EllipseGeometry(P(120, 118), 12, 12));
        // Cow-catcher (pilot) at the front.
        dc.DrawGeometry(gold, null, Poly(P(60, 128), P(60, 164), P(40, 164)));

        // Smoke puffs above the stack.
        dc.DrawGeometry(smoke, null, new EllipseGeometry(P(90, 74), 12, 12));
        dc.DrawGeometry(smoke, null, new EllipseGeometry(P(110, 58), 16, 16));
        dc.DrawGeometry(smoke, null, new EllipseGeometry(P(136, 46), 20, 20));

        // Driving wheels.
        dc.DrawGeometry(steel, new Pen(goldDk, 3), new EllipseGeometry(P(88, 190), 24, 24));
        dc.DrawGeometry(steel, new Pen(goldDk, 3), new EllipseGeometry(P(150, 190), 24, 24));
        dc.DrawGeometry(goldDk, null, new EllipseGeometry(P(88, 190), 6, 6));
        dc.DrawGeometry(goldDk, null, new EllipseGeometry(P(150, 190), 6, 6));
    }

    /// <summary>Rising sun — Shogun is a feudal-Japan strategy.</summary>
    private static void DrawShogun(DrawingContext dc)
    {
        DrawTile(dc);
        var red = Brush(0xC8, 0x44, 0x3A);
        var redLt = Brush(0xE0, 0x60, 0x50);
        var c = P(128, 128);
        const double inner = 52, outer = 108;

        for (int i = 0; i < 16; i++)
        {
            double a = i * 22.5;
            dc.DrawGeometry(red, null, Poly(
                Ray(c, a - 5, inner), Ray(c, a, outer), Ray(c, a + 5, inner)));
        }
        dc.DrawGeometry(redLt, null, new EllipseGeometry(c, 46, 46));
    }

    /// <summary>Katana — Sword of the Samurai is a feudal-Japan action game.</summary>
    private static void DrawSwordOfSamurai(DrawingContext dc)
    {
        DrawTile(dc);
        var steel = Brush(0xC0, 0xC8, 0xD0);
        var steelDk = Brush(0x80, 0x88, 0x90);
        var darkRed = Brush(0xB8, 0x38, 0x30);
        var gold = Brush(0xC9, 0xA2, 0x4B);

        // Blade (curved, top-right to bottom-left).
        var blade = Curves(P(196, 52), false, (P(150, 80), P(80, 182)));
        dc.DrawGeometry(null, new Pen(steel, 7), blade);
        // Guard (tsuba).
        dc.DrawGeometry(gold, new Pen(steelDk, 1), new EllipseGeometry(P(86, 176), 12, 12));
        // Handle.
        var handle = Curves(P(86, 176), false, (P(70, 194), P(58, 208)));
        dc.DrawGeometry(null, new Pen(darkRed, 13), handle);
        // Pommel.
        dc.DrawGeometry(gold, null, new EllipseGeometry(P(58, 210), 7, 7));
    }

    /// <summary>Target reticle — Syndicate Plus is a cyberpunk tactical game.</summary>
    private static void DrawSyndicate(DrawingContext dc)
    {
        DrawTile(dc);
        var yel = Brush(0xE8, 0xB9, 0x23);
        var c = P(128, 128);
        dc.DrawGeometry(null, new Pen(yel, 6), new EllipseGeometry(c, 68, 68));
        dc.DrawGeometry(null, new Pen(yel, 4), new EllipseGeometry(c, 38, 38));
        const double gap = 22;
        var p = new Pen(yel, 5);
        dc.DrawLine(p, P(48, 128), P(128 - gap, 128));  dc.DrawLine(p, P(128 + gap, 128), P(208, 128));
        dc.DrawLine(p, P(128, 48), P(128, 128 - gap));  dc.DrawLine(p, P(128, 128 + gap), P(128, 208));
        dc.DrawGeometry(yel, null, new EllipseGeometry(c, 6, 6));
    }

    /// <summary>Five-point star — The Perfect General II is a wargame.</summary>
    private static void DrawPerfectGeneral(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = Brush(0xC0, 0xA0, 0x40);
        var goldLt = Brush(0xE4, 0xC8, 0x60);
        var c = P(128, 128);
        const double oR = 80, iR = 32;
        var pts = new Point[10];
        for (int i = 0; i < 10; i++)
        {
            double a = (-90 + i * 36) * Math.PI / 180;
            double r = i % 2 == 0 ? oR : iR;
            pts[i] = new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        }
        dc.DrawGeometry(gold, new Pen(goldLt, 2), Poly(pts));
    }

    /// <summary>Lance with pennant — War of the Lance is a Dragonlance game.</summary>
    private static void DrawWarOfTheLance(DrawingContext dc)
    {
        DrawTile(dc);
        var gold = Brush(0xD4, 0xA0, 0x40);
        var goldLt = Brush(0xF0, 0xC8, 0x50);
        var red = Brush(0xC8, 0x30, 0x30);
        var brown = Brush(0x8A, 0x6A, 0x3A);

        // Shaft (diagonal).
        dc.DrawLine(new Pen(brown, 8), P(56, 202), P(172, 86));
        // Spearhead.
        dc.DrawGeometry(goldLt, new Pen(gold, 2), Poly(P(172, 86), P(202, 56), P(186, 100)));
        // Pennant (triangular flag hanging from shaft).
        dc.DrawGeometry(red, new Pen(gold, 1.5), Poly(P(104, 154), P(148, 110), P(136, 136), P(118, 150)));
    }

    /// <summary>Radiation trefoil — Wasteland is a post-apocalyptic RPG.</summary>
    private static void DrawWasteland(DrawingContext dc)
    {
        DrawTile(dc);
        var grn = Brush(0x7B, 0xAA, 0x50);
        var grnLt = Brush(0xB0, 0xD8, 0x78);
        var c = P(128, 128);
        const double R = 80;

        for (int i = 0; i < 3; i++)
        {
            double a1 = (-120 + i * 120) * Math.PI / 180;
            double a2 = (-60 + i * 120) * Math.PI / 180;
            dc.DrawGeometry(grn, null, Poly(c,
                new(c.X + R * Math.Cos(a1), c.Y + R * Math.Sin(a1)),
                new(c.X + R * Math.Cos(a2), c.Y + R * Math.Sin(a2))));
        }
        dc.DrawGeometry(grnLt, null, new EllipseGeometry(c, 24, 24));
    }
}
