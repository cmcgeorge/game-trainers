using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SotSAgeTrainer.Core;

namespace SotSAgeTrainer.Infrastructure;

/// <summary>Colours a character row by role: YOU green, kin blue, rival red.</summary>
public sealed class RoleBrushConverter : IValueConverter
{
    private static readonly Brush You = new SolidColorBrush(Color.FromRgb(0x3F, 0xC3, 0x80));
    private static readonly Brush Kin = new SolidColorBrush(Color.FromRgb(0x5A, 0x9B, 0xE0));
    private static readonly Brush Rival = new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x6C));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is CharacterRole r
            ? r switch { CharacterRole.You => You, CharacterRole.Kin => Kin, _ => Rival }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
