using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Identity;

/// <summary>
/// Phase 40.23. Asks identity-service for the current organization's roster.
///
/// <para>
/// <b>The organization is put on the wire as the header the gateway would have set</b>
/// (<c>X-Organization-Id</c>), read from <see cref="ITenantContext"/> — never from anything the
/// caller's request body said. A service-to-service call carries no JWT, so this header is the only
/// thing that tells identity-service which tenant is being asked about, which is exactly why the
/// route on the other side is guarded by a shared secret as well as by <c>[TenantScoped]</c>.
/// </para>
///
/// <para>
/// <b>Every failure raises.</b> There is no cached fallback and no empty-list-on-error path, because
/// the caller's next act is to write the rows that decide who was asked to do something: a roster
/// that silently comes back short issues an assignment to a subset of the team and reports success,
/// and nobody discovers it until the РОП wonders why the funnel says nine people instead of forty.
/// A raised exception surfaces as a refused issue that a human can retry.
/// </para>
/// </summary>
internal sealed class IdentityOrganizationMemberDirectory : IOrganizationMemberDirectory
{
    public const string HttpClientName = "IdentityService";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly IdentityServiceConfiguration _configuration;
    private readonly ILogger<IdentityOrganizationMemberDirectory> _logger;

    public IdentityOrganizationMemberDirectory(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IOptions<IdentityServiceConfiguration> configurationOptions,
        ILogger<IdentityOrganizationMemberDirectory> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.ActiveMemberIdsPath;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add(IdentityHeaders.OrganizationId, organizationId.ToString());

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Identity-service returned {StatusCode} for the member roster of organization {OrganizationId}.",
                response.StatusCode, organizationId);

            throw new InvalidOperationException(
                $"Identity service returned {(int)response.StatusCode} while resolving the organization's members.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OrganizationMemberIdsResponse>(
            SerializerOptions, cancellationToken);

        if (payload?.UserIds is null)
        {
            throw new InvalidOperationException(
                "Identity service returned an unreadable member roster.");
        }

        return payload.UserIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// A local copy of identity-service's <c>OrganizationMemberIdsDto</c>, matching how every
    /// cross-service contract in this system is expressed: the two services agree on a wire shape,
    /// not on a shared type.
    /// </summary>
    private sealed record OrganizationMemberIdsResponse(IReadOnlyList<Guid>? UserIds);
}
