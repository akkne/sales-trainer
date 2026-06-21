namespace Sellevate.Notification.Features.Users;

/// <summary>
/// Read/write access to the local user replica used to resolve a recipient's email address and
/// display name when sending an email notification.
/// </summary>
public interface IUserDirectory
{
    Task<UserProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Updates only the display name, leaving the stored email untouched. No-op if the
    /// user is not yet replicated.</summary>
    Task UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default);
}
