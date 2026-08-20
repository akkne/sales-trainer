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
}
