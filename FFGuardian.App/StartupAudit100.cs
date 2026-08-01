using Microsoft.Win32;

namespace FFGuardian;

internal sealed record StartupEntry100(
    string Source,
    string Name,
    string Command,
    string ExecutablePath,
    string SignatureStatus,
    bool SignatureTrusted,
    int RiskScore,
    string Evidence);

internal static class StartupAudit100
{
    public static IReadOnlyList<StartupEntry100> Collect(CancellationToken cancellationToken)
    {
        List<StartupEntry100> entries = [];
        CollectRegistry(entries, RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registro utente", cancellationToken);
        CollectRegistry(entries, RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Registro utente RunOnce", cancellationToken);
        CollectRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registro sistema 64 bit", cancellationToken);
        CollectRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Registro sistema RunOnce 64 bit", cancellationToken);
        CollectRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registro sistema 32 bit", cancellationToken);
        CollectRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Registro sistema RunOnce 32 bit", cancellationToken);

        CollectStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Cartella avvio utente", cancellationToken);
        CollectStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Cartella avvio comune", cancellationToken);

        return entries.OrderByDescending(item => item.RiskScore).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectRegistry(
        ICollection<StartupEntry100> target,
        RegistryHive hive,
        RegistryView view,
        string path,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? key = baseKey.OpenSubKey(path);
            if (key is null)
                return;

            foreach (string valueName in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                target.Add(Evaluate(source, string.IsNullOrWhiteSpace(valueName) ? "(Predefinito)" : valueName, command));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void CollectStartupFolder(
        ICollection<StartupEntry100> target,
        string folder,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return;
        }

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            target.Add(Evaluate(source, Path.GetFileName(file), file));
        }
    }

    private static StartupEntry100 Evaluate(string source, string name, string command)
    {
        string executable = ExtractExecutablePath(command);
        List<string> evidence = [];
        int score = 0;
        AuthenticodeResult100 signature = new(false, false, string.Empty, "Non verificabile", 0);

        if (string.IsNullOrWhiteSpace(executable))
        {
            score += 15;
            evidence.Add("Percorso eseguibile non identificato");
        }
        else if (!File.Exists(executable))
        {
            score += 15;
            evidence.Add("Percorso di avvio non esistente");
        }
        else
        {
            signature = AuthenticodeVerifier100.Verify(executable);
            if (!signature.IsSigned)
            {
                score += 8;
                evidence.Add("Firma digitale assente");
            }
            else if (!signature.IsTrusted)
            {
                score += 22;
                evidence.Add("Firma digitale non attendibile");
            }

            if (IsUserWritable(executable))
            {
                score += 10;
                evidence.Add("Avvio da cartella modificabile dall’utente");
            }
        }

        return new StartupEntry100(
            source,
            name,
            command,
            executable,
            signature.Status,
            signature.IsTrusted,
            score,
            evidence.Count == 0 ? "Voce di avvio registrata; nessuna anomalia evidente" : string.Join("; ", evidence));
    }

    private static string ExtractExecutablePath(string command)
    {
        string value = Environment.ExpandEnvironmentVariables(command.Trim());
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : string.Empty;
        }

        string[] knownExtensions = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".hta", ".lnk"];
        foreach (string extension in knownExtensions)
        {
            int index = value.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return value[..(index + extension.Length)].Trim();
        }

        return value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private static bool IsUserWritable(string path)
    {
        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        string[] folders =
        {
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        return folders.Any(folder => !string.IsNullOrWhiteSpace(folder) && normalized.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase));
    }
}
