using SalesTrainer.Api.Features.Notifications.Services.Abstract;
using SalesTrainer.Api.Features.Notifications.Services.Implementation;

namespace SalesTrainer.Api.Features.Notifications;

public static class NotificationFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationCleanupJob>();
        return services;
    }
}
