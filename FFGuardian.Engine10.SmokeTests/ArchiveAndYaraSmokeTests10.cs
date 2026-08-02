using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using FFGuardian.Engine10;

internal static class ArchiveAndYaraSmokeTests10
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Archive-Yara-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using RSA rsa = RSA.Create(2048);
            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                rsa.ExportSubjectPublicKeyInfoPem(),
                Path.Combine(root, "quarantine"),
                Path.Combine(root, "rollback"));

            string eicarZip = Path.Combine(root, "eicar.zip");
            using (ZipArchive archive = ZipFile.Open(eicarZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("sample.txt");
                using StreamWriter writer = new(entry.Open(), Encoding.ASCII);
                writer.Write("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
            }

            FileScanResult10 eicarResult = engine.ScanFileAsync(eicarZip).GetAwaiter().GetResult();
            Ensure(eicarResult.Verdict == ThreatVerdict10.Malicious,
                $"EICAR nello ZIP non rilevato: {eicarResult.Verdict}.");
            Ensure(eicarResult.DetectionName == "Test.EICAR",
                "EICAR nello ZIP non usa il rilevamento previsto.");

            string scriptZip = Path.Combine(root, "script.zip");
            using (ZipArchive archive = ZipFile.Open(scriptZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("update.ps1");
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write("powershell -encodedcommand AAAA; Invoke-Expression; DownloadString('https://example.invalid')");
            }

            FileScanResult10 scriptResult = engine.ScanFileAsync(scriptZip).GetAwaiter().GetResult();
            Ensure(scriptResult.Verdict == ThreatVerdict10.Suspicious,
                $"Script sospetto nello ZIP non rilevato: {scriptResult.Verdict}.");

            string traversalZip = Path.Combine(root, "traversal.zip");
            using (ZipArchive archive = ZipFile.Open(traversalZip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../payload.cmd");
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write("echo safe smoke test");
            }

            FileScanResult10 traversalResult = engine.ScanFileAsync(traversalZip).GetAwaiter().GetResult();
            Ensure(traversalResult.Verdict == ThreatVerdict10.Suspicious,
                $"Attraversamento directory nello ZIP non rilevato: {traversalResult.Verdict}.");

            string harmless = Path.Combine(root, "harmless.txt");
            File.WriteAllText(harmless, "Documento normale senza indicatori di minaccia.");
            FileScanResult10 harmlessResult = engine.ScanFileAsync(harmless).GetAwaiter().GetResult();
            Ensure(harmlessResult.Verdict is ThreatVerdict10.Unknown or ThreatVerdict10.Clean,
                "Il motore YARA-style ha prodotto un falso positivo sul file innocuo.");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
