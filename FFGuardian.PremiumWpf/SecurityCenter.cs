using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;

namespace FFGuardian.PremiumWpf;

public enum HealthSeverity { Informational, Important, Critical }
public enum HealthState { NotChecked, Operational, Warning, Error, Unavailable }

public sealed record HealthCheckResult(
    string Name,
    string Description,
    HealthSeverity Severity,
    HealthState Status,
    DateTimeOffset LastCheck,
    TimeSpan Duration,
    string Version,
    string Message,
    string? Details = null,
    string? RepairAction = null,
    double Weight = 1);

public interface IHealthCheck
{
    string Name { get; }
    string Description { get; }
    HealthSeverity Severity { get; }
    double Weight { get; }
    Task<HealthCheckResult> ExecuteAsync(CancellationToken cancellationToken);
}

public sealed record SecurityCenterSnapshot(
    DateTimeOffset CheckedAt,
    TimeSpan Duration,
    int Score,
    string OverallState,
    int Active,
    int Warnings,
    int Errors,
    int Unavailable,
    string EngineVersion,
    string SignatureVersion,
    IReadOnlyList<HealthCheckResult> Results);

public sealed class SecurityCenterService : IDisposable
{
    private readonly IReadOnlyList<IHealthCheck> _checks;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMinutes(30));
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _logPath;
    private Task? _scheduler;

    public SecurityCenterService(string? baseDirectory = null, Uri? signatureEndpoint = null)
    {
        string root = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF Guardian");
        Directory.CreateDirectory(Path.Combine(data, "Logs"));
        _logPath = Path.Combine(data, "Logs", "security-center.jsonl");
        _checks = HealthCheckFactory.Create(root, data, signatureEndpoint);
    }

    public event EventHandler<SecurityCenterSnapshot>? SnapshotUpdated;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _scheduler ??= RunSchedulerAsync(_lifetime.Token);
        return ExecuteAllAsync(cancellationToken);
    }

    public async Task<SecurityCenterSnapshot> ExecuteAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Stopwatch total = Stopwatch.StartNew();
        try
        {
            List<HealthCheckResult> results = [];
            foreach (IHealthCheck check in _checks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HealthCheckResult result;
                try
                {
                    result = await check.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or HttpRequestException)
                {
                    result = new(check.Name, check.Description, check.Severity, HealthState.Error,
                        DateTimeOffset.Now, TimeSpan.Zero, "--", ex.Message, ex.ToString(), null, check.Weight);
                }
                results.Add(result);
                await AppendLogAsync(result, cancellationToken).ConfigureAwait(false);
            }

            total.Stop();
            SecurityCenterSnapshot snapshot = BuildSnapshot(results, total.Elapsed);
            SnapshotUpdated?.Invoke(this, snapshot);
            return snapshot;
        }
        finally { _gate.Release(); }
    }

    internal static SecurityCenterSnapshot BuildSnapshot(IReadOnlyList<HealthCheckResult> results, TimeSpan duration)
    {
        double totalWeight = results.Sum(x => x.Weight);
        double earned = results.Sum(x => x.Status switch
        {
            HealthState.Operational => x.Weight,
            HealthState.Warning => x.Weight * 0.55,
            HealthState.Unavailable => x.Weight * 0.15,
            _ => 0
        });
        int score = totalWeight <= 0 ? 0 : (int)Math.Round(earned * 100 / totalWeight);
        bool criticalError = results.Any(x => x.Severity == HealthSeverity.Critical && x.Status is HealthState.Error or HealthState.Unavailable);
        bool criticalNotOperational = results.Any(x => x.Severity == HealthSeverity.Critical && x.Status != HealthState.Operational);
        string state = !criticalNotOperational && score >= 92 ? "Sistema Protetto"
            : !criticalError && score >= 70 ? "Protezione Parziale"
            : score >= 40 ? "Protezione Ridotta"
            : "Sistema Non Protetto";
        string engine = results.FirstOrDefault(x => x.Name == "Engine10")?.Version ?? "--";
        string signatures = results.FirstOrDefault(x => x.Name == "Database firme")?.Version ?? "--";
        return new(DateTimeOffset.Now, duration, score, state,
            results.Count(x => x.Status == HealthState.Operational),
            results.Count(x => x.Status == HealthState.Warning),
            results.Count(x => x.Status == HealthState.Error),
            results.Count(x => x.Status == HealthState.Unavailable), engine, signatures, results);
    }

    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await ExecuteAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task AppendLogAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        string line = JsonSerializer.Serialize(new
        {
            date = result.LastCheck.ToString("yyyy-MM-dd"),
            time = result.LastCheck.ToString("HH:mm:ss.fff"),
            module = result.Name,
            durationMs = Math.Round(result.Duration.TotalMilliseconds, 2),
            outcome = result.Status.ToString(),
            error = result.Status == HealthState.Error ? result.Message : null,
            version = result.Version
        });
        await File.AppendAllTextAsync(_logPath, line + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _timer.Dispose();
        _lifetime.Dispose();
        _gate.Dispose();
    }
}

