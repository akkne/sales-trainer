using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. Asks ai-service whether this organization still has batch allowance, before a sweep
/// claims work it cannot finish.
///
/// <para>
/// The two expensive pipelines — 40.27's content generation and 40.32's batch adaptation — claim a
/// lease with one conditional UPDATE that also spends an attempt, and only then call. Learning about
/// the quota wall after that point costs an attempt and holds a lease for an organization that was
/// never going to be served; three ticks of that and the run is `failed` for a reason that has
/// nothing to do with the run. So the question is asked first, and asked once per organization per
/// tick rather than per item.
/// </para>
///
/// <para>
/// <b>It never increments anything</b> — the roadmap's warning about double counting. The charge
/// happens exactly once, in ai-service, when a completion comes back. This is a read, so calling it
/// twice changes nothing.
/// </para>
/// </summary>
public interface IAiQuotaClient
{
    Task<bool> HasBatchAllowanceAsync(CancellationToken cancellationToken = default);
}

internal sealed class AiQuotaClient(
    HttpClient httpClient,
    IOptions<AiServiceConfiguration> configurationOptions,
    ITenantContext tenantContext,
    ILogger<AiQuotaClient> logger) : IAiQuotaClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AiServiceConfiguration _configuration = configurationOptions.Value;

    public async Task<bool> HasBatchAllowanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                _configuration.BaseUrl.TrimEnd('/') + _configuration.QuotaPreflightPath + "?workload=batch");

            AiCallHeaders.Apply(request, tenantContext, AiCallHeaders.BatchWorkload);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Quota preflight returned {StatusCode}; proceeding and letting the meter decide",
                    response.StatusCode);
                return true;
            }

            var result = await response.Content.ReadFromJsonAsync<AiQuotaPreflightResult>(
                SerializerOptions, cancellationToken);

            return result?.Allowed ?? true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Phase 40.33. **Fail open, deliberately, and only here.** This call is an optimisation:
            // its whole job is to avoid spending an attempt on work that would be refused anyway. If
            // it cannot be answered, the correct behaviour is to do what the pipeline did before this
            // block existed — claim, call, and let the meter on the other side refuse if it must. The
            // real gate is in ai-service and cannot be skipped by a network blip; treating an
            // unreachable preflight as "no allowance" would let one flapping connection stop every
            // customer's content pipeline while their budgets sat untouched.
            logger.LogWarning(exception, "Quota preflight unreachable; proceeding without it");
            return true;
        }
    }

    private sealed record AiQuotaPreflightResult(bool Allowed);
}
