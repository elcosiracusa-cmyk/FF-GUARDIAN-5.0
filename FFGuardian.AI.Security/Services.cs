using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFGuardian.AI.Security;

public sealed class ThreatScoreCalculator : IThreatScoreCalculator
{
    public ThreatScore Calculate(IEnumerable<ThreatEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ThreatEvidence[] items = evidence.ToArray();
        int value = Math.Clamp(items.Sum(item => item.Direction == EvidenceDirection.Risk ? Math.Abs(item.Weight) : -Math.Abs(item.Weight)), 0, 100);
        double confidence = items.Length == 0 ? 0 : Math.Clamp(items.Average(item => item.Confidence), 0, 1);
        AiRiskLevel level = value switch { <= 19 => AiRiskLevel.Low, <= 39 => AiRiskLevel.Attention, <= 59 => AiRiskLevel.Suspicious, <= 79 => AiRiskLevel.High, _ => AiRiskLevel.Critical };
        return new(value, level, confidence);
    }
}

public sealed class BehaviorCorrelationService : IBehaviorCorrelationService
{
    public IReadOnlyCollection<ThreatEvidence> Correlate(BehaviorSecurityFeatures? b)
    {
        if (b is null) return Array.Empty<ThreatEvidence>();
        List<ThreatEvidence> e = [];
        if (b.MassiveWrites > 100) e.Add(new("massive-writes", "Scritture massive osservate", 30, EvidenceDirection.Risk, "Behavior", .85));
        if (b.MassiveRenames > 50) e.Add(new("massive-renames", "Rinomine massive osservate", 25, EvidenceDirection.Risk, "Behavior", .8));
        if (b.PersistenceChange) e.Add(new("persistence", "Modifica di persistenza osservata", 20, EvidenceDirection.Risk, "Behavior", .75));
        if (b.SecurityTampering) e.Add(new("security-tampering", "Tentativo di alterare protezioni", 35, EvidenceDirection.Risk, "Behavior", .95));
        if (b.SuspiciousShellUse && (b.PersistenceChange || b.SecurityTampering || b.MassiveWrites > 100)) e.Add(new("shell-correlation", "Shell anomala correlata ad altri eventi", 18, EvidenceDirection.Risk, "Behavior", .8));
        return e;
    }
}

