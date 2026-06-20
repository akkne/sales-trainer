using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Analytics.Features.Presence.Services.Implementation;
using StackExchange.Redis;

namespace Sellevate.Analytics.Tests.Unit;

[TestFixture]
public class PresenceTrackerTests
{
    private const string OnlineKey = "presence:online";
    private const int PresenceWindowSeconds = 5 * 60;

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
    public async Task MarkSeenAsync_AddsUserToOnlineSetWithCurrentTimestampScore()
    {
        var beforeUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _presenceTracker.MarkSeenAsync("user-123");

        var afterUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _database.Received(1).SortedSetAddAsync(
            OnlineKey,
            "user-123",
            Arg.Is<double>(score => score >= beforeUnixSeconds && score <= afterUnixSeconds),
            Arg.Any<SortedSetWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task CountOnlineAsync_CountsOnlyMembersInsideThePresenceWindow()
    {
        _database.SortedSetLengthAsync(
                OnlineKey,
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<CommandFlags>())
            .Returns(7);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var onlineCount = await _presenceTracker.CountOnlineAsync();

        onlineCount.Should().Be(7);
        await _database.Received(1).SortedSetLengthAsync(
            OnlineKey,
            Arg.Is<double>(windowStart =>
                windowStart >= nowUnixSeconds - PresenceWindowSeconds - 2
                && windowStart <= nowUnixSeconds - PresenceWindowSeconds + 2),
            double.PositiveInfinity,
            Arg.Any<Exclude>(),
            Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task PruneAsync_RemovesMembersOlderThanThePresenceWindow()
    {
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _presenceTracker.PruneAsync();

        await _database.Received(1).SortedSetRemoveRangeByScoreAsync(
            OnlineKey,
            double.NegativeInfinity,
            Arg.Is<double>(windowStart =>
                windowStart >= nowUnixSeconds - PresenceWindowSeconds - 2
                && windowStart <= nowUnixSeconds - PresenceWindowSeconds + 2),
            Exclude.Stop,
            Arg.Any<CommandFlags>());
    }
}
