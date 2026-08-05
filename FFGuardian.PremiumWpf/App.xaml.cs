using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian.PremiumWpf;

public partial class App : Application, IDisposable
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };
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
            StartupDiagnostics.Write("Startup.MainWindow.Resolve.Begin");
            ServiceProvider provider = _services ?? throw new InvalidOperationException("Service provider non inizializzato.");
            SecurityStatusService statusService = provider.GetRequiredService<SecurityStatusService>();
            _viewModel = new MainViewModel(statusService);
            StartupDiagnostics.Write("Startup.MainViewModel.Created");
            MainWindow window = new(_viewModel);
            StartupDiagnostics.Write("Startup.MainWindow.Created");
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
        StartupDiagnostics.Write("Startup.Configuration.Load", message: "Configurazione predefinita basata su AppContext.BaseDirectory");
        StartupDiagnostics.Write("Startup.Services.Register.Begin");
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        services.AddSingleton<SecurityStatusService>();
        StartupDiagnostics.Write("Startup.Services.Register.End");
        StartupDiagnostics.Write("Startup.ServiceProvider.Build.Begin");
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
            StartupDiagnostics.Write("Startup.ScreenshotCompleted", message: screenshot);
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
        {
            if (string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }

    private static void ShowStartupFailure(Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"FFGuardian non è riuscito ad avviarsi. È stato creato un report diagnostico.\n\n{StartupDiagnostics.LogPath}\n\n{exception.GetType().Name}: {exception.Message}",
                "FFGuardian — Errore di avvio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
        _viewModel = null;
        _services?.Dispose();
        _services = null;
        GC.SuppressFinalize(this);
    }
}
