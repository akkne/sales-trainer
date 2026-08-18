using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

/// <summary>
/// Resolves an address to an organization, then to that organization's login method
/// (docs/TENANCY/TENANCY.md §4.5). Runs before authentication, so it has no tenant context and
/// queries across organizations by design — see <see cref="OrganizationAuthConfiguration"/> for
/// why that table is not behind row-level security.
///
/// <para>
/// An organization with no configuration row yet resolves to passwords — the same answer the
/// platform default gives, so an outsider cannot tell the two apart.
/// </para>
/// </summary>
internal sealed class OrganizationAuthConfigurationResolver(
    IdentityDbContext databaseContext,
    ILogger<OrganizationAuthConfigurationResolver> logger) : IOrganizationAuthConfigurationResolver
{
    public async Task<ResolvedLoginMethod> ResolveForEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();

        var organizationId = await FindOrganizationByActiveMembershipAsync(normalizedEmail, cancellationToken)
            ?? await FindOrganizationByEmailDomainAsync(normalizedEmail, cancellationToken);

        if (organizationId is not { } resolvedOrganizationId)
        {
            return ResolvedLoginMethod.PlatformDefault;
        }

        var configuredMethod = await databaseContext.OrganizationAuthConfigurations
            .AsNoTracking()
            .Where(configuration => configuration.OrganizationId == resolvedOrganizationId)
            .Select(configuration => configuration.Method)
            .FirstOrDefaultAsync(cancellationToken);

        var method = string.IsNullOrEmpty(configuredMethod) ? AuthMethodNames.Password : configuredMethod;

        logger.LogInformation(
            "Login method resolved OrganizationId={OrganizationIdentifier} Method={Method}",
            resolvedOrganizationId, method);

        return new ResolvedLoginMethod(method, resolvedOrganizationId);
    }

    /// <summary>
    /// The "resolve by invite" branch of the roadmap: someone who accepted an invite already has a
    /// membership, which is a far stronger signal than a shared mail domain. Matches
    /// <c>AuthenticationService.IssueTokensForUserAsync</c>'s deterministic
    /// "earliest active membership" choice so both agree while a user can only hold one.
    /// </summary>
    private async Task<Guid?> FindOrganizationByActiveMembershipAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
        => await databaseContext.Users
            .AsNoTracking()
            .Where(user => user.Email == normalizedEmail)
            .Join(
                databaseContext.Memberships.Where(membership => membership.Status == MembershipStatus.Active),
                user => user.Id,
                membership => membership.UserId,
                (_, membership) => membership)
            .OrderBy(membership => membership.JoinedAt)
            .Select(membership => (Guid?)membership.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Guid?> FindOrganizationByEmailDomainAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var emailDomain = ExtractEmailDomain(normalizedEmail);
        if (emailDomain is null)
        {
            return null;
        }

        return await databaseContext.OrganizationAuthConfigurations
            .AsNoTracking()
            .Where(configuration => configuration.AllowedEmailDomains.Contains(emailDomain))
            .OrderBy(configuration => configuration.CreatedAt)
            .Select(configuration => (Guid?)configuration.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? ExtractEmailDomain(string normalizedEmail)
    {
        var separatorIndex = normalizedEmail.LastIndexOf('@');
        if (separatorIndex < 0 || separatorIndex == normalizedEmail.Length - 1)
        {
            return null;
        }

        return normalizedEmail[(separatorIndex + 1)..];
    }
}
