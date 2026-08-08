using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class ScanCacheService : IScanCacheService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _cachePath;
    private readonly TimeSpan _retention;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, ScanCacheEntry>? _entries;
    private bool _dirty;

    public ScanCacheService(IOptions<SecurityCoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string dataRoot = Path.GetFullPath(options.Value.DataDirectory);
        _cachePath = Path.Combine(dataRoot, "Cache", "scan-cache.json");
        _retention = options.Value.ScanCacheRetention;
    }

    public async Task<ScanCacheEntry?> TryGetValidAsync(string path, long size, DateTime lastWriteUtc, CancellationToken cancellationToken)
    {
        string normalized = Path.GetFullPath(path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, ScanCacheEntry> entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!entries.TryGetValue(normalized, out ScanCacheEntry? entry)) return null;
            if (entry.Size != size || entry.LastWriteUtc != lastWriteUtc || DateTimeOffset.UtcNow - entry.VerifiedAt > _retention)
            {
                entries.Remove(normalized);
                _dirty = true;
                return null;
            }
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StoreAsync(ScanCacheEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string normalized = Path.GetFullPath(entry.Path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, ScanCacheEntry> entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
            entries[normalized] = entry with { Path = normalized };
            _dirty = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PruneAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, ScanCacheEntry> entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - _retention;
            string[] stale = entries
                .Where(pair => pair.Value.VerifiedAt < cutoff || !File.Exists(pair.Key))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (string key in stale) entries.Remove(key);
            if (stale.Length > 0) _dirty = true;
            if (_dirty)
            {
                await SaveAsync(entries, cancellationToken).ConfigureAwait(false);
                _dirty = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, ScanCacheEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_entries is not null) return _entries;
        if (!File.Exists(_cachePath)) return _entries = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using FileStream stream = new(_cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            Dictionary<string, ScanCacheEntry>? loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, ScanCacheEntry>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return _entries = loaded is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return _entries = new(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return _entries = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveAsync(Dictionary<string, ScanCacheEntry> entries, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_cachePath);
        if (directory is null) throw new InvalidOperationException("Percorso cache non valido.");
        Directory.CreateDirectory(directory);
        string temporary = _cachePath + ".tmp";
        await using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, _cachePath, true);
    }

    public void Dispose() => _gate.Dispose();
}
