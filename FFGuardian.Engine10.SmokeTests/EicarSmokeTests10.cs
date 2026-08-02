using System.Runtime.CompilerServices;
using System.Text;
using FFGuardian.Engine10;

internal static class EicarSmokeTests10
{
    private const string Eicar =
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-EICAR-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                updaterPublicKeyPem: null,
                Path.Combine(root, "quarantine"),
                Path.Combine(root, "rollback"));

            Verify(engine, Path.Combine(root, "eicar.com"));
            Verify(engine, Path.Combine(root, "eicar.com.txt"));
            Verify(engine, Path.Combine(root, "documento-rinominato.bin"));

            string harmless = Path.Combine(root, "harmless.txt");
            File.WriteAllText(harmless, "FF Guardian harmless EICAR control file", Encoding.UTF8);
            FileScanResult10 harmlessResult = engine.ScanFileAsync(harmless).GetAwaiter().GetResult();
            Ensure(harmlessResult.Verdict is not ThreatVerdict10.Malicious,
                "Il controllo innocuo non deve essere rilevato come EICAR.");
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

    private static void Verify(FFGuardianEngine10 engine, string path)
    {
        File.WriteAllText(path, Eicar, Encoding.ASCII);
        FileScanResult10 result = engine.ScanFileAsync(path).GetAwaiter().GetResult();

        Ensure(result.Verdict == ThreatVerdict10.Malicious,
            $"EICAR non rilevato in {Path.GetFileName(path)}: {result.Verdict}.");
        Ensure(string.Equals(result.DetectionName, "Test.EICAR", StringComparison.Ordinal),
            $"Nome rilevamento EICAR errato: {result.DetectionName}.");
        Ensure(result.Confidence == 100,
            $"Confidenza EICAR inattesa: {result.Confidence}.");
        Ensure(result.Sha256.Length == 64,
            "Lo SHA-256 del campione EICAR non è stato calcolato.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
