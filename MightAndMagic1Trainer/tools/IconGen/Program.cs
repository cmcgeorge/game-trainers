using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen;

/// <summary>
/// Renders the Might &amp; Magic 1 Trainer application icon — a gold faceted gem on a
/// dark rounded tile (matching the app's gold-on-dark theme) — at several sizes and
/// packs them into a single multi-resolution <c>app.ico</c>. WPF-only; no extra deps.
/// Run: <c>dotnet run --project tools\IconGen -- &lt;output.ico&gt;</c>
/// </summary>
internal static class Program
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    [STAThread]
    private static int Main(string[] args)
    {
        string outPath = args.Length > 0
            ? args[0]
            : Path.Combine("src", "MightAndMagic1Trainer", "Assets", "app.ico");

        var pngs = new List<byte[]>();
        foreach (int s in Sizes) pngs.Add(RenderPng(s));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        WriteIco(outPath, Sizes, pngs);

        Console.WriteLine($"Wrote {outPath} ({Sizes.Length} sizes: {string.Join(", ", Sizes)}).");
        return 0;
    }

    // --- drawing ----------------------------------------------------------------
    private static byte[] RenderPng(int size)
    {
        const double Canvas = 256.0;
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / Canvas, size / Canvas));
            Draw(dc);
            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void Draw(DrawingContext dc)
    {
        // Dark rounded tile background (theme: #1E1F26 → #272A35).
        var tile = new Rect(0, 0, 256, 256);
        var bg = new LinearGradientBrush(
            Color.FromRgb(0x2B, 0x2E, 0x3A), Color.FromRgb(0x18, 0x19, 0x20), 90);
        dc.DrawRoundedRectangle(bg, new Pen(new SolidColorBrush(Color.FromRgb(0x3C, 0x41, 0x50)), 4), tile, 50, 50);

        // Gem outline (a brilliant-cut diamond).
        var pTableL = new Point(82, 78);
        var pTableR = new Point(174, 78);
        var pTableM = new Point(128, 78);
        var pGirdleL = new Point(46, 116);
        var pGirdleR = new Point(210, 116);
        var pTip = new Point(128, 200);

        var outline = MakeGeometry(new[] { pTableL, pTableR, pGirdleR, pTip, pGirdleL }, close: true);

        // Faceted gold fill, lighter at the table, deeper toward the tip.
        var gold = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xF4, 0xD9, 0x90), 0.0),
                new GradientStop(Color.FromRgb(0xE0, 0xA9, 0x3C), 0.45),
                new GradientStop(Color.FromRgb(0x9E, 0x6E, 0x1C), 1.0),
            }
        };
        dc.DrawGeometry(gold, null, outline);

        // Sheen across the crown (table band).
        var crown = MakeGeometry(new[] { pTableL, pTableR, pGirdleR, pGirdleL }, close: true);
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
        var glint = MakeGeometry(new[]
        {
            new Point(92, 82), new Point(122, 82), new Point(110, 104), new Point(80, 104)
        }, close: true);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), null, glint);

        // Crisp outer edge on the gem.
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x7A, 0x56, 0x16)), 2.5), outline);
    }

    private static StreamGeometry MakeGeometry(Point[] pts, bool close)
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

    // --- .ico container ---------------------------------------------------------
    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        // ICONDIR
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
}
