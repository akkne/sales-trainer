namespace Sellevate.Company.Features.Companies.Models;

public sealed class Company
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CallLogEntry> CallLogEntries { get; set; } = new List<CallLogEntry>();
    public ICollection<PracticeCall> PracticeCalls { get; set; } = new List<PracticeCall>();
}
