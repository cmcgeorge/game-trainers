using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EyeOfTheBeholder1Trainer.Game;

namespace EyeOfTheBeholder1Trainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 18;
    public const int Border = 26;

    private static readonly Brush BgBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush FloorBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush StairsBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush PortalBrush = Frozen(Color.FromRgb(0x79, 0x9B, 0xD7));
    private static readonly Brush BossBrush = Frozen(Color.FromRgb(0xC8, 0x64, 0x64));
    private static readonly Brush SecretBrush = Frozen(Color.FromRgb(0xB0, 0x70, 0xE0));
    private static readonly Brush NpcBrush = Frozen(Color.FromRgb(0x68, 0xBB, 0xC4));
    private static readonly Brush ItemBrush = Frozen(Color.FromRgb(0xE0, 0xB0, 0x40));
    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);
    private static readonly Typeface Mono = new("Consolas");

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    public static ImageSource Render(DungeonLevel level)
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
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static (Brush brush, string label) PoiStyle(string name) => name switch
    {
        "Stairs Up" => (StairsBrush, "U"),
        "Stairs Down" => (StairsBrush, "D"),
        "Portal" or "Secret Portal" => (PortalBrush, "P"),
        "Xanathar" or "Skeleton King" => (BossBrush, "B"),
        "Secret" => (SecretBrush, "S"),
        "Piergeiron" => (NpcBrush, "N"),
        _ => (ItemBrush, "I"),
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGridLines(DrawingContext dc, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double px = Border + x * Cell + 0.5;
            dc.DrawLine(x % 4 == 0 ? ThickPen : ThinPen, new Point(px, Border), new Point(px, Border + Cell * height));
            if (x % 4 == 0 && x < width) dc.DrawText(Ruler(x), new Point(px + 2, Border - 16));
        }
        for (int y = 0; y <= height; y++)
        {
            double py = Border + y * Cell + 0.5;
            dc.DrawLine(y % 4 == 0 ? ThickPen : ThinPen, new Point(Border, py), new Point(Border + Cell * width, py));
            if (y % 4 == 0 && y < height) dc.DrawText(Ruler(y), new Point(Border - 18, py + 2));
        }
    }

    private static FormattedText Ruler(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Mono, 10, RulerBrush, VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

    private static void DrawLabel(DrawingContext dc, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 11, BgBrush,
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
