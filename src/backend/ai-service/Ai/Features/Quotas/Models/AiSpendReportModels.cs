namespace Sellevate.Ai.Features.Quotas.Models;

/// <summary>
/// Phase 40.33. What the caller's organization has spent this month, and how close it is to the wall.
///
/// <para>
/// This is the roadmap's «расход виден в дашборде раньше, чем в счёте от провайдера». It is an API
/// answer and not a Prometheus series on purpose: per-organization numbers in a metric label are the
/// thing <c>docs/MONITORING.md</c> has refused four times, because a customer id in a label puts
/// identities and unbounded cardinality into the monitoring store. Platform-wide totals are safe
/// there; "which customer" is answered here, from the rows.
/// </para>
/// </summary>
public sealed class AiSpendReportDto
{
    public required string PeriodKey { get; init; }

    public required string Currency { get; init; }

    /// <summary><c>ok</c>, <c>warning</c> (past the soft threshold), <c>batch_paused</c>, or <c>exhausted</c>.</summary>
    public required string QuotaState { get; init; }

    public long LlmPromptTokens { get; init; }

    public long LlmCompletionTokens { get; init; }

    public long LlmTotalTokens { get; init; }

    public long LlmMonthlyTokenLimit { get; init; }

    public long LlmCallCount { get; init; }

    /// <summary>How many of <see cref="LlmCallCount"/> were counted from an estimate rather than a reported usage block.</summary>
    public long LlmEstimatedCallCount { get; init; }

    public long SpeechCharacters { get; init; }

    public int VoiceUsedMinutesToday { get; init; }

    public int VoiceDailyLimitMinutes { get; init; }

    public int VoiceUsedMinutesThisMonth { get; init; }

    public int VoiceMonthlyLimitMinutes { get; init; }

    /// <summary>Derived from the price table, never stored. Null when no price is configured for any model used.</summary>
    public decimal? EstimatedCost { get; init; }

    /// <summary>True when at least one model used this month has no entry in the price table.</summary>
    public bool HasUnpricedModels { get; init; }

    public List<AiSpendModelLineDto> Models { get; init; } = [];
}

public sealed class AiSpendModelLineDto
{
    public required string Model { get; init; }

    public required string Kind { get; init; }

    public long PromptTokens { get; init; }

    public long CompletionTokens { get; init; }

    public long CallCount { get; init; }

    public long SpeechCharacters { get; init; }

    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// The allowance as the platform operator sees and edits it. Every field is nullable and null means
/// "fall back to the platform default", which is also what clearing a field does.
/// </summary>
public sealed class AiQuotaSettingsDto
{
    public int? VoiceDailyLimitMinutes { get; init; }

    public int? VoiceMonthlyLimitMinutes { get; init; }

    public long? LlmMonthlyTokenLimit { get; init; }

    public int? BatchReservePercent { get; init; }

    public string? Note { get; init; }

    /// <summary>False when no row exists and every number below is the platform default.</summary>
    public bool IsOrganizationSpecific { get; init; }

    public int EffectiveVoiceDailyLimitMinutes { get; init; }

    public int EffectiveVoiceMonthlyLimitMinutes { get; init; }

    public long EffectiveLlmMonthlyTokenLimit { get; init; }

    public int EffectiveBatchReservePercent { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

/// <summary>The editable half of <see cref="AiQuotaSettingsDto"/>. Omitted fields clear to the platform default.</summary>
public sealed class AiQuotaWriteModel
{
    public int? VoiceDailyLimitMinutes { get; init; }

    public int? VoiceMonthlyLimitMinutes { get; init; }

    public long? LlmMonthlyTokenLimit { get; init; }

    public int? BatchReservePercent { get; init; }

    public string? Note { get; init; }
}