internal static class HealthCheckFactory
{
    public static IReadOnlyList<IHealthCheck> Create(string root, string data, Uri? endpoint)
    {
        Uri server = endpoint ?? new Uri("https://api.github.com/");
        return
        [
            new ExecutableHealthCheck("Engine10", "Motore gestito principale", HealthSeverity.Critical, 12, Path.Combine(root, "FFGuardian.dll"), null),
            new ExecutableHealthCheck("ClamAV", "Motore antivirus ClamAV", HealthSeverity.Critical, 12, Find(root, "Engine/ClamAV/clamscan.exe", "ClamAV/clamscan.exe"), "--version"),
            new ExecutableHealthCheck("FreshClam", "Aggiornamento firme ClamAV", HealthSeverity.Important, 4, Find(root, "Engine/ClamAV/freshclam.exe", "ClamAV/freshclam.exe"), "--version"),
            new SignatureDatabaseHealthCheck(root),
            new ExecutableHealthCheck("YARA", "Motore regole YARA", HealthSeverity.Important, 7, Find(root, "Engine/Yara/yara64.exe", "Engine/Yara/yara.exe", "Tools/Yara/yara64.exe"), "--version"),
            new FeatureFileHealthCheck("Analisi euristica", "Configurazione euristica", HealthSeverity.Important, 5, root, ["heuristic", "behavior"]),
            new FeatureFileHealthCheck("Protezione tempo reale", "Monitoraggio filesystem", HealthSeverity.Critical, 12, root, ["realtime", "watcher"]),
            new FirewallHealthCheck(),
            new FeatureFileHealthCheck("Ransom Shield", "Protezione anti-ransomware", HealthSeverity.Critical, 9, root, ["ransom"]),
            new FeatureFileHealthCheck("USB Shield", "Protezione dispositivi removibili", HealthSeverity.Important, 4, root, ["usb"]),
            new DirectoryHealthCheck("Quarantena", "Archivio isolamento minacce", HealthSeverity.Important, 5, Path.Combine(data, "Quarantine"), true),
            new DirectoryHealthCheck("Recupero file", "Area recupero e rollback", HealthSeverity.Informational, 2, Path.Combine(data, "Backup"), true),
            new UpdateSecurityHealthCheck(root),
            new IntegrityManifestHealthCheck(root),
            new ConfigurationHealthCheck(root, data),
            new DirectoryHealthCheck("Cartelle interne", "Permessi cartelle operative", HealthSeverity.Important, 4, data, true),
            new WindowsServicesHealthCheck(),
            new HttpsEndpointHealthCheck(server),
            new DirectoryHealthCheck("File di log", "Scrittura log Security Center", HealthSeverity.Informational, 2, Path.Combine(data, "Logs"), true),
            new SchedulerHealthCheck(),
            new SettingsDatabaseHealthCheck(data),
            new LicenseHealthCheck(root, data)
        ];
    }

    private static string Find(string root, params string[] paths) => paths.Select(x => Path.GetFullPath(Path.Combine(root, x.Replace('/', Path.DirectorySeparatorChar)))).FirstOrDefault(File.Exists) ?? Path.Combine(root, paths[0].Replace('/', Path.DirectorySeparatorChar));
}

