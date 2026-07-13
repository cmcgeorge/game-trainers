using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer;

/// <summary>Scales a grid coordinate/size to pixels for the Maps schematic (n × cell).</summary>
public sealed class MapScaleConverter : IValueConverter
{
    public double Cell { get; set; } = 18;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double cell = ParseCell(parameter);
        double n = value switch
        {
            int i => i,
            double d => d,
            IConvertible c => c.ToDouble(culture),
            _ => 0
        };
        return n * cell;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private double ParseCell(object? parameter)
    {
        if (parameter is double d) return d;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p;
        return Cell;
    }
}

/// <summary>
/// Projects a grid Y (origin north-west, y increasing south — Pool of Radiance's own convention)
/// to a pixel Canvas.Top. The schematic is drawn with (0,0) at the top-left, so Y is used directly
/// (no flip); this converter exists to keep the schematic markup identical in shape to the Dragon
/// Wars template and to centralise the cell size.
/// </summary>
public sealed class MapFlipYConverter : IMultiValueConverter
{
    public double Cell { get; set; } = 18;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double y = ToDouble(values.Length > 0 ? values[0] : null, culture);
        return y * Cell;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ToDouble(object? value, CultureInfo culture) => value switch
    {
        int i => i,
        double d => d,
        IConvertible c => c.ToDouble(culture),
        _ => 0
    };
}

/// <summary>Maps a <see cref="FloorKind"/> to a translucent fill for the map schematic.</summary>
public sealed class FloorBrushConverter : IValueConverter
{
    private static readonly Brush Water = new SolidColorBrush(Color.FromArgb(0xB0, 0x5B, 0x9B, 0xD5));
    private static readonly Brush Stone = new SolidColorBrush(Color.FromArgb(0x80, 0xCF, 0xCF, 0xC2));

    static FloorBrushConverter()
    {
        Water.Freeze();
        Stone.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        FloorKind.Water => Water,
        FloorKind.Stone => Stone,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="WallKind"/> to an edge-segment brush (transparent when no wall).</summary>
public sealed class WallBrushConverter : IValueConverter
{
    private static readonly Brush Wall = new SolidColorBrush(Color.FromRgb(0xE0, 0xB3, 0x41));
    private static readonly Brush Door = new SolidColorBrush(Color.FromRgb(0x5F, 0xA3, 0x5C));

    static WallBrushConverter()
    {
        Wall.Freeze();
        Door.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        WallKind.Wall => Wall,
        WallKind.Door => Door,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
