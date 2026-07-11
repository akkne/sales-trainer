using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreatePracticeCallRequestDto(
    [Required][MaxLength(100)] string DialogSessionId,
    [MaxLength(1000)] string? Goal = null);
