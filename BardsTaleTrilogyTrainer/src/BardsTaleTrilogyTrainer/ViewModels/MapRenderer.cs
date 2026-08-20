using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BardsTaleTrilogyTrainer.Game;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>
/// Draws a decoded <see cref="MapGrid"/> the way the games' own graph-paper maps looked:
/// walls on cell edges, doors as gaps, and a glyph for anything that happens when the party
/// steps on a square.
///
/// <para>Map coordinates run X east and Z north from a south-west origin, so Z is flipped
/// when it becomes a pixel row — north ends up at the top, as on paper.</para>
/// </summary>
public static class MapRenderer
{
    /// <summary>Pixels per map square. Big enough to click a single cell accurately.</summary>
    public const int Cell = 22;

    /// <summary>Margin around the grid for the coordinate rulers.</summary>
    public const int Border = 24;

    // Palette, matching the trainer's dark shell.
    private static readonly Brush Background = Frozen(Color.FromRgb(0x14, 0x15, 0x1A));
    private static readonly Brush Floor = Frozen(Color.FromRgb(0x24, 0x26, 0x2E));
    private static readonly Brush Blocked = Frozen(Color.FromRgb(0x15, 0x16, 0x1C));
    private static readonly Brush Ruler = Frozen(Color.FromRgb(0x8A, 0x8D, 0x99));
    private static readonly Brush ModuleFill = Frozen(Color.FromRgb(0x2C, 0x3A, 0x4A));
    private static readonly Brush StairsFill = Frozen(Color.FromRgb(0x2E, 0x46, 0x32));
    private static readonly Brush HazardFill = Frozen(Color.FromRgb(0x46, 0x2A, 0x2A));
    private static readonly Brush SpecialFill = Frozen(Color.FromRgb(0x3A, 0x33, 0x18));
    private static readonly Brush DarkFill = Frozen(Color.FromRgb(0x1A, 0x1A, 0x24));
    private static readonly Brush GlyphBrush = Frozen(Color.FromRgb(0xE0, 0xE2, 0xE8));

