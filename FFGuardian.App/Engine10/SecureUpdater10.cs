using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class SecureUpdater10 : IDisposable
{
    private const long MaximumPackageBytes = 1024L * 1024 * 1024;
    private readonly string _publicKeyPem;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public SecureUpdater10(string publicKeyPem, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        _publicKeyPem = publicKeyPem;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    public async Task<UpdateVerificationResult10> VerifyPackageAsync(
        UpdateManifest10 manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!ValidateManifestFields(manifest, out string validationError))
            return new(false, validationError, string.Empty, 0);
        if (!VerifyManifestSignature(manifest))
            return new(false, "Firma del manifesto non valida.", string.Empty, 0);
        if (!File.Exists(packagePath))
            return new(false, "Pacchetto non trovato.", string.Empty, 0);

        FileInfo info = new(packagePath);
        if (info.Length > MaximumPackageBytes)
            return new(false, "Pacchetto superiore al limite massimo consentito.", string.Empty, info.Length);

        string calculatedHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
        if (info.Length != manifest.Size)
            return new(false, "Dimensione del pacchetto non corrispondente.", calculatedHash, info.Length);
        if (!string.Equals(calculatedHash, NormalizeHash(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
            return new(false, "SHA-256 del pacchetto non valido.", calculatedHash, info.Length);

        return new(true, "Pacchetto e manifesto verificati.", calculatedHash, info.Length);
    }

    public async Task<UpdateStageResult10> DownloadAndStageAsync(
        UpdateManifest10 manifest,
        UpdateDownloadRequest10 request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(request);

        if (!ValidateManifestFields(manifest, out string validationError))
            return Failure(validationError, request.InstalledVersion);
        if (!VerifyManifestSignature(manifest))
            return Failure("Firma del manifesto non valida.", request.InstalledVersion);
        if (!Version.TryParse(manifest.Version, out Version? targetVersion))
            return Failure("Versione obiettivo non valida.", request.InstalledVersion);
        if (!Version.TryParse(manifest.MinimumVersion, out Version? minimumVersion))
            return Failure("Versione minima non valida.", request.InstalledVersion, targetVersion);
        if (targetVersion <= request.InstalledVersion)
            return Failure("Aggiornamento bloccato: downgrade o reinstallazione della stessa versione.", request.InstalledVersion, targetVersion);
        if (request.InstalledVersion < minimumVersion)
            return Failure("La versione installata è inferiore al requisito minimo del pacchetto.", request.InstalledVersion, targetVersion);
        if (!IsSafeHttpsUri(request.PackageUri))
            return Failure("Download bloccato: è richiesto HTTPS con host valido.", request.InstalledVersion, targetVersion);
        if (!IsSafePackageFileName(manifest.PackageFileName))
            return Failure("Nome pacchetto non valido.", request.InstalledVersion, targetVersion);

        string stagingRoot = Path.GetFullPath(request.StagingDirectory);
        Directory.CreateDirectory(stagingRoot);
        string targetFolder = Path.Combine(stagingRoot, targetVersion.ToString());
        Directory.CreateDirectory(targetFolder);
        string stagedPath = Path.Combine(targetFolder, manifest.PackageFileName);
        string tempPath = stagedPath + ".download";
        string backupPath = string.Empty;

        TryDelete(tempPath);
        try
        {
            await DownloadPackageAsync(request.PackageUri, tempPath, manifest.Size, cancellationToken).ConfigureAwait(false);
            UpdateVerificationResult10 verification = await VerifyPackageAsync(manifest, tempPath, cancellationToken)
                .ConfigureAwait(false);
            if (!verification.IsValid)
                return Failure(verification.Status, request.InstalledVersion, targetVersion, verification.CalculatedSha256);

            if (request.RequireAuthenticode && IsExecutablePackage(manifest.PackageFileName))
            {
                global::FFGuardian.AuthenticodeResult100 signature =
                    global::FFGuardian.AuthenticodeVerifier100.Verify(tempPath);
                if (!signature.IsSigned || !signature.IsTrusted)
                    return Failure(
                        $"Firma Authenticode del pacchetto non attendibile: {signature.Status}",
                        request.InstalledVersion,
                        targetVersion,
                        verification.CalculatedSha256);
            }

            if (!string.IsNullOrWhiteSpace(request.InstalledPackagePath) && File.Exists(request.InstalledPackagePath))
            {
                string backupFolder = Path.Combine(stagingRoot, "previous", request.InstalledVersion.ToString());
                Directory.CreateDirectory(backupFolder);
                backupPath = Path.Combine(backupFolder, Path.GetFileName(request.InstalledPackagePath));
                string backupTemp = backupPath + ".tmp";
                TryDelete(backupTemp);
                File.Copy(request.InstalledPackagePath, backupTemp, overwrite: true);
                File.Move(backupTemp, backupPath, overwrite: true);
            }

            File.Move(tempPath, stagedPath, overwrite: true);
            string stagedHash = await ComputeSha256Async(stagedPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(stagedHash, verification.CalculatedSha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(stagedPath);
                return Failure("Integrità persa durante lo staging atomico.", request.InstalledVersion, targetVersion, stagedHash);
            }

            return new UpdateStageResult10(
                true,
                "Aggiornamento verificato e preparato. Nessuna installazione automatica eseguita.",
                stagedPath,
                backupPath,
                request.InstalledVersion,
                targetVersion,
                stagedHash,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            TryDelete(tempPath);
            return Failure($"Preparazione aggiornamento non riuscita: {ex.Message}", request.InstalledVersion, targetVersion);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private async Task DownloadPackageAsync(
        Uri packageUri,
        string destinationPath,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, packageUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.RequestMessage?.RequestUri is not Uri finalUri || !IsSafeHttpsUri(finalUri))
            throw new HttpRequestException("Redirect verso destinazione non HTTPS bloccato.");
        if (response.Content.Headers.ContentLength is long declaredLength && declaredLength != expectedSize)
            throw new InvalidDataException("Dimensione HTTP dichiarata diversa dal manifesto.");

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream output = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > expectedSize || total > MaximumPackageBytes)
                throw new InvalidDataException("Download superiore alla dimensione autorizzata.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (total != expectedSize)
            throw new InvalidDataException("Download incompleto rispetto al manifesto.");
    }

    private bool VerifyManifestSignature(UpdateManifest10 manifest)
    {
        try
        {
            byte[] signature = Convert.FromBase64String(manifest.SignatureBase64);
            byte[] payload = Encoding.UTF8.GetBytes(CanonicalPayload(manifest));
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(_publicKeyPem);
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    internal static string CanonicalPayload(UpdateManifest10 manifest) => JsonSerializer.Serialize(new
    {
        manifest.Version,
        manifest.Channel,
        manifest.PackageFileName,
        Sha256 = NormalizeHash(manifest.Sha256),
        manifest.Size,
        manifest.MinimumVersion
    });

    private static bool ValidateManifestFields(UpdateManifest10 manifest, out string error)
    {
        if (manifest.Size <= 0 || manifest.Size > MaximumPackageBytes)
        {
            error = "Dimensione del manifesto non valida.";
            return false;
        }
        if (!Version.TryParse(manifest.Version, out _))
        {
            error = "Versione del manifesto non valida.";
            return false;
        }
        if (!Version.TryParse(manifest.MinimumVersion, out _))
        {
            error = "Versione minima del manifesto non valida.";
            return false;
        }
        if (NormalizeHash(manifest.Sha256).Length != 64)
        {
            error = "SHA-256 del manifesto non valido.";
            return false;
        }
        if (!IsSafePackageFileName(manifest.PackageFileName))
        {
            error = "Nome pacchetto del manifesto non valido.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool IsSafeHttpsUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(uri.DnsSafeHost) &&
        uri.UserInfo.Length == 0;

    private static bool IsSafePackageFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsExecutablePackage(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msix", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static UpdateStageResult10 Failure(
        string status,
        Version installedVersion,
        Version? targetVersion = null,
        string calculatedSha256 = "") =>
        new(false, status, string.Empty, string.Empty, installedVersion,
            targetVersion ?? installedVersion, calculatedSha256, DateTime.UtcNow);

    private static HttpClient CreateDefaultHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    private static string NormalizeHash(string value) =>
        (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttpClient) _httpClient.Dispose();
    }
}
