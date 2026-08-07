using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class YaraService(IEngineLocatorService locator, IProcessRunner runner, IOptions<SecurityCoreOptions> options) : IYaraService
{
    private const string RuleName = "FFGuardian_Yara_Test";
    private const string Marker = "FFGUARDIAN_YARA_TEST_STRING";

    public async Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken)
    {
        YaraDiagnostics diagnostics = await GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return new("YARA", diagnostics.ExecutablePath, diagnostics.Version, diagnostics.Status == YaraRuntimeStatus.Active, diagnostics.StatusText);
    }

    public async Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        string executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException("Eseguibile YARA non trovato.");
        string[] rules = FindRules();
        if (rules.Length == 0) throw new FileNotFoundException("Regole YARA non disponibili.");
        List<YaraMatch> matches = [];
        foreach (string rule in rules)
        {
            ProcessResult result = await runner.RunAsync(new(executable, [rule, Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
            if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(FormatFailure(result));
            matches.AddRange(Parse(result.StandardOutput, path));
        }
        return matches;
    }

    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        YaraDiagnostics diagnostics = await GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return new("YARA", diagnostics.Status == YaraRuntimeStatus.Active, diagnostics.Version, diagnostics.StatusText, diagnostics.LastCheck, diagnostics.Duration);
    }

    public async Task<YaraDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        string executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        if (executable.Length == 0) return Create(YaraRuntimeStatus.ExecutableNotFound, "ESEGUIBILE YARA NON TROVATO", stopwatch, checkedAt);
        string compiler = await locator.LocateYaraCompilerAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        if (compiler.Length == 0) return Create(YaraRuntimeStatus.CompilerNotFound, "COMPILATORE YARAC NON TROVATO", stopwatch, checkedAt, executable: executable);

        ProcessResult versionResult;
        try
        {
            versionResult = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Create(YaraRuntimeStatus.EngineStartError, "ERRORE AVVIO MOTORE", stopwatch, checkedAt, executable, compiler, stderr: exception.Message);
        }

        string version = FirstLine(versionResult.StandardOutput, versionResult.StandardError);
        if (versionResult.TimedOut || versionResult.ExitCode != 0 || !IsValidVersion(version))
            return Create(YaraRuntimeStatus.EngineStartError, "ERRORE AVVIO MOTORE", stopwatch, checkedAt, executable, compiler, version, stdout: versionResult.StandardOutput, stderr: versionResult.StandardError, exitCode: versionResult.ExitCode, timedOut: versionResult.TimedOut);

        string[] rules = FindRules();
        if (rules.Length == 0) return Create(YaraRuntimeStatus.RulesUnavailable, "REGOLE NON DISPONIBILI", stopwatch, checkedAt, executable, compiler, version, rulesPath: string.Join("; ", RuleDirectories()));

        string validationRoot = Path.Combine(Path.GetTempPath(), "FFGuardian-Yara-Validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(validationRoot);
        try
        {
            foreach (string rule in rules)
            {
                string compiled = Path.Combine(validationRoot, Guid.NewGuid().ToString("N") + ".yarc");
                ProcessResult compileResult = await runner.RunAsync(new(compiler, [rule, compiled], Path.GetDirectoryName(compiler)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
                if (compileResult.TimedOut || compileResult.ExitCode != 0 || !File.Exists(compiled))
                    return Create(YaraRuntimeStatus.RulesInvalid, "REGOLE NON VALIDE", stopwatch, checkedAt, executable, compiler, version, Path.GetDirectoryName(rule) ?? string.Empty, rules.Length, stdout: compileResult.StandardOutput, stderr: compileResult.StandardError, exitCode: compileResult.ExitCode, timedOut: compileResult.TimedOut);
            }

            string testRule = Path.Combine(validationRoot, "ffguardian-selftest.yar");
            string testFile = Path.Combine(validationRoot, "sample with spaces.txt");
            await File.WriteAllTextAsync(testRule, "rule FFGuardian_Yara_Test\n{\n    strings:\n        $test = \"FFGUARDIAN_YARA_TEST_STRING\"\n\n    condition:\n        $test\n}\n", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(testFile, Marker, Encoding.ASCII, cancellationToken).ConfigureAwait(false);
            ProcessResult selfTest = await runner.RunAsync(new(executable, [testRule, testFile], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
            bool passed = !selfTest.TimedOut && selfTest.ExitCode == 0 && selfTest.StandardOutput.Contains(RuleName, StringComparison.Ordinal);
            if (!passed) return Create(YaraRuntimeStatus.SelfTestFailed, "SELF-TEST FALLITO", stopwatch, checkedAt, executable, compiler, version, Path.GetDirectoryName(rules[0]) ?? string.Empty, rules.Length, true, false, selfTest.StandardOutput, selfTest.StandardError, selfTest.ExitCode, selfTest.TimedOut);
            return Create(YaraRuntimeStatus.Active, "YARA REALE: ATTIVO", stopwatch, checkedAt, executable, compiler, version, Path.GetDirectoryName(rules[0]) ?? string.Empty, rules.Length, true, true, selfTest.StandardOutput, selfTest.StandardError, selfTest.ExitCode, selfTest.TimedOut);
        }
        finally { TryDelete(validationRoot); }
    }

    public static IReadOnlyList<YaraMatch> Parse(string output, string target) => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0).Select(line => new YaraMatch(line.Split(' ', 2)[0], target, line)).ToArray();
    private string[] FindRules() => RuleDirectories().Where(Directory.Exists).SelectMany(directory => Directory.EnumerateFiles(directory, "*.yar", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(directory, "*.yara", SearchOption.AllDirectories))).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private IEnumerable<string> RuleDirectories()
    {
        string root = Path.GetFullPath(options.Value.BaseDirectory);
        yield return Path.GetFullPath(Path.Combine(root, "Engine", "Yara", "Rules"));
        yield return Path.GetFullPath(Path.Combine(root, "Rules", "Yara"));
        yield return Path.GetFullPath(Path.Combine(root, "Rules"));
    }
    private static bool IsValidVersion(string value)
    {
        foreach (string token in value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            if (Version.TryParse(token.Trim().TrimStart('v', 'V'), out Version? parsed) && parsed.Major >= 1) return true;
        return false;
    }
    private static YaraDiagnostics Create(YaraRuntimeStatus status, string text, Stopwatch stopwatch, DateTimeOffset checkedAt, string executable = "", string compiler = "", string version = "--", string rulesPath = "", int ruleCount = 0, bool rulesValid = false, bool selfTest = false, string stdout = "", string stderr = "", int exitCode = -1, bool timedOut = false)
    {
        stopwatch.Stop();
        return new(status, text, executable, executable.Length == 0 ? string.Empty : Path.GetFileName(executable), compiler, version, rulesPath, ruleCount, rulesValid, selfTest, stdout, stderr, exitCode, timedOut, checkedAt, stopwatch.Elapsed);
    }
    private static string FormatFailure(ProcessResult result) => result.TimedOut ? "Timeout YARA." : $"YARA exit code {result.ExitCode}: {result.StandardError}";
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
        string database = GetDatabaseDirectory();
        EnsureDatabaseAvailable(database);
        ProcessResult result = await runner.RunAsync(new(executable, ["--no-summary", $"--database={database}", Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"ClamAV exit code {result.ExitCode}." : result.StandardError);
        return Parse(result.StandardOutput);
    }

    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        EngineVersionInfo version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
        if (!version.Operational) return new("ClamAV", false, version.Version, version.Message, DateTimeOffset.UtcNow, stopwatch.Elapsed);

        string database = GetDatabaseDirectory();
        try
        {
            EnsureDatabaseAvailable(database);
        }
        catch (FileNotFoundException exception)
        {
            stopwatch.Stop();
            return new("ClamAV", false, version.Version, exception.Message, DateTimeOffset.UtcNow, stopwatch.Elapsed);
        }

        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-ClamAV-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string clean = Path.Combine(root, "clean.txt");
            string eicar = Path.Combine(root, "eicar.txt");
            await File.WriteAllTextAsync(clean, "FFGuardian harmless test", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(eicar, Eicar, Encoding.ASCII, cancellationToken).ConfigureAwait(false);

            ProcessResult selfTest = await runner.RunAsync(new(
                version.Path,
                ["--no-summary", $"--database={database}", clean, eicar],
                Path.GetDirectoryName(version.Path)!,
                options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);

            if (selfTest.TimedOut || selfTest.ExitCode is not 0 and not 1)
            {
                stopwatch.Stop();
                string failure = selfTest.TimedOut ? "Timeout durante il self-test ClamAV." : string.IsNullOrWhiteSpace(selfTest.StandardError) ? $"ClamAV exit code {selfTest.ExitCode}." : selfTest.StandardError.Trim();
                return new("ClamAV", false, version.Version, failure, DateTimeOffset.UtcNow, stopwatch.Elapsed);
            }

            IReadOnlyList<ClamAvDetection> detections = Parse(selfTest.StandardOutput);
            bool cleanDetected = detections.Any(item => string.Equals(Path.GetFullPath(item.TargetPath), Path.GetFullPath(clean), StringComparison.OrdinalIgnoreCase));
            bool eicarDetected = detections.Any(item => string.Equals(Path.GetFullPath(item.TargetPath), Path.GetFullPath(eicar), StringComparison.OrdinalIgnoreCase) && item.Signature.Contains("Eicar", StringComparison.OrdinalIgnoreCase));
            bool operational = !cleanDetected && eicarDetected;
            stopwatch.Stop();
            return new("ClamAV", operational, version.Version,
                operational
                    ? $"Database reale caricato da {database}; file innocuo pulito ed EICAR rilevato."
                    : "Self-test ClamAV non superato: risultato clean/EICAR incoerente.",
                DateTimeOffset.UtcNow, stopwatch.Elapsed);
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

    private string GetDatabaseDirectory() => Path.GetFullPath(Path.Combine(options.Value.BaseDirectory, "Engine", "ClamAV", "database"));

    private static void EnsureDatabaseAvailable(string database)
    {
        if (!Directory.Exists(database)) throw new FileNotFoundException($"Database ClamAV assente: {database}");
        bool hasSignatures = Directory.EnumerateFiles(database, "*", SearchOption.TopDirectoryOnly)
            .Any(path => Path.GetExtension(path).Equals(".cvd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".cld", StringComparison.OrdinalIgnoreCase));
        if (!hasSignatures) throw new FileNotFoundException($"Database ClamAV presente ma senza firme .cvd/.cld: {database}");
    }

    private static void TryDelete(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class FreshClamService(IOptions<SecurityCoreOptions> options, IProcessRunner runner) : IFreshClamService
{
    public async Task<EngineHealthResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string root = Path.GetFullPath(options.Value.BaseDirectory);
        string executable = Path.Combine(root, "Engine", "ClamAV", "freshclam.exe");
        string config = Path.Combine(root, "Engine", "ClamAV", "freshclam.conf");
        string database = Path.Combine(root, "Engine", "ClamAV", "database");
        if (!File.Exists(executable)) return new("FreshClam", false, "--", "Eseguibile non trovato.", DateTimeOffset.UtcNow, stopwatch.Elapsed);
        if (!File.Exists(config)) return new("FreshClam", false, "--", $"Configurazione freshclam.conf assente: {config}", DateTimeOffset.UtcNow, stopwatch.Elapsed);

        ProcessResult result = await runner.RunAsync(new(executable, [$"--config-file={config}", "--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        bool signaturesPresent = Directory.Exists(database) && Directory.EnumerateFiles(database, "*", SearchOption.TopDirectoryOnly)
            .Any(path => Path.GetExtension(path).Equals(".cvd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".cld", StringComparison.OrdinalIgnoreCase));
        bool operational = !result.TimedOut && result.ExitCode == 0 && signaturesPresent;
        string version = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "--";
        string message = operational
            ? $"FreshClam configurato; database firme disponibile in {database}."
            : !signaturesPresent
                ? $"Database firme assente o vuoto: {database}"
                : string.IsNullOrWhiteSpace(result.StandardError) ? $"FreshClam exit code {result.ExitCode}." : result.StandardError.Trim();
        return new("FreshClam", operational, version, message, DateTimeOffset.UtcNow, stopwatch.Elapsed);
    }
}