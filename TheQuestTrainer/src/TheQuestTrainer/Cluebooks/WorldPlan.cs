using System.Globalization;
using System.Text;
using GameTrainers.Common.Documents;
using TheQuestTrainer.Adventures;

namespace TheQuestTrainer.Cluebooks;

/// <summary>
/// Draws the outdoor world as a plan: one square per grid cell, labelled with the place's name.
///
/// The Quest's outdoor world is a square grid of 21×21-tile maps whose ids spell out their cell —
/// <c>base_s0804</c> is column 8, row 4 — so the plan is the grid, one-based, north at the top. That
/// is the same arithmetic the running game does when it recomputes the player's world-absolute
/// position (<c>docs/ReverseEngineering.md</c> §17.4), which is why the trainer's Map tab and this
/// plan agree.
///
/// The markup goes through <see cref="SvgCanvas"/>; what is left here is the layout, which is the
/// half that is about The Quest. SVG rather than a bitmap because it scales, costs nothing to
/// produce, and drops straight into the HTML cluebook without an image file beside it.
/// </summary>
public static class WorldPlan
{
    /// <summary>Line height of a cell's caption, in pixels.</summary>
    private const int CaptionLineHeight = 12;

    /// <summary>Margin around the grid, leaving room for the column and row rulers.</summary>
    private const int Margin = 28;

    /// <summary>
    /// Renders the plan.
    /// </summary>
    /// <param name="cluebook">The cluebook, for the grid size and which cells have a chapter.</param>
    /// <returns>An <c>&lt;svg&gt;</c> element, or the empty string when the world has no grid.</returns>
    public static string Render(Cluebook cluebook)
    {
        ArgumentNullException.ThrowIfNull(cluebook);

        var adventure = cluebook.Adventure;
        int cell = Math.Max(24, cluebook.Options.PlanCellSize);
        int columns = Math.Max(adventure.GridWidth, adventure.OutdoorMaps.Select(m => m.Column ?? 0).DefaultIfEmpty(0).Max());
        int rows = Math.Max(adventure.GridHeight, adventure.OutdoorMaps.Select(m => m.Row ?? 0).DefaultIfEmpty(0).Max());

        if (columns <= 0 || rows <= 0) return "";

        var byCell = new Dictionary<(int, int), AdventureMap>();
        foreach (var map in adventure.OutdoorMaps) byCell.TryAdd((map.Column!.Value, map.Row!.Value), map);

        var withChapter = cluebook.Chapters.Where(c => c.Map.IsOutdoorCell)
                                           .Select(c => (c.Map.Column!.Value, c.Map.Row!.Value))
                                           .ToHashSet();

        // Responsive: the plan is embedded in the cluebook page, so it takes the page's width.
        var svg = SvgCanvas.Responsive(Margin * 2 + columns * cell, Margin * 2 + rows * cell,
                                       $"Plan of {adventure.Name}");
        svg.Style(Css);

        for (int c = 1; c <= columns; c++)
        {
            svg.Text(Margin + (c - 1) * cell + cell / 2.0, Margin - 10, c.ToString(CultureInfo.InvariantCulture),
                     ("class", "ruler"), ("text-anchor", "middle"));
        }

        for (int r = 1; r <= rows; r++)
        {
            svg.Text(Margin - 10, Margin + (r - 1) * cell + cell / 2.0 + 4, r.ToString(CultureInfo.InvariantCulture),
                     ("class", "ruler"), ("text-anchor", "end"));
        }

        for (int r = 1; r <= rows; r++)
        {
            for (int c = 1; c <= columns; c++)
            {
                int x = Margin + (c - 1) * cell;
                int y = Margin + (r - 1) * cell;
                byCell.TryGetValue((c, r), out var map);

                string style = map is null ? "cell bare" : withChapter.Contains((c, r)) ? "cell named" : "cell";

                if (map is null)
                {
                    svg.Rect(x, y, cell, cell, ("class", style));
                    continue;
                }

                // The title is written as the rectangle's child, so it really is that square's
                // tooltip; SvgCanvas is what makes that the only way to write it.
                svg.Rect(x, y, cell, cell, $"{map.Name} ({map.Id}) — cell {c}, {r}", ("class", style));
                Caption(svg, map.Name, x, y, cell);
            }
        }

        return svg.ToSvg();
    }

    /// <summary>
    /// Writes a cell's name into its square, wrapped over as many lines as fit.
    ///
    /// Word-wrapped by character count rather than measured: SVG has no layout pass, and a place name
    /// that runs a little wide is better than one clipped to a single line.
    /// </summary>
    private static void Caption(SvgCanvas svg, string name, int x, int y, int cell)
    {
        if (name.Length == 0) return;

        int perLine = Math.Max(6, cell / 6);
        var lines = Wrap(name, perLine, Math.Max(1, cell / (CaptionLineHeight + 2)));
        double top = y + cell / 2.0 - (lines.Count - 1) * CaptionLineHeight / 2.0;

        for (int i = 0; i < lines.Count; i++)
        {
            svg.Text(x + cell / 2.0, top + i * CaptionLineHeight, lines[i],
                     ("class", "name"), ("text-anchor", "middle"));
        }
    }

    private static List<string> Wrap(string text, int perLine, int maxLines)
    {
        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > perLine)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == maxLines) return Truncate(lines);
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word.Length > perLine ? word[..perLine] : word);
        }

        if (line.Length > 0) lines.Add(line.ToString());
        return lines.Count > maxLines ? Truncate(lines[..maxLines]) : lines;
    }

    private static List<string> Truncate(List<string> lines)
    {
        if (lines.Count > 0) lines[^1] += "…";
        return lines;
    }

    private const string Css =
        ".cell{fill:#f6f1e4;stroke:#b9ab8c;stroke-width:1}" +
        ".cell.bare{fill:#e7eef3}" +
        ".cell.named{fill:#fbf7ec}" +
        ".name{font:600 10px 'Segoe UI',sans-serif;fill:#3a3222}" +
        ".ruler{font:600 11px 'Segoe UI',sans-serif;fill:#6f6449}";
}
