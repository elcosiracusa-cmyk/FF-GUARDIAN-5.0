using System.Diagnostics;

namespace FFGuardian.Engine10;

internal sealed class ScheduledTaskAudit10
{
    public async Task<IReadOnlyList<AuditFinding10>> AuditAsync(CancellationToken cancellationToken = default)
    {
        List<AuditFinding10> findings = [];
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
            string name = ParseFirstCsvField(line);
            if (string.IsNullOrWhiteSpace(name)) continue;
            findings.Add(new AuditFinding10(
                $"TASK-{name}", "Attività pianificate", name, name,
                AuditSeverity10.Informational, 0, "Attività registrata nel sistema.",
                string.Empty, "Azione non ancora verificata", false));
        }

        return findings;
    }

    private static string ParseFirstCsvField(string line)
    {
        if (!line.StartsWith('"')) return line.Split(',', 2)[0].Trim();
        int end = line.IndexOf('"', 1);
        return end > 1 ? line[1..end].Replace("\"\"", "\"") : string.Empty;
    }
}
