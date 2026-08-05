using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian.PremiumWpf;

internal static class BootstrapModes
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };

    public static bool HasArgument(IEnumerable<string> arguments, string expected) =>
        arguments.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

    public static int RunSafeMode()
    {
        StartupDiagnostics.Write("SafeMode.Begin");
        ServiceProvider? provider = null;
        try
        {
            string serviceStatus;
            try
            {
                provider = BuildProvider();
                ResolveRequiredServices(provider);
                serviceStatus = "Dependency Injection: operativa";
            }
            catch (Exception exception)
            {
                serviceStatus = $"Dependency Injection: errore — {exception.Message}";
                StartupDiagnostics.Write("SafeMode.Services.Failed", exception);
            }

            Application app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
            TextBox diagnostics = new()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(16),
                Text = BuildSafeModeText(serviceStatus)
            };
            Window window = new()
            {
                Title = "FFGuardian — Modalità sicura",
                Width = 760,
                Height = 520,
                MinWidth = 560,
                MinHeight = 380,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = diagnostics
            };
            app.MainWindow = window;
            return app.Run(window);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("SafeMode.Failed", exception);
            return 110;
        }
        finally
        {
            provider?.Dispose();
        }
    }

    public static int RunSmokeTest(string[] arguments)
    {
        string reportPath = GetArgumentValue(arguments, "--report") ??
            Path.Combine(Path.GetDirectoryName(StartupDiagnostics.LogPath)!, "smoke-test.json");
        SmokeTestReport report = new()
        {
            StartedUtc = DateTimeOffset.UtcNow,
            BaseDirectory = AppContext.BaseDirectory,
            CurrentDirectory = Environment.CurrentDirectory,
            RuntimeVersion = Environment.Version.ToString(),
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        };

        ServiceProvider? provider = null;
        MainViewModel? viewModel = null;
        MainWindow? window = null;
        try
        {
            VerifyWritableDirectories(report);
            provider = BuildProvider();
            ResolveRequiredServices(provider);
            IScanService scanService = provider.GetRequiredService<IScanService>();
            if (scanService is not UnifiedScanService) throw new InvalidOperationException("IScanService non usa l'orchestratore unificato.");
            if (scanService.GetStatus().State != ScanState.Ready) throw new InvalidOperationException("Stato iniziale scansione non valido.");
            report.Steps.Add("Unified scan service resolved");

            viewModel = provider.GetRequiredService<MainViewModel>();
            window = new MainWindow(viewModel);
            window.Measure(new Size(1280, 720));
            window.Arrange(new Rect(0, 0, 1280, 720));
            window.UpdateLayout();
            report.Steps.Add("MainWindow and XAML resources loaded");

            INavigationService navigation = provider.GetRequiredService<INavigationService>();
            foreach (NavigationPageViewModel page in navigation.Pages)
            {
                NavigationResult result = navigation.NavigateTo(page.Route);
                if (!result.Success || !string.Equals(navigation.CurrentRoute, page.Route, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Navigazione non riuscita: {page.Route} — {result.Message}");
                report.Routes.Add(page.Route);
                report.Steps.Add($"Route opened: {page.Route}");
            }

            NavigationResult missing = navigation.NavigateTo("route-inesistente");
            if (missing.Success) throw new InvalidOperationException("Una route inesistente è stata accettata.");

            string manifest = Path.Combine(AppContext.BaseDirectory, "Assets", "ffguardian-files-manifest.json");
            report.ManifestPresent = File.Exists(manifest);
            report.Success = report.Routes.Count == navigation.Pages.Count;
            report.ExitCode = report.Success ? 0 : 121;
            return report.ExitCode;
        }
        catch (Exception exception)
        {
            report.Success = false;
            report.ExitCode = 120;
            report.Error = exception.ToString();
            StartupDiagnostics.Write("SmokeTest.Failed", exception);
            return 120;
        }
        finally
        {
            window?.Close();
            viewModel?.Dispose();
            provider?.Dispose();
            report.CompletedUtc = DateTimeOffset.UtcNow;
            WriteReport(reportPath, report);
        }
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        services.AddUnifiedFFGuardianScanService();
        services.AddSingleton<SecurityStatusService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IScanTargetSelector, ScanTargetSelector>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static void ResolveRequiredServices(ServiceProvider provider)
    {
        Type[] required =
        [
            typeof(IProcessRunner), typeof(IEngineLocatorService), typeof(IFileHashService),
            typeof(IPathExclusionService), typeof(ISecurityEventLogger), typeof(IYaraService),
            typeof(IClamAvService), typeof(IFreshClamService), typeof(IQuarantineService),
            typeof(IScanService), typeof(IAntivirusHealthService), typeof(SecurityStatusService),
            typeof(INavigationService), typeof(IScanTargetSelector), typeof(MainViewModel)
        ];
        foreach (Type serviceType in required) provider.GetRequiredService(serviceType);
    }

    private static void VerifyWritableDirectories(SmokeTestReport report)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFGuardian");
        foreach (string name in new[] { "Logs", "Cache", "Quarantine", "Settings", "Reports" })
        {
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "FFGuardian bootstrap probe");
            File.Delete(probe);
            report.Steps.Add($"Writable directory: {directory}");
        }
    }

    private static string BuildSafeModeText(string serviceStatus) => string.Join(Environment.NewLine,
        "FFGuardian è stato avviato in modalità sicura.", string.Empty, serviceStatus,
        $"Versione: {GetApplicationVersion()}",
        $"Architettura: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
        $"Runtime: {Environment.Version}",
        $"Base directory: {AppContext.BaseDirectory}",
        $"Directory corrente: {Environment.CurrentDirectory}",
        $"Log: {StartupDiagnostics.LogPath}", string.Empty,
        "YARA, ClamAV, realtime, Ransom Shield e aggiornamenti non sono stati avviati.");

    private static string GetApplicationVersion()
    {
        string? process = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(process) ? "--" : FileVersionInfo.GetVersionInfo(process).FileVersion ?? "--";
    }

    private static string? GetArgumentValue(string[] arguments, string name)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase)) return arguments[index + 1];
        return null;
    }

    private static void WriteReport(string path, SmokeTestReport report)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report, ReportJsonOptions));
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("SmokeTest.ReportWriteFailed", exception, path);
        }
    }

    private sealed class SmokeTestReport
    {
        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset CompletedUtc { get; set; }
        public string BaseDirectory { get; set; } = string.Empty;
        public string CurrentDirectory { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public bool ManifestPresent { get; set; }
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string? Error { get; set; }
        public List<string> Steps { get; } = [];
        public List<string> Routes { get; } = [];
    }
}
