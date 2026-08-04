using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal enum YaraHealthState
{
    Active,
    NotInstalled,
    RulesUnavailable,
    RulesInvalid,
    EngineError
}

internal enum YaraRiskLevel
{
    Informational,
    Suspicious,
    High,
    Critical
}

internal sealed record YaraConfiguration(
    string RootDirectory,
    string EngineDirectory,
    string RulesDirectory,
    string CustomRulesDirectory,
    string CompiledDirectory,
    string UpdatesDirectory,
    string LogsDirectory,
    string QuarantineDirectory,
    string ManifestPath,
    string ManifestSignaturePath,
    string? ManifestPublicKeyPem,
    long MaximumFileSizeBytes,
    TimeSpan ProcessTimeout,
    IReadOnlyList<string> Exclusions)
{
    public string YaraExecutable => Path.Combine(EngineDirectory, "yara64.exe");
    public string YaracExecutable => Path.Combine(EngineDirectory, "yarac64.exe");
    public string CompiledRulesPath => Path.Combine(CompiledDirectory, "ffguardian-rules.yarc");

    public static YaraConfiguration CreateDefault()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine", "Yara");
        return new YaraConfiguration(
            root,
            root,
            Path.Combine(root, "Rules"),
            Path.Combine(root, "Rules", "custom"),
            Path.Combine(root, "Compiled"),
            Path.Combine(root, "Updates"),
            Path.Combine(root, "Logs"),
            Path.Combine(root, "Quarantine"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "yara-engine-manifest.json"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "yara-engine-manifest.sig"),
            Environment.GetEnvironmentVariable("FFGUARDIAN_YARA_MANIFEST_PUBLIC_KEY"),
            256L * 1024L * 1024L,
            TimeSpan.FromMinutes(5),
            Array.Empty<string>());
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(RulesDirectory);
        Directory.CreateDirectory(CustomRulesDirectory);
        Directory.CreateDirectory(CompiledDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(QuarantineDirectory);
    }
}

internal sealed record YaraEngineManifest(
    string Component,
    string Version,
    string Architecture,
    string DownloadUrl,
    string Sha256,
    string PackageFileName,
    DateTime PublishedUtc);

internal sealed record YaraRuleMetadata(
    string RuleName,
    string SourcePath,
    string Author,
    string Description,
    string Category,
    string Severity,
    DateTime? Date,
    bool Enabled);

internal sealed record YaraStringMatch(
    string Identifier,
    long Offset,
    string Value);

internal sealed record YaraScanResult(
    string RuleName,
    string Namespace,
    IReadOnlyList<string> Tags,
    string FilePath,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<YaraStringMatch> StringMatches,
    YaraRiskLevel Risk,
    DateTime DetectedUtc,
    TimeSpan Duration,
    string EngineVersion,
    string RulePackageVersion);

internal sealed record YaraHealthReport(
    YaraHealthState State,
    string Version,
    int ValidRules,
    DateTime CheckedUtc,
    string Detail);

internal sealed record YaraProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut);

internal sealed class YaraProcessRunner : IDisposable
{
    private readonly ConcurrentDictionary<int, Process> _active = new();
    private bool _disposed;

    public async Task<YaraProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!File.Exists(executable))
            throw new FileNotFoundException("Eseguibile YARA non trovato.", executable);

        ProcessStartInfo start = new()
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = start, EnableRaisingEvents = true };
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException("Avvio del processo YARA non riuscito.");
        _active[process.Id] = process;

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        finally
        {
            _active.TryRemove(process.Id, out _);
        }

        string stdout = await outputTask.ConfigureAwait(false);
        string stderr = await errorTask.ConfigureAwait(false);
        stopwatch.Stop();
        progress?.Report(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        return new YaraProcessResult(
            timedOut ? -1 : process.ExitCode,
            stdout,
            stderr,
            stopwatch.Elapsed,
            timedOut);
    }

    public Task StopAllAsync()
    {
        foreach (Process process in _active.Values)
            TryKill(process);
        return Task.CompletedTask;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (Process process in _active.Values)
            TryKill(process);
        _active.Clear();
    }
}

internal sealed class YaraManifestVerifier
{
    private readonly YaraConfiguration _configuration;

