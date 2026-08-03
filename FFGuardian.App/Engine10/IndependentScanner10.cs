using System.Security.Cryptography;

namespace FFGuardian.Engine10;

internal sealed class IndependentScanner10
{
    private static readonly HashSet<string> ActiveContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".sys", ".msi", ".msix",
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf", ".lnk"
    };

    private readonly SignatureDatabase10 _database;

    public IndependentScanner10(SignatureDatabase10 database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<FileScanResult10> ScanFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        try
        {
            FileInfo info = new(fullPath);
            if (!info.Exists)
                return Error(fullPath, "File non trovato.");

            if (IsTrustedInternalComponent(fullPath))
            {
                return new FileScanResult10(
                    fullPath,
                    string.Empty,
                    info.Length,
                    ThreatVerdict10.Clean,
                    100,
                    "FFGuardian.Internal.Trusted",
                    new[] { "Componente interno FFGuardian escluso automaticamente dalla scansione." },
                    DateTime.UtcNow);
            }

            if (info.Length == 0)
            {
                return new FileScanResult10(fullPath, string.Empty, 0, ThreatVerdict10.Unknown, 10,
                    "Empty.File", new[] { "Il file è vuoto." }, DateTime.UtcNow);
            }

            string sha256 = await ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);

            if (await EicarDetector10.IsEicarAsync(fullPath, cancellationToken).ConfigureAwait(false))
            {
                return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Malicious, 100,
                    "Test.EICAR",
                    new[] { "Rilevato il campione di test antivirus EICAR nel contenuto del file." },
                    DateTime.UtcNow);
            }

            if (await _database.IsAllowListedAsync(sha256, cancellationToken).ConfigureAwait(false))
            {
                return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Clean, 100,
                    "AllowList.Trusted", new[] { "Hash presente nella allowlist locale." }, DateTime.UtcNow);
            }

            SignatureEntry10? signature = await _database.FindSignatureAsync(sha256, cancellationToken)
                .ConfigureAwait(false);
            if (signature is not null)
            {
                return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Malicious,
                    Math.Clamp(signature.Confidence, 1, 100), signature.DetectionName,
                    new[] { $"Corrispondenza firma: {signature.Id}." }, DateTime.UtcNow);
            }

            IReadOnlyList<ExternalThreatResult10> externalResults = await ExternalThreatEngines10
                .ScanAsync(fullPath, cancellationToken).ConfigureAwait(false);
            ExternalThreatResult10? externalMatch = externalResults
                .Where(result => result.Available && result.IsMatch && !result.IsError)
                .OrderByDescending(result => result.Confidence)
                .FirstOrDefault();
            if (externalMatch is not null)
            {
                return new FileScanResult10(
                    fullPath,
                    sha256,
                    info.Length,
                    ThreatVerdict10.Malicious,
                    Math.Clamp(externalMatch.Confidence, 1, 100),
                    externalMatch.DetectionName,
                    externalMatch.Evidence,
                    DateTime.UtcNow);
            }

            List<string> reasons = externalResults
                .Where(result => result.IsError)
                .SelectMany(result => result.Evidence)
                .ToList();
            int risk = 0;
            string extension = info.Extension;
            string? preferredDetection = null;
            bool strongIndicator = false;

            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ArchiveAnalysisResult10 archive = await AdvancedArchiveAnalyzer10
                    .AnalyzeAsync(fullPath, cancellationToken).ConfigureAwait(false);
                if (archive.IsMalicious)
                {
                    return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Malicious, 100,
                        archive.DetectionName, archive.Reasons, DateTime.UtcNow);
                }

                risk += archive.RiskScore;
                reasons.AddRange(archive.Reasons);
                if (archive.RiskScore >= 45)
                {
                    preferredDetection = archive.DetectionName;
                    strongIndicator = true;
                }
            }
            else
            {
                IReadOnlyList<YaraRuleMatch10> ruleMatches = await YaraRuleEngine10
                    .MatchFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
                YaraRuleMatch10? maliciousRule = ruleMatches.FirstOrDefault(match => match.IsMalicious);
                if (maliciousRule is not null)
                {
                    return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Malicious, 100,
                        maliciousRule.DetectionName,
                        ruleMatches.Select(match => match.Evidence).Distinct(StringComparer.Ordinal).ToArray(),
                        DateTime.UtcNow);
                }

                foreach (YaraRuleMatch10 match in ruleMatches)
                {
                    risk += Math.Min(55, match.RiskScore);
                    reasons.Add(match.Evidence);
                    preferredDetection ??= match.DetectionName;
                    if (match.RiskScore >= 45)
                        strongIndicator = true;
                }
            }

            if (ActiveContentExtensions.Contains(extension))
            {
                risk += 8;
                reasons.Add("Il file può contenere codice attivo, eseguibile o script.");
            }

            StaticAnalysisResult10 staticAnalysis = await StaticContentAnalyzer10
                .AnalyzeAsync(fullPath, cancellationToken).ConfigureAwait(false);
            risk += staticAnalysis.RiskScore;
            reasons.AddRange(staticAnalysis.Reasons);
            if (staticAnalysis.RiskScore >= 55)
                strongIndicator = true;

            double entropy = await EstimateEntropyAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (entropy >= 7.65 && info.Length >= 4_096)
            {
                risk += 18;
                reasons.Add($"Entropia elevata ({entropy:F2}), possibile compressione, cifratura o offuscamento.");
            }

            if (IsDoubleExtension(info.Name))
            {
                risk += 25;
                reasons.Add("Nome con doppia estensione potenzialmente ingannevole.");
            }

            if (IsFromTemporaryLocation(fullPath) && ActiveContentExtensions.Contains(extension))
            {
                risk += 15;
                reasons.Add("Contenuto attivo rilevato in una cartella temporanea.");
            }

            risk = Math.Clamp(risk, 0, 100);
            FalsePositiveAssessment10 reputation = FalsePositiveGuard10.Assess(
                fullPath,
                info.Length,
                info.LastWriteTimeUtc,
                risk,
                strongIndicator);
            int adjustedRisk = FalsePositiveGuard10.ApplyReduction(risk, reputation);
            if (reputation.RiskReduction > 0)
            {
                reasons.AddRange(reputation.Reasons);
                reasons.Add($"Riduzione reputazionale prudente: -{reputation.RiskReduction} punti ({risk} → {adjustedRisk}).");
            }
            risk = adjustedRisk;

            ThreatVerdict10 verdict = risk >= 45 ? ThreatVerdict10.Suspicious : ThreatVerdict10.Unknown;
            string detection = verdict == ThreatVerdict10.Suspicious
                ? preferredDetection ?? DetermineDetectionName(extension)
                : reputation.TrustedSignature ? "Reputation.Trusted.Signed" : "Unknown.File";
            int confidence = verdict == ThreatVerdict10.Suspicious
                ? Math.Clamp(45 + risk / 2, 55, 95)
                : reputation.TrustedSignature
                    ? Math.Clamp(75 + reputation.RiskReduction, 75, 95)
                    : Math.Clamp(55 - risk / 2, 20, 60);

            if (reasons.Count == 0)
                reasons.Add("Nessuna firma nota e nessun indicatore euristico rilevante.");

            return new FileScanResult10(fullPath, sha256, info.Length, verdict, confidence, detection,
                reasons.Distinct(StringComparer.Ordinal).ToArray(), DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return Error(fullPath, ex.Message);
        }
    }

    private static bool IsTrustedInternalComponent(string fullPath)
    {
        string normalized = Path.GetFullPath(fullPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string applicationRoot = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsInsideDirectory(normalized, applicationRoot))
            return true;

        string localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian");
        if (IsInsideDirectory(normalized, localRoot))
            return true;

        string commonRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian");
        return IsInsideDirectory(normalized, commonRoot);
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        string root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<double> EstimateEntropyAsync(string path, CancellationToken cancellationToken)
    {
        const int sampleLimit = 1024 * 1024;
        byte[] buffer = new byte[sampleLimit];
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 64, FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        if (total == 0) return 0;

        int[] counts = new int[256];
        for (int index = 0; index < total; index++) counts[buffer[index]]++;
        double entropy = 0;
        foreach (int count in counts)
        {
            if (count == 0) continue;
            double probability = (double)count / total;
            entropy -= probability * Math.Log2(probability);
        }
        return entropy;
    }

    private static bool IsDoubleExtension(string fileName)
    {
        string withoutLast = Path.GetFileNameWithoutExtension(fileName);
        string previousExtension = Path.GetExtension(withoutLast);
        return !string.IsNullOrWhiteSpace(previousExtension) &&
            ActiveContentExtensions.Contains(Path.GetExtension(fileName));
    }

    private static bool IsFromTemporaryLocation(string fullPath)
    {
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        return fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineDetectionName(string extension)
    {
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return "Heuristic.Suspicious.Archive";
        if (extension is ".ps1" or ".bat" or ".cmd" or ".vbs" or ".js" or ".jse" or ".hta" or ".wsf")
            return "Heuristic.Suspicious.Script";
        return "Heuristic.Suspicious.File";
    }

    private static FileScanResult10 Error(string path, string message) => new(
        path, string.Empty, 0, ThreatVerdict10.Error, 0, "Scan.Error", new[] { message }, DateTime.UtcNow);
}
