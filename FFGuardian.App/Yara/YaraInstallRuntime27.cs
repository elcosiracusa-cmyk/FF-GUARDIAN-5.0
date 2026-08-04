using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FFGuardian;

internal sealed class YaraInstallationService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraManifestVerifier _manifestVerifier;
    private readonly YaraProcessRunner _runner;
    private readonly HttpClient _http;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _health;
    private readonly Action<string> _log;

    public YaraInstallationService(YaraConfiguration configuration, YaraManifestVerifier manifestVerifier,
        YaraProcessRunner runner, HttpClient http, Func<CancellationToken, Task<YaraHealthReport>> health,
        Action<string> log)
    { _configuration = configuration; _manifestVerifier = manifestVerifier; _runner = runner;
      _http = http; _health = health; _log = log; }

    public async Task InstallAsync(IProgress<int>? percent, IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        if (!Environment.Is64BitOperatingSystem) throw new PlatformNotSupportedException("Richiesto Windows x64.");
        YaraEngineManifest manifest = _manifestVerifier.LoadAndVerify();
        _configuration.EnsureDirectories();
        string session = Path.Combine(_configuration.UpdatesDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        string archive = Path.Combine(session, manifest.PackageFileName);
        string staging = Path.Combine(session, "extracted");
        try
        {
            status?.Report("Download pacchetto YARA ufficiale");
            using HttpResponseMessage response = await _http.GetAsync(manifest.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream output = new(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920]; long written = 0; int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                if (total > 0) percent?.Report((int)Math.Clamp(written * 70 / total, 0, 70));
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Close();
            status?.Report("Verifica SHA-256");
            string actual;
            await using (FileStream package = File.OpenRead(archive))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(package, cancellationToken).ConfigureAwait(false));
            if (!actual.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("SHA-256 pacchetto YARA non corrispondente.");
            percent?.Report(75);
            status?.Report("Estrazione controllata");
            ExtractSelected(archive, staging);
            percent?.Report(85);
            await InstallFromStagingAsync(staging, cancellationToken).ConfigureAwait(false);
            percent?.Report(95);
            YaraHealthReport health = await _health(cancellationToken).ConfigureAwait(false);
            if (health.State != YaraHealthState.Active)
                throw new InvalidOperationException("Test finale YARA fallito: " + health.Detail);
            percent?.Report(100);
            _log($"YARA_INSTALL version={manifest.Version} result=success");
        }
        finally { TryDeleteDirectory(session); }
    }

    internal async Task InstallFromStagingAsync(string staging, CancellationToken cancellationToken)
    {
        string yara = FindExecutable(staging, "yara");
        string yarac = FindExecutable(staging, "yarac");
        Directory.CreateDirectory(_configuration.EngineDirectory);
        File.Copy(yara, _configuration.YaraExecutable, overwrite: true);
        File.Copy(yarac, _configuration.YaracExecutable, overwrite: true);
        foreach (string dll in Directory.EnumerateFiles(staging, "*.dll", SearchOption.AllDirectories))
            File.Copy(dll, Path.Combine(_configuration.EngineDirectory, Path.GetFileName(dll)), overwrite: true);
        YaraProcessResult result = await _runner.RunAsync(_configuration.YaraExecutable, ["--version"],
            _configuration.EngineDirectory, TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut) throw new InvalidOperationException("Nuovo motore YARA non avviabile.");
    }

    private static string FindExecutable(string root, string baseName)
    {
        string[] names = [$"{baseName}64.exe", $"{baseName}.exe"];
        foreach (string name in names)
        {
            string? found = Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
            if (found is not null) return found;
        }
        throw new InvalidDataException($"{baseName}64.exe assente nel pacchetto.");
    }

    private static void ExtractSelected(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        using ZipArchive zip = ZipFile.OpenRead(archive);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = Path.GetFileName(entry.FullName);
            if (string.IsNullOrWhiteSpace(name)) continue;
            bool allowed = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase);
            if (!allowed) continue;
            string target = Path.GetFullPath(Path.Combine(destination, name));
            string safeRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Percorso ZIP non sicuro.");
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    internal static void TryDeleteDirectory(string path)
    { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

internal sealed class YaraUpdateService
{
    private readonly YaraConfiguration _configuration;
    private readonly YaraInstallationService _installer;
    private readonly YaraManifestVerifier _manifestVerifier;
    private readonly YaraProcessRunner _runner;
    private readonly Func<CancellationToken, Task<YaraHealthReport>> _health;
    private readonly Action<string> _log;
    public YaraUpdateService(YaraConfiguration configuration, YaraInstallationService installer,
        YaraManifestVerifier manifestVerifier, YaraProcessRunner runner,
        Func<CancellationToken, Task<YaraHealthReport>> health, Action<string> log)
    { _configuration = configuration; _installer = installer; _manifestVerifier = manifestVerifier;
      _runner = runner; _health = health; _log = log; }

    public async Task UpdateAsync(IProgress<int>? percent, IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        YaraEngineManifest manifest = _manifestVerifier.LoadAndVerify();
        YaraHealthReport current = await _health(cancellationToken).ConfigureAwait(false);
        if (current.State == YaraHealthState.Active && Version.TryParse(current.Version, out Version? installed) &&
            Version.TryParse(manifest.Version, out Version? available) && installed >= available)
        { status?.Report("Motore YARA già aggiornato."); percent?.Report(100); return; }
        string backup = _configuration.EngineDirectory + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        await _runner.StopAllAsync().ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_configuration.EngineDirectory)) CopyDirectory(_configuration.EngineDirectory, backup);
            await _installer.InstallAsync(percent, status, cancellationToken).ConfigureAwait(false);
            _log($"YARA_UPDATE version={manifest.Version} result=success");
            YaraInstallationService.TryDeleteDirectory(backup);
        }
        catch
        {
            await _runner.StopAllAsync().ConfigureAwait(false);
            if (Directory.Exists(backup))
            {
                YaraInstallationService.TryDeleteDirectory(_configuration.EngineDirectory);
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
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (string directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}

internal sealed class YaraRuntime : IDisposable
{
    private readonly StreamWriter _logWriter;
    private readonly HttpClient _http;
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
        Configuration = YaraConfiguration.CreateDefault(); Configuration.EnsureDirectories(); SeedBundledRules();
        string logPath = Path.Combine(Configuration.LogsDirectory, "yara-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            { AutoFlush = true };
        void Log(string message) { lock (_logWriter) _logWriter.WriteLine($"{DateTimeOffset.Now:O} {message}"); }
        ProcessRunner = new YaraProcessRunner();
        Rules = new YaraRuleManager(Configuration, ProcessRunner, Log);
        Health = new YaraHealthCheckService(Configuration, ProcessRunner, Rules);
        Scanner = new YaraScannerService(Configuration, ProcessRunner, new YaraOutputParser(), Health.CheckAsync, Log);
        Quarantine = new YaraQuarantineService(Configuration, Log);
        YaraManifestVerifier verifier = new(Configuration);
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FFGuardian/10.0.1");
        Installation = new YaraInstallationService(Configuration, verifier, ProcessRunner, _http, Health.CheckAsync, Log);
        Updates = new YaraUpdateService(Configuration, Installation, verifier, ProcessRunner, Health.CheckAsync, Log);
    }

    private void SeedBundledRules()
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "Rules", "Yara");
        if (!Directory.Exists(bundled)) return;
        foreach (string source in Directory.EnumerateFiles(bundled, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => path.EndsWith(".yar", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".yara", StringComparison.OrdinalIgnoreCase)))
        {
            string destination = Path.Combine(Configuration.RulesDirectory, Path.GetFileName(source));
            if (!File.Exists(destination)) File.Copy(source, destination);
        }
    }

    public async Task<bool> RunHarmlessSelfTestAsync(CancellationToken cancellationToken)
    {
        string test = Path.Combine(Path.GetTempPath(), "ffguardian-yara-test-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(test, "FFGUARDIAN_YARA_TEST_STRING", cancellationToken).ConfigureAwait(false);
            IReadOnlyList<YaraScanResult> results = await Scanner.ScanFileAsync(test, true, cancellationToken).ConfigureAwait(false);
            return results.Any(result => result.RuleName == "FFGuardian_Yara_Test");
        }
        finally { try { if (File.Exists(test)) File.Delete(test); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    public void Dispose() { ProcessRunner.Dispose(); _http.Dispose(); _logWriter.Dispose(); }
}
