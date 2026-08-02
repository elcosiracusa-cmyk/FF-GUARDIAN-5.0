using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed record SignatureUpdateManifest10(
    string Version,
    DateTime GeneratedUtc,
    string DatabaseUrl,
    string DatabaseSha256,
    long Size,
    string SignatureBase64);

internal sealed record SignatureUpdateResult10(
    bool Succeeded,
    string Status,
    string InstalledVersion,
    string BackupPath,
    DateTime CompletedUtc);

internal sealed class SignatureUpdateManager10 : IDisposable
{
    private const long MaximumDatabaseBytes = 50L * 1024L * 1024L;
    private readonly HttpClient _httpClient;
    private readonly RSA _rsa;

    public SignatureUpdateManager10(string publicKeyPem, HttpMessageHandler? handler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        _rsa = RSA.Create();
        _rsa.ImportFromPem(publicKeyPem);
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _httpClient.Timeout = TimeSpan.FromSeconds(45);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FFGuardian", "10.0"));
    }

    public async Task<SignatureUpdateResult10> DownloadAndInstallAsync(
        Uri manifestUri,
        SignatureDatabase10 database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestUri);
        ArgumentNullException.ThrowIfNull(database);
        EnsureHttps(manifestUri);

        using HttpResponseMessage manifestResponse = await _httpClient.GetAsync(
            manifestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        manifestResponse.EnsureSuccessStatusCode();

        string manifestJson = await manifestResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        SignatureUpdateManifest10 manifest = JsonSerializer.Deserialize<SignatureUpdateManifest10>(manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifesto firme non valido.");

        ValidateManifest(manifest);
        VerifyManifestSignature(manifest);

        Uri databaseUri = new(manifest.DatabaseUrl, UriKind.Absolute);
        EnsureHttps(databaseUri);
        if (manifest.Size <= 0 || manifest.Size > MaximumDatabaseBytes)
            throw new InvalidDataException("Dimensione database firme non consentita.");

        Version currentVersion = ParseVersion(database.Version);
        Version targetVersion = ParseVersion(manifest.Version);
        if (targetVersion <= currentVersion)
        {
            return new SignatureUpdateResult10(
                true,
                "Database firme già aggiornato.",
                database.Version,
                string.Empty,
                DateTime.UtcNow);
        }

        string stagingFolder = Path.Combine(Path.GetTempPath(), "FFGuardian", "SignatureUpdates");
        Directory.CreateDirectory(stagingFolder);
        string packagePath = Path.Combine(stagingFolder, $"signatures-{Guid.NewGuid():N}.json");

        try
        {
            using HttpResponseMessage packageResponse = await _httpClient.GetAsync(
                databaseUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            packageResponse.EnsureSuccessStatusCode();

            long? declaredLength = packageResponse.Content.Headers.ContentLength;
            if (declaredLength is > MaximumDatabaseBytes)
                throw new InvalidDataException("Database firme remoto troppo grande.");

            await using (Stream source = await packageResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (FileStream destination = new(packagePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaximumDatabaseBytes)
                        throw new InvalidDataException("Database firme oltre il limite consentito.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }

            FileInfo downloaded = new(packagePath);
            if (downloaded.Length != manifest.Size)
                throw new InvalidDataException("Dimensione database firme non corrispondente al manifesto.");

            string calculatedHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(calculatedHash),
                    Convert.FromHexString(manifest.DatabaseSha256)))
                throw new CryptographicException("SHA-256 del database firme non valido.");

            string backup = await database.InstallVerifiedDatabaseAsync(
                packagePath, manifest.Version, cancellationToken).ConfigureAwait(false);

            return new SignatureUpdateResult10(
                true,
                "Database firme aggiornato e verificato.",
                database.Version,
                backup,
                DateTime.UtcNow);
        }
        finally
        {
            try { if (File.Exists(packagePath)) File.Delete(packagePath); }
            catch { }
        }
    }

    private void VerifyManifestSignature(SignatureUpdateManifest10 manifest)
    {
        string canonical = string.Join("|",
            manifest.Version.Trim(),
            manifest.GeneratedUtc.ToUniversalTime().ToString("O"),
            manifest.DatabaseUrl.Trim(),
            manifest.DatabaseSha256.Trim().ToUpperInvariant(),
            manifest.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));

        byte[] data = Encoding.UTF8.GetBytes(canonical);
        byte[] signature = Convert.FromBase64String(manifest.SignatureBase64);
        if (!_rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Firma RSA-PSS del manifesto firme non valida.");
    }

    private static void ValidateManifest(SignatureUpdateManifest10 manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Versione firme mancante.");
        if (manifest.GeneratedUtc > DateTime.UtcNow.AddMinutes(10))
            throw new InvalidDataException("Data manifesto firme non valida.");
        if (DateTime.UtcNow - manifest.GeneratedUtc > TimeSpan.FromDays(30))
            throw new InvalidDataException("Manifesto firme troppo vecchio.");
        if (manifest.DatabaseSha256.Length != 64 || !manifest.DatabaseSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("SHA-256 manifesto non valido.");
        _ = Convert.FromBase64String(manifest.SignatureBase64);
    }

    private static void EnsureHttps(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Gli aggiornamenti firme richiedono HTTPS.");
    }

    private static Version ParseVersion(string value)
    {
        string numeric = value.Split('-', '+')[0];
        return Version.TryParse(numeric, out Version? parsed) ? parsed : new Version(0, 0);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rsa.Dispose();
    }
}