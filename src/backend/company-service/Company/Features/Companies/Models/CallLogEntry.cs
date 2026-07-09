namespace Sellevate.Company.Features.Companies.Models;

public sealed class CallLogEntry
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
}
