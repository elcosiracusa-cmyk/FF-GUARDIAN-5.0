using System.Diagnostics;
using System.Text.Json;

namespace FFGuardian;

internal static class AutonomousSecurityEngine
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian");
    private static readonly string StatePath = Path.Combine(DataFolder, "autonomous-state.json");
    private static readonly string LogPath = Path.Combine(DataFolder, "Logs", "autonomous-engine.log");
    private static System.Threading.Timer? _timer;
    private static bool _started;

    public static void Start()
    {
        if (_started) return;
        _started = true;

        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        _timer = new System.Threading.Timer(
            async _ => await RunCycleAsync(),
            null,
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMinutes(15));

        Log("Motore autonomo FF GUARDIAN 5.4 avviato.");
    }

    private static async Task RunCycleAsync()
    {
        if (!await Gate.WaitAsync(0)) return;

        try
        {
            EngineState state = LoadState();
            DateTime now = DateTime.Now;

            state.LastProtectionCheck = now;
            state.LastProtectionSummary = await ReadProtectionSummaryAsync();

            if (state.LastSignatureUpdate is null || now - state.LastSignatureUpdate.Value >= TimeSpan.FromHours(24))
            {
                if (await RunDefenderCommandAsync("Update-MpSignature", "Aggiornamento firme"))
                    state.LastSignatureUpdate = now;
            }

            if (state.LastQuickScan is null || now - state.LastQuickScan.Value >= TimeSpan.FromDays(7))
            {
                if (await RunDefenderCommandAsync("Start-MpScan -ScanType QuickScan", "Scansione rapida programmata", true))
                    state.LastQuickScan = now;
            }

            state.LastSuccessfulCycle = now;
            state.LastError = null;
            SaveState(state);
            Log("Ciclo autonomo completato.");
        }
        catch (Exception ex)
        {
            EngineState state = LoadState();
            state.LastError = ex.Message;
            SaveState(state);
            Log($"Errore ciclo autonomo: {ex.Message}");
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string> ReadProtectionSummaryAsync()
    {
        const string command = "$s=Get-MpComputerStatus; 'Defender=' + $s.AntivirusEnabled + ';TempoReale=' + $s.RealTimeProtectionEnabled + ';Firme=' + $s.AntivirusSignatureVersion";
        CommandResult result = await RunPowerShellAsync(command);
        return result.ExitCode == 0 ? result.Output.Trim() : "Stato non disponibile";
    }

    private static async Task<bool> RunDefenderCommandAsync(string command, string operation, bool acceptBusy = false)
    {
        CommandResult result = await RunPowerShellAsync(command);
        string combined = $"{result.Output} {result.Error}";
        bool busy = combined.Contains("another scan", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("scansione", StringComparison.OrdinalIgnoreCase) &&
                    combined.Contains("corso", StringComparison.OrdinalIgnoreCase);

        if (result.ExitCode == 0 || acceptBusy && busy)
        {
            Log(busy ? $"{operation}: Defender sta già eseguendo una scansione." : $"{operation}: completata.");
            return true;
        }

        Log($"{operation}: non completata. {Clean(combined)}");
        return false;
    }

    private static async Task<CommandResult> RunPowerShellAsync(string command)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CommandResult(process.ExitCode, output, error);
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

    private static string Clean(string value)
    {
        string text = value.Replace("_x000D__x000A_", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= 500 ? text : text[..500];
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private sealed class EngineState
    {
        public DateTime? LastProtectionCheck { get; set; }
        public DateTime? LastSignatureUpdate { get; set; }
        public DateTime? LastQuickScan { get; set; }
        public DateTime? LastSuccessfulCycle { get; set; }
        public string? LastProtectionSummary { get; set; }
        public string? LastError { get; set; }
    }
}
