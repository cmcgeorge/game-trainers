using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Questron2Trainer.Game;

namespace Questron2Trainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 24;
    public const int Border = 26;

    private static readonly Brush BgBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush FloorBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush TownBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush DungeonBrush = Frozen(Color.FromRgb(0x79, 0x9B, 0xD7));
    private static readonly Brush ItemBrush = Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush BossBrush = Frozen(Color.FromRgb(0xD7, 0x70, 0x70));
    private static readonly Brush CastleBrush = Frozen(Color.FromRgb(0xB0, 0x70, 0xE0));
    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);
    private static readonly Typeface Mono = new("Consolas");

    public static ImageSource Render(AreaLevel area)
    {
        int width = Border * 2 + Cell * area.Width;
        int height = Border * 2 + Cell * area.Height;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, width, height));
            for (int y = 0; y < area.Height; y++)
                for (int x = 0; x < area.Width; x++)
                    dc.DrawRectangle(area.Grid[x, y] == CellKind.Wall ? WallBrush : FloorBrush, null, CellRect(x, y));
            DrawGridLines(dc, area.Width, area.Height);
            foreach (var poi in area.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                dc.DrawRectangle(brush, null, rect);
                DrawLabel(dc, rect, label);
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static (Brush brush, string label) PoiStyle(string name) => name switch
    {
        "Stairs Up" => (DungeonBrush, "U"),
        "Stairs Down" => (DungeonBrush, "D"),
        var n when n.Contains("Town") || n.Contains("Gate") || n.Contains("Trail") || n.Contains("Road") => (TownBrush, "T"),
        var n when n.Contains("Dungeon") || n.Contains("Entrance") || n.Contains("Passage") => (DungeonBrush, "D"),
        var n when n.Contains("Boss") || n.Contains("Gargoyle") || n.Contains("Keeper") => (BossBrush, "B"),
        var n when n.Contains("Castle") => (CastleBrush, "C"),
        var n when n.Contains("Shore") || n.Contains("Harbor") => (TownBrush, "S"),
        var n when n.Contains("Cache") || n.Contains("Relic") || n.Contains("Treasure") => (ItemBrush, "I"),
        _ => (ItemBrush, "N"),
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

    private static FormattedText Ruler(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 10, RulerBrush, 1);

    private static void DrawLabel(DrawingContext dc, Rect rect, string label)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12, BgBrush, VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
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
