namespace Sellevate.Notification.Features.Users;

/// <summary>
/// A minimal local replica of a user, just enough to address an email to them. Populated from
/// Identity's user.* events and stored in Redis (the notification service has no database).
/// </summary>
public sealed record UserProfile(Guid UserId, string Email, string DisplayName);
