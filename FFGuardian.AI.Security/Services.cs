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
    public IReadOnlyCollection<ThreatEvidence> Correlate(BehaviorSecurityFeatures? behavior)
    {
        if (behavior is null) return Array.Empty<ThreatEvidence>();
        List<ThreatEvidence> evidence = [];
        if (behavior.MassiveWrites > 100) evidence.Add(new("massive-writes", "Scritture massive osservate", 30, EvidenceDirection.Risk, "Behavior", .85));
        if (behavior.MassiveRenames > 50) evidence.Add(new("massive-renames", "Rinomine massive osservate", 25, EvidenceDirection.Risk, "Behavior", .8));
        if (behavior.PersistenceChange) evidence.Add(new("persistence", "Modifica di persistenza osservata", 20, EvidenceDirection.Risk, "Behavior", .75));
        if (behavior.SecurityTampering) evidence.Add(new("security-tampering", "Tentativo di alterare protezioni", 35, EvidenceDirection.Risk, "Behavior", .95));
        if (behavior.SuspiciousShellUse && (behavior.PersistenceChange || behavior.SecurityTampering || behavior.MassiveWrites > 100)) evidence.Add(new("shell-correlation", "Shell anomala correlata ad altri eventi", 18, EvidenceDirection.Risk, "Behavior", .8));
        return evidence;
    }
}

public sealed class SafeFeatureExtractor(IOptions<AiThreatAnalyzerOptions> options) : IFeatureExtractor
{
    private static readonly string[] ExecutableExtensions = [".exe", ".scr", ".com", ".bat", ".cmd", ".ps1", ".js", ".vbs"];
    private static readonly string[] SuspiciousIndicators = ["powershell", "cmd.exe", "schtasks", "reg add", "vssadmin", "bcdedit", "CreateRemoteThread", "VirtualAllocEx"];
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
        string extension = Path.GetExtension(full).ToLowerInvariant();
        bool mz = prefix.Length >= 2 && prefix[0] == (byte)'M' && prefix[1] == (byte)'Z';
        string detected = mz ? "pe" : DetectText(prefix) ? "text" : "binary";
        string name = Path.GetFileName(full);
        string stem = extension.Length <= name.Length ? name[..^extension.Length] : name;
        bool doubleExtension = name.Count(character => character == '.') >= 2 && ExecutableExtensions.Any(item => stem.EndsWith(item, StringComparison.OrdinalIgnoreCase));
        bool mismatch = mz && extension is not ".exe" and not ".dll" and not ".scr" and not ".sys";
        List<string> suspicious = ExtractSuspiciousStrings(prefix);
        return new(hash, info.Length, extension, detected, doubleExtension, mismatch, mz, false, false, null, entropy, 0, suspicious.Count > 2, entropy > 7.2 && mz, suspicious);
    }

    private static bool DetectText(byte[] bytes) => bytes.Length > 0 && bytes.Take(Math.Min(bytes.Length, 4096)).Count(value => value is 9 or 10 or 13 || value is >= 32 and <= 126) >= Math.Min(bytes.Length, 4096) * .9;

    private static double CalculateEntropy(byte[] bytes)
    {
        if (bytes.Length == 0) return 0;
        int[] frequencies = new int[256];
        foreach (byte value in bytes) frequencies[value]++;
        double entropy = 0;
        foreach (int frequency in frequencies)
        {
            if (frequency <= 0) continue;
            double probability = (double)frequency / bytes.Length;
            entropy -= probability * Math.Log2(probability);
        }
        return entropy;
    }

    private static List<string> ExtractSuspiciousStrings(byte[] bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);
        return SuspiciousIndicators.Where(indicator => text.Contains(indicator, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

public sealed class VerifiedLocalModelProvider(IOptions<AiThreatAnalyzerOptions> options) : IAiModelProvider
{
    private readonly AiThreatAnalyzerOptions _options = options.Value;
    public async Task<ModelVersionInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.ModelPath) || !File.Exists(_options.ModelLockPath)) return new("FFGuardian Local Threat Model", "--", "", false, "Modello locale non installato o lock file assente.");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(_options.ModelLockPath, cancellationToken));
        string expected = document.RootElement.GetProperty("sha256").GetString() ?? string.Empty;
        string version = document.RootElement.GetProperty("version").GetString() ?? "--";
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
    private static readonly Action<ILogger, Exception?> LogAnalysisFailure = LoggerMessage.Define(LogLevel.Error, new EventId(1001, "AiAnalysisFailure"), "AI Threat Analyzer failed");

    public async Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(20));
        try
        {
            FileSecurityFeatures features = await extractor.ExtractAsync(request.FilePath, timeout.Token);
            List<ThreatEvidence> evidence = request.ExternalEvidence?.ToList() ?? [];
            if (features.DoubleExtension) evidence.Add(new("double-extension", "Doppia estensione eseguibile", 12, EvidenceDirection.Risk, "Static", .8));
            if (features.ExtensionMismatch) evidence.Add(new("extension-mismatch", "Tipo reale non coerente con l'estensione", 12, EvidenceDirection.Risk, "Static", .9));
            if (features.IsExecutable && !features.SignatureValid) evidence.Add(new("unsigned-executable", "Eseguibile senza firma valida", 8, EvidenceDirection.Risk, "Static", .45));
            if (features.Entropy > 7.2) evidence.Add(new("high-entropy", "Entropia elevata", 10, EvidenceDirection.Risk, "Static", .5));
            if (features.PackerIndicator) evidence.Add(new("packer-indicator", "Possibile compressione o offuscamento", 12, EvidenceDirection.Risk, "Static", .55));
            evidence.AddRange(behavior.Correlate(request.Behavior));
            if (await allowlist.ContainsAsync(features.Sha256, timeout.Token)) evidence.Add(new("known-safe-hash", "Hash autorizzato localmente", 60, EvidenceDirection.Trust, "LocalAllowlist", .95));
            ModelVersionInfo model = await modelProvider.GetStatusAsync(timeout.Token);
            ThreatScore score = calculator.Calculate(evidence);
            ThreatEvidence[] risk = evidence.Where(item => item.Direction == EvidenceDirection.Risk).ToArray();
            ThreatEvidence[] trust = evidence.Where(item => item.Direction == EvidenceDirection.Trust).ToArray();
            return new(score, features, request.Behavior, risk, trust, evidence.Select(item => item.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), model, DateTimeOffset.UtcNow, explanation.Explain(score, risk, trust, model), ["Il modello locale non influenza il punteggio finché non è verificato.", "Nessun file viene eseguito o inviato al cloud."], score.Value >= 40, score.Value >= 80);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(new(0, AiRiskLevel.Unavailable, 0), null, request.Behavior, [], [], [], new("--", "--", "", false, "Annullato"), DateTimeOffset.UtcNow, "Analisi annullata.", [], false, false, true);
        }
        catch (Exception exception)
        {
            LogAnalysisFailure(logger, exception);
            return new(new(0, AiRiskLevel.Unavailable, 0), null, request.Behavior, [], [], [], new("--", "--", "", false, "Non disponibile"), DateTimeOffset.UtcNow, "Analisi non disponibile.", ["Errore registrato localmente senza includere dati sensibili."], false, false, false, exception.Message);
        }
    }
}

