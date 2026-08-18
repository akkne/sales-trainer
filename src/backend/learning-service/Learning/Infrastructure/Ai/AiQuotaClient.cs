using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// The HTTP half of <see cref="IAiQuotaClient"/>.
///
/// <para>
/// Phase 40.33. <b>Fail open, deliberately, and only here.</b> This call is an optimisation: its whole
/// job is to avoid spending an attempt on work that would be refused anyway. If it cannot be answered,
/// the correct behaviour is to do what the pipeline did before the preflight existed — claim, call, and
/// let the meter on the other side refuse if it must. The real gate is in ai-service and cannot be
/// skipped by a network blip; treating an unreachable preflight as "no allowance" would let one
/// flapping connection stop every customer's content pipeline while their budgets sat untouched.
/// </para>
/// </summary>
internal sealed class AiQuotaClient(
    HttpClient httpClient,
    IOptions<AiServiceConfiguration> configurationOptions,
    ITenantContext tenantContext,
    ILogger<AiQuotaClient> logger) : IAiQuotaClient
{
    private const string WorkloadQueryParameterName = "workload";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AiServiceConfiguration _configuration = configurationOptions.Value;

    public async Task<bool> HasBatchAllowanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                _configuration.BaseUrl.TrimEnd('/')
                + _configuration.QuotaPreflightPath
                + $"?{WorkloadQueryParameterName}={AiCallHeaders.BatchWorkload}");

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
            logger.LogWarning(exception, "Quota preflight unreachable; proceeding without it");
            return true;
        }
    }

    private sealed record AiQuotaPreflightResult(bool Allowed);
}
