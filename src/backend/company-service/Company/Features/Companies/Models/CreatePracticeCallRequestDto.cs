using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreatePracticeCallRequestDto(
    [Required] string DialogSessionId,
    [Required][MaxLength(1000)] string Goal);
