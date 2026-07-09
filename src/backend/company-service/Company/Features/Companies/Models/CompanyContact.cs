namespace Sellevate.Company.Features.Companies.Models;

public sealed class CompanyContact
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company? Company { get; set; }
    public ICollection<CallLogEntry> CallLogEntries { get; set; } = new List<CallLogEntry>();
}
