using Sellevate.Ai.Features.Dialog.Overrides;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Features.Organizations;
using Sellevate.Ai.Infrastructure.Learning;

namespace Sellevate.Ai.Features.Dialog;

/// <summary>
/// Registers the dialog roleplay feature.
///
/// <para>
/// Everything here is scoped, and for one reason in three guises: the session repository closes over the
/// per-request <c>ITenantContext</c> (Phase 40.11) and is the only door to the <c>dialog_sessions</c>
/// collection; the organization profile provider memoizes per request and reads whichever tenant the
/// request belongs to (40.19); the assignment client asks learning-service about the caller. A singleton
/// among them would capture one request's tenant and serve it to every later request.
/// </para>
///
/// <para>
/// The assignment client is registered here rather than as a feature of its own because
/// <c>DialogService</c> is its only caller, and it is the only thing that makes an assignment's persona
/// reach a prompt (40.23).
/// </para>
/// </summary>
public static class DialogServiceCollectionExtensions
{
    public static IServiceCollection AddDialogFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenAiConfiguration>(configuration.GetSection(OpenAiConfiguration.SectionName));
        services.AddScoped<IOpenAiChatService, OpenAiChatService>();
        services.AddScoped<IDialogSessionRepository, DialogSessionRepository>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<IScenarioValidationService, ScenarioValidationService>();
        services.AddScoped<IDialogModeOverrideService, DialogModeOverrideService>();

        services.AddScoped<IOrganizationProfileProvider, OrganizationProfileProvider>();
        services.AddLearningAssignmentClient(configuration);

        return services;
    }
}
