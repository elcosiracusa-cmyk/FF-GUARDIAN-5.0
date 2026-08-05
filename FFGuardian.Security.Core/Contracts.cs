namespace FFGuardian.Security.Core;

public sealed class SecurityCoreOptions
{
    public string BaseDirectory { get; set; } = AppContext.BaseDirectory;
    public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF Guardian");
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaximumScanConcurrency { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
    public int ScanQueueCapacity { get; set; } = 512;
    public long MaximumFileSizeBytes { get; set; } = 512L * 1024 * 1024;
    public bool EnableIncrementalScanCache { get; set; } = true;
    public TimeSpan ScanCacheRetention { get; set; } = TimeSpan.FromDays(30);
    public List<string> QuickScanDirectories { get; set; } = [];
}

public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, TimeSpan Timeout);
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, TimeSpan Duration);
public sealed record EngineVersionInfo(string Name, string Path, string Version, bool Operational, string Message);
public sealed record EngineHealthResult(string Name, bool Operational, string Version, string Message, DateTimeOffset CheckedAt, TimeSpan Duration);
public sealed record YaraMatch(string Rule, string TargetPath, string RawOutput);
public enum YaraRuntimeStatus { Active, NotInstalled, ExecutableNotFound, CompilerNotFound, RulesUnavailable, RulesInvalid, SelfTestFailed, EngineStartError }
public sealed record YaraDiagnostics(YaraRuntimeStatus Status, string StatusText, string ExecutablePath, string ExecutableName, string CompilerPath, string Version, string RulesPath, int RuleCount, bool RulesValid, bool SelfTestPassed, string StandardOutput, string StandardError, int ExitCode, bool TimedOut, DateTimeOffset LastCheck, TimeSpan Duration);
public sealed record ClamAvDetection(string Signature, string TargetPath, string RawOutput);
public sealed record ScanDetection(string Engine, string Name, string FilePath, string Details);
public enum ScanMode { Direct, Quick, Full, Custom }
public enum ScanState { Ready, Enumerating, Scanning, Cancelling, Completed, Cancelled, Failed }
public sealed record ScanProgress(int FilesScanned, int FilesSkipped, string CurrentPath, int TotalFiles = 0, string Phase = "Scanning", string Engine = "", TimeSpan Elapsed = default, TimeSpan? EstimatedRemaining = null)
{
    public int Percentage => TotalFiles <= 0 ? 0 : Math.Clamp((int)Math.Round((FilesScanned + FilesSkipped) * 100d / TotalFiles), 0, 100);
}
public sealed record ScanStatus(ScanMode Mode, ScanState State, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, int TotalFiles, int FilesScanned, int FilesSkipped, int FilesFailed, int ThreatsFound, string CurrentPath, string CurrentEngine, string Message, TimeSpan Elapsed, TimeSpan? EstimatedRemaining)
{
    public static ScanStatus Ready { get; } = new(ScanMode.Direct, ScanState.Ready, null, null, 0, 0, 0, 0, 0, string.Empty, string.Empty, "Pronto", TimeSpan.Zero, null);
}
public sealed record ScanRequest(IReadOnlyCollection<string> Paths, bool Recursive = true, bool QuarantineDetections = false, bool ForceRescan = false);
public sealed record ScanResult(DateTimeOffset StartTime, DateTimeOffset EndTime, int FilesScanned, int FilesSkipped, int FilesFailed, IReadOnlyList<ScanDetection> Detections, IReadOnlyList<string> EnginesUsed, bool WasCancelled, IReadOnlyList<string> Errors);
public sealed record ScanCacheEntry(string Path, long Size, DateTime LastWriteUtc, string Sha256, DateTimeOffset VerifiedAt);
public sealed record QuarantineEntry(Guid Id, string OriginalName, string OriginalPath, string StoredPath, string Sha256, long Size, string Engine, string Detection, DateTimeOffset CreatedAt, string Risk);
public sealed record QuarantineResult(bool Success, QuarantineEntry? Entry, string Message);

public interface IProcessRunner { Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken); }
public interface IFileHashService { Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken); }
public interface IScanCacheService { Task<ScanCacheEntry?> TryGetValidAsync(string path, long size, DateTime lastWriteUtc, CancellationToken cancellationToken); Task StoreAsync(ScanCacheEntry entry, CancellationToken cancellationToken); Task PruneAsync(CancellationToken cancellationToken); }
public interface IEngineLocatorService { Task<string?> LocateYaraAsync(CancellationToken cancellationToken); Task<string?> LocateYaraCompilerAsync(CancellationToken cancellationToken); Task<string?> LocateClamAvAsync(CancellationToken cancellationToken); }
public interface IPathExclusionService { bool ShouldExclude(string path); bool IsInside(string candidate, string trustedDirectory); }
public interface ISecurityEventLogger { Task LogAsync(string componentName, string outcome, string message, CancellationToken cancellationToken); }
public interface IYaraService { Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken); Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken); Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken); Task<YaraDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken); }
public interface IClamAvService { Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken); Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken); Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken); }
public interface IFreshClamService { Task<EngineHealthResult> GetHealthAsync(CancellationToken cancellationToken); }
public interface IQuarantineService { Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken); Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken); Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken); Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken); }
public interface IScanService
{
    Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
    Task<ScanResult> ScanQuickAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException("Scansione rapida non implementata dal servizio.");
    Task<ScanResult> ScanFullAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException("Scansione completa non implementata dal servizio.");
    Task<ScanResult> ScanCustomAsync(string path, IProgress<ScanProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException("Scansione personalizzata non implementata dal servizio.");
    Task CancelAsync() => Task.CompletedTask;
    ScanStatus GetStatus() => ScanStatus.Ready;
}
public interface IAntivirusHealthService { Task<IReadOnlyList<EngineHealthResult>> CheckAsync(CancellationToken cancellationToken); }
