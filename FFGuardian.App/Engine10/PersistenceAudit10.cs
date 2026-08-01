using Microsoft.Win32;

namespace FFGuardian.Engine10;

internal sealed class PersistenceAudit10
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path, string Scope)[] Locations =
    {
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", "Utente"),
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Utente una tantum"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Sistema 64 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "Sistema 32 bit")
    };

    public Task<IReadOnlyList<AuditFinding10>> AuditAsync(CancellationToken cancellationToken = default)
    {
        List<AuditFinding10> findings = [];
        foreach ((RegistryHive hive, RegistryView view, string path, string scope) in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? key = baseKey.OpenSubKey(path, writable: false);
                if (key is null) continue;

                foreach (string valueName in key.GetValueNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                    string executable = ExtractExecutablePath(command);
                    bool exists = !string.IsNullOrWhiteSpace(executable) && File.Exists(executable);
                    global::FFGuardian.AuthenticodeResult100 signature = exists
                        ? global::FFGuardian.AuthenticodeVerifier100.Verify(executable)
                        : new(false, false, string.Empty, "File non trovato", 0);

                    int score = 0;
                    List<string> evidence = [$"Origine: {scope}"];
                    if (!exists) { score += 20; evidence.Add("Percorso non esistente"); }
                    else if (!signature.IsSigned) { score += 10; evidence.Add("Firma digitale assente"); }
                    else if (!signature.IsTrusted) { score += 25; evidence.Add("Firma digitale non attendibile"); }
                    if (IsUserWritable(executable)) { score += 15; evidence.Add("Avvio da cartella modificabile dall’utente"); }

                    findings.Add(new AuditFinding10(
                        $"PERSIST-{hive}-{view}-{valueName}", "Persistenza", valueName, command,
                        Severity(score), Math.Clamp(score, 0, 100), string.Join("; ", evidence), string.Empty,
                        signature.Status, score >= 15));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                findings.Add(new AuditFinding10(
                    $"PERSIST-ERROR-{hive}-{view}", "Persistenza", scope, path,
                    AuditSeverity10.Low, 5, $"Area non leggibile: {ex.Message}", string.Empty,
                    "Non verificabile", false));
            }
        }

        return Task.FromResult<IReadOnlyList<AuditFinding10>>(findings);
    }

    private static string ExtractExecutablePath(string command)
    {
        string value = Environment.ExpandEnvironmentVariables(command.Trim());
        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : string.Empty;
        }
        int exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? value[..(exe + 4)].Trim() : value.Split(' ', 2)[0].Trim();
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
