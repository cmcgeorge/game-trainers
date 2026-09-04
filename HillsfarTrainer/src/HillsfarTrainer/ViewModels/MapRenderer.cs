using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HillsfarTrainer.Game;

namespace HillsfarTrainer.ViewModels;

public static class MapRenderer
{
    public const int Cell = 24;
    public const int Border = 26;

    private static readonly Brush BackgroundBrush = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush OpenBrush = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush WallBrush = Frozen(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush ShopBrush = Frozen(Color.FromRgb(0x68, 0xA6, 0xD7));
    private static readonly Brush TavernBrush = Frozen(Color.FromRgb(0xB8, 0x72, 0x43));
    private static readonly Brush TempleBrush = Frozen(Color.FromRgb(0xD0, 0xD0, 0xA0));
    private static readonly Brush ArenaBrush = Frozen(Color.FromRgb(0xC6, 0x65, 0x65));
    private static readonly Brush GovernmentBrush = Frozen(Color.FromRgb(0x92, 0x7A, 0xC9));
    private static readonly Brush DocksBrush = Frozen(Color.FromRgb(0x59, 0xA9, 0xB5));
    private static readonly Brush CryptBrush = Frozen(Color.FromRgb(0x8D, 0x8D, 0x9B));
    private static readonly Brush ItemBrush = Frozen(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush NpcBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));
    private static readonly Brush EnemyBrush = Frozen(Color.FromRgb(0xD4, 0x57, 0x57));
    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38));
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A));
    private static readonly Typeface Mono = new("Consolas");

    private static readonly double PixelsPerDip = 1.0;

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    public static ImageSource Render(AreaLevel level)
    {
        int width = level.Width, height = level.Height;
        int pixelWidth = PixelWidth(width), pixelHeight = PixelHeight(height);
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, pixelWidth, pixelHeight));
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    context.DrawRectangle(level.Grid[x, y] == CellKind.Wall ? WallBrush : OpenBrush, null,
                        CellRect(x, y));

            DrawGridLines(context, width, height);
            foreach (var poi in level.Pois)
            {
                var rect = CellRect(poi.X, poi.Y);
                var (brush, label) = PoiStyle(poi.Name);
                context.DrawRectangle(brush, null, rect);
                DrawLabel(context, rect, label);
            }
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static (Brush Brush, string Label) PoiStyle(string name) => name switch
    {
        "Shop" => (ShopBrush, "S"),
        "Tavern" => (TavernBrush, "T"),
        "Temple" => (TempleBrush, "M"),
        "Arena" => (ArenaBrush, "A"),
        "Government" => (GovernmentBrush, "G"),
        "Docks" => (DocksBrush, "D"),
        "Crypt" => (CryptBrush, "C"),
        "Item" => (ItemBrush, "I"),
        "NPC" => (NpcBrush, "N"),
        "Enemy" => (EnemyBrush, "E"),
        _ => (NpcBrush, ""),
    };

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static void DrawGridLines(DrawingContext context, int width, int height)
    {
        for (int x = 0; x <= width; x++)
        {
            double pixel = Border + x * Cell + 0.5;
            context.DrawLine(x % 5 == 0 ? ThickPen : ThinPen,
                new Point(pixel, Border), new Point(pixel, Border + Cell * height));
            if (x % 5 == 0 && x < width)
                context.DrawText(Ruler(x), new Point(pixel + 2, Border - 16));
        }

        for (int y = 0; y <= height; y++)
        {
            double pixel = Border + y * Cell + 0.5;
            context.DrawLine(y % 5 == 0 ? ThickPen : ThinPen,
                new Point(Border, pixel), new Point(Border + Cell * width, pixel));
            if (y % 5 == 0 && y < height)
                context.DrawText(Ruler(y), new Point(Border - 18, pixel + 2));
        }
    }

    private static FormattedText Ruler(int value) => new(value.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 10, RulerBrush, 1);

    private static void DrawLabel(DrawingContext context, Rect rect, string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12,
            BackgroundBrush, PixelsPerDip);
        context.DrawText(text, new Point(rect.X + (Cell - text.Width) / 2, rect.Y + (Cell - text.Height) / 2));
    }

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
