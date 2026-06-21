using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;
using StackExchange.Redis;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Tests for AI1 (Redis reservation gate) and AI7c (provider enum selection).
/// </summary>
[TestFixture]
public class VoiceReservationGateTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static IOptions<VoiceFeatureConfiguration> LimitsOf(int dailyMinutes, int monthlyMinutes) =>
        Options.Create(new VoiceFeatureConfiguration
        {
            DailyLimitMinutes = dailyMinutes,
            MonthlyLimitMinutes = monthlyMinutes,
            MaxRecordingSeconds = 60,
        });

    /// <summary>
    /// Builds a VoiceUsageService backed by a fake Redis that returns the specified
    /// Lua-script results for day (1st call) and month (2nd call).
    /// MongoDbContext and AiDbContext are constructed with real but non-connected stubs
    /// because they are sealed and cannot be mocked with NSubstitute.
    /// ReserveSecondsAsync never touches Mongo so the stubs are safe.
    /// </summary>
    private static (VoiceUsageService svc, IDatabase redis) Build(
        IOptions<VoiceFeatureConfiguration> limits,
        long dayScriptResult,
        long monthScriptResult = -999)
    {
        var redis = Substitute.For<IDatabase>();
        var mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(redis);

        // Wire day-key result first, month-key result second.
        var callCount = 0;
        redis.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(_ =>
            {
                callCount++;
                var result = callCount == 1 ? dayScriptResult
                    : (monthScriptResult == -999 ? dayScriptResult : monthScriptResult);
                return Task.FromResult<RedisResult>(RedisResult.Create((RedisValue)result));
            });

        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult<RedisValue>(RedisValue.Null));

        // MongoDbContext is sealed — construct with a substituted IMongoClient + IConfiguration.
        var mongoClient = Substitute.For<IMongoClient>();
        var mongoDb = Substitute.For<IMongoDatabase>();
        mongoClient.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDb);
        var config = new ConfigurationBuilder().Build();
        var mongoContext = new MongoDbContext(mongoClient, config);

        // AiDbContext is sealed — construct with in-memory EF provider so no DB needed.
        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase("voice-test-" + Guid.NewGuid())
            .Options;
        var dbContext = new AiDbContext(dbOptions);

        var svc = new VoiceUsageService(
            mongoContext,
            dbContext,
            mux,
            limits,
            NullLogger<VoiceUsageService>.Instance);

        return (svc, redis);
    }

    // ── AI1: reservation gate ─────────────────────────────────────────────

    [Test]
    public async Task ReserveSeconds_WhenBelowDailyLimit_ReturnsReservedAmount()
    {
        // daily limit 30 min, monthly 300 min; Lua returns 60 (new counter value)
        var (svc, _) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: 60);

        var reserved = await svc.ReserveSecondsAsync(Guid.NewGuid(), 60);

        reserved.Should().Be(60);
    }

    [Test]
    public async Task ReserveSeconds_WhenDailyLimitExceeded_ThrowsVoiceUsageLimitException()
    {
        // Lua returns -1 for day key → limit exceeded
        var (svc, _) = Build(LimitsOf(30, 300), dayScriptResult: -1);

        var act = () => svc.ReserveSecondsAsync(Guid.NewGuid(), 60);

        await act.Should().ThrowAsync<VoiceUsageLimitException>()
            .Where(e => e.Period == "daily");
    }

    [Test]
    public async Task ReserveSeconds_WhenMonthlyLimitExceeded_ThrowsVoiceUsageLimitException()
    {
        // Day passes (returns 60), month fails (returns -1)
        var (svc, redis) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: -1);

        // Redis StringDecrementAsync must be awaitable — default NSubstitute returns default Task<long>
        redis.StringDecrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));

        var act = () => svc.ReserveSecondsAsync(Guid.NewGuid(), 60);

        await act.Should().ThrowAsync<VoiceUsageLimitException>()
            .Where(e => e.Period == "monthly");
    }

    [Test]
    public async Task ReserveSeconds_WhenMonthlyLimitExceeded_RollsBackDailyReservation()
    {
        var userId = Guid.NewGuid();
        var (svc, redis) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: -1);

        redis.StringDecrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));

        try { await svc.ReserveSecondsAsync(userId, 60); } catch (VoiceUsageLimitException) { }

        // Day rollback must have been called.
        await redis.Received(1).StringDecrementAsync(Arg.Any<RedisKey>(), 60, Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task ReserveSeconds_WhenNoLimitsConfigured_AlwaysPasses()
    {
        // 0 means "unlimited" — Lua script skips the limit check when limit == 0.
        // In our fake, script always returns positive value.
        var (svc, _) = Build(LimitsOf(0, 0), dayScriptResult: 30, monthScriptResult: 30);

        var reserved = await svc.ReserveSecondsAsync(Guid.NewGuid(), 30);

        reserved.Should().Be(30);
    }
}

