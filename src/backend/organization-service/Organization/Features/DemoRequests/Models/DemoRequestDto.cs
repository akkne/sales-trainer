namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// The admin list/status-update shape. The trailing fields report how far <c>POST …/provision</c> has
/// carried this lead.
///
/// <para>
/// <see cref="OrganizationName"/> and <see cref="OrganizationSlug"/> are included, and are not a
/// second copy of data another service owns: <c>Organizations</c> lives in <b>this</b> service's own
/// database, one join away, so resolving them here costs one query and nothing else. Leaving them out
/// is what forced the admin screen to cache the provision response in memory and render "unknown" for
/// any lead provisioned before the current page load — a state the operator sees constantly and the
/// service could always have answered. Both are null until the lead is provisioned.
/// </para>
///
/// <para>
/// The invite's expiry is deliberately still absent, and that asymmetry is the point: <c>Invite</c>
/// belongs to identity-service's database, so reporting it here would mean either a cross-service read
/// on a list endpoint or a replica of somebody else's table. It is carried once, by
/// <c>DemoRequestProvisioningResultDto</c>, at the moment a provision call actually produces it.
/// </para>
/// </summary>
public sealed record DemoRequestDto(
    Guid Id,
    string FullName,
    string WorkEmail,
    string Phone,
    string CompanyName,
    string? JobTitle,
    string SalesTeamSize,
    string? Comment,
    string Status,
    DateTime ConsentGivenAt,
    DateTime? MarketingConsentGivenAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? OrganizationId,
    string? OrganizationName,
    string? OrganizationSlug,
    string ProvisioningState,
    Guid? BootstrapInviteId,
    string? BootstrapAdminEmail,
    DateTime? ProvisionedAt);