    public YaraManifestVerifier(YaraConfiguration configuration) =>
        _configuration = configuration;

    public YaraEngineManifest LoadAndVerify()
    {
        if (!File.Exists(_configuration.ManifestPath))
            throw new FileNotFoundException("Manifest YARA FFGuardian non trovato.", _configuration.ManifestPath);
        if (!File.Exists(_configuration.ManifestSignaturePath))
            throw new CryptographicException("Firma del manifest YARA non disponibile.");
        if (string.IsNullOrWhiteSpace(_configuration.ManifestPublicKeyPem))
            throw new CryptographicException("Chiave pubblica del manifest YARA non configurata.");

        byte[] manifestBytes = File.ReadAllBytes(_configuration.ManifestPath);
        byte[] signature = Convert.FromBase64String(
            File.ReadAllText(_configuration.ManifestSignaturePath).Trim());
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(_configuration.ManifestPublicKeyPem);
        bool verified = rsa.VerifyData(
            manifestBytes,
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);
        if (!verified)
            throw new CryptographicException("Firma RSA-PSS del manifest YARA non valida.");

        YaraEngineManifest? manifest = JsonSerializer.Deserialize<YaraEngineManifest>(
            manifestBytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest is null)
            throw new InvalidDataException("Manifest YARA non leggibile.");
        Validate(manifest);
        return manifest;
    }

