using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Quotas.Constants;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Features.Quotas.Services.Implementation;

/// <summary>
/// Phase 40.33. Reads and writes one organization's allowance, and renders the month's spend.
///
/// <para>
/// <see cref="ResolveAsync"/> is memoized for the lifetime of the request: the meter asks for it on
/// every LLM call, and a dialog turn makes two. Memoizing per request rather than per process is
/// deliberate — an operator raising a limit expects the next call to see it, not the next deploy.
/// </para>
/// </summary>
internal sealed class AiQuotaService : IAiQuotaService
{
    private readonly AiDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;
    private readonly IOptions<AiQuotaConfiguration> _quotaOptions;
    private readonly IDatabase _redis;

    private ResolvedAiQuota? _memoizedQuota;

    public AiQuotaService(
        AiDbContext databaseContext,
        ITenantContext tenantContext,
        IOptions<AiQuotaConfiguration> quotaOptions,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _databaseContext = databaseContext;
        _tenantContext = tenantContext;
        _quotaOptions = quotaOptions;
        _redis = connectionMultiplexer.GetDatabase();
    }

    public async Task<ResolvedAiQuota> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_memoizedQuota is not null)
        {
            return _memoizedQuota;
        }

        var row = await LoadRowAsync(cancellationToken);
        _memoizedQuota = Resolve(row);
        return _memoizedQuota;
    }

    public async Task<AiQuotaSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(cancellationToken);
        return ToSettings(row, Resolve(row));
    }

    /// <summary>
    /// Reads <paramref name="organizationId"/>'s row directly, bypassing <see cref="_tenantContext"/>
    /// entirely — unlike every other method here, deliberately. The one caller of this method
    /// (<c>AdminAiQuotaController.GetQuotaForOrganization</c>) is already <c>RequirePlatformAdmin</c>,
    /// which means the request is already in platform-wide mode by the time it reaches here, so
    /// <c>OrganizationQuota</c>'s query filter (<c>IsPlatformWide || OrganizationId == current</c>) is
    /// wide open and the explicit predicate below is what actually narrows the read to one
    /// organization — the same shape <see cref="LoadRowAsync"/> uses for the caller's own
    /// organization, just against an id named by the route instead of the token.
    /// </summary>
    public async Task<AiQuotaSettingsDto> GetSettingsForOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var row = await _databaseContext.OrganizationQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(quota => quota.OrganizationId == organizationId, cancellationToken);
        return ToSettings(row, Resolve(row));
    }

    public async Task<AiQuotaSettingsDto> SaveSettingsAsync(
        AiQuotaWriteModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var organizationId = _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        var row = await _databaseContext.OrganizationQuotas
            .FirstOrDefaultAsync(quota => quota.OrganizationId == organizationId, cancellationToken);

        if (row is null)
        {
            row = new OrganizationQuota { OrganizationId = organizationId };
            _databaseContext.OrganizationQuotas.Add(row);
        }

        row.VoiceDailyLimitMinutes = NonNegativeOrNull(model.VoiceDailyLimitMinutes);
        row.VoiceMonthlyLimitMinutes = NonNegativeOrNull(model.VoiceMonthlyLimitMinutes);
        row.LlmMonthlyTokenLimit = model.LlmMonthlyTokenLimit is { } tokens && tokens >= 0 ? tokens : null;
        row.BatchReservePercent = model.BatchReservePercent is { } percent
            ? Math.Clamp(percent, 0, AiQuotaScales.MaximumBatchReservePercent)
            : null;
        row.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        _memoizedQuota = null;
        return ToSettings(row, Resolve(row));
    }

    /// <summary>
    /// The month's spend for the caller's organization, or the installation-wide total when the
    /// caller carries no organization.
    ///
    /// <para>
    /// The row query is constrained to the caller's organization *when there is one*, and left to the
    /// query filter when there is not. The distinction is the whole point. A platform administrator
    /// carrying no organization is meant to read the installation-wide total, and does. A platform
    /// administrator who also holds a membership is asking about that membership's organization — but
    /// the filter reads <c>IsPlatformWide || …</c>, so without the explicit predicate they were shown
    /// the installation total under their own organization's heading. Review, 40.34.
    /// </para>
    ///
    /// <para>
    /// <see cref="AiSpendReportDto.EstimatedCost"/> is null as soon as any line is unpriced, because a
    /// partial total is worse than none. Skipping unpriced lines and summing the rest produced a
    /// concrete-looking figure that omitted the dominant cost: the shipped price table lists
    /// yandex-tts and no LLM model at all, so the whole LLM bill contributed zero while
    /// <c>HasUnpricedModels</c> sat quietly beside a number that read as complete. The same reasoning
    /// already governs the per-line value — an unpriced model reports null, never zero, because zero
    /// reads as "this model is free". Review, 40.34.
    /// </para>
    /// </summary>
    public async Task<AiSpendReportDto> GetSpendReportAsync(CancellationToken cancellationToken = default)
    {
        var configuration = _quotaOptions.Value;
        var periodKey = AiUsagePeriod.Current();
        var quota = await ResolveAsync(cancellationToken);

        var organizationId = _tenantContext.OrganizationId;
        var rows = await _databaseContext.AiUsageRecords
            .Where(record => record.PeriodKey == periodKey)
            .Where(record => organizationId == null || record.OrganizationId == organizationId)
            .OrderBy(record => record.Model)
            .ToListAsync(cancellationToken);

        var lines = new List<AiSpendModelLineDto>(rows.Count);
        decimal? totalCost = null;
        var hasUnpricedModels = false;

        foreach (var row in rows)
        {
            var lineCost = PriceLine(row, configuration, out var unpriced);
            hasUnpricedModels |= unpriced;

            if (lineCost is { } cost)
            {
                totalCost = (totalCost ?? 0m) + cost;
            }

            lines.Add(new AiSpendModelLineDto
            {
                Model = row.Model,
                Kind = row.Kind,
                PromptTokens = row.PromptTokens,
                CompletionTokens = row.CompletionTokens,
                CallCount = row.CallCount,
                SpeechCharacters = row.SpeechCharacters,
                EstimatedCost = lineCost,
            });
        }

        var llmRows = rows.Where(row => row.Kind == AiUsageKinds.Llm).ToList();
        var promptTokens = llmRows.Sum(row => row.PromptTokens);
        var completionTokens = llmRows.Sum(row => row.CompletionTokens);
        var totalTokens = promptTokens + completionTokens;

        var (daySeconds, monthSeconds) = await ReadVoiceSecondsAsync();

        return new AiSpendReportDto
        {
            PeriodKey = periodKey,
            Currency = configuration.Currency,
            QuotaState = DescribeState(totalTokens, quota, configuration),
            LlmPromptTokens = promptTokens,
            LlmCompletionTokens = completionTokens,
            LlmTotalTokens = totalTokens,
            LlmMonthlyTokenLimit = quota.LlmMonthlyTokenLimit,
            LlmCallCount = llmRows.Sum(row => row.CallCount),
            LlmEstimatedCallCount = llmRows.Sum(row => row.EstimatedCallCount),
            SpeechCharacters = rows.Where(row => row.Kind != AiUsageKinds.Llm).Sum(row => row.SpeechCharacters),
            VoiceUsedMinutesToday = daySeconds / AiQuotaScales.SecondsPerMinute,
            VoiceDailyLimitMinutes = quota.VoiceDailyLimitMinutes,
            VoiceUsedMinutesThisMonth = monthSeconds / AiQuotaScales.SecondsPerMinute,
            VoiceMonthlyLimitMinutes = quota.VoiceMonthlyLimitMinutes,
            EstimatedCost = hasUnpricedModels ? null : totalCost,
            HasUnpricedModels = hasUnpricedModels,
            Models = lines,
        };
    }

    /// <summary>
    /// The four states an organization can be in, and the one that matters is the third: batch work
    /// has stopped while interactive work has not. A report that only said «ok / exhausted» would
    /// leave an administrator wondering why their content pipeline went quiet on a month they can
    /// still hold conversations in.
    /// </summary>
    private static string DescribeState(long spentTokens, ResolvedAiQuota quota, AiQuotaConfiguration configuration)
    {
        if (quota.LlmMonthlyTokenLimit <= 0)
        {
            return AiQuotaStates.Ok;
        }

        if (spentTokens >= quota.LlmMonthlyTokenLimit)
        {
            return AiQuotaStates.Exhausted;
        }

        if (spentTokens >= quota.BatchTokenCeiling)
        {
            return AiQuotaStates.BatchPaused;
        }

        var warningPercent = Math.Clamp(configuration.SoftWarningPercent, 1, AiQuotaScales.PercentScale);
        return spentTokens >= quota.LlmMonthlyTokenLimit * warningPercent / AiQuotaScales.PercentScale
            ? AiQuotaStates.Warning
            : AiQuotaStates.Ok;
    }

    /// <summary>
    /// Prices one usage row, reporting a model nobody priced as unpriced rather than as free. Zero
    /// looks like "this model costs nothing", which is the one reading of a spend report that must
    /// never be accidental.
    /// </summary>
    private static decimal? PriceLine(AiUsageRecord row, AiQuotaConfiguration configuration, out bool unpriced)
    {
        var billableUnits = row.Kind == AiUsageKinds.Llm
            ? row.PromptTokens + row.CompletionTokens
            : row.SpeechCharacters;

        if (billableUnits == 0)
        {
            unpriced = false;
            return 0m;
        }

        if (configuration.PricePerMillionTokens.TryGetValue(row.Model, out var pricePerMillion))
        {
            unpriced = false;
            return billableUnits * pricePerMillion / AiQuotaScales.PriceUnitTokens;
        }

        unpriced = configuration.FallbackPricePerMillionTokens <= 0m;
        return unpriced
            ? null
            : billableUnits * configuration.FallbackPricePerMillionTokens / AiQuotaScales.PriceUnitTokens;
    }

    private async Task<(int DaySeconds, int MonthSeconds)> ReadVoiceSecondsAsync()
    {
        if (_tenantContext.OrganizationId is not { } organizationId)
        {
            return (0, 0);
        }

        var now = DateTime.UtcNow;
        var day = await _redis.StringGetAsync(AiVoiceQuotaKeys.Day(organizationId, now));
        var month = await _redis.StringGetAsync(AiVoiceQuotaKeys.Month(organizationId, now));

        return (day.HasValue ? (int)day : 0, month.HasValue ? (int)month : 0);
    }

    /// <summary>
    /// The caller organization's quota row, or null when there is no organization on the request.
    ///
    /// <para>
    /// The organization predicate is explicit rather than left to the global query filter. That filter
    /// reads <c>IsPlatformWide || OrganizationId == current</c>, so for Sellevate staff who also hold
    /// a membership — a combination <c>TenantContext</c> supports on purpose — a filter-only query
    /// returns whichever row Postgres hands back first. <c>GET /admin/ai-quota</c> would render
    /// another customer's limits and free-text note, and a <c>PUT</c> of that same form would copy
    /// them onto the caller's own organization. Found in review, 40.34;
    /// <see cref="SaveSettingsAsync"/> always had it.
    /// </para>
    /// </summary>
    private async Task<OrganizationQuota?> LoadRowAsync(CancellationToken cancellationToken)
    {
        if (_tenantContext.OrganizationId is not { } organizationId)
        {
            return null;
        }

        return await _databaseContext.OrganizationQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(quota => quota.OrganizationId == organizationId, cancellationToken);
    }

    private ResolvedAiQuota Resolve(OrganizationQuota? row)
    {
        var defaults = _quotaOptions.Value;

        return new ResolvedAiQuota(
            VoiceDailyLimitMinutes: row?.VoiceDailyLimitMinutes ?? defaults.DefaultVoiceDailyLimitMinutes,
            VoiceMonthlyLimitMinutes: row?.VoiceMonthlyLimitMinutes ?? defaults.DefaultVoiceMonthlyLimitMinutes,
            LlmMonthlyTokenLimit: row?.LlmMonthlyTokenLimit ?? defaults.DefaultLlmMonthlyTokenLimit,
            BatchReservePercent: row?.BatchReservePercent ?? defaults.DefaultBatchReservePercent,
            IsOrganizationSpecific: row is not null);
    }

    private static AiQuotaSettingsDto ToSettings(OrganizationQuota? row, ResolvedAiQuota resolved) => new()
    {
        VoiceDailyLimitMinutes = row?.VoiceDailyLimitMinutes,
        VoiceMonthlyLimitMinutes = row?.VoiceMonthlyLimitMinutes,
        LlmMonthlyTokenLimit = row?.LlmMonthlyTokenLimit,
        BatchReservePercent = row?.BatchReservePercent,
        Note = row?.Note,
        IsOrganizationSpecific = resolved.IsOrganizationSpecific,
        EffectiveVoiceDailyLimitMinutes = resolved.VoiceDailyLimitMinutes,
        EffectiveVoiceMonthlyLimitMinutes = resolved.VoiceMonthlyLimitMinutes,
        EffectiveLlmMonthlyTokenLimit = resolved.LlmMonthlyTokenLimit,
        EffectiveBatchReservePercent = resolved.BatchReservePercent,
        UpdatedAt = row?.UpdatedAt,
    };

    private static int? NonNegativeOrNull(int? value) => value is { } number && number >= 0 ? number : null;
}
