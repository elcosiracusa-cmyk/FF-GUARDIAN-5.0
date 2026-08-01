using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class RollbackManager10
{
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RollbackManager10(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian", "Rollback");
        Directory.CreateDirectory(_root);
    }

    public async Task<RollbackRecord10> BackupFileAsync(string path, string action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("File da salvare non trovato.", path);

        string id = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(_root, id);
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, "payload.bin");
        string metadata = Path.Combine(folder, "record.json");

        await using (FileStream source = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        await using (FileStream destination = new(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        RollbackRecord10 record = new(id, action, Path.GetFullPath(path), backup, metadata, DateTime.UtcNow, false);
        await WriteRecordAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task RestoreFileAsync(RollbackRecord10 record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Restored) throw new InvalidOperationException("Il backup risulta già ripristinato.");
        if (!File.Exists(record.BackupPath)) throw new FileNotFoundException("Backup non disponibile.", record.BackupPath);

        string? directory = Path.GetDirectoryName(record.Target);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temp = record.Target + ".ffguardian-restore.tmp";
        File.Copy(record.BackupPath, temp, true);
        File.Move(temp, record.Target, true);
        await WriteRecordAsync(record with { Restored = true }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteRecordAsync(RollbackRecord10 record, CancellationToken cancellationToken)
    {
        string temp = record.MetadataPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(record, JsonOptions), cancellationToken).ConfigureAwait(false);
        File.Move(temp, record.MetadataPath, true);
    }
}
