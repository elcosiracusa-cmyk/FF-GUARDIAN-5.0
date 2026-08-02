using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

internal static class BaselineSignatureSmokeTests10
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "FFGuardian-Baseline-Signatures-" + Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(root, "signatures.json");

        try
        {
            Directory.CreateDirectory(root);
            SignatureDatabase10 database = new(databasePath);

            Ensure(database.SignatureCount >= 1,
                "Il database firme iniziale non può essere vuoto.");
            Ensure(!database.Version.Contains("empty", StringComparison.OrdinalIgnoreCase),
                "FFGuardian non deve avviarsi con una versione firme vuota.");
            Ensure(File.Exists(databasePath),
                "Il database firme iniziale non è stato salvato su disco.");

            SignatureEntry10? eicar = database
                .FindSignatureAsync(BaselineSignatureCatalog10.EicarSha256)
                .GetAwaiter()
                .GetResult();

            Ensure(eicar is not null,
                "La baseline non contiene la firma hash EICAR prevista.");
            Ensure(eicar.Enabled,
                "La firma EICAR della baseline risulta disabilitata.");
            Ensure(string.Equals(eicar.DetectionName, "Test.EICAR", StringComparison.Ordinal),
                "La firma EICAR non usa il nome di rilevamento previsto.");
            Ensure(eicar.Confidence == 100,
                "La firma EICAR non usa confidenza 100.");
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
