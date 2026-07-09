using System.ComponentModel.DataAnnotations;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class CompanyCallContextDto
{
    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = null!;

    [Required]
    [MaxLength(8000)]
    public string CompanyDescription { get; set; } = null!;

    [MaxLength(500)]
    public string? CallGoal { get; set; }
}
