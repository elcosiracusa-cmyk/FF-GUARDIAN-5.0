using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FFGuardian.PremiumWpf;

public sealed class ComponentStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => "Operativo",
        false => "Non operativo",
        null => "Non verificato",
        _ => "Non verificato"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ComponentStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            true => "AccentBrush",
            false => "DangerBrush",
            _ => "WarningBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ComponentStatusSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => "✓",
        false => "×",
        null => "!",
        _ => "!"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