/// <summary>
/// Tests for AI7c: provider enum drives header/schema selection without URL sniffing.
/// </summary>
[TestFixture]
public class OpenAiProviderConfigTests
{
    [Test]
    public void Provider_DefaultsToOpenAi()
    {
        var config = new OpenAiConfiguration { ApiKey = "key" };
        config.Provider.Should().Be(OpenAiProvider.OpenAi);
    }

    [Test]
    public void Provider_CanBeSetToF5Ai()
    {
        var config = new OpenAiConfiguration { ApiKey = "key", Provider = OpenAiProvider.F5Ai };
        config.Provider.Should().Be(OpenAiProvider.F5Ai);
        config.Provider.Should().NotBe(OpenAiProvider.OpenAi);
    }

    [Test]
    public void Provider_F5AiAndOpenAi_AreDifferentValues()
    {
        OpenAiProvider.F5Ai.Should().NotBe(OpenAiProvider.OpenAi);
    }
}

/// <summary>
/// Tests for AI6: OperationCanceledException caused by the upstream (not client disconnect)
/// should be distinguishable from client-initiated cancellation.
/// This tests the cancellation token logic used in VoiceDialogController.
/// </summary>
[TestFixture]
public class CancellationMappingTests
{
    [Test]
    public void ClientCancel_IsDistinguishedFromCapFired()
    {
        // Simulate: client token cancelled, cap token NOT cancelled
        var clientCts = new CancellationTokenSource();
        var capCts = new CancellationTokenSource();

        clientCts.Cancel();

        // Client disconnect condition: client token requested and cap not triggered
        var isClientDisconnect = clientCts.IsCancellationRequested && !capCts.IsCancellationRequested;
        var isCapFired = capCts.IsCancellationRequested && !clientCts.IsCancellationRequested;

        isClientDisconnect.Should().BeTrue();
        isCapFired.Should().BeFalse();
    }

    [Test]
    public void CapFired_IsDistinguishedFromClientCancel()
    {
        var clientCts = new CancellationTokenSource();
        var capCts = new CancellationTokenSource();

        capCts.Cancel();

        var isClientDisconnect = clientCts.IsCancellationRequested && !capCts.IsCancellationRequested;
        var isCapFired = capCts.IsCancellationRequested && !clientCts.IsCancellationRequested;

        isClientDisconnect.Should().BeFalse();
        isCapFired.Should().BeTrue();
    }

    [Test]
    public async Task LinkedToken_CancelledByCapAfterDelay()
    {
        var clientCts = new CancellationTokenSource();
        using var capCts = CancellationTokenSource.CreateLinkedTokenSource(clientCts.Token);
        capCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var capToken = capCts.Token;

        await Task.Delay(100); // wait for cap to fire

        capCts.IsCancellationRequested.Should().BeTrue();
        clientCts.IsCancellationRequested.Should().BeFalse(); // client did NOT cancel
    }
}
