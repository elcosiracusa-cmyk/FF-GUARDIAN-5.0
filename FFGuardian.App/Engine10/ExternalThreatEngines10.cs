using System.Diagnostics;
using System.Text;

namespace FFGuardian.Engine10;

internal sealed record ExternalThreatResult10(
    string Engine,
    bool Available,
    bool IsMatch,
    bool IsError,
    string DetectionName,
    int Confidence,
    IReadOnlyList<string> Evidence);

internal sealed record ExternalEngineStatus10(
    bool ClamAvAvailable,
    string ClamAvPath,
    bool FreshClamAvailable,
    string FreshClamPath,
    bool YaraAvailable,
    string YaraPath,
    int YaraRuleFiles);

internal static class ExternalThreatEngines10
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(8);

    internal static ExternalEngineStatus10 GetStatus()
    {
        // External engines are security-sensitive native binaries. Resolve them only
        // from FFGuardian-controlled packaged roots; never pick an arbitrary executable
        // from PATH or an unrelated machine-wide installation.
        string packagedClamAv = Path.Combine(AppContext.BaseDirectory, "Engine", "ClamAV");
        string legacyClamAv = Path.Combine(AppContext.BaseDirectory, "Tools", "ClamAV");
        string packagedYara = Path.Combine(AppContext.BaseDirectory, "Engine", "Yara");
        string legacyYara = Path.Combine(AppContext.BaseDirectory, "Tools", "YARA");

        string clamScan = FindExecutable("clamscan.exe", new[] { packagedClamAv, legacyClamAv });
        string freshClam = FindExecutable("freshclam.exe", new[]
        {
            Path.GetDirectoryName(clamScan) ?? string.Empty,
            packagedClamAv,
            legacyClamAv
        });
        string yara = FindExecutable("yara64.exe", new[] { packagedYara, legacyYara });
        if (string.IsNullOrWhiteSpace(yara))
            yara = FindExecutable("yara.exe", new[] { packagedYara, legacyYara });

        string[] rules = GetYaraRuleFiles();
        return new ExternalEngineStatus10(
            File.Exists(clamScan), clamScan,
            File.Exists(freshClam), freshClam,
            File.Exists(yara), yara,
            rules.Length);
    }

    internal static async Task<IReadOnlyList<ExternalThreatResult10>> ScanAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ExternalEngineStatus10 status = GetStatus();
        List<Task<ExternalThreatResult10>> scans = [];

        if (status.ClamAvAvailable)
            scans.Add(ScanWithClamAvAsync(status.ClamAvPath, path, cancellationToken));
        if (status.YaraAvailable && status.YaraRuleFiles > 0)
            scans.Add(ScanWithYaraAsync(status.YaraPath, GetYaraRuleFiles(), path, cancellationToken));

        if (scans.Count == 0)
            return Array.Empty<ExternalThreatResult10>();

        ExternalThreatResult10[] results = await Task.WhenAll(scans).ConfigureAwait(false);
        return results;
    }

    internal static async Task<IReadOnlyList<string>> UpdateDatabasesAsync(
        CancellationToken cancellationToken = default)
    {
        ExternalEngineStatus10 status = GetStatus();
        List<string> messages = [];

        if (!status.FreshClamAvailable)
        {
            messages.Add("ClamAV non installato: aggiornamento freshclam non eseguito.");
            return messages;
        }

        ProcessExecution10 update = await RunProcessAsync(
            status.FreshClamPath,
            new[] { "--quiet" },
            UpdateTimeout,
            cancellationToken).ConfigureAwait(false);

        if (update.TimedOut)
            throw new TimeoutException("Aggiornamento ClamAV scaduto dopo 8 minuti.");
        if (update.ExitCode != 0)
            throw new InvalidOperationException($"freshclam non riuscito ({update.ExitCode}): {Compact(update.StandardError)}");

        messages.Add("Database ufficiale ClamAV aggiornato tramite freshclam.");
        return messages;
    }

    private static async Task<ExternalThreatResult10> ScanWithClamAvAsync(
        string executable,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessExecution10 execution = await RunProcessAsync(
                executable,
                new[] { "--no-summary", "--infected", path },
                ScanTimeout,
                cancellationToken).ConfigureAwait(false);

            if (execution.TimedOut)
                return Error("ClamAV", "Timeout durante la scansione ClamAV.");

            string output = execution.StandardOutput.Trim();
            if (execution.ExitCode == 1 || output.Contains(" FOUND", StringComparison.OrdinalIgnoreCase))
            {
                string detection = ParseClamDetection(output);
                return new ExternalThreatResult10(
                    "ClamAV", true, true, false,
                    string.IsNullOrWhiteSpace(detection) ? "ClamAV.Malware" : $"ClamAV.{detection}",
                    98,
                    new[] { $"ClamAV: {Compact(output)}" });
            }

            if (execution.ExitCode == 0)
                return new ExternalThreatResult10("ClamAV", true, false, false, string.Empty, 0,
                    new[] { "ClamAV non ha rilevato firme note." });

            return Error("ClamAV", $"Errore ClamAV {execution.ExitCode}: {Compact(execution.StandardError)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return Error("ClamAV", ex.Message);
        }
    }

    private static async Task<ExternalThreatResult10> ScanWithYaraAsync(
        string executable,
        IReadOnlyList<string> ruleFiles,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            List<string> arguments = [];
            arguments.AddRange(ruleFiles);
            arguments.Add(path);

            ProcessExecution10 execution = await RunProcessAsync(
                executable,
                arguments,
                ScanTimeout,
                cancellationToken).ConfigureAwait(false);

            if (execution.TimedOut)
                return Error("YARA", "Timeout durante la scansione YARA.");
            if (execution.ExitCode > 1)
                return Error("YARA", $"Errore YARA {execution.ExitCode}: {Compact(execution.StandardError)}");

            string[] matches = execution.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (matches.Length == 0)
                return new ExternalThreatResult10("YARA", true, false, false, string.Empty, 0,
                    new[] { $"YARA reale: nessuna regola corrispondente ({ruleFiles.Count} file regole)." });

            string firstRule = matches[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? "MatchedRule";
            return new ExternalThreatResult10(
                "YARA", true, true, false,
                $"YARA.{SanitizeDetection(firstRule)}",
                95,
                matches.Take(12).Select(match => $"YARA: {match}").ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return Error("YARA", ex.Message);
        }
    }

    private static async Task<ProcessExecution10> RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Impossibile avviare {Path.GetFileName(executable)}.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return new ProcessExecution10(process.ExitCode,
                await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false),
                false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled or timed-out native scan must never be left running in the
            // background, otherwise repeated Engine10 scans can accumulate orphaned
            // clamscan/yara processes and stall CI or the application.
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            if (cancellationToken.IsCancellationRequested)
                throw;

            return new ProcessExecution10(-1, string.Empty, "Timeout", true);
        }
    }

    private static string[] GetYaraRuleFiles()
    {
        string localRules = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "YaraRules");
        string packagedRules = Path.Combine(AppContext.BaseDirectory, "Engine", "Yara", "Rules");
        string legacyBundledRules = Path.Combine(AppContext.BaseDirectory, "Rules");

        return new[] { packagedRules, legacyBundledRules, localRules }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.yar", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(folder, "*.yara", SearchOption.TopDirectoryOnly)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(128)
            .ToArray();
    }

    private static string FindExecutable(string fileName, IEnumerable<string> candidateFolders)
    {
        foreach (string folder in candidateFolders.Where(folder => !string.IsNullOrWhiteSpace(folder)))
        {
            try
            {
                string candidate = Path.Combine(folder, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
            }
        }

        return string.Empty;
    }

    private static string ParseClamDetection(string output)
    {
        string line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.Contains(" FOUND", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        int colon = line.LastIndexOf(':');
        string tail = colon >= 0 ? line[(colon + 1)..] : line;
        return tail.Replace("FOUND", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string SanitizeDetection(string value)
    {
        char[] safe = value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
            ? character
            : '_').ToArray();
        return new string(safe);
    }

    private static string Compact(string value)
    {
        string compact = string.Join(" ", value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= 500 ? compact : compact[..500] + "…";
    }

    private static ExternalThreatResult10 Error(string engine, string message) =>
        new(engine, true, false, true, string.Empty, 0, new[] { $"{engine}: {message}" });

    private sealed record ProcessExecution10(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut);
}
