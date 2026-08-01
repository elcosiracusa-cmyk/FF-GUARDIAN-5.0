using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class SignatureDatabase10
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SignatureDatabaseDocument10 _document;

    public SignatureDatabase10(string? databasePath = null)
    {
        string dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian",
            "Engine10");

        Directory.CreateDirectory(dataFolder);
        _databasePath = databasePath ?? Path.Combine(dataFolder, "signatures-v10.json");
        _document = LoadOrCreate();
    }

    public string Version => _document.DatabaseVersion;

    public async Task<SignatureEntry10?> FindSignatureAsync(string sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _document.Signatures.FirstOrDefault(entry =>
                entry.Enabled && string.Equals(entry.Sha256, sha256, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsAllowListedAsync(string sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _document.AllowListSha256.Contains(sha256, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _document = LoadOrCreate();
        }
        finally
        {
            _gate.Release();
        }
    }

    private SignatureDatabaseDocument10 LoadOrCreate()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                SignatureDatabaseDocument10? loaded = JsonSerializer.Deserialize<SignatureDatabaseDocument10>(
                    File.ReadAllText(_databasePath), JsonOptions);

                if (loaded is not null && loaded.SchemaVersion == 1)
                    return Normalize(loaded);
            }
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }

        SignatureDatabaseDocument10 empty = new(
            SchemaVersion: 1,
            DatabaseVersion: "10.0.0-empty",
            GeneratedUtc: DateTime.UtcNow,
            Signatures: Array.Empty<SignatureEntry10>(),
            AllowListSha256: Array.Empty<string>());

        SaveAtomic(empty);
        return empty;
    }

    private void SaveAtomic(SignatureDatabaseDocument10 document)
    {
        string? folder = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);

        string temp = _databasePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temp, _databasePath, true);
    }

    private static SignatureDatabaseDocument10 Normalize(SignatureDatabaseDocument10 document)
    {
        SignatureEntry10[] signatures = document.Signatures
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Sha256))
            .Select(entry => entry with { Sha256 = entry.Sha256.Trim().ToUpperInvariant() })
            .GroupBy(entry => entry.Sha256, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.Confidence).First())
            .ToArray();

        string[] allowList = document.AllowListSha256
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return document with { Signatures = signatures, AllowListSha256 = allowList };
    }
}