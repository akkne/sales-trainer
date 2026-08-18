using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Evaluation;
using Sellevate.Ai.Features.Quotas.Constants;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Quotas;

/// <summary>
/// Phase 40.33. «Есть ли у этой организации ещё бюджет?» — asked by a worker before it claims work,
/// never by a person.
///
/// <para>
/// It exists because of a specific ordering the roadmap calls out: the expensive background
/// pipelines (40.27's content generation, 40.32's batch adaptation) claim a lease with one
/// conditional UPDATE that also spends an attempt, and only then make the call. Discovering the
/// quota wall after that point burns an attempt and holds a lease for an organization that was never
/// going to be served — three ticks of that and a run is `failed` for a reason that has nothing to do
/// with the run.
/// </para>
///
/// <para>
/// <b>It reads and never writes</b>, which is what keeps it out of the double-counting the roadmap
/// warns about: the charge happens exactly once, inside the meter, when the completion comes back.
/// The preflight can therefore be called as often as a sweep likes and changes nothing.
/// </para>
///
/// <para>
/// Internal and un-gatewayed, like <c>POST /ai/evaluate</c> and <c>/ai/content/*</c>. The
/// organization arrives in <c>X-Organization-Id</c>, which the sweep sets from the tenant its scope
/// is already pinned to.
/// </para>
/// </summary>
[ApiController]
[Route("ai/quota")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
[TenantTransaction]
public sealed class AiQuotaPreflightController(IAiSpendMeter spendMeter) : ControllerBase
{
    [HttpGet("preflight")]
    public async Task<ActionResult<AiQuotaPreflightResult>> Preflight(
        [FromQuery] string? workload,
        CancellationToken cancellationToken)
    {
        var workloadClass = string.Equals(workload, AiWorkloadClassNames.Batch, StringComparison.OrdinalIgnoreCase)
            ? AiWorkloadClass.Batch
            : AiWorkloadClass.Interactive;

        var allowed = await spendMeter.HasLlmAllowanceAsync(workloadClass, cancellationToken);
        return Ok(new AiQuotaPreflightResult(allowed));
    }
}

public sealed record AiQuotaPreflightResult(bool Allowed);
