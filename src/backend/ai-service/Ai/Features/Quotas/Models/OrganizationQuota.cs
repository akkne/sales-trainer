using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Quotas.Models;

/// <summary>
/// Phase 40.33. One organization's voice and LLM allowance, stored in ai-db because ai-service is
/// the point that enforces it.
///
/// <para>
/// <b>Why here and not in organization-service.</b> A quota is not part of the tenant registry and
/// not part of the content-substitution profile; it is an operational setting of the meter. The
/// alternative shape — a column on <c>OrganizationProfile</c>, replicated here over
/// <c>organization.profile.updated</c> like 40.19's replica — was rejected on one concrete failure:
/// the moment an operator most needs to change a limit is the moment a customer is standing at it,
/// and a Kafka replica that is lagging (or whose consumer is dead-lettering) would leave the raise
/// invisible to the enforcer with nothing in the raise's own response saying so. The row the meter
/// reads is the row the operator wrote.
/// </para>
///
/// <para>
/// <b>Every limit is nullable, and null means "the platform default"</b>
/// (<c>AiQuotas:Default…</c>). That is what makes the fail-open / fail-closed question a
/// non-question: an organization with no row is not unmetered, it is metered against the defaults —
/// which is exactly what ai-service did before this block, when voice limits lived in
/// <c>Voice:DailyLimitMinutes</c> and applied to everybody.
/// </para>
///
/// <para>
/// Strict tenant data with the tenant column as the primary key, the same call
/// <c>OrganizationProfileReplica</c> made in 40.19: there is no global quota row, and a NULL owner
/// would mean one customer's limit binding everybody.
/// </para>
/// </summary>
public sealed class OrganizationQuota : ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Voice minutes per UTC day for the whole organization. Null = platform default, 0 = window disabled.</summary>
    public int? VoiceDailyLimitMinutes { get; set; }

    /// <summary>Voice minutes per UTC month for the whole organization. Null = platform default, 0 = window disabled.</summary>
    public int? VoiceMonthlyLimitMinutes { get; set; }

    /// <summary>Prompt + completion tokens per UTC month across every model. Null = platform default, 0 = no limit.</summary>
    public long? LlmMonthlyTokenLimit { get; set; }

    /// <summary>
    /// Percent of the monthly LLM allowance batch work may not touch, so a background pipeline stops
    /// before the learners do. Null = platform default.
    /// </summary>
    public int? BatchReservePercent { get; set; }

    /// <summary>Free text for the operator: which contract this number came from, who asked for the raise.</summary>
    public string? Note { get; set; }

    public DateTime UpdatedAt { get; set; }
}
