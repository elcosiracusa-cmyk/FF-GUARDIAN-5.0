using System.IO.Compression;

namespace FFGuardian.Engine10;

internal sealed record ArchiveAnalysisResult10(
    int RiskScore,
    bool IsMalicious,
    string DetectionName,
    IReadOnlyList<string> Reasons);

internal static class AdvancedArchiveAnalyzer10
{
    private const int MaximumEntries = 2_000;
    private const long MaximumEntryBytes = 32L * 1024 * 1024;
    private const long MaximumTotalBytes = 512L * 1024 * 1024;
    private const int MaximumBytesPerRuleScan = 4 * 1024 * 1024;

    private static readonly HashSet<string> ActiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".sys", ".msi", ".msix",
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf", ".lnk"
    };

    public static async Task<ArchiveAnalysisResult10> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        List<string> reasons = [];
        int risk = 0;
        long totalUncompressed = 0;
        long totalCompressed = 0;
        int activeEntries = 0;
        int traversalEntries = 0;
        int oversizedEntries = 0;
        int inspectedEntries = 0;

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaximumEntries)
        {
            risk += 35;
            reasons.Add($"Numero di elementi oltre il limite di sicurezza: {archive.Entries.Count:N0}.");
        }

        foreach (ZipArchiveEntry entry in archive.Entries.Take(MaximumEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            inspectedEntries++;
            totalUncompressed = SaturatingAdd(totalUncompressed, entry.Length);
            totalCompressed = SaturatingAdd(totalCompressed, entry.CompressedLength);

            string normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Split('/').Any(segment => segment == ".."))
                traversalEntries++;

            string extension = Path.GetExtension(entry.Name);
            if (ActiveExtensions.Contains(extension))
                activeEntries++;

            if (entry.Length > MaximumEntryBytes)
            {
                oversizedEntries++;
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name) || entry.Length == 0)
                continue;

            int sampleLength = (int)Math.Min(entry.Length, MaximumBytesPerRuleScan);
            byte[] sample = new byte[sampleLength];
            await using Stream entryStream = entry.Open();
            int totalRead = 0;
            while (totalRead < sample.Length)
            {
                int read = await entryStream.ReadAsync(sample.AsMemory(totalRead), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;
                totalRead += read;
            }

            IReadOnlyList<YaraRuleMatch10> matches = YaraRuleEngine10.MatchBytes(sample.AsSpan(0, totalRead));
            YaraRuleMatch10? malicious = matches.FirstOrDefault(match => match.IsMalicious);
            if (malicious is not null)
            {
                reasons.Add($"{malicious.Evidence} Elemento: {entry.FullName}.");
                return new ArchiveAnalysisResult10(100, true, malicious.DetectionName, reasons);
            }

            foreach (YaraRuleMatch10 match in matches)
            {
                risk += Math.Min(35, match.RiskScore / 2);
                reasons.Add($"{match.Evidence} Elemento: {entry.FullName}.");
            }
        }

        if (traversalEntries > 0)
        {
            risk += 40;
            reasons.Add($"Percorsi con attraversamento directory: {traversalEntries}.");
        }

        if (activeEntries > 0)
        {
            risk += Math.Min(35, 10 + activeEntries * 3);
            reasons.Add($"Elementi eseguibili o script: {activeEntries}.");
        }

        if (oversizedEntries > 0)
        {
            risk += 18;
            reasons.Add($"Elementi oltre il limite di analisi profonda: {oversizedEntries}.");
        }

        if (totalUncompressed > MaximumTotalBytes)
        {
            risk += 40;
            reasons.Add("Dimensione totale dichiarata oltre il limite di sicurezza.");
        }

        double ratio = totalCompressed > 0 ? (double)totalUncompressed / totalCompressed : 0;
        if (totalUncompressed > 64L * 1024 * 1024 && ratio > 150)
        {
            risk += 45;
            reasons.Add($"Rapporto di compressione anomalo ({ratio:F0}:1), possibile ZIP bomb.");
        }

        reasons.Add($"Elementi ZIP ispezionati: {inspectedEntries:N0}.");
        return new ArchiveAnalysisResult10(
            Math.Clamp(risk, 0, 100),
            false,
            risk >= 45 ? "Heuristic.Suspicious.Archive" : "Archive.NoStrongMatch",
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
            return long.MaxValue;
        return left + right;
    }
}