public sealed class SafeFeatureExtractor(IOptions<AiThreatAnalyzerOptions> options) : IFeatureExtractor
{
    private readonly AiThreatAnalyzerOptions _options = options.Value;
    public async Task<FileSecurityFeatures> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        string full = Path.GetFullPath(filePath);
        FileInfo info = new(full);
        if (!info.Exists) throw new FileNotFoundException("File non trovato.", full);
        if (info.Length > _options.MaximumFileBytes) throw new InvalidOperationException("File oltre il limite configurato.");
        string hash;
        double entropy;
        byte[] prefix;
        await using (FileStream stream = new(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            stream.Position = 0;
            int length = (int)Math.Min(info.Length, _options.MaximumStringBytes);
            prefix = new byte[length];
            int read = await stream.ReadAsync(prefix.AsMemory(), cancellationToken);
            if (read != prefix.Length) Array.Resize(ref prefix, read);
            entropy = CalculateEntropy(prefix);
        }
        string ext = Path.GetExtension(full).ToLowerInvariant();
        bool mz = prefix.Length >= 2 && prefix[0] == (byte)'M' && prefix[1] == (byte)'Z';
        string detected = mz ? "pe" : DetectText(prefix) ? "text" : "binary";
        string name = Path.GetFileName(full);
        bool doubleExt = name.Count(c => c == '.') >= 2 && new[] { ".exe", ".scr", ".com", ".bat", ".cmd", ".ps1", ".js", ".vbs" }.Any(x => name[..^ext.Length].EndsWith(x, StringComparison.OrdinalIgnoreCase));
        bool mismatch = mz && ext is not ".exe" and not ".dll" and not ".scr" and not ".sys";
        List<string> suspicious = ExtractSuspiciousStrings(prefix);
        return new(hash, info.Length, ext, detected, doubleExt, mismatch, mz, false, false, null, entropy, 0, suspicious.Count > 2, entropy > 7.2 && mz, suspicious);
    }
    private static bool DetectText(byte[] bytes) => bytes.Length > 0 && bytes.Take(Math.Min(bytes.Length, 4096)).Count(b => b is 9 or 10 or 13 || b is >= 32 and <= 126) >= Math.Min(bytes.Length, 4096) * .9;
    private static double CalculateEntropy(byte[] bytes) { if (bytes.Length == 0) return 0; int[] f = new int[256]; foreach (byte b in bytes) f[b]++; double e = 0; foreach (int n in f) if (n > 0) { double p = (double)n / bytes.Length; e -= p * Math.Log2(p); } return e; }
    private static List<string> ExtractSuspiciousStrings(byte[] bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);
        string[] indicators = ["powershell", "cmd.exe", "schtasks", "reg add", "vssadmin", "bcdedit", "CreateRemoteThread", "VirtualAllocEx"];
        return indicators.Where(i => text.Contains(i, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

public sealed class VerifiedLocalModelProvider(IOptions<AiThreatAnalyzerOptions> options) : IAiModelProvider
{
    private readonly AiThreatAnalyzerOptions _options = options.Value;
    public async Task<ModelVersionInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.ModelPath) || !File.Exists(_options.ModelLockPath)) return new("FFGuardian Local Threat Model", "--", "", false, "Modello locale non installato o lock file assente.");
        using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(_options.ModelLockPath, cancellationToken));
        string expected = doc.RootElement.GetProperty("sha256").GetString() ?? string.Empty;
        string version = doc.RootElement.GetProperty("version").GetString() ?? "--";
        await using FileStream stream = File.OpenRead(_options.ModelPath);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        bool valid = expected.Length == 64 && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected.ToLowerInvariant()));
        return new("FFGuardian Local Threat Model", version, actual, valid, valid ? "Modello verificato." : "Hash modello non valido.");
    }
}

public sealed class AiExplanationService : IAiExplanationService
{
    public string Explain(ThreatScore score, IReadOnlyCollection<ThreatEvidence> risk, IReadOnlyCollection<ThreatEvidence> trust, ModelVersionInfo model) =>
        $"Rischio stimato {score.Level} — {score.Value}/100. Evidenze di rischio: {risk.Count}; elementi favorevoli: {trust.Count}. Modello locale: {(model.IsVerified ? model.Version : "non disponibile")}. Il risultato richiede correlazione con i motori antivirus e non costituisce una garanzia.";
}

