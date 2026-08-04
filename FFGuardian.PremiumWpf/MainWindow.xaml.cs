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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        string? screenshot = Environment.GetCommandLineArgs()
            .SkipWhile(argument => !string.Equals(argument, "--screenshot", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(screenshot)) return;
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        SaveScreenshot(Path.GetFullPath(screenshot));
        Application.Current.Shutdown(0);
    }

    private void SaveScreenshot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
