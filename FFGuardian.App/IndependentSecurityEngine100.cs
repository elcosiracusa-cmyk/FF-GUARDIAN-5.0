using FFGuardian.Engine10;

namespace FFGuardian;

internal enum IndependentRiskLevel
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

internal sealed record IndependentFinding(
    string Category,
    string Name,
    string Target,
    IndependentRiskLevel Risk,
    int Score,
    string Evidence,
    string Sha256,
    string SignatureStatus);

internal sealed record IndependentAuditResult(
    DateTime StartedAt,
    DateTime CompletedAt,
    int SecurityScore,
    IReadOnlyList<IndependentFinding> Findings,
    int FilesExamined,
    int StartupEntries,
    int ServicesExamined,
    int ScheduledTasksExamined);

internal sealed class IndependentSecurityEngine100 : IDisposable
{
    private readonly FFGuardianEngine10 _engine = new();
    private bool _disposed;

    public async Task<IndependentAuditResult> RunAuditAsync(
        string? scanRoot,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EngineAuditResult10 audit = await _engine.RunAuditAsync(progress, cancellationToken).ConfigureAwait(false);
        List<IndependentFinding> findings = audit.Findings.Select(ConvertAuditFinding).ToList();
        int filesExamined = 0;

        if (!string.IsNullOrWhiteSpace(scanRoot))
        {
            FolderScanSummary10 folder = await _engine.ScanFolderAsync(scanRoot, progress, cancellationToken)
                .ConfigureAwait(false);
            filesExamined = folder.FilesVisited;
            findings.AddRange(folder.Results
                .Where(result => result.Verdict is ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious or ThreatVerdict10.Error)
                .Select(ConvertFileResult));
        }

        int penalty = findings.Where(finding => finding.Score > 0)
            .Sum(finding => Math.Clamp(finding.Score, 0, 100));
        int score = Math.Clamp(100 - Math.Min(100, penalty), 0, 100);

        return new IndependentAuditResult(
            audit.StartedUtc.ToLocalTime(),
            DateTime.UtcNow.ToLocalTime(),
            score,
            findings.OrderByDescending(finding => finding.Score).ToArray(),
            filesExamined,
            audit.PersistenceItems,
            audit.ServiceItems,
            audit.ScheduledTaskItems);
    }

    public async Task<IndependentFinding?> AnalyzeFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        FileScanResult10 result = await _engine.ScanFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        return result.Verdict is ThreatVerdict10.Clean or ThreatVerdict10.Unknown
            ? null
            : ConvertFileResult(result);
    }

    private static IndependentFinding ConvertAuditFinding(AuditFinding10 finding) => new(
        finding.Category,
        finding.Name,
        finding.Target,
        ConvertSeverity(finding.Severity),
        finding.RiskScore,
        finding.Evidence,
        finding.Sha256,
        finding.SignatureStatus);

    private static IndependentFinding ConvertFileResult(FileScanResult10 result) => new(
        "File",
        Path.GetFileName(result.Path),
        result.Path,
        result.Verdict switch
        {
            ThreatVerdict10.Malicious => IndependentRiskLevel.Critical,
            ThreatVerdict10.Suspicious => IndependentRiskLevel.High,
            ThreatVerdict10.Error => IndependentRiskLevel.Low,
            _ => IndependentRiskLevel.Informational
        },
        result.Verdict switch
        {
            ThreatVerdict10.Malicious => Math.Max(60, result.Confidence),
            ThreatVerdict10.Suspicious => Math.Max(30, result.Confidence / 2),
            ThreatVerdict10.Error => 5,
            _ => 0
        },
        string.Join("; ", result.Reasons),
        result.Sha256,
        result.DetectionName);

    private static IndependentRiskLevel ConvertSeverity(AuditSeverity10 severity) => severity switch
    {
        AuditSeverity10.Critical => IndependentRiskLevel.Critical,
        AuditSeverity10.High => IndependentRiskLevel.High,
        AuditSeverity10.Medium => IndependentRiskLevel.Medium,
        AuditSeverity10.Low => IndependentRiskLevel.Low,
        _ => IndependentRiskLevel.Informational
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
