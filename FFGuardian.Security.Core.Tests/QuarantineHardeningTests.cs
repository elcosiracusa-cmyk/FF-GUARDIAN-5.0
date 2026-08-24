using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

internal static class QuarantineHardeningTests
{
    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("I test della quarantena protetta richiedono Windows DPAPI.");

        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-QuarantineHardening-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string data = Path.Combine(root, "Data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);

        try
        {
            ServiceCollection services = new();
            services.AddFFGuardianSecurityServices(options =>
            {
                options.BaseDirectory = app;
                options.DataDirectory = data;
            });
            services.AddUnifiedFFGuardianScanService();

            await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            IQuarantineService quarantine = provider.GetRequiredService<IQuarantineService>();
            IFileHashService hashes = provider.GetRequiredService<IFileHashService>();
            Assert(quarantine is HardenedQuarantineService, "hardened quarantine DI registration");

            string original = Path.Combine(root, "suspicious sample.exe");
            byte[] plaintext = Encoding.UTF8.GetBytes("FFGuardian encrypted quarantine fixture " + Guid.NewGuid().ToString("N"));
            await File.WriteAllBytesAsync(original, plaintext);

            QuarantineResult stored = await quarantine.QuarantineAsync(original, "Fixture", "Test.Rule", "High", CancellationToken.None);
            Assert(stored.Success && stored.Entry is not null, "encrypted quarantine store");
            QuarantineEntry entry = stored.Entry ?? throw new InvalidOperationException("Quarantine entry missing.");
            Assert(!File.Exists(original), "source removed after verified encrypted commit");
            Assert(File.Exists(entry.StoredPath), "encrypted payload exists");

            byte[] encryptedPayload = await File.ReadAllBytesAsync(entry.StoredPath);
            Assert(!encryptedPayload.AsSpan().SequenceEqual(plaintext), "payload differs from plaintext");
            Assert(encryptedPayload.Length > plaintext.Length, "encrypted container has authentication overhead");

            string quarantineRoot = Path.Combine(data, "Quarantine");
            string protectedKey = Path.Combine(quarantineRoot, "quarantine.key.dpapi");
            Assert(File.Exists(protectedKey), "DPAPI protected key exists");
            byte[] protectedKeyBytes = await File.ReadAllBytesAsync(protectedKey);
            Assert(protectedKeyBytes.Length != 64, "master key is not stored as raw 64-byte material");

            string metadata = Path.Combine(quarantineRoot, entry.Id.ToString("N") + ".json");
            byte[] metadataOriginal = await File.ReadAllBytesAsync(metadata);
            await File.AppendAllTextAsync(metadata, "TAMPER");
            bool metadataRejected = false;
            try
            {
                await quarantine.RestoreAsync(entry.Id, Path.Combine(root, "metadata-tamper-restore.bin"), false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException)
            {
                metadataRejected = true;
            }
            Assert(metadataRejected, "tampered metadata rejected");
            await File.WriteAllBytesAsync(metadata, metadataOriginal);

            string restoredPath = Path.Combine(root, "restored verified.bin");
            QuarantineResult restored = await quarantine.RestoreAsync(entry.Id, restoredPath, false, CancellationToken.None);
            Assert(restored.Success && File.Exists(restoredPath), "encrypted quarantine restore");
            Assert((await hashes.ComputeSha256Async(restoredPath, CancellationToken.None)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase), "restored SHA-256 verified");

            await using (FileStream tamper = new(entry.StoredPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                tamper.Position = Math.Min(24, tamper.Length - 1);
                int value = tamper.ReadByte();
                tamper.Position--;
                tamper.WriteByte((byte)(value ^ 0x5A));
                tamper.Flush(flushToDisk: true);
            }

            bool payloadRejected = false;
            try
            {
                await quarantine.RestoreAsync(entry.Id, Path.Combine(root, "payload-tamper-restore.bin"), false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is InvalidDataException or CryptographicException)
            {
                payloadRejected = true;
            }
            Assert(payloadRejected, "tampered encrypted payload rejected");

            string sentinel = Path.Combine(root, "must-survive.txt");
            await File.WriteAllTextAsync(sentinel, "do not delete");
            Guid forgedId = Guid.NewGuid();
            string forgedPayload = Path.Combine(quarantineRoot, forgedId.ToString("N") + ".qdat");
            string forgedMetadata = Path.Combine(quarantineRoot, forgedId.ToString("N") + ".json");
            await File.WriteAllTextAsync(forgedPayload, "legacy canonical payload");
            string forgedHash = await hashes.ComputeSha256Async(forgedPayload, CancellationToken.None);
            QuarantineEntry forged = new(
                forgedId,
                "forged.exe",
                Path.Combine(root, "forged.exe"),
                sentinel,
                forgedHash,
                new FileInfo(forgedPayload).Length,
                "Fixture",
                "Forged.StoredPath",
                DateTimeOffset.UtcNow,
                "High");
            await File.WriteAllTextAsync(forgedMetadata, JsonSerializer.Serialize(forged));

            bool forgedDeleted = await quarantine.DeleteAsync(forgedId, CancellationToken.None);
            Assert(forgedDeleted, "legacy forged record handled");
            Assert(File.Exists(sentinel), "metadata StoredPath cannot escape quarantine root");
            Assert(!File.Exists(forgedPayload), "canonical forged quarantine payload removed");

            Assert(await quarantine.DeleteAsync(entry.Id, CancellationToken.None), "tampered record delete remains root-confined");
            Assert(File.Exists(restoredPath), "delete quarantine does not delete restored file");

            Console.WriteLine("PASS quarantine hardening tests");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS " + name);
    }
}
