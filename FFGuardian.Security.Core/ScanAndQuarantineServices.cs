using System.Collections.Concurrent;
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
        string hash = await hashes.ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            QuarantineEntry? existing = await FindByHashAsync(hash, cancellationToken).ConfigureAwait(false);
            if (existing is not null && File.Exists(existing.StoredPath))
                return new(false, existing, "File già presente in quarantena.");

            Guid id = Guid.NewGuid();
            string stored = Path.Combine(_root, id.ToString("N") + ".qdat");
            string metadata = Path.Combine(_root, id.ToString("N") + ".json");
            FileInfo info = new(source);
            try
            {
                await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (FileStream output = new(stored, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
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
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken)
    {
        QuarantineEntry? entry = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null || !File.Exists(entry.StoredPath)) return new(false, entry, "Elemento non trovato.");
        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination) && !overwrite) return new(false, entry, "Destinazione già esistente.");
        string storedHash = await hashes.ComputeSha256Async(entry.StoredPath, cancellationToken).ConfigureAwait(false);
        if (!entry.Sha256.Equals(storedHash, StringComparison.OrdinalIgnoreCase)) return new(false, entry, "Hash quarantena non valido.");
        string? directory = Path.GetDirectoryName(destination);
        if (directory is null) return new(false, entry, "Destinazione non valida.");
        Directory.CreateDirectory(directory);
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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return entries.OrderByDescending(entry => entry.CreatedAt).ToArray();
    }

    private async Task<QuarantineEntry?> FindByHashAsync(string hash, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root)) return null;
        foreach (string file in Directory.EnumerateFiles(_root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                QuarantineEntry? entry = JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), JsonOptions);
                if (entry is not null && entry.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase)) return entry;
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    private async Task<QuarantineEntry?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        string metadata = Path.Combine(_root, id.ToString("N") + ".json");
        if (!File.Exists(metadata)) return null;
        try
        {
            return JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(metadata, cancellationToken).ConfigureAwait(false), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => _gate.Dispose();
}

public sealed class ScanService : IScanService
{
    private readonly IYaraService _yara;
    private readonly IClamAvService _clam;
    private readonly IPathExclusionService _exclusions;
    private readonly IQuarantineService _quarantine;
    private readonly ISecurityEventLogger _logger;
    private readonly IFileHashService _hashes;
    private readonly IScanCacheService _cache;
    private readonly SecurityCoreOptions _options;

    public ScanService(
        IYaraService yara,
        IClamAvService clam,
        IPathExclusionService exclusions,
        IQuarantineService quarantine,
        ISecurityEventLogger logger,
        IFileHashService hashes,
        IScanCacheService cache,
        IOptions<SecurityCoreOptions> options)
    {
        _yara = yara;
        _clam = clam;
        _exclusions = exclusions;
        _quarantine = quarantine;
        _logger = logger;
        _hashes = hashes;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int scanned = 0;
        int skipped = 0;
        int failed = 0;
        ConcurrentBag<ScanDetection> detections = [];
        ConcurrentQueue<string> errors = new();
        ConcurrentDictionary<string, byte> engines = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            ParallelOptions parallelOptions = new()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(_options.MaximumScanConcurrency, 1, 32)
            };

            await Parallel.ForEachAsync(SafeEnumerateFiles(request, errors, cancellationToken), parallelOptions, async (file, token) =>
            {
                if (_exclusions.ShouldExclude(file))
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                try
                {
                    FileInfo info = new(file);
                    if (!info.Exists || info.Length > _options.MaximumFileSizeBytes)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    if (_options.EnableIncrementalScanCache && !request.ForceRescan)
                    {
                        ScanCacheEntry? cached = await _cache.TryGetValidAsync(file, info.Length, info.LastWriteTimeUtc, token).ConfigureAwait(false);
                        if (cached is not null)
                        {
                            Interlocked.Increment(ref skipped);
                            progress?.Report(new(Volatile.Read(ref scanned), Volatile.Read(ref skipped), file));
                            return;
                        }
                    }

                    IReadOnlyList<YaraMatch> yaraMatches = await _yara.ScanFileAsync(file, token).ConfigureAwait(false);
                    engines.TryAdd("YARA", 0);
                    foreach (YaraMatch match in yaraMatches)
                        detections.Add(new("YARA", match.Rule, file, match.RawOutput));

                    IReadOnlyList<ClamAvDetection> clamMatches = await _clam.ScanFileAsync(file, token).ConfigureAwait(false);
                    engines.TryAdd("ClamAV", 0);
                    foreach (ClamAvDetection match in clamMatches)
                        detections.Add(new("ClamAV", match.Signature, file, match.RawOutput));

                    int completed = Interlocked.Increment(ref scanned);
                    progress?.Report(new(completed, Volatile.Read(ref skipped), file));

                    ScanDetection? first = detections.FirstOrDefault(item => item.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase));
                    if (request.QuarantineDetections && first is not null)
                    {
                        await _quarantine.QuarantineAsync(file, first.Engine, first.Name, "High", token).ConfigureAwait(false);
                    }
                    else if (first is null && _options.EnableIncrementalScanCache)
                    {
                        string sha256 = await _hashes.ComputeSha256Async(file, token).ConfigureAwait(false);
                        await _cache.StoreAsync(new(file, info.Length, info.LastWriteTimeUtc, sha256, DateTimeOffset.UtcNow), token).ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    Interlocked.Increment(ref failed);
                    errors.Enqueue($"{file}: {exception.Message}");
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections.ToArray(), engines.Keys.ToArray(), true, errors.ToArray());
        }

        if (_options.EnableIncrementalScanCache)
            await _cache.PruneAsync(cancellationToken).ConfigureAwait(false);

        await _logger.LogAsync("Scan", "Completed", $"scanned={scanned}; skipped={skipped}; failed={failed}; detections={detections.Count}", cancellationToken).ConfigureAwait(false);
        return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections.ToArray(), engines.Keys.ToArray(), false, errors.ToArray());
    }

    private static IEnumerable<string> SafeEnumerateFiles(ScanRequest request, ConcurrentQueue<string> errors, CancellationToken cancellationToken)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in request.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Enqueue($"{path}: percorso non valido.");
                continue;
            }

            if (File.Exists(full))
            {
                if (visited.Add(full)) yield return full;
                continue;
            }
            if (!Directory.Exists(full)) continue;

            Stack<string> pending = new();
            pending.Push(full);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(directory); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    errors.Enqueue($"{directory}: {exception.Message}");
                    continue;
                }
                foreach (string file in files)
                    if (visited.Add(file)) yield return file;

                if (!request.Recursive) continue;
                IEnumerable<string> directories;
                try { directories = Directory.EnumerateDirectories(directory); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    errors.Enqueue($"{directory}: {exception.Message}");
                    continue;
                }
                foreach (string child in directories) pending.Push(child);
            }
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
        services.AddSingleton<IScanCacheService, ScanCacheService>();
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
