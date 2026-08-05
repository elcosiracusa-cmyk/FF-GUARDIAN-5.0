using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class QuarantineService(IOptions<SecurityCoreOptions> options, IFileHashService hashes, ISecurityEventLogger logger) : IQuarantineService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root = Path.Combine(options.Value.DataDirectory, "Quarantine");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(path);
        if (!File.Exists(source)) return new(false, null, "File sorgente assente.");
        Directory.CreateDirectory(_root);
        Guid id = Guid.NewGuid();
        string stored = Path.Combine(_root, id.ToString("N") + ".qdat");
        string metadata = Path.Combine(_root, id.ToString("N") + ".json");
        string hash = await hashes.ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
        FileInfo info = new(source);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (FileStream output = new(stored, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            string copiedHash = await hashes.ComputeSha256Async(stored, cancellationToken).ConfigureAwait(false);
            if (!hash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(stored);
                return new(false, null, "Verifica copia quarantena fallita.");
            }
            QuarantineEntry entry = new(id, info.Name, source, stored, hash, info.Length, engine, detection, DateTimeOffset.UtcNow, risk);
            await File.WriteAllTextAsync(metadata, JsonSerializer.Serialize(entry, JsonOptions), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            File.Delete(source);
            await logger.LogAsync("Quarantine", "Stored", id.ToString(), cancellationToken).ConfigureAwait(false);
            return new(true, entry, "File isolato.");
        }
        catch
        {
            TryDelete(stored);
            TryDelete(metadata);
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken)
    {
        QuarantineEntry? entry = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null || !File.Exists(entry.StoredPath)) return new(false, entry, "Elemento non trovato.");
        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination) && !overwrite) return new(false, entry, "Destinazione già esistente.");
        string storedHash = await hashes.ComputeSha256Async(entry.StoredPath, cancellationToken).ConfigureAwait(false);
        if (!entry.Sha256.Equals(storedHash, StringComparison.OrdinalIgnoreCase)) return new(false, entry, "Hash quarantena non valido.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(entry.StoredPath, destination, overwrite);
        string restoredHash = await hashes.ComputeSha256Async(destination, cancellationToken).ConfigureAwait(false);
        if (!entry.Sha256.Equals(restoredHash, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(destination);
            return new(false, entry, "Verifica ripristino fallita.");
        }
        await logger.LogAsync("Quarantine", "Restored", id.ToString(), cancellationToken).ConfigureAwait(false);
        return new(true, entry, "File ripristinato; la conferma utente deve essere acquisita dalla UI.");
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        QuarantineEntry? entry = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null) return false;
        TryDelete(entry.StoredPath);
        TryDelete(Path.Combine(_root, id.ToString("N") + ".json"));
        await logger.LogAsync("Quarantine", "Deleted", id.ToString(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root)) return [];
        List<QuarantineEntry> entries = [];
        foreach (string file in Directory.EnumerateFiles(_root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                QuarantineEntry? entry = JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), JsonOptions);
                if (entry is not null) entries.Add(entry);
            }
            catch (JsonException) { }
        }
        return entries.OrderByDescending(entry => entry.CreatedAt).ToArray();
    }

    private async Task<QuarantineEntry?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        string metadata = Path.Combine(_root, id.ToString("N") + ".json");
        if (!File.Exists(metadata)) return null;
        return JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(metadata, cancellationToken).ConfigureAwait(false), JsonOptions);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => _gate.Dispose();
}

public sealed class ScanService(IYaraService yara, IClamAvService clam, IPathExclusionService exclusions, IQuarantineService quarantine, ISecurityEventLogger logger) : IScanService
{
    public async Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int scanned = 0;
        int skipped = 0;
        int failed = 0;
        List<ScanDetection> detections = [];
        List<string> errors = [];
        HashSet<string> engines = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string file in EnumerateFiles(request))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (exclusions.ShouldExclude(file)) { skipped++; continue; }
                try
                {
                    IReadOnlyList<YaraMatch> yaraMatches = await yara.ScanFileAsync(file, cancellationToken).ConfigureAwait(false);
                    engines.Add("YARA");
                    detections.AddRange(yaraMatches.Select(match => new ScanDetection("YARA", match.Rule, file, match.RawOutput)));
                    IReadOnlyList<ClamAvDetection> clamMatches = await clam.ScanFileAsync(file, cancellationToken).ConfigureAwait(false);
                    engines.Add("ClamAV");
                    detections.AddRange(clamMatches.Select(match => new ScanDetection("ClamAV", match.Signature, file, match.RawOutput)));
                    scanned++;
                    progress?.Report(new(scanned, skipped, file));
                    ScanDetection? first = detections.FirstOrDefault(item => item.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase));
                    if (request.QuarantineDetections && first is not null)
                        await quarantine.QuarantineAsync(file, first.Engine, first.Name, "High", cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    failed++;
                    errors.Add($"{file}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections, engines.ToArray(), true, errors);
        }
        await logger.LogAsync("Scan", "Completed", $"scanned={scanned}; detections={detections.Count}", cancellationToken).ConfigureAwait(false);
        return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections, engines.ToArray(), false, errors);
    }

    private static IEnumerable<string> EnumerateFiles(ScanRequest request)
    {
        foreach (string path in request.Paths)
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full)) yield return full;
            else if (Directory.Exists(full))
                foreach (string file in Directory.EnumerateFiles(full, "*", request.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)) yield return file;
        }
    }
}

public sealed class AntivirusHealthService(IYaraService yara, IClamAvService clam, IFreshClamService freshClam) : IAntivirusHealthService
{
    public async Task<IReadOnlyList<EngineHealthResult>> CheckAsync(CancellationToken cancellationToken) =>
        [await yara.RunSelfTestAsync(cancellationToken).ConfigureAwait(false), await clam.RunSelfTestAsync(cancellationToken).ConfigureAwait(false), await freshClam.GetHealthAsync(cancellationToken).ConfigureAwait(false)];
}

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddFFGuardianSecurityServices(this IServiceCollection services, Action<SecurityCoreOptions>? configure = null)
    {
        services.AddOptions<SecurityCoreOptions>();
        if (configure is not null) services.Configure(configure);
        services.AddSingleton<IProcessRunner, SecureProcessRunner>();
        services.AddSingleton<IFileHashService, FileHashService>();
        services.AddSingleton<IEngineLocatorService, EngineLocatorService>();
        services.AddSingleton<IPathExclusionService, PathExclusionService>();
        services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();
        services.AddSingleton<IYaraService, YaraService>();
        services.AddSingleton<IClamAvService, ClamAvService>();
        services.AddSingleton<IFreshClamService, FreshClamService>();
        services.AddSingleton<IQuarantineService, QuarantineService>();
        services.AddSingleton<IScanService, ScanService>();
        services.AddSingleton<IAntivirusHealthService, AntivirusHealthService>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return services;
    }
}
