using Sellevate.Ai.Features.Dialog.Overrides;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

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
        return services;
    }
}
