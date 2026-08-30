using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KnightsOfLegendTrainer.Game;

namespace KnightsOfLegendTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 28;
    public const int Border = 26;

    private static readonly Brush Background = Brush(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Brush(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush Wall = Brush(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush Ruler = Brush(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush Town = Brush(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush Castle = Brush(Color.FromRgb(0x79, 0x9B, 0xD7));
    private static readonly Brush Dungeon = Brush(Color.FromRgb(0xA0, 0x70, 0xB8));
    private static readonly Brush Item = Brush(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush Npc = Brush(Color.FromRgb(0x5F, 0xB9, 0xC8));
    private static readonly Brush Enemy = Brush(Color.FromRgb(0xC8, 0x62, 0x62));
    private static readonly Brush Start = Brush(Color.FromRgb(0xB0, 0x70, 0xE0));
    private static readonly Brush Arena = Brush(Color.FromRgb(0xD0, 0x82, 0x4C));
    private static readonly Brush Guild = Brush(Color.FromRgb(0x65, 0xB8, 0xA0));
    private static readonly Pen ThinPen = Pen(Color.FromRgb(0x2E, 0x30, 0x38));
    private static readonly Pen ThickPen = Pen(Color.FromRgb(0x4A, 0x4D, 0x5A));
    private static readonly Typeface Mono = new("Consolas");

    public static ImageSource Render(AreaLevel level)
    {
        int width = Border * 2 + Cell * level.Width;
        int height = Border * 2 + Cell * level.Height;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                    drawing.DrawRectangle(level.Grid[x, y] == CellKind.Wall ? Wall : Floor, null, CellRect(x, y));
            DrawGrid(drawing, level.Width, level.Height);
            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                drawing.DrawRectangle(brush, null, rect);
                DrawLabel(drawing, rect, label);
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static (Brush brush, string label) PoiStyle(string name) => name switch
    {
        "Start" => (Start, "S"),
        "Trading Post" or "Town Gate" or "Lock Gate" or "Barrier Gate" => (Town, "T"),
        "Fortress of Brettle" or "Tower Keep" or "Lord Norgan's Keep" or "Krag Keep" or "Assembly Building" => (Castle, "C"),
        "Forest Dungeon" or "Ghor Dungeon" => (Dungeon, "D"),
        "Quest Item" or "Seggallion's Trail" => (Item, "I"),
        "Quest Givers" or "Quest Contacts" or "Fistan Stockhard" or "Monvin the Elder" or "Ballaster" => (Npc, "N"),
        "Cyclops Patrol" or "Enemy Guard" => (Enemy, "E"),
        "Arena" => (Arena, "A"),
        _ when name.Contains("Guild", StringComparison.Ordinal) || name.Contains("Training", StringComparison.Ordinal) => (Guild, "G"),
        _ => (Floor, ""),
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGrid(DrawingContext drawing, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double px = Border + x * Cell + 0.5;
            drawing.DrawLine(x % 5 == 0 ? ThickPen : ThinPen, new Point(px, Border), new Point(px, Border + Cell * height));
            if (x % 5 == 0 && x < width) drawing.DrawText(RulerText(x), new Point(px + 2, Border - 16));
        }
        for (int y = 0; y <= height; y++)
        {
            double py = Border + y * Cell + 0.5;
            drawing.DrawLine(y % 5 == 0 ? ThickPen : ThinPen, new Point(Border, py), new Point(Border + Cell * width, py));
            if (y % 5 == 0 && y < height) drawing.DrawText(RulerText(y), new Point(Border - 18, py + 2));
        }
    }

    private static FormattedText RulerText(int value) => new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Mono, 10, Ruler, VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

    private static void DrawLabel(DrawingContext drawing, Rect rect, string label)
    {
        if (label.Length == 0) return;
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 13, Background,
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
        drawing.DrawText(text, new Point(rect.X + (Cell - text.Width) / 2, rect.Y + (Cell - text.Height) / 2));
    }

    private static Brush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1);
        pen.Freeze();
        return pen;
    }
}
