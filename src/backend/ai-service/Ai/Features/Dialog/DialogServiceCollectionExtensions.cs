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
        services.AddScoped<IDialogService, DialogService>();
        return services;
    }
}
