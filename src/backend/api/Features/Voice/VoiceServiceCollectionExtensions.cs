using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Voice;

public static class VoiceServiceCollectionExtensions
{
    public static IServiceCollection AddVoiceFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<YandexTtsConfiguration>(configuration.GetSection(YandexTtsConfiguration.SectionName));
        services.Configure<VoicerTtsConfiguration>(configuration.GetSection(VoicerTtsConfiguration.SectionName));
        services.Configure<GoogleTtsConfiguration>(configuration.GetSection(GoogleTtsConfiguration.SectionName));
        services.Configure<VoiceUsageLimitsConfiguration>(configuration.GetSection(VoiceUsageLimitsConfiguration.SectionName));
        services.AddScoped<IYandexTtsService, YandexTtsService>();
        services.AddScoped<IVoicerTtsService, VoicerTtsService>();
        services.AddScoped<IGoogleTtsService, GoogleTtsService>();
        services.AddScoped<ITtsRouter, TtsRouter>();
        services.AddScoped<IVoiceDialogService, VoiceDialogService>();
        services.AddScoped<IVoiceUsageService, VoiceUsageService>();
        return services;
    }
}
