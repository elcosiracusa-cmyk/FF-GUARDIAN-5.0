using System.Diagnostics;
using System.Text.Json;

namespace FFGuardian;

internal sealed class DefenderService
{
    public async Task<SecurityState> GetStateAsync()
    {
        var json = await RunPsAsync("$s=Get-MpComputerStatus;$p=Get-MpPreference;$f=@(Get-NetFirewallProfile|Select Name,Enabled);[pscustomobject]@{Antivirus=$s.AntivirusEnabled;Realtime=$s.RealTimeProtectionEnabled;SignaturesOld=$s.DefenderSignaturesOutOfDate;SignatureVersion=$s.AntivirusSignatureVersion;QuickScan=$s.QuickScanEndTime;FullScan=$s.FullScanEndTime;Firewall=$f;PUA=$p.PUAProtection;Network=$p.EnableNetworkProtection;CFA=$p.EnableControlledFolderAccess}|ConvertTo-Json -Depth 5 -Compress");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var firewall = root.GetProperty("Firewall").EnumerateArray().All(x => x.GetProperty("Enabled").GetBoolean());
        return new SecurityState(
            root.GetProperty("Antivirus").GetBoolean(),
            root.GetProperty("Realtime").GetBoolean(),
            !root.GetProperty("SignaturesOld").GetBoolean(),
            firewall,
            root.GetProperty("PUA").GetInt32() == 1,
            root.GetProperty("Network").GetInt32() == 1,
            root.GetProperty("CFA").GetInt32() != 0,
            root.GetProperty("SignatureVersion").GetString() ?? "-");
    }

    public Task QuickScanAsync() => RunPsAsync("Start-MpScan -ScanType QuickScan");
    public Task FullScanAsync() => RunPsAsync("Start-MpScan -ScanType FullScan");
    public Task CustomScanAsync(string path) => RunPsAsync($"Start-MpScan -ScanType CustomScan -ScanPath '{path.Replace("'", "''")}'");
    public Task UpdateAsync() => RunPsAsync("Update-MpSignature");

    public async Task<List<ThreatRow>> GetThreatsAsync()
    {
        var json = await RunPsAsync("@(Get-MpThreatDetection|Sort InitialDetectionTime -Descending|Select -First 100 ThreatID,InitialDetectionTime,ActionSuccess,Resources)|ConvertTo-Json -Depth 6 -Compress");
        if (string.IsNullOrWhiteSpace(json) || json == "null") return [];
        using var doc = JsonDocument.Parse(json);
        IEnumerable<JsonElement> items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : new[] { doc.RootElement };
        return items.Select(x => new ThreatRow(
            x.TryGetProperty("ThreatID", out var id) ? id.ToString() : "-",
            x.TryGetProperty("InitialDetectionTime", out var t) ? t.ToString() : "-",
            x.TryGetProperty("ActionSuccess", out var a) && a.GetBoolean() ? "Corretta" : "Da verificare",
            x.TryGetProperty("Resources", out var r) ? r.ToString() : "-")).ToList();
    }

    private static async Task<string> RunPsAsync(string command)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';" + command));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Impossibile avviare PowerShell.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Comando Defender non riuscito." : error.Trim());
        return output.Trim();
    }
}

internal sealed record SecurityState(bool Antivirus, bool Realtime, bool Signatures, bool Firewall, bool Pua, bool Network, bool Ransomware, string SignatureVersion)
{
    public int Score => new[] { Antivirus, Realtime, Signatures, Firewall, Pua, Network, Ransomware }.Count(x => x) * 100 / 7;
}

internal sealed record ThreatRow(string Id, string Data, string Stato, string Risorsa);
