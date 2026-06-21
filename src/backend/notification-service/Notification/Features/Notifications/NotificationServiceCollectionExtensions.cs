using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.Notification.Eventing;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Emails.Delayed;
using Sellevate.Notification.Features.Notifications.Emails.Templates;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Features.Notifications.Services.Implementation;
using Sellevate.Notification.Features.Users;
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
        services.Configure<NotificationEmailConfiguration>(
            configuration.GetSection(NotificationEmailConfiguration.SectionName));

        services.AddSingleton<INotificationStore, RedisNotificationStore>();
        services.AddScoped<INotificationService, Services.Implementation.NotificationService>();
        services.AddSingleton<INotificationEventMapper, NotificationEventMapper>();

        AddEmailNotifications(services, configuration);

        services.AddHostedService<NotificationEventConsumer>();
        services.AddHostedService<UserReplicaConsumer>();
        services.AddHostedService<DelayedChatEmailDispatcherService>();
        return services;
    }

    private static void AddEmailNotifications(IServiceCollection services, IConfiguration configuration)
    {
        // Shared transactional-email transport (MailerSend) from BuildingBlocks.
        services.AddSellevateEmail(configuration);

        // Local user replica (email/display-name lookup) — the service has no database.
        services.AddSingleton<IUserDirectory, RedisUserDirectory>();

        // OOP template set: one template per type plus a generic fallback, dispatched by the renderer.
        services.AddSingleton<INotificationEmailTemplate, FriendRequestEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, ChatMessageEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, DiscussReplyEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, LeagueUpdatedEmailTemplate>();
        services.AddSingleton<GenericNotificationEmailTemplate>();

        services.AddSingleton<INotificationEmailRenderer>(serviceProvider => new NotificationEmailRenderer(
            serviceProvider.GetServices<INotificationEmailTemplate>(),
            serviceProvider.GetRequiredService<GenericNotificationEmailTemplate>(),
            ResolveFrontendBaseUrl(configuration)));

        services.AddScoped<INotificationEmailDispatcher, NotificationEmailDispatcher>();
        services.AddSingleton<IDelayedChatEmailScheduler, RedisDelayedChatEmailScheduler>();
    }

    private static string ResolveFrontendBaseUrl(IConfiguration configuration)
    {
        // Frontend:Url may carry a comma-separated CORS origin list; the first entry is the canonical UI.
        var configured = configuration["Frontend:Url"] ?? "http://localhost:3000";
        return configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "http://localhost:3000";
    }
}
