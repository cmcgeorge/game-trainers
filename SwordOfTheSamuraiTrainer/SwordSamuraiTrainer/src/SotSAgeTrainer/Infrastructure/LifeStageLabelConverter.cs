using System;
using System.Globalization;
using System.Windows.Data;
using SotSAgeTrainer.Core;

namespace SotSAgeTrainer.Infrastructure;

/// <summary>Renders a <see cref="LifeStage"/> as its friendly label (e.g. "Mature adult").</summary>
public sealed class LifeStageLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is LifeStage s ? LifeStages.Label(s) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
