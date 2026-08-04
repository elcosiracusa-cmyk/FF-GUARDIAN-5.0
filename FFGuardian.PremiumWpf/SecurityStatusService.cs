using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;

namespace FFGuardian.PremiumWpf;

public sealed record ComponentStatus(string Name, bool? IsOperational, string Detail);
public sealed record DashboardStatus(int Score, string ProtectionText, string ProtectionDetail,
    string LastScan, string LastUpdate, string EngineVersion, string DatabaseVersion,
    IReadOnlyList<ComponentStatus> Components);

public sealed class SecurityStatusService
{
    private readonly string _baseDirectory;

    public SecurityStatusService() : this(AppContext.BaseDirectory) { }

    internal SecurityStatusService(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
    }

    public Task<DashboardStatus> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = _baseDirectory;
        bool yara = Find(root, "Engine/Yara/yara64.exe", "Engine/Yara/yara.exe", "Tools/Yara/yara64.exe", "Tools/Yara/yara.exe");
        bool clam = Find(root, "Engine/ClamAV/clamscan.exe", "ClamAV/clamscan.exe");
        bool fresh = Find(root, "Engine/ClamAV/freshclam.exe", "ClamAV/freshclam.exe");
        bool engine = File.Exists(Path.Combine(root, "FFGuardian.dll"));
        bool signatures = Directory.Exists(Path.Combine(root, "Database")) || Directory.Exists(Path.Combine(root, "Engine", "ClamAV", "Database"));
        bool network = NetworkInterface.GetIsNetworkAvailable();
        ComponentStatus[] components =
        [
            new("Engine10", engine, engine ? "Componente applicativo rilevato" : "Componente non verificato"),
            new("ClamAV", clam, clam ? "Eseguibile rilevato" : "Motore non disponibile"),
            new("FreshClam", fresh, fresh ? "Updater rilevato" : "Updater non disponibile"),
            new("YARA", yara, yara ? "Motore portable rilevato; test runtime richiesto" : "Motore non disponibile"),
            new("Database firme", signatures, signatures ? "Directory firme rilevata" : "Database non verificato"),
            new("Connettività aggiornamenti", network, network ? "Rete disponibile" : "Rete non disponibile")
        ];
        int verified = components.Count(component => component.IsOperational == true);
        int score = (int)Math.Round(verified * 100d / components.Length);
        string state = score >= 85 ? "Sistema Protetto" : score >= 50 ? "Attenzione" : "Protezione Disattivata";
        string detail = score >= 85 ? "I componenti verificabili risultano disponibili." : "Uno o più componenti richiedono attenzione o test runtime.";
        string processPath = Environment.ProcessPath ?? string.Empty;
        string version = string.IsNullOrWhiteSpace(processPath) ? "--" : FileVersionInfo.GetVersionInfo(processPath).FileVersion ?? "--";
        return Task.FromResult(new DashboardStatus(score, state, detail, "Non disponibile", "Non disponibile", version, signatures ? "Rilevata" : "--", components));
    }

    private static bool Find(string root, params string[] relatives) => relatives.Any(relative =>
        File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))));
}
