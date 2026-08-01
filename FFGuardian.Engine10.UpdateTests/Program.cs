using System.Net;
using System.Security.Cryptography;
using System.Text;
using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Update-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(root);

        try
        {
            byte[] package = Encoding.UTF8.GetBytes("FF Guardian signed update package test 10.0.2");
            string hash = Convert.ToHexString(SHA256.HashData(package));
            string installedPackage = Path.Combine(root, "FFGuardian-current.bin");
            byte[] installedBytes = Encoding.UTF8.GetBytes("FF Guardian installed package 10.0.1");
            await File.WriteAllBytesAsync(installedPackage, installedBytes);

            using RSA rsa = RSA.Create(2048);
            string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
            CountingHandler handler = new(package);
            using HttpClient client = new(handler);
            using SecureUpdater10 updater = new(publicKey, client);

            UpdateManifest10 manifest = SignManifest(
                rsa,
                version: "10.0.2",
                packageFileName: "FFGuardian-10.0.2.bin",
                hash,
                package.LongLength,
                minimumVersion: "10.0.0");

            UpdateDownloadRequest10 request = new(
                new Uri("https://updates.example.invalid/FFGuardian-10.0.2.bin"),
                new Version(10, 0, 1),
                staging,
                installedPackage,
                RequireAuthenticode: false);

            UpdateStageResult10 staged = await updater.DownloadAndStageAsync(manifest, request);
            Ensure(staged.Succeeded, staged.Status);
            Ensure(File.Exists(staged.StagedPackagePath), "Pacchetto verificato non presente nello staging.");
            Ensure(File.Exists(staged.PreviousVersionBackupPath), "Backup della versione precedente non creato.");
            Ensure((await File.ReadAllBytesAsync(staged.StagedPackagePath)).SequenceEqual(package),
                "Il pacchetto preparato non coincide con il download firmato.");
            Ensure((await File.ReadAllBytesAsync(staged.PreviousVersionBackupPath)).SequenceEqual(installedBytes),
                "Il backup della versione precedente non è esatto.");
            Ensure(staged.CalculatedSha256 == hash, "SHA-256 dello staging non corrispondente.");
            Ensure(handler.RequestCount == 1, "Numero inatteso di download HTTPS.");

            UpdateManifest10 downgradeManifest = SignManifest(
                rsa,
                version: "10.0.1",
                packageFileName: "FFGuardian-10.0.1.bin",
                hash,
                package.LongLength,
                minimumVersion: "10.0.0");
            UpdateStageResult10 downgrade = await updater.DownloadAndStageAsync(downgradeManifest, request);
            Ensure(!downgrade.Succeeded && downgrade.Status.Contains("downgrade", StringComparison.OrdinalIgnoreCase),
                "Il downgrade non è stato bloccato.");
            Ensure(handler.RequestCount == 1, "Il downgrade ha avviato un download non autorizzato.");

            UpdateManifest10 newerManifest = SignManifest(
                rsa,
                version: "10.0.3",
                packageFileName: "FFGuardian-10.0.3.bin",
                hash,
                package.LongLength,
                minimumVersion: "10.0.0");
            UpdateDownloadRequest10 insecureRequest = request with
            {
                PackageUri = new Uri("http://updates.example.invalid/FFGuardian-10.0.3.bin")
            };
            UpdateStageResult10 insecure = await updater.DownloadAndStageAsync(newerManifest, insecureRequest);
            Ensure(!insecure.Succeeded && insecure.Status.Contains("HTTPS", StringComparison.OrdinalIgnoreCase),
                "Un download HTTP non cifrato non è stato bloccato.");
            Ensure(handler.RequestCount == 1, "Il collegamento HTTP ha avviato una richiesta.");

            UpdateManifest10 tampered = newerManifest with { SignatureBase64 = Convert.ToBase64String(new byte[256]) };
            UpdateStageResult10 badSignature = await updater.DownloadAndStageAsync(tampered, request);
            Ensure(!badSignature.Succeeded && badSignature.Status.Contains("Firma", StringComparison.OrdinalIgnoreCase),
                "Un manifesto con firma errata non è stato respinto.");
            Ensure(handler.RequestCount == 1, "Il manifesto non valido ha avviato un download.");

            Console.WriteLine("FFGuardian.Engine10 secure update tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 secure update tests: FAILED");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static UpdateManifest10 SignManifest(
        RSA rsa,
        string version,
        string packageFileName,
        string sha256,
        long size,
        string minimumVersion)
    {
        UpdateManifest10 unsigned = new(
            version,
            "stable",
            packageFileName,
            sha256,
            size,
            minimumVersion,
            string.Empty);
        byte[] payload = Encoding.UTF8.GetBytes(SecureUpdater10.CanonicalPayload(unsigned));
        byte[] signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        public int RequestCount { get; private set; }

        public CountingHandler(byte[] payload) => _payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(_payload)
            };
            response.Content.Headers.ContentType = new("application/octet-stream");
            response.Content.Headers.ContentLength = _payload.LongLength;
            return Task.FromResult(response);
        }
    }
}
