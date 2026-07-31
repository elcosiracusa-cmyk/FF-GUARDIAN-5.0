using System.Collections.Generic;
using System.Threading.Tasks;

namespace FFGuardian;

internal interface IDefenderService
{
    Task<SecurityState> GetStateAsync();
    Task QuickScanAsync();
    Task FullScanAsync();
    Task CustomScanAsync(string path);
    Task UpdateAsync();
    void OpenWindowsSecurity();
    Task<List<ThreatRow>> GetThreatsAsync();
    Task<List<EventRow>> GetOperationalEventsAsync();
}
