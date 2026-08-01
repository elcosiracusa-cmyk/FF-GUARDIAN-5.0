namespace FFGuardian.Engine10;

internal enum AuditSeverity10
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

internal sealed record AuditFinding10(
    string Id,
    string Category,
    string Name,
    string Target,
    AuditSeverity10 Severity,
    int RiskScore,
    string Evidence,
    string Sha256,
    string SignatureStatus,
    bool RemediationAvailable);

internal sealed record RemediationPlan10(
    string Id,
    string FindingId,
    string Action,
    string Target,
    string Description,
    bool RequiresConfirmation,
    bool RollbackSupported);

internal sealed record RollbackRecord10(
    string Id,
    string Action,
    string Target,
    string BackupPath,
    string MetadataPath,
    DateTime CreatedUtc,
    bool Restored);

internal sealed record UpdateManifest10(
    string Version,
    string Channel,
    string PackageFileName,
    string Sha256,
    long Size,
    string MinimumVersion,
    string SignatureBase64);

internal sealed record UpdateVerificationResult10(
    bool IsValid,
    string Status,
    string CalculatedSha256,
    long ActualSize);
