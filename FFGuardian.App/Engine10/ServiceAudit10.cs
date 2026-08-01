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
                string name = string.IsNullOrWhiteSpace(service.DisplayName) ? service.ServiceName : service.DisplayName;
                findings.Add(new AuditFinding10(
                    $"SERVICE-{service.ServiceName}",
                    "Servizi",
                    name,
                    service.ServiceName,
                    AuditSeverity10.Informational,
                    0,
                    $"Stato: {service.Status}; tipo: {service.ServiceType}",
                    string.Empty,
                    "Percorso eseguibile non ancora verificato",
                    false));
            }
        }

        return Task.FromResult<IReadOnlyList<AuditFinding10>>(findings);
    }
}
