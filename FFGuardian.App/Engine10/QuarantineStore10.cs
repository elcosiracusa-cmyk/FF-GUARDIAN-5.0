using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class QuarantineStore10
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QuarantineStore10(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian",
            "Engine10",
            "Quarantine");
        Directory.CreateDirectory(_root);
    }

    public async Task<QuarantineRecord10> QuarantineAsync(
        FileScanResult10 result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Verdict is not ThreatVerdict10.Malicious and not ThreatVerdict10.Suspicious)
            throw new InvalidOperationException("La quarantena è consentita solo per file sospetti o malevoli.");

        string source = Path.GetFullPath(result.Path);
        if (!File.Exists(source)) throw new FileNotFoundException("File da mettere in quarantena non trovato.", source);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = Guid.NewGuid().ToString("N");
            string folder = Path.Combine(_root, id);
            Directory.CreateDirectory(folder);

            string storedPath = Path.Combine(folder, "payload.bin");
            string metadataPath = Path.Combine(folder, "metadata.json");
            string tempPayload = storedPath + ".tmp";

            File.Move(source, tempPayload);
            File.Move(tempPayload, storedPath, true);

            QuarantineRecord10 record = new(
                id,
                source,
                storedPath,
                result.Sha256,
                result.DetectionName,
                DateTime.UtcNow,
                false);

            await File.WriteAllTextAsync(metadataPath + ".tmp", JsonSerializer.Serialize(record, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
            File.Move(metadataPath + ".tmp", metadataPath, true);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string folder = Path.Combine(_root, id);
            string metadataPath = Path.Combine(folder, "metadata.json");
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("Metadati quarantena non trovati.", metadataPath);

            QuarantineRecord10 record = JsonSerializer.Deserialize<QuarantineRecord10>(
                await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException("Metadati quarantena non validi.");

            if (record.Restored) return;
            if (!File.Exists(record.StoredPath)) throw new FileNotFoundException("Contenuto quarantena non trovato.", record.StoredPath);

            string? destinationFolder = Path.GetDirectoryName(record.OriginalPath);
            if (!string.IsNullOrWhiteSpace(destinationFolder)) Directory.CreateDirectory(destinationFolder);
            if (File.Exists(record.OriginalPath))
                throw new IOException("Nel percorso originale esiste già un file. Ripristino annullato.");

            File.Move(record.StoredPath, record.OriginalPath);
            QuarantineRecord10 updated = record with { Restored = true };
            await File.WriteAllTextAsync(metadataPath + ".tmp", JsonSerializer.Serialize(updated, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
            File.Move(metadataPath + ".tmp", metadataPath, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}