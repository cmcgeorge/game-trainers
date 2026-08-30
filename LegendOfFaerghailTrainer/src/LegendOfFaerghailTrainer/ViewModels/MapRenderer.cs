using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.ViewModels;

public static class MapRenderer
{
    private const int Cell = 24;
    private const int Border = 26;
    private static readonly Brush Background = Brush(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Brush(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush Wall = Brush(Color.FromRgb(0x3A, 0x3D, 0x4A));
    private static readonly Brush Poi = Brush(Color.FromRgb(0xC8, 0x9B, 0x3C));
    private static readonly Brush Text = Brush(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Pen Thin = Pen(Color.FromRgb(0x2E, 0x30, 0x38));
    private static readonly Pen Thick = Pen(Color.FromRgb(0x4A, 0x4D, 0x5A));

    public static ImageSource Render(AreaLevel map)
    {
        int width = Border * 2 + Cell * map.Width;
        int height = Border * 2 + Cell * map.Height;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    dc.DrawRectangle(map.Grid[x, y] == CellKind.Wall ? Wall : Floor, null, CellRect(x, y));
            for (int i = 0; i <= 20; i++)
            {
                var pen = i % 5 == 0 ? Thick : Thin;
                dc.DrawLine(pen, new Point(Border + i * Cell + .5, Border), new Point(Border + i * Cell + .5, Border + 20 * Cell));
                dc.DrawLine(pen, new Point(Border, Border + i * Cell + .5), new Point(Border + 20 * Cell, Border + i * Cell + .5));
            }
            foreach (var poi in map.Pois)
            {
                Rect rect = CellRect(poi.X, poi.Y);
                dc.DrawRectangle(Poi, null, rect);
                var label = new FormattedText("•", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 18, Text, 1);
                dc.DrawText(label, new Point(rect.X + 7, rect.Y + 1));
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    private static Brush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(Color color)
    {
        var pen = new Pen(Brush(color), 1);
        pen.Freeze();
        return pen;
    }
}
