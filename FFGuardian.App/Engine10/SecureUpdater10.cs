using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class SecureUpdater10
{
    private readonly string _publicKeyPem;

    public SecureUpdater10(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        _publicKeyPem = publicKeyPem;
    }

    public async Task<UpdateVerificationResult10> VerifyPackageAsync(
        UpdateManifest10 manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!File.Exists(packagePath))
            return new(false, "Pacchetto non trovato.", string.Empty, 0);

        FileInfo info = new(packagePath);
        string calculatedHash;
        await using (FileStream stream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                         1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            calculatedHash = Convert.ToHexString(hash);
        }

        if (info.Length != manifest.Size)
            return new(false, "Dimensione del pacchetto non corrispondente.", calculatedHash, info.Length);
        if (!string.Equals(calculatedHash, NormalizeHash(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
            return new(false, "SHA-256 del pacchetto non valido.", calculatedHash, info.Length);
        if (!VerifyManifestSignature(manifest))
            return new(false, "Firma del manifesto non valida.", calculatedHash, info.Length);

        return new(true, "Pacchetto e manifesto verificati.", calculatedHash, info.Length);
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

    private static string CanonicalPayload(UpdateManifest10 manifest) => JsonSerializer.Serialize(new
    {
        manifest.Version,
        manifest.Channel,
        manifest.PackageFileName,
        Sha256 = NormalizeHash(manifest.Sha256),
        manifest.Size,
        manifest.MinimumVersion
    });

    private static string NormalizeHash(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
