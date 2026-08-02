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
    public DateTime GeneratedUtc => _document.GeneratedUtc;
    public string DatabasePath => _databasePath;
    public bool IsStale => DateTime.UtcNow - _document.GeneratedUtc > TimeSpan.FromDays(7);

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

    public async Task<string> InstallVerifiedDatabaseAsync(
        string verifiedDatabasePath,
        string expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SignatureDatabaseDocument10 candidate = ParseAndValidate(verifiedDatabasePath);
            if (!string.Equals(candidate.DatabaseVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Versione database inattesa: {candidate.DatabaseVersion}");

            string backup = _databasePath + ".previous";
            string staging = _databasePath + ".new";
            File.Copy(verifiedDatabasePath, staging, true);

            try
            {
                if (File.Exists(_databasePath))
                    File.Copy(_databasePath, backup, true);
                File.Move(staging, _databasePath, true);
                _document = candidate;
                return backup;
            }
            catch
            {
                if (File.Exists(backup))
                    File.Copy(backup, _databasePath, true);
                _document = LoadOrCreate();
                throw;
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
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
                return ParseAndValidate(_databasePath);
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

    private static SignatureDatabaseDocument10 ParseAndValidate(string path)
    {
        SignatureDatabaseDocument10? loaded = JsonSerializer.Deserialize<SignatureDatabaseDocument10>(
            File.ReadAllText(path), JsonOptions);
        if (loaded is null || loaded.SchemaVersion != 1)
            throw new InvalidDataException("Database firme non valido o schema non supportato.");
        if (string.IsNullOrWhiteSpace(loaded.DatabaseVersion))
            throw new InvalidDataException("Versione database firme mancante.");
        if (loaded.GeneratedUtc > DateTime.UtcNow.AddMinutes(10))
            throw new InvalidDataException("Data del database firme non valida.");
        return Normalize(loaded);
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
            .Where(entry => entry.Sha256.Length == 64 && entry.Sha256.All(Uri.IsHexDigit))
            .GroupBy(entry => entry.Sha256, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.Confidence).First())
            .ToArray();

        string[] allowList = document.AllowListSha256
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Where(value => value.Length == 64 && value.All(Uri.IsHexDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return document with { Signatures = signatures, AllowListSha256 = allowList };
    }
}