    private static void Validate(YaraEngineManifest manifest)
    {
        if (!string.Equals(manifest.Component, "yara-engine", StringComparison.Ordinal))
            throw new InvalidDataException("Componente manifest YARA non valido.");
        if (!string.Equals(manifest.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException("Il pacchetto YARA non è x64.");
        if (!Version.TryParse(manifest.Version, out _))
            throw new InvalidDataException("Versione YARA del manifest non valida.");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("URL YARA non HTTPS o non appartenente a GitHub.");
        if (!Regex.IsMatch(manifest.Sha256, "^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("SHA-256 YARA non valido.");
        if (Path.GetFileName(manifest.PackageFileName) != manifest.PackageFileName)
            throw new InvalidDataException("Nome pacchetto YARA non valido.");
    }
}

internal sealed class YaraOutputParser
{
    private static readonly Regex Header = new(
        "^(?<rule>[A-Za-z_][A-Za-z0-9_]*)\\s*(?:\\[(?<tags>[^]]*)\\])?\\s*(?<path>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StringLine = new(
        "^0x(?<offset>[0-9A-Fa-f]+):(?<identifier>\\$[A-Za-z0-9_]+):\\s*(?<value>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<YaraScanResult> Parse(
        string output,
        TimeSpan duration,
        string engineVersion,
        string rulePackageVersion)
    {
        List<YaraScanResult> results = [];
        string? rule = null;
        string file = string.Empty;
        List<string> tags = [];
        List<YaraStringMatch> strings = [];

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(rule))
                return;
            Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
            results.Add(new YaraScanResult(
                rule,
                "default",
                tags.ToArray(),
                file,
                metadata,
                strings.ToArray(),
                Classify(tags, metadata),
                DateTime.UtcNow,
                duration,
                engineVersion,
                rulePackageVersion));
            rule = null;
            file = string.Empty;
            tags = [];
            strings = [];
        }

        foreach (string rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            Match stringMatch = StringLine.Match(line);
            if (stringMatch.Success && rule is not null)
            {
                long offset = Convert.ToInt64(stringMatch.Groups["offset"].Value, 16);
                strings.Add(new YaraStringMatch(
                    stringMatch.Groups["identifier"].Value,
                    offset,
                    stringMatch.Groups["value"].Value));
                continue;
            }

            Match header = Header.Match(line);
            if (!header.Success || line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase))
                continue;
            Flush();
            rule = header.Groups["rule"].Value;
            file = header.Groups["path"].Value.Trim('"');
            tags = header.Groups["tags"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        Flush();
        return results;
    }

    private static YaraRiskLevel Classify(
        IReadOnlyCollection<string> tags,
        IReadOnlyDictionary<string, string> metadata)
    {
        string value = metadata.TryGetValue("severity", out string? severity)
            ? severity
            : string.Join(' ', tags);
        if (value.Contains("critical", StringComparison.OrdinalIgnoreCase))
            return YaraRiskLevel.Critical;
        if (value.Contains("high", StringComparison.OrdinalIgnoreCase))
            return YaraRiskLevel.High;
        if (value.Contains("suspicious", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("medium", StringComparison.OrdinalIgnoreCase))
            return YaraRiskLevel.Suspicious;
        return YaraRiskLevel.Informational;
    }
}

internal sealed class YaraRuleManager
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly Action<string> _log;

    public YaraRuleManager(
        YaraConfiguration configuration,
        YaraProcessRunner runner,
        Action<string> log)
    {
        _configuration = configuration;
        _runner = runner;
        _log = log;
    }

    public IReadOnlyList<string> GetEnabledRuleFiles() => Directory.Exists(_configuration.RulesDirectory)
        ? Directory.EnumerateFiles(_configuration.RulesDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".yar", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".yara", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(".disabled.yar", StringComparison.OrdinalIgnoreCase) &&
                           !path.EndsWith(".disabled.yara", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : Array.Empty<string>();

    public async Task<int> ValidateAndCompileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files = GetEnabledRuleFiles();
        if (files.Count == 0)
            throw new InvalidOperationException("Nessuna regola .yar o .yara disponibile.");
        EnsureUniqueRuleNames(files);
        if (!File.Exists(_configuration.YaracExecutable))
            throw new FileNotFoundException("yarac64.exe non trovato.", _configuration.YaracExecutable);

        Directory.CreateDirectory(_configuration.CompiledDirectory);
        string temporary = _configuration.CompiledRulesPath + ".tmp";
        List<string> arguments = [.. files, temporary];
        YaraProcessResult result = await _runner.RunAsync(
            _configuration.YaracExecutable,
            arguments,
            _configuration.EngineDirectory,
            _configuration.ProcessTimeout,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || !File.Exists(temporary))
        {
            TryDelete(temporary);
            throw new InvalidDataException(
                "Regole YARA non valide: " + FirstUsefulLine(result.StandardError, result.StandardOutput));
        }
        File.Move(temporary, _configuration.CompiledRulesPath, overwrite: true);
        _log($"YARA_RULES_COMPILED count={files.Count}");
        return files.Count;
    }

    public async Task<YaraRuleMetadata> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(sourcePath);
        if (!extension.Equals(".yar", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yara", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sono accettati soltanto file .yar e .yara.");
        Directory.CreateDirectory(_configuration.CustomRulesDirectory);
        string destination = Path.Combine(
            _configuration.CustomRulesDirectory,
            Path.GetFileName(sourcePath));
        string temporary = destination + ".importing";
        File.Copy(sourcePath, temporary, overwrite: true);
        try
        {
            File.Move(temporary, destination, overwrite: true);
            await ValidateAndCompileAsync(cancellationToken).ConfigureAwait(false);
            return ReadMetadata(destination);
        }
        catch
        {
            TryDelete(destination);
            TryDelete(temporary);
            throw;
        }
    }

    public YaraRuleMetadata ReadMetadata(string path)
    {
        string text = File.ReadAllText(path);
        string ruleName = Regex.Match(text, @"\brule\s+([A-Za-z_][A-Za-z0-9_]*)")
            .Groups[1].Value;
        string Value(string key) => Regex.Match(
            text,
            $@"\b{Regex.Escape(key)}\s*=\s*\"(?<v>[^\"]*)\"",
            RegexOptions.IgnoreCase).Groups["v"].Value;
        DateTime? date = DateTime.TryParse(Value("date"), out DateTime parsed) ? parsed : null;
        return new YaraRuleMetadata(
            ruleName,
            path,
            Value("author"),
            Value("description"),
            Value("category"),
            Value("severity"),
            date,
            !path.Contains(".disabled.", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureUniqueRuleNames(IEnumerable<string> files)
    {
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        Regex ruleRegex = new(@"\brule\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        foreach (string file in files)
        {
            foreach (Match match in ruleRegex.Matches(File.ReadAllText(file)))
            {
                string name = match.Groups[1].Value;
                if (names.TryGetValue(name, out string? existing))
                    throw new InvalidDataException(
                        $"Nome regola duplicato '{name}' in '{existing}' e '{file}'.");
                names[name] = file;
            }
        }
    }

    private static string FirstUsefulLine(params string[] values) => values
        .SelectMany(value => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0) ?? "errore sconosciuto";

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class YaraHealthCheckService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly YaraRuleManager _rules;

    public YaraHealthCheckService(
        YaraConfiguration configuration,
        YaraProcessRunner runner,
        YaraRuleManager rules)
    {
        _configuration = configuration;
        _runner = runner;
        _rules = rules;
    }

    public async Task<YaraHealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        DateTime checkedUtc = DateTime.UtcNow;
        if (!File.Exists(_configuration.YaraExecutable) ||
            !File.Exists(_configuration.YaracExecutable))
            return new YaraHealthReport(
                YaraHealthState.NotInstalled, "--", 0, checkedUtc,
                "yara64.exe o yarac64.exe non presenti.");
        try
        {
            YaraProcessResult versionResult = await _runner.RunAsync(
                _configuration.YaraExecutable,
                ["--version"],
                _configuration.EngineDirectory,
                TimeSpan.FromSeconds(20),
                cancellationToken).ConfigureAwait(false);
            if (versionResult.ExitCode != 0 || versionResult.TimedOut)
                return new YaraHealthReport(
                    YaraHealthState.EngineError, "--", 0, checkedUtc,
                    "yara64.exe --version non riuscito.");
            string version = Regex.Match(
                versionResult.StandardOutput + versionResult.StandardError,
                @"\d+(?:\.\d+){1,3}").Value;
            if (_rules.GetEnabledRuleFiles().Count == 0)
                return new YaraHealthReport(
                    YaraHealthState.RulesUnavailable, version, 0, checkedUtc,
                    "Nessuna regola disponibile.");
            int count;
            try
            {
                count = await _rules.ValidateAndCompileAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                return new YaraHealthReport(
                    YaraHealthState.RulesInvalid, version, 0, checkedUtc, ex.Message);
            }
            return new YaraHealthReport(
                YaraHealthState.Active, version, count, checkedUtc,
                "Versione, regole compilate e motore verificati.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new YaraHealthReport(
                YaraHealthState.EngineError, "--", 0, checkedUtc, ex.Message);
        }
    }
}

internal sealed class YaraScannerService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly YaraOutputParser _parser;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _health;
    private readonly Action<string> _log;

    public YaraScannerService(
        YaraConfiguration configuration,
        YaraProcessRunner runner,
        YaraOutputParser parser,
        Func<CancellationToken, Task<YaraHealthReport>> health,
        Action<string> log)
    {
        _configuration = configuration;
        _runner = runner;
        _parser = parser;
        _health = health;
        _log = log;
    }

    public Task<IReadOnlyList<YaraScanResult>> ScanFileAsync(
        string path,
        bool includeStrings,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null) =>
        ScanTargetAsync(path, recursive: false, includeStrings, cancellationToken, progress);

    public Task<IReadOnlyList<YaraScanResult>> ScanFolderAsync(
        string path,
        bool includeStrings,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null) =>
        ScanTargetAsync(path, recursive: true, includeStrings, cancellationToken, progress);

    public async Task<IReadOnlyList<YaraScanResult>> ScanQuickAsync(
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        ];
        List<YaraScanResult> results = [];
        foreach (string root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(await ScanFolderAsync(root, false, cancellationToken, progress)
                .ConfigureAwait(false));
        }
        return results;
    }

    private async Task<IReadOnlyList<YaraScanResult>> ScanTargetAsync(
        string target,
        bool recursive,
        bool includeStrings,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        string fullPath = Path.GetFullPath(target);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new FileNotFoundException("Percorso di scansione YARA non trovato.", fullPath);
        if (IsExcluded(fullPath))
            return Array.Empty<YaraScanResult>();
        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > _configuration.MaximumFileSizeBytes)
            throw new InvalidOperationException("File oltre il limite massimo configurato.");

        YaraHealthReport health = await _health(cancellationToken).ConfigureAwait(false);
        if (health.State != YaraHealthState.Active)
            throw new InvalidOperationException("YARA non attivo: " + health.Detail);

        List<string> arguments = [];
        if (recursive)
        {
            arguments.Add("--recursive");
            arguments.Add("--no-follow-symlinks");
        }
        if (includeStrings)
            arguments.Add("--print-strings");
        arguments.Add("--compiled-rules");
        arguments.Add(_configuration.CompiledRulesPath);
        arguments.Add(fullPath);

        progress?.Report("YARA: scansione di " + fullPath);
        YaraProcessResult process = await _runner.RunAsync(
            _configuration.YaraExecutable,
            arguments,
            _configuration.EngineDirectory,
            _configuration.ProcessTimeout,
            cancellationToken,
            progress).ConfigureAwait(false);
        if (process.TimedOut)
            throw new TimeoutException("La scansione YARA ha superato il timeout.");
        if (process.ExitCode is not (0 or 1))
            throw new InvalidOperationException(
                "Errore YARA: " + (string.IsNullOrWhiteSpace(process.StandardError)
                    ? process.StandardOutput
                    : process.StandardError));
        IReadOnlyList<YaraScanResult> results = _parser.Parse(
            process.StandardOutput,
            process.Duration,
            health.Version,
            File.GetLastWriteTimeUtc(_configuration.CompiledRulesPath).ToString("yyyyMMddHHmmss"));
        _log($"YARA_SCAN target=\"{fullPath}\" matches={results.Count} durationMs={process.Duration.TotalMilliseconds:F0}");
        return results;
    }

    private bool IsExcluded(string path) => _configuration.Exclusions.Any(exclusion =>
        path.StartsWith(Path.GetFullPath(exclusion), StringComparison.OrdinalIgnoreCase));
}

internal sealed record YaraQuarantineRecord(
    string Id,
    string OriginalPath,
    string StoredPath,
    string Sha256,
    DateTime QuarantinedUtc,
    string RuleName);

internal sealed class YaraQuarantineService
{
    private readonly YaraConfiguration _configuration;
    private readonly Action<string> _log;

    public YaraQuarantineService(YaraConfiguration configuration, Action<string> log)
    {
        _configuration = configuration;
        _log = log;
    }

    public async Task<YaraQuarantineRecord> QuarantineAsync(
        string filePath,
        string ruleName,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File da quarantinare non trovato.", fullPath);
        Directory.CreateDirectory(_configuration.QuarantineDirectory);
        string id = Guid.NewGuid().ToString("N");
        string storedPath = Path.Combine(_configuration.QuarantineDirectory, id + ".ffq");
        string hash;
        await using (FileStream input = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] digest = await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false);
            hash = Convert.ToHexString(digest);
        }
        File.Move(fullPath, storedPath);
        File.SetAttributes(storedPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
        YaraQuarantineRecord record = new(id, fullPath, storedPath, hash, DateTime.UtcNow, ruleName);
        string metadataPath = storedPath + ".json";
        await File.WriteAllTextAsync(
            metadataPath,
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
        _log($"YARA_QUARANTINE id={id} sha256={hash} original=\"{fullPath}\"");
        return record;
    }

    public async Task RestoreAsync(
        string id,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed)
            throw new InvalidOperationException("Il ripristino richiede conferma esplicita.");
        string metadataPath = Path.Combine(_configuration.QuarantineDirectory, id + ".ffq.json");
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException("Record di quarantena non trovato.", metadataPath);
        YaraQuarantineRecord? record = JsonSerializer.Deserialize<YaraQuarantineRecord>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
        if (record is null || !File.Exists(record.StoredPath))
            throw new InvalidDataException("Archivio di quarantena non valido.");
        await using FileStream input = new(record.StoredPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)
            .ConfigureAwait(false));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual), Convert.FromHexString(record.Sha256)))
            throw new CryptographicException("Integrità della quarantena non valida.");
        Directory.CreateDirectory(Path.GetDirectoryName(record.OriginalPath)!);
        File.SetAttributes(record.StoredPath, FileAttributes.Normal);
        File.Move(record.StoredPath, record.OriginalPath, overwrite: false);
        File.Delete(metadataPath);
        _log($"YARA_RESTORE id={id} path=\"{record.OriginalPath}\"");
    }
}

