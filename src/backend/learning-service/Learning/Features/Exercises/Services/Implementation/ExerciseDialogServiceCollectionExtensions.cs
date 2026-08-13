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

        // Mirror ai-service: retry on 5xx/429/timeout plus a circuit breaker, so a stalled or
        // flapping provider degrades into a fast 503 instead of pinning request threads for
        // the default 100s and taking the whole exercise flow down with it.
        foreach (var upstreamClientName in new[] { "OpenAI", "YandexTts" })
        {
            services.AddHttpClient(upstreamClientName)
                .ConfigureHttpClient(client =>
                    client.Timeout = TimeSpan.FromSeconds(90)) // outer timeout > Polly total; Polly controls per-attempt
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                    PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(30))
                .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                    options.Retry.MaxRetryAttempts = 2;
                    options.Retry.Delay = TimeSpan.FromSeconds(1);
                    // Polly requires SamplingDuration >= 2 x AttemptTimeout, else startup validation fails.
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                    options.CircuitBreaker.MinimumThroughput = 5;
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                });
        }

        services.AddScoped<IOpenAiChatService, OpenAiChatService>();
        services.AddScoped<IYandexTtsService, YandexTtsService>();
        services.AddScoped<ITtsRouter, TtsRouter>();

        services.AddScoped<IExerciseDialogService, ExerciseDialogService>();

        return services;
    }
}
