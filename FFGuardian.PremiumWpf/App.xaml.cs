using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian.PremiumWpf;

public partial class App : Application, IDisposable
{
    private ServiceProvider? _services;
    private MainViewModel? _viewModel;
    private bool _disposed;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupDiagnostics.Write("Startup.Begin", message: $"BaseDirectory={AppContext.BaseDirectory}");
        try
        {
            BuildServices();
            ServiceProvider provider = _services ?? throw new InvalidOperationException("Service provider non inizializzato.");
            _viewModel = provider.GetRequiredService<MainViewModel>();
            MainWindow window = new(_viewModel);
            MainWindow = window;
            string? screenshot = FindArgumentValue(e.Args, "--screenshot");
            if (!string.IsNullOrWhiteSpace(screenshot))
            {
                RunScreenshotMode(window, screenshot);
                return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            StartupDiagnostics.Write("Startup.Dashboard.Opened");
            _ = RefreshAfterWindowShownAsync(window);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Startup.Failed", exception);
            ShowStartupFailure(exception);
            Shutdown(1);
        }
    }

    private void BuildServices()
    {
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        services.AddUnifiedFFGuardianScanService();
        services.AddSingleton<IEngine10Service, Engine10Service>();
        services.AddSingleton<SecurityStatusService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IScanTargetSelector, ScanTargetSelector>();
        services.AddSingleton<MainViewModel>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        StartupDiagnostics.Write("Startup.ServiceProvider.Build.End");
    }

    private async Task RefreshAfterWindowShownAsync(MainWindow window)
    {
        try
        {
            await window.Dispatcher.InvokeAsync(
                () => _viewModel!.RefreshAsync(),
                DispatcherPriority.ApplicationIdle).Task.Unwrap();
            StartupDiagnostics.Write("Startup.HealthChecks.Completed");
        }
        catch (OperationCanceledException exception)
        {
            StartupDiagnostics.Write("Startup.HealthChecks.Cancelled", exception);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Startup.HealthChecks.Failed", exception);
        }
    }

    private void RunScreenshotMode(MainWindow window, string screenshot)
    {
        try
        {
            _viewModel!.RefreshAsync().GetAwaiter().GetResult();
            window.RenderDashboardScreenshot(screenshot);
            Shutdown(0);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Startup.ScreenshotFailed", exception);
            Shutdown(2);
        }
    }

    private static string? FindArgumentValue(string[] arguments, string argumentName)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase)) return arguments[index + 1];
        return null;
    }

    private static void ShowStartupFailure(Exception exception)
    {
        try
        {
            MessageBox.Show($"FFGuardian non è riuscito ad avviarsi.\n\n{StartupDiagnostics.LogPath}\n\n{exception.Message}",
                "FFGuardian — Errore di avvio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception messageException)
        {
            StartupDiagnostics.Write("Startup.ErrorDialog.Failed", messageException);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Write("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
        ShowStartupFailure(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        StartupDiagnostics.Write("AppDomainUnhandledException", e.ExceptionObject as Exception,
            e.IsTerminating ? "Processo in terminazione" : "Eccezione non gestita");

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupDiagnostics.Write("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupDiagnostics.Write("Application.Exit", message: $"ExitCode={e.ApplicationExitCode.ToString(CultureInfo.InvariantCulture)}");
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _viewModel?.Dispose();
        _services?.Dispose();
        GC.SuppressFinalize(this);
    }
}
