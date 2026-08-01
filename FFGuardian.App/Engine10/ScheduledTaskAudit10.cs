using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace FFGuardian.Engine10;

internal sealed class ScheduledTaskAudit10
{
    public async Task<IReadOnlyList<AuditFinding10>> AuditAsync(CancellationToken cancellationToken = default)
    {
        List<AuditFinding10> findings = [];
        ProcessStartInfo startInfo = new("schtasks.exe", "/Query /V /FO CSV /NH")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.Unicode,
            StandardErrorEncoding = Encoding.Unicode
        };

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            return findings;

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            findings.Add(new AuditFinding10(
                "TASK-QUERY-ERROR", "Attività pianificate", "Analisi non completata", "schtasks.exe",
                AuditSeverity10.Low, 5,
                string.IsNullOrWhiteSpace(error) ? "Impossibile leggere le attività pianificate." : error.Trim(),
                string.Empty, "Non applicabile", false));
            return findings;
        }

        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> fields = ParseCsv(line);
            if (fields.Count == 0) continue;

            string taskName = fields.ElementAtOrDefault(1) ?? fields[0];
            string status = fields.ElementAtOrDefault(3) ?? "Sconosciuto";
            string runAs = fields.ElementAtOrDefault(7) ?? "Non specificato";
            string command = fields.ElementAtOrDefault(8) ?? string.Empty;
            string schedule = fields.ElementAtOrDefault(17) ?? string.Empty;
            string executable = ExtractExecutablePath(command);
            bool exists = !string.IsNullOrWhiteSpace(executable) && File.Exists(executable);
            global::FFGuardian.AuthenticodeResult100 signature = exists
                ? global::FFGuardian.AuthenticodeVerifier100.Verify(executable)
                : new(false, false, string.Empty, "File non trovato", 0);

            int score = 0;
            List<string> evidence = [$"Stato: {status}", $"Eseguita come: {runAs}"];
            if (!string.IsNullOrWhiteSpace(schedule)) evidence.Add($"Pianificazione: {schedule}");

            if (string.IsNullOrWhiteSpace(command))
            {
                score += 10;
                evidence.Add("Comando non leggibile");
            }
            else if (!exists)
            {
                score += 20;
                evidence.Add("Programma richiamato non trovato");
            }
            else if (!signature.IsSigned)
            {
                score += 10;
                evidence.Add("Programma senza firma digitale");
            }
            else if (!signature.IsTrusted)
            {
                score += 25;
                evidence.Add("Firma digitale non attendibile");
            }

            if (IsScriptCommand(command))
            {
                score += 15;
                evidence.Add("Attività basata su script o interprete");
            }

            if (IsUserWritable(executable))
            {
                score += 20;
                evidence.Add("Comando collocato in cartella modificabile dall'utente");
            }

            if (runAs.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) && score > 0)
            {
                score += 15;
                evidence.Add("Attività sospetta eseguita con privilegi SYSTEM");
            }

            findings.Add(new AuditFinding10(
                $"TASK-{SanitizeId(taskName)}",
                "Attività pianificate",
                string.IsNullOrWhiteSpace(taskName) ? "Attività senza nome" : taskName,
                string.IsNullOrWhiteSpace(command) ? taskName : command,
                Severity(score),
                Math.Clamp(score, 0, 100),
                string.Join("; ", evidence),
                string.Empty,
                signature.Status,
                score >= 15));
        }

        return findings;
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        List<string> fields = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string ExtractExecutablePath(string command)
    {
        string value = Environment.ExpandEnvironmentVariables(command.Trim());
        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : string.Empty;
        }

        foreach (string extension in new[] { ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".hta" })
        {
            int index = value.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return value[..(index + extension.Length)].Trim();
        }

        return value.Split(' ', 2)[0].Trim();
    }

    private static bool IsScriptCommand(string command) =>
        command.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
        command.Contains("pwsh", StringComparison.OrdinalIgnoreCase) ||
        command.Contains("wscript", StringComparison.OrdinalIgnoreCase) ||
        command.Contains("cscript", StringComparison.OrdinalIgnoreCase) ||
        command.Contains("mshta", StringComparison.OrdinalIgnoreCase) ||
        command.Contains(".ps1", StringComparison.OrdinalIgnoreCase) ||
        command.Contains(".vbs", StringComparison.OrdinalIgnoreCase) ||
        command.Contains(".js", StringComparison.OrdinalIgnoreCase);

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

    private static string SanitizeId(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
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
