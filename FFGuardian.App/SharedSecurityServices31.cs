using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian;

internal static class SharedSecurityServices31
{
    private static readonly Lazy<ServiceProvider> Provider = new(CreateProvider, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IYaraService Yara => Provider.Value.GetRequiredService<IYaraService>();
    public static IClamAvService ClamAv => Provider.Value.GetRequiredService<IClamAvService>();
    public static IScanService Scan => Provider.Value.GetRequiredService<IScanService>();
    public static IQuarantineService Quarantine => Provider.Value.GetRequiredService<IQuarantineService>();
    public static IAntivirusHealthService Health => Provider.Value.GetRequiredService<IAntivirusHealthService>();

    private static ServiceProvider CreateProvider()
    {
        ServiceCollection services = new();
        services.AddFFGuardianSecurityServices(options => options.BaseDirectory = AppContext.BaseDirectory);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
