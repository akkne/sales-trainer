using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Dialog.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Dialog;

public static class DialogServiceCollectionExtensions
{
    public static IServiceCollection AddDialogFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IOpenAiChatService, OpenAiChatService>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<DialogSeeder>();
        return services;
    }
}
