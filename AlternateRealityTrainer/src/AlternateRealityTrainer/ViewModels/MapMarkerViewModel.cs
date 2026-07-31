using System.Windows.Media;
using AlternateRealityTrainer.Game;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>
/// One location drawn on the City map. Position and colour come from <see cref="CityMap"/>, so the
/// on-screen map and the exported SVG agree square for square.
/// </summary>
public sealed class MapMarkerViewModel : ObservableObject
{
    private readonly MapMarker _marker;

    public MapMarkerViewModel(MapMarker marker)
    {
        _marker = marker;
        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(marker.Colour));
        Fill.Freeze();
    }

    public PlaceKind Kind => _marker.Kind;
    public double Left => _marker.Left;
    public double Top => _marker.Top;
    public double Size => _marker.Size;
    public string Symbol => _marker.Symbol.ToString();
    public SolidColorBrush Fill { get; }
    public string Tooltip => _marker.Description;

    private double _opacity = 1.0;
    /// <summary>Dimmed to a hint when the map is filtered to some other kind of building.</summary>
    public double Opacity { get => _opacity; private set => SetField(ref _opacity, value); }

    private double _glyphSize = 9;
    /// <summary>Font size of the letter inside the marker; grows a little when highlighted.</summary>
    public double GlyphSize { get => _glyphSize; private set => SetField(ref _glyphSize, value); }

    /// <summary>Applies the current filter: "All" shows everything at full strength.</summary>
    public void ApplyFilter(PlaceKind? only)
    {
        bool match = only == null || only == Kind;
        Opacity = match ? 1.0 : 0.18;
        GlyphSize = only != null && match ? 10 : 9;
    }
}

/// <summary>An axis number down the side or across the top of the map.</summary>
public sealed class MapTickViewModel
{
    public MapTickViewModel(MapTick tick)
    {
        Label = tick.Label;
        Left = tick.X;
        Top = tick.Y;
        Width = tick.Width;
    }

    public string Label { get; }
    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
}

/// <summary>
/// A terrain swatch. Its colour and label come from <see cref="CityMap"/>, the same source the drawn
/// map and the exported SVG use, so the legend cannot end up describing a palette nothing else uses.
/// </summary>
public sealed class TerrainLegendViewModel
{
    public TerrainLegendViewModel(TerrainKind kind)
    {
        Label = CityMap.LabelFor(kind);
        string colour = CityMap.IsPainted(kind) ? CityMap.ColourFor(kind) : "#FBF8F2";
        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour));
        Fill.Freeze();
    }

    public string Label { get; }
    public SolidColorBrush Fill { get; }
}

/// <summary>A legend swatch.</summary>
public sealed class MapLegendViewModel
{
    public MapLegendViewModel(MapLegendEntry entry)
    {
        Label = entry.Label;
        Symbol = entry.Symbol.ToString();
        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Colour));
        Fill.Freeze();
    }

    public string Label { get; }
    public string Symbol { get; }
    public SolidColorBrush Fill { get; }
}
