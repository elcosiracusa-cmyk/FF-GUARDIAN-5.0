namespace FFGuardian.Engine10;

internal sealed record EngineAuditResult10(
    DateTime StartedUtc,
    DateTime CompletedUtc,
    int SecurityScore,
    IReadOnlyList<AuditFinding10> Findings,
    int PersistenceItems,
    int ServiceItems,
    int ScheduledTaskItems);

internal sealed record FolderScanSummary10(
    DateTime StartedUtc,
    DateTime CompletedUtc,
    string RootPath,
    int FilesVisited,
    int FilesScanned,
    int CleanFiles,
    int UnknownFiles,
    int SuspiciousFiles,
    int MaliciousFiles,
    int ErrorFiles,
    IReadOnlyList<FileScanResult10> Results);

internal sealed class FFGuardianEngine10 : IDisposable
{
    private static readonly HashSet<string> ScannableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".msi", ".msix", ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta"
    };

    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SignatureDatabase10 _signatureDatabase;
    private readonly IndependentScanner10 _scanner;
    private readonly PersistenceAudit10 _persistenceAudit;
    private readonly ServiceAudit10 _serviceAudit;
    private readonly ScheduledTaskAudit10 _scheduledTaskAudit;
    private readonly QuarantineStore10 _quarantineStore;
    private readonly RollbackManager10 _rollbackManager;
    private readonly RemediationEngine10 _remediationEngine;
    private readonly SecureUpdater10? _secureUpdater;
    private bool _disposed;

    public FFGuardianEngine10(string? signatureDatabasePath = null, string? updaterPublicKeyPem = null)
    {
        _signatureDatabase = new SignatureDatabase10(signatureDatabasePath);
        _scanner = new IndependentScanner10(_signatureDatabase);
        _persistenceAudit = new PersistenceAudit10();
        _serviceAudit = new ServiceAudit10();
        _scheduledTaskAudit = new ScheduledTaskAudit10();
        _quarantineStore = new QuarantineStore10();
        _rollbackManager = new RollbackManager10();
        _remediationEngine = new RemediationEngine10(_quarantineStore, _rollbackManager);
        _secureUpdater = string.IsNullOrWhiteSpace(updaterPublicKeyPem)
            ? null
            : new SecureUpdater10(updaterPublicKeyPem);
    }

    public string SignatureDatabaseVersion => _signatureDatabase.Version;
    public bool SecureUpdatesConfigured => _secureUpdater is not null;

    public async Task<FileScanResult10> ScanFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _scanner.ScanFileAsync(path, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<FolderScanSummary10> ScanFolderAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Cartella non trovata: {root}");

        return await ExecuteExclusiveAsync(async token =>
        {
            DateTime started = DateTime.UtcNow;
            List<FileScanResult10> results = [];
            int visited = 0;
            int scanned = 0;
            Stack<string> pending = new();
            pending.Push(root);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string current = pending.Pop();

                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(current); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    progress?.Report($"Cartella non accessibile: {current}");
                    continue;
                }

                foreach (string file in files)
                {
                    token.ThrowIfCancellationRequested();
                    visited++;
                    if (!ScannableExtensions.Contains(Path.GetExtension(file)))
                        continue;

                    scanned++;
                    FileScanResult10 result = await _scanner.ScanFileAsync(file, token).ConfigureAwait(false);
                    results.Add(result);
                    if (scanned % 25 == 0)
                        progress?.Report($"File analizzati: {scanned:N0} — Visitati: {visited:N0}");
                }

                IEnumerable<string> directories;
                try { directories = Directory.EnumerateDirectories(current); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (string directory in directories)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(directory);
                        if ((attributes & FileAttributes.ReparsePoint) == 0)
                            pending.Push(directory);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                    }
                }
            }

            return new FolderScanSummary10(
                started,
                DateTime.UtcNow,
                root,
                visited,
                scanned,
                results.Count(result => result.Verdict == ThreatVerdict10.Clean),
                results.Count(result => result.Verdict == ThreatVerdict10.Unknown),
                results.Count(result => result.Verdict == ThreatVerdict10.Suspicious),
                results.Count(result => result.Verdict == ThreatVerdict10.Malicious),
                results.Count(result => result.Verdict == ThreatVerdict10.Error),
                results.OrderByDescending(result => VerdictOrder(result.Verdict))
                    .ThenByDescending(result => result.Confidence)
                    .ToArray());
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EngineAuditResult10> RunAuditAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(async token =>
        {
            DateTime started = DateTime.UtcNow;

            progress?.Report("Analisi della persistenza…");
            IReadOnlyList<AuditFinding10> persistence = await _persistenceAudit.AuditAsync(token).ConfigureAwait(false);

            progress?.Report("Analisi dei servizi…");
            IReadOnlyList<AuditFinding10> services = await _serviceAudit.AuditAsync(token).ConfigureAwait(false);

            progress?.Report("Analisi delle attività pianificate…");
            IReadOnlyList<AuditFinding10> tasks = await _scheduledTaskAudit.AuditAsync(token).ConfigureAwait(false);

            AuditFinding10[] findings = persistence
                .Concat(services)
                .Concat(tasks)
                .OrderByDescending(finding => finding.RiskScore)
                .ThenBy(finding => finding.Category, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int penalty = findings.Where(finding => finding.RiskScore > 0)
                .Sum(finding => Math.Clamp(finding.RiskScore, 0, 100));
            int score = Math.Clamp(100 - Math.Min(100, penalty), 0, 100);

            return new EngineAuditResult10(
                started,
                DateTime.UtcNow,
                score,
                findings,
                persistence.Count,
                services.Count,
                tasks.Count);
        }, cancellationToken).ConfigureAwait(false);
    }

    public AuthenticodeResult100 VerifyAuthenticode(string path)
    {
        ThrowIfDisposed();
        return global::FFGuardian.AuthenticodeVerifier100.Verify(path);
    }

    public RemediationPlan10 CreateQuarantinePlan(AuditFinding10 finding)
    {
        ThrowIfDisposed();
        return _remediationEngine.CreateQuarantinePlan(finding);
    }

    public async Task<QuarantineRecord10> ExecuteQuarantineAsync(
        RemediationPlan10 plan,
        FileScanResult10 scanResult,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _remediationEngine.ExecuteQuarantineAsync(plan, scanResult, confirmed, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ExecuteExclusiveAsync(async token =>
        {
            await _quarantineStore.RestoreAsync(quarantineId, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReloadSignaturesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ExecuteExclusiveAsync(async token =>
        {
            await _signatureDatabase.ReloadAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UpdateVerificationResult10> VerifyUpdateAsync(
        UpdateManifest10 manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_secureUpdater is null)
            return new UpdateVerificationResult10(false, "Chiave pubblica aggiornamenti non configurata.", string.Empty, 0);

        return await ExecuteExclusiveAsync(
            token => _secureUpdater.VerifyPackageAsync(manifest, packagePath, token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ExecuteExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (!await _operationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Un'altra operazione del motore è già in esecuzione.");

        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static int VerdictOrder(ThreatVerdict10 verdict) => verdict switch
    {
        ThreatVerdict10.Malicious => 5,
        ThreatVerdict10.Suspicious => 4,
        ThreatVerdict10.Error => 3,
        ThreatVerdict10.Unknown => 2,
        ThreatVerdict10.Clean => 1,
        _ => 0
    };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operationGate.Dispose();
    }
}
