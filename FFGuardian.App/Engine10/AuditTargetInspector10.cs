using System.Security.Cryptography;

namespace FFGuardian.Engine10;

internal sealed record AuditTargetInspection10(
    string ExecutablePath,
    bool Exists,
    string Sha256,
    string SignatureStatus,
    int RiskScore,
    IReadOnlyList<string> Evidence);

internal static class AuditTargetInspector10
{
    private static readonly string[] ScriptIndicators =
    {
        "powershell", "pwsh", "wscript", "cscript", "mshta", "rundll32", "regsvr32",
        "cmd.exe /c", ".ps1", ".vbs", ".js", ".hta", "-encodedcommand", " -enc "
    };

    public static AuditTargetInspection10 Inspect(string command)
    {
        string executable = ExtractExecutablePath(command);
        bool exists = !string.IsNullOrWhiteSpace(executable) && File.Exists(executable);
        List<string> evidence = [];
        int risk = 0;
        string sha256 = string.Empty;
        string signatureStatus = "Non verificabile";

        if (string.IsNullOrWhiteSpace(command))
        {
            risk += 15;
            evidence.Add("Comando vuoto o non leggibile");
        }
        else if (!exists)
        {
            risk += 20;
            evidence.Add("File richiamato non trovato");
        }
        else
        {
            sha256 = ComputeSha256(executable);
            global::FFGuardian.AuthenticodeResult100 signature =
                global::FFGuardian.AuthenticodeVerifier100.Verify(executable);
            signatureStatus = signature.Status;
            if (!signature.IsSigned)
            {
                risk += 10;
                evidence.Add("Firma digitale assente");
            }
            else if (!signature.IsTrusted)
            {
                risk += 25;
                evidence.Add("Firma digitale non attendibile");
            }
        }

        if (IsUserWritable(executable))
        {
            risk += 20;
            evidence.Add("Percorso modificabile dall'utente");
        }

        if (ScriptIndicators.Any(indicator => command.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
        {
            risk += 15;
            evidence.Add("Comando basato su interprete, script o loader di sistema");
        }

        return new AuditTargetInspection10(
            executable, exists, sha256, signatureStatus,
            Math.Clamp(risk, 0, 100), evidence);
    }

    internal static string ExtractExecutablePath(string command)
    {
        string value = Environment.ExpandEnvironmentVariables((command ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : string.Empty;
        }

        foreach (string extension in new[] { ".exe", ".com", ".sys", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".hta", ".wsf" })
        {
            int index = value.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return value[..(index + extension.Length)].Trim();
        }

        return value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    internal static bool IsUserWritable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        string[] roots =
        {
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        return roots.Any(root =>
            !string.IsNullOrWhiteSpace(root) &&
            full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
    }

    internal static AuditSeverity10 Severity(int score) => score switch
    {
        >= 50 => AuditSeverity10.Critical,
        >= 30 => AuditSeverity10.High,
        >= 15 => AuditSeverity10.Medium,
        >= 5 => AuditSeverity10.Low,
        _ => AuditSeverity10.Informational
    };

    private static string ComputeSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using SHA256 sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream));
    }
}