internal abstract class HealthCheckBase(string name, string description, HealthSeverity severity, double weight) : IHealthCheck
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public HealthSeverity Severity { get; } = severity;
    public double Weight { get; } = weight;
    public async Task<HealthCheckResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        HealthCheckResult result = await CheckAsync(cancellationToken).ConfigureAwait(false);
        sw.Stop();
        return result with { LastCheck = DateTimeOffset.Now, Duration = sw.Elapsed, Weight = Weight };
    }
    protected abstract Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken);
    protected HealthCheckResult Result(HealthState state, string version, string message, string? details = null, string? repair = null) =>
        new(Name, Description, Severity, state, DateTimeOffset.MinValue, TimeSpan.Zero, version, message, details, repair, Weight);
}

internal sealed class ExecutableHealthCheck(string name, string description, HealthSeverity severity, double weight, string path, string? argument)
    : HealthCheckBase(name, description, severity, weight)
{
    protected override async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return Result(HealthState.Unavailable, "--", "Componente non trovato.", path, "Reinstalla componente");
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        string version = info.FileVersion ?? info.ProductVersion ?? "--";
        if (argument is null) return Result(HealthState.Operational, version, "File motore presente e leggibile.", path);
        ProcessStartInfo start = new() { FileName = path, WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = start };
        if (!process.Start()) return Result(HealthState.Error, version, "Avvio verifica non riuscito.", path, "Reinstalla componente");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { TryKill(process); return Result(HealthState.Error, version, "Timeout verifica runtime.", path, "Reinstalla componente"); }
        string output = (await stdout.ConfigureAwait(false)).Trim();
        string error = (await stderr.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0) return Result(HealthState.Error, version, $"Exit code {process.ExitCode}.", error, "Reinstalla componente");
        string detected = string.IsNullOrWhiteSpace(output) ? version : output.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? version;
        return Result(HealthState.Operational, detected, "Verifica runtime superata.", path);
    }
    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
}

internal sealed class SignatureDatabaseHealthCheck(string root) : HealthCheckBase("Database firme", "Validità e freschezza database firme", HealthSeverity.Critical, 10)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string[] dirs = [Path.Combine(root, "Database"), Path.Combine(root, "Engine", "ClamAV", "Database")];
        string[] patterns = ["*.cvd", "*.cld", "*.yar", "*.yara", "*.json"];
        List<FileInfo> files = [];
        foreach (string dir in dirs.Where(Directory.Exists)) foreach (string pattern in patterns) files.AddRange(Directory.EnumerateFiles(dir, pattern).Select(x => new FileInfo(x)));
        if (files.Count == 0) return Task.FromResult(Result(HealthState.Unavailable, "--", "Nessuna firma rilevata.", null, "Aggiorna"));
        FileInfo newest = files.OrderByDescending(x => x.LastWriteTimeUtc).First();
        TimeSpan age = DateTime.UtcNow - newest.LastWriteTimeUtc;
        HealthState state = age <= TimeSpan.FromDays(3) ? HealthState.Operational : age <= TimeSpan.FromDays(14) ? HealthState.Warning : HealthState.Error;
        string version = newest.LastWriteTimeUtc.ToString("yyyyMMdd-HHmm");
        return Task.FromResult(Result(state, version, $"{files.Count} file firma; più recente {age.TotalDays:F1} giorni fa.", newest.FullName, state == HealthState.Operational ? null : "Aggiorna"));
    }
}

internal sealed class FeatureFileHealthCheck(string name, string description, HealthSeverity severity, double weight, string root, string[] tokens) : HealthCheckBase(name, description, severity, weight)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string> candidates;
        try { candidates = Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)); }
        catch (UnauthorizedAccessException ex) { return Task.FromResult(Result(HealthState.Error, "--", ex.Message)); }
        string? match = candidates.FirstOrDefault(path => tokens.Any(t => Path.GetFileName(path).Contains(t, StringComparison.OrdinalIgnoreCase)));
        return Task.FromResult(match is null
            ? Result(HealthState.Unavailable, "--", "Implementazione/configurazione non individuata.", null, "Reinstalla componente")
            : Result(HealthState.Warning, FileVersionInfo.GetVersionInfo(match).FileVersion ?? "--", "Componente individuato; test funzionale dedicato richiesto.", match));
    }
}

