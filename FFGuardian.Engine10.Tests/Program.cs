using System.Security.Cryptography;
using System.Text.Json;
using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Engine10-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await TestSignatureAndAllowListAsync(root);
            await TestQuarantineAndRestoreAsync(root);
            await TestRollbackAsync(root);
            await TestInvalidUpdateIsRejectedAsync(root);
            TestAuthenticodeMissingFile();

            Console.WriteLine("ENGINE10_SMOKE_TESTS_PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ENGINE10_SMOKE_TESTS_FAILED");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task TestSignatureAndAllowListAsync(string root)
    {
        string maliciousPath = Path.Combine(root, "known-test.exe");
        string trustedPath = Path.Combine(root, "trusted-test.exe");
        await File.WriteAllTextAsync(maliciousPath, "harmless signature test payload");
        await File.WriteAllTextAsync(trustedPath, "harmless allowlist test payload");

        string maliciousHash = await HashAsync(maliciousPath);
        string trustedHash = await HashAsync(trustedPath);
        string databasePath = Path.Combine(root, "signatures.json");

        SignatureDatabaseDocument10 document = new(
            1,
            "10-test",
            DateTime.UtcNow,
            new[]
            {
                new SignatureEntry10("TEST-0001", maliciousHash, "Test.Known.File", 10, 100, true)
            },
            new[] { trustedHash });

        await File.WriteAllTextAsync(databasePath, JsonSerializer.Serialize(document));
        SignatureDatabase10 database = new(databasePath);
        IndependentScanner10 scanner = new(database);

        FileScanResult10 malicious = await scanner.ScanFileAsync(maliciousPath);
        Assert(malicious.Verdict == ThreatVerdict10.Malicious, "La firma nota non è stata rilevata.");
        Assert(malicious.Confidence == 100, "La confidenza della firma nota non è corretta.");

        FileScanResult10 trusted = await scanner.ScanFileAsync(trustedPath);
        Assert(trusted.Verdict == ThreatVerdict10.Clean, "La allowlist non è stata rispettata.");
    }

    private static async Task TestQuarantineAndRestoreAsync(string root)
    {
        string quarantineRoot = Path.Combine(root, "quarantine");
        string filePath = Path.Combine(root, "quarantine-test.exe");
        await File.WriteAllTextAsync(filePath, "harmless quarantine test payload");
        string hash = await HashAsync(filePath);

        FileScanResult10 result = new(
            filePath,
            hash,
            new FileInfo(filePath).Length,
            ThreatVerdict10.Suspicious,
            80,
            "Test.Suspicious.File",
            new[] { "Test controllato" },
            DateTime.UtcNow);

        QuarantineStore10 store = new(quarantineRoot);
        QuarantineRecord10 record = await store.QuarantineAsync(result);
        Assert(!File.Exists(filePath), "Il file è rimasto nel percorso originale dopo la quarantena.");
        Assert(File.Exists(record.StoredPath), "Il contenuto non è presente nella quarantena.");

        await store.RestoreAsync(record.Id);
        Assert(File.Exists(filePath), "Il file non è stato ripristinato dalla quarantena.");
    }

    private static async Task TestRollbackAsync(string root)
    {
        string rollbackRoot = Path.Combine(root, "rollback");
        string filePath = Path.Combine(root, "rollback-test.txt");
        await File.WriteAllTextAsync(filePath, "original");

        RollbackManager10 manager = new(rollbackRoot);
        RollbackRecord10 record = await manager.BackupFileAsync(filePath, "TestBackup");
        await File.WriteAllTextAsync(filePath, "modified");
        await manager.RestoreFileAsync(record);

        string restored = await File.ReadAllTextAsync(filePath);
        Assert(restored == "original", "Il rollback non ha ripristinato il contenuto originale.");
    }

    private static async Task TestInvalidUpdateIsRejectedAsync(string root)
    {
        string packagePath = Path.Combine(root, "update.bin");
        await File.WriteAllTextAsync(packagePath, "harmless update test payload");
        string hash = await HashAsync(packagePath);

        using RSA rsa = RSA.Create();
        rsa.KeySize = 2048;
        string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        SecureUpdater10 updater = new(publicKey);
        UpdateManifest10 manifest = new(
            "10.0.2",
            "test",
            Path.GetFileName(packagePath),
            hash,
            new FileInfo(packagePath).Length,
            "10.0.1",
            Convert.ToBase64String(new byte[256]));

        UpdateVerificationResult10 verification = await updater.VerifyPackageAsync(manifest, packagePath);
        Assert(!verification.IsValid, "Un aggiornamento con firma non valida è stato accettato.");
    }

    private static void TestAuthenticodeMissingFile()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        global::FFGuardian.AuthenticodeResult100 result = global::FFGuardian.AuthenticodeVerifier100.Verify(missing);
        Assert(!result.IsTrusted, "Un file inesistente è stato considerato attendibile.");
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
