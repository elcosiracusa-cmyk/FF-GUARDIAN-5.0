using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FFGuardian.PremiumWpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += InsertProtectionProgressPanel;
    }

    private void InsertProtectionProgressPanel(object sender, RoutedEventArgs e)
    {
        Loaded -= InsertProtectionProgressPanel;
        if (Content is not Grid shell) return;

        Grid? contentGrid = shell.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 1);
        if (contentGrid is null || contentGrid.RowDefinitions.Count < 2) return;

        contentGrid.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
        foreach (UIElement child in contentGrid.Children)
        {
            int row = Grid.GetRow(child);
            if (row >= 1) Grid.SetRow(child, row + 1);
        }

        ProtectionProgressPanel progressPanel = new();
        Grid.SetRow(progressPanel, 1);
        Panel.SetZIndex(progressPanel, 10);
        contentGrid.Children.Add(progressPanel);
    }

    public void RenderDashboardScreenshot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        const double width = 1440;
        const double height = 860;
        Width = width;
        Height = height;
        Measure(new Size(width, height));
        Arrange(new Rect(0, 0, width, height));
        UpdateLayout();

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        RenderTargetBitmap bitmap = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(fullPath);
        encoder.Save(stream);
    }
}
