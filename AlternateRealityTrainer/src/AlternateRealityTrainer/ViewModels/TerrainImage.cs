using System.Windows;
using System.Windows.Media;
using AlternateRealityTrainer.Game;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>
/// Turns a <see cref="CityTerrain"/> into a single drawing.
///
/// Roughly 2,400 of the 4,096 squares are painted, and putting that many elements on a Canvas makes
/// WPF crawl. Instead every square of a given kind is merged into one geometry, giving three
/// drawings for the whole city — which lays out and zooms instantly.
/// </summary>
public static class TerrainImage
{
    /// <summary>
    /// Builds a frozen, ready-to-bind image of <paramref name="terrain"/>, or null when there is
    /// none. Coordinates match <see cref="CityMap"/>, so the drawing lines up with the markers.
    /// </summary>
    public static ImageSource? Build(CityTerrain? terrain)
    {
        if (terrain == null) return null;

        var group = new DrawingGroup();

        foreach (var kind in new[] { TerrainKind.Scenery, TerrainKind.Building, TerrainKind.Wall })
        {
            var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
            for (int n = 1; n <= CityTerrain.Size; n++)
            {
                for (int e = 1; e <= CityTerrain.Size; e++)
                {
                    if (terrain.KindAt(n, e) != kind) continue;
                    geometry.Children.Add(new RectangleGeometry(new Rect(
                        CityMap.Margin + (e - 1) * CityMap.CellSize,
                        CityMap.Margin + (CityTerrain.Size - n) * CityMap.CellSize,
                        CityMap.CellSize,
                        CityMap.CellSize)));
                }
            }
            if (geometry.Children.Count == 0) continue;

            geometry.Freeze();
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CityMap.ColourFor(kind)));
            brush.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, geometry));
        }

        if (group.Children.Count == 0) return null;

        // Pin the drawing to the full map rectangle so it cannot be rescaled to its own bounds --
        // otherwise a city with nothing painted along one edge would drift out of alignment.
        var bounds = new RectangleGeometry(new Rect(0, 0, CityMap.Width, CityMap.Height));
        bounds.Freeze();
        group.Children.Insert(0, new GeometryDrawing(Brushes.Transparent, null, bounds));

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
