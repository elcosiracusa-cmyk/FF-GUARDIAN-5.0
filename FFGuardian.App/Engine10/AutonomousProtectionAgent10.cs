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
        string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

        return new ProtectionAgentOptions10(
            new[] { downloads, desktop, startup, commonStartup }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromMilliseconds(750),
            512,
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
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf", ".lnk", ".zip"
    };

    private readonly FFGuardianEngine10 _engine;
    private readonly ProtectionAgentOptions10 _options;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly CancellationTokenSource _stop = new();
    private Task? _worker;
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
        {
            string path;
            try { path = Path.GetFullPath(configuredPath); }
            catch { continue; }
            if (!Directory.Exists(path))
                continue;

            FileSystemWatcher watcher = new(path)
            {
                IncludeSubdirectories = _options.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                Filter = "*.*",
                InternalBufferSize = 32 * 1024,
                EnableRaisingEvents = false
            };
            watcher.Created += OnFileEvent;
            watcher.Changed += OnFileEvent;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }

        _worker = Task.Run(() => WorkerAsync(_stop.Token));
        Raise(string.Empty, "Started", null, $"Monitoraggio attivo su {_watchers.Count} cartelle.");
    }

    internal bool QueueFileForTest(string path) => TryQueue(path);

    private void OnFileEvent(object sender, FileSystemEventArgs e) => TryQueue(e.FullPath);
    private void OnRenamed(object sender, RenamedEventArgs e) => TryQueue(e.FullPath);

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        Raise(string.Empty, "WatcherError", null, $"Errore monitoraggio: {e.GetException().Message}");

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

        _recent[fullPath] = now;
        TrimRecent(now);
        bool written = _queue.Writer.TryWrite(fullPath);
        if (!written)
            Raise(fullPath, "QueueRejected", null, "Coda monitoraggio piena.");
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
                        ThreatVerdict10.Malicious => "Minaccia rilevata. È richiesta conferma per la remediation.",
                        ThreatVerdict10.Suspicious => "File sospetto rilevato. È richiesta revisione.",
                        ThreatVerdict10.Error => "Scansione non completata.",
                        _ => "Scansione automatica completata."
                    };
                    Raise(path, "Scanned", result, status);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("operazione", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    _queue.Writer.TryWrite(path);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Raise(path, "ScanError", null, ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> WaitForStableFileAsync(string path, CancellationToken cancellationToken)
    {
        long previousLength = -1;
        DateTime previousWrite = DateTime.MinValue;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                return false;

            try
            {
                FileInfo info = new(path);
                long length = info.Length;
                DateTime write = info.LastWriteTimeUtc;
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.EnableRaisingEvents = false;
        _queue.Writer.TryComplete();
        _stop.Cancel();
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        Raise(string.Empty, "Stopped", null, "Monitoraggio arrestato.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        await StopAsync().ConfigureAwait(false);
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        _stop.Dispose();
    }
}
