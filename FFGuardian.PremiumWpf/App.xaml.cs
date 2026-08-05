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
            ServiceCollection services = new();
            services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
            services.AddSingleton<SecurityStatusService>();
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            SecurityStatusService statusService = _services.GetRequiredService<SecurityStatusService>();
            _viewModel = new MainViewModel(statusService);
            MainWindow window = new(_viewModel);
            MainWindow = window;

            string? screenshot = FindScreenshotPath(e.Args);
            if (!string.IsNullOrWhiteSpace(screenshot))
            {
                RunScreenshotMode(window, screenshot);
                return;
            }

            window.Show();
            StartupDiagnostics.Write("Startup.WindowShown");
            _ = window.Dispatcher.InvokeAsync(
                () => _viewModel.RefreshAsync(),
                DispatcherPriority.ApplicationIdle).Task.Unwrap();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("Startup.Failed", ex);
            ShowStartupFailure(ex);
            Shutdown(1);
        }
    }

    private void RunScreenshotMode(MainWindow window, string screenshot)
    {
        try
        {
            _viewModel!.RefreshAsync().GetAwaiter().GetResult();
            window.RenderDashboardScreenshot(screenshot);
            StartupDiagnostics.Write("Startup.ScreenshotCompleted", message: screenshot);
            Shutdown(0);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("Startup.ScreenshotFailed", ex);
            Shutdown(2);
        }
    }

    private static string? FindScreenshotPath(string[] arguments)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], "--screenshot", StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }

    private static void ShowStartupFailure(Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"FFGuardian non è riuscito ad avviarsi.\n\nDettagli salvati in:\n{StartupDiagnostics.LogPath}\n\n{exception.Message}",
                "FFGuardian — Errore di avvio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Non causare un secondo arresto se USER32 non riesce a mostrare il dialogo.
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Write("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
        ShowStartupFailure(e.Exception);
        Current.Shutdown(3);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Write("AppDomainUnhandledException", e.ExceptionObject as Exception,
            e.IsTerminating ? "Processo in terminazione" : "Eccezione non gestita");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupDiagnostics.Write("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupDiagnostics.Write("Application.Exit", message: $"ExitCode={e.ApplicationExitCode}");
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
        _viewModel = null;
        _services?.Dispose();
        _services = null;
        GC.SuppressFinalize(this);
    }
}
