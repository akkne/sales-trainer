using Sellevate.BuildingBlocks.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. The organization and workload headers every company → ai call now carries.
///
/// <para>
/// company-service's four AI calls — briefing, persona, parse-log, readiness — used to reach
/// ai-service with nothing but the internal shared secret, so their LLM spend belonged to nobody.
/// ai-service now refuses a metered call with no organization, which is why this is not optional and
/// why both ends ship in one commit. The organization is the one company-service is already scoped
/// to (40.12: scope here is double, by organization *and* by user).
/// </para>
///
/// <para>
/// All four declare themselves interactive: every one of them is a person pressing a button and
/// waiting for the answer. None runs in a sweep.
/// </para>
///
/// <para>
/// The learning-service copy of this file says the same thing for the same reason. It is duplicated
/// rather than shared through BuildingBlocks because it is four lines of header names, and the
/// alternative is a building block that exists to hold a constant.
/// </para>
/// </summary>
internal static class AiCallHeaders
{
    public const string WorkloadHeaderName = "X-Ai-Workload";

    public const string InteractiveWorkload = "interactive";

    public static void Apply(HttpRequestMessage request, ITenantContext tenantContext)
    {
        if (tenantContext.OrganizationId is { } organizationId)
        {
            request.Headers.TryAddWithoutValidation(
                IdentityHeaders.OrganizationId, organizationId.ToString());
        }

        request.Headers.TryAddWithoutValidation(WorkloadHeaderName, InteractiveWorkload);
    }
}
