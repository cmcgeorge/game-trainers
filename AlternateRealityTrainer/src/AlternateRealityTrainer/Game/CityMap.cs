using System.Globalization;
using GameTrainers.Common.Documents;

namespace AlternateRealityTrainer.Game;

/// <summary>One drawn location: where it sits on the map and what to say about it.</summary>
/// <param name="Place">The location itself.</param>
/// <param name="CentreX">Marker centre, in map units, measured from the map's left edge.</param>
/// <param name="CentreY">Marker centre, in map units, measured from the map's top edge.</param>
public readonly record struct MapMarker(Place Place, double CentreX, double CentreY)
{
    public PlaceKind Kind => Place.Kind;
    public char Symbol => Place.Symbol;

    /// <summary>Marker radius in map units.</summary>
    public double Radius => CityMap.MarkerRadius;

    /// <summary>Left edge of the marker's bounding box — what a Canvas wants.</summary>
    public double Left => CentreX - Radius;

    /// <summary>Top edge of the marker's bounding box.</summary>
    public double Top => CentreY - Radius;

    /// <summary>Bounding-box side length.</summary>
    public double Size => Radius * 2;

    /// <summary>Fill colour as <c>#RRGGBB</c>, shared by the on-screen map and the exported SVG.</summary>
    public string Colour => CityMap.ColourFor(Kind);

    /// <summary>A one-line description for a tooltip or an SVG <c>&lt;title&gt;</c>.</summary>
    public string Description =>
        Place.Note.Length == 0
            ? $"{Kind} — {Place.Coordinate}"
            : $"{Kind} — {Place.Coordinate} — {Place.Note}";
}

/// <summary>One drawn terrain square.</summary>
/// <param name="North">Row, 1 at the southern edge.</param>
/// <param name="East">Column, 1 at the western edge.</param>
/// <param name="Kind">What occupies it.</param>
/// <param name="Left">Left edge of the square, in map units.</param>
/// <param name="Top">Top edge of the square, in map units.</param>
public readonly record struct MapTile(int North, int East, TerrainKind Kind, double Left, double Top)
{
    /// <summary>Side length, in map units.</summary>
    public double Size => CityMap.CellSize;

    /// <summary>Fill colour as <c>#RRGGBB</c>, shared by the on-screen map and the exported SVG.</summary>
    public string Colour => CityMap.ColourFor(Kind);
}

/// <summary>An axis label: the number and where it goes.</summary>
/// <param name="Label">The coordinate, as text.</param>
/// <param name="X">Left edge of the label's box, in map units.</param>
/// <param name="Y">Top edge of the label's box, in map units.</param>
/// <param name="Width">Box width, so the label can be centred over its cell.</param>
public readonly record struct MapTick(string Label, double X, double Y, double Width);

/// <summary>A legend entry.</summary>
public readonly record struct MapLegendEntry(PlaceKind Kind, char Symbol, string Colour, string Label);

/// <summary>
/// The geometry and palette of The City's location map, shared by the trainer's interactive canvas
/// and the SVG it can export — so the two never drift apart.
///
/// The City is a 64 × 64 grid whose square 1N 1E is the <b>south-west</b> corner, so north runs up
/// the map and east runs right, and row 1 is at the bottom. This is a map of where the doors are,
/// not where the walls are: the game's street layout is a maze and is not encoded here.
/// </summary>
public static class CityMap
{
    /// <summary>Side of one grid square, in map units.</summary>
    public const double CellSize = 15;

    /// <summary>Space reserved outside the grid for the axis labels.</summary>
    public const double Margin = 26;

    /// <summary>Radius of a location marker.</summary>
    public const double MarkerRadius = CellSize * 0.46;

    /// <summary>Every <i>n</i>th grid line is drawn heavier, to make counting squares easier.</summary>
    public const int MajorEvery = 8;

    /// <summary>Side of the grid itself, in map units.</summary>
    public static double GridSize => GameFacts.CitySize * CellSize;

    /// <summary>Overall width of the map, including both margins.</summary>
    public static double Width => GridSize + Margin * 2;

    /// <summary>Overall height of the map, including both margins.</summary>
    public static double Height => GridSize + Margin * 2;

    /// <summary>Distance from the map's left edge to the centre of column <paramref name="east"/>.</summary>
    public static double CentreX(int east) => Margin + (east - 0.5) * CellSize;

    /// <summary>
    /// Distance from the map's top edge to the centre of row <paramref name="north"/>. North counts
    /// up from the southern edge, so row 1 lands at the bottom.
    /// </summary>
    public static double CentreY(int north) => Margin + (GameFacts.CitySize - north + 0.5) * CellSize;

