using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoduelTrainer.Game;

namespace AutoduelTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 32;
    public const int Border = 28;

    private static readonly Brush Background = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Open = Frozen(Color.FromRgb(0x24, 0x28, 0x30));
    private static readonly Brush Road = Frozen(Color.FromRgb(0x5A, 0x52, 0x44));
    private static readonly Brush Wall = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush Ruler = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Typeface Mono = new("Consolas");

    public static ImageSource Render(AreaLevel area)
    {
        int width = Border * 2 + Cell * area.Width;
        int height = Border * 2 + Cell * area.Height;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            for (int y = 0; y < area.Height; y++)
                for (int x = 0; x < area.Width; x++)
                {
                    var rect = CellRect(x, y);
                    context.DrawRectangle(area.Grid[x, y] switch
                    {
                        CellKind.Wall => Wall,
                        CellKind.Road => Road,
                        _ => Open,
                    }, null, rect);
                    context.DrawRectangle(null, GridPen, rect);
                }
            foreach (var poi in area.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                context.DrawRectangle(brush, null, rect);
                DrawLabel(context, rect, label);
            }
            DrawRulers(context, area.Width, area.Height);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static (Brush Brush, string Label) PoiStyle(string name) => name switch
    {
        _ when name.Contains("Arena", StringComparison.Ordinal) => (Frozen(Color.FromRgb(0xCF, 0x72, 0x52)), "A"),
        _ when name.Contains("Shop", StringComparison.Ordinal) || name.Contains("Plant", StringComparison.Ordinal) || name.Contains("Dealer", StringComparison.Ordinal) || name.Contains("Upgrade", StringComparison.Ordinal) => (Frozen(Color.FromRgb(0x75, 0xB8, 0xD0)), "S"),
        _ when name.Contains("Truck Stop", StringComparison.Ordinal) => (Frozen(Color.FromRgb(0x76, 0xC2, 0x8A)), "T"),
        _ when name.Contains("Cargo", StringComparison.Ordinal) || name.Contains("Cache", StringComparison.Ordinal) || name.Contains("Wreckage", StringComparison.Ordinal) || name.Contains("Salvage", StringComparison.Ordinal) => (Frozen(Color.FromRgb(0xD5, 0xB8, 0x52)), "I"),
        _ when name.Contains("City", StringComparison.Ordinal) || name.Contains("Town", StringComparison.Ordinal) || name is "New York" or "Boston" or "Chicago" or "Los Angeles" or "Detroit" or "Houston" or "Watertown" or "Manchester" or "Albany" or "Buffalo" or "Pittsburgh" or "Philadelphia" or "Baltimore" or "Washington" => (Frozen(Color.FromRgb(0xB0, 0x70, 0xE0)), "C"),
        _ => (Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8)), "N"),
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawRulers(DrawingContext context, int width, int height)
    {
        for (int x = 0; x < width; x++) context.DrawText(Text(x), new Point(Border + x * Cell + 2, 7));
        for (int y = 0; y < height; y++) context.DrawText(Text(y), new Point(5, Border + y * Cell + 7));
    }

    private static FormattedText Text(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Mono, 10, Ruler, VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

    private static void DrawLabel(DrawingContext context, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 14, Background,
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
        context.DrawText(text, new Point(rect.X + (Cell - text.Width) / 2, rect.Y + (Cell - text.Height) / 2));
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color, double width)
    {
        var pen = new Pen(new SolidColorBrush(color), width);
        pen.Freeze();
        return pen;
    }
}