internal sealed class YaraInstallationService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraManifestVerifier _manifestVerifier;
    private readonly YaraProcessRunner _runner;
    private readonly HttpClient _httpClient;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _healthCheck;
    private readonly Action<string> _log;

    public YaraInstallationService(
        YaraConfiguration configuration,
        YaraManifestVerifier manifestVerifier,
        YaraProcessRunner runner,
        HttpClient httpClient,
        Func<CancellationToken, Task<YaraHealthReport>> healthCheck,
        Action<string> log)
    {
        _configuration = configuration;
        _manifestVerifier = manifestVerifier;
        _runner = runner;
        _httpClient = httpClient;
        _healthCheck = healthCheck;
        _log = log;
    }

    public async Task InstallAsync(
        IProgress<int>? percent,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        if (!Environment.Is64BitOperatingSystem)
            throw new PlatformNotSupportedException("YARA FFGuardian richiede Windows x64.");
        YaraEngineManifest manifest = _manifestVerifier.LoadAndVerify();
        _configuration.EnsureDirectories();
        string session = Path.Combine(_configuration.UpdatesDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        string archive = Path.Combine(session, manifest.PackageFileName);
        string staging = Path.Combine(session, "extracted");
        try
        {
            status?.Report("Download pacchetto YARA ufficiale");
            using HttpResponseMessage response = await _httpClient.GetAsync(
                manifest.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using FileStream destination = new(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                if (total > 0)
                    percent?.Report((int)Math.Clamp(written * 70 / total, 0, 70));
            }
            status?.Report("Verifica SHA-256");
            destination.Close();
            string actualHash;
            await using (FileStream file = File.OpenRead(archive))
                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)
                    .ConfigureAwait(false));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(manifest.Sha256)))
                throw new CryptographicException("SHA-256 del pacchetto YARA non corrispondente.");
            percent?.Report(75);

            status?.Report("Estrazione controllata");
            ExtractSelectedFiles(archive, staging);
            percent?.Report(85);
            await InstallFromStagingAsync(staging, cancellationToken).ConfigureAwait(false);
            percent?.Report(95);
            YaraHealthReport health = await _healthCheck(cancellationToken).ConfigureAwait(false);
            if (health.State != YaraHealthState.Active)
                throw new InvalidOperationException("Test finale YARA fallito: " + health.Detail);
            percent?.Report(100);
            _log($"YARA_INSTALL version={manifest.Version} result=success");
        }
        finally
        {
            TryDeleteDirectory(session);
        }
    }

    internal async Task InstallFromStagingAsync(string staging, CancellationToken cancellationToken)
    {
        string yara = Directory.EnumerateFiles(staging, "yara*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("64", StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(path).Equals("yara.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("yara64.exe assente nel pacchetto.");
        string yarac = Directory.EnumerateFiles(staging, "yarac*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("64", StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(path).Equals("yarac.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("yarac64.exe assente nel pacchetto.");
        Directory.CreateDirectory(_configuration.EngineDirectory);
        File.Copy(yara, _configuration.YaraExecutable, overwrite: true);
        File.Copy(yarac, _configuration.YaracExecutable, overwrite: true);
        foreach (string dll in Directory.EnumerateFiles(staging, "*.dll", SearchOption.AllDirectories))
            File.Copy(dll, Path.Combine(_configuration.EngineDirectory, Path.GetFileName(dll)), overwrite: true);
        YaraProcessResult version = await _runner.RunAsync(
            _configuration.YaraExecutable,
            ["--version"],
            _configuration.EngineDirectory,
            TimeSpan.FromSeconds(20),
            cancellationToken).ConfigureAwait(false);
        if (version.ExitCode != 0)
            throw new InvalidOperationException("Il nuovo motore YARA non si avvia.");
    }

    private static void ExtractSelectedFiles(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        using ZipArchive zip = ZipFile.OpenRead(archive);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = Path.GetFileName(entry.FullName);
            bool allowed = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase);
            if (!allowed || string.IsNullOrWhiteSpace(name))
                continue;
            string target = Path.GetFullPath(Path.Combine(destination, name));
            if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Percorso ZIP YARA non sicuro.");
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class YaraUpdateService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraInstallationService _installer;
    private readonly YaraManifestVerifier _manifestVerifier;
    private readonly YaraProcessRunner _runner;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _healthCheck;
    private readonly Action<string> _log;

    public YaraUpdateService(
        YaraConfiguration configuration,
        YaraInstallationService installer,
        YaraManifestVerifier manifestVerifier,
        YaraProcessRunner runner,
        Func<CancellationToken, Task<YaraHealthReport>> healthCheck,
        Action<string> log)
    {
        _configuration = configuration;
        _installer = installer;
        _manifestVerifier = manifestVerifier;
        _runner = runner;
        _healthCheck = healthCheck;
        _log = log;
    }

    public async Task UpdateAsync(
        IProgress<int>? percent,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        YaraEngineManifest manifest = _manifestVerifier.LoadAndVerify();
        YaraHealthReport current = await _healthCheck(cancellationToken).ConfigureAwait(false);
        if (current.State == YaraHealthState.Active &&
            Version.TryParse(current.Version, out Version? installed) &&
            Version.TryParse(manifest.Version, out Version? available) &&
            installed >= available)
        {
            status?.Report("Il motore YARA è già aggiornato.");
            percent?.Report(100);
            return;
        }

        string backup = _configuration.EngineDirectory + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        await _runner.StopAllAsync().ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_configuration.EngineDirectory))
                CopyDirectory(_configuration.EngineDirectory, backup);
            await _installer.InstallAsync(percent, status, cancellationToken).ConfigureAwait(false);
            _log($"YARA_UPDATE version={manifest.Version} result=success");
            TryDeleteDirectory(backup);
        }
        catch
        {
            await _runner.StopAllAsync().ConfigureAwait(false);
            if (Directory.Exists(backup))
            {
                TryDeleteDirectory(_configuration.EngineDirectory);
                Directory.Move(backup, _configuration.EngineDirectory);
            }
            _log($"YARA_UPDATE version={manifest.Version} result=rollback");
            throw;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (string directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class YaraRuntime : IDisposable
{
    private readonly StreamWriter _logWriter;
    public YaraConfiguration Configuration { get; }
    public YaraProcessRunner ProcessRunner { get; }
    public YaraRuleManager Rules { get; }
    public YaraHealthCheckService Health { get; }
    public YaraScannerService Scanner { get; }
    public YaraQuarantineService Quarantine { get; }
    public YaraInstallationService Installation { get; }
    public YaraUpdateService Updates { get; }

    public YaraRuntime()
    {
        Configuration = YaraConfiguration.CreateDefault();
        Configuration.EnsureDirectories();
        string logPath = Path.Combine(Configuration.LogsDirectory, "yara-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        { AutoFlush = true };
        void Log(string message)
        {
            lock (_logWriter)
                _logWriter.WriteLine($"{DateTimeOffset.Now:O} {message}");
        }
        ProcessRunner = new YaraProcessRunner();
        Rules = new YaraRuleManager(Configuration, ProcessRunner, Log);
        Health = new YaraHealthCheckService(Configuration, ProcessRunner, Rules);
        Scanner = new YaraScannerService(
            Configuration, ProcessRunner, new YaraOutputParser(), Health.CheckAsync, Log);
        Quarantine = new YaraQuarantineService(Configuration, Log);
        YaraManifestVerifier verifier = new(Configuration);
        HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FFGuardian/10.0.1");
        Installation = new YaraInstallationService(
            Configuration, verifier, ProcessRunner, http, Health.CheckAsync, Log);
        Updates = new YaraUpdateService(
            Configuration, Installation, verifier, ProcessRunner, Health.CheckAsync, Log);
    }

    public async Task<bool> RunHarmlessSelfTestAsync(CancellationToken cancellationToken)
    {
        string testFile = Path.Combine(Path.GetTempPath(), "ffguardian-yara-test-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(testFile, "FFGUARDIAN_YARA_TEST_STRING", cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<YaraScanResult> results = await Scanner.ScanFileAsync(
                testFile, true, cancellationToken).ConfigureAwait(false);
            return results.Any(result => result.RuleName == "FFGuardian_Yara_Test");
        }
        finally
        {
            try { if (File.Exists(testFile)) File.Delete(testFile); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void Dispose()
    {
        ProcessRunner.Dispose();
        _logWriter.Dispose();
    }
}
