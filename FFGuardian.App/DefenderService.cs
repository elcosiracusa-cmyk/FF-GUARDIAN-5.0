using System.Diagnostics;
using System.Text.Json;

namespace FFGuardian;

internal sealed class DefenderService
{
    public async Task<SecurityState> GetStateAsync()
    {
        var json=await RunPsAsync("$s=Get-MpComputerStatus;$p=Get-MpPreference;$f=@(Get-NetFirewallProfile|Select Name,Enabled);[pscustomobject]@{Antivirus=$s.AntivirusEnabled;Realtime=$s.RealTimeProtectionEnabled;SignaturesOld=$s.DefenderSignaturesOutOfDate;SignatureVersion=$s.AntivirusSignatureVersion;EngineVersion=$s.AMEngineVersion;QuickScan=$s.QuickScanEndTime;FullScan=$s.FullScanEndTime;Firewall=$f;PUA=$p.PUAProtection;Network=$p.EnableNetworkProtection;CFA=$p.EnableControlledFolderAccess}|ConvertTo-Json -Depth 5 -Compress");
        if(string.IsNullOrWhiteSpace(json)||json=="null")throw new InvalidOperationException("Microsoft Defender non ha restituito dati leggibili.");
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var firewall=false;
        if(root.TryGetProperty("Firewall",out var fw)){if(fw.ValueKind==JsonValueKind.Array){var a=fw.EnumerateArray().ToArray();firewall=a.Length>0&&a.All(x=>ReadBool(x,"Enabled"));}else if(fw.ValueKind==JsonValueKind.Object)firewall=ReadBool(fw,"Enabled");}
        var antivirus=ReadBool(root,"Antivirus"),realtime=ReadBool(root,"Realtime"),signatures=!ReadBool(root,"SignaturesOld",true),pua=ReadInt(root,"PUA")==1,network=ReadInt(root,"Network")==1,cfa=ReadInt(root,"CFA")!=0;
        var issues=new List<string>();if(!antivirus)issues.Add("Microsoft Defender non è attivo.");if(!realtime)issues.Add("Protezione in tempo reale disattivata.");if(!signatures)issues.Add("Definizioni antivirus da aggiornare.");if(!firewall)issues.Add("Uno o più profili Firewall sono disattivati.");if(!pua)issues.Add("Protezione PUA non in blocco.");if(!network)issues.Add("Protezione rete non in blocco.");if(!cfa)issues.Add("Ransomware Guard non attivo.");
        return new SecurityState(antivirus,realtime,signatures,firewall,pua,network,cfa,ReadString(root,"SignatureVersion","-"),ReadString(root,"EngineVersion","-"),ReadString(root,"QuickScan","Non disponibile"),ReadString(root,"FullScan","Non disponibile"),issues);
    }

    public Task QuickScanAsync()=>RunPsAsync("Start-MpScan -ScanType QuickScan");
    public Task FullScanAsync()=>RunPsAsync("Start-MpScan -ScanType FullScan");
    public Task CustomScanAsync(string path)=>RunPsAsync($"Start-MpScan -ScanType CustomScan -ScanPath '{path.Replace("'","''")}'");
    public Task UpdateAsync()=>RunPsAsync("Update-MpSignature");
    public void OpenWindowsSecurity()=>Process.Start(new ProcessStartInfo("windowsdefender:"){UseShellExecute=true});

    public async Task<List<ThreatRow>> GetThreatsAsync()
    {
        var json=await RunPsAsync("@(Get-MpThreatDetection|Sort InitialDetectionTime -Descending|Select -First 100 ThreatID,InitialDetectionTime,ActionSuccess,Resources)|ConvertTo-Json -Depth 6 -Compress");if(string.IsNullOrWhiteSpace(json)||json=="null")return[];using var doc=JsonDocument.Parse(json);var items=doc.RootElement.ValueKind==JsonValueKind.Array?doc.RootElement.EnumerateArray().ToArray():new[]{doc.RootElement};return items.Select(x=>new ThreatRow(ReadString(x,"ThreatID","-"),ReadString(x,"InitialDetectionTime","-"),ReadBool(x,"ActionSuccess")?"Corretta":"Da verificare",ReadString(x,"Resources","-"))).ToList();
    }

    public async Task<List<EventRow>> GetOperationalEventsAsync()
    {
        var json=await RunPsAsync("@(Get-WinEvent -LogName 'Microsoft-Windows-Windows Defender/Operational' -MaxEvents 80 -ErrorAction SilentlyContinue|Select TimeCreated,Id,LevelDisplayName,Message)|ConvertTo-Json -Depth 4 -Compress");if(string.IsNullOrWhiteSpace(json)||json=="null")return[];using var doc=JsonDocument.Parse(json);var items=doc.RootElement.ValueKind==JsonValueKind.Array?doc.RootElement.EnumerateArray().ToArray():new[]{doc.RootElement};return items.Select(x=>new EventRow(ReadString(x,"TimeCreated","-"),ReadString(x,"Id","-"),ReadString(x,"LevelDisplayName","-"),ReadString(x,"Message","-").Replace("\r"," ").Replace("\n"," "))).ToList();
    }

    private static bool ReadBool(JsonElement p,string n,bool d=false){if(!p.TryGetProperty(n,out var v))return d;return v.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number when v.TryGetInt32(out var i)=>i!=0,JsonValueKind.String when bool.TryParse(v.GetString(),out var b)=>b,JsonValueKind.String when int.TryParse(v.GetString(),out var i)=>i!=0,_=>d};}
    private static int ReadInt(JsonElement p,string n,int d=0){if(!p.TryGetProperty(n,out var v))return d;if(v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out var i))return i;return int.TryParse(v.ToString(),out i)?i:d;}
    private static string ReadString(JsonElement p,string n,string d=""){if(!p.TryGetProperty(n,out var v)||v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)return d;return v.ToString();}
    private static async Task<string> RunPsAsync(string command){var encoded=Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';"+command));var psi=new ProcessStartInfo("powershell.exe",$"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};using var process=Process.Start(psi)??throw new InvalidOperationException("Impossibile avviare PowerShell.");var output=await process.StandardOutput.ReadToEndAsync();var error=await process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)?"Comando Defender non riuscito.":error.Trim());return output.Trim();}
}

internal sealed record SecurityState(bool Antivirus,bool Realtime,bool Signatures,bool Firewall,bool Pua,bool Network,bool Ransomware,string SignatureVersion,string EngineVersion,string LastQuickScan,string LastFullScan,List<string> Issues){public int Score=>new[]{Antivirus,Realtime,Signatures,Firewall,Pua,Network,Ransomware}.Count(x=>x)*100/7;}
internal sealed record ThreatRow(string Id,string Data,string Stato,string Risorsa);
internal sealed record EventRow(string Data,string Id,string Livello,string Messaggio);