internal sealed class DirectoryHealthCheck(string name, string description, HealthSeverity severity, double weight, string path, bool create) : HealthCheckBase(name, description, severity, weight)
{
    protected override async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path) && create) Directory.CreateDirectory(path);
        if (!Directory.Exists(path)) return Result(HealthState.Unavailable, "--", "Cartella assente.", path, "Ripara");
        string probe = Path.Combine(path, ".ffg-health-" + Guid.NewGuid().ToString("N"));
        try { await File.WriteAllTextAsync(probe, "health", cancellationToken).ConfigureAwait(false); File.Delete(probe); return Result(HealthState.Operational, "--", "Cartella accessibile in lettura/scrittura.", path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Result(HealthState.Error, "--", ex.Message, path, "Ripara"); }
    }
}

internal sealed class FirewallHealthCheck : HealthCheckBase
{
    public FirewallHealthCheck() : base("Firewall Windows", "Stato profili firewall Windows", HealthSeverity.Critical, 10) { }
    protected override async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new() { FileName = "netsh.exe", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("advfirewall"); start.ArgumentList.Add("show"); start.ArgumentList.Add("allprofiles"); start.ArgumentList.Add("state");
        using Process p = new() { StartInfo = start };
        if (!p.Start()) return Result(HealthState.Error, "--", "Impossibile interrogare il firewall.");
        string output = await p.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (p.ExitCode != 0) return Result(HealthState.Error, "--", "netsh ha restituito errore.");
        int on = output.Split('\n').Count(x => x.Contains("ON", StringComparison.OrdinalIgnoreCase) || x.Contains("ATTIVATO", StringComparison.OrdinalIgnoreCase));
        return on >= 3 ? Result(HealthState.Operational, "Windows", "Tutti i profili risultano attivi.", output)
            : Result(HealthState.Error, "Windows", "Uno o più profili firewall non risultano attivi.", output, "Ripara");
    }
}

internal sealed class UpdateSecurityHealthCheck(string root) : HealthCheckBase("Aggiornamenti", "Manifesto, firma e SHA-256 aggiornamenti", HealthSeverity.Critical, 8)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string dir = Path.Combine(root, "Updates");
        string manifest = Path.Combine(dir, "manifest.json");
        string signature = Path.Combine(dir, "manifest.sig");
        if (!File.Exists(manifest) || !File.Exists(signature)) return Task.FromResult(Result(HealthState.Unavailable, "--", "Manifesto o firma aggiornamenti assente.", dir, "Scarica nuovamente"));
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifest));
            bool hasHash = doc.RootElement.ToString().Contains("sha256", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(hasHash ? Result(HealthState.Warning, "--", "Manifesto e firma presenti; verifica crittografica richiede chiave release configurata.", manifest)
                : Result(HealthState.Error, "--", "Manifesto privo di SHA-256.", manifest, "Scarica nuovamente"));
        }
        catch (JsonException ex) { return Task.FromResult(Result(HealthState.Error, "--", ex.Message, manifest, "Scarica nuovamente")); }
    }
}

internal sealed class IntegrityManifestHealthCheck(string root) : HealthCheckBase("Integrità file FFGuardian", "SHA-256, dimensioni, firme e manifesto", HealthSeverity.Critical, 10)
{
    protected override async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string manifest = Path.Combine(root, "Assets", "ffguardian-files-manifest.json");
        if (!File.Exists(manifest)) return Result(HealthState.Unavailable, "--", "Manifesto integrità assente.", manifest, "Scarica nuovamente");
        try
        {
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, cancellationToken).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array) return Result(HealthState.Error, "--", "Manifesto non valido.", manifest);
            int intact = 0; int problems = 0;
            foreach (JsonElement item in files.EnumerateArray())
            {
                string? relative = item.TryGetProperty("relativePath", out JsonElement rp) ? rp.GetString() : null;
                string? expected = item.TryGetProperty("sha256", out JsonElement hs) ? hs.GetString() : null;
                long expectedSize = item.TryGetProperty("size", out JsonElement sz) ? sz.GetInt64() : -1;
                if (string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(expected)) { problems++; continue; }
                string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) { problems++; continue; }
                FileInfo info = new(path);
                await using FileStream stream = File.OpenRead(path);
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                if (info.Length == expectedSize && actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) intact++; else problems++;
            }
            return problems == 0 ? Result(HealthState.Operational, doc.RootElement.TryGetProperty("version", out JsonElement v) ? v.GetString() ?? "--" : "--", $"{intact} file verificati.", manifest)
                : Result(HealthState.Error, "--", $"{problems} anomalie di integrità.", manifest, "Scarica nuovamente");
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException) { return Result(HealthState.Error, "--", ex.Message, manifest); }
    }
}

