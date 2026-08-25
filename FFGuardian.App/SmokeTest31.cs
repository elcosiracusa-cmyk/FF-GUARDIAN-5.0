using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FFGuardian.Security.Core;

namespace FFGuardian;

internal static class SmokeTest31
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly TimeSpan EngineSmokeTimeout = TimeSpan.FromSeconds(40);

    [ModuleInitializer]
    internal static void Initialize()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (!arguments.Any(argument => string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
            return;

        int exitCode;
        try
        {
            exitCode = RunAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Fatal smoke-test failure: {exception}");
            exitCode = 3;
        }

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

            EngineHealthResult yara = await RunEngineCheckAsync(
                "YARA",
                static token => SharedSecurityServices31.Yara.RunSelfTestAsync(token),
                cancellationToken).ConfigureAwait(false);
            AddEngineCheck(checks, yara);
            success &= yara.Operational;

            EngineHealthResult clamAv = await RunEngineCheckAsync(
                "ClamAV",
                static token => SharedSecurityServices31.ClamAv.RunSelfTestAsync(token),
                cancellationToken).ConfigureAwait(false);
            AddEngineCheck(checks, clamAv);
            success &= clamAv.Operational;

            EngineHealthResult freshClam = await RunEngineCheckAsync(
                "FreshClam",
                static token => SharedSecurityServices31.FreshClam.GetHealthAsync(token),
                cancellationToken).ConfigureAwait(false);
            AddEngineCheck(checks, freshClam);
            success &= freshClam.Operational;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            success = false;
            checks.Add(new { name = "Unhandled smoke operation", success = false, message = exception.ToString() });
        }
        catch (Exception exception)
        {
            success = false;
            checks.Add(new { name = "Unexpected smoke operation", success = false, message = exception.ToString() });
        }

        object report = new
        {
            schemaVersion = 2,
            success,
            startedAt,
            completedAt = DateTimeOffset.UtcNow,
            baseDirectory = AppContext.BaseDirectory,
            processId = Environment.ProcessId,
            engineTimeoutSeconds = EngineSmokeTimeout.TotalSeconds,
            checks
        };

        await WriteReportAsync(reportPath, report).ConfigureAwait(false);
        Console.WriteLine($"Smoke-test report: {reportPath}");
        return success ? 0 : 3;
    }

    private static async Task<EngineHealthResult> RunEngineCheckAsync(
        string name,
        Func<CancellationToken, Task<EngineHealthResult>> operation,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<EngineHealthResult> operationTask;

        try
        {
            operationTask = operation(linked.Token);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new(name, false, "--", $"Errore avvio controllo: {exception.Message}", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }

        Task completed = await Task.WhenAny(operationTask, Task.Delay(EngineSmokeTimeout, CancellationToken.None)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, operationTask))
        {
            linked.Cancel();
            stopwatch.Stop();
            _ = operationTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return new(name, false, "--", $"Timeout smoke test dopo {EngineSmokeTimeout.TotalSeconds:0} secondi.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }

        try
        {
            EngineHealthResult result = await operationTask.ConfigureAwait(false);
            stopwatch.Stop();
            return result with { Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new(name, false, "--", "Controllo motore annullato internamente.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            stopwatch.Stop();
            return new(name, false, "--", exception.Message, DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new(name, false, "--", exception.ToString(), DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }
    }

    private static void AddEngineCheck(List<object> checks, EngineHealthResult result)
    {
        checks.Add(new
        {
            name = result.Name,
            success = result.Operational,
            version = result.Version,
            message = result.Message,
            durationMs = result.Duration.TotalMilliseconds
        });
    }

    private static async Task WriteReportAsync(string reportPath, object report)
    {
        try
        {
            string fullPath = Path.GetFullPath(reportPath);
            string? reportDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(reportDirectory))
                Directory.CreateDirectory(reportDirectory);
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, JsonOptions), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Unable to write smoke-test report: {exception}");
        }
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
