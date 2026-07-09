namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class CompanyCallContextDto
{
    public string CompanyName { get; set; } = null!;
    public string CompanyDescription { get; set; } = null!;
    public string? CallGoal { get; set; }
}
