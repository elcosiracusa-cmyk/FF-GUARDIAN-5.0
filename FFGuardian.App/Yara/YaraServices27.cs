using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal sealed class YaraOutputParser
{
    private static readonly Regex Header = new(
        "^(?<rule>[A-Za-z_][A-Za-z0-9_]*)\\s*(?:\\[(?<tags>[^]]*)\\])?\\s+(?<path>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StringLine = new(
        "^0x(?<offset>[0-9A-Fa-f]+):(?<identifier>\\$[A-Za-z0-9_]+):\\s*(?<value>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<YaraScanResult> Parse(string output, TimeSpan duration,
        string engineVersion, string rulePackageVersion)
    {
        List<YaraScanResult> results = [];
        string? rule = null;
        string file = string.Empty;
        List<string> tags = [];
        List<YaraStringMatch> strings = [];
        void Flush()
        {
            if (string.IsNullOrWhiteSpace(rule)) return;
            Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
            results.Add(new YaraScanResult(rule, "default", tags.ToArray(), file, metadata,
                strings.ToArray(), Classify(tags), DateTime.UtcNow, duration,
                engineVersion, rulePackageVersion));
            rule = null; file = string.Empty; tags = []; strings = [];
        }
        foreach (string raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            Match stringMatch = StringLine.Match(line);
            if (stringMatch.Success && rule is not null)
            {
                strings.Add(new YaraStringMatch(stringMatch.Groups["identifier"].Value,
                    Convert.ToInt64(stringMatch.Groups["offset"].Value, 16),
                    stringMatch.Groups["value"].Value));
                continue;
            }
            Match header = Header.Match(line);
            if (!header.Success || line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase)) continue;
            Flush();
            rule = header.Groups["rule"].Value;
            file = header.Groups["path"].Value.Trim('"');
            tags = header.Groups["tags"].Value.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        Flush();
        return results;
    }

    private static YaraRiskLevel Classify(IEnumerable<string> tags)
    {
        string value = string.Join(' ', tags);
        if (value.Contains("critical", StringComparison.OrdinalIgnoreCase)) return YaraRiskLevel.Critical;
        if (value.Contains("high", StringComparison.OrdinalIgnoreCase)) return YaraRiskLevel.High;
        if (value.Contains("suspicious", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("medium", StringComparison.OrdinalIgnoreCase)) return YaraRiskLevel.Suspicious;
        return YaraRiskLevel.Informational;
    }
}

internal sealed class YaraRuleManager
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly Action<string> _log;
    public YaraRuleManager(YaraConfiguration configuration, YaraProcessRunner runner, Action<string> log)
    { _configuration = configuration; _runner = runner; _log = log; }

    public IReadOnlyList<string> GetEnabledRuleFiles() => Directory.Exists(_configuration.RulesDirectory)
        ? Directory.EnumerateFiles(_configuration.RulesDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".yar", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".yara", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(".disabled.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
        : Array.Empty<string>();

    public async Task<int> ValidateAndCompileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files = GetEnabledRuleFiles();
        if (files.Count == 0) throw new InvalidOperationException("Nessuna regola YARA disponibile.");
        EnsureUniqueRuleNames(files);
        if (!File.Exists(_configuration.YaracExecutable))
            throw new FileNotFoundException("yarac64.exe non trovato.");
        Directory.CreateDirectory(_configuration.CompiledDirectory);
        string temporary = _configuration.CompiledRulesPath + ".tmp";
        List<string> arguments = [.. files, temporary];
        YaraProcessResult result = await _runner.RunAsync(_configuration.YaracExecutable, arguments,
            _configuration.EngineDirectory, _configuration.ProcessTimeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || !File.Exists(temporary))
        {
            TryDelete(temporary);
            string error = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput : result.StandardError;
            throw new InvalidDataException("Regole YARA non valide: " + error.Trim());
        }
        File.Move(temporary, _configuration.CompiledRulesPath, overwrite: true);
        _log($"YARA_RULES_COMPILED count={files.Count}");
        return files.Count;
    }

    public async Task<YaraRuleMetadata> ImportAsync(string sourcePath, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(sourcePath);
        if (!extension.Equals(".yar", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yara", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sono accettati soltanto file .yar e .yara.");
        Directory.CreateDirectory(_configuration.CustomRulesDirectory);
        string destination = Path.Combine(_configuration.CustomRulesDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destination, overwrite: true);
        try
        {
            await ValidateAndCompileAsync(cancellationToken).ConfigureAwait(false);
            return ReadMetadata(destination);
        }
        catch { TryDelete(destination); throw; }
    }

    public YaraRuleMetadata ReadMetadata(string path)
    {
        string text = File.ReadAllText(path);
        string ruleName = Regex.Match(text, "\\brule\\s+([A-Za-z_][A-Za-z0-9_]*)").Groups[1].Value;
        string ReadValue(string key)
        {
            string pattern = "\\b" + Regex.Escape(key) + "\\s*=\\s*\"(?<v>[^\"]*)\"";
            return Regex.Match(text, pattern, RegexOptions.IgnoreCase).Groups["v"].Value;
        }
        DateTime? date = DateTime.TryParse(ReadValue("date"), out DateTime parsed) ? parsed : null;
        return new YaraRuleMetadata(ruleName, path, ReadValue("author"), ReadValue("description"),
            ReadValue("category"), ReadValue("severity"), date,
            !path.Contains(".disabled.", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureUniqueRuleNames(IEnumerable<string> files)
    {
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        Regex ruleRegex = new("\\brule\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        foreach (string file in files)
        foreach (Match match in ruleRegex.Matches(File.ReadAllText(file)))
        {
            string name = match.Groups[1].Value;
            if (names.TryGetValue(name, out string? existing))
                throw new InvalidDataException($"Regola duplicata '{name}' in '{existing}' e '{file}'.");
            names[name] = file;
        }
    }

    private static void TryDelete(string path)
    { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

internal sealed class YaraHealthCheckService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly YaraRuleManager _rules;
    public YaraHealthCheckService(YaraConfiguration configuration, YaraProcessRunner runner, YaraRuleManager rules)
    { _configuration = configuration; _runner = runner; _rules = rules; }

    public async Task<YaraHealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (!File.Exists(_configuration.YaraExecutable) || !File.Exists(_configuration.YaracExecutable))
            return new YaraHealthReport(YaraHealthState.NotInstalled, "--", 0, now,
                "yara64.exe o yarac64.exe non presenti.");
        try
        {
            YaraProcessResult versionResult = await _runner.RunAsync(_configuration.YaraExecutable,
                ["--version"], _configuration.EngineDirectory, TimeSpan.FromSeconds(20), cancellationToken)
                .ConfigureAwait(false);
            if (versionResult.ExitCode != 0 || versionResult.TimedOut)
                return new YaraHealthReport(YaraHealthState.EngineError, "--", 0, now,
                    "yara64.exe --version non riuscito.");
            string version = Regex.Match(versionResult.StandardOutput + versionResult.StandardError,
                "\\d+(?:\\.\\d+){1,3}").Value;
            if (_rules.GetEnabledRuleFiles().Count == 0)
                return new YaraHealthReport(YaraHealthState.RulesUnavailable, version, 0, now,
                    "Nessuna regola disponibile.");
            try
            {
                int count = await _rules.ValidateAndCompileAsync(cancellationToken).ConfigureAwait(false);
                return new YaraHealthReport(YaraHealthState.Active, version, count, now,
                    "Motore e regole verificati realmente.");
            }
            catch (InvalidDataException ex)
            {
                return new YaraHealthReport(YaraHealthState.RulesInvalid, version, 0, now, ex.Message);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { return new YaraHealthReport(YaraHealthState.EngineError, "--", 0, now, ex.Message); }
    }
}

internal sealed class YaraScannerService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraProcessRunner _runner;
    private readonly YaraOutputParser _parser;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _health;
    private readonly Action<string> _log;
    public YaraScannerService(YaraConfiguration configuration, YaraProcessRunner runner,
        YaraOutputParser parser, Func<CancellationToken, Task<YaraHealthReport>> health, Action<string> log)
    { _configuration = configuration; _runner = runner; _parser = parser; _health = health; _log = log; }

    public Task<IReadOnlyList<YaraScanResult>> ScanFileAsync(string path, bool includeStrings,
        CancellationToken cancellationToken, IProgress<string>? progress = null) =>
        ScanTargetAsync(path, false, includeStrings, cancellationToken, progress);
    public Task<IReadOnlyList<YaraScanResult>> ScanFolderAsync(string path, bool includeStrings,
        CancellationToken cancellationToken, IProgress<string>? progress = null) =>
        ScanTargetAsync(path, true, includeStrings, cancellationToken, progress);

    public async Task<IReadOnlyList<YaraScanResult>> ScanQuickAsync(CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        string[] roots = [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)];
        List<YaraScanResult> results = [];
        foreach (string root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        { cancellationToken.ThrowIfCancellationRequested(); results.AddRange(await ScanFolderAsync(root, false,
                cancellationToken, progress).ConfigureAwait(false)); }
        return results;
    }

    private async Task<IReadOnlyList<YaraScanResult>> ScanTargetAsync(string target, bool recursive,
        bool includeStrings, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        string fullPath = Path.GetFullPath(target);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new FileNotFoundException("Percorso non trovato.");
        if (_configuration.Exclusions.Any(x => fullPath.StartsWith(Path.GetFullPath(x), StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<YaraScanResult>();
        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > _configuration.MaximumFileSizeBytes)
            throw new InvalidOperationException("File oltre il limite configurato.");
        YaraHealthReport health = await _health(cancellationToken).ConfigureAwait(false);
        if (health.State != YaraHealthState.Active) throw new InvalidOperationException("YARA non attivo: " + health.Detail);
        List<string> arguments = [];
        if (recursive) { arguments.Add("--recursive"); arguments.Add("--no-follow-symlinks"); }
        if (includeStrings) arguments.Add("--print-strings");
        arguments.Add("--compiled-rules"); arguments.Add(_configuration.CompiledRulesPath); arguments.Add(fullPath);
        progress?.Report("YARA: " + fullPath);
        YaraProcessResult process = await _runner.RunAsync(_configuration.YaraExecutable, arguments,
            _configuration.EngineDirectory, _configuration.ProcessTimeout, cancellationToken, progress)
            .ConfigureAwait(false);
        if (process.TimedOut) throw new TimeoutException("Timeout scansione YARA.");
        if (process.ExitCode is not (0 or 1)) throw new InvalidOperationException("Errore YARA: " + process.StandardError);
        IReadOnlyList<YaraScanResult> results = _parser.Parse(process.StandardOutput, process.Duration,
            health.Version, File.GetLastWriteTimeUtc(_configuration.CompiledRulesPath).ToString("yyyyMMddHHmmss"));
        _log($"YARA_SCAN target=\"{fullPath}\" matches={results.Count}");
        return results;
    }
}

internal sealed class YaraQuarantineService
{
    private readonly YaraConfiguration _configuration;
    private readonly Action<string> _log;
    public YaraQuarantineService(YaraConfiguration configuration, Action<string> log)
    { _configuration = configuration; _log = log; }

    public async Task<YaraQuarantineRecord> QuarantineAsync(string filePath, string ruleName,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("File non trovato.");
        Directory.CreateDirectory(_configuration.QuarantineDirectory);
        string id = Guid.NewGuid().ToString("N");
        string stored = Path.Combine(_configuration.QuarantineDirectory, id + ".ffq");
        string hash;
        await using (FileStream input = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
        File.Move(fullPath, stored);
        File.SetAttributes(stored, FileAttributes.Hidden | FileAttributes.ReadOnly);
        YaraQuarantineRecord record = new(id, fullPath, stored, hash, DateTime.UtcNow, ruleName);
        await File.WriteAllTextAsync(stored + ".json", JsonSerializer.Serialize(record,
            new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        _log($"YARA_QUARANTINE id={id} sha256={hash}");
        return record;
    }

    public async Task RestoreAsync(string id, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed) throw new InvalidOperationException("Conferma richiesta.");
        string metadata = Path.Combine(_configuration.QuarantineDirectory, id + ".ffq.json");
        YaraQuarantineRecord? record = JsonSerializer.Deserialize<YaraQuarantineRecord>(
            await File.ReadAllTextAsync(metadata, cancellationToken).ConfigureAwait(false));
        if (record is null || !File.Exists(record.StoredPath)) throw new InvalidDataException("Quarantena non valida.");
        await using FileStream input = new(record.StoredPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
        if (!actual.Equals(record.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Integrità quarantena non valida.");
        Directory.CreateDirectory(Path.GetDirectoryName(record.OriginalPath)!);
        File.SetAttributes(record.StoredPath, FileAttributes.Normal);
        File.Move(record.StoredPath, record.OriginalPath, overwrite: false);
        File.Delete(metadata);
        _log($"YARA_RESTORE id={id}");
    }
}
