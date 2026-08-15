using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Company.Features.Companies.Models;

public sealed class CallLogEntry : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.12. The organization that owns this row. Together with <see cref="UserId"/> this is
    /// the <b>double scope</b> of company-service: a row belongs to one salesperson inside one
    /// organization. The organization half is enforced by the query filter in
    /// <c>CompanyDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs; the user half stays an explicit predicate on every
    /// query. Never assigned from a request — <c>TenantSaveChangesInterceptor</c> stamps it from
    /// <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company? Company { get; set; }
    public CompanyContact? Contact { get; set; }
}
