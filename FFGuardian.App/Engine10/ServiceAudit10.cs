using Microsoft.Win32;
using System.ServiceProcess;

namespace FFGuardian.Engine10;

internal sealed class ServiceAudit10
{
    public Task<IReadOnlyList<AuditFinding10>> AuditAsync(CancellationToken cancellationToken = default)
    {
        List<AuditFinding10> findings = [];

        foreach (ServiceController service in ServiceController.GetServices())
        {
            using (service)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string serviceName = service.ServiceName;
                string displayName = string.IsNullOrWhiteSpace(service.DisplayName) ? serviceName : service.DisplayName;
                string registryPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";

                try
                {
                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryPath, writable: false);
                    string rawImagePath = key?.GetValue("ImagePath")?.ToString() ?? string.Empty;
                    string executablePath = ExtractExecutablePath(rawImagePath);
                    string account = key?.GetValue("ObjectName")?.ToString() ?? "Non specificato";
                    int startType = ConvertToInt32(key?.GetValue("Start"), -1);
                    bool exists = !string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath);
                    global::FFGuardian.AuthenticodeResult100 signature = exists
                        ? global::FFGuardian.AuthenticodeVerifier100.Verify(executablePath)
                        : new(false, false, string.Empty, "File non trovato", 0);

                    int score = 0;
                    List<string> evidence =
                    [
                        $"Stato: {service.Status}",
                        $"Tipo: {service.ServiceType}",
                        $"Avvio: {DescribeStartType(startType)}",
                        $"Account: {account}"
                    ];

                    if (string.IsNullOrWhiteSpace(rawImagePath))
                    {
                        score += 15;
                        evidence.Add("Percorso binario mancante");
                    }
                    else if (!exists)
                    {
                        score += 25;
                        evidence.Add("File eseguibile del servizio non trovato");
                    }
                    else if (!signature.IsSigned)
                    {
                        score += 10;
                        evidence.Add("Eseguibile senza firma digitale");
                    }
                    else if (!signature.IsTrusted)
                    {
                        score += 25;
                        evidence.Add("Firma digitale non attendibile");
                    }

                    if (IsUserWritable(executablePath))
                    {
                        score += 25;
                        evidence.Add("Eseguibile collocato in una cartella modificabile dall'utente");
                    }

                    if (startType is 0 or 1 && !signature.IsTrusted && exists)
                    {
                        score += 20;
                        evidence.Add("Driver o servizio di avvio precoce non attendibile");
                    }

                    findings.Add(new AuditFinding10(
                        $"SERVICE-{serviceName}",
                        "Servizi",
                        displayName,
                        string.IsNullOrWhiteSpace(rawImagePath) ? serviceName : rawImagePath,
                        Severity(score),
                        Math.Clamp(score, 0, 100),
                        string.Join("; ", evidence),
                        string.Empty,
                        signature.Status,
                        score >= 15));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException or InvalidOperationException)
                {
                    findings.Add(new AuditFinding10(
                        $"SERVICE-ERROR-{serviceName}",
                        "Servizi",
                        displayName,
                        serviceName,
                        AuditSeverity10.Low,
                        5,
                        $"Servizio non analizzabile completamente: {ex.Message}",
                        string.Empty,
                        "Non verificabile",
                        false));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<AuditFinding10>>(findings);
    }

    private static int ConvertToInt32(object? value, int fallback)
    {
        try { return value is null ? fallback : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    private static string DescribeStartType(int value) => value switch
    {
        0 => "Boot",
        1 => "System",
        2 => "Automatico",
        3 => "Manuale",
        4 => "Disabilitato",
        _ => "Sconosciuto"
    };

    private static string ExtractExecutablePath(string command)
    {
        string value = Environment.ExpandEnvironmentVariables(command.Trim());
        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : string.Empty;
        }

        int exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        int sys = value.IndexOf(".sys", StringComparison.OrdinalIgnoreCase);
        int endIndex = exe >= 0 ? exe + 4 : sys >= 0 ? sys + 4 : -1;
        return endIndex > 0 ? value[..endIndex].Trim() : value.Split(' ', 2)[0].Trim();
    }

    private static bool IsUserWritable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full;
        try { full = Path.GetFullPath(path); } catch { return false; }

        string[] roots =
        {
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        return roots.Any(root => !string.IsNullOrWhiteSpace(root) && full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
    }

    private static AuditSeverity10 Severity(int score) => score switch
    {
        >= 50 => AuditSeverity10.Critical,
        >= 30 => AuditSeverity10.High,
        >= 15 => AuditSeverity10.Medium,
        >= 5 => AuditSeverity10.Low,
        _ => AuditSeverity10.Informational
    };
}
