using Sellevate.Analytics.Features.Presence.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Analytics.Features.Presence.Services.Implementation;

/// <summary>
/// Phase 40.13. Presence lives in Redis sorted sets, one per organization, following the
/// <c>org:{orgId}:</c> convention 40.11 established for ai-service.
///
/// <para>
/// Before this change every user in the product shared the single key <c>presence:online</c>. No
/// endpoint exposed the count to customers, so nothing leaked yet — but the roadmap names this one
/// explicitly for a reason: the moment anyone builds "how many of my team are online", a shared key
/// answers with the whole platform's headcount, which tells customer A how many people customer B
/// employs and how active they are. Prefixing the key now means that feature cannot be built
/// wrong.
/// </para>
///
/// <para>
/// The registry set exists because analytics-service has no database: it is the only way the gauge
/// updater can find the organizations to sum over. It holds organization ids and nothing else — no
/// user ids, no counts — and is read only by the in-process background job.
/// </para>
/// </summary>
internal sealed class PresenceTracker : IPresenceTracker
{
    /// <summary>
    /// One organization's online set. Old un-prefixed <c>presence:online</c> keys are never read
    /// again after this change and are dropped by an operational step rather than left to expire:
    /// a sorted set that nothing writes to has no TTL and would sit in Redis forever. See
    /// docs/DONT_FORGET.md.
    /// </summary>
    internal static string OnlineKey(Guid organizationId) => $"org:{organizationId:N}:presence:online";

    /// <summary>
    /// The set of organizations that have recorded presence at least once. Deliberately NOT
    /// prefixed — it is the index across organizations, so a per-organization prefix on it would be
    /// a contradiction. It is also the reason nothing else needs one: with this list, every read of
    /// actual presence data goes through <see cref="OnlineKey"/>.
    /// </summary>
    internal const string OrganizationRegistryKey = "presence:organizations";

    private static readonly TimeSpan PresenceWindow = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer _redisConnection;

    public PresenceTracker(IConnectionMultiplexer redisConnection)
    {
        ArgumentNullException.ThrowIfNull(redisConnection);
        _redisConnection = redisConnection;
    }

    public async Task MarkSeenAsync(Guid organizationId, string userId, CancellationToken cancellationToken = default)
    {
        RequireOrganization(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var lastSeenUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var database = _redisConnection.GetDatabase();

        await database.SortedSetAddAsync(OnlineKey(organizationId), userId, lastSeenUnixSeconds);
        await database.SetAddAsync(OrganizationRegistryKey, organizationId.ToString("N"));
    }

    public async Task<long> CountOnlineAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        RequireOrganization(organizationId);

        var windowStartUnixSeconds = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        return await _redisConnection.GetDatabase()
            .SortedSetLengthAsync(OnlineKey(organizationId), windowStartUnixSeconds, double.PositiveInfinity);
    }

    public async Task<long> CountOnlineAcrossAllOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var windowStartUnixSeconds = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        var database = _redisConnection.GetDatabase();

        var total = 0L;
        foreach (var organizationId in await ReadRegisteredOrganizationsAsync(database))
        {
            total += await database.SortedSetLengthAsync(
                OnlineKey(organizationId), windowStartUnixSeconds, double.PositiveInfinity);
        }

        return total;
    }

    public async Task PruneAsync(CancellationToken cancellationToken = default)
    {
        var windowStartUnixSeconds = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        var database = _redisConnection.GetDatabase();

        foreach (var organizationId in await ReadRegisteredOrganizationsAsync(database))
        {
            await database.SortedSetRemoveRangeByScoreAsync(
                OnlineKey(organizationId), double.NegativeInfinity, windowStartUnixSeconds, Exclude.Stop);

            // An organization whose whole team went offline leaves an empty sorted set, which Redis
            // deletes on its own; drop the registry entry with it so a customer that churned does
            // not cost a round trip on every tick forever.
            if (await database.SortedSetLengthAsync(OnlineKey(organizationId)) == 0)
            {
                await database.SetRemoveAsync(OrganizationRegistryKey, organizationId.ToString("N"));
            }
        }
    }

    private static async Task<List<Guid>> ReadRegisteredOrganizationsAsync(IDatabase database)
    {
        var members = await database.SetMembersAsync(OrganizationRegistryKey);

        var organizationIds = new List<Guid>(members.Length);
        foreach (var member in members)
        {
            // A malformed member is skipped rather than thrown on: it can only come from a manual
            // Redis edit, and one bad entry must not stop the gauge for every other organization.
            if (Guid.TryParse(member.ToString(), out var organizationId))
            {
                organizationIds.Add(organizationId);
            }
        }

        return organizationIds;
    }

    /// <summary>
    /// Fails closed and loudly, matching <c>TenantSaveChangesInterceptor</c>'s wording so operators
    /// grep once. An empty organization here would build the key <c>org:00000000...:presence:online</c>
    /// — a shared bucket that silently pools every tenant whose context was not set, which is the
    /// exact failure the prefix exists to prevent.
    /// </summary>
    private static void RequireOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("Organization context is not set.");
        }
    }
}
