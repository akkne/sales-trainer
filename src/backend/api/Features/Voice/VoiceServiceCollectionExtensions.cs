using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Implementation;

namespace SalesTrainer.Api.Features.Voice;

public static class VoiceServiceCollectionExtensions
{
    public static IServiceCollection AddVoiceFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IVoicerTtsService, VoicerTtsService>();
        services.AddScoped<IGoogleTtsService, GoogleTtsService>();
        services.AddScoped<IVoiceDialogService, VoiceDialogService>();
        return services;
    }
}
