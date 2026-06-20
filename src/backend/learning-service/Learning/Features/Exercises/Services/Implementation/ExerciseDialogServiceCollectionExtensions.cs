using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.Exercises;

public static class ExerciseDialogServiceCollectionExtensions
{
    public static IServiceCollection AddExerciseDialogServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenAiConfiguration>(configuration.GetSection(OpenAiConfiguration.SectionName));
        services.Configure<TtsRouterConfiguration>(configuration.GetSection(TtsRouterConfiguration.SectionName));
        services.Configure<YandexTtsConfiguration>(configuration.GetSection(YandexTtsConfiguration.SectionName));
        services.Configure<GoogleTtsConfiguration>(configuration.GetSection(GoogleTtsConfiguration.SectionName));

        services.AddHttpClient("OpenAI");
        services.AddHttpClient("YandexTts");
        services.AddHttpClient("GoogleTts");

        services.AddScoped<IOpenAiChatService, OpenAiChatService>();
        services.AddScoped<IYandexTtsService, YandexTtsService>();
        services.AddScoped<IGoogleTtsService, GoogleTtsService>();
        services.AddScoped<ITtsRouter, TtsRouter>();

        services.AddScoped<IExerciseDialogService, ExerciseDialogService>();

        return services;
    }
}
