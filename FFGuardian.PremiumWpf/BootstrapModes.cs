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
    public static bool HasArgument(string[] args, string expected) =>
        args.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

    public static int RunSafeMode()
    {
        StartupDiagnostics.Write("SafeMode.Begin");
        try
        {
            Application app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
            TextBox diagnostics = new()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(16),
                Text = BuildSafeModeText()
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
            return app.Run(window);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("SafeMode.Failed", exception);
            return 110;
        }
    }

    public static int RunSmokeTest(string[] args)
    {
        string reportPath = GetArgumentValue(args, "--report") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFGuardian", "Logs", "smoke-test.json");
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
            ServiceCollection services = new();
            services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
            services.AddSingleton<SecurityStatusService>();
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            report.Steps.Add("Dependency injection validated");
            SecurityStatusService status = provider.GetRequiredService<SecurityStatusService>();
            viewModel = new MainViewModel(status);
            report.Steps.Add("MainViewModel resolved");
            window = new MainWindow(viewModel);
            report.Steps.Add("MainWindow and XAML resources loaded");
            window.Measure(new Size(1280, 720));
            window.Arrange(new Rect(0, 0, 1280, 720));
            report.Steps.Add("Dashboard layout measured");
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

    private static string BuildSafeModeText() => string.Join(Environment.NewLine,
        "FFGuardian è stato avviato in modalità sicura.",
        string.Empty,
        $"Versione: {GetApplicationVersion()}",
        $"Architettura: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
        $"Runtime: {Environment.Version}",
        $"Base directory: {AppContext.BaseDirectory}",
        $"Directory corrente: {Environment.CurrentDirectory}",
        $"Log: {StartupDiagnostics.LogPath}",
        string.Empty,
        "In questa modalità YARA, ClamAV, realtime, Ransom Shield e aggiornamenti non vengono avviati.");

    private static string GetApplicationVersion()
    {
        string? process = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(process)
            ? "--"
            : FileVersionInfo.GetVersionInfo(process).FileVersion ?? "--";
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
        return null;
    }

    private static void OpenLogDirectory()
    {
        string directory = Path.GetDirectoryName(StartupDiagnostics.LogPath)!;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { directory }
        });
    }

    private static void WriteReport(string path, SmokeTestReport report)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
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
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string? Error { get; set; }
        public List<string> Steps { get; } = [];
    }
}
