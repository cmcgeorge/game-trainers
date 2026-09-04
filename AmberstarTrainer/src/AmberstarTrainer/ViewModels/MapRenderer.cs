using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AmberstarTrainer.Game;

namespace AmberstarTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 28;
    public const int Border = 26;

    private static readonly Brush Background = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush Wall = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush Water = Frozen(Color.FromRgb(0x3B, 0x82, 0xB6));
    private static readonly Brush Mountain = Frozen(Color.FromRgb(0x7A, 0x6D, 0x62));
    private static readonly Brush Forest = Frozen(Color.FromRgb(0x3F, 0x7D, 0x4A));
    private static readonly Brush Desert = Frozen(Color.FromRgb(0xBE, 0x99, 0x4E));
    private static readonly Brush Grid = Frozen(Color.FromRgb(0x4A, 0x4D, 0x5A));
    private static readonly Brush Ruler = Frozen(Color.FromRgb(0xB0, 0xB4, 0xC0));
    private static readonly Brush Poi = Frozen(Color.FromRgb(0xD9, 0xA4, 0x42));
    private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38));
    private static readonly Typeface Mono = new("Consolas");

    private static readonly double PixelsPerDip = 1.0;

    public static ImageSource Render(AreaLevel level)
    {
        int width = Border * 2 + Cell * level.Width;
        int height = Border * 2 + Cell * level.Height;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                {
                    var rect = CellRect(x, y);
                    context.DrawRectangle(Brush(level.Grid[x, y]), null, rect);
                    context.DrawRectangle(null, GridPen, rect);
                }
            DrawRulers(context, level);
            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                context.DrawRectangle(Poi, null, rect);
                DrawLabel(context, rect, Marker(poi.Name));
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush Brush(AreaCellKind kind) => kind switch
    {
        AreaCellKind.Floor => Floor,
        AreaCellKind.Water => Water,
        AreaCellKind.Mountain => Mountain,
        AreaCellKind.Forest => Forest,
        AreaCellKind.Desert => Desert,
        _ => Wall,
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawRulers(DrawingContext context, AreaLevel level)
    {
        for (int x = 0; x < level.Width; x += 5)
            context.DrawText(Text(x), new Point(Border + x * Cell + 2, Border - 16));
        for (int y = 0; y < level.Height; y += 5)
            context.DrawText(Text(y), new Point(Border - 18, Border + y * Cell + 2));
    }

    private static FormattedText Text(int value) => new(value.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 10, Ruler, 1);

    private static void DrawLabel(DrawingContext context, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12,
            Background, PixelsPerDip);
        context.DrawText(text, new Point(rect.X + (Cell - text.Width) / 2, rect.Y + (Cell - text.Height) / 2));
    }

    private static string Marker(string name) => name switch
    {
        "Temple" or "Sun Temple" or "Hidden Shrine" => "T",
        "Tavern" => "V",
        "Shops" or "Shop" => "S",
        "Lord Chile" => "L",
        _ => "•",
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
