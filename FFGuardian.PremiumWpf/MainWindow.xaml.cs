using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FFGuardian.PremiumWpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
