namespace SalesTrainer.Api.Features.Transcription;

public static class TranscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddTranscriptionFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ITranscriptionService, WhisperTranscriptionService>();
        return services;
    }
}
