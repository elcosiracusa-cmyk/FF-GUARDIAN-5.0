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
                StartupDiagnostics.Write("SafeMode.Services.Resolved");
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
            Button openLogs = new()
            {
                Content = "Apri cartella log",
                MinWidth = 150,
                Height = 38,
                Margin = new Thickness(16, 0, 16, 16),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            openLogs.Click += (_, _) => OpenLogDirectory();
            DockPanel panel = new();
            DockPanel.SetDock(openLogs, Dock.Bottom);
            panel.Children.Add(openLogs);
            panel.Children.Add(diagnostics);
            Window window = new()
            {
                Title = "FFGuardian — Modalità sicura",
                Width = 760,
                Height = 520,
                MinWidth = 560,
                MinHeight = 380,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = panel
            };
            app.MainWindow = window;
            StartupDiagnostics.Write("SafeMode.WindowCreated");
            int exitCode = app.Run(window);
            StartupDiagnostics.Write("SafeMode.Exit", message: $"ExitCode={exitCode.ToString(CultureInfo.InvariantCulture)}");
            return exitCode;
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
            StartupDiagnostics.Write("SmokeTest.Begin", message: reportPath);
            VerifyWritableDirectories(report);
            provider = BuildProvider();
            ResolveRequiredServices(provider);
            report.Steps.Add("Dependency injection validated");

            SecurityStatusService status = provider.GetRequiredService<SecurityStatusService>();
            viewModel = new MainViewModel(status);
            report.Steps.Add("MainViewModel resolved without health checks");
            window = new MainWindow(viewModel);
            report.Steps.Add("MainWindow and XAML resources loaded");
            window.Measure(new Size(1280, 720));
            window.Arrange(new Rect(0, 0, 1280, 720));
            window.UpdateLayout();
            report.Steps.Add("Dashboard layout measured");

            string manifest = Path.Combine(AppContext.BaseDirectory, "Assets", "ffguardian-files-manifest.json");
            report.ManifestPresent = File.Exists(manifest);
            report.Success = true;
            report.ExitCode = 0;
            StartupDiagnostics.Write("SmokeTest.Success");
            return 0;
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
        StartupDiagnostics.Write("Bootstrap.Services.Register.Begin");
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        services.AddSingleton<SecurityStatusService>();
        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        StartupDiagnostics.Write("Bootstrap.Services.Register.End");
        return provider;
    }

    private static void ResolveRequiredServices(ServiceProvider provider)
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
            provider.GetRequiredService(serviceType);
            StartupDiagnostics.Write("Bootstrap.Service.Resolved", message: serviceType.FullName);
        }
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
        "FFGuardian è stato avviato in modalità sicura.",
        string.Empty,
        serviceStatus,
        $"Versione: {GetApplicationVersion()}",
        $"Architettura: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
        $"Runtime: {Environment.Version}",
        $"Base directory: {AppContext.BaseDirectory}",
        $"Directory corrente: {Environment.CurrentDirectory}",
        $"Log: {StartupDiagnostics.LogPath}",
        string.Empty,
        "YARA, ClamAV, realtime, Ransom Shield e aggiornamenti non sono stati avviati.");

    private static string GetApplicationVersion()
    {
        string? process = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(process)
            ? "--"
            : FileVersionInfo.GetVersionInfo(process).FileVersion ?? "--";
    }

    private static string? GetArgumentValue(string[] arguments, string name)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }

    private static void OpenLogDirectory()
    {
        string directory = Path.GetDirectoryName(StartupDiagnostics.LogPath)!;
        ProcessStartInfo startInfo = new()
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(directory);
        using Process? process = Process.Start(startInfo);
    }

    private static void WriteReport(string path, SmokeTestReport report)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report, ReportJsonOptions));
            StartupDiagnostics.Write("SmokeTest.Report.Written", message: fullPath);
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
    }
}
