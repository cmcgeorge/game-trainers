using System.Globalization;
using System.Text;
using Space1889Trainer.Files;

namespace Space1889Trainer.Cluebooks;

/// <summary>
/// Draws a map as a small SVG schematic: one path per <see cref="TileClass"/> (run-length rectangles, so a
/// city map stays in the tens of kilobytes), labels for exits, shops, entrances and scripted spots, and dots with
/// tooltips for NPCs. No game artwork is copied.
/// </summary>
public static class SchematicSvg
{
    /// <summary>User units per map square.</summary>
    public const int Cell = 6;

    public static string Render(GameMap map, IReadOnlyList<MapAnnotation> annotations, string title)
    {
        ArgumentNullException.ThrowIfNull(map);
        int w = map.Columns * Cell, h = map.Rows * Cell;
        var svg = SvgCanvas.Responsive(w, h, title);
        svg.Title(title);

        var paths = new Dictionary<TileClass, StringBuilder>();
        for (int r = 0; r < map.Rows; r++)
        {
            int c = 0;
            while (c < map.Columns)
            {
                var cls = TileRules.Classify(map[r, c]);
                int start = c;
                while (c < map.Columns && TileRules.Classify(map[r, c]) == cls) c++;
                if (!paths.TryGetValue(cls, out var sb)) paths[cls] = sb = new StringBuilder();
                sb.Append('M').Append(start * Cell).Append(' ').Append(r * Cell)
                  .Append('h').Append((c - start) * Cell).Append('v').Append(Cell)
                  .Append('h').Append(-(c - start) * Cell).Append('z');
            }
        }
        foreach (var (cls, d) in paths.OrderBy(p => p.Key))
            svg.Element("path", ("d", d.ToString()), ("fill", Hex(MapLibrary.SchematicColors[cls])));

        foreach (var a in annotations)
        {
            double x = a.Column * Cell, y = a.Row * Cell;
            if (a.Kind == AnnotationKind.Npc)
            {
                using (svg.Scope("circle", ("cx", x + Cell / 2.0), ("cy", y + Cell / 2.0), ("r", Cell / 2.0), ("fill", "#b3261e")))
                    svg.Title(a.Description);
                continue;
            }
            string color = a.Kind switch
            {
                AnnotationKind.Exit => "#7a5c00",
                AnnotationKind.Service => "#005f5a",
                AnnotationKind.DigSite => "#8a2be2",
                AnnotationKind.Hidden => "#b0306a",
                _ => "#9a3b00",
            };
            using (svg.Scope("text", ("x", x), ("y", Math.Max(9, y - 1)), ("font-size", 9), ("font-family", "sans-serif"),
                                     ("fill", color), ("stroke", "#ffffff"), ("stroke-width", 2.5), ("paint-order", "stroke")))
            {
                svg.Title(a.Description);
                svg.Content(a.Label);
            }
        }
        return svg.ToSvg();
    }

    private static string Hex(int rgb) => "#" + rgb.ToString("x6", CultureInfo.InvariantCulture);
}
