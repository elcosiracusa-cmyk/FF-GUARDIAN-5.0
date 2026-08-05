using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FFGuardian.Security.Core;

public static class UnifiedScanRegistration
{
    public static IServiceCollection AddUnifiedFFGuardianScanService(this IServiceCollection services)
    {
        services.RemoveAll<IScanService>();
        services.TryAddSingleton<ScanService>();
        services.AddSingleton<IScanService, UnifiedScanService>();
        return services;
    }
}
