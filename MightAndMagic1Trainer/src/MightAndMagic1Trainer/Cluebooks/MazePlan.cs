using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Cluebooks;

/// <summary>A square a plan should point at: where it is, what to print in it, and what it is.</summary>
/// <param name="X">Column, 0-15 west to east.</param>
/// <param name="Y">Row, 0-15 south to north.</param>
/// <param name="Label">One or two characters to print in the square. The plain-text plan uses the first.</param>
/// <param name="Title">What is there, for a tooltip.</param>
public sealed record PlanMarker(int X, int Y, string Label, string Title);

/// <summary>
/// Draws one 16×16 maze as a plan: north up, one square per cell, with the doors, the flagged edges
/// and — the reason a cluebook wants this at all — the walls you can walk straight through.
///
/// <para><b>An illusory wall is the whole point of a Might &amp; Magic 1 map.</b> The game draws a
/// wall from its graphic plane and decides whether you may pass from a separate passability plane,
/// and where the two disagree there is a secret passage: the wizard behind the Erliquin inn is
/// reached by walking into what looks like the back wall. A plan that drew only the walls you cannot
/// pass would be a worse map than the one in the player's head.</para>
///
/// <para>What an edge <i>is</i> is <see cref="MazeMap"/>'s to say — <see cref="EdgeFace"/>,
/// <see cref="MazeMap.SecretPassages"/> and the rest live beside the maze so that this and the
/// trainer's own Map (drawn) tab cannot drift apart on the same question. What is here is only the
/// drawing: grid squares into pixels, or into characters.</para>
///
/// <para>The markup goes through <see cref="SvgCanvas"/>, and every edge of one style is emitted as
/// a single <c>&lt;path&gt;</c> rather than one element per segment, which is what keeps 55 plans in
/// one page a document rather than a download.</para>
/// </summary>
public static class MazePlan
{
    /// <summary>Squares across and down, from the game's own maze geometry.</summary>
    public const int Size = MazeMap.Size;

    /// <summary>Room around the grid for the coordinate rulers.</summary>
    private const int Margin = 22;

    /// <summary>Glyphs for the plain-text plan, matching <c>docs/maze-atlas.md</c>'s legend.</summary>
    private const string Glyphs = " oSD#";

    /// <summary>
    /// Renders the maze as an <c>&lt;svg&gt;</c> element.
    /// </summary>
    /// <param name="map">The maze.</param>
    /// <param name="cell">Pixels per square.</param>
    /// <param name="includeStyle">
    /// Whether to inline <see cref="Style"/>. A plan on its own needs it; a page holding all 55 wants
    /// it once, in the page, because an inline SVG's CSS is not scoped to that SVG.
    /// </param>
    /// <param name="markers">
    /// Squares to point at. Drawn as a numbered disc in the middle of the square, over the floor and
    /// under nothing — a marker never hides a wall, because the walls are what the plan is for.
    /// </param>
    public static string RenderSvg(MazeMap map, int cell = 30, bool includeStyle = true,
                                   IReadOnlyList<PlanMarker>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        cell = Math.Clamp(cell, 8, 80);

        double side = Size * (double)cell;
        var svg = SvgCanvas.Responsive(Margin * 2 + side, Margin * 2 + side, $"Plan of {map.DisplayName}");
        if (includeStyle) svg.Style(Style);

        // The floor first, so every edge draws over it.
        svg.Rect(Margin, Margin, side, side, ("class", "mp-floor"));

        var paths = new Dictionary<EdgeFace, StringBuilder>
        {
            [EdgeFace.Wall] = new(), [EdgeFace.Door] = new(),
            [EdgeFace.Special] = new(), [EdgeFace.Illusory] = new(),
        };
        var oneWay = new StringBuilder();

        Walk(map, (a, b, x1, y1, x2, y2) =>
        {
            var face = MazeMap.Stronger(a, b);
            if (face == EdgeFace.None) return;

            var d = paths[face];
            d.Append('M').Append(N(Margin + x1 * cell)).Append(' ').Append(N(Margin + y1 * cell))
             .Append('L').Append(N(Margin + x2 * cell)).Append(' ').Append(N(Margin + y2 * cell)).Append(' ');

            // A disagreement between the two squares is marked rather than resolved silently.
            if (a == b) return;
            double mx = Margin + (x1 + x2) / 2.0 * cell, my = Margin + (y1 + y2) / 2.0 * cell;
            oneWay.Append('M').Append(N(mx - 2)).Append(' ').Append(N(my))
                  .Append('a').Append(" 2 2 0 1 0 4 0 a 2 2 0 1 0 -4 0 ");
        });

        foreach (var (face, d) in paths)
        {
            if (d.Length == 0) continue;
            svg.Element("path", ("class", ClassOf(face)), ("d", d.ToString().TrimEnd()));
        }

        if (oneWay.Length > 0) svg.Element("path", ("class", "mp-oneway"), ("d", oneWay.ToString().TrimEnd()));

        Markers(svg, cell, markers);
        Rulers(svg, cell, side);
        return svg.ToSvg();
    }

