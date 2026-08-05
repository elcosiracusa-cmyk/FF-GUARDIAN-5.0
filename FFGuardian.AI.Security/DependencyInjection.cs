using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian.AI.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddFFGuardianAiSecurity(this IServiceCollection services, Action<AiThreatAnalyzerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null) services.Configure(configure); else services.AddOptions<AiThreatAnalyzerOptions>();
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
