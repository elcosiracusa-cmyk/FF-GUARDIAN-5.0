using System.Diagnostics;
using System.IO;
using FFGuardian.Security.Core;

namespace FFGuardian.PremiumWpf;

public sealed record ComponentStatus(string Name, bool? IsOperational, string Detail);
public sealed record DashboardStatus(int Score, string ProtectionText, string ProtectionDetail,
    string LastScan, string LastUpdate, string EngineVersion, string DatabaseVersion,
    IReadOnlyList<ComponentStatus> Components);

public sealed class SecurityStatusService(IAntivirusHealthService healthService)
{
    public async Task<DashboardStatus> ReadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<EngineHealthResult> health = await healthService.CheckAsync(cancellationToken).ConfigureAwait(false);
        ComponentStatus engine = ProbeManagedEngine();
        ComponentStatus[] runtime = health.Select(result => new ComponentStatus(result.Name, result.Operational,
            $"{result.Message} Versione: {result.Version}")).ToArray();
        ComponentStatus[] components = [engine, .. runtime];
        int verified = components.Count(component => component.IsOperational == true);
        int score = components.Length == 0 ? 0 : (int)Math.Round(verified * 100d / components.Length);
        string state = score == 100 ? "Sistema Protetto" : score >= 50 ? "Attenzione" : "Protezione Disattivata";
        string detail = score == 100
            ? "Tutti i componenti hanno superato un controllo runtime reale."
            : "Uno o più componenti non hanno superato il controllo runtime.";
        string processPath = Environment.ProcessPath ?? string.Empty;
        string version = string.IsNullOrWhiteSpace(processPath) ? "--" : FileVersionInfo.GetVersionInfo(processPath).FileVersion ?? "--";
        string database = health.FirstOrDefault(item => item.Name == "FreshClam")?.Version ?? "--";
        return new DashboardStatus(score, state, detail, "Non disponibile", "Non disponibile", version, database, components);
    }

    private static ComponentStatus ProbeManagedEngine()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "FFGuardian.Premium.dll");
        if (!File.Exists(path)) return new("Engine10", false, "Assembly frontend non trovato.");
        string version = FileVersionInfo.GetVersionInfo(path).FileVersion ?? "--";
        return new("Engine10", true, $"Assembly caricato: {version}");
    }
}
