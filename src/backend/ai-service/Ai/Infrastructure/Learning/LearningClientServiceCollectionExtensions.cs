using Sellevate.Ai.Common.Constants;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// Registers the typed client ai-service reads an assignment's practice context with.
///
/// <para>
/// The call carries the same internal-service handshake ai-service demands of its own callers, pointed
/// the other way: learning-service's own filter rejects the call without the header once its secret is
/// configured, and the header is omitted rather than sent empty when ours is not, so a local run without
/// the secret still works on both ends.
/// </para>
///
/// <para>
/// The client timeout is clamped rather than trusted. It sits in front of a learner pressing "start" and
/// the lookup degrades to "no assignment" on timeout, so a misconfigured zero would turn every practice
/// start into an un-personalised one silently, and a misconfigured hour would hang the screen.
/// </para>
/// </summary>
public static class LearningClientServiceCollectionExtensions
{
    /// <summary>Shortest client timeout honoured, whatever configuration asks for.</summary>
    private const int MinimumTimeoutSeconds = 1;

    /// <summary>Longest client timeout honoured, whatever configuration asks for.</summary>
    private const int MaximumTimeoutSeconds = 30;

    /// <summary>Timeout used when the section names none. Mirrors the options class's own default.</summary>
    private const int DefaultTimeoutSeconds = 5;

    public static IServiceCollection AddLearningAssignmentClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LearningServiceConfiguration>(
            configuration.GetSection(LearningServiceConfiguration.SectionName));

        services.AddHttpClient<IAssignmentPracticeContextClient, AssignmentPracticeContextClient>(httpClient =>
        {
            var internalServiceSecret = configuration[InternalServiceAuthentication.SecretConfigurationKey];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
            {
                httpClient.DefaultRequestHeaders.Add(
                    InternalServiceAuthentication.HeaderName, internalServiceSecret);
            }

            var timeoutSeconds = configuration.GetValue<int?>(
                $"{LearningServiceConfiguration.SectionName}:{nameof(LearningServiceConfiguration.TimeoutSeconds)}")
                ?? DefaultTimeoutSeconds;
            httpClient.Timeout = TimeSpan.FromSeconds(
                Math.Clamp(timeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds));
        });

        return services;
    }

    /// <summary>
    /// C-3 audit fix. Registers the typed client dialog bundles read the skill catalog with, so their
    /// DTO can carry the slug and title the frontend has always declared (docs/AUDIT_CONTRACTS.md).
    /// Mirrors <see cref="AddLearningAssignmentClient"/> — same secret handshake, same clamped timeout.
    /// </summary>
    public static IServiceCollection AddLearningSkillLookupClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LearningServiceConfiguration>(
            configuration.GetSection(LearningServiceConfiguration.SectionName));

        // R-15 audit fix. Singleton so the cached catalog is shared across every request, the same
        // way TtsAudioCache is the one singleton cache in ai-service's voice feature.
        services.AddSingleton<SkillCatalogCache>();

        services.AddHttpClient<ISkillLookupClient, SkillLookupClient>(httpClient =>
        {
            var internalServiceSecret = configuration[InternalServiceAuthentication.SecretConfigurationKey];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
            {
                httpClient.DefaultRequestHeaders.Add(
                    InternalServiceAuthentication.HeaderName, internalServiceSecret);
            }

            var timeoutSeconds = configuration.GetValue<int?>(
                $"{LearningServiceConfiguration.SectionName}:{nameof(LearningServiceConfiguration.TimeoutSeconds)}")
                ?? DefaultTimeoutSeconds;
            httpClient.Timeout = TimeSpan.FromSeconds(
                Math.Clamp(timeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds));
        });

        return services;
    }
}
