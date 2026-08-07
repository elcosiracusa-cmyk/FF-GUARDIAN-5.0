using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace FFGuardian.PremiumWpf;

public sealed record UpdateCheckItem(
    string Name,
    string InstalledVersion,
    string LatestVersion,
    bool CheckSucceeded,
    bool UpdateAvailable,
    string Message,
    string ReleaseUrl);

public sealed class GitHubUpdateService : IDisposable
{
    private const string SoftwareLatestRelease = "https://api.github.com/repos/elcosiracusa-cmyk/FF-GUARDIAN-5.0/releases/latest";
    private const string YaraLatestRelease = "https://api.github.com/repos/VirusTotal/yara/releases/latest";
    private const string ClamAvLatestRelease = "https://api.github.com/repos/Cisco-Talos/clamav/releases/latest";

    private readonly HttpClient _client;
    private bool _disposed;

    public GitHubUpdateService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FFGuardian", "10.0.1"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<IReadOnlyList<UpdateCheckItem>> CheckAllAsync(CancellationToken cancellationToken)
    {
        string softwareVersion = typeof(GitHubUpdateService).Assembly.GetName().Version?.ToString(3) ?? "10.0.1";
        Task<UpdateCheckItem> software = CheckReleaseAsync("FFGuardian", softwareVersion, SoftwareLatestRelease, cancellationToken);
        Task<UpdateCheckItem> yara = CheckReleaseAsync("YARA", "4.5.5", YaraLatestRelease, cancellationToken);
        Task<UpdateCheckItem> clamAv = CheckReleaseAsync("ClamAV", "1.5.3", ClamAvLatestRelease, cancellationToken);
        return await Task.WhenAll(software, yara, clamAv).ConfigureAwait(false);
    }

    public static void OpenRelease(UpdateCheckItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!Uri.TryCreate(item.ReleaseUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("URL di aggiornamento non valido.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private async Task<UpdateCheckItem> CheckReleaseAsync(
        string name,
        string installedVersion,
        string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(name, installedVersion, "--", true, false, "Nessuna release pubblicata su GitHub.", string.Empty);

            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = document.RootElement;
            string tag = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
            string releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
            string latestVersion = NormalizeVersion(tag);
            bool updateAvailable = TryCompareVersions(latestVersion, installedVersion, out int comparison) && comparison > 0;
            string message = updateAvailable
                ? $"Aggiornamento disponibile: {installedVersion} → {latestVersion}."
                : $"Aggiornato. Versione installata {installedVersion}; ultima {latestVersion}.";
            return new(name, installedVersion, latestVersion, true, updateAvailable, message, releaseUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(name, installedVersion, "--", false, false, $"Controllo non riuscito: {exception.Message}", string.Empty);
        }
    }

    private static string NormalizeVersion(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("clamav-", StringComparison.OrdinalIgnoreCase)) normalized = normalized[7..];
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        return normalized;
    }

    private static bool TryCompareVersions(string latest, string installed, out int comparison)
    {
        comparison = 0;
        if (!Version.TryParse(latest, out Version? latestVersion) || !Version.TryParse(installed, out Version? installedVersion)) return false;
        comparison = latestVersion.CompareTo(installedVersion);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
