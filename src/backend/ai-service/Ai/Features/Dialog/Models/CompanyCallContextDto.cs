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

    /// <summary>Optional buyer persona to role-play as (39.14). All null/omitted when no persona
    /// is selected for the call.</summary>
    [MaxLength(200)]
    public string? PersonaName { get; set; }

    [MaxLength(200)]
    public string? PersonaPosition { get; set; }

    [MaxLength(4000)]
    public string? PersonaPersonality { get; set; }

    [MaxLength(16)]
    public string? PersonaDifficulty { get; set; }
}
