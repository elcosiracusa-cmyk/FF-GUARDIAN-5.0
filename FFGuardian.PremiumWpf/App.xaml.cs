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
            if (HasArgument(e.Args, "--safe-mode"))
            {
                RunSafeMode();
                return;
            }

            if (HasArgument(e.Args, "--smoke-test"))
            {
                RunSmokeTest(e.Args);
                return;
            }

            BuildServices();
            StartupDiagnostics.Write("Startup.MainWindow.Resolve.Begin");
            SecurityStatusService statusService = _services!.GetRequiredService<SecurityStatusService>();
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

    private void RunSafeMode()
    {
        StartupDiagnostics.Write("SafeMode.Begin");
        string serviceStatus;
        try
        {
            BuildServices();
            _services!.GetRequiredService<IProcessRunner>();
            _services.GetRequiredService<IEngineLocatorService>();
            _services.GetRequiredService<IFileHashService>();
            _services.GetRequiredService<IPathExclusionService>();
            serviceStatus = "Dependency Injection: operativa";
            StartupDiagnostics.Write("SafeMode.Services.Resolved");
        }
        catch (Exception exception)
        {
            serviceStatus = $"Dependency Injection: errore — {exception.Message}";
            StartupDiagnostics.Write("SafeMode.Services.Failed", exception);
        }

        TextBlock title = new()
        {
            Text = "FFGuardian — Modalità sicura",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        };
        TextBlock details = new()
        {
            Text = $"I motori antivirus e la protezione realtime non sono stati avviati.\n\n{serviceStatus}\n\nBase: {AppContext.BaseDirectory}\nLog: {StartupDiagnostics.LogPath}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gainsboro,
            FontSize = 14
        };
        Button close = new()
        {
            Content = "Chiudi",
            Width = 120,
            Height = 40,
            Margin = new Thickness(0, 24, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        StackPanel panel = new() { Margin = new Thickness(28) };
        panel.Children.Add(title);
        panel.Children.Add(details);
        panel.Children.Add(close);

        Window window = new()
        {
            Title = "FFGuardian — Modalità sicura",
            Width = 720,
            Height = 420,
            MinWidth = 560,
            MinHeight = 320,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(7, 17, 31)),
            Content = panel
        };
        close.Click += (_, _) => window.Close();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        StartupDiagnostics.Write("SafeMode.WindowShown");
    }

    private void RunSmokeTest(string[] arguments)
    {
        string reportPath = FindArgumentValue(arguments, "--report") ??
            Path.Combine(Path.GetDirectoryName(StartupDiagnostics.LogPath)!, "smoke-report.json");
        SmokeBootstrapReport report = new()
        {
            StartedUtc = DateTimeOffset.UtcNow,
            BaseDirectory = AppContext.BaseDirectory,
            CurrentDirectory = Environment.CurrentDirectory
        };

        try
        {
            StartupDiagnostics.Write("SmokeTest.Directories.Begin");
            string writableRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFGuardian");
            Directory.CreateDirectory(writableRoot);
            string probePath = Path.Combine(writableRoot, $"write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "FFGuardian smoke test", System.Text.Encoding.UTF8);
            File.Delete(probePath);
            report.WritableDirectories = true;
            StartupDiagnostics.Write("SmokeTest.Directories.Success");

            BuildServices();
            ResolveRequiredServices();
            report.DependencyInjection = true;
            StartupDiagnostics.Write("SmokeTest.Services.Success");

            SecurityStatusService statusService = _services!.GetRequiredService<SecurityStatusService>();
            _viewModel = new MainViewModel(statusService);
            report.ViewModel = true;
            StartupDiagnostics.Write("SmokeTest.ViewModel.Success");

            MainWindow testWindow = new(_viewModel);
            testWindow.Measure(new Size(1366, 768));
            testWindow.Arrange(new Rect(0, 0, 1366, 768));
            testWindow.UpdateLayout();
            testWindow.Close();
            report.XamlResources = true;
            report.MainWindow = true;
            StartupDiagnostics.Write("SmokeTest.MainWindow.Success");

            string manifest = Path.Combine(AppContext.BaseDirectory, "Assets", "ffguardian-files-manifest.json");
            report.ManifestPresent = File.Exists(manifest);
            report.Success = report.WritableDirectories && report.DependencyInjection && report.ViewModel && report.XamlResources && report.MainWindow;
        }
        catch (Exception exception)
        {
            report.Error = exception.ToString();
            report.Success = false;
            StartupDiagnostics.Write("SmokeTest.Failed", exception);
        }
        finally
        {
            report.CompletedUtc = DateTimeOffset.UtcNow;
            WriteSmokeReport(reportPath, report);
        }

        Shutdown(report.Success ? 0 : 20);
    }

    private void ResolveRequiredServices()
    {
        Type[] required =
        [
            typeof(IProcessRunner), typeof(IEngineLocatorService), typeof(IFileHashService),
            typeof(IPathExclusionService), typeof(ISecurityEventLogger), typeof(IYaraService),
            typeof(IClamAvService), typeof(IFreshClamService), typeof(IQuarantineService),
            typeof(IScanService), typeof(IAntivirusHealthService), typeof(SecurityStatusService)
        ];
        foreach (Type serviceType in required)
        {
            _services!.GetRequiredService(serviceType);
            StartupDiagnostics.Write("SmokeTest.Service.Resolved", message: serviceType.FullName);
        }
    }

    private static void WriteSmokeReport(string path, SmokeBootstrapReport report)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, ReportJsonOptions));
        StartupDiagnostics.Write("SmokeTest.Report.Written", message: fullPath);
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

    private static bool HasArgument(IEnumerable<string> arguments, string expected) =>
        arguments.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

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

internal sealed class SmokeBootstrapReport
{
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
    public string BaseDirectory { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
    public bool WritableDirectories { get; set; }
    public bool DependencyInjection { get; set; }
    public bool ViewModel { get; set; }
    public bool XamlResources { get; set; }
    public bool MainWindow { get; set; }
    public bool ManifestPresent { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
