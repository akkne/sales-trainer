using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Organization.Infrastructure.Configuration;
using Sellevate.Organization.Infrastructure.Identity.Exceptions;

namespace Sellevate.Organization.Infrastructure.Identity;

/// <summary>
/// Talks to <c>identity-service</c>'s <c>internal/organizations/{organizationId}/bootstrap-admin</c>
/// over the typed <see cref="HttpClient"/> registered by
/// <see cref="IdentityOrganizationBootstrapClientServiceCollectionExtensions"/>, which attaches the
/// shared internal-service-secret header. This is organization-service's first outbound
/// <see cref="HttpClient"/> and its first reference to that handshake — every other service that
/// already talks to identity-service (learning-service's <c>IdentityOrganizationMemberDirectory</c>)
/// follows the same shape: a local record mirroring the wire contract rather than a shared type, and
/// every failure raised rather than swallowed into a fallback, because the caller's next act is
/// writing rows that decide whether a lead ever gets a working administrator.
/// </summary>
internal sealed class IdentityOrganizationBootstrapClient(
    HttpClient httpClient,
    IOptions<IdentityServiceConfiguration> configurationOptions,
    ILogger<IdentityOrganizationBootstrapClient> logger) : IIdentityOrganizationBootstrapClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IdentityBootstrapAdminResult> BootstrapAdministratorAsync(
        Guid organizationId,
        string organizationName,
        string organizationSlug,
        string email,
        string? role,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var configuration = configurationOptions.Value;
        var requestPath = string.Format(configuration.BootstrapAdministratorPathTemplate, organizationId);
        var requestUri = configuration.BaseUrl.TrimEnd('/') + requestPath;

        using var response = await httpClient.PostAsJsonAsync(
            requestUri,
            new BootstrapAdministratorRequest(organizationName, organizationSlug, email, role, actorUserId),
            SerializerOptions,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var payload = await response.Content.ReadFromJsonAsync<BootstrapAdministratorResponse>(
                SerializerOptions, cancellationToken);

            if (payload is null)
            {
                throw new InvalidOperationException(
                    $"identity-service returned an unreadable bootstrap-admin response for organization '{organizationId}'.");
            }

            return new IdentityBootstrapAdminResult(payload.InviteId, payload.Email, payload.ExpiresAt);
        }

        var errorBody = await ReadErrorMessageAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new IdentityOrganizationBootstrapBadRequestException(errorBody);
        }

        logger.LogWarning(
            "identity-service returned {StatusCode} for bootstrap-admin on organization {OrganizationId}: {ErrorBody}",
            response.StatusCode, organizationId, errorBody);

        throw new InvalidOperationException(
            $"identity-service returned {(int)response.StatusCode} for bootstrap-admin on organization '{organizationId}'.");
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorPayload = await response.Content.ReadFromJsonAsync<BootstrapAdministratorErrorResponse>(
                SerializerOptions, cancellationToken);
            return errorPayload?.Message ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// A local mirror of identity-service's request shape, not a shared type — the two services agree
    /// on a wire contract, the same convention every other cross-service client in this system follows.
    /// </summary>
    private sealed record BootstrapAdministratorRequest(
        string OrganizationName,
        string OrganizationSlug,
        string Email,
        string? Role,
        Guid ActorUserId);

    private sealed record BootstrapAdministratorResponse(Guid InviteId, string Email, DateTime ExpiresAt);

    private sealed record BootstrapAdministratorErrorResponse(string? Message);
}
