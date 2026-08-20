using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>
/// Draws one of the game's areas the way players drew them on graph paper: walls on the cell
/// edges, doorways as marked gaps, an arrow on the one-way ones, and — in Skara Brae, which
/// has no edge walls at all — an outline traced around every building and along the city rim.
///
/// <para>The geometry (<see cref="Cell"/>, <see cref="Border"/>) is deliberately unchanged
/// from the plain counting grid this replaced: the user's calibration anchors are stored in
/// image pixels, so moving a cell would silently invalidate every saved calibration.</para>
/// </summary>
public static class MapRenderer
{
    /// <summary>Pixels per cell — big enough to click accurately.</summary>
    public const int Cell = 24;

    /// <summary>Margin around the grid for the ruler labels.</summary>
    public const int Border = 26;

    private static readonly Brush Background = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Frozen(Color.FromRgb(0x1E, 0x1F, 0x26));
    private static readonly Brush Blocked = Frozen(Color.FromRgb(0x0C, 0x0D, 0x11));
    private static readonly Brush Feature = Frozen(Color.FromRgb(0x2C, 0x3A, 0x4A));
    private static readonly Brush RulerBrush = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush GlyphBrush = Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8));
    private static readonly Brush ArrowBrush = Frozen(Color.FromRgb(0x6F, 0xC2, 0x76));

    private static readonly Pen ThinPen = FrozenPen(Color.FromRgb(0x2E, 0x30, 0x38), 1);
    private static readonly Pen ThickPen = FrozenPen(Color.FromRgb(0x4A, 0x4D, 0x5A), 1);
    private static readonly Pen WallPen = FrozenPen(Color.FromRgb(0xD2, 0xD6, 0xE0), 2.5);
    private static readonly Pen DoorPen = FrozenPen(Color.FromRgb(0x6F, 0xC2, 0x76), 2.5);
    private static readonly Pen SecretPen = FrozenPen(Color.FromRgb(0xC8, 0x9B, 0x3C), 2.5, dashed: true);

    private static readonly Typeface Mono = new("Consolas");

    public static int PixelWidth(int width) => Border * 2 + Cell * width;
    public static int PixelHeight(int height) => Border * 2 + Cell * height;

    /// <summary>Renders a whole area to a frozen bitmap, ready to bind to an Image.</summary>
    public static ImageSource Render(BoardSquare[,] board, int width, int height)
    {
        int wPx = PixelWidth(width), hPx = PixelHeight(height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, wPx, hPx));
            dc.DrawRectangle(Floor, null, new Rect(Border, Border, Cell * width, Cell * height));

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var square = board[x, y];
                    if (square.Feature == SquareFeature.Open) continue;
                    dc.DrawRectangle(square.IsBlocked ? Blocked : Feature, null, CellRect(x, y));
                }

            DrawGridLines(dc, width, height);

            // Only the city grid carries buildings; a dungeon's barriers are all on its edges,
            // and tracing its rim as well would double the outer wall.
            bool hasBuildings = HasBuildings(board, width, height);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var square = board[x, y];
                    DrawLabel(dc, CellRect(x, y), square.Label);
                    DrawWalls(dc, CellRect(x, y), square);
                    if (hasBuildings) DrawBarriers(dc, CellRect(x, y), board, width, height, x, y);
                }
        }

        var bmp = new RenderTargetBitmap(wPx, hPx, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static Rect CellRect(int x, int y) => new(Border + x * Cell, Border + y * Cell, Cell, Cell);

    /// <summary>
    /// The counting grid and its rulers: a light line per cell, a heavier one every five, and
    /// the index along the top and left edges. The rulers count from the image's top-left
    /// corner and are only an aid — the calibration anchors define the real game coordinates,
    /// including which way each axis runs.
    /// </summary>
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
            if (y % 5 == 0 && y < height) dc.DrawText(Ruler(y), new Point(4, py + 2));
        }
    }

    /// <summary>
    /// The walls a dungeon square records. Only its west and north edges are drawn plus the
    /// map's own east and south rim, because an interior east or south edge belongs to the
    /// neighbour — see <see cref="BoardSquare"/>.
    /// </summary>
    private static void DrawWalls(DrawingContext dc, Rect r, BoardSquare square)
    {
        DrawSide(dc, square.West, new Point(r.Left, r.Top), new Point(r.Left, r.Bottom), vertical: true);
        DrawSide(dc, square.North, new Point(r.Left, r.Top), new Point(r.Right, r.Top), vertical: false);
        DrawSide(dc, square.East, new Point(r.Right, r.Top), new Point(r.Right, r.Bottom), vertical: true);
        DrawSide(dc, square.South, new Point(r.Left, r.Bottom), new Point(r.Right, r.Bottom), vertical: false);
    }

    /// <summary>
    /// Traces the barriers of a city square. Skara Brae has no edge walls: what stops the
    /// party is a whole square being a building, plus the rim of the city itself. Each edge is
    /// drawn from its open side only, so the inside of a building stays clean and no barrier
    /// is painted twice.
    /// </summary>
    private static void DrawBarriers(DrawingContext dc, Rect r, BoardSquare[,] board,
        int width, int height, int x, int y)
    {
        if (board[x, y].IsBlocked) return;   // the open neighbours draw this square's outline

        if (IsBarrier(board, width, height, x, y - 1)) dc.DrawLine(WallPen, new Point(r.Left, r.Top), new Point(r.Right, r.Top));
        if (IsBarrier(board, width, height, x, y + 1)) dc.DrawLine(WallPen, new Point(r.Left, r.Bottom), new Point(r.Right, r.Bottom));
        if (IsBarrier(board, width, height, x - 1, y)) dc.DrawLine(WallPen, new Point(r.Left, r.Top), new Point(r.Left, r.Bottom));
        if (IsBarrier(board, width, height, x + 1, y)) dc.DrawLine(WallPen, new Point(r.Right, r.Top), new Point(r.Right, r.Bottom));
    }

    /// <summary>True when the party cannot cross into (x, y): a building, or off the map.</summary>
    private static bool IsBarrier(BoardSquare[,] board, int width, int height, int x, int y) =>
        x < 0 || y < 0 || x >= width || y >= height || board[x, y].IsBlocked;

    private static bool HasBuildings(BoardSquare[,] board, int width, int height)
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (board[x, y].IsBlocked) return true;
        return false;
    }

    /// <summary>
    /// One edge. A solid wall runs the whole way; a doorway leaves the middle third marked in
    /// its own colour so an opening shows at a glance, and a one-way doorway gets an arrow
    /// pointing the way it opens.
    /// </summary>
    private static void DrawSide(DrawingContext dc, WallKind kind, Point a, Point b, bool vertical)
    {
        if (kind == WallKind.None) return;
        if (kind == WallKind.Wall) { dc.DrawLine(WallPen, a, b); return; }

        var third = new Point(a.X + (b.X - a.X) / 3.0, a.Y + (b.Y - a.Y) / 3.0);
        var twoThirds = new Point(a.X + (b.X - a.X) * 2.0 / 3.0, a.Y + (b.Y - a.Y) * 2.0 / 3.0);
        dc.DrawLine(WallPen, a, third);
        dc.DrawLine(WallPen, twoThirds, b);
        dc.DrawLine(kind.IsSecret() ? SecretPen : DoorPen, third, twoThirds);

        int sign = kind.OneWaySign();
        if (sign != 0) DrawArrow(dc, new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), vertical, sign);
    }

    /// <summary>
    /// A small filled arrowhead beside a one-way doorway, pointing the only way through it:
    /// east or west across a vertical edge, south or north across a horizontal one.
    /// </summary>
    private static void DrawArrow(DrawingContext dc, Point mid, bool vertical, int sign)
    {
        const double Length = 6, HalfWidth = 3.5;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            Point tip = vertical ? new Point(mid.X + Length * sign, mid.Y) : new Point(mid.X, mid.Y + Length * sign);
            Point back1 = vertical ? new Point(mid.X, mid.Y - HalfWidth) : new Point(mid.X - HalfWidth, mid.Y);
            Point back2 = vertical ? new Point(mid.X, mid.Y + HalfWidth) : new Point(mid.X + HalfWidth, mid.Y);
            ctx.BeginFigure(tip, isFilled: true, isClosed: true);
            ctx.LineTo(back1, isStroked: false, isSmoothJoin: false);
            ctx.LineTo(back2, isStroked: false, isSmoothJoin: false);
        }
        geometry.Freeze();
        dc.DrawGeometry(ArrowBrush, null, geometry);
    }

    private static void DrawLabel(DrawingContext dc, Rect rect, string? label)
    {
        if (label == null) return;
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Mono, 9, GlyphBrush, 1.0);
        dc.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2,
                                    rect.Y + (rect.Height - text.Height) / 2));
    }

    private static FormattedText Ruler(int value) =>
        new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Mono, 11, RulerBrush, 1.0);

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double thickness, bool dashed = false)
    {
        var pen = new Pen(Frozen(c), thickness);
        if (dashed) pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
        pen.Freeze();
        return pen;
    }
}
