namespace FFGuardian.Engine10;

internal enum ThreatVerdict10
{
    Clean,
    Unknown,
    Suspicious,
    Malicious,
    Error
}

internal sealed record FileScanResult10(
    string Path,
    string Sha256,
    long Size,
    ThreatVerdict10 Verdict,
    int Confidence,
    string DetectionName,
    IReadOnlyList<string> Reasons,
    DateTime ScannedUtc);

internal sealed record SignatureEntry10(
    string Id,
    string Sha256,
    string DetectionName,
    int Severity,
    int Confidence,
    bool Enabled);

internal sealed record SignatureDatabaseDocument10(
    int SchemaVersion,
    string DatabaseVersion,
    DateTime GeneratedUtc,
    IReadOnlyList<SignatureEntry10> Signatures,
    IReadOnlyList<string> AllowListSha256);

internal sealed record QuarantineRecord10(
    string Id,
    string OriginalPath,
    string StoredPath,
    string Sha256,
    string DetectionName,
    DateTime QuarantinedUtc,
    bool Restored);