using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian;

internal sealed record RansomMaximumAlert10(
    DateTime CreatedUtc,
    string Severity,
    int Score,
    string Folder,
    string TriggerPath,
    int ChangedFiles,
    int RenamedFiles,
    int DeletedFiles,
    int HighEntropyFiles,
    string SuspectedProcess,
    IReadOnlyList<string> Reasons);

internal sealed class RansomShieldMaximum10 : IDisposable
{
    private static readonly HashSet<string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".locked", ".lock", ".crypted", ".crypt", ".encrypted", ".enc",
        ".wncry", ".wcry", ".ryk", ".ryuk", ".conti", ".revil", ".sodinokibi",
        ".lockbit", ".akira", ".blackcat", ".alphv", ".clop", ".play", ".mallox"
    };

    private static readonly string[] RansomNoteTokens =
    {
        "decrypt", "ransom", "recover", "restore", "your_files", "files_encrypted",
        "how_to_decrypt", "payment", "bitcoin", "tor_browser"
    };

    private static readonly string[] CanaryNames =
    {
        ".ffguardian-max-document.txt",
        ".ffguardian-max-photo.jpg",
        ".ffguardian-max-sheet.xlsx"
    };

    private readonly RansomShieldSettings10 _settings;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentQueue<RansomSignal10> _signals = new();
    private readonly ConcurrentDictionary<string, DateTime> _dedup = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CanaryState10> _canaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stop = new();
    private readonly System.Threading.Timer _evaluationTimer;
    private readonly SemaphoreSlim _analysisGate = new(2, 2);
    private bool _started;
    private DateTime _lastAlertUtc = DateTime.MinValue;

    public event EventHandler<RansomMaximumAlert10>? Alert;
    public bool IsRunning => _started;
    public int ProtectedFolderCount => _watchers.Count;

    public RansomShieldMaximum10(RansomShieldSettings10 settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _evaluationTimer = new System.Threading.Timer(Evaluate, null, Timeout.Infinite, Timeout.Infinite);
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
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                   NotifyFilters.Size | NotifyFilters.CreationTime,
                    InternalBufferSize = 64 * 1024,
                    Filter = "*.*",
                    EnableRaisingEvents = false
                };
                watcher.Created += (_, e) => QueueInspection("Created", e.FullPath, null);
                watcher.Changed += (_, e) => QueueInspection("Changed", e.FullPath, null);
                watcher.Deleted += (_, e) => QueueInspection("Deleted", e.FullPath, null);
                watcher.Renamed += (_, e) => QueueInspection("Renamed", e.FullPath, e.OldFullPath);
                watcher.Error += (_, e) => StabilityCoordinator82.WriteStabilityLog(
                    e.GetException() ?? new IOException("Overflow Ransom Shield Maximum."));
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }

        _started = _watchers.Count > 0;
        if (_started)
            _evaluationTimer.Change(1000, 1000);
    }

    public void Restart() => Start();

    private void QueueInspection(string kind, string path, string? oldPath)
    {
        if (!_started || string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
            return;

        string key = kind + "|" + path;
        DateTime now = DateTime.UtcNow;
        if (_dedup.TryGetValue(key, out DateTime previous) && now - previous < TimeSpan.FromMilliseconds(600))
            return;
        _dedup[key] = now;

        _ = Task.Run(() => InspectAsync(kind, path, oldPath, _stop.Token));
    }

    private async Task InspectAsync(string kind, string path, string? oldPath, CancellationToken cancellationToken)
    {
        await _analysisGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTime now = DateTime.UtcNow;
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(path);
            int score = kind switch
            {
                "Deleted" => 8,
                "Renamed" => 10,
                "Changed" => 4,
                _ => 2
            };
            List<string> reasons = [];
            bool highEntropy = false;

            if (IsCanary(path) && kind is "Changed" or "Deleted" or "Renamed")
            {
                score += 100;
                reasons.Add("File-esca FFGuardian Maximum modificato");
            }

            if (SuspiciousExtensions.Contains(extension))
            {
                score += 60;
                reasons.Add($"Estensione tipica di cifratura: {extension}");
            }

            if (kind == "Renamed" && !string.IsNullOrWhiteSpace(oldPath))
            {
                string oldExtension = Path.GetExtension(oldPath);
                if (!string.Equals(oldExtension, extension, StringComparison.OrdinalIgnoreCase))
                {
                    score += 18;
                    reasons.Add($"Cambio estensione: {oldExtension} → {extension}");
                }
            }

            string normalizedName = fileName.Replace(' ', '_').ToLowerInvariant();
            int ransomTokens = RansomNoteTokens.Count(token =>
                normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (ransomTokens >= 2 ||
                normalizedName.Contains("readme", StringComparison.OrdinalIgnoreCase) && ransomTokens >= 1)
            {
                score += 55;
                reasons.Add("Nome compatibile con nota di riscatto");
            }

            if (kind is "Created" or "Changed" or "Renamed" && File.Exists(path))
            {
                try
                {
                    FileInfo info = new(path);
                    if (info.Length is >= 4096 and <= 32L * 1024 * 1024)
                    {
                        double entropy = await EstimateEntropyAsync(path, cancellationToken).ConfigureAwait(false);
                        if (entropy >= 7.75)
                        {
                            highEntropy = true;
                            score += 28;
                            reasons.Add($"Entropia elevata {entropy:F2}, possibile cifratura");
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            _signals.Enqueue(new RansomSignal10(
                now,
                Math.Clamp(score, 0, 100),
                kind,
                path,
                highEntropy,
                reasons));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
        finally
        {
            _analysisGate.Release();
        }
    }

    private void Evaluate(object? state)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            DateTime cutoff = now.AddSeconds(-Math.Clamp(_settings.WindowSeconds, 5, 120));
            while (_signals.TryPeek(out RansomSignal10? old) && old.Time < cutoff)
                _signals.TryDequeue(out _);

            RansomSignal10[] snapshot = _signals.ToArray()
                .Where(signal => signal.Time >= cutoff)
                .ToArray();
            if (snapshot.Length == 0)
                return;

            int changes = snapshot.Count(signal => signal.Kind is "Changed" or "Created");
            int renames = snapshot.Count(signal => signal.Kind == "Renamed");
            int deletes = snapshot.Count(signal => signal.Kind == "Deleted");
            int highEntropy = snapshot.Count(signal => signal.HighEntropy);
            bool canary = snapshot.Any(signal => signal.Reasons.Any(reason =>
                reason.Contains("File-esca", StringComparison.OrdinalIgnoreCase)));
            bool ransomNote = snapshot.Any(signal => signal.Reasons.Any(reason =>
                reason.Contains("nota di riscatto", StringComparison.OrdinalIgnoreCase)));
            int suspiciousExtensions = snapshot.Count(signal => signal.Reasons.Any(reason =>
                reason.Contains("Estensione tipica", StringComparison.OrdinalIgnoreCase)));

            int burstScore = snapshot.Length >= _settings.ChangeThreshold
                ? Math.Min(50, 20 + snapshot.Length - _settings.ChangeThreshold)
                : 0;
            int score = Math.Clamp(
                snapshot.Sum(signal => Math.Min(25, signal.Score)) + burstScore,
                0,
                100);

            bool criticalPattern = canary ||
                ransomNote && (renames + deletes + highEntropy >= 3) ||
                suspiciousExtensions >= 3 ||
                highEntropy >= 5 ||
                snapshot.Length >= _settings.ChangeThreshold && renames + deletes >= 5;

            if (!criticalPattern && score < 80)
                return;
            if (now - _lastAlertUtc < TimeSpan.FromSeconds(20))
                return;
            _lastAlertUtc = now;

            string triggerPath = snapshot.OrderByDescending(signal => signal.Score).First().Path;
            string folder = snapshot.GroupBy(signal => Path.GetDirectoryName(signal.Path) ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .FirstOrDefault()?.Key ?? "Cartella protetta";
            string suspectedProcess = FindSuspectedProcess(now);
            string severity = canary || score >= 95 ? "CRITICA" : "ALTA";
            string[] reasons = snapshot.SelectMany(signal => signal.Reasons)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();

            RansomMaximumAlert10 alert = new(
                now,
                severity,
                score,
                folder,
                triggerPath,
                changes,
                renames,
                deletes,
                highEntropy,
                suspectedProcess,
                reasons);

            WriteEvidence(alert, snapshot);
            try { Alert?.Invoke(this, alert); }
            catch { }
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static string FindSuspectedProcess(DateTime now)
    {
        try
        {
            string temp = Path.GetFullPath(Path.GetTempPath());
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            Process? candidate = Process.GetProcesses()
                .Select(process =>
                {
                    try
                    {
                        string path = process.MainModule?.FileName ?? string.Empty;
                        DateTime started = process.StartTime.ToUniversalTime();
                        bool recent = now - started < TimeSpan.FromMinutes(3);
                        bool untrustedLocation = path.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith(downloads, StringComparison.OrdinalIgnoreCase);
                        return recent && untrustedLocation ? process : null;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .FirstOrDefault(process => process is not null);

            if (candidate is null)
                return "Non attribuito — revisione manuale richiesta";
            try
            {
                return $"{candidate.ProcessName} (PID {candidate.Id}) — candidato, non confermato";
            }
            finally
            {
                candidate.Dispose();
            }
        }
        catch
        {
            return "Non attribuito — revisione manuale richiesta";
        }
    }

    private void EnsureCanaries(string folder)
    {
        foreach (string name in CanaryNames)
        {
            string path = Path.Combine(folder, name);
            try
            {
                if (!File.Exists(path))
                {
                    byte[] content = name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        ? new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }
                        : Encoding.UTF8.GetBytes("FFGuardian Maximum protected canary. Do not edit.\n");
                    File.WriteAllBytes(path, content);
                    File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
                }

                _canaries[path] = new CanaryState10(
                    ComputeSha256(path),
                    File.GetLastWriteTimeUtc(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }
    }

    private bool IsCanary(string path) =>
        _canaries.ContainsKey(path) || CanaryNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private static async Task<double> EstimateEntropyAsync(string path, CancellationToken cancellationToken)
    {
        const int maximumSample = 1024 * 1024;
        byte[] buffer = new byte[maximumSample];
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(total, buffer.Length - total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }
        if (total == 0)
            return 0;

        int[] counts = new int[256];
        for (int index = 0; index < total; index++)
            counts[buffer[index]]++;
        double entropy = 0;
        foreach (int count in counts)
        {
            if (count == 0)
                continue;
            double probability = (double)count / total;
            entropy -= probability * Math.Log2(probability);
        }
        return entropy;
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteEvidence(RansomMaximumAlert10 alert, IReadOnlyList<RansomSignal10> snapshot)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "RansomShield", "Maximum");
        Directory.CreateDirectory(folder);

        string id = alert.CreatedUtc.ToString("yyyyMMdd-HHmmss-fff");
        string evidencePath = Path.Combine(folder, $"ransom-evidence-{id}.json");
        string summaryPath = Path.Combine(folder, "maximum-events.jsonl");

        object evidence = new
        {
            Alert = alert,
            Signals = snapshot.TakeLast(250).ToArray(),
            Machine = Environment.MachineName,
            User = Environment.UserName,
            Os = Environment.OSVersion.ToString()
        };
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions { WriteIndented = true }));
        File.AppendAllText(summaryPath, JsonSerializer.Serialize(alert) + Environment.NewLine);
        StabilityCoordinator82.WriteInformationLog(
            $"RANSOM SHIELD MAXIMUM: {alert.Severity} {alert.Score}/100 — {alert.Folder} — {alert.SuspectedProcess}");
    }

    public void Stop()
    {
        _started = false;
        _evaluationTimer.Change(Timeout.Infinite, Timeout.Infinite);
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        while (_signals.TryDequeue(out _)) { }
        _dedup.Clear();
        _canaries.Clear();
    }

    public void Dispose()
    {
        Stop();
        _stop.Cancel();
        _evaluationTimer.Dispose();
        _analysisGate.Dispose();
        _stop.Dispose();
    }

    internal static int CalculateTestScore(
        int events,
        int renames,
        int deletes,
        int highEntropy,
        int suspiciousExtensions,
        bool canary,
        bool ransomNote,
        int threshold)
    {
        int score = 0;
        if (events >= threshold) score += Math.Min(50, 20 + events - threshold);
        score += Math.Min(30, renames * 4);
        score += Math.Min(30, deletes * 4);
        score += Math.Min(40, highEntropy * 8);
        score += Math.Min(45, suspiciousExtensions * 15);
        if (ransomNote) score += 35;
        if (canary) score += 100;
        return Math.Clamp(score, 0, 100);
    }

    private sealed record RansomSignal10(
        DateTime Time,
        int Score,
        string Kind,
        string Path,
        bool HighEntropy,
        IReadOnlyList<string> Reasons);

    private sealed record CanaryState10(string Sha256, DateTime LastWriteUtc);
}
