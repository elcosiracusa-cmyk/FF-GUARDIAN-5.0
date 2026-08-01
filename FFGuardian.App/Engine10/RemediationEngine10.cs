namespace FFGuardian.Engine10;

internal sealed class RemediationEngine10
{
    private const string QuarantineAction = "QuarantineFile";
    private const string DisableStartupFileAction = "DisableStartupFile";

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
            Guid.NewGuid().ToString("N"), finding.Id, QuarantineAction, finding.Target,
            "Crea un backup verificabile e sposta il file nella quarantena locale cifrata.",
            RequiresConfirmation: true, RollbackSupported: true);
    }

    public RemediationPlan10 CreateDisableStartupFilePlan(AuditFinding10 finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        string target = Path.GetFullPath(finding.Target);
        if (!IsStartupFolderFile(target))
            throw new InvalidOperationException("La correzione è consentita solo per file presenti nelle cartelle Startup di Windows.");

        return new RemediationPlan10(
            Guid.NewGuid().ToString("N"), finding.Id, DisableStartupFileAction, target,
            "Crea un backup e disabilita l'elemento Startup rinominandolo in modo reversibile.",
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
        RequireConfirmation(plan, confirmed);
        if (!string.Equals(plan.Action, QuarantineAction, StringComparison.Ordinal))
            throw new NotSupportedException($"Azione non supportata: {plan.Action}");

        string target = Path.GetFullPath(plan.Target);
        string scannedPath = Path.GetFullPath(scanResult.Path);
        if (!string.Equals(target, scannedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Il risultato della scansione non corrisponde al file da correggere.");
        if (scanResult.Verdict is not ThreatVerdict10.Malicious and not ThreatVerdict10.Suspicious)
            throw new InvalidOperationException("La quarantena richiede un risultato sospetto o malevolo.");
        if (!File.Exists(target))
            throw new FileNotFoundException("File da isolare non trovato.", target);

        await _rollback.BackupFileAsync(target, plan.Action, cancellationToken).ConfigureAwait(false);
        return await _quarantine.QuarantineAsync(scanResult, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemediationExecutionResult10> ExecuteDisableStartupFileAsync(
        RemediationPlan10 plan,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireConfirmation(plan, confirmed);
        if (!string.Equals(plan.Action, DisableStartupFileAction, StringComparison.Ordinal))
            throw new NotSupportedException($"Azione non supportata: {plan.Action}");

        string target = Path.GetFullPath(plan.Target);
        if (!IsStartupFolderFile(target))
            throw new InvalidOperationException("Il target non appartiene a una cartella Startup autorizzata.");
        if (!File.Exists(target))
            throw new FileNotFoundException("Elemento Startup non trovato.", target);

        string disabledPath = target + ".ffguardian-disabled";
        if (File.Exists(disabledPath))
            throw new IOException("Esiste già un elemento Startup disabilitato con lo stesso nome.");

        RollbackRecord10 rollback = await _rollback
            .BackupFileAsync(target, plan.Action, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            File.Move(target, disabledPath);
            if (File.Exists(target) || !File.Exists(disabledPath))
                throw new IOException("Verifica della disabilitazione Startup non riuscita.");

            return new RemediationExecutionResult10(
                plan.Id,
                plan.Action,
                target,
                true,
                $"Elemento Startup disabilitato: {disabledPath}",
                rollback.Id,
                DateTime.UtcNow);
        }
        catch
        {
            if (!File.Exists(target) && File.Exists(rollback.BackupPath))
                await _rollback.RestoreFileAsync(rollback, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void RequireConfirmation(RemediationPlan10 plan, bool confirmed)
    {
        if (plan.RequiresConfirmation && !confirmed)
            throw new InvalidOperationException("La correzione richiede conferma esplicita.");
    }

    private static bool IsStartupFolderFile(string path)
    {
        string full = Path.GetFullPath(path);
        string[] startupFolders =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        return startupFolders.Any(folder =>
            !string.IsNullOrWhiteSpace(folder) &&
            IsPathInside(full, Path.GetFullPath(folder)));
    }

    private static bool IsPathInside(string path, string root)
    {
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
