using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal enum YaraHealthState { Active, NotInstalled, RulesUnavailable, RulesInvalid, EngineError }
internal enum YaraRiskLevel { Informational, Suspicious, High, Critical }

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
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine", "Yara");
        return new YaraConfiguration(root, root, Path.Combine(root, "Rules"),
            Path.Combine(root, "Rules", "custom"), Path.Combine(root, "Compiled"),
            Path.Combine(root, "Updates"), Path.Combine(root, "Logs"),
            Path.Combine(root, "Quarantine"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "yara-engine-manifest.json"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "yara-engine-manifest.sig"),
            Environment.GetEnvironmentVariable("FFGUARDIAN_YARA_MANIFEST_PUBLIC_KEY"),
            256L * 1024L * 1024L, TimeSpan.FromMinutes(5), Array.Empty<string>());
    }

    public void EnsureDirectories()
    {
        foreach (string path in new[] { RootDirectory, RulesDirectory, CustomRulesDirectory,
                     CompiledDirectory, UpdatesDirectory, LogsDirectory, QuarantineDirectory })
            Directory.CreateDirectory(path);
    }
}

internal sealed record YaraEngineManifest(string Component, string Version, string Architecture,
    string DownloadUrl, string Sha256, string PackageFileName, DateTime PublishedUtc);
internal sealed record YaraRuleMetadata(string RuleName, string SourcePath, string Author,
    string Description, string Category, string Severity, DateTime? Date, bool Enabled);
internal sealed record YaraStringMatch(string Identifier, long Offset, string Value);
internal sealed record YaraScanResult(string RuleName, string Namespace, IReadOnlyList<string> Tags,
    string FilePath, IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<YaraStringMatch> StringMatches, YaraRiskLevel Risk, DateTime DetectedUtc,
    TimeSpan Duration, string EngineVersion, string RulePackageVersion);
internal sealed record YaraHealthReport(YaraHealthState State, string Version, int ValidRules,
    DateTime CheckedUtc, string Detail);
internal sealed record YaraProcessResult(int ExitCode, string StandardOutput, string StandardError,
    TimeSpan Duration, bool TimedOut);
internal sealed record YaraQuarantineRecord(string Id, string OriginalPath, string StoredPath,
    string Sha256, DateTime QuarantinedUtc, string RuleName);

internal sealed class YaraProcessRunner : IDisposable
{
    private readonly ConcurrentDictionary<int, Process> _active = new();
    private bool _disposed;

    public async Task<YaraProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!File.Exists(executable)) throw new FileNotFoundException("Eseguibile YARA non trovato.", executable);
        ProcessStartInfo start = new()
        {
            FileName = executable, WorkingDirectory = workingDirectory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = start, EnableRaisingEvents = true };
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!process.Start()) throw new InvalidOperationException("Avvio del processo YARA non riuscito.");
        _active[process.Id] = process;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        bool timedOut = false;
        try { await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { timedOut = true; TryKill(process); }
        catch (OperationCanceledException) { TryKill(process); throw; }
        finally { _active.TryRemove(process.Id, out _); }
        string stdout = await outputTask.ConfigureAwait(false);
        string stderr = await errorTask.ConfigureAwait(false);
        stopwatch.Stop();
        progress?.Report(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        return new YaraProcessResult(timedOut ? -1 : process.ExitCode, stdout, stderr,
            stopwatch.Elapsed, timedOut);
    }

    public Task StopAllAsync()
    {
        foreach (Process process in _active.Values) TryKill(process);
        return Task.CompletedTask;
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (Process process in _active.Values) TryKill(process);
        _active.Clear();
    }
}

internal sealed class YaraManifestVerifier
{
    private readonly YaraConfiguration _configuration;
    public YaraManifestVerifier(YaraConfiguration configuration) => _configuration = configuration;

    public YaraEngineManifest LoadAndVerify()
    {
        if (!File.Exists(_configuration.ManifestPath))
            throw new FileNotFoundException("Manifest YARA FFGuardian non trovato.");
        if (!File.Exists(_configuration.ManifestSignaturePath))
            throw new CryptographicException("Firma del manifest YARA non disponibile.");
        if (string.IsNullOrWhiteSpace(_configuration.ManifestPublicKeyPem))
            throw new CryptographicException("Chiave pubblica del manifest YARA non configurata.");
        byte[] data = File.ReadAllBytes(_configuration.ManifestPath);
        byte[] signature = Convert.FromBase64String(File.ReadAllText(_configuration.ManifestSignaturePath).Trim());
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(_configuration.ManifestPublicKeyPem);
        if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Firma RSA-PSS del manifest YARA non valida.");
        YaraEngineManifest? manifest = JsonSerializer.Deserialize<YaraEngineManifest>(data,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest is null) throw new InvalidDataException("Manifest YARA non leggibile.");
        Validate(manifest);
        return manifest;
    }

    private static void Validate(YaraEngineManifest manifest)
    {
        if (manifest.Component != "yara-engine") throw new InvalidDataException("Componente manifest non valido.");
        if (!manifest.Architecture.Equals("x64", StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException("Pacchetto YARA non x64.");
        if (!Version.TryParse(manifest.Version, out _)) throw new InvalidDataException("Versione non valida.");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("URL YARA non ufficiale o non HTTPS.");
        if (!Regex.IsMatch(manifest.Sha256, "^[A-Fa-f0-9]{64}$"))
            throw new InvalidDataException("SHA-256 YARA non valido.");
        if (Path.GetFileName(manifest.PackageFileName) != manifest.PackageFileName)
            throw new InvalidDataException("Nome pacchetto non valido.");
    }
}