    /// <summary>Marker fill for each kind of building, as <c>#RRGGBB</c>.</summary>
    public static string ColourFor(PlaceKind kind) => kind switch
    {
        PlaceKind.Inn => "#2E7D32",      // green
        PlaceKind.Tavern => "#C77800",   // amber
        PlaceKind.Bank => "#1565C0",     // blue
        PlaceKind.Shop => "#6A1B9A",     // purple
        PlaceKind.Smithy => "#5D4037",   // brown
        PlaceKind.Healer => "#C2185B",   // crimson
        _ => "#00838F",                  // teal — guilds
    };

    /// <summary>
    /// Fill for each terrain kind. Streets are left as the page background so the markers and the
    /// grid stay legible; only the solid and scenery squares are painted.
    /// </summary>
    public static string ColourFor(TerrainKind kind) => kind switch
    {
        TerrainKind.Building => "#C9BCA6",   // warm stone: the blocks the streets wind between
        TerrainKind.Wall => "#8C7E68",       // darker: the city boundary and dividing walls
        TerrainKind.Scenery => "#BFD3B4",    // green: open ground beyond the streets
        _ => "#00000000",                    // street and doorway: unpainted
    };

    /// <summary>True when the kind is painted at all.</summary>
    public static bool IsPainted(TerrainKind kind) =>
        kind is TerrainKind.Building or TerrainKind.Wall or TerrainKind.Scenery;

    /// <summary>What each terrain kind is called on the legend.</summary>
    public static string LabelFor(TerrainKind kind) => kind switch
    {
        TerrainKind.Building => "Building",
        TerrainKind.Wall => "Wall",
        TerrainKind.Scenery => "Open ground",
        TerrainKind.Doorway => "Doorway",
        _ => "Street",
    };

    /// <summary>
    /// Every painted square of <paramref name="terrain"/>, positioned. Street squares are skipped --
    /// they are the background -- which keeps the drawing to roughly half the 4,096 cells.
    /// </summary>
    public static IReadOnlyList<MapTile> Tiles(CityTerrain? terrain)
    {
        var tiles = new List<MapTile>();
        if (terrain == null) return tiles;
        for (int n = 1; n <= GameFacts.CitySize; n++)
        {
            for (int e = 1; e <= GameFacts.CitySize; e++)
            {
                var kind = terrain.KindAt(n, e);
                if (!IsPainted(kind)) continue;
                tiles.Add(new MapTile(n, e, kind,
                    Margin + (e - 1) * CellSize,
                    Margin + (GameFacts.CitySize - n) * CellSize));
            }
        }
        return tiles;
    }

    /// <summary>What each kind is called on the legend.</summary>
    public static string LabelFor(PlaceKind kind) => kind switch
    {
        PlaceKind.Inn => "Inn",
        PlaceKind.Tavern => "Tavern",
        PlaceKind.Bank => "Bank",
        PlaceKind.Shop => "Shop",
        PlaceKind.Smithy => "Smithy",
        PlaceKind.Healer => "Healer",
        _ => "Guild",
    };

    /// <summary>Every location, positioned. Ordered so the rarer kinds draw last and stay on top.</summary>
    public static IReadOnlyList<MapMarker> Markers() =>
        CityBook.Places
            .OrderByDescending(p => CityBook.Places.Count(q => q.Kind == p.Kind))
            .Select(p => new MapMarker(p, CentreX(p.East), CentreY(p.North)))
            .ToList();

    /// <summary>Column numbers along the top and row numbers down the left, every <paramref name="every"/> squares.</summary>
    public static IReadOnlyList<MapTick> Ticks(int every = 4)
    {
        var ticks = new List<MapTick>();
        for (int n = every; n <= GameFacts.CitySize; n += every)
        {
            // Column label, centred over its square, sitting in the top margin.
            ticks.Add(new MapTick(n.ToString(CultureInfo.InvariantCulture),
                CentreX(n) - CellSize, Margin - CellSize - 2, CellSize * 2));
            // Row label, centred on its square, sitting in the left margin.
            ticks.Add(new MapTick(n.ToString(CultureInfo.InvariantCulture),
                0, CentreY(n) - CellSize / 2, Margin - 4));
        }
        return ticks;
    }

    /// <summary>The legend, in the order the kinds are declared.</summary>
    public static IReadOnlyList<MapLegendEntry> Legend() =>
        Enum.GetValues<PlaceKind>()
            .Select(k => new MapLegendEntry(k, KindSymbol(k), ColourFor(k), LabelFor(k)))
            .ToList();

    private static char KindSymbol(PlaceKind kind) =>
        CityBook.Places.First(p => p.Kind == kind).Symbol;

