using System.Globalization;
using System.Windows.Data;

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
