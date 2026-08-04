using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DarkDesigns1Trainer.Game;

namespace DarkDesigns1Trainer;

/// <summary>Multiplies a grid coordinate by the map cell size to place it on the canvas.</summary>
public sealed class MapScaleConverter : IValueConverter
{
    /// <summary>Side of one drawn square, in device-independent pixels.</summary>
    public const double CellSize = 18;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double cell = parameter switch
        {
            double d => d,
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
            _ => CellSize,
        };
        double n = value switch
        {
            int i => i,
            double d => d,
            IConvertible c => c.ToDouble(culture),
            _ => 0,
        };
        return n * cell;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Paints one edge of a square. Dark Designs draws a wall and an undiscovered secret door
/// identically, which is the point — the schematic shows what the party can see, not what the
/// bytes say.
/// </summary>
public sealed class WallBrushConverter : IValueConverter
{
    private static readonly Brush Wall = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
    private static readonly Brush Door = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B));

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

/// <summary>Fills a square according to what happens when the party steps on it.</summary>
public sealed class SquareBrushConverter : IValueConverter
{
    private static readonly Brush StairsUp = new SolidColorBrush(Color.FromArgb(0xD0, 0x27, 0xAE, 0x60));
    private static readonly Brush StairsDown = new SolidColorBrush(Color.FromArgb(0xD0, 0x21, 0x77, 0xB4));
    private static readonly Brush Chest = new SolidColorBrush(Color.FromArgb(0xD0, 0xE6, 0xA2, 0x3C));
    private static readonly Brush Item = new SolidColorBrush(Color.FromArgb(0xD0, 0x9B, 0x59, 0xB6));
    private static readonly Brush Edge = new SolidColorBrush(Color.FromArgb(0xC0, 0xC0, 0x39, 0x2B));

    /// <summary>A chest or item the party has already taken — worth showing, but muted.</summary>
    private static readonly Brush Emptied = new SolidColorBrush(Color.FromArgb(0x60, 0x88, 0x88, 0x88));

    static SquareBrushConverter()
    {
        StairsUp.Freeze();
        StairsDown.Freeze();
        Chest.Freeze();
        Item.Freeze();
        Edge.Freeze();
        Emptied.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SquareKind.StairsUp => StairsUp,
        SquareKind.StairsDown => StairsDown,
        SquareKind.TreasureChest => Chest,
        SquareKind.Item => Item,
        SquareKind.Edge => Edge,
        SquareKind.Emptied => Emptied,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Tints squares the party has already stood on, so the schematic shows what is explored.</summary>
public sealed class VisitedBrushConverter : IValueConverter
{
    private static readonly Brush Visited = new SolidColorBrush(Color.FromArgb(0x30, 0x27, 0xAE, 0x60));

    static VisitedBrushConverter() => Visited.Freeze();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visited : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Rotates the party marker to point the way it is facing (0 = North, clockwise).</summary>
public sealed class FacingAngleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int facing = value switch
        {
            int i => i,
            IConvertible c => c.ToInt32(culture),
            _ => 0,
        };
        return (facing & 3) * 90.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
