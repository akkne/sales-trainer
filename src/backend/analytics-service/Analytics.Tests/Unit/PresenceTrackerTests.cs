using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Analytics.Features.Presence.Services.Implementation;
using StackExchange.Redis;

namespace Sellevate.Analytics.Tests.Unit;

[TestFixture]
public class PresenceTrackerTests
{
    private const int PresenceWindowSeconds = 5 * 60;

    private static readonly Guid OrganizationA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string OnlineKeyFor(Guid organizationId) => $"org:{organizationId:N}:presence:online";

    private IDatabase _database = null!;
    private IConnectionMultiplexer _redisConnection = null!;
    private PresenceTracker _presenceTracker = null!;

    [SetUp]
    public void SetUp()
    {
        _database = Substitute.For<IDatabase>();
        _redisConnection = Substitute.For<IConnectionMultiplexer>();
        _redisConnection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _presenceTracker = new PresenceTracker(_redisConnection);
    }

    [Test]
    public async Task MarkSeenAsync_AddsUserToTheOrganizationsOwnOnlineSet()
    {
        var beforeUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _presenceTracker.MarkSeenAsync(OrganizationA, "user-123");

        var afterUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _database.Received(1).SortedSetAddAsync(
            OnlineKeyFor(OrganizationA),
            "user-123",
            Arg.Is<double>(score => score >= beforeUnixSeconds && score <= afterUnixSeconds),
            Arg.Any<SortedSetWhen>(),
            Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// The point of the whole change: two organizations must not touch the same key. Asserted on
    /// the key rather than on a count, because the count only diverges once real data exists and
    /// the leak is in the key shape.
    /// </summary>
    [Test]
    public async Task MarkSeenAsync_UsesADifferentKeyForEachOrganization()
    {
        await _presenceTracker.MarkSeenAsync(OrganizationA, "user-a");
        await _presenceTracker.MarkSeenAsync(OrganizationB, "user-b");

        await _database.Received(1).SortedSetAddAsync(
            OnlineKeyFor(OrganizationA), "user-a",
            Arg.Any<double>(), Arg.Any<SortedSetWhen>(), Arg.Any<CommandFlags>());
        await _database.Received(1).SortedSetAddAsync(
            OnlineKeyFor(OrganizationB), "user-b",
            Arg.Any<double>(), Arg.Any<SortedSetWhen>(), Arg.Any<CommandFlags>());

        OnlineKeyFor(OrganizationA).Should().NotBe(OnlineKeyFor(OrganizationB));
    }

    [Test]
    public async Task MarkSeenAsync_RegistersTheOrganizationSoTheGaugeCanFindIt()
    {
        await _presenceTracker.MarkSeenAsync(OrganizationA, "user-123");

        await _database.Received(1).SetAddAsync(
            PresenceTracker.OrganizationRegistryKey,
            OrganizationA.ToString("N"),
            Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// An unset tenant must raise, never quietly write to <c>org:00000000...</c> — a shared bucket
    /// pooling every caller whose context was missing is the exact failure the prefix prevents.
    /// </summary>
    [Test]
    public async Task MarkSeenAsync_WithNoOrganization_Raises()
    {
        var act = () => _presenceTracker.MarkSeenAsync(Guid.Empty, "user-123");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Organization context is not set.");
    }

    [Test]
    public async Task CountOnlineAsync_WithNoOrganization_Raises()
    {
        var act = () => _presenceTracker.CountOnlineAsync(Guid.Empty);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Organization context is not set.");
    }

    [Test]
    public async Task CountOnlineAsync_CountsOnlyMembersInsideThePresenceWindow()
    {
        _database.SortedSetLengthAsync(
                OnlineKeyFor(OrganizationA),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<CommandFlags>())
            .Returns(7);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var onlineCount = await _presenceTracker.CountOnlineAsync(OrganizationA);

        onlineCount.Should().Be(7);
        await _database.Received(1).SortedSetLengthAsync(
            OnlineKeyFor(OrganizationA),
            Arg.Is<double>(windowStart =>
                windowStart >= nowUnixSeconds - PresenceWindowSeconds - 2
                && windowStart <= nowUnixSeconds - PresenceWindowSeconds + 2),
            double.PositiveInfinity,
            Arg.Any<Exclude>(),
            Arg.Any<CommandFlags>());
    }

    /// <summary>
    /// One organization's count never includes another's members, because it never reads another's
    /// key. The substitute returns a value only for A's key; B's returns the default 0.
    /// </summary>
    [Test]
    public async Task CountOnlineAsync_DoesNotSeeAnotherOrganizationsUsers()
    {
        _database.SortedSetLengthAsync(
                OnlineKeyFor(OrganizationA),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
            .Returns(4);

        var countForB = await _presenceTracker.CountOnlineAsync(OrganizationB);

        countForB.Should().Be(0, "organization B has recorded no presence, and A's four users are not its business");
    }

    [Test]
    public async Task CountOnlineAcrossAllOrganizationsAsync_SumsEveryRegisteredOrganization()
    {
        _database.SetMembersAsync(PresenceTracker.OrganizationRegistryKey, Arg.Any<CommandFlags>())
            .Returns([OrganizationA.ToString("N"), OrganizationB.ToString("N")]);
        _database.SortedSetLengthAsync(
                OnlineKeyFor(OrganizationA),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
            .Returns(4);
        _database.SortedSetLengthAsync(
                OnlineKeyFor(OrganizationB),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
            .Returns(3);

        var total = await _presenceTracker.CountOnlineAcrossAllOrganizationsAsync();

        total.Should().Be(7);
    }

    [Test]
    public async Task PruneAsync_RemovesStaleMembersFromEachRegisteredOrganization()
    {
        _database.SetMembersAsync(PresenceTracker.OrganizationRegistryKey, Arg.Any<CommandFlags>())
            .Returns([OrganizationA.ToString("N")]);
        // The emptiness check reads the whole set (min = -infinity); returning a non-zero length
        // keeps the organization in the registry so this test asserts pruning alone.
        _database.SortedSetLengthAsync(
                OnlineKeyFor(OrganizationA), Arg.Is<double>(minimum => double.IsNegativeInfinity(minimum)), Arg.Any<double>(),
                Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
            .Returns(2);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _presenceTracker.PruneAsync();

        await _database.Received(1).SortedSetRemoveRangeByScoreAsync(
            OnlineKeyFor(OrganizationA),
            double.NegativeInfinity,
            Arg.Is<double>(windowStart =>
                windowStart >= nowUnixSeconds - PresenceWindowSeconds - 2
                && windowStart <= nowUnixSeconds - PresenceWindowSeconds + 2),
            Exclude.Stop,
            Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task PruneAsync_ForgetsAnOrganizationOnceItsSetIsEmpty()
    {
        _database.SetMembersAsync(PresenceTracker.OrganizationRegistryKey, Arg.Any<CommandFlags>())
            .Returns([OrganizationA.ToString("N")]);

        // No SortedSetLengthAsync setup: the substitute returns 0, which is exactly the "everybody
        // went offline" case this test is about.
        await _presenceTracker.PruneAsync();

        await _database.Received(1).SetRemoveAsync(
            PresenceTracker.OrganizationRegistryKey,
            OrganizationA.ToString("N"),
            Arg.Any<CommandFlags>());
    }
}
