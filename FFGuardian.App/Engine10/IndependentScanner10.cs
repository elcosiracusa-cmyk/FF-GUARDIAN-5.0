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

            if (info.Length == 0)
            {
                return new FileScanResult10(
                    fullPath,
                    string.Empty,
                    0,
                    ThreatVerdict10.Unknown,
                    10,
                    "Empty.File",
                    new[] { "Il file è vuoto." },
                    DateTime.UtcNow);
            }

            string sha256 = await ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);

            if (await _database.IsAllowListedAsync(sha256, cancellationToken).ConfigureAwait(false))
            {
                return new FileScanResult10(
                    fullPath,
                    sha256,
                    info.Length,
                    ThreatVerdict10.Clean,
                    100,
                    "AllowList.Trusted",
                    new[] { "Hash presente nella allowlist locale." },
                    DateTime.UtcNow);
            }

            SignatureEntry10? signature = await _database
                .FindSignatureAsync(sha256, cancellationToken)
                .ConfigureAwait(false);
            if (signature is not null)
            {
                return new FileScanResult10(
                    fullPath,
                    sha256,
                    info.Length,
                    ThreatVerdict10.Malicious,
                    Math.Clamp(signature.Confidence, 1, 100),
                    signature.DetectionName,
                    new[] { $"Corrispondenza firma: {signature.Id}." },
                    DateTime.UtcNow);
            }

            List<string> reasons = [];
            int risk = 0;
            string extension = info.Extension;

            if (ActiveContentExtensions.Contains(extension))
            {
                risk += 8;
                reasons.Add("Il file può contenere codice attivo, eseguibile o script.");
            }

            StaticAnalysisResult10 staticAnalysis = await StaticContentAnalyzer10
                .AnalyzeAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            risk += staticAnalysis.RiskScore;
            reasons.AddRange(staticAnalysis.Reasons);

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
            ThreatVerdict10 verdict = risk switch
            {
                >= 75 => ThreatVerdict10.Suspicious,
                >= 45 => ThreatVerdict10.Suspicious,
                _ => ThreatVerdict10.Unknown
            };

            string detection = verdict == ThreatVerdict10.Suspicious
                ? DetermineDetectionName(extension)
                : "Unknown.File";
            int confidence = verdict == ThreatVerdict10.Suspicious
                ? Math.Clamp(45 + risk / 2, 55, 95)
                : Math.Clamp(55 - risk / 2, 20, 60);

            if (reasons.Count == 0)
                reasons.Add("Nessuna firma nota e nessun indicatore euristico rilevante.");

            return new FileScanResult10(
                fullPath,
                sha256,
                info.Length,
                verdict,
                confidence,
                detection,
                reasons.Distinct(StringComparer.Ordinal).ToArray(),
                DateTime.UtcNow);
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

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<double> EstimateEntropyAsync(string path, CancellationToken cancellationToken)
    {
        const int sampleLimit = 1024 * 1024;
        byte[] buffer = new byte[sampleLimit];
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream
                .ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }

        if (total == 0)
            return 0;

        int[] counts = new int[256];
        for (int index = 0; index < total; index++)
            counts[buffer[index]]++;

        double entropy = 0;
        foreach (int count in counts)
        {
            if (count == 0)
                continue;
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
        path,
        string.Empty,
        0,
        ThreatVerdict10.Error,
        0,
        "Scan.Error",
        new[] { message },
        DateTime.UtcNow);
}
