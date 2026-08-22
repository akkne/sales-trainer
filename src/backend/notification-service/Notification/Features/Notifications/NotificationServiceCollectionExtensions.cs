using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Eventing;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Emails.Delayed;
using Sellevate.Notification.Features.Notifications.Emails.Templates;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Features.Notifications.Services.Implementation;
using Sellevate.Notification.Features.Users;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications;

/// <summary>
/// The service's single composition root. Lifetimes are not incidental: the store, the mapper, the
/// user directory and the delayed-email scheduler are stateless singletons, while
/// <c>NotificationService</c> and the email dispatcher are scoped because they resolve the ambient
/// <c>ITenantContext</c> — which is why the store takes the organization as a parameter instead.
/// </summary>
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
        // Q-4: stateless and organization-free (a preference belongs to the identity, not to a seat
        // in one organization), so a singleton like the store above rather than scoped like the
        // services that resolve the ambient ITenantContext.
        services.AddSingleton<INotificationPreferencesStore, RedisNotificationPreferencesStore>();
        services.AddScoped<INotificationService, Services.Implementation.NotificationService>();
        services.AddSingleton<INotificationEventMapper, NotificationEventMapper>();

        AddEmailNotifications(services, configuration);

        services.AddHostedService<NotificationEventConsumer>();
        services.AddHostedService<UserReplicaConsumer>();
        services.AddHostedService<DelayedChatEmailDispatcherService>();
        return services;
    }

    /// <summary>
    /// Registers the email side channel: the shared MailerSend transport from BuildingBlocks, the
    /// Redis user replica that supplies the recipient address, and one template per emailed type.
    /// The templates are registered as a collection so the renderer can index them by type; the
    /// generic fallback is registered on its own concrete type so it stays out of that index.
    /// </summary>
    private static void AddEmailNotifications(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSellevateEmail(configuration);

        services.AddSingleton<IUserDirectory, RedisUserDirectory>();

        services.AddSingleton<INotificationEmailTemplate, FriendRequestEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, FriendRequestAcceptedEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, ChatMessageEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, DiscussReplyEmailTemplate>();
        services.AddSingleton<INotificationEmailTemplate, WelcomeEmailTemplate>();
        services.AddSingleton<GenericNotificationEmailTemplate>();

        services.AddSingleton<INotificationEmailRenderer>(serviceProvider => new NotificationEmailRenderer(
            serviceProvider.GetServices<INotificationEmailTemplate>(),
            serviceProvider.GetRequiredService<GenericNotificationEmailTemplate>(),
            ResolveFrontendBaseUrl(configuration)));

        services.AddScoped<INotificationEmailDispatcher, NotificationEmailDispatcher>();
        services.AddSingleton<IDelayedChatEmailScheduler, RedisDelayedChatEmailScheduler>();
    }

    /// <summary>
    /// The origin absolute links in emails are built against. <see cref="ConfigurationKeys.FrontendUrl"/>
    /// doubles as the CORS allow-list and may therefore carry a comma-separated list; the first entry
    /// is the canonical UI.
    /// </summary>
    private static string ResolveFrontendBaseUrl(IConfiguration configuration)
    {
        var configured = configuration[ConfigurationKeys.FrontendUrl] ?? ConfigurationKeys.DefaultFrontendUrl;
        return configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? ConfigurationKeys.DefaultFrontendUrl;
    }
}
