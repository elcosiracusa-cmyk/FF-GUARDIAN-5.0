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
        FileScanResult10 scanResult,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scanResult);
        if (!confirmed) throw new InvalidOperationException("La correzione richiede conferma esplicita.");
        if (!string.Equals(plan.Action, "QuarantineFile", StringComparison.Ordinal))
            throw new NotSupportedException($"Azione non supportata: {plan.Action}");

        string target = Path.GetFullPath(plan.Target);
        string scannedPath = Path.GetFullPath(scanResult.Path);
        if (!string.Equals(target, scannedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Il risultato della scansione non corrisponde al file da correggere.");
        if (scanResult.Verdict is not ThreatVerdict10.Malicious and not ThreatVerdict10.Suspicious)
            throw new InvalidOperationException("La quarantena richiede un risultato sospetto o malevolo.");
        if (!File.Exists(target)) throw new FileNotFoundException("File da isolare non trovato.", target);

        await _rollback.BackupFileAsync(target, plan.Action, cancellationToken).ConfigureAwait(false);
        return await _quarantine.QuarantineAsync(scanResult, cancellationToken).ConfigureAwait(false);
    }
}
