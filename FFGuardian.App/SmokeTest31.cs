using System.Runtime.CompilerServices;
using System.Text.Json;
using FFGuardian.Security.Core;

namespace FFGuardian;

internal static class SmokeTest31
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [ModuleInitializer]
    internal static void Initialize()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (!arguments.Any(argument => string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
            return;

        int exitCode = RunAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
        Environment.Exit(exitCode);
    }

    private static async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        string reportPath = ReadReportPath(arguments) ?? Path.Combine(AppContext.BaseDirectory, "smoke-test-report.json");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        List<object> checks = [];
        bool success = true;

        try
        {
            string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
            success &= AddFileCheck(checks, "FFGuardian.exe", Path.Combine(baseDirectory, "FFGuardian.exe"));
            success &= AddFileCheck(checks, "FFGuardian.dll", Path.Combine(baseDirectory, "FFGuardian.dll"));
            success &= AddFileCheck(checks, "FFGuardian.Security.Core.dll", Path.Combine(baseDirectory, "FFGuardian.Security.Core.dll"));
            success &= AddFileCheck(checks, "Runtime configuration", Path.Combine(baseDirectory, "FFGuardian.runtimeconfig.json"));

            string manifest = Path.Combine(baseDirectory, "Assets", "release-manifest.json");
            if (!File.Exists(manifest))
                manifest = Path.Combine(baseDirectory, "Assets", "ffguardian-files-manifest.json");
            success &= AddFileCheck(checks, "Release manifest", manifest);

            IReadOnlyList<EngineHealthResult> health = await SharedSecurityServices31.Health.CheckAsync(cancellationToken).ConfigureAwait(false);
            foreach (EngineHealthResult result in health)
            {
                checks.Add(new
                {
                    name = result.Name,
                    success = result.Operational,
                    version = result.Version,
                    message = result.Message,
                    durationMs = result.Duration.TotalMilliseconds
                });
                success &= result.Operational;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            success = false;
            checks.Add(new { name = "Unhandled smoke operation", success = false, message = exception.ToString() });
        }

        object report = new
        {
            schemaVersion = 1,
            success,
            startedAt,
            completedAt = DateTimeOffset.UtcNow,
            baseDirectory = AppContext.BaseDirectory,
            processId = Environment.ProcessId,
            checks
        };

        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Smoke-test report: {reportPath}");
        return success ? 0 : 3;
    }

    private static bool AddFileCheck(List<object> checks, string name, string path)
    {
        bool exists = File.Exists(path);
        checks.Add(new { name, success = exists, path, message = exists ? "Presente" : "Mancante" });
        return exists;
    }

    private static string? ReadReportPath(string[] arguments)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], "--report", StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }
}
