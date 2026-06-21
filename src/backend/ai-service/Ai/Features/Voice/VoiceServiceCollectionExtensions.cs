using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Features.Voice.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

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
            provider.GetRequiredService<TtsAudioCache>()));
        services.AddScoped<IVoiceDialogService, VoiceDialogService>();
        services.AddScoped<IVoiceUsageService, VoiceUsageService>();
        return services;
    }
}