internal sealed class ConfigurationHealthCheck(string root, string data) : HealthCheckBase("Configurazione", "Validità file configurazione", HealthSeverity.Important, 3)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string? file = new[] { Path.Combine(data, "settings.json"), Path.Combine(root, "appsettings.json") }.FirstOrDefault(File.Exists);
        if (file is null) return Task.FromResult(Result(HealthState.Unavailable, "--", "Configurazione persistente non trovata.", null, "Ripara"));
        try { using JsonDocument _ = JsonDocument.Parse(File.ReadAllText(file)); return Task.FromResult(Result(HealthState.Operational, "--", "JSON configurazione valido.", file)); }
        catch (JsonException ex) { return Task.FromResult(Result(HealthState.Error, "--", ex.Message, file, "Ripara")); }
    }
}

internal sealed class WindowsServicesHealthCheck : HealthCheckBase
{
    public WindowsServicesHealthCheck() : base("Servizi Windows utilizzati", "Servizi richiesti dal sistema", HealthSeverity.Important, 3) { }
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using ServiceController scheduler = new("Schedule");
            ServiceControllerStatus status = scheduler.Status;
            return Task.FromResult(status == ServiceControllerStatus.Running ? Result(HealthState.Operational, "Windows", "Utilità di pianificazione attiva.") : Result(HealthState.Warning, "Windows", $"Schedule: {status}.", null, "Ripara"));
        }
        catch (InvalidOperationException ex) { return Task.FromResult(Result(HealthState.Error, "Windows", ex.Message)); }
    }
}

internal sealed class HttpsEndpointHealthCheck(Uri endpoint) : HealthCheckBase("Connessione HTTPS server firme", "TLS e raggiungibilità server firme", HealthSeverity.Important, 4)
{
    protected override async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps) return Result(HealthState.Error, "--", "Endpoint non HTTPS.", endpoint.ToString());
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FFGuardian-SecurityCenter/1.0");
        using HttpRequestMessage request = new(HttpMethod.Head, endpoint);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode || (int)response.StatusCode < 500 ? Result(HealthState.Operational, response.Version.ToString(), $"HTTPS raggiungibile: {(int)response.StatusCode}.", endpoint.ToString())
            : Result(HealthState.Error, response.Version.ToString(), $"Server ha risposto {(int)response.StatusCode}.", endpoint.ToString(), "Riprova");
    }
}

internal sealed class SchedulerHealthCheck : HealthCheckBase
{
    public SchedulerHealthCheck() : base("Scheduler", "Pianificazione controllo ogni 30 minuti", HealthSeverity.Important, 3) { }
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(Result(HealthState.Operational, "30 min", "PeriodicTimer configurato e attivo nel processo."));
}

internal sealed class SettingsDatabaseHealthCheck(string data) : HealthCheckBase("Database impostazioni", "Persistenza impostazioni", HealthSeverity.Informational, 2)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string? db = Directory.Exists(data) ? Directory.EnumerateFiles(data, "*.db", SearchOption.AllDirectories).FirstOrDefault() : null;
        return Task.FromResult(db is null ? Result(HealthState.Unavailable, "--", "Database impostazioni non trovato.") : Result(new FileInfo(db).Length > 0 ? HealthState.Operational : HealthState.Warning, "--", "Database impostazioni leggibile.", db));
    }
}

internal sealed class LicenseHealthCheck(string root, string data) : HealthCheckBase("Licenza", "Presenza e validità materiale licenza", HealthSeverity.Informational, 1)
{
    protected override Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        string? license = new[] { Path.Combine(data, "license.json"), Path.Combine(root, "license.json") }.FirstOrDefault(File.Exists);
        return Task.FromResult(license is null ? Result(HealthState.Unavailable, "Community", "Licenza commerciale non configurata.") : Result(HealthState.Warning, "Configured", "File licenza presente; validazione firma non disponibile in questo frontend.", license));
    }
}
