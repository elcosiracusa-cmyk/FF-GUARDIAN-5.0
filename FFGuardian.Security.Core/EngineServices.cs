using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class YaraService(IEngineLocatorService locator, IProcessRunner runner, IOptions<SecurityCoreOptions> options) : IYaraService
{
    private const string RuleName = "FFGuardian_Runtime_SelfTest";
    private const string Marker = "FFGUARDIAN_YARA_TEST_STRING";

    public async Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("YARA", string.Empty, "--", false, "Eseguibile non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        string version = FirstLine(result.StandardOutput, result.StandardError);
        bool operational = !result.TimedOut && result.ExitCode == 0;
        return new("YARA", executable, version, operational, operational ? "Versione verificata." : result.TimedOut ? "Timeout." : result.StandardError);
    }

    public async Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException("YARA non trovato.");
        string rulesDirectory = Path.Combine(options.Value.BaseDirectory, "Rules");
        string? rule = Directory.Exists(rulesDirectory)
            ? Directory.EnumerateFiles(rulesDirectory, "*.yar", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(rulesDirectory, "*.yara", SearchOption.AllDirectories)).FirstOrDefault()
            : null;
        if (rule is null) throw new FileNotFoundException("Nessuna regola YARA disponibile.");
        ProcessResult result = await runner.RunAsync(new(executable, [rule, Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(result.StandardError);
        return Parse(result.StandardOutput, path);
    }

    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("YARA", false, "--", "Eseguibile non trovato.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Yara-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rule = Path.Combine(root, "selftest.yar");
            string sample = Path.Combine(root, "sample.txt");
            await File.WriteAllTextAsync(rule, $"rule {RuleName} {{ strings: $a = \"{Marker}\" ascii condition: $a }}", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(sample, Marker, cancellationToken).ConfigureAwait(false);
            ProcessResult result = await runner.RunAsync(new(executable, [rule, sample], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
            bool operational = !result.TimedOut && result.ExitCode == 0 && result.StandardOutput.Contains(RuleName, StringComparison.Ordinal);
            EngineVersionInfo version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new("YARA", operational, version.Version, operational ? "Regola innocua rilevata realmente." : result.StandardError + result.StandardOutput, DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }
        finally { TryDelete(root); }
    }

    public static IReadOnlyList<YaraMatch> Parse(string output, string target) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .Select(line => new YaraMatch(line.Split(' ', 2)[0], target, line))
        .ToArray();

    private static string FirstLine(params string[] values) => values.SelectMany(value => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Select(value => value.Trim()).FirstOrDefault(value => value.Length > 0) ?? "--";
    private static void TryDelete(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class ClamAvService(IEngineLocatorService locator, IProcessRunner runner, IOptions<SecurityCoreOptions> options) : IClamAvService
{
    private const string Eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    public async Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateClamAvAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("ClamAV", string.Empty, "--", false, "Eseguibile non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        string version = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "--";
        bool operational = !result.TimedOut && result.ExitCode == 0;
        return new("ClamAV", executable, version, operational, operational ? "Versione verificata." : result.StandardError);
    }

    public async Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateClamAvAsync(cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException("ClamAV non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--no-summary", Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(result.StandardError);
        return Parse(result.StandardOutput);
    }

    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        EngineVersionInfo version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
        if (!version.Operational) return new("ClamAV", false, version.Version, version.Message, DateTimeOffset.UtcNow, stopwatch.Elapsed);
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-ClamAV-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string clean = Path.Combine(root, "clean.txt");
            string eicar = Path.Combine(root, "eicar.txt");
            await File.WriteAllTextAsync(clean, "FFGuardian harmless test", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(eicar, Eicar, Encoding.ASCII, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ClamAvDetection> cleanResult = await ScanFileAsync(clean, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ClamAvDetection> eicarResult = await ScanFileAsync(eicar, cancellationToken).ConfigureAwait(false);
            bool operational = cleanResult.Count == 0 && eicarResult.Any(item => item.Signature.Contains("Eicar", StringComparison.OrdinalIgnoreCase));
            stopwatch.Stop();
            return new("ClamAV", operational, version.Version, operational ? "File innocuo pulito ed EICAR rilevato realmente." : "Self-test ClamAV non superato.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }
        finally { TryDelete(root); }
    }

    public static IReadOnlyList<ClamAvDetection> Parse(string output)
    {
        List<ClamAvDetection> detections = [];
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int found = line.LastIndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
            if (found < 0) continue;
            int separator = line.LastIndexOf(": ", found, StringComparison.Ordinal);
            if (separator <= 0 || separator + 2 >= found) continue;
            string target = line[..separator].Trim();
            string signature = line[(separator + 2)..found].Trim();
            if (target.Length > 0 && signature.Length > 0) detections.Add(new(signature, target, line));
        }
        return detections;
    }

    private static void TryDelete(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class FreshClamService(IOptions<SecurityCoreOptions> options, IProcessRunner runner) : IFreshClamService
{
    public async Task<EngineHealthResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string executable = Path.Combine(options.Value.BaseDirectory, "Engine", "ClamAV", "freshclam.exe");
        if (!File.Exists(executable)) return new("FreshClam", false, "--", "Eseguibile non trovato.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        bool operational = !result.TimedOut && result.ExitCode == 0;
        return new("FreshClam", operational, result.StandardOutput.Trim(), operational ? "Versione verificata; test rete non eseguito." : result.StandardError, DateTimeOffset.UtcNow, stopwatch.Elapsed);
    }
}
