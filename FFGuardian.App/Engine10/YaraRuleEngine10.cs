using System.Text;

namespace FFGuardian.Engine10;

internal sealed record YaraRuleMatch10(
    string RuleId,
    string DetectionName,
    int RiskScore,
    bool IsMalicious,
    string Evidence);

internal static class YaraRuleEngine10
{
    private const int MaximumBytesToInspect = 4 * 1024 * 1024;

    private sealed record Rule10(
        string Id,
        string Detection,
        int Risk,
        bool Malicious,
        string[] AllOf,
        string[] AnyOf);

    private static readonly Rule10[] Rules =
    {
        new(
            "FFG-YARA-TEST-EICAR",
            "Test.EICAR",
            100,
            true,
            new[] { "EICAR-STANDARD-ANTIVIRUS-TEST-FILE" },
            Array.Empty<string>()),
        new(
            "FFG-YARA-PS-DOWNLOAD-EXEC",
            "Yara.Suspicious.PowerShell.DownloadExecute",
            72,
            false,
            new[] { "powershell" },
            new[] { "invoke-expression", "downloadstring", "frombase64string", "-encodedcommand" }),
        new(
            "FFG-YARA-LOLBIN-CHAIN",
            "Yara.Suspicious.LolBinChain",
            66,
            false,
            Array.Empty<string>(),
            new[] { "certutil.exe", "bitsadmin.exe", "regsvr32.exe", "mshta.exe", "rundll32.exe" }),
        new(
            "FFG-YARA-RANSOM-NOTE",
            "Yara.Suspicious.RansomNote",
            68,
            false,
            Array.Empty<string>(),
            new[] { "your files have been encrypted", "all your files are encrypted", "decrypt your files", "bitcoin wallet" }),
        new(
            "FFG-YARA-SHADOW-COPY-TAMPER",
            "Yara.Suspicious.ShadowCopyTamper",
            78,
            false,
            Array.Empty<string>(),
            new[] { "vssadmin delete shadows", "wmic shadowcopy delete", "delete shadows /all" })
    };

    public static async Task<IReadOnlyList<YaraRuleMatch10>> MatchFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FileInfo info = new(path);
        if (!info.Exists || info.Length == 0)
            return Array.Empty<YaraRuleMatch10>();

        int length = (int)Math.Min(info.Length, MaximumBytesToInspect);
        byte[] buffer = new byte[length];
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }

        return MatchBytes(buffer.AsSpan(0, total));
    }

    public static IReadOnlyList<YaraRuleMatch10> MatchBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return Array.Empty<YaraRuleMatch10>();

        string text = Encoding.Latin1.GetString(bytes).ToLowerInvariant();
        List<YaraRuleMatch10> matches = [];

        foreach (Rule10 rule in Rules)
        {
            bool allSatisfied = rule.AllOf.Length == 0 ||
                rule.AllOf.All(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
            bool anySatisfied = rule.AnyOf.Length == 0 ||
                rule.AnyOf.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

            if (!allSatisfied || !anySatisfied)
                continue;

            matches.Add(new YaraRuleMatch10(
                rule.Id,
                rule.Detection,
                rule.Risk,
                rule.Malicious,
                $"Corrispondenza regola {rule.Id}."));
        }

        return matches
            .OrderByDescending(match => match.IsMalicious)
            .ThenByDescending(match => match.RiskScore)
            .ToArray();
    }
}
