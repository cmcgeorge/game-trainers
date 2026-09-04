using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarklandsTrainer.Game;

namespace DarklandsTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 24;
    public const int Border = 26;

    private static readonly Brush BackgroundBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush FloorBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x4A, 0x4D, 0x5A));
    private static readonly Brush ForestBrush = Frozen(Color.FromRgb(0x3E, 0x72, 0x4B));
    private static readonly Brush CityBrush = Frozen(Color.FromRgb(0xB8, 0x72, 0x48));
    private static readonly Brush TownBrush = Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush VillageBrush = Frozen(Color.FromRgb(0xB9, 0xA4, 0x6A));
    private static readonly Brush MonasteryBrush = Frozen(Color.FromRgb(0x95, 0x80, 0xC2));
    private static readonly Brush InnBrush = Frozen(Color.FromRgb(0xD1, 0x88, 0x6A));
    private static readonly Brush CastleBrush = Frozen(Color.FromRgb(0x8C, 0x9C, 0xB8));
    private static readonly Brush DungeonBrush = Frozen(Color.FromRgb(0x90, 0x58, 0x58));
    private static readonly Brush StartBrush = Frozen(Color.FromRgb(0xB0, 0x70, 0xE0));
    private static readonly Brush PoiDefaultBrush = Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8));
    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);
    private static readonly Typeface Mono = new("Consolas");

    private static readonly double PixelsPerDip = 1.0;

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    public static ImageSource Render(AreaLevel level)
    {
        int width = level.Width;
        int height = level.Height;
        int pixelWidth = PixelWidth(width);
        int pixelHeight = PixelHeight(height);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, pixelWidth, pixelHeight));
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    context.DrawRectangle(CellBrush(level.Grid[x, y]), null, CellRect(x, y));
            DrawGridLines(context, width, height);
            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                context.DrawRectangle(PoiBrush(poi.Name), null, rect);
                DrawLabel(context, rect, PoiLabel(poi.Name));
            }
        }
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush CellBrush(CellKind kind) => kind switch
    {
        CellKind.Wall => WallBrush,
        CellKind.Forest => ForestBrush,
        CellKind.City => CityBrush,
        CellKind.Town => TownBrush,
        CellKind.Village => VillageBrush,
        CellKind.Monastery => MonasteryBrush,
        CellKind.Inn => InnBrush,
        CellKind.Castle => CastleBrush,
        CellKind.Dungeon => DungeonBrush,
        CellKind.Start => StartBrush,
        _ => FloorBrush,
    };

    private static Brush PoiBrush(string name) => name switch
    {
        var value when value.Contains("City", StringComparison.Ordinal) || value is "Nuremberg" or "Mainz" or "Cologne" or "Augsburg" or "Regensburg" or "Hamburg" or "Frankfurt" => CityBrush,
        var value when value.Contains("Town", StringComparison.Ordinal) => TownBrush,
        var value when value.Contains("Monastery", StringComparison.Ordinal) => MonasteryBrush,
        var value when value.Contains("Inn", StringComparison.Ordinal) => InnBrush,
        var value when value.Contains("Castle", StringComparison.Ordinal) || value.Contains("Fortress", StringComparison.Ordinal) => CastleBrush,
        var value when value.Contains("Cave", StringComparison.Ordinal) || value.Contains("Temple", StringComparison.Ordinal) || value.Contains("Dungeon", StringComparison.Ordinal) => DungeonBrush,
        var value when value.Contains("Starting", StringComparison.Ordinal) => StartBrush,
        _ => PoiDefaultBrush,
    };

    private static string PoiLabel(string name) => name switch
    {
        "Starting Road" => "S",
        var value when value.Contains("City", StringComparison.Ordinal) || value is "Nuremberg" or "Mainz" or "Cologne" or "Augsburg" or "Regensburg" or "Hamburg" or "Frankfurt" => "C",
        var value when value.Contains("Town", StringComparison.Ordinal) => "T",
        var value when value.Contains("Monastery", StringComparison.Ordinal) => "M",
        var value when value.Contains("Inn", StringComparison.Ordinal) => "I",
        var value when value.Contains("Castle", StringComparison.Ordinal) || value.Contains("Fortress", StringComparison.Ordinal) => "N",
        _ => "D",
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGridLines(DrawingContext context, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double pixel = Border + x * Cell + 0.5;
            context.DrawLine(x % 5 == 0 ? ThickPen : ThinPen, new Point(pixel, Border), new Point(pixel, Border + Cell * height));
            if (x % 5 == 0 && x < width) context.DrawText(Ruler(x), new Point(pixel + 2, Border - 16));
        }
        for (int y = 0; y <= height; y++)
        {
            double pixel = Border + y * Cell + 0.5;
            context.DrawLine(y % 5 == 0 ? ThickPen : ThinPen, new Point(Border, pixel), new Point(Border + Cell * width, pixel));
            if (y % 5 == 0 && y < height) context.DrawText(Ruler(y), new Point(Border - 18, pixel + 2));
        }
    }

    private static FormattedText Ruler(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Mono, 10, RulerBrush, PixelsPerDip);

    private static void DrawLabel(DrawingContext context, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12, LabelBrush,
            PixelsPerDip);
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
