using System.Collections.ObjectModel;

namespace FFGuardian.AI.Security;

public enum AiRiskLevel { Low, Attention, Suspicious, High, Critical, Unavailable }
public enum EvidenceDirection { Risk, Trust }

public sealed record ThreatEvidence(string Code, string Description, int Weight, EvidenceDirection Direction, string Source, double Confidence);
public sealed record ModelVersionInfo(string Name, string Version, string Sha256, bool IsVerified, string StatusMessage);
public sealed record BehaviorSecurityFeatures(int ProcessCreations, int ChildProcesses, int MassiveWrites, int MassiveRenames, bool PersistenceChange, bool ScheduledTaskChange, bool ServiceChange, bool SensitiveDirectoryWrite, bool SuspiciousShellUse, bool SecurityTampering, bool AnomalousNetwork, bool UsbActivity);
public sealed record FileSecurityFeatures(string Sha256, long Size, string DeclaredExtension, string DetectedType, bool DoubleExtension, bool ExtensionMismatch, bool IsExecutable, bool IsSigned, bool SignatureValid, string? Publisher, double Entropy, int PeSectionCount, bool SuspiciousImports, bool PackerIndicator, IReadOnlyCollection<string> SuspiciousStrings);
public sealed record AiAnalysisRequest(string FilePath, IReadOnlyCollection<ThreatEvidence>? ExternalEvidence = null, BehaviorSecurityFeatures? Behavior = null, TimeSpan? Timeout = null);
public sealed record ThreatScore(int Value, AiRiskLevel Level, double Confidence);
public sealed record AiAnalysisResult(ThreatScore Score, FileSecurityFeatures? FileFeatures, BehaviorSecurityFeatures? BehaviorFeatures, IReadOnlyCollection<ThreatEvidence> RiskEvidence, IReadOnlyCollection<ThreatEvidence> TrustEvidence, IReadOnlyCollection<string> Engines, ModelVersionInfo Model, DateTimeOffset AnalyzedAt, string Explanation, IReadOnlyCollection<string> Limitations, bool SuggestedDeepScan, bool SuggestedQuarantine, bool IsCancelled = false, string? Error = null);

public interface IAiThreatAnalyzer { Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken); }
public interface IFeatureExtractor { Task<FileSecurityFeatures> ExtractAsync(string filePath, CancellationToken cancellationToken); }
public interface IThreatScoreCalculator { ThreatScore Calculate(IEnumerable<ThreatEvidence> evidence); }
public interface IBehaviorCorrelationService { IReadOnlyCollection<ThreatEvidence> Correlate(BehaviorSecurityFeatures? behavior); }
public interface IAiModelProvider { Task<ModelVersionInfo> GetStatusAsync(CancellationToken cancellationToken); }
public interface IAiExplanationService { string Explain(ThreatScore score, IReadOnlyCollection<ThreatEvidence> risk, IReadOnlyCollection<ThreatEvidence> trust, ModelVersionInfo model); }
public interface ILocalHashAllowlist { Task<bool> ContainsAsync(string sha256, CancellationToken cancellationToken); Task AddAsync(string sha256, string reason, DateTimeOffset? expiresAt, CancellationToken cancellationToken); Task RevokeAsync(string sha256, CancellationToken cancellationToken); }

public sealed class AiThreatAnalyzerOptions
{
    public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFGuardian", "AI");
    public string ModelPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Models", "ffguardian-threat.onnx");
    public string ModelLockPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Models", "model.lock.json");
    public long MaximumFileBytes { get; set; } = 256L * 1024 * 1024;
    public int MaximumStringBytes { get; set; } = 1024 * 1024;
    public Dictionary<string, int> Weights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clamav-detection"] = 50, ["yara-high"] = 35, ["known-malicious-hash"] = 80,
        ["unsigned-executable"] = 8, ["high-entropy"] = 10, ["double-extension"] = 12,
        ["extension-mismatch"] = 12, ["packer-indicator"] = 12, ["suspicious-imports"] = 10,
        ["massive-writes"] = 30, ["massive-renames"] = 25, ["persistence"] = 20,
        ["security-tampering"] = 35, ["trusted-signature"] = -30, ["known-safe-hash"] = -60
    };
}
