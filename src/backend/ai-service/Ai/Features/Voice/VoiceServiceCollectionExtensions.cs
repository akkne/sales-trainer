using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Features.Voice.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

/// <summary>
/// Registers the voice roleplay feature.
///
/// <para>
/// <see cref="TtsAudioCache"/> is the one singleton here: the saving comes from phrases repeating
/// across sessions, which a per-request cache cannot see. Everything else is scoped because it closes
/// over the request's tenant. <see cref="ITtsRouter"/> is deliberately built by hand so the cache
/// decorates the real router rather than replacing it — resolving <see cref="ITtsRouter"/> from inside
/// the decorator would resolve the decorator.
/// </para>
/// </summary>
public static class VoiceServiceCollectionExtensions
{
    public static IServiceCollection AddVoiceFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VoiceFeatureConfiguration>(configuration.GetSection(VoiceFeatureConfiguration.SectionName));
        services.Configure<TtsRouterConfiguration>(configuration.GetSection(TtsRouterConfiguration.SectionName));
        services.Configure<YandexTtsConfiguration>(configuration.GetSection(YandexTtsConfiguration.SectionName));
        services.Configure<VoiceUsageLimitsConfiguration>(configuration.GetSection(VoiceUsageLimitsConfiguration.SectionName));
        services.AddScoped<IYandexTtsService, YandexTtsService>();
        services.AddSingleton<TtsAudioCache>();
        services.AddScoped<TtsRouter>();
        services.AddScoped<ITtsRouter>(provider => new CachingTtsRouter(
            provider.GetRequiredService<TtsRouter>(),
            provider.GetRequiredService<TtsAudioCache>(),
            provider.GetRequiredService<IOptions<TtsRouterConfiguration>>().Value.MaximumCacheableTextLength));
        services.AddScoped<IVoiceDialogService, VoiceDialogService>();
        services.AddScoped<IVoiceUsageService, VoiceUsageService>();
        return services;
    }
}
