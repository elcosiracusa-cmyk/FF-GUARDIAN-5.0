using System.Windows;

namespace FFGuardian.PremiumWpf;

public partial class App : Application, IDisposable
{
    private MainViewModel? _viewModel;
    private bool _disposed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SecurityStatusService statusService = new();
        _viewModel = new MainViewModel(statusService);
        MainWindow window = new(_viewModel);

        string? screenshot = FindScreenshotPath(e.Args);
        if (!string.IsNullOrWhiteSpace(screenshot))
        {
            _viewModel.RefreshAsync().GetAwaiter().GetResult();
            window.RenderDashboardScreenshot(screenshot);
            Shutdown(0);
            return;
        }

        window.Show();
    }

    private static string? FindScreenshotPath(IReadOnlyList<string> arguments)
    {
        for (int index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], "--screenshot", StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _viewModel?.Dispose();
        _viewModel = null;
        GC.SuppressFinalize(this);
    }
}
