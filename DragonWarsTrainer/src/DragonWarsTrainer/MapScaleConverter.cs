using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DragonWarsTrainer.Game;

namespace DragonWarsTrainer;

public sealed class MapScaleConverter : IValueConverter
{
    public double Cell { get; set; } = 15;

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

public sealed class MapFlipYConverter : IMultiValueConverter
{
    public double Cell { get; set; } = 15;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double y = ToDouble(values.Length > 0 ? values[0] : null, culture);
        double height = ToDouble(values.Length > 1 ? values[1] : null, culture);
        return (height - 1 - y) * Cell;
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
    private static readonly Brush Abyss = new SolidColorBrush(Color.FromArgb(0xC0, 0x33, 0x33, 0x33));
    private static readonly Brush Stone = new SolidColorBrush(Color.FromArgb(0x80, 0xCF, 0xCF, 0xC2));

    static FloorBrushConverter()
    {
        Water.Freeze();
        Abyss.Freeze();
        Stone.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        FloorKind.Water => Water,
        FloorKind.Abyss => Abyss,
        FloorKind.Stone => Stone,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="WallKind"/> to an edge-segment brush (transparent when no wall).</summary>
public sealed class WallBrushConverter : IValueConverter
{
    private static readonly Brush Wall = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly Brush Door = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B));
    private static readonly Brush Fence = new SolidColorBrush(Color.FromRgb(0x8B, 0x5A, 0x2B));

    static WallBrushConverter()
    {
        Wall.Freeze();
        Door.Freeze();
        Fence.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        WallKind.Wall => Wall,
        WallKind.Door => Door,
        WallKind.Fence => Fence,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
