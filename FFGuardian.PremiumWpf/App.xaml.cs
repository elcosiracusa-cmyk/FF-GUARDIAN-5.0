using System.Windows;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian.PremiumWpf;

public partial class App : Application, IDisposable
{
    private ServiceProvider? _services;
    private MainViewModel? _viewModel;
    private bool _disposed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        services.AddSingleton<SecurityStatusService>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        SecurityStatusService statusService = _services.GetRequiredService<SecurityStatusService>();
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

    private static string? FindScreenshotPath(string[] arguments)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], "--screenshot", StringComparison.OrdinalIgnoreCase)) return arguments[index + 1];
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
        _services?.Dispose();
        _services = null;
        GC.SuppressFinalize(this);
    }
}
