using System.Diagnostics;
using System.Globalization;
using System.IO;
using FFGuardian.Security.Core;

namespace FFGuardian.PremiumWpf;

public sealed record ComponentStatus(string Name, bool? IsOperational, string Detail);
public sealed record DashboardStatus(int Score, string ProtectionText, string ProtectionDetail,
    string LastScan, string LastUpdate, string EngineVersion, string DatabaseVersion,
    IReadOnlyList<ComponentStatus> Components);

public sealed class SecurityStatusService(
    IAntivirusHealthService healthService,
    IEngine10Service engine10Service,
    AiSecurityHealthService aiHealthService,
    GitHubUpdateService updateService)
{
    public async Task<DashboardStatus> ReadAsync(CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<EngineHealthResult>> healthTask = healthService.CheckAsync(cancellationToken);
        Task<SecurityComponentHealth> engine10Task = engine10Service.GetHealthAsync(cancellationToken);
        Task<ComponentStatus> aiTask = aiHealthService.CheckAsync(cancellationToken);
        Task<IReadOnlyList<UpdateCheckItem>> updatesTask = updateService.CheckAllAsync(cancellationToken);

        await Task.WhenAll(healthTask, engine10Task, aiTask, updatesTask).ConfigureAwait(false);

        IReadOnlyList<EngineHealthResult> health = await healthTask.ConfigureAwait(false);
        SecurityComponentHealth engine10 = await engine10Task.ConfigureAwait(false);
        ComponentStatus ai = await aiTask.ConfigureAwait(false);
        IReadOnlyList<UpdateCheckItem> updates = await updatesTask.ConfigureAwait(false);

        ComponentStatus engine = new("Engine10", engine10.RuntimeVerified,
            $"{engine10.Status}. {engine10.Message} Versione: {engine10.Version}");
        ComponentStatus[] runtime = health.Select(result => new ComponentStatus(result.Name, result.Operational,
            $"{result.Message} Versione: {result.Version}")).ToArray();
        ComponentStatus[] updateComponents = updates.Select(update => new ComponentStatus(
            $"Aggiornamento {update.Name}",
            update.CheckSucceeded,
            update.Message)).ToArray();

        ComponentStatus[] securityComponents = [engine, ai, .. runtime];
        ComponentStatus[] components = [.. securityComponents, .. updateComponents];
        int verified = securityComponents.Count(component => component.IsOperational == true);
        int score = securityComponents.Length == 0 ? 0 : (int)Math.Round(verified * 100d / securityComponents.Length);
        bool hasUpdates = updates.Any(update => update.UpdateAvailable);
        string state = score == 100 ? "Sistema Protetto" : score >= 50 ? "Attenzione" : "Protezione Disattivata";
        string detail = score == 100
            ? hasUpdates
                ? "I componenti runtime sono operativi; sono disponibili aggiornamenti verificati su GitHub."
                : "Tutti i componenti, inclusa l'analisi AI locale, hanno superato un controllo runtime reale."
            : "Uno o più componenti non hanno superato il controllo runtime.";
        string database = health.FirstOrDefault(item => item.Name == "FreshClam")?.Version ?? "--";
        string lastUpdate = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
        return new DashboardStatus(score, state, detail, "Non disponibile", lastUpdate, engine10.Version, database, components);
    }
}
