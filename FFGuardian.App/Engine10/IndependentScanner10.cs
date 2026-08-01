using System.Security.Cryptography;

namespace FFGuardian.Engine10;

internal sealed class IndependentScanner10
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".msi", ".msix", ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta"
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
                return new FileScanResult10(fullPath, string.Empty, 0, ThreatVerdict10.Unknown, 10,
                    "Empty.File", new[] { "Il file è vuoto." }, DateTime.UtcNow);

            string sha256 = await ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);

            if (await _database.IsAllowListedAsync(sha256, cancellationToken).ConfigureAwait(false))
            {
                return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Clean, 100,
                    "AllowList.Trusted", new[] { "Hash presente nella allowlist locale." }, DateTime.UtcNow);
            }

            SignatureEntry10? signature = await _database.FindSignatureAsync(sha256, cancellationToken).ConfigureAwait(false);
            if (signature is not null)
            {
                return new FileScanResult10(fullPath, sha256, info.Length, ThreatVerdict10.Malicious,
                    Math.Clamp(signature.Confidence, 1, 100), signature.DetectionName,
                    new[] { $"Corrispondenza firma: {signature.Id}." }, DateTime.UtcNow);
            }

            List<string> reasons = new();
            int risk = 0;
            string extension = info.Extension;

            if (ExecutableExtensions.Contains(extension))
            {
                risk += 15;
                reasons.Add("Il file può contenere codice eseguibile o script.");
            }

            double entropy = await EstimateEntropyAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (entropy >= 7.55)
            {
                risk += 20;
                reasons.Add($"Entropia elevata ({entropy:F2}), possibile compressione o cifratura.");
            }

            if (IsDoubleExtension(info.Name))
            {
                risk += 25;
                reasons.Add("Nome con doppia estensione potenzialmente ingannevole.");
            }

            if (IsFromTemporaryLocation(fullPath) && ExecutableExtensions.Contains(extension))
            {
                risk += 15;
                reasons.Add("Eseguibile rilevato in una cartella temporanea.");
            }

            ThreatVerdict10 verdict = risk >= 45 ? ThreatVerdict10.Suspicious : ThreatVerdict10.Unknown;
            string detection = verdict == ThreatVerdict10.Suspicious ? "Heuristic.Suspicious.File" : "Unknown.File";
            int confidence = verdict == ThreatVerdict10.Suspicious ? Math.Min(80, 40 + risk) : Math.Max(20, 50 - risk);

            if (reasons.Count == 0)
                reasons.Add("Nessuna firma nota e nessun indicatore euristico rilevante.");

            return new FileScanResult10(fullPath, sha256, info.Length, verdict, confidence, detection, reasons, DateTime.UtcNow);
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
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 128, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<double> EstimateEntropyAsync(string path, CancellationToken cancellationToken)
    {
        const int sampleLimit = 1024 * 1024;
        byte[] buffer = new byte[sampleLimit];
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 64, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }

        if (total == 0) return 0;

        int[] counts = new int[256];
        for (int i = 0; i < total; i++) counts[buffer[i]]++;

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
        string previous = Path.GetExtension(withoutLast);
        return !string.IsNullOrWhiteSpace(previous) && ExecutableExtensions.Contains(Path.GetExtension(fileName));
    }

    private static bool IsFromTemporaryLocation(string fullPath)
    {
        string temp = Path.GetFullPath(Path.GetTempPath());
        return fullPath.StartsWith(temp, StringComparison.OrdinalIgnoreCase);
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