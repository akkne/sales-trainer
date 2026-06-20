using Sellevate.Notification.Eventing;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Features.Notifications.Services.Implementation;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NotificationStorageConfiguration>(
            configuration.GetSection(NotificationStorageConfiguration.SectionName));

        services.AddSingleton<INotificationStore, RedisNotificationStore>();
        services.AddScoped<INotificationService, Services.Implementation.NotificationService>();
        services.AddSingleton<INotificationEventMapper, NotificationEventMapper>();
        services.AddHostedService<NotificationEventConsumer>();
        return services;
    }
}
