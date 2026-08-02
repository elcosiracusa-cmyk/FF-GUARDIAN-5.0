using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace FFGuardian;

internal sealed record RansomIntelligenceAlert10(
    DateTime CreatedUtc,
    string Severity,
    int Score,
    string Status,
    string Folder,
    string TriggerPath,
    IReadOnlyList<string> Reasons);

internal sealed class RansomShieldIntelligence10 : IDisposable
{
    private static readonly HashSet<string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".locked", ".lock", ".crypted", ".crypt", ".encrypted", ".enc",
        ".wncry", ".wcry", ".ryk", ".ryuk", ".conti", ".revil", ".sodinokibi",
        ".lockbit", ".akira", ".blackcat", ".alphv", ".clop", ".play"
    };

    private static readonly string[] RansomNoteTokens =
    [
        "readme", "decrypt", "recover", "recovery", "restore", "ransom",
        "how_to", "how-to", "instructions", "your_files", "files_encrypted"
    ];

    private static readonly HashSet<string> CanaryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ffguardian-document-check.txt",
        ".ffguardian-photo-check.jpg",
        ".ffguardian-spreadsheet-check.xlsx"
    };

    private readonly RansomShieldSettings10 _settings;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, DateTime> _recentEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<(DateTime Time, int Score, string Path, string Reason)> _signals = new();
    private readonly System.Threading.Timer _cleanupTimer;
    private bool _running;

    public event EventHandler<RansomIntelligenceAlert10>? Alert;
    public bool IsRunning => _running;

    public RansomShieldIntelligence10(RansomShieldSettings10 settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _cleanupTimer = new System.Threading.Timer(Cleanup, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        Stop();
        if (!_settings.Enabled)
            return;

        foreach (string folder in _settings.GetProtectedFolders())
        {
            try
            {
                EnsureCanaries(folder);
                FileSystemWatcher watcher = new(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    InternalBufferSize = 32 * 1024,
                    EnableRaisingEvents = true
                };
                watcher.Created += (_, e) => Inspect("Created", e.FullPath, null);
                watcher.Changed += (_, e) => Inspect("Changed", e.FullPath, null);
                watcher.Deleted += (_, e) => Inspect("Deleted", e.FullPath, null);
                watcher.Renamed += (_, e) => Inspect("Renamed", e.FullPath, e.OldFullPath);
                watcher.Error += (_, e) => StabilityCoordinator82.WriteStabilityLog(
                    e.GetException() ?? new IOException("Overflow del monitor Ransom Shield Intelligence."));
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }

        _running = _watchers.Count > 0;
        _cleanupTimer.Change(1000, 1000);
    }

    public void Restart() => Start();

    private void Inspect(string kind, string path, string? oldPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                return;

            string key = $"{kind}|{path}";
            DateTime now = DateTime.UtcNow;
            if (_recentEvents.TryGetValue(key, out DateTime previous) && (now - previous).TotalMilliseconds < 750)
                return;
            _recentEvents[key] = now;

            List<(int Score, string Reason)> reasons = [];
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(path);

            if (CanaryNames.Contains(fileName) && kind is "Changed" or "Deleted" or "Renamed")
                reasons.Add((100, "File-esca FFGuardian modificato"));

            if (SuspiciousExtensions.Contains(extension))
                reasons.Add((55, $"Estensione associata a cifratura: {extension}"));

            string normalized = fileName.Replace(' ', '_').ToLowerInvariant();
            if (RansomNoteTokens.Count(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase)) >= 2)
                reasons.Add((45, "Possibile nota di riscatto"));

            if (kind == "Renamed" && !string.IsNullOrWhiteSpace(oldPath))
            {
                string oldExtension = Path.GetExtension(oldPath);
                if (!string.Equals(oldExtension, extension, StringComparison.OrdinalIgnoreCase) &&
                    (SuspiciousExtensions.Contains(extension) || string.IsNullOrWhiteSpace(extension)))
                    reasons.Add((35, "Cambio anomalo dell’estensione"));
            }

            if (kind == "Deleted")
                reasons.Add((8, "Eliminazione in cartella protetta"));
            else if (kind == "Renamed")
                reasons.Add((10, "Rinomina in cartella protetta"));

            foreach ((int score, string reason) in reasons)
                _signals.Enqueue((now, score, path, reason));

            Evaluate(now, path);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private void Evaluate(DateTime now, string triggerPath)
    {
        DateTime cutoff = now.AddSeconds(-Math.Clamp(_settings.WindowSeconds, 5, 120));
        while (_signals.TryPeek(out var signal) && signal.Time < cutoff)
            _signals.TryDequeue(out _);

        var snapshot = _signals.ToArray().Where(signal => signal.Time >= cutoff).ToArray();
        if (snapshot.Length == 0)
            return;

        int totalScore = Math.Min(100, snapshot.Sum(signal => signal.Score));
        bool canaryTriggered = snapshot.Any(signal => signal.Reason.Contains("File-esca", StringComparison.OrdinalIgnoreCase));
        bool highConfidencePattern = snapshot.Count(signal => signal.Score >= 35) >= 2;
        if (!canaryTriggered && !highConfidencePattern && totalScore < 70)
            return;

        string alertKey = $"alert|{Path.GetDirectoryName(triggerPath)}";
        if (_recentEvents.TryGetValue(alertKey, out DateTime previousAlert) && (now - previousAlert).TotalSeconds < 20)
            return;
        _recentEvents[alertKey] = now;

        string[] reasons = snapshot.Select(signal => signal.Reason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        string severity = canaryTriggered || totalScore >= 90 ? "CRITICA" : "ALTA";
        string status = canaryTriggered
            ? "File-esca modificato: possibile cifratura non autorizzata"
            : "Indicatori multipli compatibili con attività ransomware";
        string folder = Path.GetDirectoryName(triggerPath) ?? "Cartella protetta";

        RansomIntelligenceAlert10 alert = new(now, severity, totalScore, status, folder, triggerPath, reasons);
        WriteEvent(alert);
        Alert?.Invoke(this, alert);
    }

    private static void EnsureCanaries(string folder)
    {
        foreach (string name in CanaryNames)
        {
            string path = Path.Combine(folder, name);
            if (File.Exists(path))
                continue;

            try
            {
                byte[] content = name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    ? [0xFF, 0xD8, 0xFF, 0xD9]
                    : Encoding.UTF8.GetBytes("FFGuardian protected file. Do not modify or delete.\n");
                File.WriteAllBytes(path, content);
                File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }
    }

    private static void WriteEvent(RansomIntelligenceAlert10 alert)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "RansomShield");
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "intelligence-events.jsonl"),
            JsonSerializer.Serialize(alert) + Environment.NewLine);
        StabilityCoordinator82.WriteInformationLog(
            $"RANSOM SHIELD INTELLIGENCE: {alert.Severity} — {alert.Score}/100 — {alert.Status} — {alert.TriggerPath}");
    }

    private void Cleanup(object? state)
    {
        DateTime cutoff = DateTime.UtcNow.AddMinutes(-5);
        foreach ((string key, DateTime time) in _recentEvents)
        {
            if (time < cutoff)
                _recentEvents.TryRemove(key, out _);
        }
    }

    public void Stop()
    {
        _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        _recentEvents.Clear();
        while (_signals.TryDequeue(out _)) { }
        _running = false;
    }

    public void Dispose()
    {
        Stop();
        _cleanupTimer.Dispose();
    }
}
