using Sellevate.Notification.Common.Constants;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Users;

/// <summary>
/// Redis-backed <see cref="IUserDirectory"/>. Each user is a small hash at
/// <see cref="RedisKeys.UserProfile"/> holding their email and display name. The data is a
/// projection of Identity's user events, so it carries no TTL — it lives until the user is deleted,
/// and Phase 40.13 deliberately left the key un-prefixed by organization (see
/// <see cref="RedisKeys.UserProfile"/> for why).
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

    private static RedisKey KeyFor(Guid userId) => RedisKeys.UserProfile(userId);

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

    /// <summary>
    /// Writes only to an already-replicated user. A <c>user.updated</c> that arrives after the
    /// matching <c>user.deleted</c> — or before the registration that creates the hash — must not
    /// resurrect the key as an emailless profile that <see cref="GetAsync"/> would then reject
    /// anyway.
    /// </summary>
    public async Task UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        var database = _connection.GetDatabase();
        var key = KeyFor(userId);

        if (await database.KeyExistsAsync(key))
        {
            await database.HashSetAsync(key, DisplayNameField, displayName ?? string.Empty);
        }
    }

    public Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _connection.GetDatabase().KeyDeleteAsync(KeyFor(userId));
}
