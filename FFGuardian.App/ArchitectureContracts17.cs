namespace FFGuardian;

internal enum GuardianScanType
{
    Quick = 0,
    Full = 1,
    Custom = 2
}

internal enum GuardianVerdict
{
    Unknown = 0,
    Clean = 1,
    Suspicious = 2,
    Malicious = 3,
    Error = 4
}

internal enum GuardianProtectionModule
{
    RealTime = 0,
    RansomShield = 1,
    UsbShield = 2,
    Firewall = 3,
    Updater = 4
}

internal enum GuardianAction
{
    None = 0,
    Allow = 1,
    Block = 2,
    Quarantine = 3,
    TerminateProcess = 4,
    Restore = 5
}

internal sealed record GuardianStartScanRequest(
    Guid RequestId,
    GuardianScanType ScanType,
    IReadOnlyList<string> Paths,
    bool ScanArchives,
    int MaxParallelism);

internal sealed record GuardianScanProgress(
    Guid ScanId,
    long FilesProcessed,
    long BytesProcessed,
    int ThreatsFound,
    string? CurrentPath,
    DateTimeOffset TimestampUtc);

internal sealed record GuardianDetection(
    Guid DetectionId,
    GuardianVerdict Verdict,
    string DetectionName,
    string FilePath,
    string Sha256,
    int Confidence,
    string SourceEngine,
    GuardianAction RecommendedAction,
    DateTimeOffset DetectedAtUtc);

internal sealed record GuardianProtectionEvent(
    Guid EventId,
    GuardianProtectionModule Module,
    string EventType,
    int Severity,
    int? ProcessId,
    string? ProcessPath,
    string? FilePath,
    string Details,
    DateTimeOffset TimestampUtc);

internal sealed record GuardianSystemSnapshot(
    double CpuPercent,
    double MemoryMegabytes,
    double DiskActivityPercent,
    long FilesScanned,
    long ThreatsBlocked,
    string EngineVersion,
    string SignatureVersion,
    DateTimeOffset TimestampUtc);

internal sealed record GuardianQuarantineItem(
    Guid QuarantineId,
    string OriginalPath,
    string StoredPath,
    string Sha256,
    string DetectionName,
    long OriginalSize,
    DateTimeOffset QuarantinedAtUtc,
    bool CanRestore);

internal sealed record GuardianUpdateManifest(
    string Product,
    string Channel,
    string EngineVersion,
    string SignatureVersion,
    string MinimumEngineVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<GuardianUpdatePackage> Packages);

internal sealed record GuardianUpdatePackage(
    string Name,
    string Sha256,
    long Size,
    Uri DownloadUri);

internal interface IGuardianControlChannel
{
    Task<Guid> StartScanAsync(GuardianStartScanRequest request, CancellationToken cancellationToken);
    Task PauseScanAsync(Guid scanId, CancellationToken cancellationToken);
    Task ResumeScanAsync(Guid scanId, CancellationToken cancellationToken);
    Task CancelScanAsync(Guid scanId, CancellationToken cancellationToken);
    Task<GuardianSystemSnapshot> GetSystemSnapshotAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GuardianQuarantineItem>> GetQuarantineAsync(CancellationToken cancellationToken);
    Task ApplyActionAsync(Guid detectionId, GuardianAction action, CancellationToken cancellationToken);
    Task CheckForUpdatesAsync(CancellationToken cancellationToken);
}

internal interface IGuardianEventChannel
{
    IAsyncEnumerable<GuardianScanProgress> ReadScanProgressAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<GuardianProtectionEvent> ReadProtectionEventsAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<GuardianDetection> ReadDetectionsAsync(CancellationToken cancellationToken);
}

internal static class GuardianIpcEndpoints
{
    internal const string ControlPipe = @"FFGuardian.Control.v1";
    internal const string EventsPipe = @"FFGuardian.Events.v1";
    internal const int MaximumMessageBytes = 4 * 1024 * 1024;
}

internal static class GuardianLocalDatabaseSchema
{
    internal const string Sql = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;
        PRAGMA synchronous = NORMAL;

        CREATE TABLE IF NOT EXISTS security_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            event_time_utc TEXT NOT NULL,
            event_type TEXT NOT NULL,
            severity INTEGER NOT NULL,
            source_module TEXT NOT NULL,
            process_id INTEGER,
            process_path TEXT,
            file_path TEXT,
            sha256 TEXT,
            detection_name TEXT,
            action_taken TEXT,
            details_json TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS scan_sessions (
            scan_id TEXT PRIMARY KEY,
            scan_type INTEGER NOT NULL,
            started_at_utc TEXT NOT NULL,
            completed_at_utc TEXT,
            status INTEGER NOT NULL,
            files_scanned INTEGER NOT NULL DEFAULT 0,
            bytes_scanned INTEGER NOT NULL DEFAULT 0,
            threats_found INTEGER NOT NULL DEFAULT 0,
            signature_version TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS scan_results (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            scan_id TEXT NOT NULL,
            file_path TEXT NOT NULL,
            sha256 TEXT,
            verdict INTEGER NOT NULL,
            confidence INTEGER NOT NULL,
            detection_name TEXT,
            source_engine TEXT NOT NULL,
            action_taken TEXT,
            FOREIGN KEY (scan_id) REFERENCES scan_sessions(scan_id)
        );

        CREATE TABLE IF NOT EXISTS quarantine_items (
            quarantine_id TEXT PRIMARY KEY,
            original_path TEXT NOT NULL,
            stored_path TEXT NOT NULL,
            sha256 TEXT NOT NULL,
            detection_name TEXT NOT NULL,
            original_size INTEGER NOT NULL,
            quarantined_at_utc TEXT NOT NULL,
            status INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS settings (
            setting_key TEXT PRIMARY KEY,
            setting_value TEXT NOT NULL,
            modified_at_utc TEXT NOT NULL
        );
        """;
}
