using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace FFGuardian.PremiumWpf;

public sealed record NetworkStatus(
    bool NetworkAvailable,
    bool DomainFirewallEnabled,
    bool PrivateFirewallEnabled,
    bool PublicFirewallEnabled,
    string PingTarget,
    bool PingSucceeded,
    long? PingMilliseconds,
    string Message,
    DateTimeOffset CheckedAtUtc);

public sealed class NetworkStatusService
{
    private readonly IPAddress _pingAddress = IPAddress.Parse("1.1.1.1");

    public async Task<NetworkStatus> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool networkAvailable = NetworkInterface.GetIsNetworkAvailable();
        bool domain = ReadFirewallProfile(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile");
        bool privateProfile = ReadFirewallProfile(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
        bool publicProfile = ReadFirewallProfile(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");

        string target = _pingAddress.ToString();
        bool pingSucceeded = false;
        long? latency = null;
        string pingText = networkAvailable ? "Ping non completato" : "Rete non disponibile";

        if (networkAvailable)
        {
            using Ping ping = new();
            try
            {
                PingReply reply = await ping.SendPingAsync(_pingAddress, 2000).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                pingSucceeded = reply.Status == IPStatus.Success;
                if (pingSucceeded) latency = reply.RoundtripTime;
                pingText = pingSucceeded ? $"Ping {target}: {reply.RoundtripTime} ms" : $"Ping {target}: {reply.Status}";
            }
            catch (PingException exception)
            {
                pingText = $"Ping {target}: {exception.Message}";
            }
        }

        string profiles = domain && privateProfile && publicProfile
            ? "Firewall Windows attivo su tutti i profili."
            : "Uno o più profili Firewall Windows risultano disattivati.";

        return new NetworkStatus(networkAvailable, domain, privateProfile, publicProfile, target, pingSucceeded, latency,
            $"{profiles} {pingText}", DateTimeOffset.UtcNow);
    }

    private static bool ReadFirewallProfile(string path)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            return key?.GetValue("EnableFirewall") is int value && value != 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
