using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.ViewModels;

/// <summary>
/// Renders a <see cref="DungeonLevel"/> as a WPF bitmap, drawing walls as filled cells
/// and floor as open space, with markers for the points of interest (stairs, elevator,
/// the Blue Ribbon, the Amulet).
/// </summary>
public static class MapRenderer
{
    public const int Cell = 24;
    public const int Border = 26;

    private static readonly Brush BgBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush FloorBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush PoiBrush = Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8));
    private static readonly Brush StairsBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush ElevatorBrush = Frozen(Color.FromRgb(0x79, 0x9B, 0xD7));
    private static readonly Brush ItemBrush = Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush AmuletBrush = Frozen(Color.FromRgb(0xE0, 0xB0, 0x40));
    private static readonly Brush StartBrush = Frozen(Color.FromRgb(0xB0, 0x70, 0xE0));

    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);

    private static readonly Typeface Mono = new("Consolas");

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    public static ImageSource Render(DungeonLevel level)
    {
        int w = level.Width, h = level.Height;
        int wPx = PixelWidth(w), hPx = PixelHeight(h);
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, wPx, hPx));

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var rect = CellRect(x, y);
                    var cell = level.Grid[x, y];
                    dc.DrawRectangle(cell == CellKind.Wall ? WallBrush : FloorBrush, null, rect);
                }

            DrawGridLines(dc, w, h);

            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                dc.DrawRectangle(brush, null, rect);
                DrawLabel(dc, rect, label);
            }
        }

        var bmp = new RenderTargetBitmap(wPx, hPx, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static (Brush brush, string label) PoiStyle(string name) => name switch
    {
        "Party Start" => (StartBrush, "@"),
        "Stairs Up" => (StairsBrush, "U"),
        "Stairs Down" => (StairsBrush, "D"),
        "Elevator" => (ElevatorBrush, "E"),
        "Blue Ribbon" => (ItemBrush, "B"),
        "The Amulet" => (AmuletBrush, "A"),
        _ => (PoiBrush, ""),
    };

    private static Rect CellRect(int x, int y) =>
        new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGridLines(DrawingContext dc, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double px = Border + x * Cell + 0.5;
            dc.DrawLine(x % 5 == 0 ? ThickPen : ThinPen,
                new Point(px, Border), new Point(px, Border + Cell * height));
            if (x % 5 == 0 && x < width)
                dc.DrawText(Ruler(x), new Point(px + 2, Border - 16));
        }

        for (int y = 0; y <= height; y++)
        {
            double py = Border + y * Cell + 0.5;
            dc.DrawLine(y % 5 == 0 ? ThickPen : ThinPen,
                new Point(Border, py), new Point(Border + Cell * width, py));
            if (y % 5 == 0 && y < height)
                dc.DrawText(Ruler(y), new Point(Border - 18, py + 2));
        }
    }

    private static FormattedText Ruler(int value) =>
        new(value.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 10, RulerBrush);

    private static void DrawLabel(DrawingContext dc, Rect rect, string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        var ft = new FormattedText(label, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Mono, 12, BgBrush,
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
        dc.DrawText(ft, new Point(rect.X + (Cell - ft.Width) / 2, rect.Y + (Cell - ft.Height) / 2));
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double width, bool dashed = false)
    {
        var p = new Pen(new SolidColorBrush(c), width);
        if (dashed)
        {
            p.DashStyle = new DashStyle(new double[] { 3, 2 }, 0);
        }
        p.Freeze();
        return p;
    }
}
