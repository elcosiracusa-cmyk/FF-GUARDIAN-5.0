using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;

namespace FFGuardian;

internal enum IndependentRiskLevel
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

internal sealed record IndependentFinding(
    string Category,
    string Name,
    string Target,
    IndependentRiskLevel Risk,
    int Score,
    string Evidence,
    string Sha256,
    string SignatureStatus);

internal sealed record IndependentAuditResult(
    DateTime StartedAt,
    DateTime CompletedAt,
    int SecurityScore,
    IReadOnlyList<IndependentFinding> Findings,
    int FilesExamined,
    int StartupEntries,
    int ServicesExamined,
    int ScheduledTasksExamined);

internal sealed class IndependentSecurityEngine100
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".scr", ".msi", ".ps1", ".bat", ".cmd", ".vbs", ".js", ".hta"
    };

    private static readonly string[] SuspiciousWritableFolders =
    {
        Path.GetTempPath(),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + Path.DirectorySeparatorChar + "Downloads"
    };

    public async Task<IndependentAuditResult> RunAuditAsync(
        string? scanRoot,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        DateTime started = DateTime.Now;
        List<IndependentFinding> findings = [];
        int filesExamined = 0;
        int startupEntries = 0;
        int servicesExamined = 0;
        int scheduledTasksExamined = 0;

        progress?.Report("Analisi delle esecuzioni automatiche…");
        foreach (IndependentFinding finding in AuditStartupEntries(cancellationToken))
        {
            findings.Add(finding);
            startupEntries++;
        }

        progress?.Report("Analisi dei servizi Windows…");
        foreach (IndependentFinding finding in AuditServices(cancellationToken))
        {
            findings.Add(finding);
            servicesExamined++;
        }

        progress?.Report("Analisi delle attività pianificate…");
        IReadOnlyList<IndependentFinding> taskFindings = await AuditScheduledTasksAsync(cancellationToken);
        findings.AddRange(taskFindings);
        scheduledTasksExamined = taskFindings.Count;

        if (!string.IsNullOrWhiteSpace(scanRoot) && Directory.Exists(scanRoot))
        {
            progress?.Report("Scansione statica dei file…");
            await foreach (string file in EnumerateFilesSafeAsync(scanRoot, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                filesExamined++;

                if (!ExecutableExtensions.Contains(Path.GetExtension(file)))
                    continue;

                IndependentFinding? finding = await AnalyzeFileAsync(file, cancellationToken);
                if (finding is not null)
                    findings.Add(finding);

                if (filesExamined % 100 == 0)
                    progress?.Report($"File esaminati: {filesExamined:N0}");
            }
        }

        int penalty = findings.Sum(f => Math.Clamp(f.Score, 0, 100));
        int securityScore = Math.Clamp(100 - Math.Min(100, penalty), 0, 100);

        return new IndependentAuditResult(
            started,
            DateTime.Now,
            securityScore,
            findings.OrderByDescending(f => f.Score).ToArray(),
            filesExamined,
            startupEntries,
            servicesExamined,
            scheduledTasksExamined);
    }

    public async Task<IndependentFinding?> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return null;

        FileInfo info = new(filePath);
        if (info.Length == 0)
        {
            return new IndependentFinding(
                "File",
                info.Name,
                filePath,
                IndependentRiskLevel.Medium,
                12,
                "File eseguibile vuoto o incompleto.",
                string.Empty,
                "Non verificabile");
        }

        string sha256 = await ComputeSha256Async(filePath, cancellationToken);
        SignatureCheck signature = CheckSignature(filePath);
        int score = 0;
        List<string> evidence = [];

        string extension = info.Extension;
        string lowerPath = filePath.ToLowerInvariant();

        if (!signature.IsSigned)
        {
            score += 8;
            evidence.Add("Firma digitale assente");
        }
        else if (!signature.IsValid)
        {
            score += 25;
            evidence.Add("Firma digitale non valida");
        }

        if (SuspiciousWritableFolders.Any(folder =>
                !string.IsNullOrWhiteSpace(folder) &&
                lowerPath.StartsWith(Path.GetFullPath(folder).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
        {
            score += 8;
            evidence.Add("Eseguibile collocato in una cartella modificabile dall’utente");
        }

        if (HasDoubleExtension(info.Name))
        {
            score += 20;
            evidence.Add("Nome con doppia estensione potenzialmente ingannevole");
        }

        if (extension.Equals(".scr", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".hta", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vbs", StringComparison.OrdinalIgnoreCase))
        {
            score += 12;
            evidence.Add($"Tipo di file ad alto rischio: {extension}");
        }

        if (info.Length > 250L * 1024 * 1024)
        {
            score += 3;
            evidence.Add("Dimensione anomala per una scansione statica rapida");
        }

        if (score == 0)
            return null;

        IndependentRiskLevel risk = score switch
        {
            >= 50 => IndependentRiskLevel.Critical,
            >= 30 => IndependentRiskLevel.High,
            >= 15 => IndependentRiskLevel.Medium,
            >= 5 => IndependentRiskLevel.Low,
            _ => IndependentRiskLevel.Informational
        };

        return new IndependentFinding(
            "File",
            info.Name,
            filePath,
            risk,
            score,
            string.Join("; ", evidence),
            sha256,
            signature.Description);
    }

    private static IEnumerable<IndependentFinding> AuditStartupEntries(CancellationToken cancellationToken)
    {
        (RegistryHive Hive, string Path, string Scope)[] locations =
        {
            (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Utente"),
            (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Utente una tantum"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Sistema"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Sistema una tantum")
        };

        foreach ((RegistryHive hive, string path, string scope) in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(path);
            if (key is null)
                continue;

            foreach (string valueName in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                string executable = ExtractExecutablePath(command);
                SignatureCheck signature = string.IsNullOrWhiteSpace(executable) ? SignatureCheck.Unknown : CheckSignature(executable);
                int score = 0;
                List<string> evidence = [];

                if (!string.IsNullOrWhiteSpace(executable) && !File.Exists(executable))
                {
                    score += 12;
                    evidence.Add("Percorso di avvio non esistente");
                }

                if (!signature.IsSigned && File.Exists(executable))
                {
                    score += 8;
                    evidence.Add("Programma di avvio senza firma digitale");
                }

                if (IsUserWritablePath(executable))
                {
                    score += 10;
                    evidence.Add("Avvio da cartella modificabile dall’utente");
                }

                yield return new IndependentFinding(
                    "Avvio automatico",
                    string.IsNullOrWhiteSpace(valueName) ? "(Predefinito)" : valueName,
                    command,
                    RiskFromScore(score),
                    score,
                    evidence.Count == 0 ? $"Voce di avvio rilevata: {scope}" : string.Join("; ", evidence),
                    string.Empty,
                    signature.Description);
            }
        }
    }

    private static IEnumerable<IndependentFinding> AuditServices(CancellationToken cancellationToken)
    {
        foreach (ServiceController service in ServiceController.GetServices())
        {
            using (service)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int score = 0;
                string evidence = "Servizio Windows rilevato";

                if (string.IsNullOrWhiteSpace(service.ServiceName))
                {
                    score = 5;
                    evidence = "Servizio con identificativo non valido";
                }

                yield return new IndependentFinding(
                    "Servizio",
                    service.DisplayName,
                    service.ServiceName,
                    RiskFromScore(score),
                    score,
                    evidence,
                    string.Empty,
                    "Percorso non ancora verificato");
            }
        }
    }

    private static async Task<IReadOnlyList<IndependentFinding>> AuditScheduledTasksAsync(CancellationToken cancellationToken)
    {
        List<IndependentFinding> findings = [];
        ProcessStartInfo startInfo = new("schtasks.exe", "/Query /FO CSV /NH")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            return findings;

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            findings.Add(new IndependentFinding(
                "Attività pianificate",
                "Analisi non completata",
                "schtasks.exe",
                IndependentRiskLevel.Low,
                3,
                string.IsNullOrWhiteSpace(error) ? "Comando non disponibile" : error.Trim(),
                string.Empty,
                "Non applicabile"));
            return findings;
        }

        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string taskName = ParseFirstCsvField(line);
            if (string.IsNullOrWhiteSpace(taskName))
                continue;

            findings.Add(new IndependentFinding(
                "Attività pianificata",
                taskName,
                taskName,
                IndependentRiskLevel.Informational,
                0,
                "Attività registrata nel sistema",
                string.Empty,
                "Azione non ancora verificata"));
        }

        return findings;
    }

    private static async IAsyncEnumerable<string> EnumerateFilesSafeAsync(
        string root,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = pending.Pop();

            string[] files;
            try { files = Directory.GetFiles(current); }
            catch { continue; }

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
                await Task.Yield();
            }

            string[] directories;
            try { directories = Directory.GetDirectories(current); }
            catch { continue; }

            foreach (string directory in directories)
                pending.Push(directory);
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static SignatureCheck CheckSignature(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return SignatureCheck.Unknown;

        try
        {
            X509Certificate certificate = X509Certificate.CreateFromSignedFile(filePath);
            using X509Certificate2 certificate2 = new(certificate);
            using X509Chain chain = new();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
            bool valid = chain.Build(certificate2);
            return new SignatureCheck(true, valid, valid
                ? $"Firma valida: {certificate2.GetNameInfo(X509NameType.SimpleName, false)}"
                : "Firma presente ma catena non valida");
        }
        catch (CryptographicException)
        {
            return new SignatureCheck(false, false, "Firma digitale assente");
        }
        catch
        {
            return new SignatureCheck(false, false, "Firma non verificabile");
        }
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

    private static bool HasDoubleExtension(string name)
    {
        string withoutLast = Path.GetFileNameWithoutExtension(name);
        return ExecutableExtensions.Contains(Path.GetExtension(withoutLast));
    }

    private static bool IsUserWritablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return false; }

        return SuspiciousWritableFolders.Any(folder =>
            !string.IsNullOrWhiteSpace(folder) &&
            normalized.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase));
    }

    private static IndependentRiskLevel RiskFromScore(int score) => score switch
    {
        >= 50 => IndependentRiskLevel.Critical,
        >= 30 => IndependentRiskLevel.High,
        >= 15 => IndependentRiskLevel.Medium,
        >= 5 => IndependentRiskLevel.Low,
        _ => IndependentRiskLevel.Informational
    };

    private static string ParseFirstCsvField(string line)
    {
        if (!line.StartsWith('"'))
            return line.Split(',', 2)[0].Trim();

        int end = line.IndexOf('"', 1);
        return end > 1 ? line[1..end].Replace("\"\"", "\"") : string.Empty;
    }

    private sealed record SignatureCheck(bool IsSigned, bool IsValid, string Description)
    {
        public static SignatureCheck Unknown { get; } = new(false, false, "Non verificabile");
    }
}
