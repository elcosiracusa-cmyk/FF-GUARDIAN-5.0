using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed record RegistryRollbackSnapshot10(
    RegistryHive Hive,
    RegistryView View,
    string KeyPath,
    string ValueName,
    bool ValueExisted,
    RegistryValueKind ValueKind,
    string? StringValue,
    long? NumericValue,
    string[]? MultiStringValue,
    byte[]? BinaryValue);

internal sealed record ServiceRollbackSnapshot10(
    string ServiceName,
    bool KeyExisted,
    string ImagePath,
    string ObjectName,
    int Start,
    int Type,
    int ErrorControl);

internal sealed record ScheduledTaskRollbackSnapshot10(
    string TaskName,
    bool Existed,
    string Xml);

internal sealed class RollbackManager10
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RollbackManager10(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian", "Rollback");
        Directory.CreateDirectory(_root);
    }

    public async Task<RollbackRecord10> BackupFileAsync(
        string path,
        string action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string target = Path.GetFullPath(path);
        if (!File.Exists(target))
            throw new FileNotFoundException("File da salvare non trovato.", target);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string folder, string metadata) = CreateRecordFolder();
            string backup = Path.Combine(folder, "payload.bin");
            string expectedHash = await ComputeSha256Async(target, cancellationToken).ConfigureAwait(false);

            await using (FileStream source = new(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            await using (FileStream destination = new(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            string backupHash = await ComputeSha256Async(backup, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(expectedHash, backupHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Il backup del file non supera la verifica SHA-256.");

            RollbackRecord10 record = new(
                Path.GetFileName(folder), action, target, backup, metadata, DateTime.UtcNow, false);
            await WriteEnvelopeAsync(record, "File", expectedHash, cancellationToken).ConfigureAwait(false);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreFileAsync(
        RollbackRecord10 record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (record.Restored)
                throw new InvalidOperationException("Il backup risulta già ripristinato.");
            if (!File.Exists(record.BackupPath))
                throw new FileNotFoundException("Backup non disponibile.", record.BackupPath);

            RollbackEnvelope10 envelope = await ReadEnvelopeAsync(record.MetadataPath, cancellationToken).ConfigureAwait(false);
            string backupHash = await ComputeSha256Async(record.BackupPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(backupHash, envelope.IntegritySha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Integrità del backup file non valida.");

            string? directory = Path.GetDirectoryName(record.Target);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temp = record.Target + ".ffguardian-restore.tmp";
            File.Copy(record.BackupPath, temp, true);
            string restoredHash = await ComputeSha256Async(temp, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(restoredHash, envelope.IntegritySha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temp);
                throw new InvalidDataException("Il file temporaneo di ripristino non supera la verifica SHA-256.");
            }

            File.Move(temp, record.Target, true);
            await MarkRestoredAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RollbackRecord10> BackupRegistryValueAsync(
        RegistryHive hive,
        RegistryView view,
        string keyPath,
        string valueName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentNullException.ThrowIfNull(valueName);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? key = baseKey.OpenSubKey(keyPath, writable: false);
            object? value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            bool existed = value is not null;
            RegistryValueKind kind = existed ? key!.GetValueKind(valueName) : RegistryValueKind.None;

            RegistryRollbackSnapshot10 snapshot = CreateRegistrySnapshot(
                hive, view, keyPath, valueName, existed, kind, value);
            return await SaveSnapshotAsync(
                action,
                $"{hive}\\{keyPath}::{valueName}",
                "RegistryValue",
                snapshot,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreRegistryValueAsync(
        RollbackRecord10 record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RollbackEnvelope10 envelope = await ReadEnvelopeAsync(record.MetadataPath, cancellationToken).ConfigureAwait(false);
            EnsureKind(envelope, "RegistryValue");
            RegistryRollbackSnapshot10 snapshot = DeserializeSnapshot<RegistryRollbackSnapshot10>(envelope);

            using RegistryKey baseKey = RegistryKey.OpenBaseKey(snapshot.Hive, snapshot.View);
            using RegistryKey key = baseKey.CreateSubKey(snapshot.KeyPath, writable: true)
                ?? throw new UnauthorizedAccessException("Impossibile aprire la chiave di registro in scrittura.");

            if (!snapshot.ValueExisted)
            {
                key.DeleteValue(snapshot.ValueName, throwOnMissingValue: false);
            }
            else
            {
                object value = RestoreRegistryValue(snapshot);
                key.SetValue(snapshot.ValueName, value, snapshot.ValueKind);
            }

            await MarkRestoredAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RollbackRecord10> BackupServiceAsync(
        string serviceName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ValidateSimpleName(serviceName, nameof(serviceName));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(keyPath, writable: false);

            ServiceRollbackSnapshot10 snapshot = new(
                serviceName,
                key is not null,
                key?.GetValue("ImagePath", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty,
                key?.GetValue("ObjectName")?.ToString() ?? string.Empty,
                ConvertToInt32(key?.GetValue("Start"), -1),
                ConvertToInt32(key?.GetValue("Type"), -1),
                ConvertToInt32(key?.GetValue("ErrorControl"), -1));

            return await SaveSnapshotAsync(action, serviceName, "Service", snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreServiceAsync(
        RollbackRecord10 record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RollbackEnvelope10 envelope = await ReadEnvelopeAsync(record.MetadataPath, cancellationToken).ConfigureAwait(false);
            EnsureKind(envelope, "Service");
            ServiceRollbackSnapshot10 snapshot = DeserializeSnapshot<ServiceRollbackSnapshot10>(envelope);
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{snapshot.ServiceName}";

            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            if (!snapshot.KeyExisted)
            {
                try { baseKey.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
                catch (UnauthorizedAccessException) { throw; }
            }
            else
            {
                using RegistryKey key = baseKey.CreateSubKey(keyPath, writable: true)
                    ?? throw new UnauthorizedAccessException("Impossibile ripristinare la configurazione del servizio.");
                SetOrDelete(key, "ImagePath", snapshot.ImagePath, RegistryValueKind.ExpandString);
                SetOrDelete(key, "ObjectName", snapshot.ObjectName, RegistryValueKind.String);
                SetOrDelete(key, "Start", snapshot.Start, RegistryValueKind.DWord);
                SetOrDelete(key, "Type", snapshot.Type, RegistryValueKind.DWord);
                SetOrDelete(key, "ErrorControl", snapshot.ErrorControl, RegistryValueKind.DWord);
            }

            await MarkRestoredAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RollbackRecord10> BackupScheduledTaskAsync(
        string taskName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ValidateTaskName(taskName);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProcessResult10 query = await RunProcessAsync(
                "schtasks.exe", $"/Query /TN \"{taskName}\" /XML", cancellationToken).ConfigureAwait(false);
            bool existed = query.ExitCode == 0;
            if (!existed && string.IsNullOrWhiteSpace(query.StandardError))
                query = query with { StandardError = "Attività pianificata non trovata." };

            ScheduledTaskRollbackSnapshot10 snapshot = new(
                taskName,
                existed,
                existed ? query.StandardOutput : string.Empty);
            return await SaveSnapshotAsync(action, taskName, "ScheduledTask", snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreScheduledTaskAsync(
        RollbackRecord10 record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RollbackEnvelope10 envelope = await ReadEnvelopeAsync(record.MetadataPath, cancellationToken).ConfigureAwait(false);
            EnsureKind(envelope, "ScheduledTask");
            ScheduledTaskRollbackSnapshot10 snapshot = DeserializeSnapshot<ScheduledTaskRollbackSnapshot10>(envelope);

            if (!snapshot.Existed)
            {
                await RunProcessCheckedAsync(
                    "schtasks.exe", $"/Delete /TN \"{snapshot.TaskName}\" /F", allowNotFound: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                string xmlPath = Path.Combine(Path.GetDirectoryName(record.MetadataPath)!, "task.xml");
                await File.WriteAllTextAsync(xmlPath, snapshot.Xml, cancellationToken).ConfigureAwait(false);
                await RunProcessCheckedAsync(
                    "schtasks.exe", $"/Create /TN \"{snapshot.TaskName}\" /XML \"{xmlPath}\" /F",
                    allowNotFound: false,
                    cancellationToken).ConfigureAwait(false);
            }

            await MarkRestoredAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RollbackRecord10> GetRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ValidateSimpleName(id, nameof(id));
        string metadata = Path.Combine(_root, id, "record.json");
        RollbackEnvelope10 envelope = await ReadEnvelopeAsync(metadata, cancellationToken).ConfigureAwait(false);
        return envelope.Record;
    }

    private async Task<RollbackRecord10> SaveSnapshotAsync<T>(
        string action,
        string target,
        string kind,
        T snapshot,
        CancellationToken cancellationToken)
    {
        (string folder, string metadata) = CreateRecordFolder();
        string snapshotPath = Path.Combine(folder, "snapshot.json");
        string snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(snapshotPath, snapshotJson, cancellationToken).ConfigureAwait(false);
        string hash = await ComputeSha256Async(snapshotPath, cancellationToken).ConfigureAwait(false);

        RollbackRecord10 record = new(
            Path.GetFileName(folder), action, target, snapshotPath, metadata, DateTime.UtcNow, false);
        await WriteEnvelopeAsync(record, kind, hash, cancellationToken).ConfigureAwait(false);
        return record;
    }

    private (string Folder, string Metadata) CreateRecordFolder()
    {
        string id = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(_root, id);
        Directory.CreateDirectory(folder);
        return (folder, Path.Combine(folder, "record.json"));
    }

    private static RegistryRollbackSnapshot10 CreateRegistrySnapshot(
        RegistryHive hive,
        RegistryView view,
        string keyPath,
        string valueName,
        bool existed,
        RegistryValueKind kind,
        object? value) => kind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString =>
            new(hive, view, keyPath, valueName, existed, kind, value?.ToString(), null, null, null),
        RegistryValueKind.DWord or RegistryValueKind.QWord =>
            new(hive, view, keyPath, valueName, existed, kind, null, value is null ? null : Convert.ToInt64(value), null, null),
        RegistryValueKind.MultiString =>
            new(hive, view, keyPath, valueName, existed, kind, null, null, value as string[], null),
        RegistryValueKind.Binary =>
            new(hive, view, keyPath, valueName, existed, kind, null, null, null, value as byte[]),
        _ => new(hive, view, keyPath, valueName, existed, kind, value?.ToString(), null, null, null)
    };

    private static object RestoreRegistryValue(RegistryRollbackSnapshot10 snapshot) => snapshot.ValueKind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString => snapshot.StringValue ?? string.Empty,
        RegistryValueKind.DWord => checked((int)(snapshot.NumericValue ?? 0)),
        RegistryValueKind.QWord => snapshot.NumericValue ?? 0L,
        RegistryValueKind.MultiString => snapshot.MultiStringValue ?? Array.Empty<string>(),
        RegistryValueKind.Binary => snapshot.BinaryValue ?? Array.Empty<byte>(),
        _ => snapshot.StringValue ?? string.Empty
    };

    private async Task WriteEnvelopeAsync(
        RollbackRecord10 record,
        string kind,
        string integritySha256,
        CancellationToken cancellationToken)
    {
        RollbackEnvelope10 envelope = new(record, kind, integritySha256, DateTime.UtcNow);
        string temp = record.MetadataPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(envelope, JsonOptions), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temp, record.MetadataPath, true);
    }

    private static async Task<RollbackEnvelope10> ReadEnvelopeAsync(
        string metadataPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException("Metadati rollback non trovati.", metadataPath);
        RollbackEnvelope10 envelope = JsonSerializer.Deserialize<RollbackEnvelope10>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), JsonOptions)
            ?? throw new InvalidDataException("Metadati rollback non validi.");

        if (!File.Exists(envelope.Record.BackupPath))
            throw new FileNotFoundException("Contenuto rollback non trovato.", envelope.Record.BackupPath);
        string hash = await ComputeSha256Async(envelope.Record.BackupPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(hash, envelope.IntegritySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Integrità del contenuto rollback non valida.");
        return envelope;
    }

    private async Task MarkRestoredAsync(RollbackEnvelope10 envelope, CancellationToken cancellationToken)
    {
        RollbackRecord10 updated = envelope.Record with { Restored = true };
        await WriteEnvelopeAsync(updated, envelope.Kind, envelope.IntegritySha256, cancellationToken).ConfigureAwait(false);
    }

    private static T DeserializeSnapshot<T>(RollbackEnvelope10 envelope)
    {
        string json = File.ReadAllText(envelope.Record.BackupPath);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException("Snapshot rollback non valido.");
    }

    private static void EnsureKind(RollbackEnvelope10 envelope, string expected)
    {
        if (!string.Equals(envelope.Kind, expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Tipo rollback non compatibile. Atteso: {expected}.");
        if (envelope.Record.Restored)
            throw new InvalidOperationException("Il rollback risulta già applicato.");
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using SHA256 sha256 = SHA256.Create();
        return Convert.ToHexString(await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static int ConvertToInt32(object? value, int fallback)
    {
        try { return value is null ? fallback : Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static void SetOrDelete(RegistryKey key, string name, string value, RegistryValueKind kind)
    {
        if (string.IsNullOrWhiteSpace(value)) key.DeleteValue(name, false);
        else key.SetValue(name, value, kind);
    }

    private static void SetOrDelete(RegistryKey key, string name, int value, RegistryValueKind kind)
    {
        if (value < 0) key.DeleteValue(name, false);
        else key.SetValue(name, value, kind);
    }

    private static void ValidateSimpleName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Identificativo non valido.", parameterName);
    }

    private static void ValidateTaskName(string taskName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        if (taskName.Contains('"') || taskName.Contains('\r') || taskName.Contains('\n'))
            throw new ArgumentException("Nome attività pianificata non valido.", nameof(taskName));
    }

    private static async Task<ProcessResult10> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Impossibile avviare {fileName}.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessResult10(process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    private static async Task RunProcessCheckedAsync(
        string fileName,
        string arguments,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        ProcessResult10 result = await RunProcessAsync(fileName, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 && !(allowNotFound && result.ExitCode == 1))
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"{fileName} ha restituito il codice {result.ExitCode}."
                    : result.StandardError.Trim());
    }

    private sealed record RollbackEnvelope10(
        RollbackRecord10 Record,
        string Kind,
        string IntegritySha256,
        DateTime WrittenUtc);

    private sealed record ProcessResult10(int ExitCode, string StandardOutput, string StandardError);
}