    private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x30, 0x32, 0x3C), 1);
    private static readonly Pen WallPen = FrozenPen(Color.FromRgb(0xD2, 0xD6, 0xE0), 2.5);
    private static readonly Pen SecretPen = FrozenPen(Color.FromRgb(0xC8, 0x9B, 0x3C), 2.5, dashed: true);
    private static readonly Pen InvisiblePen = FrozenPen(Color.FromRgb(0x6A, 0x7A, 0x9A), 2.0, dashed: true);
    private static readonly Pen RailingPen = FrozenPen(Color.FromRgb(0x8A, 0x8D, 0x99), 1.5, dashed: true);
    private static readonly Pen DoorPen = FrozenPen(Color.FromRgb(0x6F, 0xC2, 0x76), 2.5);
    private static readonly Pen LockedDoorPen = FrozenPen(Color.FromRgb(0xE0, 0x6C, 0x6C), 2.5);
    private static readonly Pen CrumblingPen = FrozenPen(Color.FromRgb(0xA0, 0x88, 0x60), 2.5, dashed: true);

    private static readonly Typeface Mono = new("Consolas");

    /// <summary>Pixel centre of a map square.</summary>
    public static (double X, double Y) CellToPixel(MapGrid grid, int x, int z) =>
        (Border + x * Cell + Cell / 2.0,
         Border + (grid.Height - 1 - z) * Cell + Cell / 2.0);

    /// <summary>The map square under an image pixel.</summary>
    public static (int X, int Z) PixelToCell(MapGrid grid, double px, double py)
    {
        int col = (int)Math.Floor((px - Border) / Cell);
        int row = (int)Math.Floor((py - Border) / Cell);
        return (col, grid.Height - 1 - row);
    }

    public static int PixelWidth(MapGrid grid) => Border * 2 + Cell * grid.Width;
    public static int PixelHeight(MapGrid grid) => Border * 2 + Cell * grid.Height;

    /// <summary>Renders the whole grid to a frozen bitmap, ready to bind to an Image.</summary>
    public static ImageSource Render(MapGrid grid)
    {
        int w = PixelWidth(grid), h = PixelHeight(grid);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
            DrawRulers(dc, grid);

            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var rect = CellRect(grid, x, z);
                    var cell = grid[x, z];
                    dc.DrawRectangle(FillFor(grid, cell), GridPen, rect);
                    DrawGlyph(dc, rect, grid, cell);
                    if (grid.IsDungeon) DrawWalls(dc, rect, cell);
                }
            }
        }

        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static Rect CellRect(MapGrid grid, int x, int z) =>
        new(Border + x * Cell, Border + (grid.Height - 1 - z) * Cell, Cell, Cell);

    /// <summary>
    /// Cell background. Dungeon squares are tinted by what happens on them — stairs, hazards,
    /// specials, darkness; city squares by whether they are street, building or a service.
    /// </summary>
    private static Brush FillFor(MapGrid grid, MapCell cell)
    {
        if (!grid.IsDungeon)
        {
            if (cell.Module != CityModule.None && cell.Module != CityModule.Generic) return ModuleFill;
            return cell.IsBlocked ? Blocked : Floor;
        }

        if (cell.HasStairs) return StairsFill;
        if ((cell.Flags & (CellFlags.HarmParty | CellFlags.PoisonGas | CellFlags.DrainMagic |
                           CellFlags.Turncoat | CellFlags.Flypaper | CellFlags.RandomTrap)) != 0)
            return HazardFill;
        if ((cell.Flags & (CellFlags.SpecialAhead | CellFlags.Spinner | CellFlags.Secret |
                           CellFlags.PresetCombat | CellFlags.Runes)) != 0)
            return SpecialFill;
        if (cell.Flags.HasFlag(CellFlags.Darkness)) return DarkFill;
        return Floor;
    }

    /// <summary>A single character marking what is on the square, when there is something.</summary>
    private static void DrawGlyph(DrawingContext dc, Rect rect, MapGrid grid, MapCell cell)
    {
        string? glyph = grid.IsDungeon ? DungeonGlyph(cell) : MapFileParser.ModuleLabel(cell.Module);
        if (glyph == null) return;

        double size = glyph.Length > 1 ? 8.5 : 12;
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Mono, size, GlyphBrush, 1.0);
        dc.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2,
                                    rect.Y + (rect.Height - text.Height) / 2));
    }

    private static string? DungeonGlyph(MapCell cell)
    {
        // Most specific first: one square can carry several flags, and the reason a mapper
        // cares most about is the one that moves or hurts the party.
        if (cell.Flags.HasFlag(CellFlags.StairsOut)) return "↑";      // up and out
        if (cell.Flags.HasFlag(CellFlags.StairsIn)) return "↓";       // down, deeper in
        if (cell.Flags.HasFlag(CellFlags.PortalUp)) return "⇑";
        if (cell.Flags.HasFlag(CellFlags.PortalDown)) return "⇓";
        if (cell.Flags.HasFlag(CellFlags.Spinner)) return "↻";
        if (cell.Flags.HasFlag(CellFlags.Flypaper)) return "✖";
        if (cell.Flags.HasFlag(CellFlags.Turncoat)) return "⚔";
        if (cell.Flags.HasFlag(CellFlags.PoisonGas)) return "☠";
        if (cell.Flags.HasFlag(CellFlags.HarmParty)) return "–";
        if (cell.Flags.HasFlag(CellFlags.DrainMagic)) return "⊖";
        if (cell.Flags.HasFlag(CellFlags.RegenMagic)) return "⊕";
        if (cell.Flags.HasFlag(CellFlags.RegenHealth)) return "❤";
        if (cell.Flags.HasFlag(CellFlags.RandomTrap)) return "⚠";
        if (cell.Flags.HasFlag(CellFlags.PresetCombat)) return "☠";
        if (cell.Flags.HasFlag(CellFlags.Runes)) return "ᚱ";
        if (cell.Flags.HasFlag(CellFlags.SpecialAhead)) return "•";
        if (cell.Flags.HasFlag(CellFlags.Secret)) return "?";
        if (cell.Flags.HasFlag(CellFlags.AntiMagic)) return "Ø";
        if (cell.Flags.HasFlag(CellFlags.SilenceBard)) return "♪";
        if (cell.Flags.HasFlag(CellFlags.Darkness)) return "●";
        return null;
    }

    /// <summary>
    /// Draws the four sides of a dungeon square. Only the north and west sides of each cell
    /// plus the outer south/east border are drawn, so a shared wall is not painted twice.
    /// </summary>
    private static void DrawWalls(DrawingContext dc, Rect r, MapCell cell)
    {
        DrawSide(dc, cell.North, new Point(r.Left, r.Top), new Point(r.Right, r.Top));
        DrawSide(dc, cell.West, new Point(r.Left, r.Top), new Point(r.Left, r.Bottom));
        DrawSide(dc, cell.South, new Point(r.Left, r.Bottom), new Point(r.Right, r.Bottom));
        DrawSide(dc, cell.East, new Point(r.Right, r.Top), new Point(r.Right, r.Bottom));
    }

    /// <summary>
    /// One side. Solid walls run the full edge; doors leave the middle third open and mark
    /// the gap, so an opening is visible at a glance even at this size.
    /// </summary>
    private static void DrawSide(DrawingContext dc, WallKind kind, Point a, Point b)
    {
        switch (kind)
        {
            case WallKind.None:
                return;

            case WallKind.Solid:
                dc.DrawLine(WallPen, a, b);
                return;

            case WallKind.InvisibleWall:
                dc.DrawLine(InvisiblePen, a, b);
                return;

            case WallKind.Railing:
                dc.DrawLine(RailingPen, a, b);
                return;

            case WallKind.CrumblingWall:
                dc.DrawLine(CrumblingPen, a, b);
                return;

            case WallKind.SecretDoor:
                DrawDoorway(dc, a, b, SecretPen, WallPen);
                return;

            case WallKind.LockedDoor:
                DrawDoorway(dc, a, b, LockedDoorPen, WallPen);
                return;

            case WallKind.Door:
                DrawDoorway(dc, a, b, DoorPen, WallPen);
                return;

            default:
                dc.DrawLine(WallPen, a, b);
                return;
        }
    }

    /// <summary>Wall stubs at each end with the doorway itself marked in between.</summary>
    private static void DrawDoorway(DrawingContext dc, Point a, Point b, Pen doorPen, Pen wallPen)
    {
        var third = new Point(a.X + (b.X - a.X) / 3.0, a.Y + (b.Y - a.Y) / 3.0);
        var twoThirds = new Point(a.X + (b.X - a.X) * 2.0 / 3.0, a.Y + (b.Y - a.Y) * 2.0 / 3.0);
        dc.DrawLine(wallPen, a, third);
        dc.DrawLine(wallPen, twoThirds, b);
        dc.DrawLine(doorPen, third, twoThirds);
    }

    /// <summary>Coordinate rulers: X along the bottom, Z up the left, every five squares.</summary>
    private static void DrawRulers(DrawingContext dc, MapGrid grid)
    {
        for (int x = 0; x < grid.Width; x += 5)
        {
            var t = Label(x);
            dc.DrawText(t, new Point(Border + x * Cell + (Cell - t.Width) / 2,
                                     Border + grid.Height * Cell + 4));
        }
        for (int z = 0; z < grid.Height; z += 5)
        {
            var t = Label(z);
            dc.DrawText(t, new Point(Border - t.Width - 4,
                                     Border + (grid.Height - 1 - z) * Cell + (Cell - t.Height) / 2));
        }
    }

    private static FormattedText Label(int value) =>
        new(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Mono, 10, Ruler, 1.0);

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double thickness, bool dashed = false)
    {
        var p = new Pen(Frozen(c), thickness);
        if (dashed) p.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
        p.Freeze();
        return p;
    }
}
