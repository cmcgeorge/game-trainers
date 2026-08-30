using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 24;
    public const int Border = 26;

    private static readonly Brush BgBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush FloorBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush TownBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush FountainBrush = Frozen(Color.FromRgb(0x79, 0x9B, 0xD7));
    private static readonly Brush ItemBrush = Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush NpcBrush = Frozen(Color.FromRgb(0xB0, 0x70, 0xE0));
    private static readonly Brush EnemyBrush = Frozen(Color.FromRgb(0xC9, 0x5B, 0x5B));
    private static readonly Brush HazardBrush = Frozen(Color.FromRgb(0xD0, 0x9A, 0x3F));
    private static readonly Brush StartBrush = Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8));
    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);
    private static readonly Typeface Mono = new("Consolas");

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    public static ImageSource Render(AreaLevel level)
    {
        int width = level.Width, height = level.Height;
        int pixelWidth = PixelWidth(width), pixelHeight = PixelHeight(height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, pixelWidth, pixelHeight));
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dc.DrawRectangle(level.Grid[x, y] == CellKind.Wall ? WallBrush : FloorBrush, null, CellRect(x, y));
            DrawGridLines(dc, width, height);
            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                dc.DrawRectangle(brush, null, rect);
                DrawLabel(dc, rect, label);
            }
        }
        var bmp = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static (Brush brush, string label) PoiStyle(string name) => name switch
    {
        var n when n.Contains("Starting") => (StartBrush, "S"),
        var n when n.Contains("Gate") || n.Contains("Road") || n.Contains("Entry") || n.Contains("Exit") => (TownBrush, "T"),
        var n when n.Contains("Fountain") => (FountainBrush, "F"),
        var n when n.Contains("Cache") || n.Contains("Relic") || n.Contains("Supplies") => (ItemBrush, "I"),
        var n when n.Contains("Raider") || n.Contains("Robot") || n.Contains("Mutant") => (EnemyBrush, "E"),
        var n when n.Contains("Radiation") || n.Contains("Hazard") || n.Contains("Toxic") || n.Contains("Contaminated") => (HazardBrush, "X"),
        _ => (NpcBrush, "N"),
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGridLines(DrawingContext dc, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double px = Border + x * Cell + 0.5;
            dc.DrawLine(x % 5 == 0 ? ThickPen : ThinPen, new Point(px, Border), new Point(px, Border + Cell * height));
            if (x % 5 == 0 && x < width) dc.DrawText(Ruler(x), new Point(px + 2, Border - 16));
        }
        for (int y = 0; y <= height; y++)
        {
            double py = Border + y * Cell + 0.5;
            dc.DrawLine(y % 5 == 0 ? ThickPen : ThinPen, new Point(Border, py), new Point(Border + Cell * width, py));
            if (y % 5 == 0 && y < height) dc.DrawText(Ruler(y), new Point(Border - 18, py + 2));
        }
    }

    private static FormattedText Ruler(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Mono, 10, RulerBrush, VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

    private static void DrawLabel(DrawingContext dc, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12, BgBrush,
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
        dc.DrawText(text, new Point(rect.X + (Cell - text.Width) / 2, rect.Y + (Cell - text.Height) / 2));
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
