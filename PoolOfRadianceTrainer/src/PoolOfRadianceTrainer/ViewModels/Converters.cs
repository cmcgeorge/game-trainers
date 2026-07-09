using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>
/// Collapses an element based on whether the bound value is null. Two ready-made static
/// instances are exposed for XAML via <c>{x:Static vm:NullToVisibility.CollapsedIfNull}</c>.
/// </summary>
public sealed class NullToVisibility : IValueConverter
{
    private readonly bool _collapseWhenNull;
    private NullToVisibility(bool collapseWhenNull) => _collapseWhenNull = collapseWhenNull;

    /// <summary>Visible when the value is non-null; collapsed when null (for the editor body).</summary>
    public static readonly NullToVisibility CollapsedIfNull = new(true);

    /// <summary>Visible when the value is null; collapsed when non-null (for the "select something" hint).</summary>
    public static readonly NullToVisibility CollapsedIfNotNull = new(false);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool visible = _collapseWhenNull ? !isNull : isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
