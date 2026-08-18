using Sellevate.Learning.Features.Exercises.Configuration;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Features.Exercises;

public static class ExerciseDialogServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    /// <summary>
    /// Phase 40.33. The interactive <c>ai_dialogue</c> exercise now reaches the provider the same way
    /// grading and the content pipeline already do — through ai-service.
    ///
    /// <para>
    /// What disappeared from here: the named <c>OpenAI</c> and <c>YandexTts</c> HttpClients, their
    /// Polly stacks, the <c>OpenAI</c>/<c>YandexTts</c>/<c>Voice</c> configuration sections, and the
    /// two provider clients behind them. The resilience did not disappear — it lives on the other
    /// side of the hop, in the one place that holds the keys — and learning-service no longer needs
    /// <c>OPENAI_API_KEY</c> or <c>YANDEX_TTS_API_KEY</c> at all, which is a smaller secret surface
    /// as well as a smaller code one.
    /// </para>
    ///
    /// <para>
    /// Both clients carry the same service-to-service secret header as the evaluation and
    /// content-pipeline clients: ai-service's <c>InternalServiceAuthFilter</c> rejects a request without
    /// it once the secret is configured, and leaves the route open in development when it is not — so an
    /// unset secret is a working local setup rather than a broken one.
    /// </para>
    /// </summary>
    public static IServiceCollection AddExerciseDialogServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var aiServiceSection = configuration.GetSection(AiServiceConfiguration.SectionName);
        var chatTimeout = TimeSpan.FromSeconds(Math.Clamp(
            aiServiceSection.GetValue("ChatTimeoutSeconds", 90), 10, 300));

        services.AddHttpClient<IOpenAiChatService, AiChatClient>(ConfigureAiServiceClient);
        services.AddHttpClient<ITtsRouter, AiTtsClient>(ConfigureAiServiceClient);

        services.Configure<ExerciseDialogOptions>(
            configuration.GetSection(ExerciseDialogOptions.SectionName));

        services.AddScoped<IExerciseDialogService, ExerciseDialogService>();

        return services;

        void ConfigureAiServiceClient(HttpClient httpClient)
        {
            httpClient.Timeout = chatTimeout;

            var internalServiceSecret = configuration["InternalAuth:ServiceSecret"];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
        }
    }
}
