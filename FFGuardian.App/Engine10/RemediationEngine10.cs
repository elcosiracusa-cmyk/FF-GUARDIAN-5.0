namespace FFGuardian.Engine10;

internal sealed class RemediationEngine10
{
    private readonly QuarantineStore10 _quarantine;
    private readonly RollbackManager10 _rollback;

    public RemediationEngine10(QuarantineStore10 quarantine, RollbackManager10 rollback)
    {
        _quarantine = quarantine ?? throw new ArgumentNullException(nameof(quarantine));
        _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
    }

    public RemediationPlan10 CreateQuarantinePlan(AuditFinding10 finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new RemediationPlan10(
            Guid.NewGuid().ToString("N"), finding.Id, "QuarantineFile", finding.Target,
            "Crea un backup verificabile e sposta il file nella quarantena locale.",
            RequiresConfirmation: true, RollbackSupported: true);
    }

    public async Task<QuarantineRecord10> ExecuteQuarantineAsync(
        RemediationPlan10 plan,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!confirmed) throw new InvalidOperationException("La correzione richiede conferma esplicita.");
        if (!string.Equals(plan.Action, "QuarantineFile", StringComparison.Ordinal))
            throw new NotSupportedException($"Azione non supportata: {plan.Action}");
        if (!File.Exists(plan.Target)) throw new FileNotFoundException("File da isolare non trovato.", plan.Target);

        await _rollback.BackupFileAsync(plan.Target, plan.Action, cancellationToken).ConfigureAwait(false);
        return await _quarantine.QuarantineAsync(plan.Target, "FFGuardian.Heuristic.Review", cancellationToken).ConfigureAwait(false);
    }
}
