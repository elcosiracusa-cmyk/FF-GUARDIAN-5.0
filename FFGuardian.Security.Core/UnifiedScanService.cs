using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class UnifiedScanService : IScanService, IDisposable
{
    private readonly ScanService _scanner;
    private readonly IPathExclusionService _exclusions;
    private readonly ISecurityEventLogger _logger;
    private readonly SecurityCoreOptions _options;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _statusGate = new();
    private CancellationTokenSource? _activeCancellation;
    private ScanStatus _status = ScanStatus.Ready;
    private bool _disposed;

    public UnifiedScanService(ScanService scanner, IPathExclusionService exclusions, ISecurityEventLogger logger, IOptions<SecurityCoreOptions> options)
    {
        _scanner = scanner;
        _exclusions = exclusions;
        _logger = logger;
        _options = options.Value;
    }

    public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        RunAsync(ScanMode.Direct, request, progress, cancellationToken);

    public Task<ScanResult> ScanQuickAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        string[] paths = BuildQuickScanPaths();
        return RunAsync(ScanMode.Quick, new ScanRequest(paths, Recursive: true), progress, cancellationToken);
    }

    public Task<ScanResult> ScanFullAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        string[] roots = _options.FullScanRootDirectories.Count > 0
            ? _options.FullScanRootDirectories.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                .Select(drive => drive.RootDirectory.FullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        return RunAsync(ScanMode.Full, new ScanRequest(roots, Recursive: true), progress, cancellationToken);
    }

    public Task<ScanResult> ScanCustomAsync(string path, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new FileNotFoundException("Il percorso selezionato non esiste.", fullPath);
        return RunAsync(ScanMode.Custom, new ScanRequest([fullPath], Recursive: Directory.Exists(fullPath)), progress, cancellationToken);
    }

    public Task CancelAsync()
    {
        CancellationTokenSource? cancellation;
        lock (_statusGate)
        {
            cancellation = _activeCancellation;
            if (cancellation is not null && !_status.State.Equals(ScanState.Cancelled))
                _status = _status with { State = ScanState.Cancelling, Message = "Annullamento in corso" };
        }
        cancellation?.Cancel();
        return Task.CompletedTask;
    }

    public ScanStatus GetStatus()
    {
        lock (_statusGate) return _status;
    }

    private async Task<ScanResult> RunAsync(ScanMode mode, ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_statusGate)
        {
            _activeCancellation?.Dispose();
            _activeCancellation = linked;
            _status = new(mode, ScanState.Enumerating, started, null, 0, 0, 0, 0, 0, string.Empty, string.Empty, "Enumerazione dei file", TimeSpan.Zero, null);
        }

        try
        {
            await _logger.LogAsync("Scan", "Started", $"mode={mode}; paths={request.Paths.Count}", CancellationToken.None).ConfigureAwait(false);
            int total = CountCandidates(request, linked.Token);
            UpdateStatus(status => status with { TotalFiles = total, State = ScanState.Scanning, Message = "Scansione in corso" });

            IProgress<ScanProgress> internalProgress = new InlineProgress<ScanProgress>(value =>
            {
                int completed = value.FilesScanned + value.FilesSkipped;
                TimeSpan elapsed = stopwatch.Elapsed;
                TimeSpan? remaining = completed > 0 && total > completed
                    ? TimeSpan.FromTicks((long)(elapsed.Ticks * ((double)(total - completed) / completed)))
                    : TimeSpan.Zero;
                string engine = string.IsNullOrWhiteSpace(value.Engine) ? "YARA / ClamAV" : value.Engine;
                ScanProgress enriched = value with { TotalFiles = total, Phase = "Scanning", Engine = engine, Elapsed = elapsed, EstimatedRemaining = remaining };
                UpdateStatus(status => status with
                {
                    State = ScanState.Scanning,
                    FilesScanned = enriched.FilesScanned,
                    FilesSkipped = enriched.FilesSkipped,
                    CurrentPath = enriched.CurrentPath,
                    CurrentEngine = enriched.Engine,
                    Elapsed = enriched.Elapsed,
                    EstimatedRemaining = enriched.EstimatedRemaining
                });
                progress?.Report(enriched);
            });

            ScanResult result = await _scanner.ScanAsync(request, internalProgress, linked.Token).ConfigureAwait(false);
            stopwatch.Stop();
            ScanState finalState = result.WasCancelled ? ScanState.Cancelled : ScanState.Completed;
            UpdateStatus(status => status with
            {
                State = finalState,
                CompletedAt = result.EndTime,
                FilesScanned = result.FilesScanned,
                FilesSkipped = result.FilesSkipped,
                FilesFailed = result.FilesFailed,
                ThreatsFound = result.Detections.Count,
                CurrentPath = string.Empty,
                CurrentEngine = string.Join(", ", result.EnginesUsed),
                Message = result.WasCancelled ? "Scansione annullata" : "Scansione completata",
                Elapsed = stopwatch.Elapsed,
                EstimatedRemaining = TimeSpan.Zero
            });
            await _logger.LogAsync("Scan", result.WasCancelled ? "Cancelled" : "Completed",
                $"mode={mode}; scanned={result.FilesScanned}; skipped={result.FilesSkipped}; failed={result.FilesFailed}; threats={result.Detections.Count}; durationMs={stopwatch.ElapsedMilliseconds}", CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            stopwatch.Stop();
            UpdateStatus(status => status with { State = ScanState.Cancelled, CompletedAt = DateTimeOffset.UtcNow, Message = "Scansione annullata", Elapsed = stopwatch.Elapsed, EstimatedRemaining = TimeSpan.Zero });
            await _logger.LogAsync("Scan", "Cancelled", $"mode={mode}; durationMs={stopwatch.ElapsedMilliseconds}", CancellationToken.None).ConfigureAwait(false);
            ScanStatus status = GetStatus();
            return new(started, DateTimeOffset.UtcNow, status.FilesScanned, status.FilesSkipped, status.FilesFailed, [], [], true, []);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            UpdateStatus(status => status with { State = ScanState.Failed, CompletedAt = DateTimeOffset.UtcNow, Message = exception.Message, Elapsed = stopwatch.Elapsed, EstimatedRemaining = null });
            await _logger.LogAsync("Scan", "Failed", $"mode={mode}; error={exception.GetType().Name}; message={exception.Message}", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock (_statusGate)
            {
                if (ReferenceEquals(_activeCancellation, linked)) _activeCancellation = null;
            }
            linked.Dispose();
            _operationGate.Release();
        }
    }

    private string[] BuildQuickScanPaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        if (_options.IncludeDefaultQuickScanLocations)
        {
            AddDirectory(paths, Path.GetTempPath());
            AddSpecialFolder(paths, Environment.SpecialFolder.DesktopDirectory);
            AddSpecialFolder(paths, Environment.SpecialFolder.Startup);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddDirectory(paths, Path.Combine(userProfile, "Downloads"));
        }
        foreach (string configured in _options.QuickScanDirectories) AddDirectory(paths, configured);

        if (_options.IncludeRunningProcessesInQuickScan)
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string? executable = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable)) paths.Add(Path.GetFullPath(executable));
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (NotSupportedException) { }
                }
            }
        }
        return paths.ToArray();
    }

    private int CountCandidates(ScanRequest request, CancellationToken cancellationToken)
    {
        int count = 0;
        HashSet<string> visitedFiles = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visitedDirectories = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in request.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (File.Exists(full))
            {
                if (!_exclusions.ShouldExclude(full) && visitedFiles.Add(full)) count++;
                continue;
            }
            if (!Directory.Exists(full)) continue;
            Stack<string> pending = new();
            pending.Push(full);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                if (!visitedDirectories.Add(directory) || _exclusions.ShouldExclude(directory)) continue;
                try
                {
                    foreach (string file in Directory.EnumerateFiles(directory))
                        if (!_exclusions.ShouldExclude(file) && visitedFiles.Add(file)) count++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                if (!request.Recursive) continue;
                try
                {
                    foreach (string child in Directory.EnumerateDirectories(directory))
                    {
                        try
                        {
                            DirectoryInfo info = new(child);
                            if ((info.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        return count;
    }

    private static void AddSpecialFolder(ISet<string> paths, Environment.SpecialFolder folder) => AddDirectory(paths, Environment.GetFolderPath(folder));

    private static void AddDirectory(ISet<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) paths.Add(Path.GetFullPath(path));
    }

    private void UpdateStatus(Func<ScanStatus, ScanStatus> update)
    {
        lock (_statusGate) _status = update(_status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeCancellation?.Cancel();
        _activeCancellation?.Dispose();
        _operationGate.Dispose();
    }

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        private readonly Action<T> _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        public void Report(T value) => _handler(value);
    }
}
