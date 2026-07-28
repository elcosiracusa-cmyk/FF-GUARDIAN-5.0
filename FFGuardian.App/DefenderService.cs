using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal sealed class DefenderService
{
    private const string ScanBusyToken = "FFG_SCAN_BUSY";

    public async Task<SecurityState> GetStateAsync()
    {
        string json = await RunPsAsync("$s=Get-MpComputerStatus;$p=Get-MpPreference;$f=@(Get-NetFirewallProfile|Select Name,Enabled);[pscustomobject]@{Antivirus=$s.AntivirusEnabled;Realtime=$s.RealTimeProtectionEnabled;SignaturesOld=$s.DefenderSignaturesOutOfDate;SignatureVersion=$s.AntivirusSignatureVersion;EngineVersion=$s.AMEngineVersion;QuickScan=$s.QuickScanEndTime;FullScan=$s.FullScanEndTime;Firewall=$f;PUA=$p.PUAProtection;Network=$p.EnableNetworkProtection;CFA=$p.EnableControlledFolderAccess}|ConvertTo-Json -Depth 5 -Compress");
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            throw new InvalidOperationException("Microsoft Defender non ha restituito dati leggibili.");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        bool firewall = false;

        if (root.TryGetProperty("Firewall", out JsonElement fw))
        {
            if (fw.ValueKind == JsonValueKind.Array)
            {
                JsonElement[] profiles = fw.EnumerateArray().ToArray();
                firewall = profiles.Length > 0 && profiles.All(x => ReadBool(x, "Enabled"));
            }
            else if (fw.ValueKind == JsonValueKind.Object)
            {
                firewall = ReadBool(fw, "Enabled");
            }
        }

        bool antivirus = ReadBool(root, "Antivirus");
        bool realtime = ReadBool(root, "Realtime");
        bool signatures = !ReadBool(root, "SignaturesOld", true);
        bool pua = ReadInt(root, "PUA") == 1;
        bool network = ReadInt(root, "Network") == 1;
        bool cfa = ReadInt(root, "CFA") != 0;

        List<string> issues = [];
        if (!antivirus) issues.Add("Microsoft Defender non è attivo.");
        if (!realtime) issues.Add("Protezione in tempo reale disattivata.");
        if (!signatures) issues.Add("Definizioni antivirus da aggiornare.");
        if (!firewall) issues.Add("Uno o più profili Firewall sono disattivati.");
        if (!pua) issues.Add("Protezione PUA non in blocco.");
        if (!network) issues.Add("Protezione rete non in blocco.");
        if (!cfa) issues.Add("Ransomware Guard non attivo.");

        return new SecurityState(
            antivirus,
            realtime,
            signatures,
            firewall,
            pua,
            network,
            cfa,
            ReadString(root, "SignatureVersion", "-"),
            ReadString(root, "EngineVersion", "-"),
            FormatDate(ReadString(root, "QuickScan", "Non disponibile")),
            FormatDate(ReadString(root, "FullScan", "Non disponibile")),
            issues);
    }

    public Task QuickScanAsync() => StartScanSafelyAsync("QuickScan");
    public Task FullScanAsync() => StartScanSafelyAsync("FullScan");
    public Task CustomScanAsync(string path) => StartScanSafelyAsync("CustomScan", path);
    public Task UpdateAsync() => RunPsAsync("Update-MpSignature");
    public void OpenWindowsSecurity() => Process.Start(new ProcessStartInfo("windowsdefender:") { UseShellExecute = true });

    private static async Task StartScanSafelyAsync(string scanType, string? path = null)
    {
        string escapedPath = path?.Replace("'", "''") ?? string.Empty;
        string startCommand = scanType == "CustomScan"
            ? $"Start-MpScan -ScanType CustomScan -ScanPath '{escapedPath}'"
            : $"Start-MpScan -ScanType {scanType}";

        string command =
            "$status=Get-MpComputerStatus;" +
            "$busy=$false;" +
            "if($status.PSObject.Properties.Name -contains 'ScanInProgress'){$busy=[bool]$status.ScanInProgress};" +
            $"if($busy){{Write-Output '{ScanBusyToken}'}}else{{{startCommand};Write-Output 'FFG_SCAN_STARTED'}}";

        string result = await RunPsAsync(command);
        if (result.Contains(ScanBusyToken, StringComparison.OrdinalIgnoreCase))
            throw new DefenderScanBusyException();
    }

    public async Task<List<ThreatRow>> GetThreatsAsync()
    {
        string json = await RunPsAsync("@(Get-MpThreatDetection|Sort InitialDetectionTime -Descending|Select -First 100 ThreatID,InitialDetectionTime,ActionSuccess,Resources)|ConvertTo-Json -Depth 6 -Compress");
        if (string.IsNullOrWhiteSpace(json) || json == "null") return [];
        using JsonDocument doc = JsonDocument.Parse(json);
        IEnumerable<JsonElement> items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : new[] { doc.RootElement };
        return items.Select(x => new ThreatRow(
            ReadString(x, "ThreatID", "-"),
            FormatDate(ReadString(x, "InitialDetectionTime", "-")),
            ReadBool(x, "ActionSuccess") ? "Corretta" : "Da verificare",
            ReadString(x, "Resources", "-"))).ToList();
    }

    public async Task<List<EventRow>> GetOperationalEventsAsync()
    {
        string json = await RunPsAsync("@(Get-WinEvent -LogName 'Microsoft-Windows-Windows Defender/Operational' -MaxEvents 80 -ErrorAction SilentlyContinue|Select TimeCreated,Id,LevelDisplayName,Message)|ConvertTo-Json -Depth 4 -Compress");
        if (string.IsNullOrWhiteSpace(json) || json == "null") return [];
        using JsonDocument doc = JsonDocument.Parse(json);
        IEnumerable<JsonElement> items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : new[] { doc.RootElement };
        return items.Select(x => new EventRow(
            FormatDate(ReadString(x, "TimeCreated", "-")),
            ReadString(x, "Id", "-"),
            ReadString(x, "LevelDisplayName", "-"),
            ReadString(x, "Message", "-").Replace("\r", " ").Replace("\n", " "))).ToList();
    }

    private static string FormatDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "-" or "Non disponibile") return value;
        Match match = Regex.Match(value, @"/Date\(([-]?\d+)");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long milliseconds))
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime.ToString("dd/MM/yyyy HH:mm"); }
            catch { return value; }
        }
        if (DateTime.TryParse(value, out DateTime date)) return date.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        return value;
    }

    private static bool ReadBool(JsonElement parent, string propertyName, bool defaultValue = false)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)) return defaultValue;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out int number) => number != 0,
            JsonValueKind.String when bool.TryParse(value.GetString(), out bool boolean) => boolean,
            JsonValueKind.String when int.TryParse(value.GetString(), out int number) => number != 0,
            _ => defaultValue
        };
    }

    private static int ReadInt(JsonElement parent, string propertyName, int defaultValue = 0)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)) return defaultValue;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        return int.TryParse(value.ToString(), out number) ? number : defaultValue;
    }

    private static string ReadString(JsonElement parent, string propertyName, string defaultValue = "")
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return defaultValue;
        return value.ToString();
    }

    private static async Task<string> RunPsAsync(string command)
    {
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';" + command));
        ProcessStartInfo psi = new("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Impossibile avviare PowerShell.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Comando Defender non riuscito." : error.Trim());
        return output.Trim();
    }
}

internal sealed class DefenderScanBusyException : InvalidOperationException
{
    public DefenderScanBusyException()
        : base("Microsoft Defender sta già eseguendo una scansione. Attendi il completamento e riprova.")
    {
    }
}

internal sealed record SecurityState(
    bool Antivirus,
    bool Realtime,
    bool Signatures,
    bool Firewall,
    bool Pua,
    bool Network,
    bool Ransomware,
    string SignatureVersion,
    string EngineVersion,
    string LastQuickScan,
    string LastFullScan,
    List<string> Issues)
{
    public int Score => new[] { Antivirus, Realtime, Signatures, Firewall, Pua, Network, Ransomware }.Count(x => x) * 100 / 7;
}

internal sealed record ThreatRow(string Id, string Data, string Stato, string Risorsa);
internal sealed record EventRow(string Data, string Id, string Livello, string Messaggio);
