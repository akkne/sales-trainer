using Sellevate.Ai.Features.Dialog.Overrides;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Features.Organizations;
using Sellevate.Ai.Infrastructure.Learning;

namespace Sellevate.Ai.Features.Dialog;

public static class DialogServiceCollectionExtensions
{
    public static IServiceCollection AddDialogFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenAiConfiguration>(configuration.GetSection(OpenAiConfiguration.SectionName));
        services.AddScoped<IOpenAiChatService, OpenAiChatService>();
        // Phase 40.11: scoped, because it closes over the per-request ITenantContext. It is the
        // only door to the dialog_sessions collection — see IDialogSessionRepository.
        services.AddScoped<IDialogSessionRepository, DialogSessionRepository>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<IScenarioValidationService, ScenarioValidationService>();
        services.AddScoped<IDialogModeOverrideService, DialogModeOverrideService>();

        // Phase 40.19. Scoped, not singleton: the memo inside it is per request and the profile it
        // reads is whichever tenant's the request belongs to.
        services.AddScoped<IOrganizationProfileProvider, OrganizationProfileProvider>();

        // Phase 40.23. Asks learning-service whether the conversation about to start is somebody's
        // assignment. Registered here rather than in a feature of its own because the one caller is
        // DialogService, and it is the only thing that makes an assignment's persona reach a prompt.
        services.AddLearningAssignmentClient(configuration);

        return services;
    }
}
