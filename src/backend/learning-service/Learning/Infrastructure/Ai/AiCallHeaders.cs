using Sellevate.BuildingBlocks.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. The two headers every learning → ai call now carries.
///
/// <para>
/// <b><c>X-Organization-Id</c>.</b> ai-service's internal routes used to be genuinely stateless —
/// "no organization, no database, no job", as 40.27 put it — and that was fine while nothing there
/// was metered. It stopped being fine the moment spend became per-organization: a call with no
/// tenant is a call nobody's budget pays for, and ai-service now refuses it. The value is read from
/// this service's own <see cref="ITenantContext"/>, which for the background sweeps is the
/// organization their scope is already pinned to and for a request is the gateway-validated header.
/// </para>
///
/// <para>
/// <b><c>X-Ai-Workload</c>.</b> Declares whether a person is waiting. ai-service cannot tell — an
/// internal POST looks identical either way — and the distinction is what lets an organization run
/// out of batch budget while its reps keep talking. Absent means interactive, so a caller that never
/// heard of this header gets the permissive answer rather than being quietly held at 90%.
/// </para>
///
/// <para>
/// Applied per request rather than on the typed client's <c>DefaultRequestHeaders</c>, because those
/// are set once at DI time and shared by every tenant that instance ever serves — the one shape of
/// this that would be a cross-tenant bug.
/// </para>
/// </summary>
internal static class AiCallHeaders
{
    public const string WorkloadHeaderName = "X-Ai-Workload";

    public const string BatchWorkload = "batch";

    public const string InteractiveWorkload = "interactive";

    public static void Apply(HttpRequestMessage request, ITenantContext tenantContext, string workload)
    {
        if (tenantContext.OrganizationId is { } organizationId)
        {
            request.Headers.TryAddWithoutValidation(
                IdentityHeaders.OrganizationId, organizationId.ToString());
        }

        request.Headers.TryAddWithoutValidation(WorkloadHeaderName, workload);
    }
}
