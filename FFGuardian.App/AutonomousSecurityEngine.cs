using System.Collections.Concurrent;
using System.Text.Json;

namespace FFGuardian;

internal enum ProtectionProfile
{
    Casa,
    Ufficio,
    MassimaProtezione
}

internal sealed record AutonomousSnapshot(
    int Score,
    string Status,
    ProtectionProfile Profile,
    DateTime? LastProtectionCheck,
    DateTime? LastSignatureUpdate,
    DateTime? LastQuickScan,
    DateTime? LastFullScan,
    int DownloadFilesChecked,
    string? LastError);

internal static class AutonomousSecurityEngine
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly DefenderService Defender = new();
    private static readonly ConcurrentDictionary<string, DateTime> RecentFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian");
    private static readonly string StatePath = Path.Combine(DataFolder, "autonomous-state-v6.json");
    private static readonly string LogPath = Path.Combine(DataFolder, "Logs", "autonomous-engine-v6.log");
    private static readonly string Downloads = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    private static readonly HashSet<string> RiskyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".zip", ".rar", ".7z", ".js", ".jse", ".vbs", ".vbe",
        ".bat", ".cmd", ".com", ".scr", ".ps1", ".psm1", ".hta", ".dll", ".iso", ".img"
    };

    private static System.Threading.Timer? _timer;
    private static FileSystemWatcher? _downloadWatcher;
    private static bool _started;

    public static event EventHandler<AutonomousSnapshot>? SnapshotChanged;

    public static void Start()
    {
        if (_started) return;
        _started = true;

        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        StartDownloadWatcher();

        _timer = new System.Threading.Timer(
            async _ => await RunCycleAsync(),
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMinutes(10));

        Log("Motore autonomo FF GUARDIAN 6.0 Advanced avviato.");
    }

    public static async Task<AutonomousSnapshot> ProtectNowAsync()
    {
        if (!await Gate.WaitAsync(0))
            return GetSnapshot("Un controllo è già in esecuzione.");

        try
        {
            EngineState state = LoadState();
            state.LastProtectionCheck = DateTime.Now;
            await Defender.UpdateAsync();
            state.LastSignatureUpdate = DateTime.Now;

            try
            {
                await Defender.QuickScanAsync();
                state.LastQuickScan = DateTime.Now;
            }
            catch (DefenderScanBusyException)
            {
                Log("Proteggi adesso: una scansione Defender era già in corso.");
            }

            SecurityState security = await Defender.GetStateAsync();
            ApplySecurityState(state, security);
            state.LastSuccessfulCycle = DateTime.Now;
            state.LastError = null;
            SaveState(state);
            Log($"Proteggi adesso completato. Punteggio {security.Score}/100.");
            return PublishSnapshot(state);
        }
        catch (Exception ex)
        {
            EngineState state = LoadState();
            state.LastError = Friendly(ex);
            SaveState(state);
            Log($"Proteggi adesso: {state.LastError}");
            return PublishSnapshot(state);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static AutonomousSnapshot GetSnapshot(string? statusOverride = null)
    {
        EngineState state = LoadState();
        return new AutonomousSnapshot(
            state.Score,
            statusOverride ?? state.Status,
            state.Profile,
            state.LastProtectionCheck,
            state.LastSignatureUpdate,
            state.LastQuickScan,
            state.LastFullScan,
            state.DownloadFilesChecked,
            state.LastError);
    }

    public static void SetProfile(ProtectionProfile profile)
    {
        EngineState state = LoadState();
        state.Profile = profile;
        SaveState(state);
        Log($"Profilo di protezione impostato: {ProfileName(profile)}.");
        PublishSnapshot(state);
    }

    private static async Task RunCycleAsync()
    {
        if (!await Gate.WaitAsync(0)) return;

        try
        {
            EngineState state = LoadState();
            DateTime now = DateTime.Now;
            SecurityState security = await Defender.GetStateAsync();
            state.LastProtectionCheck = now;
            ApplySecurityState(state, security);

            if (state.LastSignatureUpdate is null || now - state.LastSignatureUpdate.Value >= TimeSpan.FromHours(24))
            {
                await Defender.UpdateAsync();
                state.LastSignatureUpdate = now;
                Log("Aggiornamento firme automatico completato.");
            }

            TimeSpan quickInterval = state.Profile switch
            {
                ProtectionProfile.MassimaProtezione => TimeSpan.FromDays(3),
                ProtectionProfile.Ufficio => TimeSpan.FromDays(5),
                _ => TimeSpan.FromDays(7)
            };

            if (state.LastQuickScan is null || now - state.LastQuickScan.Value >= quickInterval)
            {
                try
                {
                    await Defender.QuickScanAsync();
                    state.LastQuickScan = now;
                    Log("Scansione rapida automatica avviata.");
                }
                catch (DefenderScanBusyException)
                {
                    Log("Scansione rapida automatica rimandata: Defender è occupato.");
                }
            }

            if (state.LastFullScan is null || now - state.LastFullScan.Value >= TimeSpan.FromDays(30))
            {
                try
                {
                    await Defender.FullScanAsync();
                    state.LastFullScan = now;
                    Log("Scansione completa mensile avviata.");
                }
                catch (DefenderScanBusyException)
                {
                    Log("Scansione completa mensile rimandata: Defender è occupato.");
                }
            }

            state.LastSuccessfulCycle = now;
            state.LastError = null;
            SaveState(state);
            PublishSnapshot(state);
            Log($"Ciclo autonomo completato. Punteggio {state.Score}/100.");
        }
        catch (Exception ex)
        {
            EngineState state = LoadState();
            state.LastError = Friendly(ex);
            state.Status = "ATTENZIONE";
            SaveState(state);
            PublishSnapshot(state);
            Log($"Errore ciclo autonomo: {state.LastError}");
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void StartDownloadWatcher()
    {
        try
        {
            Directory.CreateDirectory(Downloads);
            _downloadWatcher = new FileSystemWatcher(Downloads)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _downloadWatcher.Created += (_, e) => QueueDownloadedFile(e.FullPath);
            _downloadWatcher.Renamed += (_, e) => QueueDownloadedFile(e.FullPath);
            Log("Controllo Download attivo.");
        }
        catch (Exception ex)
        {
            Log($"Controllo Download non disponibile: {Friendly(ex)}");
        }
    }

    private static void QueueDownloadedFile(string path)
    {
        if (!RiskyExtensions.Contains(Path.GetExtension(path))) return;
        DateTime now = DateTime.UtcNow;
        if (RecentFiles.TryGetValue(path, out DateTime previous) && now - previous < TimeSpan.FromSeconds(30)) return;
        RecentFiles[path] = now;
        _ = Task.Run(async () => await ScanDownloadedFileAsync(path));
    }

    private static async Task ScanDownloadedFileAsync(string path)
    {
        await Task.Delay(2500);
        if (!File.Exists(path)) return;

        try
        {
            await Defender.CustomScanAsync(path);
            EngineState state = LoadState();
            state.DownloadFilesChecked++;
            SaveState(state);
            PublishSnapshot(state);
            Log($"File Download controllato: {Path.GetFileName(path)}");
        }
        catch (DefenderScanBusyException)
        {
            Log($"Controllo Download rimandato perché Defender è occupato: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            Log($"Controllo Download non riuscito ({Path.GetFileName(path)}): {Friendly(ex)}");
        }
    }

    private static void ApplySecurityState(EngineState state, SecurityState security)
    {
        state.Score = security.Score;
        state.Status = security.Score >= 90 ? "PROTETTO" : security.Score >= 70 ? "DA MIGLIORARE" : "ATTENZIONE";
        state.LastProtectionSummary = string.Join(" | ", security.Issues);
    }

    private static AutonomousSnapshot PublishSnapshot(EngineState state)
    {
        AutonomousSnapshot snapshot = new(
            state.Score,
            state.Status,
            state.Profile,
            state.LastProtectionCheck,
            state.LastSignatureUpdate,
            state.LastQuickScan,
            state.LastFullScan,
            state.DownloadFilesChecked,
            state.LastError);
        SnapshotChanged?.Invoke(null, snapshot);
        return snapshot;
    }

    private static EngineState LoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return new EngineState();
            return JsonSerializer.Deserialize<EngineState>(File.ReadAllText(StatePath)) ?? new EngineState();
        }
        catch
        {
            return new EngineState();
        }
    }

    private static void SaveState(EngineState state)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string Friendly(Exception ex)
    {
        (string message, _) = ErrorMessageFormatter.Format(ex);
        return message.Length <= 500 ? message : message[..500];
    }

    private static string ProfileName(ProtectionProfile profile) => profile switch
    {
        ProtectionProfile.Casa => "Casa",
        ProtectionProfile.Ufficio => "Ufficio",
        _ => "Massima protezione"
    };

    private sealed class EngineState
    {
        public int Score { get; set; } = 100;
        public string Status { get; set; } = "INIZIALIZZAZIONE";
        public ProtectionProfile Profile { get; set; } = ProtectionProfile.Casa;
        public DateTime? LastProtectionCheck { get; set; }
        public DateTime? LastSignatureUpdate { get; set; }
        public DateTime? LastQuickScan { get; set; }
        public DateTime? LastFullScan { get; set; }
        public DateTime? LastSuccessfulCycle { get; set; }
        public string? LastProtectionSummary { get; set; }
        public string? LastError { get; set; }
        public int DownloadFilesChecked { get; set; }
    }
}