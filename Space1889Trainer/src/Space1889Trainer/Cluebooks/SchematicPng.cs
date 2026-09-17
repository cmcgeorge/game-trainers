using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Space1889Trainer.Files;

namespace Space1889Trainer.Cluebooks;

/// <summary>
/// Renders a map schematic (flat colours, no game artwork) to PNG with rulers every ten squares, labels for exits,
/// shops, entrances and scripted spots, and numbered NPC dots — what the strategy guide's map images are.
/// Uses WPF drawing, so call it on an STA thread.
/// </summary>
public static class SchematicPng
{
    /// <summary>Space left for the rulers.</summary>
    public const int Margin = 18;

    public static byte[] Render(GameMap map, IReadOnlyList<MapAnnotation> annotations, int cellPixels = 8)
    {
        ArgumentNullException.ThrowIfNull(map);
        var image = MapLibrary.RenderSchematic(map, cellPixels);
        int w = image.Width + Margin, h = image.Height + Margin;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1D, 0x20, 0x27)), null, new Rect(0, 0, w, h));
            var bmp = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
            dc.DrawImage(bmp, new Rect(Margin, Margin, image.Width, image.Height));

            var rulerPen = new Pen(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), 1);
            for (int c = 0; c <= map.Columns; c += 10)
            {
                double x = Margin + c * cellPixels + 0.5;
                dc.DrawLine(rulerPen, new Point(x, Margin - 5), new Point(x, Margin));
                if (c < map.Columns) Label(dc, c.ToString(CultureInfo.InvariantCulture), x + 2, 1, Colors.Gainsboro, false);
            }
            for (int r = 0; r <= map.Rows; r += 10)
            {
                double y = Margin + r * cellPixels + 0.5;
                dc.DrawLine(rulerPen, new Point(Margin - 5, y), new Point(Margin, y));
                if (r < map.Rows) Label(dc, r.ToString(CultureInfo.InvariantCulture), 1, y + 1, Colors.Gainsboro, false);
            }

            int npcNumber = 0;
            foreach (var a in annotations)
            {
                double x = Margin + a.Column * cellPixels, y = Margin + a.Row * cellPixels;
                if (a.Kind == AnnotationKind.Npc)
                {
                    npcNumber++;
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xD0, 0x30, 0x28)), new Pen(Brushes.White, 1),
                                   new Point(x + cellPixels / 2.0, y + cellPixels / 2.0), cellPixels / 2.0 + 1, cellPixels / 2.0 + 1);
                    continue;
                }
                var color = a.Kind switch
                {
                    AnnotationKind.Exit => Color.FromRgb(0xF2, 0xCF, 0x4A),
                    AnnotationKind.Service => Color.FromRgb(0x4F, 0xE3, 0xDB),
                    AnnotationKind.DigSite => Color.FromRgb(0xD5, 0x9C, 0xFF),
                    AnnotationKind.Hidden => Color.FromRgb(0xFF, 0x6F, 0xB5),
                    _ => Color.FromRgb(0xFF, 0x9A, 0x4C),
                };
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(color), 1.5), new Rect(x, y, cellPixels, cellPixels));
                Label(dc, a.Label, x, Math.Max(Margin, y - 12), color, true);
            }
        }

        var target = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void Label(DrawingContext dc, string text, double x, double y, Color color, bool boxed)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface("Segoe UI"), 10, new SolidColorBrush(color), 1.0);
        if (boxed) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xD0, 0x10, 0x12, 0x16)), null, new Rect(x - 1, y, ft.Width + 2, ft.Height));
        dc.DrawText(ft, new Point(x, y));
    }
}