public sealed class AiThreatAnalyzer(IFeatureExtractor extractor, IThreatScoreCalculator calculator, IBehaviorCorrelationService behavior, IAiModelProvider modelProvider, IAiExplanationService explanation, ILocalHashAllowlist allowlist, ILogger<AiThreatAnalyzer> logger) : IAiThreatAnalyzer
{
    public async Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(20));
        try
        {
            FileSecurityFeatures f = await extractor.ExtractAsync(request.FilePath, timeout.Token);
            List<ThreatEvidence> evidence = request.ExternalEvidence?.ToList() ?? [];
            if (f.DoubleExtension) evidence.Add(new("double-extension", "Doppia estensione eseguibile", 12, EvidenceDirection.Risk, "Static", .8));
            if (f.ExtensionMismatch) evidence.Add(new("extension-mismatch", "Tipo reale non coerente con l'estensione", 12, EvidenceDirection.Risk, "Static", .9));
            if (f.IsExecutable && !f.SignatureValid) evidence.Add(new("unsigned-executable", "Eseguibile senza firma valida", 8, EvidenceDirection.Risk, "Static", .45));
            if (f.Entropy > 7.2) evidence.Add(new("high-entropy", "Entropia elevata", 10, EvidenceDirection.Risk, "Static", .5));
            if (f.PackerIndicator) evidence.Add(new("packer-indicator", "Possibile compressione o offuscamento", 12, EvidenceDirection.Risk, "Static", .55));
            evidence.AddRange(behavior.Correlate(request.Behavior));
            if (await allowlist.ContainsAsync(f.Sha256, timeout.Token)) evidence.Add(new("known-safe-hash", "Hash autorizzato localmente", 60, EvidenceDirection.Trust, "LocalAllowlist", .95));
            ModelVersionInfo model = await modelProvider.GetStatusAsync(timeout.Token);
            ThreatScore score = calculator.Calculate(evidence);
            ThreatEvidence[] risk = evidence.Where(e => e.Direction == EvidenceDirection.Risk).ToArray();
            ThreatEvidence[] trust = evidence.Where(e => e.Direction == EvidenceDirection.Trust).ToArray();
            return new(score, f, request.Behavior, risk, trust, evidence.Select(e => e.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), model, DateTimeOffset.UtcNow, explanation.Explain(score, risk, trust, model), ["Il modello locale non influenza il punteggio finché non è verificato.", "Nessun file viene eseguito o inviato al cloud."], score.Value >= 40, score.Value >= 80);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(new(0, AiRiskLevel.Unavailable, 0), null, request.Behavior, [], [], [], new("--", "--", "", false, "Annullato"), DateTimeOffset.UtcNow, "Analisi annullata.", [], false, false, true); }
        catch (Exception ex) { logger.LogError(ex, "AI Threat Analyzer failed"); return new(new(0, AiRiskLevel.Unavailable, 0), null, request.Behavior, [], [], [], new("--", "--", "", false, "Non disponibile"), DateTimeOffset.UtcNow, "Analisi non disponibile.", ["Errore registrato localmente senza includere dati sensibili."], false, false, false, ex.Message); }
    }
}

public sealed class LocalHashAllowlist(IOptions<AiThreatAnalyzerOptions> options) : ILocalHashAllowlist
{
    private readonly string _path = Path.Combine(options.Value.DataDirectory, "allowlist.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<bool> ContainsAsync(string sha256, CancellationToken ct) { await _gate.WaitAsync(ct); try { Dictionary<string, Entry> d = await ReadAsync(ct); return d.TryGetValue(Normalize(sha256), out Entry? e) && (e.ExpiresAt is null || e.ExpiresAt > DateTimeOffset.UtcNow); } finally { _gate.Release(); } }
    public async Task AddAsync(string sha256, string reason, DateTimeOffset? expiresAt, CancellationToken ct) { await _gate.WaitAsync(ct); try { Dictionary<string, Entry> d = await ReadAsync(ct); d[Normalize(sha256)] = new(reason, expiresAt); await WriteAsync(d, ct); } finally { _gate.Release(); } }
    public async Task RevokeAsync(string sha256, CancellationToken ct) { await _gate.WaitAsync(ct); try { Dictionary<string, Entry> d = await ReadAsync(ct); d.Remove(Normalize(sha256)); await WriteAsync(d, ct); } finally { _gate.Release(); } }
    private async Task<Dictionary<string, Entry>> ReadAsync(CancellationToken ct) => File.Exists(_path) ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(await File.ReadAllTextAsync(_path, ct)) ?? new(StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
    private async Task WriteAsync(Dictionary<string, Entry> d, CancellationToken ct) { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); string temp = _path + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(d), ct); File.Move(temp, _path, true); }
    private static string Normalize(string hash) { string n = hash.Trim().ToLowerInvariant(); if (n.Length != 64 || n.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("SHA-256 non valido.", nameof(hash)); return n; }
    private sealed record Entry(string Reason, DateTimeOffset? ExpiresAt);
}
