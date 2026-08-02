using System.Collections.Concurrent;

namespace FFGuardian.Engine10;

internal sealed record FalsePositiveAssessment10(
    int RiskReduction,
    bool TrustedSignature,
    bool TrustedLocation,
    string Publisher,
    IReadOnlyList<string> Reasons);

internal static class FalsePositiveGuard10
{
    private sealed record CacheEntry10(
        long Length,
        DateTime LastWriteUtc,
        DateTime ExpiresUtc,
        FalsePositiveAssessment10 Assessment);

    private static readonly ConcurrentDictionary<string, CacheEntry10> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] HighReputationPublishers =
    {
        "Microsoft", "Windows", "Google", "Mozilla", "Adobe", "Intel",
        "NVIDIA", "Advanced Micro Devices", "AMD", "Dell", "HP", "Lenovo"
    };

    internal static FalsePositiveAssessment10 Assess(
        string fullPath,
        long length,
        DateTime lastWriteUtc,
        int rawRisk,
        bool hasStrongIndicator)
    {
        if (hasStrongIndicator || rawRisk >= 80 || !OperatingSystem.IsWindows())
            return None("Indicatori forti presenti: reputazione non applicata.");

        if (Cache.TryGetValue(fullPath, out CacheEntry10? cached) &&
            cached.Length == length &&
            cached.LastWriteUtc == lastWriteUtc &&
            cached.ExpiresUtc > DateTime.UtcNow)
        {
            return cached.Assessment;
        }

        FalsePositiveAssessment10 assessment = Calculate(fullPath, rawRisk);
        Cache[fullPath] = new CacheEntry10(
            length,
            lastWriteUtc,
            DateTime.UtcNow.AddMinutes(30),
            assessment);
        TrimCache();
        return assessment;
    }

    private static FalsePositiveAssessment10 Calculate(string fullPath, int rawRisk)
    {
        global::FFGuardian.AuthenticodeResult100 signature =
            global::FFGuardian.AuthenticodeVerifier100.Verify(fullPath);

        bool trustedLocation = IsTrustedLocation(fullPath);
        bool highReputationPublisher = signature.IsTrusted &&
            HighReputationPublishers.Any(name =>
                signature.Signer.Contains(name, StringComparison.OrdinalIgnoreCase));

        int reduction = 0;
        List<string> reasons = [];

        if (signature.IsTrusted)
        {
            reduction += 12;
            reasons.Add($"Firma Authenticode valida: {signature.Signer}.");
        }

        if (signature.IsTrusted && trustedLocation)
        {
            reduction += 8;
            reasons.Add("File firmato presente in un percorso applicativo o di sistema protetto.");
        }

        if (highReputationPublisher)
        {
            reduction += 5;
            reasons.Add("Editore con reputazione elevata e catena di firma valida.");
        }

        // La reputazione non deve trasformare un rischio medio/alto in un verdetto pulito.
        reduction = Math.Min(reduction, Math.Max(0, rawRisk - 20));
        reduction = Math.Clamp(reduction, 0, 25);

        return new FalsePositiveAssessment10(
            reduction,
            signature.IsTrusted,
            trustedLocation,
            signature.Signer,
            reasons);
    }

    internal static int ApplyReduction(int rawRisk, FalsePositiveAssessment10 assessment) =>
        Math.Clamp(rawRisk - Math.Clamp(assessment.RiskReduction, 0, 25), 0, 100);

    internal static bool IsTrustedLocation(string fullPath)
    {
        string normalized;
        try { normalized = Path.GetFullPath(fullPath); }
        catch { return false; }

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return IsUnder(normalized, windows) ||
               IsUnder(normalized, programFiles) ||
               IsUnder(normalized, programFilesX86);
    }

    private static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return false;
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static FalsePositiveAssessment10 None(string reason) =>
        new(0, false, false, string.Empty, new[] { reason });

    private static void TrimCache()
    {
        if (Cache.Count <= 4096)
            return;

        DateTime now = DateTime.UtcNow;
        foreach ((string key, CacheEntry10 value) in Cache)
        {
            if (value.ExpiresUtc <= now)
                Cache.TryRemove(key, out _);
        }

        if (Cache.Count <= 4096)
            return;

        foreach (string key in Cache.Keys.Take(Cache.Count - 4096))
            Cache.TryRemove(key, out _);
    }
}
