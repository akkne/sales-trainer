namespace Sellevate.Company.Features.Companies.Models;

public sealed class CompanyPersona
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public PersonaDifficulty Difficulty { get; set; } = PersonaDifficulty.Medium;
    public DateTime CreatedAt { get; set; }

    public Company? Company { get; set; }
}
