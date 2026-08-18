using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;
using Sellevate.Ai.Features.Quotas.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Tests for AI1 (Redis reservation gate) and AI7c (provider enum selection).
/// </summary>
[TestFixture]
public class VoiceReservationGateTests
{
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
    /// AiDbContext is constructed with a real but non-connected in-memory provider because it is
    /// sealed and cannot be mocked with NSubstitute; the dialog-session store is reached through
    /// IDialogSessionRepository, which substitutes cleanly. ReserveSecondsAsync never touches
    /// Mongo anyway.
    ///
    /// <para>
    /// <b>The fake answers by call order, not by key: first call is the day window, second is the
    /// month.</b> Anything that changes how many Redis calls <c>ReserveSecondsAsync</c> makes, or in
    /// what order, silently re-points every case in this fixture at the wrong window — the assertions
    /// keep passing while testing something else. A Lua result of <c>-1</c> means "limit exceeded";
    /// a positive number is the new counter value.
    /// </para>
    ///
    /// <para>
    /// <c>StringDecrementAsync</c> is stubbed explicitly because an unstubbed NSubstitute call returns
    /// a default <c>Task&lt;long&gt;</c>, which is <see langword="null"/> and throws when awaited — so
    /// the rollback path would fail for a reason that has nothing to do with rollback.
    /// </para>
    /// </summary>
    private static (VoiceUsageService voiceUsageService, IDatabase redis) Build(
        IOptions<VoiceFeatureConfiguration> limits,
        long dayScriptResult,
        long monthScriptResult = -999)
    {
        var redis = Substitute.For<IDatabase>();
        var mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(redis);

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

        var dbContext = AiDbContextFactory.CreateInMemory("voice-test-" + Guid.NewGuid());

        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(AiDbContextFactory.DefaultOrganizationId);

        var voiceUsageService = new VoiceUsageService(
            Substitute.For<IDialogSessionRepository>(),
            dbContext,
            tenantContext,
            mux,
            limits,
            Substitute.For<IAiSpendMeter>(),
            Substitute.For<IAiQuotaService>(),
            NullLogger<VoiceUsageService>.Instance);

        return (voiceUsageService, redis);
    }

    /// <summary>
    /// Phase 40.11. A voice quota is already per-user, so this key could not have leaked one
    /// customer's usage into another's — but "no ai-service Redis key is shared across
    /// organizations" is only checkable by reading key names if every key carries the prefix.
    /// </summary>
    [Test]
    public async Task ReserveSeconds_KeysAreNamespacedByOrganization()
    {
        var (voiceUsageService, redis) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: 60);

        await voiceUsageService.ReserveSecondsAsync(Guid.NewGuid(), 60);

        var usedKeys = redis.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IDatabase.ScriptEvaluateAsync))
            .SelectMany(call => (RedisKey[])call.GetArguments()[1]!)
            .Select(key => key.ToString())
            .ToList();

        usedKeys.Should().HaveCount(2);
        usedKeys.Should().OnlyContain(key =>
            key.StartsWith($"org:{AiDbContextFactory.DefaultOrganizationId}:voice:", StringComparison.Ordinal));
    }

    [Test]
    public async Task ReserveSeconds_WhenBelowDailyLimit_ReturnsReservedAmount()
    {
        var (voiceUsageService, _) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: 60);

        var reserved = await voiceUsageService.ReserveSecondsAsync(Guid.NewGuid(), 60);

        reserved.Should().Be(60);
    }

    [Test]
    public async Task ReserveSeconds_WhenDailyLimitExceeded_ThrowsVoiceUsageLimitException()
    {
        var (voiceUsageService, _) = Build(LimitsOf(30, 300), dayScriptResult: -1);

        var act = () => voiceUsageService.ReserveSecondsAsync(Guid.NewGuid(), 60);

        await act.Should().ThrowAsync<VoiceUsageLimitException>()
            .Where(e => e.Period == "daily");
    }

    [Test]
    public async Task ReserveSeconds_WhenMonthlyLimitExceeded_ThrowsVoiceUsageLimitException()
    {
        var (voiceUsageService, redis) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: -1);

        redis.StringDecrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));

        var act = () => voiceUsageService.ReserveSecondsAsync(Guid.NewGuid(), 60);

        await act.Should().ThrowAsync<VoiceUsageLimitException>()
            .Where(e => e.Period == "monthly");
    }

    [Test]
    public async Task ReserveSeconds_WhenMonthlyLimitExceeded_RollsBackDailyReservation()
    {
        var userId = Guid.NewGuid();
        var (voiceUsageService, redis) = Build(LimitsOf(30, 300), dayScriptResult: 60, monthScriptResult: -1);

        redis.StringDecrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));

        try { await voiceUsageService.ReserveSecondsAsync(userId, 60); } catch (VoiceUsageLimitException) { }

        await redis.Received(1).StringDecrementAsync(Arg.Any<RedisKey>(), 60, Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task ReserveSeconds_WhenNoLimitsConfigured_AlwaysPasses()
    {
        var (voiceUsageService, _) = Build(LimitsOf(0, 0), dayScriptResult: 30, monthScriptResult: 30);

        var reserved = await voiceUsageService.ReserveSecondsAsync(Guid.NewGuid(), 30);

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
        var clientCts = new CancellationTokenSource();
        var capCts = new CancellationTokenSource();

        clientCts.Cancel();

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

        await Task.Delay(100);

        capCts.IsCancellationRequested.Should().BeTrue();
        clientCts.IsCancellationRequested.Should().BeFalse();
    }
}
