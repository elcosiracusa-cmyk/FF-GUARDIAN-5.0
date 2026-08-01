using System.IO.Compression;
using System.Text;

namespace FFGuardian.Engine10;

internal sealed record StaticAnalysisResult10(int RiskScore, IReadOnlyList<string> Reasons);

internal static class StaticContentAnalyzer10
{
    private const int MaximumTextCharacters = 256 * 1024;
    private const int MaximumArchiveEntries = 10_000;
    private const long MaximumArchiveUncompressedBytes = 2L * 1024 * 1024 * 1024;

    private static readonly HashSet<string> PeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".sys"
    };

    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf"
    };

    private static readonly HashSet<string> ExecutableArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".msi", ".msix", ".sys",
        ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".hta", ".wsf", ".lnk"
    };

    private static readonly string[] SuspiciousScriptIndicators =
    {
        "invoke-expression", "iex(", "downloadstring", "downloadfile", "frombase64string",
        "-encodedcommand", " -enc ", "wscript.shell", "shell.application", "mshta.exe",
        "regsvr32.exe", "rundll32.exe", "certutil.exe", "bitsadmin.exe", "start-bitstransfer",
        "new-object net.webclient", "invoke-webrequest", "curl.exe", "wget.exe"
    };

    public static async Task<StaticAnalysisResult10> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extension = Path.GetExtension(path);

        if (PeExtensions.Contains(extension))
            return await AnalyzePortableExecutableAsync(path, cancellationToken).ConfigureAwait(false);

        if (ScriptExtensions.Contains(extension))
            return await AnalyzeScriptAsync(path, cancellationToken).ConfigureAwait(false);

        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return AnalyzeZipArchive(path, cancellationToken);

        return new StaticAnalysisResult10(0, Array.Empty<string>());
    }

    private static async Task<StaticAnalysisResult10> AnalyzePortableExecutableAsync(
        string path,
        CancellationToken cancellationToken)
    {
        List<string> reasons = [];
        int risk = 0;

        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length < 64)
            return new StaticAnalysisResult10(35, new[] { "File troppo piccolo per contenere una struttura PE valida." });

        byte[] dosHeader = new byte[64];
        int read = await ReadExactlyOrLessAsync(stream, dosHeader, cancellationToken).ConfigureAwait(false);
        if (read < dosHeader.Length || dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
            return new StaticAnalysisResult10(35, new[] { "Estensione eseguibile con intestazione DOS non valida." });

        int peOffset = BitConverter.ToInt32(dosHeader, 0x3C);
        if (peOffset < 64 || peOffset > stream.Length - 24)
            return new StaticAnalysisResult10(35, new[] { "Offset PE fuori dai limiti del file." });

        stream.Position = peOffset;
        byte[] peHeader = new byte[24];
        read = await ReadExactlyOrLessAsync(stream, peHeader, cancellationToken).ConfigureAwait(false);
        bool validSignature = read == peHeader.Length &&
            peHeader[0] == (byte)'P' && peHeader[1] == (byte)'E' && peHeader[2] == 0 && peHeader[3] == 0;
        if (!validSignature)
            return new StaticAnalysisResult10(35, new[] { "Firma PE non valida." });

        ushort machine = BitConverter.ToUInt16(peHeader, 4);
        ushort sectionCount = BitConverter.ToUInt16(peHeader, 6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(peHeader, 20);

        if (machine is not (0x014c or 0x8664 or 0x01c0 or 0xaa64))
        {
            risk += 8;
            reasons.Add($"Architettura PE insolita: 0x{machine:X4}.");
        }

        if (sectionCount is 0 or > 96)
        {
            risk += 25;
            reasons.Add($"Numero di sezioni PE anomalo: {sectionCount}.");
        }
        else if (sectionCount > 16)
        {
            risk += 8;
            reasons.Add($"Numero di sezioni PE elevato: {sectionCount}.");
        }

        if (optionalHeaderSize < 96 || peOffset + 24L + optionalHeaderSize > stream.Length)
        {
            risk += 25;
            reasons.Add("Intestazione opzionale PE assente, troncata o incoerente.");
        }
        else
        {
            int optionalBytesToRead = Math.Min((int)optionalHeaderSize, 512);
            byte[] optionalHeader = new byte[optionalBytesToRead];
            read = await ReadExactlyOrLessAsync(stream, optionalHeader, cancellationToken).ConfigureAwait(false);
            if (read >= 2)
            {
                ushort magic = BitConverter.ToUInt16(optionalHeader, 0);
                if (magic is not (0x010b or 0x020b))
                {
                    risk += 20;
                    reasons.Add($"Formato intestazione opzionale PE non riconosciuto: 0x{magic:X4}.");
                }
            }
        }

        global::FFGuardian.AuthenticodeResult100 signature =
            global::FFGuardian.AuthenticodeVerifier100.Verify(path);
        if (!signature.IsSigned)
        {
            risk += 8;
            reasons.Add("Firma Authenticode assente.");
        }
        else if (!signature.IsTrusted)
        {
            risk += 25;
            reasons.Add("Firma Authenticode presente ma non attendibile.");
        }

        return new StaticAnalysisResult10(Math.Clamp(risk, 0, 100), reasons);
    }

    private static async Task<StaticAnalysisResult10> AnalyzeScriptAsync(
        string path,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[MaximumTextCharacters];
        using StreamReader reader = new(
            path,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 8192);

        int read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (read == 0)
            return new StaticAnalysisResult10(0, Array.Empty<string>());

        string content = new(buffer, 0, read);
        string[] matches = SuspiciousScriptIndicators
            .Where(indicator => content.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<string> reasons = [];
        int risk = Math.Min(45, matches.Length * 8);
        if (matches.Length > 0)
            reasons.Add($"Indicatori di download, esecuzione o offuscamento nello script: {matches.Length}.");

        int longestLine = content.Split('\n').Select(line => line.Length).DefaultIfEmpty(0).Max();
        if (longestLine > 8_192)
        {
            risk += 12;
            reasons.Add("Script con una riga eccezionalmente lunga, possibile offuscamento.");
        }

        int base64LikeRuns = CountBase64LikeRuns(content);
        if (base64LikeRuns > 0)
        {
            risk += Math.Min(18, base64LikeRuns * 6);
            reasons.Add($"Sequenze lunghe compatibili con contenuto codificato: {base64LikeRuns}.");
        }

        return new StaticAnalysisResult10(Math.Clamp(risk, 0, 100), reasons);
    }

    private static StaticAnalysisResult10 AnalyzeZipArchive(string path, CancellationToken cancellationToken)
    {
        List<string> reasons = [];
        int risk = 0;
        int executableEntries = 0;
        int traversalEntries = 0;
        long totalCompressed = 0;
        long totalUncompressed = 0;

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            risk += 30;
            reasons.Add($"Archivio con un numero eccessivo di elementi: {archive.Entries.Count:N0}.");
        }

        foreach (ZipArchiveEntry entry in archive.Entries.Take(MaximumArchiveEntries + 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalCompressed = SaturatingAdd(totalCompressed, entry.CompressedLength);
            totalUncompressed = SaturatingAdd(totalUncompressed, entry.Length);

            string normalizedName = entry.FullName.Replace('\\', '/');
            if (normalizedName.StartsWith("/", StringComparison.Ordinal) ||
                normalizedName.Split('/').Any(segment => segment == ".."))
                traversalEntries++;

            if (ExecutableArchiveExtensions.Contains(Path.GetExtension(entry.Name)))
                executableEntries++;
        }

        if (traversalEntries > 0)
        {
            risk += 35;
            reasons.Add($"Percorsi di archivio non sicuri o con attraversamento directory: {traversalEntries}.");
        }

        if (executableEntries > 0)
        {
            risk += Math.Min(30, 8 + executableEntries * 3);
            reasons.Add($"Elementi eseguibili o script presenti nell'archivio: {executableEntries}.");
        }

        if (totalUncompressed > MaximumArchiveUncompressedBytes)
        {
            risk += 30;
            reasons.Add("Dimensione totale dichiarata dell'archivio superiore al limite di sicurezza.");
        }

        double ratio = totalCompressed > 0 ? (double)totalUncompressed / totalCompressed : 0;
        if (totalUncompressed > 100L * 1024 * 1024 && ratio > 200)
        {
            risk += 35;
            reasons.Add($"Rapporto di compressione anomalo ({ratio:F0}:1), possibile archivio bomba.");
        }

        return new StaticAnalysisResult10(Math.Clamp(risk, 0, 100), reasons);
    }

    private static async Task<int> ReadExactlyOrLessAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = await stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static int CountBase64LikeRuns(string content)
    {
        int runs = 0;
        int current = 0;
        foreach (char character in content)
        {
            bool compatible = char.IsLetterOrDigit(character) || character is '+' or '/' or '=';
            if (compatible)
            {
                current++;
            }
            else
            {
                if (current >= 256)
                    runs++;
                current = 0;
            }
        }
        if (current >= 256)
            runs++;
        return runs;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
            return long.MaxValue;
        return left + right;
    }
}
