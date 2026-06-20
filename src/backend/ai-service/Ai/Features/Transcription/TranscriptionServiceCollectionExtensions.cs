using Sellevate.Ai.Features.Transcription.Services.Abstract;
using Sellevate.Ai.Features.Transcription.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Transcription;

public static class TranscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddTranscriptionFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WhisperConfiguration>(configuration.GetSection(WhisperConfiguration.SectionName));
        services.AddScoped<ITranscriptionService, WhisperTranscriptionService>();
        return services;
    }
}
