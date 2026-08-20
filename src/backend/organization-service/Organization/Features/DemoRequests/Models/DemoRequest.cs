namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// A "Request a demo" lead submitted by a visitor with no Sellevate account. Deliberately NOT
/// <c>ITenantScoped</c>, for the same reason <see cref="Organizations.Models.Organization"/> is not
/// (docs/TENANCY/TENANCY.md §1.2, §1.9): a lead precedes any tenant, so there is no organization to
/// scope it to yet — the whole point of this table is to exist before one does.
/// </summary>
public sealed class DemoRequest
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string WorkEmail { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string? JobTitle { get; set; }

    public SalesTeamSize SalesTeamSize { get; set; }

    public string? Comment { get; set; }

    public DemoRequestStatus Status { get; set; } = DemoRequestStatus.New;

    /// <summary>
    /// When the required data-processing consent (152-ФЗ) was given. A timestamp rather than a
    /// boolean: a boolean would only record that the box was ticked, and the question that is ever
    /// actually asked about consent is <em>when</em> it was given, not whether.
    /// </summary>
    public DateTime ConsentGivenAt { get; set; }

    /// <summary>
    /// When the separate, optional marketing consent was given, or <see langword="null"/> if it never
    /// was. Kept apart from <see cref="ConsentGivenAt"/> because 152-ФЗ/GDPR guidance treats data
    /// processing and marketing outreach as two distinct purposes that must not share one checkbox — a
    /// visitor may request a demo while declining to be added to a mailing list. Stored as a timestamp
    /// for the same reason as <see cref="ConsentGivenAt"/>: <see langword="null"/> means "not given",
    /// and a non-null value means "given, at this moment," which is what matters if it is ever asked
    /// about.
    /// </summary>
    public DateTime? MarketingConsentGivenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The organization <c>POST …/provision</c> created for this lead, or <see langword="null"/>
    /// before that ever ran. A partial unique index on this column (where not null) is what stops two
    /// leads from ever ending up pointed at the same organization, including under a concurrent
    /// double-click — see <c>DemoRequestEntityConfiguration</c>.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public DemoRequestProvisioningState ProvisioningState { get; set; } = DemoRequestProvisioningState.NotProvisioned;

    /// <summary>The invite <c>identity-service</c> minted for this lead's first administrator.</summary>
    public Guid? BootstrapInviteId { get; set; }

    /// <summary>
    /// The address the bootstrap invite was actually sent to, resolved and fixed the moment the
    /// organization is created. A later call to <c>/provision</c> for the same lead reuses this value
    /// rather than re-reading the request's <c>adminEmail</c>, because by then the invite it names may
    /// already exist — changing the target address on a retry would either strand the first address or
    /// require silently revoking and reissuing an invite nobody asked to revoke.
    /// </summary>
    public string? BootstrapAdminEmail { get; set; }

    public DateTime? ProvisionedAt { get; set; }
}
