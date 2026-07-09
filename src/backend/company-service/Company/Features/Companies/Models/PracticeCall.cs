namespace Sellevate.Company.Features.Companies.Models;

public sealed class PracticeCall
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string DialogSessionId { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Company? Company { get; set; }
}