public sealed class LocalHashAllowlist(IOptions<AiThreatAnalyzerOptions> options) : ILocalHashAllowlist, IDisposable
{
    private readonly string _path = Path.Combine(options.Value.DataDirectory, "allowlist.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> ContainsAsync(string sha256, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, Entry> entries = await ReadAsync(cancellationToken);
            return entries.TryGetValue(Normalize(sha256), out Entry? entry) && (entry.ExpiresAt is null || entry.ExpiresAt > DateTimeOffset.UtcNow);
        }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(string sha256, string reason, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, Entry> entries = await ReadAsync(cancellationToken);
            entries[Normalize(sha256)] = new(reason, expiresAt);
            await WriteAsync(entries, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task RevokeAsync(string sha256, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, Entry> entries = await ReadAsync(cancellationToken);
            entries.Remove(Normalize(sha256));
            await WriteAsync(entries, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, Entry>> ReadAsync(CancellationToken cancellationToken) =>
        File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(await File.ReadAllTextAsync(_path, cancellationToken)) ?? new(StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);

    private async Task WriteAsync(Dictionary<string, Entry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = _path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(entries), cancellationToken);
        File.Move(temporaryPath, _path, true);
    }

    private static string Normalize(string hash)
    {
        string normalized = hash.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character))) throw new ArgumentException("SHA-256 non valido.", nameof(hash));
        return normalized;
    }

    public void Dispose() => _gate.Dispose();

    private sealed record Entry(string Reason, DateTimeOffset? ExpiresAt);
}
