using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian;

internal sealed record YaraPortableProbeResult(
    bool Active,
    bool Installed,
    string ExecutablePath,
    string Version,
    string Detail,
    string StandardOutput,
    string StandardError,
    int ExitCode);

internal static class YaraPortableManager29
{
    private const string SelfTestRuleName = "FFGuardian_Yara_Portable_SelfTest";
    private const string SelfTestMarker = "FFGUARDIAN_YARA_PORTABLE_SELFTEST_29";
    private static readonly Uri OfficialLatestReleaseApi =
        new("https://api.github.com/repos/VirusTotal/yara/releases/latest");

    private static readonly string[] RelativeCandidates =
    [
        Path.Combine("Engine", "Yara", "yara64.exe"),
        Path.Combine("Engine", "Yara", "yara.exe"),
        Path.Combine("Tools", "Yara", "yara64.exe"),
        Path.Combine("Tools", "Yara", "yara.exe")
    ];

    public static string InstallDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Engine", "Yara"));

    public static string? FindExecutable()
    {
        string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        foreach (string relative in RelativeCandidates)
        {
            string candidate = Path.GetFullPath(Path.Combine(baseDirectory, relative));
            if (!candidate.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
                continue;
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public static async Task<YaraPortableProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        string? executable = FindExecutable();
        if (executable is null)
        {
            return new YaraPortableProbeResult(false, false, string.Empty, "--",
                "Eseguibile YARA non trovato nei percorsi portable previsti.",
                string.Empty, string.Empty, -1);
        }

        YaraCommandResult version = await RunAsync(executable, ["--version"],
            Path.GetDirectoryName(executable)!, TimeSpan.FromSeconds(20), cancellationToken)
            .ConfigureAwait(false);

        if (version.TimedOut || version.ExitCode != 0)
        {
            string detail = version.TimedOut
                ? "Timeout durante yara --version."
                : $"yara --version non riuscito (exit code {version.ExitCode}).";
            return new YaraPortableProbeResult(false, true, executable, "--", detail,
                version.StandardOutput, version.StandardError, version.ExitCode);
        }

        string versionText = FirstNonEmptyLine(version.StandardOutput, version.StandardError);
        YaraCommandResult selfTest = await RunSelfTestAsync(executable, cancellationToken)
            .ConfigureAwait(false);
        bool matched = !selfTest.TimedOut && selfTest.ExitCode == 0 &&
                       selfTest.StandardOutput.Contains(SelfTestRuleName,
                           StringComparison.OrdinalIgnoreCase);

        if (!matched)
        {
            string detail = selfTest.TimedOut
                ? "Timeout durante il test della regola YARA innocua."
                : $"Test regola innocua non superato (exit code {selfTest.ExitCode}).";
            return new YaraPortableProbeResult(false, true, executable, versionText, detail,
                selfTest.StandardOutput, selfTest.StandardError, selfTest.ExitCode);
        }

        return new YaraPortableProbeResult(true, true, executable, versionText,
            "Motore portable, --version e regola innocua verificati realmente.",
            selfTest.StandardOutput, selfTest.StandardError, selfTest.ExitCode);
    }

    public static async Task<YaraPortableProbeResult> InstallOfficialWindowsX64Async(
        IProgress<int>? progress,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        status?.Report("Ricerca del pacchetto ufficiale YARA Windows x64...");
        progress?.Report(5);

        using HttpClient client = CreateOfficialClient();
        using HttpResponseMessage releaseResponse = await client.GetAsync(
            OfficialLatestReleaseApi, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        releaseResponse.EnsureSuccessStatusCode();

        await using Stream releaseStream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        OfficialRelease? release = await JsonSerializer.DeserializeAsync<OfficialRelease>(releaseStream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            .ConfigureAwait(false);
        if (release is null || release.Assets is null)
            throw new InvalidDataException("Risposta release ufficiale YARA non valida.");

        OfficialAsset asset = release.Assets.FirstOrDefault(IsOfficialWindowsX64Asset)
            ?? throw new InvalidDataException("Pacchetto ufficiale YARA Windows x64 non trovato.");

        if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !downloadUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !downloadUri.AbsolutePath.StartsWith("/VirusTotal/yara/releases/download/",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("URL del pacchetto YARA non ufficiale.");

        string expectedHash = ParseDigest(asset.Digest);
        string workRoot = Path.Combine(Path.GetTempPath(), "FFGuardian-Yara-" + Guid.NewGuid().ToString("N"));
        string archivePath = Path.Combine(workRoot, Path.GetFileName(asset.Name));
        string extractPath = Path.Combine(workRoot, "extract");
        Directory.CreateDirectory(workRoot);

        try
        {
            status?.Report($"Download ufficiale: {asset.Name}");
            progress?.Report(15);
            using HttpResponseMessage packageResponse = await client.GetAsync(downloadUri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            packageResponse.EnsureSuccessStatusCode();
            await using (Stream source = await packageResponse.Content.ReadAsStreamAsync(cancellationToken)
                             .ConfigureAwait(false))
            await using (FileStream destination = new(archivePath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 81920, useAsync: true))
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            progress?.Report(55);
            status?.Report("Verifica SHA-256 del pacchetto...");
            string actualHash;
            await using (FileStream archive = new(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken)
                    .ConfigureAwait(false));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException(
                    $"SHA-256 YARA non valido. Atteso {expectedHash}, ottenuto {actualHash}.");

            progress?.Report(70);
            status?.Report("Estrazione in Engine\\Yara...");
            ZipFile.ExtractToDirectory(archivePath, extractPath, overwriteFiles: true);
            string? extractedYara = Directory.EnumerateFiles(extractPath, "yara64.exe",
                    SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(extractPath, "yara.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
            if (extractedYara is null)
                throw new InvalidDataException("Il pacchetto ufficiale non contiene yara64.exe o yara.exe.");

            string sourceDirectory = Path.GetDirectoryName(extractedYara)!;
            Directory.CreateDirectory(InstallDirectory);
            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(InstallDirectory, Path.GetFileName(file)), overwrite: true);

            progress?.Report(90);
            status?.Report("Verifica reale del motore installato...");
            YaraPortableProbeResult probe = await ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (!probe.Active)
                throw new InvalidOperationException("YARA installato ma test reale non superato: " + probe.Detail);

            progress?.Report(100);
            status?.Report("YARA portable installato e verificato.");
            return probe;
        }
        finally
        {
            try { if (Directory.Exists(workRoot)) Directory.Delete(workRoot, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<YaraCommandResult> RunSelfTestAsync(
        string executable, CancellationToken cancellationToken)
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Yara-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string rulePath = Path.Combine(root, "selftest.yar");
        string samplePath = Path.Combine(root, "selftest.txt");
        try
        {
            string rule = $$"""
                rule {{SelfTestRuleName}}
                {
                    strings:
                        $marker = "{{SelfTestMarker}}" ascii
                    condition:
                        $marker
                }
                """;
            await File.WriteAllTextAsync(rulePath, rule, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(samplePath, SelfTestMarker, Encoding.ASCII, cancellationToken)
                .ConfigureAwait(false);
            return await RunAsync(executable, [rulePath, samplePath],
                Path.GetDirectoryName(executable)!, TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<YaraCommandResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new()
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = start };
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException("Impossibile avviare YARA.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        stopwatch.Stop();
        int exitCode = timedOut ? -1 : process.ExitCode;
        return new YaraCommandResult(exitCode, stdout, stderr, timedOut, stopwatch.Elapsed);
    }

    private static HttpClient CreateOfficialClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FFGuardian", "5.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static bool IsOfficialWindowsX64Asset(OfficialAsset asset)
    {
        string name = asset.Name ?? string.Empty;
        bool zip = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        bool windows = name.Contains("win64", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("windows-x64", StringComparison.OrdinalIgnoreCase) ||
                       (name.Contains("windows", StringComparison.OrdinalIgnoreCase) &&
                        name.Contains("x64", StringComparison.OrdinalIgnoreCase));
        return zip && windows && !string.IsNullOrWhiteSpace(asset.Digest);
    }

    private static string ParseDigest(string? digest)
    {
        const string prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(digest) ||
            !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Digest SHA-256 ufficiale non disponibile.");
        string hash = digest[prefix.Length..].Trim();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new CryptographicException("Digest SHA-256 ufficiale non valido.");
        return hash;
    }

    private static string FirstNonEmptyLine(params string[] values) => values
        .SelectMany(value => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0) ?? "--";

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private sealed record YaraCommandResult(int ExitCode, string StandardOutput,
        string StandardError, bool TimedOut, TimeSpan Duration);

    private sealed record OfficialRelease(string? TagName, IReadOnlyList<OfficialAsset>? Assets);
    private sealed record OfficialAsset(string? Name, string? BrowserDownloadUrl, string? Digest);
}
