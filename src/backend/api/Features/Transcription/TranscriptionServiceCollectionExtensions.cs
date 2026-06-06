using SalesTrainer.Api.Features.Transcription.Services.Abstract;
using SalesTrainer.Api.Features.Transcription.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Transcription;

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