    /// <summary>
    /// Renders the whole map as a standalone SVG document — the version that goes into the strategy
    /// guide, and what <b>Save map…</b> writes.
    ///
    /// The markup goes through <see cref="SvgCanvas"/>, which is shared with the other trainer that
    /// draws a plan; the geometry and the palette stay here, because they are about this city. Pure
    /// text, no dependency on WPF, so the verification harness can assert it.
    /// </summary>
    public static string RenderSvg(CityTerrain? terrain = null)
    {
        var legend = Legend();
        const double legendHeight = 34;
        double totalHeight = Height + legendHeight;

        // A file rather than something embedded in a page: one element per line, so the exported map
        // can be read, diffed and inspected. docs/city-map.svg is a committed copy of this output.
        var svg = SvgCanvas.File(Width, totalHeight, "The City of Xebec's Demise",
                                 ("font-family", "Segoe UI, Helvetica, Arial, sans-serif"));
        svg.Title("The City of Xebec's Demise — locations");
        svg.Rect(0, 0, Width, totalHeight, ("fill", "#FBF8F2"));

        // Terrain, painted under the grid so the grid lines still read on top of it.
        var tiles = Tiles(terrain);
        if (tiles.Count > 0)
        {
            using (svg.Scope("g", ("shape-rendering", "crispEdges")))
            {
                foreach (var t in tiles) svg.Rect(t.Left, t.Top, t.Size, t.Size, ("fill", t.Colour));
            }
        }

        // Grid: the minor lines first, then the major ones over them.
        using (svg.Scope("g", ("stroke", "#DDD5C7"), ("stroke-width", 1)))
        {
            for (int i = 0; i <= GameFacts.CitySize; i++)
            {
                if (i % MajorEvery == 0) continue;
                GridLines(svg, i);
            }
        }

        using (svg.Scope("g", ("stroke", "#B9AE9B"), ("stroke-width", 1)))
        {
            for (int i = 0; i <= GameFacts.CitySize; i += MajorEvery) GridLines(svg, i);
        }

        // Axis labels.
        using (svg.Scope("g", ("fill", "#6B6154"), ("font-size", 10), ("text-anchor", "middle")))
        {
            for (int n = 4; n <= GameFacts.CitySize; n += 4)
            {
                svg.Text(CentreX(n), Margin - 8, n.ToString(CultureInfo.InvariantCulture));
                svg.Text(Margin - 12, CentreY(n) + 3.5, n.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Compass hints on the outside edges.
        using (svg.Scope("g", ("fill", "#9A8F7E"), ("font-size", 10), ("font-weight", "bold")))
        {
            svg.Text(Margin - 20, Margin - 8, "N");
            svg.Text(Margin + GridSize + 6, Margin + GridSize + 14, "E →", ("text-anchor", "end"));
        }

        // Markers. The title is the marker group's own child, so it is that marker's tooltip.
        using (svg.Scope("g", ("font-size", 10), ("font-weight", "bold"), ("text-anchor", "middle")))
        {
            foreach (var m in Markers())
            {
                using (svg.Scope("g"))
                {
                    svg.Title(m.Description);
                    svg.Circle(m.CentreX, m.CentreY, m.Radius,
                               ("fill", m.Colour), ("stroke", "#FBF8F2"), ("stroke-width", 1.2));
                    svg.Text(m.CentreX, m.CentreY + 3.2, m.Symbol.ToString(), ("fill", "#FFFFFF"));
                }
            }
        }

        // Legend.
        using (svg.Scope("g", ("font-size", 11)))
        {
            double x = Margin;
            double y = Height + 4;

            if (tiles.Count > 0)
            {
                foreach (var kind in new[] { TerrainKind.Building, TerrainKind.Wall, TerrainKind.Scenery })
                {
                    string label = LabelFor(kind);
                    svg.Rect(x, y, 14, 14, ("fill", ColourFor(kind)), ("stroke", "#B9AE9B"), ("stroke-width", 0.5));
                    svg.Text(x + 19, y + 11, label, ("fill", "#4A4238"));
                    x += 19 + label.Length * 7.2 + 16;
                }
            }

            foreach (var e in legend)
            {
                svg.Circle(x + 7, y + 7, 7, ("fill", e.Colour));
                svg.Text(x + 7, y + 10.5, e.Symbol.ToString(),
                         ("fill", "#FFFFFF"), ("font-size", 9), ("font-weight", "bold"), ("text-anchor", "middle"));
                svg.Text(x + 19, y + 11, e.Label, ("fill", "#4A4238"));
                x += 19 + e.Label.Length * 7.2 + 16;
            }
        }

        return svg.ToSvg();
    }

    /// <summary>The pair of grid lines at grid index <paramref name="i"/>, across and down.</summary>
    private static void GridLines(SvgCanvas svg, int i)
    {
        double p = Margin + i * CellSize;
        svg.Line(p, Margin, p, Margin + GridSize);
        svg.Line(Margin, p, Margin + GridSize, p);
    }
}
