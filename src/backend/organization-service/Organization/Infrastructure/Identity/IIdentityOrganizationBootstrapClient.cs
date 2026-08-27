namespace Sellevate.Organization.Infrastructure.Identity;

/// <summary>
/// The one call organization-service makes to <c>identity-service</c>: mint (or recover) the
/// bootstrap invite for a provisioned organization's administrator. See
/// <c>DemoRequestProvisioningService</c> for why this call exists at all, and
/// docs/DECISIONS.md for why it is synchronous despite the Phase 40.9 precedent against exactly this
/// shape of call.
/// </summary>
public interface IIdentityOrganizationBootstrapClient
{
    /// <summary>
    /// Every failure raises rather than returning a sentinel, and the two exception types below are
    /// the whole vocabulary a caller needs to distinguish:
    /// <see cref="Exceptions.IdentityOrganizationBootstrapBadRequestException"/> for a role or email
    /// identity-service could not accept, and any other exception for everything else — a timeout, an
    /// unreachable host, an unexpected status code — which the caller treats alike as "this call did
    /// not complete". There is no "already has an administrator" outcome: an organization may have as
    /// many administrators as it needs, so a second bootstrap for a second address simply succeeds.
    /// </summary>
    Task<IdentityBootstrapAdminResult> BootstrapAdministratorAsync(
        Guid organizationId,
        string organizationName,
        string organizationSlug,
        string email,
        string? role,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
