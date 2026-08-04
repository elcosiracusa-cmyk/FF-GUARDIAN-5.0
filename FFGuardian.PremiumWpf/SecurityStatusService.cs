using System.Diagnostics;
using System.IO;
using System.Text;

namespace FFGuardian.PremiumWpf;

public sealed record ComponentStatus(string Name, bool? IsOperational, string Detail);
public sealed record DashboardStatus(int Score, string ProtectionText, string ProtectionDetail,
    string LastScan, string LastUpdate, string EngineVersion, string DatabaseVersion,
    IReadOnlyList<ComponentStatus> Components);

public sealed class SecurityStatusService
{
    private readonly string _baseDirectory;

    public SecurityStatusService() : this(AppContext.BaseDirectory) { }

    internal SecurityStatusService(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    public async Task<DashboardStatus> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ComponentStatus engine = ProbeManagedEngine();
        ComponentStatus yara = await ProbeExecutableAsync("YARA", [
            "Engine/Yara/yara64.exe", "Engine/Yara/yara.exe",
            "Tools/Yara/yara64.exe", "Tools/Yara/yara.exe"], cancellationToken).ConfigureAwait(false);
        ComponentStatus clam = await ProbeExecutableAsync("ClamAV", [
            "Engine/ClamAV/clamscan.exe", "ClamAV/clamscan.exe"], cancellationToken).ConfigureAwait(false);
        ComponentStatus fresh = await ProbeExecutableAsync("FreshClam", [
            "Engine/ClamAV/freshclam.exe", "ClamAV/freshclam.exe"], cancellationToken).ConfigureAwait(false);
        ComponentStatus signatures = ProbeSignatureDatabase();

        ComponentStatus[] scoredComponents = [engine, clam, fresh, yara, signatures];
        int verified = scoredComponents.Count(component => component.IsOperational == true);
        int score = (int)Math.Round(verified * 100d / scoredComponents.Length);
        string state = score == 100 ? "Sistema Protetto" : score >= 40 ? "Attenzione" : "Protezione Disattivata";
        string detail = score == 100
            ? "Tutti i componenti inclusi nel punteggio hanno superato una verifica reale."
            : "Uno o più componenti non hanno superato la verifica runtime.";

        string processPath = Environment.ProcessPath ?? string.Empty;
        string version = string.IsNullOrWhiteSpace(processPath)
            ? "--"
            : FileVersionInfo.GetVersionInfo(processPath).FileVersion ?? "--";
        string databaseVersion = signatures.IsOperational == true ? signatures.Detail : "--";

        return new DashboardStatus(score, state, detail, "Non disponibile", "Non disponibile",
            version, databaseVersion, scoredComponents);
    }

    private ComponentStatus ProbeManagedEngine()
    {
        string path = Path.Combine(_baseDirectory, "FFGuardian.dll");
        if (!File.Exists(path))
            return new ComponentStatus("Engine10", false, "Assembly principale non trovato.");
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        string version = info.FileVersion ?? info.ProductVersion ?? "versione non disponibile";
        return new ComponentStatus("Engine10", true, $"Assembly caricato dal pacchetto: {version}");
    }

    private ComponentStatus ProbeSignatureDatabase()
    {
        string[] directories = [
            Path.Combine(_baseDirectory, "Database"),
            Path.Combine(_baseDirectory, "Engine", "ClamAV", "Database")
        ];
        string[] extensions = ["*.cvd", "*.cld", "*.json", "*.yar", "*.yara"];
        foreach (string directory in directories)
        {
            if (!Directory.Exists(directory)) continue;
            int files = extensions.Sum(pattern => Directory.EnumerateFiles(directory, pattern,
                SearchOption.TopDirectoryOnly).Count());
            if (files > 0)
                return new ComponentStatus("Database firme", true, $"{files} file firma rilevati e leggibili.");
        }
        return new ComponentStatus("Database firme", false, "Nessun file firma verificabile trovato.");
    }

    private async Task<ComponentStatus> ProbeExecutableAsync(string name, string[] relatives,
        CancellationToken cancellationToken)
    {
        string? executable = relatives.Select(relative => Path.GetFullPath(Path.Combine(_baseDirectory,
                relative.Replace('/', Path.DirectorySeparatorChar))))
            .FirstOrDefault(File.Exists);
        if (executable is null)
            return new ComponentStatus(name, false, "Eseguibile non trovato.");

        ProcessStartInfo start = new()
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--version");

        using Process process = new() { StartInfo = start };
        try
        {
            if (!process.Start())
                return new ComponentStatus(name, false, "Avvio del processo non riuscito.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = (await stdout.ConfigureAwait(false)).Trim();
            string error = (await stderr.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0)
                return new ComponentStatus(name, false, $"--version terminato con codice {process.ExitCode}: {Trim(error)}");
            string version = Trim(string.IsNullOrWhiteSpace(output) ? error : output);
            return new ComponentStatus(name, true, $"Verifica --version superata: {version}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ComponentStatus(name, false, "Timeout durante la verifica --version.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            TryKill(process);
            return new ComponentStatus(name, false, $"Verifica runtime non riuscita: {ex.Message}");
        }
    }

    private static string Trim(string value) => value.Length <= 160 ? value : value[..160] + "…";

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
