using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FFGuardian.Engine10;

internal sealed record ProtectionAgentOptions10(
    IReadOnlyList<string> MonitoredFolders,
    TimeSpan DuplicateWindow,
    TimeSpan StabilityDelay,
    int MaximumQueueLength,
    bool IncludeSubdirectories)
{
    public static ProtectionAgentOptions10 CreateDefault()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(userProfile, "Downloads");
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        string temp = Path.GetTempPath();

        return new ProtectionAgentOptions10(
            new[] { downloads, desktop, documents, startup, commonStartup, temp }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromMilliseconds(750),
            1024,
            true);
    }
}

internal sealed record ProtectionAgentEvent10(
    string Path,
    string EventType,
    FileScanResult10? ScanResult,
    string Status,
    DateTime TimestampUtc);

internal sealed class AutonomousProtectionAgent10 : IAsyncDisposable
{
    private static readonly HashSet<string> WatchedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".sys", ".msi", ".msix",
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf", ".lnk", ".zip",
        ".doc", ".docm", ".docx", ".xls", ".xlsm", ".xlsx", ".ppt", ".pptm", ".pptx",
        ".iso", ".img", ".jar"
    };

    private readonly FFGuardianEngine10 _engine;
    private readonly ProtectionAgentOptions10 _options;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly CancellationTokenSource _stop = new();
    private Task? _worker;
    private Timer? _driveRefreshTimer;
    private int _started;
    private int _disposed;

    public AutonomousProtectionAgent10(FFGuardianEngine10 engine, ProtectionAgentOptions10? options = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _options = options ?? ProtectionAgentOptions10.CreateDefault();
        if (_options.MaximumQueueLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "La capacità della coda deve essere positiva.");

        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.MaximumQueueLength)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public event EventHandler<ProtectionAgentEvent10>? Activity;
    public bool IsRunning => Volatile.Read(ref _started) == 1 && !_stop.IsCancellationRequested;
    public int MonitoredFolderCount => _watchers.Count;

    public void Start()
    {
        ThrowIfDisposed();
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        foreach (string configuredPath in _options.MonitoredFolders)
            TryAddWatcher(configuredPath, "Cartella protetta");

        RefreshRemovableDriveWatchers();
        _driveRefreshTimer = new Timer(
            _ => RefreshRemovableDriveWatchers(),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15));

        _worker = Task.Run(() => WorkerAsync(_stop.Token));
        Raise(string.Empty, "Started", null,
            $"Protezione in tempo reale attiva su {_watchers.Count} percorsi, inclusi supporti USB disponibili.");
    }

    internal bool QueueFileForTest(string path) => TryQueue(path);

    private void TryAddWatcher(string configuredPath, string source)
    {
        string path;
        try { path = Path.GetFullPath(configuredPath); }
        catch { return; }
        if (!Directory.Exists(path))
            return;

        lock (_watchers)
        {
            if (_watchers.Any(watcher => string.Equals(watcher.Path, path, StringComparison.OrdinalIgnoreCase)))
                return;

            try
            {
                FileSystemWatcher watcher = new(path)
                {
                    IncludeSubdirectories = _options.IncludeSubdirectories,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                   NotifyFilters.CreationTime | NotifyFilters.Size,
                    Filter = "*.*",
                    InternalBufferSize = 64 * 1024,
                    EnableRaisingEvents = false
                };
                watcher.Created += OnFileEvent;
                watcher.Changed += OnFileEvent;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
                Raise(path, "WatcherAdded", null, $"{source}: {path}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Raise(path, "WatcherUnavailable", null, ex.Message);
            }
        }
    }

    private void RefreshRemovableDriveWatchers()
    {
        if (!IsRunning)
            return;

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Removable)
                        TryAddWatcher(drive.RootDirectory.FullName, "Supporto USB protetto");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex)
        {
            Raise(string.Empty, "DriveEnumerationError", null, ex.Message);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => TryQueue(e.FullPath);
    private void OnRenamed(object sender, RenamedEventArgs e) => TryQueue(e.FullPath);

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        string path = sender is FileSystemWatcher watcher ? watcher.Path : string.Empty;
        Raise(path, "WatcherError", null, $"Errore monitoraggio: {e.GetException().Message}");
    }

    private bool TryQueue(string path)
    {
        if (!IsRunning || string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch { return false; }

        if (!WatchedExtensions.Contains(Path.GetExtension(fullPath)))
            return false;

        DateTime now = DateTime.UtcNow;
        if (_recent.TryGetValue(fullPath, out DateTime previous) && now - previous < _options.DuplicateWindow)
            return false;
        if (!_queued.TryAdd(fullPath, 0))
            return false;

        _recent[fullPath] = now;
        TrimRecent(now);
        bool written = _queue.Writer.TryWrite(fullPath);
        if (!written)
        {
            _queued.TryRemove(fullPath, out _);
            Raise(fullPath, "QueueRejected", null, "Coda monitoraggio piena.");
        }
        else
        {
            Raise(fullPath, "Queued", null, "File accodato per la scansione in tempo reale.");
        }
        return written;
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (string path in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    if (!await WaitForStableFileAsync(path, cancellationToken).ConfigureAwait(false))
                    {
                        Raise(path, "Skipped", null, "File non disponibile o non stabilizzato.");
                        continue;
                    }

                    FileScanResult10 result = await _engine.ScanFileAsync(path, cancellationToken).ConfigureAwait(false);
                    string status = result.Verdict switch
                    {
                        ThreatVerdict10.Malicious =>
                            $"MINACCIA RILEVATA: {result.DetectionName}. Quarantena disponibile con conferma.",
                        ThreatVerdict10.Suspicious =>
                            $"File sospetto: {result.DetectionName}. Revisione consigliata.",
                        ThreatVerdict10.Error => "Scansione non completata.",
                        _ => "Scansione automatica completata."
                    };
                    Raise(path, result.Verdict == ThreatVerdict10.Malicious ? "ThreatDetected" : "Scanned", result, status);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("operazione", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    _queue.Writer.TryWrite(path);
                    continue;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StabilityCoordinator82.WriteStabilityLog(ex);
                    Raise(path, "ScanError", null, ex.Message);
                }
                finally
                {
                    _queued.TryRemove(path, out _);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Raise(string.Empty, "WorkerError", null, ex.Message);
        }
    }

    private async Task<bool> WaitForStableFileAsync(string path, CancellationToken cancellationToken)
    {
        long previousLength = -1;
        DateTime previousWrite = DateTime.MinValue;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                return false;

            try
            {
                FileInfo info = new(path);
                long length = info.Length;
                DateTime write = info.LastWriteTimeUtc;
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (length == previousLength && write == previousWrite)
                    return true;
                previousLength = length;
                previousWrite = write;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            await Task.Delay(_options.StabilityDelay, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private void TrimRecent(DateTime now)
    {
        if (_recent.Count < _options.MaximumQueueLength * 2)
            return;
        DateTime threshold = now - _options.DuplicateWindow - _options.DuplicateWindow;
        foreach ((string key, DateTime value) in _recent)
            if (value < threshold)
                _recent.TryRemove(key, out _);
    }

    private void Raise(string path, string eventType, FileScanResult10? result, string status)
    {
        try { Activity?.Invoke(this, new ProtectionAgentEvent10(path, eventType, result, status, DateTime.UtcNow)); }
        catch { }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return;

        _driveRefreshTimer?.Dispose();
        _driveRefreshTimer = null;
        lock (_watchers)
        {
            foreach (FileSystemWatcher watcher in _watchers)
                watcher.EnableRaisingEvents = false;
        }
        _queue.Writer.TryComplete();
        _stop.Cancel();
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        Raise(string.Empty, "Stopped", null, "Protezione in tempo reale arrestata.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        await StopAsync().ConfigureAwait(false);
        lock (_watchers)
        {
            foreach (FileSystemWatcher watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
        }
        _stop.Dispose();
    }
}
