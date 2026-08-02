using System.Security.Cryptography;
using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Reputation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            FalsePositiveAssessment10 excessive = new(
                100,
                true,
                true,
                "Test Publisher",
                new[] { "Test" });
            Ensure(FalsePositiveGuard10.ApplyReduction(70, excessive) == 45,
                "La riduzione reputazionale deve essere limitata a 25 punti.");
            Ensure(FalsePositiveGuard10.ApplyReduction(10, excessive) == 0,
                "La riduzione non può produrre un rischio negativo.");

            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                updaterPublicKeyPem: null,
                Path.Combine(root, "Quarantine"),
                Path.Combine(root, "Rollback"));

            string harmless = Path.Combine(root, "document.txt");
            await File.WriteAllTextAsync(harmless, "Documento innocuo per test reputazione FFGuardian.");
            FileScanResult10 harmlessResult = await engine.ScanFileAsync(harmless);
            Ensure(harmlessResult.Verdict is ThreatVerdict10.Unknown or ThreatVerdict10.Clean,
                $"File innocuo classificato erroneamente: {harmlessResult.Verdict}.");

            string eicarPath = Path.Combine(root, "eicar.com");
            await File.WriteAllTextAsync(
                eicarPath,
                "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
            FileScanResult10 eicarResult = await engine.ScanFileAsync(eicarPath);
            Ensure(eicarResult.Verdict == ThreatVerdict10.Malicious,
                "La reputazione non deve annullare EICAR.");
            Ensure(eicarResult.DetectionName == "Test.EICAR",
                "EICAR deve mantenere il nome Test.EICAR.");

            string suspicious = Path.Combine(root, "invoice.pdf.exe");
            await File.WriteAllBytesAsync(suspicious, RandomNumberGenerator.GetBytes(8192));
            FileScanResult10 suspiciousResult = await engine.ScanFileAsync(suspicious);
            Ensure(suspiciousResult.Verdict == ThreatVerdict10.Suspicious,
                $"Il file artificiale sospetto è stato attenuato eccessivamente: {suspiciousResult.Verdict}.");

            Console.WriteLine("FFGuardian.Engine10 reputation tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 reputation tests: FAILED");
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
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
