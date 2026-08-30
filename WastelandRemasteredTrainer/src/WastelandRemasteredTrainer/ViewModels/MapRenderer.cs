using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WastelandRemasteredTrainer.Game;

namespace WastelandRemasteredTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 22;
    public const int Border = 24;

    private static readonly Brush Background = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush Wall = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush Ruler = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush Text = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Pen Thin = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38));
    private static readonly Pen Thick = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A));
    private static readonly Typeface Mono = new("Consolas");

    public static ImageSource Render(AreaLevel map)
    {
        int width = Border * 2 + Cell * map.Width;
        int height = Border * 2 + Cell * map.Height;
        var visual = new DrawingVisual();
        double pixelsPerDip = VisualTreeHelper.GetDpi(visual).PixelsPerDip;
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    dc.DrawRectangle(map.Grid[x, y] == CellKind.Wall ? Wall : Floor, null, CellRect(x, y));

            DrawGrid(dc, map.Width, map.Height, pixelsPerDip);
            foreach (var poi in map.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                dc.DrawRectangle(PoiBrush(poi.Symbol), null, rect);
                var label = new FormattedText(poi.Symbol, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Mono, 12, Text,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                dc.DrawText(label, new Point(rect.X + (Cell - label.Width) / 2, rect.Y + (Cell - label.Height) / 2));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGrid(DrawingContext dc, int width, int height, double pixelsPerDip)
    {
        for (int x = 0; x <= width; x++)
        {
            double px = Border + x * Cell + 0.5;
            dc.DrawLine(x % 5 == 0 ? Thick : Thin, new Point(px, Border), new Point(px, Border + Cell * height));
            if (x % 5 == 0 && x < width) dc.DrawText(RulerText(x, pixelsPerDip), new Point(px + 2, Border - 16));
        }
        for (int y = 0; y <= height; y++)
        {
            double py = Border + y * Cell + 0.5;
            dc.DrawLine(y % 5 == 0 ? Thick : Thin, new Point(Border, py), new Point(Border + Cell * width, py));
            if (y % 5 == 0 && y < height) dc.DrawText(RulerText(y, pixelsPerDip), new Point(Border - 18, py + 2));
        }
    }

    private static FormattedText RulerText(int value, double pixelsPerDip) => new(value.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 10, Ruler, pixelsPerDip);

    private static Brush PoiBrush(string symbol) => symbol switch
    {
        "R" => Frozen(Color.FromRgb(0x6F, 0xC2, 0x76)),
        "T" => Frozen(Color.FromRgb(0x79, 0xB9, 0xC8)),
        "D" => Frozen(Color.FromRgb(0xA4, 0x8A, 0xD4)),
        "I" => Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C)),
        "N" => Frozen(Color.FromRgb(0x79, 0x9B, 0xD7)),
        "E" => Frozen(Color.FromRgb(0xD8, 0x63, 0x63)),
        "S" => Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8)),
        "B" => Frozen(Color.FromRgb(0xDE, 0x84, 0x36)),
        _ => Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8)),
    };

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1);
        pen.Freeze();
        return pen;
    }
}
