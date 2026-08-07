using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FFGuardian.AI.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddFFGuardianAiSecurity(this IServiceCollection services, Action<AiThreatAnalyzerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null) services.Configure(configure); else services.AddOptions<AiThreatAnalyzerOptions>();

        // Hosts normally register logging themselves. Smoke-test and headless hosts may not;
        // in that case provide a no-op logger so DI validation remains deterministic without
        // suppressing exceptions or changing analyzer behavior.
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(NullLogger<>)));

        services.AddSingleton<IFeatureExtractor, SafeFeatureExtractor>();
        services.AddSingleton<IThreatScoreCalculator, ThreatScoreCalculator>();
        services.AddSingleton<IBehaviorCorrelationService, BehaviorCorrelationService>();
        services.AddSingleton<IAiModelProvider, VerifiedLocalModelProvider>();
        services.AddSingleton<IAiExplanationService, AiExplanationService>();
        services.AddSingleton<ILocalHashAllowlist, LocalHashAllowlist>();
        services.AddSingleton<IAiThreatAnalyzer, AiThreatAnalyzer>();
        return services;
    }
}
