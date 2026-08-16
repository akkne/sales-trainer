using StackExchange.Redis;

namespace Sellevate.Notification.Features.Users;

/// <summary>
/// Redis-backed <see cref="IUserDirectory"/>. Each user is a small hash at
/// <c>notifications:user:{userId}</c> holding their email and display name. The data is a
/// projection of Identity's user events, so it carries no TTL — it lives until the user is deleted.
///
/// <para>
/// Phase 40.13 reviewed this key and deliberately left it un-prefixed. It is the same call
/// learning-service and ai-service made about their <c>UserReplicas</c> tables: an identity in this
/// product is cross-organization (docs/TENANCY/TENANCY.md §4.2), so an organization is not a
/// property of the row and putting one in the key would mean either duplicating the projection per
/// organization or picking one arbitrarily. What lives here — an email address and a display name
/// — is what identity-service broadcasts platform-wide, and it is read only to address an email
/// that some other, org-scoped decision already decided to send.
/// </para>
/// </summary>
public sealed class RedisUserDirectory : IUserDirectory
{
    private const string EmailField = "email";
    private const string DisplayNameField = "displayName";

    private readonly IConnectionMultiplexer _connection;

    public RedisUserDirectory(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    private static RedisKey KeyFor(Guid userId) => $"notifications:user:{userId:N}";

    public async Task<UserProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _connection.GetDatabase().HashGetAllAsync(KeyFor(userId));
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(entry => (string)entry.Name!, entry => (string?)entry.Value);
        var email = map.GetValueOrDefault(EmailField);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new UserProfile(userId, email, map.GetValueOrDefault(DisplayNameField) ?? string.Empty);
    }

    public Task UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return _connection.GetDatabase().HashSetAsync(
            KeyFor(profile.UserId),
            [
                new HashEntry(EmailField, profile.Email),
                new HashEntry(DisplayNameField, profile.DisplayName ?? string.Empty),
            ]);
    }

    public async Task UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        var database = _connection.GetDatabase();
        var key = KeyFor(userId);

        // Only touch an already-replicated user; never resurrect a deleted/absent key.
        if (await database.KeyExistsAsync(key))
        {
            await database.HashSetAsync(key, DisplayNameField, displayName ?? string.Empty);
        }
    }

    public Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _connection.GetDatabase().KeyDeleteAsync(KeyFor(userId));
}
