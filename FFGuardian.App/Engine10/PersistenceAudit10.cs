using Microsoft.Win32;

namespace FFGuardian.Engine10;

internal sealed class PersistenceAudit10
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path, string Scope)[] ValueLocations =
    {
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run utente"),
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce utente"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run sistema 64 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run sistema 32 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce sistema 64 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce sistema 32 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Winlogon 64 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Winlogon 32 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit 64 bit"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit 32 bit")
    };

    private static readonly HashSet<string> WinlogonValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell", "Userinit", "Taskman", "AppSetup"
    };

    private static readonly HashSet<string> AppInitValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppInit_DLLs", "LoadAppInit_DLLs"
    };

    public Task<IReadOnlyList<AuditFinding10>> AuditAsync(CancellationToken cancellationToken = default)
    {
        List<AuditFinding10> findings = [];
        AuditRegistryValues(findings, cancellationToken);
        AuditStartupFolders(findings, cancellationToken);
        AuditImageFileExecutionOptions(findings, cancellationToken);
        return Task.FromResult<IReadOnlyList<AuditFinding10>>(findings);
    }

    private static void AuditRegistryValues(List<AuditFinding10> findings, CancellationToken cancellationToken)
    {
        foreach ((RegistryHive hive, RegistryView view, string path, string scope) in ValueLocations)
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
                    if (path.EndsWith("Winlogon", StringComparison.OrdinalIgnoreCase) && !WinlogonValues.Contains(valueName))
                        continue;
                    if (path.EndsWith("Windows", StringComparison.OrdinalIgnoreCase) && !AppInitValues.Contains(valueName))
                        continue;

                    string command = key.GetValue(valueName, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
                    if (valueName.Equals("LoadAppInit_DLLs", StringComparison.OrdinalIgnoreCase) && command is "0" or "")
                        continue;

                    AddFinding(findings, $"PERSIST-{hive}-{view}-{Sanitize(valueName)}", scope, valueName, command);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                findings.Add(new AuditFinding10(
                    $"PERSIST-ERROR-{hive}-{view}-{Sanitize(scope)}", "Persistenza", scope, path,
                    AuditSeverity10.Low, 5, $"Area non leggibile: {ex.Message}", string.Empty,
                    "Non verificabile", false));
            }
        }
    }

    private static void AuditStartupFolders(List<AuditFinding10> findings, CancellationToken cancellationToken)
    {
        (string Path, string Scope)[] folders =
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup utente"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup comune")
        };

        foreach ((string folder, string scope) in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;

            try
            {
                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddFinding(findings, $"STARTUP-{Sanitize(scope)}-{Sanitize(Path.GetFileName(file))}", scope,
                        Path.GetFileName(file), file);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                findings.Add(new AuditFinding10(
                    $"STARTUP-ERROR-{Sanitize(scope)}", "Persistenza", scope, folder,
                    AuditSeverity10.Low, 5, $"Cartella non leggibile: {ex.Message}", string.Empty,
                    "Non verificabile", false));
            }
        }
    }

    private static void AuditImageFileExecutionOptions(List<AuditFinding10> findings, CancellationToken cancellationToken)
    {
        const string path = @"Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? root = baseKey.OpenSubKey(path, writable: false);
            if (root is null) return;

            foreach (string subKeyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using RegistryKey? subKey = root.OpenSubKey(subKeyName, writable: false);
                string debugger = subKey?.GetValue("Debugger")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(debugger)) continue;

                AddFinding(findings, $"IFEO-{Sanitize(subKeyName)}", "IFEO Debugger", subKeyName, debugger, additionalRisk: 30,
                    additionalEvidence: "Debugger IFEO configurato");
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            findings.Add(new AuditFinding10(
                "IFEO-ERROR", "Persistenza", "Image File Execution Options", path,
                AuditSeverity10.Low, 5, $"Area non leggibile: {ex.Message}", string.Empty,
                "Non verificabile", false));
        }
    }

    private static void AddFinding(
        List<AuditFinding10> findings,
        string id,
        string scope,
        string name,
        string command,
        int additionalRisk = 0,
        string? additionalEvidence = null)
    {
        AuditTargetInspection10 inspection = AuditTargetInspector10.Inspect(command);
        int score = Math.Clamp(inspection.RiskScore + additionalRisk, 0, 100);
        List<string> evidence = [$"Origine: {scope}"];
        evidence.AddRange(inspection.Evidence);
        if (!string.IsNullOrWhiteSpace(additionalEvidence)) evidence.Add(additionalEvidence);

        findings.Add(new AuditFinding10(
            id, "Persistenza", name, command,
            AuditTargetInspector10.Severity(score), score, string.Join("; ", evidence),
            inspection.Sha256, inspection.SignatureStatus, score >= 15));
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}