    /// <summary>
    /// Renders the maze as 33 lines of 33 characters, north up — the same shape
    /// <c>docs/maze-atlas.md</c> prints and <see cref="BuiltInMazes"/> stores.
    ///
    /// <para>That is not a coincidence worth losing: the bundled grids are parsed into a
    /// <see cref="MazeMap"/> and this renders one back, so the harness can round-trip all 55 and
    /// catch a renderer that has quietly started drawing the map transposed or upside down.</para>
    /// </summary>
    public static string[] RenderAscii(MazeMap map, IReadOnlyList<PlanMarker>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        int lines = Size * 2 + 1;
        var rows = new char[lines][];
        for (int i = 0; i < lines; i++)
        {
            rows[i] = new char[lines];
            Array.Fill(rows[i], ' ');
            // Corners belong to the horizontal lines, which are the even-numbered ones.
            if (i % 2 == 0)
                for (int k = 0; k < lines; k += 2) rows[i][k] = '+';
        }

        for (int y = 0; y < Size; y++)
        {
            int r = Size - 1 - y;                    // row 0 of the grid is y = 15, the north edge
            for (int x = 0; x < Size; x++)
            {
                rows[r * 2][x * 2 + 1] = Glyph(map.HorizontalEdge(y + 1, x));       // north of this square
                rows[r * 2 + 2][x * 2 + 1] = Glyph(map.HorizontalEdge(y, x));       // south of it
                rows[r * 2 + 1][x * 2] = Glyph(map.VerticalEdge(x, y));             // west of it
                rows[r * 2 + 1][x * 2 + 2] = Glyph(map.VerticalEdge(x + 1, y));     // east of it
            }
        }

        // A marker sits in the middle of its square, which the grid leaves blank; nothing else is
        // ever written there, so a mark can never cover a wall.
        foreach (var marker in markers ?? Array.Empty<PlanMarker>())
        {
            if (marker.X is < 0 or >= Size || marker.Y is < 0 or >= Size || marker.Label.Length == 0) continue;
            rows[(Size - 1 - marker.Y) * 2 + 1][marker.X * 2 + 1] = marker.Label[0];
        }

        return rows.Select(r => new string(r)).ToArray();
    }

    private static char Glyph((EdgeFace A, EdgeFace B) sides) => Glyphs[(int)MazeMap.Stronger(sides.A, sides.B)];

    /// <summary>
    /// Visits every edge of the maze once, giving both squares' view of it and its two endpoints in
    /// grid units — column 0..16 left to right, row 0..16 top to bottom with north at the top.
    /// </summary>
    private static void Walk(MazeMap map, Action<EdgeFace, EdgeFace, int, int, int, int> edge)
    {
        for (int y = 0; y < Size; y++)
        {
            int row = Size - 1 - y;                  // game y counts north; the drawing counts down
            for (int vx = 0; vx <= Size; vx++)
            {
                var (west, east) = map.VerticalEdge(vx, y);
                edge(west, east, vx, row, vx, row + 1);
            }
        }

        for (int hy = 0; hy <= Size; hy++)
        {
            int row = Size - hy;
            for (int x = 0; x < Size; x++)
            {
                var (south, north) = map.HorizontalEdge(hy, x);
                edge(south, north, x, row, x + 1, row);
            }
        }
    }

    private static string ClassOf(EdgeFace face) => face switch
    {
        EdgeFace.Wall => "mp-wall",
        EdgeFace.Door => "mp-door",
        EdgeFace.Special => "mp-special",
        _ => "mp-illusory",
    };

    /// <summary>Draws the marked squares as numbered discs.</summary>
    private static void Markers(SvgCanvas svg, int cell, IReadOnlyList<PlanMarker>? markers)
    {
        foreach (var marker in markers ?? Array.Empty<PlanMarker>())
        {
            if (marker.X is < 0 or >= Size || marker.Y is < 0 or >= Size) continue;

            double cx = Margin + (marker.X + 0.5) * cell;
            double cy = Margin + (Size - marker.Y - 0.5) * cell;   // game y counts north; the drawing counts down

            svg.Circle(cx, cy, cell * 0.32, ("class", "mp-mark"));
            svg.Text(cx, cy + cell * 0.13, marker.Label,
                     ("class", "mp-marklabel"), ("text-anchor", "middle"));
        }
    }

    /// <summary>Numbers every fourth column and row, and marks north.</summary>
    private static void Rulers(SvgCanvas svg, int cell, double side)
    {
        for (int i = 0; i < Size; i++)
        {
            if (i % 4 != 0 && i != Size - 1) continue;
            string label = i.ToString(CultureInfo.InvariantCulture);

            svg.Text(Margin + (i + 0.5) * cell, Margin - 7, label, ("class", "mp-ruler"), ("text-anchor", "middle"));
            // Game y counts north, so row i is drawn (Size - 1 - i) squares down from the top.
            svg.Text(Margin - 6, Margin + (Size - i - 0.5) * cell + 4, label, ("class", "mp-ruler"), ("text-anchor", "end"));
        }

        svg.Text(Margin + side + 6, Margin + 8, "N", ("class", "mp-ruler"));
        svg.Text(Margin + side + 6, Margin + 18, "↑", ("class", "mp-ruler"));
    }

    private static string N(double value) => SvgCanvas.Number(value);

    /// <summary>The plan's stylesheet, so a page full of plans can carry one copy of it.</summary>
    public const string Style =
        ".mp-floor{fill:#fbf7ec;stroke:#e0d5b8;stroke-width:1}" +
        ".mp-wall{stroke:#3a3222;stroke-width:2.5;fill:none;stroke-linecap:square}" +
        ".mp-door{stroke:#b5892b;stroke-width:2.5;fill:none;stroke-linecap:square;stroke-dasharray:5 3}" +
        ".mp-special{stroke:#3f7a8c;stroke-width:2.5;fill:none;stroke-linecap:square;stroke-dasharray:2 3}" +
        ".mp-illusory{stroke:#b0a488;stroke-width:1.5;fill:none;stroke-dasharray:1 3}" +
        ".mp-oneway{fill:#a3432b;stroke:none}" +
        ".mp-ruler{font:600 9px 'Segoe UI',sans-serif;fill:#8a7d5e}" +
        ".mp-mark{fill:#2f6b4f;stroke:#fbf7ec;stroke-width:1.5}" +
        ".mp-marklabel{font:700 11px 'Segoe UI',sans-serif;fill:#fbf7ec}";
}
