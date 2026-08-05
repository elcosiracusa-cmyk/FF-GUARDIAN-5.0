using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FFGuardian.PremiumWpf;

/// <summary>
/// Global protection progress surface. It binds only to real values exposed by MainViewModel.
/// Modules without an active telemetry source remain explicitly in the "In attesa" state.
/// </summary>
public sealed class ProtectionProgressPanel : Border
{
    private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(43, 132, 255));
    private static readonly Brush CompletedBrush = new SolidColorBrush(Color.FromRgb(38, 201, 123));
    private static readonly Brush WaitingBrush = new SolidColorBrush(Color.FromRgb(91, 107, 128));

    public ProtectionProgressPanel()
    {
        Margin = new Thickness(16, 12, 16, 8);
        Padding = new Thickness(18, 14, 18, 14);
        CornerRadius = new CornerRadius(12);
        Background = new SolidColorBrush(Color.FromRgb(11, 27, 45));
        BorderBrush = new SolidColorBrush(Color.FromRgb(31, 58, 82));
        BorderThickness = new Thickness(1);

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid heading = new();
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock title = Text("PROTEZIONE GLOBALE", 13, FontWeights.SemiBold, Brushes.White);
        TextBlock state = Text(string.Empty, 12, FontWeights.SemiBold, RunningBrush);
        state.SetBinding(TextBlock.TextProperty, new Binding("ScanStatusMessage") { FallbackValue = "In attesa" });
        state.SetBinding(TextBlock.ForegroundProperty, new Binding("ScanState") { Converter = new ScanStateBrushConverter() });
        Grid.SetColumn(state, 1);
        heading.Children.Add(title);
        heading.Children.Add(state);
        root.Children.Add(heading);

        Grid progressRow = new() { Margin = new Thickness(0, 10, 0, 8) };
        progressRow.ColumnDefinitions.Add(new ColumnDefinition());
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ProgressBar progress = new()
        {
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Foreground = RunningBrush,
            Background = new SolidColorBrush(Color.FromRgb(29, 46, 64)),
            BorderThickness = new Thickness(0)
        };
        progress.SetBinding(ProgressBar.ValueProperty, new Binding("ScanProgressPercent"));
        progress.SetBinding(ProgressBar.IsIndeterminateProperty, new MultiBinding
        {
            Converter = new RealIndeterminateConverter(),
            Bindings = { new Binding("IsScanning"), new Binding("ScanTotalFiles") }
        });
        TextBlock percent = Text(string.Empty, 12, FontWeights.SemiBold, Brushes.White);
        percent.Margin = new Thickness(12, -4, 0, 0);
        percent.SetBinding(TextBlock.TextProperty, new MultiBinding
        {
            Converter = new RealPercentConverter(),
            Bindings = { new Binding("IsScanning"), new Binding("ScanTotalFiles"), new Binding("ScanProgressPercent"), new Binding("ScanState") }
        });
        Grid.SetColumn(percent, 1);
        progressRow.Children.Add(progress);
        progressRow.Children.Add(percent);
        Grid.SetRow(progressRow, 1);
        root.Children.Add(progressRow);

        WrapPanel facts = new();
        facts.Children.Add(Fact("Modulo", "ScanStatusMessage"));
        facts.Children.Add(Fact("File", "ScanCurrentFile", "In attesa"));
        facts.Children.Add(Fact("Motore", "ScanCurrentEngine", "In attesa"));
        facts.Children.Add(Fact("Analizzati", "ScanFilesScanned"));
        facts.Children.Add(Fact("Tempo", "ScanElapsed", "00:00:00", "hh\\:mm\\:ss"));
        facts.Children.Add(Fact("Stimato", "ScanEstimatedRemaining", "--", "hh\\:mm\\:ss"));
        facts.Children.Add(ModuleChip("Realtime"));
        facts.Children.Add(ModuleChip("Ransom Shield"));
        facts.Children.Add(ModuleChip("Firewall"));
        facts.Children.Add(ModuleChip("USB Shield"));
        Grid.SetRow(facts, 2);
        root.Children.Add(facts);

        Child = root;
    }

    private static StackPanel Fact(string label, string path, string fallback = "0", string? format = null)
    {
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 18, 3) };
        panel.Children.Add(Text(label + ": ", 11, FontWeights.Normal, new SolidColorBrush(Color.FromRgb(135, 154, 175))));
        TextBlock value = Text(string.Empty, 11, FontWeights.SemiBold, Brushes.White);
        value.MaxWidth = label == "File" ? 320 : 180;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        value.SetBinding(TextBlock.TextProperty, new Binding(path) { FallbackValue = fallback, TargetNullValue = fallback, StringFormat = format is null ? null : "{0:" + format + "}" });
        panel.Children.Add(value);
        return panel;
    }

    private static Border ModuleChip(string module)
    {
        Border chip = new()
        {
            Margin = new Thickness(0, 3, 8, 3),
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(22, 39, 57))
        };
        chip.Child = Text(module + " · In attesa", 10, FontWeights.Normal, WaitingBrush);
        return chip;
    }

    private static TextBlock Text(string value, double size, FontWeight weight, Brush brush) => new()
    {
        Text = value,
        FontSize = size,
        FontWeight = weight,
        Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center
    };
}

internal sealed class RealIndeterminateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 && values[0] is true && values[1] is int total && total <= 0;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class RealPercentConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool active = values.Length > 0 && values[0] is true;
        int total = values.Length > 1 && values[1] is int count ? count : 0;
        int percent = values.Length > 2 && values[2] is int value ? value : 0;
        string state = values.Length > 3 ? values[3]?.ToString() ?? string.Empty : string.Empty;
        if (state.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return "100%";
        if (!active) return "In attesa";
        return total > 0 ? $"{percent}%" : "Calcolo file…";
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class ScanStateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() switch
    {
        "Completed" => CompletedBrush,
        "Failed" => Brushes.IndianRed,
        "Cancelled" => Brushes.Goldenrod,
        "Ready" => WaitingBrush,
        _ => RunningBrush
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(43, 132, 255));
    private static readonly Brush CompletedBrush = new SolidColorBrush(Color.FromRgb(38, 201, 123));
    private static readonly Brush WaitingBrush = new SolidColorBrush(Color.FromRgb(91, 107, 128));
}
